using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WorkshopLevelData
{
    public string Format { get; private set; } = "";
    public int Version { get; private set; }
    public string Business { get; private set; } = "";
    public List<WorkshopFloorData> Floors { get; } = new();
    public List<WorkshopElementData> Elements { get; } = new();
    public List<WorkshopStaffData> Staff { get; } = new();
    public List<WorkshopWaypointData> Waypoints { get; } = new();
    public List<WorkshopAccessCardData> AccessCards { get; } = new();
    public List<WorkshopFloorLinkData> FloorLinks { get; } = new();
    public bool HasGeometry => Elements.Count > 0;
    public bool HasAuthoredStaff => Staff.Count > 0;

    public static WorkshopLevelData? Load(string path, out string error)
    {
        error = "";
        if (!Godot.FileAccess.FileExists(path)) { error = $"Workshop file not found: {path}"; return null; }
        try
        {
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var parsed = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            var result = new WorkshopLevelData
            {
                Format = parsed.GetValueOrDefault("format", "").AsString(),
                Version = parsed.GetValueOrDefault("version", 0).AsInt32(),
                Business = parsed.GetValueOrDefault("business", "Unnamed business").AsString(),
            };
            if (result.Format != "office-shift-godot-level" || result.Version < 2) { error = "Expected office-shift-godot-level version 2 or newer."; return null; }
            foreach (Variant floorVariant in parsed.GetValueOrDefault("floors", new Godot.Collections.Array()).AsGodotArray())
            {
                var floor = WorkshopFloorData.From(floorVariant.AsGodotDictionary());
                if (!floor.IsValid) { error = "Workshop contains an invalid floor."; return null; }
                result.Floors.Add(floor);
                foreach (Variant elementVariant in floorVariant.AsGodotDictionary().GetValueOrDefault("elements", new Godot.Collections.Array()).AsGodotArray())
                {
                    var element = WorkshopElementData.From(elementVariant.AsGodotDictionary(), floor.Id);
                    if (!element.IsValid) { error = "Workshop contains an invalid element."; return null; }
                    if (!result.Elements.Any(existing => existing.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase))) result.Elements.Add(element);
                }
            }
            foreach (Variant elementVariant in parsed.GetValueOrDefault("elements", new Godot.Collections.Array()).AsGodotArray())
            {
                var element = WorkshopElementData.From(elementVariant.AsGodotDictionary(), "");
                if (!element.IsValid) { error = "Workshop contains an invalid element."; return null; }
                if (!result.Elements.Any(existing => existing.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase))) result.Elements.Add(element);
            }
            foreach (Variant linkVariant in parsed.GetValueOrDefault("floorLinks", new Godot.Collections.Array()).AsGodotArray())
            {
                var link = WorkshopFloorLinkData.From(linkVariant.AsGodotDictionary());
                if (!link.IsValid) { error = "Workshop contains an invalid floor link."; return null; }
                result.FloorLinks.Add(link);
            }
            foreach (Variant cardVariant in parsed.GetValueOrDefault("accessCards", new Godot.Collections.Array()).AsGodotArray())
            {
                var card = WorkshopAccessCardData.From(cardVariant.AsGodotDictionary());
                if (!card.IsValid) { error = "Workshop contains an invalid access card."; return null; }
                result.AccessCards.Add(card);
            }
            foreach (Variant staffVariant in parsed.GetValueOrDefault("staff", new Godot.Collections.Array()).AsGodotArray())
            {
                var staff = WorkshopStaffData.From(staffVariant.AsGodotDictionary());
                if (!staff.IsValid) { error = "Workshop contains an invalid staff assignment."; return null; }
                result.Staff.Add(staff);
            }
            foreach (Variant waypointVariant in parsed.GetValueOrDefault("waypoints", new Godot.Collections.Array()).AsGodotArray())
            {
                var waypoint = WorkshopWaypointData.From(waypointVariant.AsGodotDictionary());
                if (!waypoint.IsValid) { error = "Workshop contains an invalid waypoint."; return null; }
                result.Waypoints.Add(waypoint);
            }
            error = Validate(result);
            return string.IsNullOrEmpty(error) ? result : null;
        }
        catch (Exception exception) { error = $"Workshop JSON parse failed: {exception.Message}"; return null; }
    }

    public static string Validate(WorkshopLevelData data)
    {
        if (data.Staff.Count > CanonicalStaff.CoworkerCount) return "Workshop assigns more staff than the canonical starting roster.";
        if (data.Staff.Select(staff => staff.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Staff.Count) return "Workshop contains duplicate staff names.";
        if (data.Staff.Any(staff => !CanonicalStaff.Assignments.Any(assignment => assignment.Profile.Name.Equals(staff.Name, StringComparison.OrdinalIgnoreCase)))) return "Workshop contains a staff member outside the canonical roster.";
        if (data.Staff.Count(staff => staff.IsExecutiveThreat) > 1) return "Workshop contains more than one executive threat.";
        if (data.Waypoints.Select(waypoint => waypoint.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Waypoints.Count) return "Workshop contains duplicate waypoint IDs.";
        var cardIds = data.AccessCards.Select(card => card.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (data.AccessCards.Any(card => cardIds.Count == 0 || !cardIds.Contains(card.Id))) return "Workshop contains invalid access card IDs.";
        if (data.Elements.Any(element => !string.IsNullOrEmpty(element.AccessCardId) && !cardIds.Contains(element.AccessCardId))) return "Workshop element references a missing access card.";
        if (data.Elements.Select(element => element.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Elements.Count) return "Workshop contains duplicate element IDs.";
        if (data.FloorLinks.Select(link => link.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.FloorLinks.Count) return "Workshop contains duplicate floor link IDs.";
        return "";
    }
}

public sealed class WorkshopFloorData
{
    public string Id { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Width > 0 && Height > 0;
    public static WorkshopFloorData From(Godot.Collections.Dictionary data) => new()
    {
        Id = data.GetValueOrDefault("id", "").AsString(), Name = data.GetValueOrDefault("name", "Unnamed floor").AsString(),
        Width = data.GetValueOrDefault("width", 0).AsInt32(), Height = data.GetValueOrDefault("height", 0).AsInt32(),
    };
}

public sealed class WorkshopFloorLinkData
{
    public string Id { get; private set; } = "";
    public string FromFloor { get; private set; } = "";
    public string ToFloor { get; private set; } = "";
    public string ElementId { get; private set; } = "";
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(FromFloor) && !string.IsNullOrWhiteSpace(ToFloor) && !FromFloor.Equals(ToFloor, StringComparison.OrdinalIgnoreCase);
    public static WorkshopFloorLinkData From(Godot.Collections.Dictionary data) => new()
    {
        Id = data.GetValueOrDefault("id", "").AsString(), FromFloor = data.GetValueOrDefault("fromFloor", "").AsString(), ToFloor = data.GetValueOrDefault("toFloor", "").AsString(), ElementId = data.GetValueOrDefault("elementId", "").AsString(),
    };
}

public sealed class WorkshopAccessCardData
{
    public string Id { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int Level { get; private set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Level >= 0;
    public static WorkshopAccessCardData From(Godot.Collections.Dictionary data) => new()
    {
        Id = data.GetValueOrDefault("id", "").AsString(), Name = data.GetValueOrDefault("name", "Access card").AsString(), Level = data.GetValueOrDefault("level", 1).AsInt32(),
    };
}

public sealed class WorkshopElementData
{
    public string Id { get; private set; } = "";
    public string Type { get; private set; } = "";
    public string Label { get; private set; } = "";
    public string FloorId { get; private set; } = "";
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Width { get; private set; } = 1f;
    public float Height { get; private set; } = 1f;
    public string Room { get; private set; } = "Open office";
    public bool Gameplay { get; private set; } = true;
    public string? AccessCardId { get; private set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(FloorId) && Width > 0f && Height > 0f;
    public static WorkshopElementData From(Godot.Collections.Dictionary data, string inheritedFloor) => new()
    {
        Id = data.GetValueOrDefault("id", "").AsString(), Type = data.GetValueOrDefault("type", "prop").AsString(),
        Label = data.GetValueOrDefault("label", "Element").AsString(), FloorId = data.GetValueOrDefault("floorId", inheritedFloor).AsString(),
        X = data.GetValueOrDefault("x", 0).AsSingle(), Y = data.GetValueOrDefault("y", 0).AsSingle(),
        Width = data.GetValueOrDefault("width", 1).AsSingle(), Height = data.GetValueOrDefault("height", 1).AsSingle(),
        Room = data.GetValueOrDefault("room", "Open office").AsString(), Gameplay = data.GetValueOrDefault("gameplay", true).AsBool(),
        AccessCardId = data.GetValueOrDefault("accessCardId", "").AsString(),
    };
}

public sealed class WorkshopStaffData
{
    public string Id { get; private set; } = ""; public string Name { get; private set; } = ""; public string Job { get; private set; } = "";
    public string Department { get; private set; } = ""; public string FloorId { get; private set; } = ""; public float X { get; private set; } public float Y { get; private set; }
    public bool IsExecutiveThreat { get; private set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(FloorId);
    public static WorkshopStaffData From(Godot.Collections.Dictionary data) => new()
    {
        Id = data.GetValueOrDefault("id", "").AsString(), Name = data.GetValueOrDefault("name", "").AsString(), Job = data.GetValueOrDefault("job", "").AsString(),
        Department = data.GetValueOrDefault("department", "General").AsString(), FloorId = data.GetValueOrDefault("floorId", "").AsString(),
        X = data.GetValueOrDefault("x", 0).AsSingle(), Y = data.GetValueOrDefault("y", 0).AsSingle(), IsExecutiveThreat = data.GetValueOrDefault("isExecutiveThreat", false).AsBool(),
    };
}

public sealed class WorkshopWaypointData
{
    public string Id { get; private set; } = ""; public string FloorId { get; private set; } = ""; public string Label { get; private set; } = "";
    public float X { get; private set; } public float Y { get; private set; } public List<string> Tags { get; } = new(); public int Capacity { get; private set; } = 4;
    public float Visibility { get; private set; } = .5f; public float SocialValue { get; private set; } = .5f; public float CoverValue { get; private set; } = .5f;
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(FloorId);
    public static WorkshopWaypointData From(Godot.Collections.Dictionary data)
    {
        var result = new WorkshopWaypointData
        {
            Id = data.GetValueOrDefault("id", "").AsString(), FloorId = data.GetValueOrDefault("floorId", "").AsString(), Label = data.GetValueOrDefault("label", "Waypoint").AsString(),
            X = data.GetValueOrDefault("x", 0).AsSingle(), Y = data.GetValueOrDefault("y", 0).AsSingle(), Capacity = Math.Max(1, data.GetValueOrDefault("capacity", 4).AsInt32()),
            Visibility = Util.Clamp(data.GetValueOrDefault("visibility", .5f).AsSingle(), 0f, 1f), SocialValue = Util.Clamp(data.GetValueOrDefault("socialValue", .5f).AsSingle(), 0f, 1f), CoverValue = Util.Clamp(data.GetValueOrDefault("coverValue", .5f).AsSingle(), 0f, 1f),
        };
        foreach (Variant tag in data.GetValueOrDefault("tags", new Godot.Collections.Array()).AsGodotArray()) result.Tags.Add(tag.AsString());
        return result;
    }
}
