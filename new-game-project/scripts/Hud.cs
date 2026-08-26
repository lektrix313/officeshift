using Godot;
using System.Collections.Generic;

/// <summary>Serializable HUD snapshot pushed from GameMode. Port of types.ts HudState.</summary>
public sealed class HudSnapshot
{
    public bool Started;
    public bool Paused;
    public bool Over;
    public bool Won;
    public string EndReason = "";
    public string Prompt = "";
    /// <summary>0..1 while channeling; -1 when idle.</summary>
    public float ChannelProgress = -1f;
    public string? Carrying;
    public string? Disguise;
    public bool Crouching;
    public bool HasMop;
    public bool HasBlueprint;
    public bool BlueprintSent;
    public bool Alert;
    public float TimeLeft;
    /// <summary>0..100 highest current suspicion among NPCs.</summary>
    public float MaxSuspicion;
    public bool BeingWatched;
    public string? Held;
    public string? Dept;
    public bool CaseActive;
    public float CasePct;
    public List<(string Label, bool Done)> Objectives { get; } = new();
    public (int Bonks, int Hides, int Reports, int Disguises, int Cleans) Stats;
}

/// <summary>
/// Corporate-dystopia HUD: crosshair, contextual prompt + channel bar,
/// objectives top-right, shift clock top-center, max-suspicion meter
/// (colorblind-safe: glyph + bar + integer %), toast stack bottom-left,
/// start/pause overlays, win/lose end card with stats. Built entirely in code.
/// </summary>
public partial class Hud : CanvasLayer
{
    private static readonly Color PanelBg = new(0.04f, 0.06f, 0.09f, 0.85f);
    private static readonly Color AccentGold = Color.FromHtml("ffd76a");
    private static readonly Color AccentCyan = Color.FromHtml("0af0ff");
    private static readonly Color Green = Color.FromHtml("39d97a");
    private static readonly Color Yellow = Color.FromHtml("ffd23a");
    private static readonly Color Red = Color.FromHtml("ff3b30");

    private Label _clockLabel = null!;
    private Label _susGlyph = null!;
    private ProgressBar _susBar = null!;
    private Label _susPct = null!;
    private Label _statusLabel = null!;
    private VBoxContainer _objectivesList = null!;
    private Label _promptLabel = null!;
    private ProgressBar _channelBar = null!;
    private VBoxContainer _toastStack = null!;
    private Control _startOverlay = null!;
    private Control _pauseOverlay = null!;
    private Control _endOverlay = null!;
    private Label _endTitle = null!;
    private Label _endReason = null!;
    private Label _endStats = null!;
    private readonly List<Label> _objectiveLabels = new();

    public override void _Ready()
    {
        var root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // crosshair
        var dot = new ColorRect { Color = new Color(1, 1, 1, 0.8f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dot.SetAnchorsPreset(Control.LayoutPreset.Center);
        dot.CustomMinimumSize = new Vector2(4, 4);
        root.AddChild(dot);

        // clock top-center
        _clockLabel = MakeLabel(root, "06:00", 32, AccentGold);
        Anchor(_clockLabel, Control.LayoutPreset.CenterTop);
        _clockLabel.OffsetTop = 10;

        // suspicion meter top-left
        var susPanel = Panel(root, new Vector2(16, 10), new Vector2(280, 58));
        _susGlyph = MakeLabel(susPanel, "[ ! ]", 16, Green);
        Position(_susGlyph, 12, 6);
        _susBar = Bar(susPanel, new Vector2(70, 12), new Vector2(140, 14));
        _susPct = MakeLabel(susPanel, "0%", 15, Colors.White);
        Position(_susPct, 220, 8);

        // status line
        _statusLabel = MakeLabel(root, "", 14, Yellow);
        Anchor(_statusLabel, Control.LayoutPreset.TopLeft);
        _statusLabel.OffsetLeft = 18;
        _statusLabel.OffsetTop = 78;

        // objectives top-right
        var objPanel = Panel(root, Vector2.Zero, new Vector2(320, 132));
        Anchor(objPanel, Control.LayoutPreset.TopRight);
        objPanel.OffsetLeft = -336;
        objPanel.OffsetTop = 10;
        objPanel.OffsetBottom = 142;
        var objTitle = MakeLabel(objPanel, "SHIFT OBJECTIVES", 13, AccentGold);
        Position(objTitle, 12, 6);
        _objectivesList = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(12, 30),
            Size = new Vector2(296, 96),
        };
        objPanel.AddChild(_objectivesList);

        // prompt + channel bar bottom-center
        _promptLabel = MakeLabel(root, "", 17, Colors.White);
        Anchor(_promptLabel, Control.LayoutPreset.CenterBottom);
        _promptLabel.GrowHorizontal = Control.GrowDirection.Both;
        _promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _promptLabel.OffsetTop = -116;
        _promptLabel.OffsetBottom = -86;
        _promptLabel.OffsetLeft = -320;
        _promptLabel.OffsetRight = 320;

        _channelBar = Bar(root, new Vector2(-180, -80), new Vector2(360, 12));
        Anchor(_channelBar, Control.LayoutPreset.CenterBottom);
        _channelBar.Visible = false;

        // toasts bottom-left
        _toastStack = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _toastStack.AddThemeConstantOverride("separation", 6);
        _toastStack.AnchorTop = 1f;
        _toastStack.AnchorBottom = 1f;
        _toastStack.OffsetLeft = 16;
        _toastStack.OffsetTop = -230;
        _toastStack.OffsetRight = 460;
        _toastStack.OffsetBottom = -20;
        root.AddChild(_toastStack);

        // ---- overlays ----
        _startOverlay = Overlay(root);
        var sb = CenterBox(_startOverlay, 560);
        Title(sb, "OFFICE SHIFT");
        Sub(sb, "INTERNAL AFFAIRS — proof-of-fun build");
        Body(sb, "WASD move · C crouch · F bonk · Q carry/drop\nE interact / hold E · R restart after shift");
        Body(sb, "Steal the blueprints. Mail them out.\nDon't get caught. Mop accordingly.");
        Sub(sb, "— click to clock in —");

        _pauseOverlay = Overlay(root);
        var pb = CenterBox(_pauseOverlay, 380);
        Title(pb, "SHIFT PAUSED", 34);
        Sub(pb, "click to resume");
        _pauseOverlay.Visible = false;

        _endOverlay = Overlay(root);
        var eb = CenterBox(_endOverlay, 640);
        _endTitle = Title(eb, "TERMINATED", 46, Red);
        _endReason = Body(eb, "");
        _endStats = Body(eb, "");
        Sub(eb, "R — clock in again");
        _endOverlay.Visible = false;
    }

    // ================= per-frame state push =================

    public void Push(HudSnapshot s)
    {
        int mins = (int)(s.TimeLeft / 60f);
        int secs = (int)s.TimeLeft % 60;
        _clockLabel.Text = $"{mins:00}:{secs:00}";
        _clockLabel.Modulate = s.TimeLeft < 60f ? Red : AccentGold;

        float t = Util.Clamp(s.MaxSuspicion / 100f, 0f, 1f);
        _susBar.Value = t;
        _susPct.Text = $"{(int)s.MaxSuspicion}%";
        if (t < 0.5f) _susBar.Modulate = Green.Lerp(Yellow, t * 2f);
        else _susBar.Modulate = Yellow.Lerp(Red, (t - 0.5f) * 2f);
        _susGlyph.Modulate = s.MaxSuspicion > 50 ? Red : s.MaxSuspicion > 15 ? Yellow : Green;

        var bits = new List<string>();
        if (s.Held != null) bits.Add(s.Held);
        if (s.Dept != null) bits.Add($"[{s.Dept} uniform]");
        if (s.Carrying != null) bits.Add($"carrying {s.Carrying}");
        if (s.Disguise != null) bits.Add($"disguised as \"{s.Disguise}\"");
        if (s.Crouching) bits.Add("crouching");
        if (s.HasMop) bits.Add("[mop]");
        if (s.HasBlueprint) bits.Add("[blueprints]");
        if (s.BeingWatched) bits.Add("*being watched*");
        if (s.CaseActive) bits.Add($"HR CASE: {CaseColorPct(s.CasePct)}% evidence");
        _statusLabel.Text = string.Join("   ", bits);

        _promptLabel.Text = s.Prompt;
        bool channeling = s.ChannelProgress >= 0;
        _channelBar.Visible = channeling;
        if (channeling) _channelBar.Value = s.ChannelProgress;

        EnsureObjectiveLabels(s.Objectives.Count);
        for (int i = 0; i < s.Objectives.Count; i++)
        {
            var (label, done) = s.Objectives[i];
            var l = _objectiveLabels[i];
            l.Text = done ? $"[DONE] {label}" : $"[ ] {label}";
            l.Modulate = done ? new Color(1, 1, 1, 0.4f) : Colors.White;
        }

        _startOverlay.Visible = !s.Started && !s.Over;
        _pauseOverlay.Visible = s.Started && !s.Over && s.Paused;
        _endOverlay.Visible = s.Over;
        if (s.Over)
        {
            _endTitle.Text = s.Won ? "PROMOTED" : "TERMINATED";
            _endTitle.Modulate = s.Won ? AccentGold : Red;
            _endReason.Text = s.EndReason;
            var st = s.Stats;
            _endStats.Text = $"Bonks: {st.Bonks}    Hides: {st.Hides}    Reports taken: {st.Reports}\nDisguises: {st.Disguises}    Stains cleaned: {st.Cleans}";
        }
    }

    // ================= toasts =================

    public void Toast(string msg, ToastKind kind)
    {
        var borderCol = kind switch
        {
            ToastKind.Warn => Yellow,
            ToastKind.Chaos => Color.FromHtml("d070c0"),
            ToastKind.Success => Green,
            _ => new Color(0.55f, 0.62f, 0.75f),
        };

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = borderCol,
            BorderWidthLeft = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        });
        var label = new Label
        {
            Text = msg,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        panel.AddChild(label);

        _toastStack.AddChild(panel);
        while (_toastStack.GetChildCount() > 5)
        {
            var oldest = _toastStack.GetChild(0);
            _toastStack.RemoveChild(oldest);
            oldest.QueueFree();
        }

        var tween = CreateTween();
        tween.TweenInterval(4.0);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.5);
        tween.TweenCallback(Callable.From(() => panel.QueueFree()));
    }

    // ================= construction helpers =================

    private static string CaseColorPct(float pct) => $"{(int)pct}%";

    private static void Anchor(Control c, Control.LayoutPreset preset) => c.SetAnchorsAndOffsetsPreset(preset);

    private static void Position(Control c, float x, float y) => c.Position = new Vector2(x, y);

    private static Label MakeLabel(Control parent, string text, int size, Color color)
    {
        var l = new Label
        {
            Text = text,
            Modulate = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }

    private static Panel Panel(Control parent, Vector2 pos, Vector2 size)
    {
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", FlatPanel());
        parent.AddChild(p);
        p.Position = pos;
        p.Size = size;
        return p;
    }

    private static StyleBoxFlat FlatPanel() => new()
    {
        BgColor = PanelBg,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
    };

    private static ProgressBar Bar(Control parent, Vector2 pos, Vector2 size)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.12f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = AccentCyan,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        });
        parent.AddChild(bar);
        bar.Position = pos;
        return bar;
    }

    private static Control Overlay(Control parent)
    {
        var dim = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.05f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(dim);
        return dim;
    }

    private static VBoxContainer CenterBox(Control overlayParent, float width)
    {
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlayParent.AddChild(center);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", FlatPanel());
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        center.AddChild(panel);
        return box;
    }

    private static Label Title(VBoxContainer box, string text, int size = 52) => Title(box, text, size, AccentGold);

    private static Label Title(VBoxContainer box, string text, int size, Color color)
    {
        var l = MakeLabel(box, text, size, color);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        return l;
    }

    private static Label Sub(VBoxContainer box, string text)
    {
        var l = MakeLabel(box, text, 15, new Color(0.7f, 0.76f, 0.85f));
        l.HorizontalAlignment = HorizontalAlignment.Center;
        return l;
    }

    private static Label Body(VBoxContainer box, string text)
    {
        var l = MakeLabel(box, text, 16, Colors.White);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        return l;
    }

    private void EnsureObjectiveLabels(int count)
    {
        while (_objectiveLabels.Count < count)
        {
            var l = MakeLabel(_objectivesList, "", 14, Colors.White);
            _objectiveLabels.Add(l);
        }
    }
}


