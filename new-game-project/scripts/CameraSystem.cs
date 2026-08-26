using Godot;
using System.Collections.Generic;

/// <summary>
/// Fixed security cameras (bible §13 lite): cone vision over key rooms.
/// Crimes committed inside a live cone accumulate taped evidence toward an
/// HR case. Avoid the cones, or shred the tapes in reception.
/// </summary>
public static class CameraSystem
{
    public sealed record CamDef(string Id, float X, float Z, float Yaw, float Range, float FovHalfRad, Color LedColor);

    public static readonly CamDef[] Cams =
    {
        new("farm-north",  -16f,  -10f,  0f,                14f, 1.0f, Color.FromHtml("ff3b30")), // over Dave's pod, facing +Z
        new("server-door", -12.5f, -13f, -System.MathF.PI / 2f, 10f, 0.9f, Color.FromHtml("ff3b30")), // server door, facing -X
        new("break-kitchen", 14f,  -21f,  System.MathF.PI / 2f, 14f, 0.9f, Color.FromHtml("ff3b30")), // kitchen, facing +X
        new("reception",   0f,   21.2f, System.MathF.PI,    12f, 1.0f, Color.FromHtml("ff3b30")), // reception, facing -Z
    };

    /// <summary>Spawn the visual camera boxes + red LEDs into the scene.</summary>
    public static void CreateNodes(Node parent)
    {
        foreach (var c in Cams)
        {
            var body = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.5f) },
                Position = new Vector3(c.X, 2.7f, c.Z),
                Rotation = new Vector3(0f, c.Yaw, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("30343c"), Roughness = 0.6f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            };
            parent.AddChild(body);

            var led = new OmniLight3D
            {
                Position = new Vector3(c.X, 2.7f, c.Z) - new Vector3(System.MathF.Sin(c.Yaw), 0f, System.MathF.Cos(c.Yaw)) * 0.35f,
                LightColor = c.LedColor,
                LightEnergy = 0.6f,
                OmniRange = 1.2f,
            };
            parent.AddChild(led);
        }
    }

    /// <summary>True when pos is inside any camera's cone with LOS.</summary>
    public static bool IsSeen(Vector3 pos, World world)
    {
        foreach (var c in Cams)
        {
            float dx = pos.X - c.X;
            float dz = pos.Z - c.Z;
            float dist = System.MathF.Sqrt(dx * dx + dz * dz);
            if (dist > c.Range) continue;

            // camera forward = (sin(yaw), cos(yaw)) matching wall-facing yaw convention
            float fx = System.MathF.Sin(c.Yaw);
            float fz = System.MathF.Cos(c.Yaw);
            float dot = (dx * fx + dz * fz) / (dist > 0.001f ? dist : 1f);
            if (dot < System.MathF.Cos(c.FovHalfRad)) continue;

            var camPos = new Vector3(c.X, 0f, c.Z);
            if (!world.LosBlocked(camPos, pos)) return true;
        }
        return false;
    }
}
