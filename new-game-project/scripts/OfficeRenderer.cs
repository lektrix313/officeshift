using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates proper 3D geometry for workshop level elements.
/// Each ElementType gets its own mesh shape, material, and collision profile.
/// Replaces the generic colored-box approach with type-specific visuals.
/// </summary>
public static class OfficeRenderer
{
    // ── Dimensions ──
    private const float WallH = 3.0f;
    private const float CubicleH = 1.4f;
    private const float DeskH = 0.75f;
    private const float ChairSeatH = 0.45f;
    private const float DoorH = 2.4f;
    private const float DoorFrameW = 0.12f;
    private const float MonitorH = 0.4f;
    private const float ServerRackH = 1.8f;
    private const float ShelfH = 1.6f;
    private const float TableH = 0.72f;
    private const float SofaH = 0.45f;
    private const float CabinetH = 0.9f;
    private const float PlantPotH = 0.35f;
    private const float PlantTopH = 0.8f;
    private const float CeilingLightH = 0.08f;
    private const float FireExtingH = 0.5f;

    // ── Materials cache ──
    private static readonly Dictionary<string, StandardMaterial3D> _matCache = new();

    public static void RenderElement(Node3D parent, WorkshopElementData el, float scale, float ox, float oz)
    {
        float cx = ox + (el.X + el.Width / 2f) * scale;
        float cz = oz + (el.Y + el.Height / 2f) * scale;
        float w = MathF.Max(0.25f, el.Width * scale);
        float d = MathF.Max(0.25f, el.Height * scale);

        switch (el.Type)
        {
            // ── Structure ──
            case "wall": RenderWall(parent, cx, cz, w, d, el); break;
            case "glass-wall": RenderGlassWall(parent, cx, cz, w, d); break;
            case "glass-partition": RenderGlassWall(parent, cx, cz, w, d); break;
            case "door": RenderDoor(parent, cx, cz, w, d, el); break;
            case "keycard-door": RenderKeycardDoor(parent, cx, cz, w, d, el); break;
            case "window": RenderWindow(parent, cx, cz, w, d); break;
            case "column": RenderColumn(parent, cx, cz); break;

            // ── Rooms ──
            case "office": RenderRoom(parent, cx, cz, w, d, "room"); break;
            case "cubicle": RenderRoom(parent, cx, cz, w, d, "cubicle"); break;
            case "reception": RenderRoom(parent, cx, cz, w, d, "reception"); break;
            case "meeting-room": RenderRoom(parent, cx, cz, w, d, "meeting"); break;
            case "server-room": RenderRoom(parent, cx, cz, w, d, "server"); break;
            case "break-room": RenderRoom(parent, cx, cz, w, d, "break"); break;
            case "bathroom": RenderRoom(parent, cx, cz, w, d, "bathroom"); break;
            case "storage-closet": RenderRoom(parent, cx, cz, w, d, "storage"); break;
            case "executive-office": RenderRoom(parent, cx, cz, w, d, "executive"); break;

            // ── Furniture ──
            case "desk": RenderDesk(parent, cx, cz, w, d); break;
            case "terminal-desk": RenderTerminalDesk(parent, cx, cz, w, d); break;
            case "chair": RenderChair(parent, cx, cz); break;
            case "printer": RenderPrinter(parent, cx, cz, w, d); break;
            case "meeting-table": RenderMeetingTable(parent, cx, cz, w, d); break;
            case "whiteboard": RenderWhiteboard(parent, cx, cz, w); break;
            case "bookshelf": RenderBookshelf(parent, cx, cz, w); break;
            case "filing-cabinet": RenderCabinet(parent, cx, cz); break;
            case "sofa": RenderSofa(parent, cx, cz, w, d); break;
            case "lounge-chair": RenderChair(parent, cx, cz); break;
            case "coffee-table": RenderCoffeeTable(parent, cx, cz, w, d); break;
            case "server-rack": RenderServerRack(parent, cx, cz); break;
            case "safe": RenderSafe(parent, cx, cz); break;
            case "shredder": RenderShredder(parent, cx, cz); break;
            case "scanner": RenderBox(parent, cx, 0.5f, cz, 0.6f, 0.2f, 0.5f, "a0a8b0"); break;
            case "monitor": RenderMonitor(parent, cx, cz); break;
            case "projector": RenderBox(parent, cx, 2.8f, cz, 0.5f, 0.15f, 0.4f, "646478"); break;
            case "tv-screen": RenderTVScreen(parent, cx, cz, w); break;

            // ── Breakroom ──
            case "water-cooler": RenderWaterCooler(parent, cx, cz); break;
            case "coffee-machine": RenderCoffeeMachine(parent, cx, cz); break;
            case "vending-machine": RenderVendingMachine(parent, cx, cz); break;

            // ── Vertical ──
            case "stair": RenderStair(parent, cx, cz, w, d); break;
            case "elevator": RenderElevator(parent, cx, cz, w, d); break;

            // ── Dressing ──
            case "plant": RenderPlant(parent, cx, cz); break;
            case "clock": RenderClock(parent, cx, cz); break;
            case "fire-extinguisher": RenderFireExtinguisher(parent, cx, cz); break;
            case "coat-rack": RenderCoatRack(parent, cx, cz); break;
            case "umbrella-stand": RenderBox(parent, cx, 0.25f, cz, 0.3f, 0.5f, 0.3f, "909098"); break;

            // ── Fallback ──
            default: RenderBox(parent, cx, DeskH / 2f, cz, w, DeskH, d, "8a7860"); break;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  STRUCTURE
    // ════════════════════════════════════════════════════════════

    private static void RenderWall(Node3D p, float cx, float cz, float w, float d, WorkshopElementData el)
    {
        // Main wall body
        AddBox(p, cx, WallH / 2f, cz, w, WallH, d, "d8d4cc", solid: true);
        // Baseboard
        AddBox(p, cx, 0.05f, cz, w + 0.04f, 0.1f, d + 0.04f, "a09888", solid: false);
        // Top trim
        AddBox(p, cx, WallH - 0.05f, cz, w + 0.04f, 0.1f, d + 0.04f, "c8c0b4", solid: false);
    }

    private static void RenderGlassWall(Node3D p, float cx, float cz, float w, float d)
    {
        // Glass panel
        AddBox(p, cx, WallH / 2f, cz, w, WallH, d, "79b9cc", solid: true, glass: true);
        // Thin frame top and bottom
        AddBox(p, cx, 0.06f, cz, w + 0.06f, 0.12f, d + 0.06f, "888888", solid: false);
        AddBox(p, cx, WallH - 0.06f, cz, w + 0.06f, 0.12f, d + 0.06f, "888888", solid: false);
    }

    private static void RenderDoor(Node3D p, float cx, float cz, float w, float d, WorkshopElementData el)
    {
        // Door frame left
        AddBox(p, cx - w / 2f + DoorFrameW / 2f, DoorH / 2f, cz, DoorFrameW, DoorH, d + 0.1f, "596575", solid: false);
        // Door frame right
        AddBox(p, cx + w / 2f - DoorFrameW / 2f, DoorH / 2f, cz, DoorFrameW, DoorH, d + 0.1f, "596575", solid: false);
        // Door frame top
        AddBox(p, cx, DoorH - 0.06f, cz, w + 0.1f, 0.12f, d + 0.1f, "596575", solid: false);
        // Door panel
        AddBox(p, cx, DoorH / 2f - 0.1f, cz, w * 0.85f, DoorH - 0.2f, 0.06f, "8a7860", solid: true);
        // Door panel inset (raised panel detail)
        AddBox(p, cx, DoorH / 2f - 0.1f, cz + 0.04f, w * 0.6f, (DoorH - 0.2f) * 0.4f, 0.02f, "9a8870", solid: false);
        // Handle
        AddBox(p, cx + w * 0.3f, 1.0f, cz + d / 2f + 0.04f, 0.06f, 0.12f, 0.06f, "c0a840", solid: false);
        // Door number plate
        AddBox(p, cx, 2.0f, cz + d / 2f + 0.04f, 0.2f, 0.1f, 0.02f, "e0d8c8", solid: false);
    }

    private static void RenderKeycardDoor(Node3D p, float cx, float cz, float w, float d, WorkshopElementData el)
    {
        RenderDoor(p, cx, cz, w, d, el);
        // Card reader panel
        AddBox(p, cx + w / 2f + 0.08f, 1.1f, cz, 0.08f, 0.15f, 0.08f, "333333", solid: false);
        // Reader LED (red = locked)
        AddBox(p, cx + w / 2f + 0.08f, 1.2f, cz, 0.04f, 0.04f, 0.04f, "ff3333", solid: false);
    }

    private static void RenderWindow(Node3D p, float cx, float cz, float w, float d)
    {
        // Window glass
        AddBox(p, cx, WallH / 2f, cz, w, WallH * 0.6f, 0.05f, "a0d8e8", solid: false, glass: true);
        // Window frame
        AddBox(p, cx, WallH * 0.3f, cz, w + 0.08f, 0.08f, 0.1f, "888888", solid: false);
        AddBox(p, cx, WallH * 0.9f, cz, w + 0.08f, 0.08f, 0.1f, "888888", solid: false);
        // Cross bar
        AddBox(p, cx, WallH / 2f, cz, 0.06f, WallH * 0.6f, 0.1f, "888888", solid: false);
    }

    private static void RenderColumn(Node3D p, float cx, float cz)
    {
        AddBox(p, cx, WallH / 2f, cz, 0.4f, WallH, 0.4f, "c0b8a8", solid: true);
    }

    // ════════════════════════════════════════════════════════════
    //  ROOMS (floor + ceiling light)
    // ════════════════════════════════════════════════════════════

    private static void RenderRoom(Node3D p, float cx, float cz, float w, float d, string kind)
    {
        string floorColor = kind switch
        {
            "cubicle" => "b8c0c4",
            "reception" => "c8b898",
            "meeting" => "b8b0c8",
            "server" => "8898a0",
            "break" => "c0c8b0",
            "bathroom" => "a0c0d0",
            "storage" => "b0a898",
            "executive" => "a898b0",
            _ => "c0b8b0",
        };
        string ceilingColor = kind switch
        {
            "server" => "606870",
            "executive" => "d0c8d8",
            _ => "e8e4dc",
        };
        float lightIntensity = kind switch
        {
            "server" => 0.6f,
            "executive" => 0.8f,
            "break" => 1.0f,
            "bathroom" => 0.7f,
            _ => 1.0f,
        };
        Color lightTint = kind switch
        {
            "break" => Color.FromHtml("fff0d0"),
            "server" => Color.FromHtml("d0e0ff"),
            "executive" => Color.FromHtml("ffe8c8"),
            "meeting" => Color.FromHtml("f0e8ff"),
            "bathroom" => Color.FromHtml("e8f0ff"),
            _ => Color.FromHtml("fff8f0"),
        };

        // Floor plane (carpet/tile)
        AddBox(p, cx, -0.02f, cz, w, 0.04f, d, floorColor, solid: false);
        // Floor accent strip (tile pattern for wet rooms)
        if (kind is "bathroom" or "break")
            AddBox(p, cx, -0.01f, cz, w - 0.2f, 0.02f, d - 0.2f, "d8d0c0", solid: false);

        // Ceiling plane
        AddBox(p, cx, WallH - 0.02f, cz, w, 0.04f, d, ceilingColor, solid: false);

        // Ceiling light fixture (fluorescent strip)
        AddBox(p, cx, WallH - 0.06f, cz, w * 0.5f, 0.04f, d * 0.12f, "f0e8d0", solid: false, emissive: true);
        // Second light strip for larger rooms
        if (w > 6f || d > 6f)
            AddBox(p, cx, WallH - 0.06f, cz + d * 0.25f, w * 0.5f, 0.04f, d * 0.12f, "f0e8d0", solid: false, emissive: true);

        // Room-tinted OmniLight3D for atmosphere
        var light = new OmniLight3D
        {
            Position = new Vector3(cx, WallH - 0.3f, cz),
            LightColor = lightTint,
            LightEnergy = lightIntensity,
            OmniRange = MathF.Max(w, d) * 0.8f,
            OmniAttenuation = 1.2f,
        };
        p.AddChild(light);
    }

    // ════════════════════════════════════════════════════════════
    //  FURNITURE
    // ════════════════════════════════════════════════════════════

    private static void RenderDesk(Node3D p, float cx, float cz, float w, float d)
    {
        // Desktop surface
        AddBox(p, cx, DeskH, cz, w, 0.06f, d, "c8b898", solid: true);
        // Legs
        float lw = 0.08f;
        AddBox(p, cx - w / 2f + 0.1f, DeskH / 2f, cz - d / 2f + 0.1f, lw, DeskH, lw, "888078", solid: false);
        AddBox(p, cx + w / 2f - 0.1f, DeskH / 2f, cz - d / 2f + 0.1f, lw, DeskH, lw, "888078", solid: false);
        AddBox(p, cx - w / 2f + 0.1f, DeskH / 2f, cz + d / 2f - 0.1f, lw, DeskH, lw, "888078", solid: false);
        AddBox(p, cx + w / 2f - 0.1f, DeskH / 2f, cz + d / 2f - 0.1f, lw, DeskH, lw, "888078", solid: false);
        // Drawer unit (one side)
        AddBox(p, cx + w / 2f - 0.35f, DeskH / 2f - 0.05f, cz, 0.35f, DeskH - 0.1f, d * 0.6f, "b0a888", solid: false);
    }

    private static void RenderTerminalDesk(Node3D p, float cx, float cz, float w, float d)
    {
        RenderDesk(p, cx, cz, w, d);
        // Monitor
        AddBox(p, cx - 0.1f, DeskH + MonitorH / 2f + 0.05f, cz - d * 0.2f, 0.5f, MonitorH, 0.04f, "222230", solid: false);
        // Monitor stand
        AddBox(p, cx - 0.1f, DeskH + 0.08f, cz - d * 0.2f, 0.08f, 0.12f, 0.08f, "444444", solid: false);
        // Screen glow
        AddBox(p, cx - 0.1f, DeskH + MonitorH / 2f + 0.05f, cz - d * 0.2f - 0.03f, 0.46f, MonitorH - 0.06f, 0.01f, "4080a0", solid: false, emissive: true);
        // Keyboard
        AddBox(p, cx, DeskH + 0.04f, cz + d * 0.1f, 0.4f, 0.02f, 0.18f, "333333", solid: false);
    }

    private static void RenderChair(Node3D p, float cx, float cz)
    {
        // Seat
        AddBox(p, cx, ChairSeatH, cz, 0.5f, 0.06f, 0.5f, "606068", solid: false);
        // Backrest
        AddBox(p, cx, ChairSeatH + 0.3f, cz - 0.22f, 0.46f, 0.55f, 0.06f, "606068", solid: false);
        // Base pole
        AddBox(p, cx, ChairSeatH / 2f, cz, 0.06f, ChairSeatH, 0.06f, "505050", solid: false);
        // Wheels (4 dots)
        float r = 0.2f;
        AddBox(p, cx - r, 0.04f, cz - r, 0.06f, 0.06f, 0.06f, "404040", solid: false);
        AddBox(p, cx + r, 0.04f, cz - r, 0.06f, 0.06f, 0.06f, "404040", solid: false);
        AddBox(p, cx - r, 0.04f, cz + r, 0.06f, 0.06f, 0.06f, "404040", solid: false);
        AddBox(p, cx + r, 0.04f, cz + r, 0.06f, 0.06f, 0.06f, "404040", solid: false);
    }

    private static void RenderPrinter(Node3D p, float cx, float cz, float w, float d)
    {
        // Body
        AddBox(p, cx, 0.45f, cz, w * 0.7f, 0.5f, d * 0.6f, "c0c8cc", solid: true);
        // Paper tray
        AddBox(p, cx, 0.2f, cz + d * 0.25f, w * 0.5f, 0.06f, d * 0.3f, "e0e0e0", solid: false);
        // Output tray
        AddBox(p, cx, 0.72f, cz - d * 0.15f, w * 0.5f, 0.04f, d * 0.25f, "d0d0d0", solid: false);
        // Status light
        AddBox(p, cx + w * 0.3f, 0.72f, cz, 0.04f, 0.04f, 0.04f, "30c030", solid: false, emissive: true);
    }

    private static void RenderMeetingTable(Node3D p, float cx, float cz, float w, float d)
    {
        // Table top
        AddBox(p, cx, TableH, cz, w, 0.06f, d, "a09080", solid: true);
        // Center support
        AddBox(p, cx, TableH / 2f, cz, 0.3f, TableH, 0.3f, "706860", solid: false);
        // Legs at corners
        float lx = w / 2f - 0.2f;
        float lz = d / 2f - 0.2f;
        AddBox(p, cx - lx, TableH / 2f, cz - lz, 0.08f, TableH, 0.08f, "706860", solid: false);
        AddBox(p, cx + lx, TableH / 2f, cz - lz, 0.08f, TableH, 0.08f, "706860", solid: false);
        AddBox(p, cx - lx, TableH / 2f, cz + lz, 0.08f, TableH, 0.08f, "706860", solid: false);
        AddBox(p, cx + lx, TableH / 2f, cz + lz, 0.08f, TableH, 0.08f, "706860", solid: false);
    }

    private static void RenderWhiteboard(Node3D p, float cx, float cz, float w)
    {
        // Board surface
        AddBox(p, cx, 1.5f, cz, w, 1.2f, 0.05f, "e8e8e0", solid: false);
        // Frame
        AddBox(p, cx, 1.5f, cz - 0.03f, w + 0.06f, 1.26f, 0.02f, "888888", solid: false);
        // Tray
        AddBox(p, cx, 0.88f, cz + 0.06f, w * 0.6f, 0.04f, 0.08f, "888888", solid: false);
    }

    private static void RenderBookshelf(Node3D p, float cx, float cz, float w)
    {
        // Frame
        AddBox(p, cx, ShelfH / 2f, cz, w, ShelfH, 0.35f, "a09078", solid: true);
        // Shelves (3 horizontal lines)
        for (int i = 0; i < 4; i++)
        {
            float y = 0.1f + i * (ShelfH / 4f);
            AddBox(p, cx, y, cz, w - 0.04f, 0.04f, 0.33f, "b8a890", solid: false);
        }
        // Books (colored blocks on shelves)
        string[] bookColors = { "8b4513", "2e4060", "6b3a3a", "3a5a3a", "5a4a6a" };
        for (int i = 0; i < 3; i++)
        {
            float y = 0.15f + i * (ShelfH / 4f);
            for (int j = 0; j < 5; j++)
            {
                float bx = cx - w / 2f + 0.15f + j * (w - 0.3f) / 5f;
                AddBox(p, bx, y + 0.12f, cz, 0.08f, 0.22f, 0.2f, bookColors[j % bookColors.Length], solid: false);
            }
        }
    }

    private static void RenderCabinet(Node3D p, float cx, float cz)
    {
        // Body
        AddBox(p, cx, CabinetH / 2f, cz, 0.45f, CabinetH, 0.55f, "a0a8b0", solid: true);
        // Drawers (3)
        for (int i = 0; i < 3; i++)
        {
            float y = 0.15f + i * 0.3f;
            AddBox(p, cx, y, cz + 0.28f, 0.4f, 0.25f, 0.02f, "909898", solid: false);
            // Handle
            AddBox(p, cx, y, cz + 0.3f, 0.12f, 0.03f, 0.03f, "c0c0c0", solid: false);
        }
    }

    private static void RenderSofa(Node3D p, float cx, float cz, float w, float d)
    {
        // Seat cushion
        AddBox(p, cx, SofaH / 2f, cz, w, SofaH, d * 0.65f, "707888", solid: false);
        // Backrest
        AddBox(p, cx, SofaH + 0.25f, cz - d * 0.3f, w, 0.5f, d * 0.25f, "606878", solid: false);
        // Armrests
        AddBox(p, cx - w / 2f + 0.06f, SofaH + 0.1f, cz, 0.12f, 0.35f, d * 0.6f, "606878", solid: false);
        AddBox(p, cx + w / 2f - 0.06f, SofaH + 0.1f, cz, 0.12f, 0.35f, d * 0.6f, "606878", solid: false);
    }

    private static void RenderCoffeeTable(Node3D p, float cx, float cz, float w, float d)
    {
        // Table top
        AddBox(p, cx, 0.4f, cz, w, 0.05f, d, "b8a888", solid: false);
        // Legs
        float lx = w / 2f - 0.1f;
        float lz = d / 2f - 0.1f;
        AddBox(p, cx - lx, 0.2f, cz - lz, 0.06f, 0.4f, 0.06f, "888078", solid: false);
        AddBox(p, cx + lx, 0.2f, cz - lz, 0.06f, 0.4f, 0.06f, "888078", solid: false);
        AddBox(p, cx - lx, 0.2f, cz + lz, 0.06f, 0.4f, 0.06f, "888078", solid: false);
        AddBox(p, cx + lx, 0.2f, cz + lz, 0.06f, 0.4f, 0.06f, "888078", solid: false);
    }

    private static void RenderServerRack(Node3D p, float cx, float cz)
    {
        // Rack body
        AddBox(p, cx, ServerRackH / 2f, cz, 0.7f, ServerRackH, 0.8f, "3a4858", solid: true);
        // Rack units (4 horizontal lines)
        for (int i = 0; i < 5; i++)
        {
            float y = 0.15f + i * (ServerRackH / 5f);
            AddBox(p, cx, y, cz + 0.41f, 0.64f, 0.08f, 0.02f, "506070", solid: false);
            // Status LEDs
            AddBox(p, cx + 0.25f, y, cz + 0.42f, 0.03f, 0.03f, 0.02f, "30c030", solid: false, emissive: true);
            AddBox(p, cx + 0.2f, y, cz + 0.42f, 0.03f, 0.03f, 0.02f, "c0c030", solid: false, emissive: true);
        }
    }

    private static void RenderSafe(Node3D p, float cx, float cz)
    {
        // Body
        AddBox(p, cx, 0.5f, cz, 0.7f, 1.0f, 0.6f, "606060", solid: true);
        // Door
        AddBox(p, cx, 0.5f, cz + 0.31f, 0.64f, 0.94f, 0.04f, "505050", solid: false);
        // Handle
        AddBox(p, cx + 0.15f, 0.55f, cz + 0.34f, 0.1f, 0.1f, 0.04f, "c0a840", solid: false);
        // Dial
        AddBox(p, cx - 0.1f, 0.55f, cz + 0.34f, 0.08f, 0.08f, 0.04f, "a0a0a0", solid: false);
    }

    private static void RenderShredder(Node3D p, float cx, float cz)
    {
        // Body
        AddBox(p, cx, 0.4f, cz, 0.35f, 0.8f, 0.3f, "404040", solid: false);
        // Paper slot
        AddBox(p, cx, 0.81f, cz, 0.25f, 0.02f, 0.08f, "333333", solid: false);
        // Bin
        AddBox(p, cx, 0.2f, cz, 0.32f, 0.38f, 0.28f, "505050", solid: false);
    }

    private static void RenderMonitor(Node3D p, float cx, float cz)
    {
        // Screen
        AddBox(p, cx, 1.2f, cz, 0.6f, 0.4f, 0.04f, "1a1a28", solid: false);
        // Screen glow
        AddBox(p, cx, 1.2f, cz - 0.03f, 0.56f, 0.36f, 0.01f, "4080a0", solid: false, emissive: true);
        // Stand
        AddBox(p, cx, 0.95f, cz, 0.06f, 0.2f, 0.06f, "444444", solid: false);
        // Base
        AddBox(p, cx, 0.86f, cz, 0.25f, 0.04f, 0.15f, "444444", solid: false);
    }

    private static void RenderTVScreen(Node3D p, float cx, float cz, float w)
    {
        // Screen
        AddBox(p, cx, 1.5f, cz, w, 0.8f, 0.05f, "101018", solid: false);
        // Glow
        AddBox(p, cx, 1.5f, cz - 0.03f, w - 0.06f, 0.74f, 0.01f, "3060a0", solid: false, emissive: true);
        // Mount bracket
        AddBox(p, cx, 1.1f, cz + 0.04f, 0.15f, 0.3f, 0.08f, "444444", solid: false);
    }

    // ════════════════════════════════════════════════════════════
    //  BREAKROOM
    // ════════════════════════════════════════════════════════════

    private static void RenderWaterCooler(Node3D p, float cx, float cz)
    {
        // Body
        AddBox(p, cx, 0.5f, cz, 0.35f, 1.0f, 0.35f, "e0e8f0", solid: false);
        // Water bottle
        AddCylinder(p, cx, 1.3f, cz, 0.15f, 0.6f, "a0d0e8");
        // Drip tray
        AddBox(p, cx, 0.15f, cz + 0.18f, 0.3f, 0.04f, 0.1f, "888888", solid: false);
    }

    private static void RenderCoffeeMachine(Node3D p, float cx, float cz)
    {
        // Body
        AddBox(p, cx, 0.55f, cz, 0.5f, 0.7f, 0.45f, "3a2a1a", solid: false);
        // Top lid
        AddBox(p, cx, 0.91f, cz, 0.48f, 0.04f, 0.43f, "4a3a2a", solid: false);
        // Cup area
        AddBox(p, cx, 0.25f, cz + 0.15f, 0.3f, 0.04f, 0.15f, "2a1a0a", solid: false);
        // Steam indicator
        AddBox(p, cx, 0.96f, cz, 0.04f, 0.04f, 0.04f, "ff6030", solid: false, emissive: true);
    }

    private static void RenderVendingMachine(Node3D p, float cx, float cz)
    {
        // Body
        AddBox(p, cx, 0.9f, cz, 0.8f, 1.8f, 0.7f, "304050", solid: true);
        // Glass front
        AddBox(p, cx, 1.0f, cz + 0.36f, 0.7f, 1.2f, 0.02f, "80c0d0", solid: false, glass: true);
        // Product rows (colored blocks)
        string[] rowColors = { "c04040", "40a040", "4040c0", "c0c040" };
        for (int i = 0; i < 4; i++)
        {
            float y = 0.5f + i * 0.35f;
            AddBox(p, cx, y, cz + 0.3f, 0.6f, 0.2f, 0.04f, rowColors[i], solid: false);
        }
        // Dispensing slot
        AddBox(p, cx, 0.15f, cz + 0.36f, 0.4f, 0.15f, 0.04f, "1a1a1a", solid: false);
    }

    // ════════════════════════════════════════════════════════════
    //  VERTICAL
    // ════════════════════════════════════════════════════════════

    private static void RenderStair(Node3D p, float cx, float cz, float w, float d)
    {
        // Steps (ascending)
        int steps = 8;
        float stepH = WallH / steps;
        float stepD = d / steps;
        for (int i = 0; i < steps; i++)
        {
            float y = stepH / 2f + i * stepH;
            float z = cz - d / 2f + stepD / 2f + i * stepD;
            AddBox(p, cx, y, z, w * 0.8f, stepH, stepD, "b0a8c0", solid: i == 0);
        }
        // Railing
        AddBox(p, cx - w * 0.4f, WallH * 0.65f, cz, 0.06f, 0.06f, d, "888888", solid: false);
        AddBox(p, cx + w * 0.4f, WallH * 0.65f, cz, 0.06f, 0.06f, d, "888888", solid: false);
    }

    private static void RenderElevator(Node3D p, float cx, float cz, float w, float d)
    {
        // Shaft walls
        AddBox(p, cx - w / 2f, WallH / 2f, cz, 0.15f, WallH, d, "808890", solid: true);
        AddBox(p, cx + w / 2f, WallH / 2f, cz, 0.15f, WallH, d, "808890", solid: true);
        AddBox(p, cx, WallH / 2f, cz - d / 2f, w, WallH, 0.15f, "808890", solid: true);
        // Doors (front)
        AddBox(p, cx - w * 0.25f, DoorH / 2f, cz + d / 2f + 0.08f, w * 0.45f, DoorH, 0.08f, "c0c0c0", solid: false);
        AddBox(p, cx + w * 0.25f, DoorH / 2f, cz + d / 2f + 0.08f, w * 0.45f, DoorH, 0.08f, "c0c0c0", solid: false);
        // Floor indicator
        AddBox(p, cx, 2.6f, cz + d / 2f + 0.1f, 0.2f, 0.12f, 0.04f, "202020", solid: false);
        AddBox(p, cx, 2.6f, cz + d / 2f + 0.12f, 0.16f, 0.06f, 0.02f, "30c030", solid: false, emissive: true);
    }

    // ════════════════════════════════════════════════════════════
    //  DRESSING
    // ════════════════════════════════════════════════════════════

    private static void RenderPlant(Node3D p, float cx, float cz)
    {
        // Pot
        AddCylinder(p, cx, PlantPotH / 2f, cz, 0.2f, PlantPotH, "a06838");
        // Soil
        AddBox(p, cx, PlantPotH, cz, 0.35f, 0.04f, 0.35f, "4a3020", solid: false);
        // Foliage (green sphere approximation with boxes)
        AddBox(p, cx, PlantPotH + PlantTopH * 0.5f, cz, 0.5f, PlantTopH, 0.5f, "408838", solid: false);
        AddBox(p, cx + 0.1f, PlantPotH + PlantTopH * 0.7f, cz - 0.1f, 0.3f, PlantTopH * 0.6f, 0.3f, "50a048", solid: false);
        AddBox(p, cx - 0.1f, PlantPotH + PlantTopH * 0.6f, cz + 0.1f, 0.25f, PlantTopH * 0.5f, 0.25f, "489840", solid: false);
    }

    private static void RenderClock(Node3D p, float cx, float cz)
    {
        // Clock face
        AddCylinder(p, cx, 2.0f, cz, 0.15f, 0.02f, "e8e0d0");
    }

    private static void RenderFireExtinguisher(Node3D p, float cx, float cz)
    {
        // Cylinder body
        AddCylinder(p, cx, FireExtingH / 2f, cz, 0.1f, FireExtingH, "c03030");
        // Handle
        AddBox(p, cx, FireExtingH + 0.08f, cz, 0.06f, 0.1f, 0.06f, "333333", solid: false);
        // Nozzle
        AddBox(p, cx, FireExtingH + 0.04f, cz + 0.08f, 0.04f, 0.04f, 0.1f, "333333", solid: false);
    }

    private static void RenderCoatRack(Node3D p, float cx, float cz)
    {
        // Pole
        AddBox(p, cx, 0.9f, cz, 0.06f, 1.8f, 0.06f, "6a5040", solid: false);
        // Base
        AddBox(p, cx, 0.04f, cz, 0.4f, 0.06f, 0.4f, "6a5040", solid: false);
        // Hooks (4)
        AddBox(p, cx - 0.12f, 1.7f, cz, 0.04f, 0.04f, 0.15f, "888078", solid: false);
        AddBox(p, cx + 0.12f, 1.7f, cz, 0.04f, 0.04f, 0.15f, "888078", solid: false);
        AddBox(p, cx, 1.7f, cz - 0.12f, 0.15f, 0.04f, 0.04f, "888078", solid: false);
        AddBox(p, cx, 1.7f, cz + 0.12f, 0.15f, 0.04f, 0.04f, "888078", solid: false);
    }

    // ════════════════════════════════════════════════════════════
    //  PRIMITIVES
    // ════════════════════════════════════════════════════════════

    private static void RenderBox(Node3D p, float cx, float cy, float cz, float w, float h, float d, string hex, bool glass = false, bool emissive = false)
    {
        AddBox(p, cx, cy, cz, w, h, d, hex, solid: false, glass, emissive);
    }

    private static void AddBox(Node3D parent, float x, float y, float z, float w, float h, float d, string hex, bool solid = false, bool glass = false, bool emissive = false)
    {
        var mat = GetMaterial(hex, glass, emissive);
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(w, h, d) },
            Position = new Vector3(x, y, z),
            MaterialOverride = mat,
            CastShadow = h > 0.3f ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mesh);

        if (!solid) return;
        var body = new StaticBody3D { Position = new Vector3(x, y, z) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(w, h, d) } });
        parent.AddChild(body);
    }

    private static void AddCylinder(Node3D parent, float x, float y, float z, float radius, float height, string hex)
    {
        var mat = GetMaterial(hex, false, false);
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
            Position = new Vector3(x, y, z),
            MaterialOverride = mat,
            CastShadow = height > 0.3f ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mesh);
    }

    private static StandardMaterial3D GetMaterial(string hex, bool glass, bool emissive)
    {
        string key = $"{hex}_{glass}_{emissive}";
        if (_matCache.TryGetValue(key, out var cached)) return cached;

        var baseColor = Color.FromHtml(hex);
        if (glass) baseColor.A = 0.35f;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = baseColor,
            Roughness = glass ? 0.1f : 0.85f,
            Metallic = glass ? 0.3f : 0f,
            Transparency = glass ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            NoDepthTest = false,
        };
        if (emissive)
        {
            mat.EmissionEnabled = true;
            mat.Emission = Color.FromHtml(hex);
            mat.EmissionEnergyMultiplier = 0.6f;
        }
        _matCache[key] = mat;
        return mat;
    }
}
