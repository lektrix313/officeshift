using System.Collections.Generic;
using Godot;

/// <summary>
/// Shared animation library loaded from assets/animations/*.fbx.
/// Discovers clips at startup, injects them into any AnimationPlayer that lacks clips.
/// Enables character-specific models (which may have no built-in animations)
/// to share a common animation set.
/// </summary>
public static class AnimationLib
{
    private static readonly Dictionary<string, Animation> _clips = new();
    private static bool _loaded;

    /// <summary>Scan assets/animations/ for FBX files and extract Animation clips.</summary>
    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string dir = "res://assets/animations";
        using var dirAccess = DirAccess.Open(dir);
        if (dirAccess == null)
        {
            GD.Print($"[AnimationLib] No animations directory at {dir}");
            return;
        }

        dirAccess.ListDirBegin();
        string fileName = dirAccess.GetNext();
        while (fileName.Length > 0)
        {
            if (fileName.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
            {
                string path = $"{dir}/{fileName}";
                LoadClipsFromFbx(path);
            }
            fileName = dirAccess.GetNext();
        }
        dirAccess.ListDirEnd();

        GD.Print($"[AnimationLib] Loaded {_clips.Count} animation clips from {dir}");
    }

    private static void LoadClipsFromFbx(string fbxPath)
    {
        try
        {
            var scene = ResourceLoader.Load<PackedScene>(fbxPath);
            if (scene == null) return;

            var instance = scene.Instantiate<Node>();
            if (instance == null) return;

            // Find AnimationPlayer in the instantiated FBX scene
            var animPlayers = instance.FindChildren("*", "AnimationPlayer", true);
            if (animPlayers.Count > 0)
            {
                var ap = (AnimationPlayer)animPlayers[0];
                foreach (string clipName in ap.GetAnimationList())
                {
                    var anim = ap.GetAnimation(clipName);
                    if (anim != null && !_clips.ContainsKey(clipName))
                    {
                        _clips[clipName] = anim;
                    }
                }
            }

            // Clean up the instantiated scene (we only wanted the animations)
            instance.QueueFree();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[AnimationLib] Failed to load {fbxPath}: {ex.Message}");
        }
    }

    /// <summary>Returns all discovered clip names.</summary>
    public static IReadOnlyCollection<string> ClipNames => _clips.Keys;

    /// <summary>Returns a clip by name, or null if not found.</summary>
    public static Animation? GetClip(string name) =>
        _clips.TryGetValue(name, out var anim) ? anim : null;

    /// <summary>
    /// Injects all available clips into the target AnimationPlayer via AnimationLibrary.
    /// Skips clips that already exist (model's own animations take priority).
    /// Returns the number of clips injected.
    /// </summary>
    public static int InjectClips(AnimationPlayer target)
    {
        if (target == null || _clips.Count == 0) return 0;

        var lib = new AnimationLibrary();
        int injected = 0;
        foreach (var kvp in _clips)
        {
            if (!target.HasAnimation(kvp.Key))
            {
                lib.AddAnimation(kvp.Key, kvp.Value);
                injected++;
            }
        }
        if (injected > 0)
            target.AddAnimationLibrary("shared", lib);
        return injected;
    }

    /// <summary>
    /// Finds the best clip matching any of the given tokens.
    /// Priority: first token match wins. Skips "idle" unless it's the only option.
    /// </summary>
    public static string FindClip(string[] tokens)
    {
        string fallback = "";
        foreach (var kvp in _clips)
        {
            string low = kvp.Key.ToLowerInvariant();
            if (low.Contains("idle")) { fallback = kvp.Key; continue; }
            foreach (string token in tokens)
            {
                if (low.Contains(token)) return kvp.Key;
            }
        }
        return fallback;
    }
}
