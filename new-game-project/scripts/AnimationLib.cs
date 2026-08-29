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

            // Key clips by FILENAME, not by their internal name. Every Mixamo export names its
            // animation "mixamo.com", so keying by internal name meant the first file won and the
            // other eleven were silently discarded -- 12 FBX files produced 1 usable clip.
            string fileKey = fbxPath[(fbxPath.LastIndexOf('/') + 1)..];
            int dot = fileKey.LastIndexOf('.');
            if (dot > 0) fileKey = fileKey[..dot];
            fileKey = fileKey.Replace('/', ' ').Replace(':', ' ').Replace(',', ' ').Replace('[', ' ').Replace(']', ' ');

            var animPlayers = instance.FindChildren("*", "AnimationPlayer", true);
            if (animPlayers.Count > 0)
            {
                var ap = (AnimationPlayer)animPlayers[0];
                var names = ap.GetAnimationList();
                foreach (string clipName in names)
                {
                    var anim = ap.GetAnimation(clipName);
                    if (anim == null) continue;
                    // one clip per file is the Mixamo norm; disambiguate only when there are several
                    string key = names.Length == 1 ? fileKey : $"{fileKey} {clipName}";
                    if (!_clips.ContainsKey(key)) _clips[key] = anim;
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
    public static int InjectClips(AnimationPlayer target) => InjectClips(target, null);

    /// <summary>
    /// Injects the shared clips, retargeting them onto <paramref name="skeleton"/> when its bone
    /// names differ from the source rig's. Without that step the clips install cleanly and
    /// animate nothing.
    /// </summary>
    public static int InjectClips(AnimationPlayer target, Skeleton3D? skeleton)
    {
        if (target == null || _clips.Count == 0) return 0;

        string skeletonPath = "";
        if (skeleton != null)
        {
            var root = target.GetNodeOrNull(target.RootNode) ?? target.GetParent();
            if (root != null) skeletonPath = root.GetPathTo(skeleton);
        }

        var lib = target.HasAnimationLibrary("shared")
            ? target.GetAnimationLibrary("shared")
            : new AnimationLibrary();
        int injected = 0;
        foreach (var kvp in _clips)
        {
            if (target.HasAnimation(kvp.Key)) continue;
            var clip = kvp.Value;
            if (skeleton != null && skeletonPath.Length > 0)
            {
                var retargeted = AnimationRetarget.For(kvp.Key, kvp.Value, skeleton, skeletonPath);
                if (retargeted == null) continue;   // nothing on this rig it could drive
                clip = retargeted;
            }
            lib.AddAnimation(kvp.Key, clip);
            injected++;
        }
        if (injected > 0 && !target.HasAnimationLibrary("shared"))
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
