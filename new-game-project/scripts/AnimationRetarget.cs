using Godot;
using System.Collections.Generic;

/// <summary>
/// Mixamo clips address bones named "mixamorig_Hips"; the Tripo/ActorCore character rigs name
/// the same joints "Hip", "Waist", "L_Upperarm". Injecting a clip across that gap succeeds and
/// then animates precisely nothing -- 0 of 47 tracks resolved on every non-Mixamo NPC.
/// This rewrites each track onto the target skeleton's own bone names.
///
/// Rotation tracks only: Mixamo hip translation is authored in a different unit scale and root
/// motion is unwanted anyway, since NPC movement is driven by the navigation code.
/// </summary>
public static class AnimationRetarget
{
    private static readonly Dictionary<string, string> MixamoToActorCore = new()
    {
        ["Hips"] = "Hip",
        ["Spine"] = "Waist",
        ["Spine1"] = "Spine01",
        ["Spine2"] = "Spine02",
        ["Neck"] = "NeckTwist01",
        ["Head"] = "Head",
        ["LeftShoulder"] = "L_Clavicle",
        ["LeftArm"] = "L_Upperarm",
        ["LeftForeArm"] = "L_Forearm",
        ["LeftHand"] = "L_Hand",
        ["RightShoulder"] = "R_Clavicle",
        ["RightArm"] = "R_Upperarm",
        ["RightForeArm"] = "R_Forearm",
        ["RightHand"] = "R_Hand",
        ["LeftUpLeg"] = "L_Thigh",
        ["LeftLeg"] = "L_Calf",
        ["LeftFoot"] = "L_Foot",
        ["LeftToeBase"] = "L_ToeBase",
        ["RightUpLeg"] = "R_Thigh",
        ["RightLeg"] = "R_Calf",
        ["RightFoot"] = "R_Foot",
        ["RightToeBase"] = "R_ToeBase",
    };

    private static readonly Dictionary<string, Animation> Cache = new();

    /// <summary>Cheap identity for a rig shape, so retargeted clips are built once per rig type.</summary>
    public static string Signature(Skeleton3D skeleton) =>
        skeleton.GetBoneCount() + ":" + (skeleton.GetBoneCount() > 0 ? skeleton.GetBoneName(0) : "none");

    /// <summary>Resolve one source bone name onto the target rig, or null if it has no counterpart.</summary>
    private static string? MapBone(string sourceBone, Skeleton3D target)
    {
        if (target.FindBone(sourceBone) >= 0) return sourceBone;      // already compatible

        string bare = sourceBone.StartsWith("mixamorig_") ? sourceBone["mixamorig_".Length..]
                    : sourceBone.StartsWith("mixamorig:") ? sourceBone["mixamorig:".Length..]
                    : sourceBone;
        if (target.FindBone(bare) >= 0) return bare;
        if (MixamoToActorCore.TryGetValue(bare, out var mapped) && target.FindBone(mapped) >= 0) return mapped;
        return null;                                                   // fingers etc: no counterpart
    }

    /// <summary>
    /// A copy of <paramref name="source"/> retargeted onto <paramref name="target"/>, or null
    /// when not one track could be mapped.
    /// </summary>
    public static Animation? For(string clipName, Animation source, Skeleton3D target, string skeletonPath)
    {
        string key = $"{Signature(target)}|{skeletonPath}|{clipName}";
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var copy = (Animation)source.Duplicate(true);
        for (int i = copy.GetTrackCount() - 1; i >= 0; i--)
        {
            if (copy.TrackGetType(i) != Animation.TrackType.Rotation3D) { copy.RemoveTrack(i); continue; }
            string bone = copy.TrackGetPath(i).GetConcatenatedSubNames();
            string? mapped = bone.Length > 0 ? MapBone(bone, target) : null;
            if (mapped == null) { copy.RemoveTrack(i); continue; }
            copy.TrackSetPath(i, new NodePath($"{skeletonPath}:{mapped}"));
        }

        var result = copy.GetTrackCount() > 0 ? copy : null;
        Cache[key] = result!;
        return result;
    }
}
