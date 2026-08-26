using Godot;

/// <summary>A usable world prop: instant (ChannelSeconds 0) or hold-channel.</summary>
public sealed record UseTarget(string Id, string Prompt, float ChannelSeconds);

/// <summary>Proximity lookup for job-sim props (positions mirror WorldData.Props).</summary>
public static class Interactables
{
    public static UseTarget? Find(Vector3 feet, HeldItem held, GameMode mode)
    {
        if (Near(feet, 25f, -20.4f, 1.4f))
        {
            return held == HeldItem.Laxative
                ? new UseTarget("coffeemaker", "E — Spike the coffee. Become an office legend.", 0f)
                : new UseTarget("coffeemaker", "Hold E — Brew a fresh pot", 2f);
        }

        if (Near(feet, 26.5f, -20.5f, 1.4f) && !mode.StinkActive)
            return new UseTarget("microwave", "Hold E — Heat up leftover fish (the audacity)", 3f);

        if (Near(feet, 31f, -17f, 2.2f))
        {
            if (mode.VendingCooldown > 0)
                return new UseTarget("vending", $"Vending machine is restocking… ({(int)System.MathF.Ceiling(mode.VendingCooldown)}s)", 0f);
            return new UseTarget("vending",
                mode.VendingLaxativeTaken ? "E — Buy an energy drink" : "E — Buy a \"digestive wellness\" sachet", 0f);
        }

        if (Near(feet, -2f, 19f, 1.9f))
        {
            if (mode.CaseEvidence <= 1f)
                return null;
            return new UseTarget("tapes", "Hold E — Shred tonight's tapes. The truth dies with them.", 4f);
        }

        foreach (var ph in WorldData.Phones)
        {
            if (Near(feet, ph.X, ph.Z, 1.6f))
            {
                if (mode.PhoneCooldown > 0)
                    return new UseTarget("phone", $"The line is busy… ({(int)System.MathF.Ceiling(mode.PhoneCooldown)}s)", 0f);
                return new UseTarget("phone", "E — Desk phone. Someone's about to get a very urgent call.", 0f);
            }
        }

        if (Near(feet, 27f, 11f, 1.9f))
            return new UseTarget("locker", $"E — Adopt {mode.NextUniformName()} from the uniform locker (HR-approved theft)", 0f);

        if (Near(feet, 12.3f, -9f, 1.8f))
        {
            if (mode.AlarmCooldown > 0)
                return new UseTarget("firealarm", $"Fire alarm re-arming… ({(int)System.MathF.Ceiling(mode.AlarmCooldown)}s)", 0f);
            return new UseTarget("firealarm", "E — Pull the fire alarm (HR will hear about this)", 0f);
        }

        return null;
    }

    private static bool Near(Vector3 p, float x, float z, float r)
    {
        float dx = p.X - x;
        float dz = p.Z - z;
        return dx * dx + dz * dz < r * r;
    }
}

