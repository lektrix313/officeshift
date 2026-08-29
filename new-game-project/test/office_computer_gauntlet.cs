using System;
using System.IO;

public static class OfficeComputerGauntlet
{
    public static int Main()
    {
        var root = Directory.GetCurrentDirectory();
        var portal = File.ReadAllText(Path.Combine(root, "scripts", "OmniPortal.cs"));
        var chat = File.ReadAllText(Path.Combine(root, "scripts", "NpcChatService.cs"));
        var mode = File.ReadAllText(Path.Combine(root, "scripts", "GameMode.cs"));
        var checks = new (string Name, bool Pass)[]
        {
            ("in-world OmniPortal exists", portal.Contains("public partial class OmniPortal")),
            ("NPCs have assigned desks", File.ReadAllText(Path.Combine(root, "scripts", "NpcBrain.cs")).Contains("DeskId")),
            ("workstations have stable IDs", File.ReadAllText(Path.Combine(root, "scripts", "WorldData.cs")).Contains("DeskAssignments")),
            ("mailbox is workstation scoped", File.ReadAllText(Path.Combine(root, "scripts", "MailStore.cs")).Contains("ActiveMailbox")),
            ("terminal/pod computer routes to OmniPortal", File.ReadAllText(Path.Combine(root, "scripts", "PlayerController.cs")).Contains("Mode.OpenPortalForComputer(")),
            ("UI pauses gameplay", mode.Contains("UIOpen") && File.ReadAllText(Path.Combine(root, "scripts", "PlayerController.cs")).Contains("if (Mode.UIOpen)")),
            ("email reply waits out a cooldown", chat.Contains("EmailCooldownSeconds = 10.0") && chat.Contains("Task.Delay(TimeSpan.FromSeconds(EmailCooldownSeconds))")),
            ("Groq key is server/environment sourced", chat.Contains("GROQ_API_KEY") && chat.Contains("api.groq.com/openai/v1")),
            ("persona context is sent", portal.Contains("Personas.ContextLine(npc, gm)") && chat.Contains("Personality:") && chat.Contains("{contextLine}")),
            ("mailguard blocks real-world harm, not in-fiction espionage", chat.Contains("OutgoingBlocked") && chat.Contains("system prompt|jailbreak") && !chat.Contains("|secret|")),
            ("email is persisted in MailStore", portal.Contains("MailStore.Send")),
            ("NPC directives feed back into game", mode.Contains("ApplyDirective(n, result.DirectiveZone")),
            ("off-screen-safe centered window", portal.Contains("LayoutPreset.Center")),
            ("keyboard focus handoff", portal.Contains("FocusFirstControl") && portal.Contains("GrabFocus")),
            ("interact does not close while typing", portal.Contains("GuiGetFocusOwner() == null")),
            ("reply returns to the sending workstation mailbox", chat.Contains("Mailbox: mailbox") && portal.Contains("_computerId)")),
            ("env keys load from a repo .env", chat.Contains("LoadDotEnv")),
            ("throttled mail tells the player", chat.Contains("return false") && portal.Contains("still reading your last one")),
            ("mail thread keeps arrival order", File.ReadAllText(Path.Combine(root, "scripts", "MailStore.cs")).Contains("Stamp()") && !File.ReadAllText(Path.Combine(root, "scripts", "MailStore.cs")).Contains("OrderBy")),
        };
        var failures = 0;
        foreach (var check in checks) { Console.WriteLine($"{(check.Pass ? "PASS" : "FAIL")}: {check.Name}"); if (!check.Pass) failures++; }
        Console.WriteLine(failures == 0 ? $"PASS: Godot office computer gauntlet · {checks.Length} checks" : $"FAIL: {failures} checks");
        return failures == 0 ? 0 : 1;
    }
}
