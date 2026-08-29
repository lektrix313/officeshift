using Godot;
using System.Linq;

/// <summary>
/// Boot-time animation diagnostic: godot --headless --path . -- --diag-anim
/// Reports, per NPC, whether a rigged model loaded, what skeleton it has, and whether the
/// shared Mixamo clips actually address that skeleton's bones. A clip whose track paths do
/// not match the target rig is injected successfully and then animates nothing.
/// </summary>
public static class AnimDiag
{
    private static bool? _enabled;
    private static bool _dumpedClip;
    private static bool _dumpedBones;

    /// <summary>Lazy: NPC bodies build before GameMode._Ready reaches any explicit detect call.</summary>
    public static bool Enabled => _enabled ??=
        OS.GetCmdlineArgs().Any(a => a == "--diag-anim")
        || OS.GetCmdlineUserArgs().Any(a => a == "--diag-anim");

    public static void Detect() { _ = Enabled; }

    public static void ReportBody(string name, string modelPath, bool rigged,
        Skeleton3D? skeleton, AnimationPlayer? anim)
    {
        if (!Enabled) return;
        if (!rigged)
        {
            GD.Print($"[AnimDiag] {name,-16} NO RIG (capsule)  model={modelPath}");
            return;
        }

        string bones = skeleton == null ? "<no Skeleton3D>" : $"{skeleton.GetBoneCount()} bones";
        string sample = "";
        if (skeleton != null && skeleton.GetBoneCount() > 0)
            sample = string.Join(",", Enumerable.Range(0, System.Math.Min(3, skeleton.GetBoneCount()))
                .Select(i => skeleton.GetBoneName(i)));
        int clips = anim?.GetAnimationList().Length ?? 0;
        GD.Print($"[AnimDiag] {name,-16} rigged  {bones} [{sample}]  clips={clips}  model={modelPath}");

        if (!_dumpedBones && skeleton != null && skeleton.FindBone("mixamorig_Hips") < 0)
        {
            _dumpedBones = true;
            GD.Print("[AnimDiag] non-Mixamo rig bones: " + string.Join(" ",
                Enumerable.Range(0, skeleton.GetBoneCount()).Select(i => skeleton.GetBoneName(i))));
        }

        // one dump of what a shared clip is actually trying to animate
        if (!_dumpedClip && anim != null)
        {
            var shared = anim.GetAnimationList().FirstOrDefault(c => c.StartsWith("shared/"));
            if (shared != null)
            {
                var a = anim.GetAnimation(shared);
                if (a != null && a.GetTrackCount() > 0)
                {
                    _dumpedClip = true;
                    GD.Print($"[AnimDiag]   clip '{shared}' has {a.GetTrackCount()} tracks; first paths:");
                    for (int i = 0; i < System.Math.Min(3, a.GetTrackCount()); i++)
                        GD.Print($"[AnimDiag]     -> {a.TrackGetPath(i)}");

                    // do those paths resolve against this model's skeleton?
                    int resolved = 0;
                    for (int i = 0; i < a.GetTrackCount(); i++)
                    {
                        string sub = a.TrackGetPath(i).GetConcatenatedSubNames();
                        if (skeleton != null && sub.Length > 0 && skeleton.FindBone(sub) >= 0) resolved++;
                    }
                    GD.Print($"[AnimDiag]   tracks resolving to a bone on this rig: {resolved}/{a.GetTrackCount()}");
                }
            }
        }
    }
}
