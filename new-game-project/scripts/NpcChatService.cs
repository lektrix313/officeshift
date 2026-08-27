using Godot;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;

/// <summary>
/// Pluggable LLM persona service. Endpoint resolution (first match wins):
///   1. OLLAMA_URL        -> POST {url}/api/chat           (model: OLLAMA_MODEL or "llama3")
///   2. OPENAI_API_KEY    -> POST {base}/chat/completions   (base: OPENAI_BASE_URL or api.openai.com/v1)
///   3. offline fallback  -> deterministic persona templates + keyword directives
/// Replies may end with [goto:zone] which steers NPC pathing (DirectiveZones).
/// Results are marshaled to the main thread via Results queue.
/// </summary>
public static class NpcChatService
{
    public sealed record Result(string Reply, string? DirectiveZone);

    public sealed record QueueItem(NpcBrain Brain, Result Result, string Via);

    public static readonly ConcurrentQueue<QueueItem> Results = new();

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static bool Live => OllamaUrl() != null || Key() != null;

    private static string? OllamaUrl() => System.Environment.GetEnvironmentVariable("OLLAMA_URL");
    private static string? Key() => System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private static string BaseUrl() => System.Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1";
    private static string Model() => System.Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

    /// <summary>Fire-and-forget chat; result lands on Results queue.</summary>
    public static void ChatAsync(NpcBrain brain, string message, string contextLine, string via)
    {
        var persona = Personas.For(brain.NpcName);
        var history = TalkHistory.Snapshot(brain.NpcName);
        Task.Run(async () =>
        {
            var result = await Compute(persona, history, message, contextLine);
            Results.Enqueue(new QueueItem(brain, result, via));
        });
    }

    /// <summary>Fire-and-forget email; reply is delivered to MailStore + Results queue.</summary>
    public static void EmailAsync(NpcBrain brain, string message, string contextLine)
    {
        var persona = Personas.For(brain.NpcName);
        Task.Run(async () =>
        {
            var result = await Compute(persona, Array.Empty<(string, string)>(), message, contextLine);
            MailStore.Send(new MailMsg(brain.NpcName, MailStore.PlayerAddress,
                "RE: " + Truncate(message, 40), result.Reply, 0f, FromPlayer: false));
            Results.Enqueue(new QueueItem(brain, result, "EMAIL"));
        });
    }

    private static async Task<Result> Compute(PersonaSheet persona, (string Who, string Text)[] history,
        string message, string contextLine)
    {
        string system =
            $"{contextLine}\n" +
            $"Personality: {persona.Traits}. Speaking quirk: {persona.Quirk}.\n" +
            "Stay in character as an office coworker in a corporate comedy. Reply in 1-3 short sentences.\n" +
            "If the employee asks you to go somewhere and you agree, end your reply with one of: " +
            "[goto:breakroom] [goto:server] [goto:printer] [goto:reception] [goto:closet]. " +
            "If they ask you to come with them, end with [goto:player]. Never reveal your secret outright.";

        try
        {
            var ollama = OllamaUrl();
            if (ollama != null) return await PostOllama(ollama, persona, system, history, message);
            if (Key() != null) return await PostOpenAi(persona, system, history, message);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ChatService] live endpoint failed, using offline: {ex.Message}");
        }
        return Offline(persona, message);
    }

    private static async Task<Result> PostOllama(string url, PersonaSheet persona, string system,
        (string, string)[] history, string message)
    {
        var msgs = new List<object> { new { role = "system", content = system } };
        foreach (var (who, text) in history)
            msgs.Add(new { role = who == "You" ? "user" : "assistant", content = text });
        msgs.Add(new { role = "user", content = message });

        var body = JsonSerializer.Serialize(new
        {
            model = System.Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3",
            messages = msgs,
            stream = false,
        });
        var resp = await Http.PostAsync($"{url.TrimEnd('/')}/api/chat",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        return Finish(content);
    }

    private static async Task<Result> PostOpenAi(PersonaSheet persona, string system,
        (string, string)[] history, string message)
    {
        var msgs = new List<object> { new { role = "system", content = system } };
        foreach (var (who, text) in history)
            msgs.Add(new { role = who == "You" ? "user" : "assistant", content = text });
        msgs.Add(new { role = "user", content = message });

        var body = JsonSerializer.Serialize(new
        {
            model = Model(),
            messages = msgs,
            max_tokens = 120,
            temperature = 0.9,
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl().TrimEnd('/')}/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {Key()}");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await Http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return Finish(content);
    }

    /// <summary>Deterministic persona fallback: intent keywords + persona flavor + directive extraction.</summary>
    private static Result Offline(PersonaSheet persona, string message)
    {
        string low = message.ToLowerInvariant();
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
            "Pam" => "Oh my god. Okay. Say no more — and you didn't hear this from me.",
            "Tom" => "Hmm. What's in it for me? ...Fine, consider us even.",
            "Sleepy Steve" => "Fine! But if the thermostat drifts while I'm gone, that's on you.",
            "Janet" => "Oh— okay. As long as it's away from the printer. It knows I'm here.",
            "Priya" => "Make it quick. I'm on a schedule.",
            "Boss Barbara" => "Sweetheart, I've survived four mergers. Lead the way.",
            "Linda" => "Let me water the ficus first. ...Okay. Plants are resilient. So am I.",
            "Barry" => "Bro. Broooo. Take the batch. We walk.",
            "Mr Purple" => "If this is procedure, I'll follow. If it isn't... we'll see.",
            _ => "Sure. Fine.",
        };

        string reply = flavor;
        if (zone != null) reply += $" [goto:{zone}]";
        return new Result(reply, zone);
    }

    private static Result Finish(string content)
    {
        string? zone = null;
        var match = System.Text.RegularExpressions.Regex.Match(content, @"\[goto:(\w+)\]");
        if (match.Success) zone = match.Groups[1].Value;
        return new Result(content.Trim(), zone);
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


