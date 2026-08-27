using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record FloorLink(string Id, string FromFloor, string ToFloor, Vector3 FromPosition, Vector3 ToPosition, OfficeObjectType Type, string? RequiredKeycard = null);

public sealed class MultiFloorNavigation
{
    private readonly Dictionary<string, int> _floorIndices = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FloorLink> _links = new();
    public IReadOnlyList<FloorLink> Links => _links;
    public IReadOnlyDictionary<string, int> Floors => _floorIndices;

    public void AddFloor(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && !_floorIndices.ContainsKey(id)) _floorIndices[id] = _floorIndices.Count;
    }

    public void AddLink(FloorLink link)
    {
        if (!_floorIndices.ContainsKey(link.FromFloor) || !_floorIndices.ContainsKey(link.ToFloor)) return;
        if (_links.All(existing => !existing.Id.Equals(link.Id, StringComparison.OrdinalIgnoreCase))) _links.Add(link);
    }

    public void AddWorkshopFloors(WorkshopLevelData workshop, float scale = 2f, float originX = -28f, float originZ = -20f)
    {
        foreach (var floor in workshop.Floors) AddFloor(floor.Id);
        var floorsByIndex = workshop.Floors.ToArray();
        foreach (var element in workshop.Elements.Where(element => element.Type is "elevator" or "stair"))
        {
            int sourceIndex = Array.FindIndex(floorsByIndex, floor => floor.Id.Equals(element.FloorId, StringComparison.OrdinalIgnoreCase));
            if (sourceIndex < 0 || sourceIndex + 1 >= floorsByIndex.Length) continue;
            string lower = floorsByIndex[sourceIndex].Id;
            string upper = floorsByIndex[sourceIndex + 1].Id;
            var source = new Vector3(originX + (element.X + element.Width / 2f) * scale, 0f, originZ + (element.Y + element.Height / 2f) * scale);
            var destination = source + Vector3.Up * 3f;
            var type = element.Type == "elevator" ? OfficeObjectType.Elevator : OfficeObjectType.Stairwell;
            AddLink(new FloorLink($"{element.Id}:up", lower, upper, source, destination, type, element.AccessCardId));
            AddLink(new FloorLink($"{element.Id}:down", upper, lower, destination, source, type, element.AccessCardId));
        }
        foreach (var authored in workshop.FloorLinks)
        {
            if (!Floors.ContainsKey(authored.FromFloor) || !Floors.ContainsKey(authored.ToFloor)) continue;
            var element = workshop.Elements.FirstOrDefault(candidate => candidate.Id.Equals(authored.ElementId, StringComparison.OrdinalIgnoreCase));
            if (element == null) continue;
            var point = new Vector3(originX + (element.X + element.Width / 2f) * scale, 0f, originZ + (element.Y + element.Height / 2f) * scale);
            AddLink(new FloorLink(authored.Id, authored.FromFloor, authored.ToFloor, point, point + Vector3.Up * 3f, element.Type == "stair" ? OfficeObjectType.Stairwell : OfficeObjectType.Elevator, element.AccessCardId));
        }
    }

    public bool CanTraverse(string fromFloor, string toFloor, string? keycardId) => FindLink(fromFloor, toFloor) is { } link &&
        (string.IsNullOrEmpty(link.RequiredKeycard) || link.RequiredKeycard.Equals(keycardId, StringComparison.OrdinalIgnoreCase));

    public FloorLink? FindLink(string fromFloor, string toFloor) => _links.FirstOrDefault(link =>
        link.FromFloor.Equals(fromFloor, StringComparison.OrdinalIgnoreCase) && link.ToFloor.Equals(toFloor, StringComparison.OrdinalIgnoreCase));

    public static string? Validate(WorkshopLevelData workshop)
    {
        var floors = workshop.Floors.Select(floor => floor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (floors.Count != workshop.Floors.Count) return "Workshop has duplicate floor IDs.";
        foreach (var staff in workshop.Staff) if (!floors.Contains(staff.FloorId)) return $"Staff {staff.Name} references a missing floor.";
        foreach (var waypoint in workshop.Waypoints) if (!floors.Contains(waypoint.FloorId)) return $"Waypoint {waypoint.Id} references a missing floor.";
        foreach (var element in workshop.Elements) if (!floors.Contains(element.FloorId)) return $"Element {element.Id} references a missing floor.";
        foreach (var floor in workshop.Floors) if (floor.Width <= 0 || floor.Height <= 0) return $"Floor {floor.Id} has invalid dimensions.";
        foreach (var link in workshop.FloorLinks)
        {
            if (!floors.Contains(link.FromFloor) || !floors.Contains(link.ToFloor)) return $"Floor link {link.Id} references a missing floor.";
            if (!string.IsNullOrEmpty(link.ElementId) && workshop.Elements.All(element => !element.Id.Equals(link.ElementId, StringComparison.OrdinalIgnoreCase))) return $"Floor link {link.Id} references a missing element.";
        }
        return null;
    }
}
