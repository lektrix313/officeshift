using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Keycard IDs follow the pattern: "dept-level" or "role".
/// Examples: "janitorial", "gary-level-3", "it-systems", "hr-confidential",
/// "executive-override", "maintenance", "procurement", "reception-visitor".
/// </summary>
public static class KeycardCatalog
{
    public static readonly IReadOnlyDictionary<string, KeycardDef> All = new Dictionary<string, KeycardDef>(StringComparer.OrdinalIgnoreCase)
    {
        ["janitorial"]       = new("janitorial",       "Janitorial Master Key",     "Joe's master key. Opens maintenance closets, incinerator, and back corridors.",           "janitorial"),
        ["gary-level-3"]     = new("gary-level-3",     "Level 3 Finance Access",    "Gary's old finance badge. Opens the server terminal and accounts vault.",                    "accounting"),
        ["it-systems"]       = new("it-systems",       "IT Systems Badge",          "Sleepy Steve's admin badge. Opens the server room and network closet.",                      "it"),
        ["hr-confidential"]  = new("hr-confidential",  "HR Confidential Access",    "Pam's restricted badge. Opens HR files and the disciplinary archive.",                       "hr"),
        ["executive-override"] = new("executive-override", "Executive Override",     "Mr Purple's master override. Opens everything on the executive floor.",                     "executive"),
        ["maintenance"]      = new("maintenance",      "Maintenance Pass",          "Basic maintenance access. Opens utility rooms and stairwells.",                             "facilities"),
        ["procurement"]      = new("procurement",      "Procurement Access",        "Kevin's inventory badge. Opens the stockroom and loading dock.",                            "procurement"),
        ["reception-visitor"] = new("reception-visitor", "Reception Visitor Pass",    "A temporary visitor badge. Opens the lobby and meeting rooms.",                            "reception"),
        ["security-camera"]  = new("security-camera",  "Security Camera Access",    "Ned's camera badge. Opens the security office and camera system.",                         "security"),
    };

    public static KeycardDef? Find(string id) =>
        All.TryGetValue(id, out var def) ? def : null;

    /// <summary>Which keycards can access a given floor (for default layout).</summary>
    public static string[] KeycardsForFloor(string floorId) => floorId switch
    {
        "floor-2" => new[] { "executive-override", "maintenance" },
        _ => Array.Empty<string>(),
    };
}

public sealed record KeycardDef(string Id, string DisplayName, string Description, string Department);

/// <summary>
/// Mutable keycard inventory for the player. Tracks which keycards have been collected,
/// provides the best keycard for a given access requirement, and supports steal/pickup/drop.
/// </summary>
public sealed class KeycardInventory
{
    private readonly HashSet<string> _cards = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Cards => _cards;
    public int Count => _cards.Count;

    public bool Has(string keycardId) => _cards.Contains(keycardId);

    public bool Add(string keycardId)
    {
        if (_cards.Add(keycardId))
        {
            var def = KeycardCatalog.Find(keycardId);
            return true;
        }
        return false; // already have it
    }

    public bool Remove(string keycardId) => _cards.Remove(keycardId);

    /// <summary>
    /// Returns the best keycard the player owns that matches a required keycard ID.
    /// Exact match wins, then department match, then any executive override.
    /// Returns null if the player has nothing useful.
    /// </summary>
    public string? BestMatch(string? requiredKeyId)
    {
        if (string.IsNullOrEmpty(requiredKeyId)) return _cards.Count > 0 ? _cards.First() : null;

        // exact match
        if (_cards.Contains(requiredKeyId)) return requiredKeyId;

        // check if any owned card can override (executive override opens most things)
        if (_cards.Contains("executive-override")) return "executive-override";

        // department match: if the required card's department matches an owned card's department
        var requiredDef = KeycardCatalog.Find(requiredKeyId);
        if (requiredDef != null)
        {
            foreach (var owned in _cards)
            {
                var ownedDef = KeycardCatalog.Find(owned);
                if (ownedDef != null && ownedDef.Department.Equals(requiredDef.Department, StringComparison.OrdinalIgnoreCase))
                    return owned;
            }
        }

        return null;
    }

    /// <summary>Check if the player can access something that requires a given keycard.</summary>
    public bool CanAccess(string? requiredKeyId) => BestMatch(requiredKeyId) != null;
}

/// <summary>
/// Each NPC can also hold keycards. When knocked out or pickpocketed, their cards transfer to the player.
/// </summary>
public static class NpcKeycardDrops
{
    /// <summary>
    /// Maps NPC names to the keycards they carry on their person.
    /// These are taken when the NPC is knocked out and the player searches them.
    /// </summary>
    private static readonly Dictionary<string, string[]> Drops = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Joe"]          = new[] { "janitorial" },
        ["Sleepy Steve"] = new[] { "it-systems", "maintenance" },
        ["Pam"]          = new[] { "hr-confidential" },
        ["Mr Purple"]    = new[] { "executive-override" },
        ["Kevin"]        = new[] { "procurement" },
        ["Rita"]         = new[] { "reception-visitor" },
        ["Nervous Ned"]  = new[] { "security-camera" },
        ["Fran"]         = new[] { "gary-level-3" },
        ["Manager Mo"]   = new[] { "maintenance" },
        ["Jen"]          = new[] { "reception-visitor", "hr-confidential" },
        ["Boss Barbara"] = new[] { "executive-override" },
        ["Data Dave"]    = new[] { "it-systems" },
        ["Dave"]         = new[] { "hr-confidential" },
        ["Bob"]          = new[] { "gary-level-3" },
        ["Liz"]          = new[] { "reception-visitor" },
        ["Old Tom"]      = new[] { "executive-override", "janitorial" },
        ["Chad"]         = Array.Empty<string>(),
        ["Boring Bill"]  = Array.Empty<string>(),
        ["Mailroom Mike"]= new[] { "janitorial", "maintenance" },
    };

    /// <summary>Returns the keycards an NPC carries (empty if they have none).</summary>
    public static string[] GetDrops(string npcName) =>
        Drops.TryGetValue(npcName, out var cards) ? cards : Array.Empty<string>();

    /// <summary>Check if a specific NPC carries a specific keycard.</summary>
    public static bool HoldsKeycard(string npcName, string keycardId) =>
        Drops.TryGetValue(npcName, out var cards) && Array.Exists(cards, c => c.Equals(keycardId, StringComparison.OrdinalIgnoreCase));
}
