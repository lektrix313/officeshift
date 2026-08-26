using Godot;
using System.Collections.Generic;

/// <summary>
/// Real-time NPC chat overlay (T near an NPC). NPC pauses routine while
/// talking; replies come from NpcChatService (LLM or offline persona) and may
/// steer their actions via [goto:zone] directives.
/// </summary>
public partial class TalkOverlay : CanvasLayer
{
    public bool IsOpen { get; private set; }
    public NpcBrain? CurrentNpc { get; private set; }

    private Label _header = null!;
    private VBoxContainer _history = null!;
    private LineEdit _input = null!;
    private ScrollContainer _scroll = null!;

    public override void _Ready()
    {
        Layer = 10;
        Visible = false;

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        panel.OffsetLeft = -420;
        panel.OffsetRight = 420;
        panel.OffsetTop = -360;
        panel.OffsetBottom = -40;
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.05f, 0.08f, 0.94f),
            BorderColor = Color.FromHtml("ffd76a"),
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
        panel.AddThemeStyleboxOverride("panel", sb);
        AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);

        _header = new Label { Text = "" };
        _header.AddThemeFontSizeOverride("font_size", 18);
        _header.Modulate = Color.FromHtml("ffd76a");
        box.AddChild(_header);

        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 190),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(_scroll);
        _history = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _history.AddThemeConstantOverride("separation", 4);
        _scroll.AddChild(_history);

        _input = new LineEdit
        {
            PlaceholderText = "Say something… (Enter to send, Esc to end)",
            CustomMinimumSize = new Vector2(0, 34),
        };
        _input.TextSubmitted += OnSubmit;
        box.AddChild(_input);
    }

    public void Open(NpcBrain npc)
    {
        CurrentNpc = npc;
        IsOpen = true;
        Visible = true;
        npc.Talking = true;

        var persona = Personas.For(npc.NpcName);
        _header.Text = $"{npc.NpcName} — {persona.Role}" + (NpcChatService.Live ? "" : "   [offline persona]");
        foreach (var child in _history.GetChildren()) child.QueueFree();
        AppendNpcLine(persona.Greeting);

        _input.Text = "";
        Input.MouseMode = Input.MouseModeEnum.Visible;
        CallDeferred(nameof(FocusInput));
    }

    private void FocusInput() => _input.GrabFocus();

    public void AppendNpcLine(string text)
    {
        if (CurrentNpc == null) return;
        TalkHistory.Push(CurrentNpc.NpcName, CurrentNpc.NpcName, text);
        AddLine($"{CurrentNpc.NpcName}: {text}", Color.FromHtml("9fd8ff"));
    }

    private void AddLine(string text, Color color)
    {
        var l = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        l.AddThemeFontSizeOverride("font_size", 14);
        l.Modulate = color;
        _history.AddChild(l);
        CallDeferred(nameof(ScrollToEnd));
    }

    private void ScrollToEnd()
    {
        _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;
    }

    /// <summary>Deterministic capture hook: submits a line as if typed.</summary>
    public void SubmitForCapture(string text)
    {
        if (!IsOpen) return;
        _input.Text = text;
        OnSubmit(text);
    }

    private void OnSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || CurrentNpc == null) return;
        var npc = CurrentNpc;
        TalkHistory.Push(npc.NpcName, "You", text);
        AddLine($"You: {text}", Colors.White);
        _input.Text = "";

        string context = Personas.ContextLine(npc, GameMode.Instance!);
        NpcChatService.ChatAsync(npc, text, context, "CHAT");
    }

    public void Close()
    {
        if (CurrentNpc != null) CurrentNpc.Talking = false;
        CurrentNpc = null;
        IsOpen = false;
        Visible = false;
    }

    public override void _Input(InputEvent e)
    {
        if (!IsOpen) return;
        if (e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            GameMode.Instance?.CloseUI();
        }
    }
}
