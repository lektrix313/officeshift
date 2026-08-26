using Godot;
using System.Collections.Generic;

/// <summary>One mail message in the OmniPortal system.</summary>
public sealed record MailMsg(string From, string To, string Subject, string Body, float SentAt, bool FromPlayer);

/// <summary>Static mail store: inbox + sent, with arrival events for UI refresh.</summary>
public static class MailStore
{
    public static readonly List<MailMsg> All = new();
    public static event Action<MailMsg>? MailArrived;

    public const string PlayerAddress = "You";

    public static void Send(MailMsg msg)
    {
        All.Add(msg);
        MailArrived?.Invoke(msg);
    }

    public static IEnumerable<MailMsg> Inbox => All.FindAll(m => m.To == PlayerAddress);

    public static IEnumerable<MailMsg> Sent => All.FindAll(m => m.From == PlayerAddress);
}
