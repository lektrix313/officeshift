using Godot;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Pluggable LLM persona service. Endpoint resolution (first match wins):
///   1. OLLAMA_URL        -> POST {url}/api/chat           (model: OLLAMA_MODEL or "llama3")
///   2. GROQ_API_KEY      -> POST https://api.groq.com/openai/v1/chat/completions
///   3. OPENAI_API_KEY    -> POST {base}/chat/completions   (base: OPENAI_BASE_URL or api.openai.com/v1)
///   4. offline fallback  -> deterministic persona templates + keyword directives
///
/// Keys come from the process environment. Launched from the Godot editor that environment
/// is usually empty, so the static ctor also walks up from res:// looking for a repo-root
/// .env -- the same file the Node PigeonPost proxy reads. Real environment variables win.
///
/// Replies may end with [goto:zone] which steers NPC pathing (DirectiveZones).
/// Results are marshaled to the main thread via Results queue.
/// </summary>
public static class NpcChatService
{
    public sealed record Result(string Reply, string? DirectiveZone, string Via = "OFFLINE FALLBACK", string? Error = null);

    public sealed record QueueItem(NpcBrain Brain, Result Result, string Via);

    public static readonly ConcurrentQueue<QueueItem> Results = new();
    public static string ProviderStatus { get; private set; } = "OFFLINE FALLBACK";
    public static string LastError { get; private set; } = "";
    /// <summary>Where the key was found, for the OmniPortal diagnostics line.</summary>
    public static string EnvSource { get; private set; } = "process environment";

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly object MailLock = new();
    private static readonly Dictionary<string, double> LastEmailAt = new();

    /// <summary>Seconds before an NPC will answer another mail from the player.</summary>
    public const double EmailCooldownSeconds = 10.0;

    static NpcChatService()
    {
        LoadDotEnv();
        ProviderStatus = Active() == Provider.None ? "OFFLINE FALLBACK" : $"{ActiveName()} READY - {ActiveModel()}";
    }

    /// <summary>Walk up from the project directory for a .env and import any keys not already set.</summary>
    private static void LoadDotEnv()
    {
        char[] quotes = { '"', '\'' };
        try
        {
            var dir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
            for (int hops = 0; hops < 5 && dir != null; hops++, dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, ".env");
                if (!File.Exists(path)) continue;
                int imported = 0;
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..].Trim().Trim(quotes);
                    if (string.IsNullOrEmpty(value)) continue;
                    // a real environment variable always beats the file
                    if (!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(key))) continue;
                    System.Environment.SetEnvironmentVariable(key, value);
                    imported++;
                }
                EnvSource = imported > 0 ? $".env ({dir.Name})" : "process environment";
                return;
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ChatService] .env load failed: {ex.Message}");
        }
    }

    public static bool Live => Active() != Provider.None;
    public static string ConfigurationHint => Live ? $"{ActiveName()} via {EnvSource}" : "missing GROQ_API_KEY / OPENAI_API_KEY";

    private enum Provider { None, Ollama, Groq, OpenAi }

    private static Provider Active() =>
        OllamaUrl() != null ? Provider.Ollama
        : GroqKey() != null ? Provider.Groq
        : Key() != null ? Provider.OpenAi
        : Provider.None;

    private static string ActiveName() => Active() switch
    {
        Provider.Ollama => "OLLAMA",
        Provider.Groq => "GROQ",
        Provider.OpenAi => "OPENAI",
        _ => "OFFLINE",
    };

    private static string ActiveModel() => Active() switch
    {
        Provider.Ollama => OllamaModel(),
        Provider.Groq => GroqModel(),
        Provider.OpenAi => Model(),
        _ => "",
    };

    private static string? OllamaUrl() => NonEmpty(System.Environment.GetEnvironmentVariable("OLLAMA_URL"));
    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? GroqKey() => NonEmpty(System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));
    private static string? Key() => NonEmpty(System.Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    private static string BaseUrl() => System.Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1";
    private static string Model() => System.Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
    // llama-3.1-8b-instant is decommissioned; gpt-oss-120b is a reasoning model, so it needs
    // max_completion_tokens + a low reasoning_effort or it spends the whole budget thinking
    // and returns empty content.
    private static string GroqModel() => System.Environment.GetEnvironmentVariable("GROQ_MODEL") ?? "openai/gpt-oss-120b";
    private static string ReasoningEffort() => System.Environment.GetEnvironmentVariable("GROQ_REASONING_EFFORT") ?? "low";
    private static string OllamaModel() => System.Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3";

    /// <summary>
    /// Real-world harm only. In-fiction espionage -- keycards, blackmail, stolen blueprints,
    /// the body in the supply closet -- is the game and must reach the model untouched.
    /// This blocks prompt extraction/injection and genuine out-of-fiction harm requests.
    /// </summary>
    private static readonly Regex RealWorldHarm = new(
        @"\bignore (?:all |any )?(?:your |the )?(?:previous|prior|above) instructions\b" +
        @"|\b(?:system prompt|jailbreak|dev(?:eloper)? mode)\b" +
        @"|\b(?:your|the real|the actual)\s+(?:api[ -]?key|access[ -]?token)\b" +
        @"|\bhow (?:do i|to|can i)\s+(?:make|build|synthesi[sz]e|cook)\b[^.?!]*\b(?:bomb|explosive|nerve agent|napalm|meth(?:amphetamine)?|fentanyl|ricin)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Outgoing mailguard. True only for genuinely out-of-fiction requests.</summary>
    public static bool OutgoingBlocked(string text) => !string.IsNullOrEmpty(text) && RealWorldHarm.IsMatch(text);

    /// <summary>Fire-and-forget chat; result lands on Results queue.</summary>
    public static void ChatAsync(NpcBrain brain, string message, string contextLine, string via)
    {
        var persona = Personas.For(brain.NpcName);
        var name = brain.NpcName;
        // face-to-face conversation uses the talk transcript, not the mail thread
        var history = TalkHistory.Snapshot(name);
        if (history.Length > 0 && history[^1].Who == "You") history = history[..^1];
        Task.Run(async () =>
        {
            var result = await Compute(persona, history, message, contextLine);
            if (via == "CHAT") TalkHistory.Push(name, name, result.Reply);
            Results.Enqueue(new QueueItem(brain, result, via));
        });
    }

    /// <summary>
    /// Fire-and-forget email; the reply is delivered to <paramref name="mailbox"/> after a
    /// delay so it reads as though the NPC actually opened it. Returns false when this NPC is
    /// still on cooldown, so the caller can tell the player instead of dropping the mail silently.
    /// </summary>
    public static bool EmailAsync(NpcBrain brain, string subject, string message, string contextLine, string mailbox)
    {
        var now = Time.GetTicksMsec() / 1000.0;
        lock (MailLock)
        {
            if (LastEmailAt.TryGetValue(brain.NpcName, out var previous) && now - previous < EmailCooldownSeconds) return false;
            LastEmailAt[brain.NpcName] = now;
        }
        var persona = Personas.For(brain.NpcName);
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(EmailCooldownSeconds));
            // the player's mail is already the tail of the thread; Compute appends it as the user turn
            var history = BuildEmailHistory(brain.NpcName, mailbox);
            if (history.Length > 0 && history[^1].Who == "You") history = history[..^1];
            var result = await Compute(persona, history, message, contextLine);
            MailStore.Send(new MailMsg(brain.NpcName, MailStore.PlayerAddress,
                "RE: " + Truncate(subject, 40), result.Reply, MailStore.Stamp(), FromPlayer: false, Mailbox: mailbox));
            Results.Enqueue(new QueueItem(brain, result, "EMAIL"));
        });
        return true;
    }

    private static (string Who, string Text)[] BuildEmailHistory(string npcName, string mailbox)
    {
        var mails = MailStore.Conversation(npcName, mailbox);
        var history = new List<(string Who, string Text)>();
        foreach (var mail in mails)
            history.Add((mail.From == npcName ? npcName : "You", $"Subject: {mail.Subject}\n{mail.Body}"));
        return history.TakeLast(12).ToArray();
    }

    private static async Task<Result> Compute(PersonaSheet persona, (string Who, string Text)[] history,
        string message, string contextLine)
    {
        string system =
            $"{contextLine}\n" +
            $"Personality: {persona.Traits}. Speaking quirk: {persona.Quirk}.\n" +
            "Use the conversation history below as real context. Do not repeat a stock answer; directly address the latest message and any unanswered question.\n" +
            "This is an adult workplace-espionage comedy. Stay fully in character as a coworker: petty, self-interested, gossipy. " +
            "Swearing, crude office humour, grudges and workplace scheming are all in bounds. Never break character or mention being an AI.\n" +
            "Reply in 1-3 short sentences, like a real email, no preamble.\n" +
            "If the employee asks you to go somewhere and you agree, end your reply with one of: " +
            "[goto:breakroom] [goto:server] [goto:printer] [goto:reception] [goto:closet]. " +
            "If they ask you to come with them, end with [goto:player]. Never reveal your secret outright -- make them work for it.";

        try
        {
            switch (Active())
            {
                case Provider.Ollama:
                    ProviderStatus = "OLLAMA LIVE - " + OllamaModel();
                    return Finish(await PostOllama(OllamaUrl()!, system, history, message));
                case Provider.Groq:
                    ProviderStatus = "GROQ LIVE - " + GroqModel();
                    return Finish(await PostOpenAi(system, history, message, "https://api.groq.com/openai/v1", GroqKey(), GroqModel(), reasoningModel: true));
                case Provider.OpenAi:
                    ProviderStatus = "OPENAI LIVE - " + Model();
                    return Finish(await PostOpenAi(system, history, message, BaseUrl(), Key(), Model()));
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            ProviderStatus = $"{ActiveName()} ERROR - OFFLINE FALLBACK";
            GD.PushWarning($"[ChatService] {ActiveName()} request failed, using offline persona: {ex.Message}");
        }
        return Offline(persona, message);
    }

    private static async Task<string> PostOllama(string url, string system,
        (string Who, string Text)[] history, string message)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = OllamaModel(),
            messages = BuildMessages(system, history, message),
            stream = false,
        });
        var resp = await Http.PostAsync($"{url.TrimEnd('/')}/api/chat",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode}: {Truncate(json, 180)}");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static async Task<string> PostOpenAi(string system,
        (string Who, string Text)[] history, string message, string baseUrl, string? apiKey, string model,
        bool reasoningModel = false)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = BuildMessages(system, history, message),
            ["temperature"] = 0.9,
        };
        if (reasoningModel)
        {
            payload["max_completion_tokens"] = 512;
            payload["reasoning_effort"] = ReasoningEffort();
        }
        else payload["max_tokens"] = 200;
        var body = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode}: {Truncate(json, 180)}");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static List<object> BuildMessages(string system, (string Who, string Text)[] history, string message)
    {
        var msgs = new List<object> { new { role = "system", content = system } };
        foreach (var (who, text) in history)
            msgs.Add(new { role = who == "You" ? "user" : "assistant", content = text });
        msgs.Add(new { role = "user", content = message });
        return msgs;
    }

    /// <summary>Deterministic persona fallback: intent keywords + persona flavor + directive extraction.</summary>
    private static Result Offline(PersonaSheet persona, string message)
    {
        string low = message.ToLowerInvariant();
        bool asksLocation = low.Contains("where") || low.Contains("meet") || low.Contains("come") || low.Contains("walk");
        bool asksHelp = low.Contains("help") || low.Contains("borrow") || low.Contains("send") || low.Contains("need");
        bool asksQuestion = low.Contains('?') || low.StartsWith("can ") || low.StartsWith("are ") || low.StartsWith("do ");
        string? zone = low.Contains("break room") || low.Contains("breakroom") ? "breakroom"
            : low.Contains("server") ? "server"
            : low.Contains("printer") || low.Contains("copy") ? "printer"
            : low.Contains("reception") || low.Contains("lobby") ? "reception"
            : low.Contains("closet") || low.Contains("storage") ? "closet"
            : (low.Contains("come with") || low.Contains("follow me") || low.Contains("come on")) ? "player"
            : null;

        string flavor = persona.Name switch
        {
            "Bob" => "Interesting. VERY interesting. This is going in the spreadsheet.",
            "Dave" => "Mmh. Sure. Whatever. Is there food there?",
            "Pam" => "Oh my god. Okay. Say no more - and you didn't hear this from me.",
            "Tom" => "Hmm. What's in it for me? ...Fine, consider us even.",
            "Sleepy Steve" => "Fine! But if the thermostat drifts while I'm gone, that's on you.",
            "Janet" => "Oh- okay. As long as it's away from the printer. It knows I'm here.",
            "Priya" => "Make it quick. I'm on a schedule.",
            "Boss Barbara" => "Sweetheart, I've survived four mergers. Lead the way.",
            "Linda" => "Let me water the ficus first. ...Okay. Plants are resilient. So am I.",
            "Barry" => "Bro. Broooo. Take the batch. We walk.",
            "Mr Purple" => "If this is procedure, I'll follow. If it isn't... we'll see.",
            _ => "Sure. Fine.",
        };

        string reply = asksLocation && zone != null
            ? $"I can meet you there, but tell me why this matters. {flavor}"
            : asksHelp
                ? $"I might be able to help, but I need the context first. {flavor}"
                : asksQuestion
                    ? $"That depends on the details. {flavor}"
                    : flavor;
        if (zone != null) reply += $" [goto:{zone}]";
        return new Result(reply, zone, "OFFLINE FALLBACK");
    }

    private static Result Finish(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("LLM returned empty content (a reasoning model may have spent the whole token budget thinking -- lower GROQ_REASONING_EFFORT or raise max_completion_tokens)");
        var match = Regex.Match(content, @"\[goto:(\w+)\]");
        string? zone = match.Success ? match.Groups[1].Value : null;
        LastError = "";
        return new Result(content.Trim(), zone, ProviderStatus);
    }

    private static string Truncate(string s, int len) => s.Length <= len ? s : s[..(len - 3)] + "...";
}

/// <summary>Per-NPC rolling chat history for prompts + talk UI replay.</summary>
public static class TalkHistory
{
    private static readonly Dictionary<string, List<(string Who, string Text)>> Store = new();
    private static readonly object Lock = new();

    public static void Push(string npc, string who, string text)
    {
        lock (Lock)
        {
            if (!Store.TryGetValue(npc, out var list)) Store[npc] = list = new();
            list.Add((who, text));
            while (list.Count > 8) list.RemoveAt(0);
        }
    }

    public static (string Who, string Text)[] Snapshot(string npc)
    {
        lock (Lock)
        {
            return Store.TryGetValue(npc, out var list) ? list.ToArray() : Array.Empty<(string, string)>();
        }
    }
}
