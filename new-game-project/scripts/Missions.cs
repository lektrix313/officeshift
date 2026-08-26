using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Mission contract system (MISSION_BIBLE grammar, lite): VERB + ASSET +
/// LOCATION + objectives. Contracts are DATA — drop a JSON file into
/// missions/ (res:// or user://) and it appears on the contract board.
/// Every objective type admits multiple solution routes by design:
///   STEAL_BLUEPRINTS — ghost it, Sales-uniform it, or brute force
///   PHOTO_WHITEBOARD — any time, but the break room is watched
///   LURE_NPC         — chat directive, email reply, or desk-phone lure
///   GHOST            — end the shift with max suspicion under 30
/// </summary>
public sealed record MissionContract(
    string Id,
    string Title,
    string Brief,
    string WinLine,
    List<MissionObjective> Objectives)
{
    public static MissionContract Default() => new(
        "ORIENTATION",
        "Orientation — Steal the Blueprints",
        "Infiltrate the server room, steal the blueprints, mail them out via the mail trolley.",
        "Blueprint delivered. OmniCore never stood a chance.",
        new List<MissionObjective> { new("STEAL_BLUEPRINTS") });
}

public sealed record MissionObjective(string Type, string Npc = "", string Zone = "");

/// <summary>Zone rectangles for LURE objective checks (id → XZ box).</summary>
public static class MissionZones
{
    public static readonly Dictionary<string, (float MinX, float MinZ, float MaxX, float MaxZ)> Rects = new()
    {
        ["server"] = (-32f, -22f, -12f, -12f),
        ["breakroom"] = (12f, -22f, 32f, -8f),
        ["printer"] = (-32f, -12f, -22f, -2f),
        ["reception"] = (-32f, 14f, 32f, 22f),
        ["closet"] = (22f, 8f, 32f, 13.5f),
    };

    public static bool Contains(string zone, Vector3 pos)
    {
        if (!Rects.TryGetValue(zone, out var r)) return false;
        return pos.X >= r.MinX && pos.X <= r.MaxX && pos.Z >= r.MinZ && pos.Z <= r.MaxZ;
    }

    private static readonly string[] ObjectiveTypes =
    {
        "STEAL_BLUEPRINTS", "PHOTO_WHITEBOARD", "LURE_NPC", "GHOST", "KNOCKOUT_NPC",
    };

    public static bool ValidObjectiveType(string t) => System.Array.IndexOf(ObjectiveTypes, t) >= 0;
}

public static class MissionManager
{
    public static MissionContract Active { get; private set; } = MissionContract.Default();
    public static List<MissionContract> Loaded { get; } = new();
    public static event Action? ContractsReloaded;

    public static void LoadAll()
    {
        Loaded.Clear();
        Loaded.Add(MissionContract.Default());

        foreach (var contract in LoadDir("res://missions")) Loaded.Add(contract);
        foreach (var contract in LoadDir("user://missions")) Loaded.Add(contract);
        ContractsReloaded?.Invoke();
    }

    private static IEnumerable<MissionContract> LoadDir(string dir)
    {
        var found = new List<MissionContract>();
        if (!DirAccess.DirExistsAbsolute(dir)) return found;
        foreach (var file in DirAccess.GetFilesAt(dir))
        {
            if (!file.EndsWith(".json")) continue;
            var path = $"{dir}/{file}";
            try
            {
                var json = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)?.GetAsText();
                if (string.IsNullOrWhiteSpace(json)) continue;
                var contract = Parse(json, file);
                if (contract != null) found.Add(contract);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Missions] failed to parse {path}: {ex.Message}");
            }
        }
        return found;
    }

    public static MissionContract? Parse(string json, string sourceName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string id = root.GetProperty("id").GetString() ?? sourceName;
        string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? id : id;
        string brief = root.TryGetProperty("brief", out var b) ? b.GetString() ?? "" : "";
        string win = root.TryGetProperty("winLine", out var w) ? w.GetString() ?? "Contract complete." : "Contract complete.";

        var objectives = new List<MissionObjective>();
        foreach (var o in root.GetProperty("objectives").EnumerateArray())
        {
            string type = o.GetProperty("type").GetString() ?? "";
            string npc = o.TryGetProperty("npc", out var n) ? n.GetString() ?? "" : "";
            string zone = o.TryGetProperty("zone", out var z) ? z.GetString() ?? "" : "";
            if (MissionZones.ValidObjectiveType(type)) objectives.Add(new MissionObjective(type, npc, zone));
            else GD.PushWarning($"[Missions] {sourceName}: unknown objective type '{type}' skipped");
        }
        if (objectives.Count == 0) return null;
        return new MissionContract(id, title, brief, win, objectives);
    }

    public static void Accept(MissionContract contract) => Active = contract;

    /// <summary>Composer support: serialize a contract to JSON and save into user://missions/.</summary>
    public static string SaveUserContract(MissionContract contract)
    {
        DirAccess.MakeDirRecursiveAbsolute("user://missions");
        var objectives = new List<object>();
        foreach (var o in contract.Objectives)
        {
            var entry = new Dictionary<string, string> { ["type"] = o.Type };
            if (o.Npc.Length > 0) entry["npc"] = o.Npc;
            if (o.Zone.Length > 0) entry["zone"] = o.Zone;
            objectives.Add(entry);
        }
        var payload = new Dictionary<string, object>
        {
            ["id"] = contract.Id,
            ["title"] = contract.Title,
            ["brief"] = contract.Brief,
            ["winLine"] = contract.WinLine,
            ["objectives"] = objectives,
        };
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        string path = $"user://missions/{contract.Id}.json";
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(json);
        LoadAll();
        return path;
    }
}
