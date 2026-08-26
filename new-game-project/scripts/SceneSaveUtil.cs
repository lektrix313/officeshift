using Godot;

/// <summary>
/// Shared save path for build-time scene generators (per engines/godot.md):
/// owner chain -> pack -> instantiate-count validation -> save.
/// Silent serialization failures are gated here on purpose.
/// </summary>
public static class SceneSaveUtil
{
    public static void SetOwnerRecursive(Node root, Node owner)
    {
        foreach (var child in root.GetChildren())
        {
            if (!string.IsNullOrEmpty(child.SceneFilePath)) continue; // never recurse into instantiated scenes/GLBs
            child.Owner = owner;
            SetOwnerRecursive(child, owner);
        }
    }

    public static int CountNodes(Node n)
    {
        int c = 1;
        foreach (var child in n.GetChildren()) c += CountNodes(child);
        return c;
    }

    /// <summary>Pack, validate, save. Returns false (after PushError) when serialization dropped nodes.</summary>
    public static bool PackAndValidate(Node root, string path)
    {
        SetOwnerRecursive(root, root);
        int expected = CountNodes(root);

        var packed = new PackedScene();
        var err = packed.Pack(root);
        if (err != Error.Ok)
        {
            GD.PushError($"[SceneSave] Pack failed for {path}: {err}");
            return false;
        }

        var test = packed.Instantiate();
        int got = test is null ? 0 : CountNodes(test);
        test?.Free();
        if (got < expected)
        {
            GD.PushError($"[SceneSave] node count dropped packing {path}: expected {expected}, got {got}");
            return false;
        }

        err = ResourceSaver.Save(packed, path);
        if (err != Error.Ok)
        {
            GD.PushError($"[SceneSave] Save failed for {path}: {err}");
            return false;
        }
        GD.Print($"[SceneSave] wrote {path} ({expected} nodes)");
        return true;
    }

    /// <summary>Attach a C# script to a freshly built node (SetScript disposes the wrapper,
    /// so callers must re-fetch the node from a temp parent afterwards).</summary>
    public static Node AttachScript(Node node, string scriptPath)
    {
        var temp = new Node();
        temp.AddChild(node);
        node.SetScript(GD.Load<Script>(scriptPath));
        var attached = temp.GetChild(0);
        temp.RemoveChild(attached);
        temp.QueueFree();
        return attached;
    }
}

