using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>One mail message in the OmniPortal system.</summary>
public sealed record MailMsg(string From, string To, string Subject, string Body, float SentAt, bool FromPlayer, string Mailbox = "company");

/// <summary>Static mail store: inbox + sent, with arrival events for UI refresh.</summary>
public static class MailStore
{
    public static readonly List<MailMsg> All = new();
    public static event Action<MailMsg>? MailArrived;

    /// <summary>NPC replies land here from a worker thread while the UI reads on the main thread.</summary>
    private static readonly object Lock = new();

    public const string PlayerAddress = "You";
    public static string ActiveMailbox { get; set; } = "company";

    /// <summary>Shared clock for SentAt so player mail and NPC replies interleave correctly.</summary>
    public static float Stamp() => (float)(Time.GetTicksMsec() / 1000.0);

    /// <summary>
    /// The thread between the player and one NPC in one mailbox, in arrival order.
    /// Insertion order is already chronological. Sorting on SentAt used to bunch every player
    /// mail ahead of every reply, which handed the LLM a scrambled transcript.
    /// </summary>
    public static MailMsg[] Conversation(string npcName, string mailbox = "company")
    {
        lock (Lock)
            return All.Where(m => m.Mailbox == mailbox
                && ((m.From == npcName && m.To == PlayerAddress) || (m.From == PlayerAddress && m.To == npcName)))
                .ToArray();
    }

    public static void Send(MailMsg msg)
    {
        lock (Lock) All.Add(msg);
        MailArrived?.Invoke(msg);
    }

    public static IEnumerable<MailMsg> Inbox
    {
        get { lock (Lock) return All.Where(m => m.To == PlayerAddress && m.Mailbox == ActiveMailbox).ToArray(); }
    }

    public static IEnumerable<MailMsg> Sent
    {
        get { lock (Lock) return All.Where(m => m.From == PlayerAddress && m.Mailbox == ActiveMailbox).ToArray(); }
    }
}
