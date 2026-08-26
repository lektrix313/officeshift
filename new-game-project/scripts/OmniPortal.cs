using Godot;
using System.Collections.Generic;

/// <summary>
/// In-game computer: OmniCore intranet parody. Inbox / Sent / Compose.
/// Compose emails an NPC persona; the reply lands in MailStore and may carry
/// an action directive that steers their AI. Esc or E stands up.
/// </summary>
public partial class OmniPortal : CanvasLayer
{
    public bool IsOpen { get; private set; }

    private PanelContainer _window = null!;
    private Label _header = null!;
    private VBoxContainer _listBox = null!;
    private VBoxContainer _composeBox = null!;
    private OptionButton _recipient = null!;
    private LineEdit _subject = null!;
    private TextEdit _body = null!;
    private Label _hint = null!;
    private enum Tab { Inbox, Sent, Compose }
    private Tab _tab = Tab.Inbox;

    public override void _Ready()
    {
        Layer = 10;
        Visible = false;

        var dim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.82f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        _window = new PanelContainer();
        _window.SetAnchorsPreset(Control.LayoutPreset.Center);
        _window.CustomMinimumSize = new Vector2(760, 520);
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.05f, 0.08f, 0.97f),
            BorderColor = Color.FromHtml("0af0ff"),
            BorderWidthBottom = 3,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
        _window.AddThemeStyleboxOverride("panel", sb);
        AddChild(_window);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _window.AddChild(box);

        _header = new Label { Text = "OMNIPORTAL — OmniCore Industries intranet" };
        _header.AddThemeFontSizeOverride("font_size", 20);
        _header.Modulate = Color.FromHtml("0af0ff");
        box.AddChild(_header);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        box.AddChild(tabs);
        AddTab(tabs, "INBOX", Tab.Inbox);
        AddTab(tabs, "SENT", Tab.Sent);
        AddTab(tabs, "COMPOSE", Tab.Compose);

        _listBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        box.AddChild(_listBox);

        _composeBox = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _composeBox.AddThemeConstantOverride("separation", 6);
        var toRow = new HBoxContainer();
        var toLabel = new Label { Text = "To:" };
        toLabel.AddThemeFontSizeOverride("font_size", 15);
        toRow.AddChild(toLabel);
        _recipient = new OptionButton { CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        toRow.AddChild(_recipient);
        _composeBox.AddChild(toRow);

        _subject = new LineEdit { PlaceholderText = "Subject", CustomMinimumSize = new Vector2(0, 30) };
        _composeBox.AddChild(_subject);

        _body = new TextEdit { PlaceholderText = "Type your corporate masterpiece…", CustomMinimumSize = new Vector2(0, 180) };
        _composeBox.AddChild(_body);

        var send = new Button { Text = "SEND", CustomMinimumSize = new Vector2(120, 34) };
        send.Pressed += OnSend;
        _composeBox.AddChild(send);
        box.AddChild(_composeBox);

        _hint = new Label { Text = "Esc — stand up" };
        _hint.Modulate = new Color(1, 1, 1, 0.5f);
        box.AddChild(_hint);
    }

    private void AddTab(HBoxContainer parent, string label, Tab tab)
    {
        var b = new Button { Text = label };
        b.Pressed += () => { _tab = tab; Refresh(); };
        parent.AddChild(b);
    }

    public void Open()
    {
        IsOpen = true;
        Visible = true;
        _tab = Tab.Inbox;
        _recipient.Clear();
        if (GameMode.Instance != null)
        {
            foreach (var n in GameMode.Instance.Npcs)
                _recipient.AddItem(n.NpcName);
        }
        Refresh();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        IsOpen = false;
        Visible = false;
    }

    public override void _Input(InputEvent e)
    {
        if (!IsOpen) return;
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            GameMode.Instance?.CloseUI();
        }
    }

    private void Refresh()
    {
        bool composing = _tab == Tab.Compose;
        _composeBox.Visible = composing;
        _listBox.Visible = !composing;
        if (composing) return;

        foreach (var child in _listBox.GetChildren()) child.QueueFree();
        _header.Text = _tab == Tab.Inbox ? "OMNIPORTAL — inbox" : "OMNIPORTAL — sent";

        var items = new List<MailMsg>(_tab == Tab.Inbox ? MailStore.Inbox : MailStore.Sent);
        items.Reverse();
        if (items.Count == 0)
        {
            var empty = new Label { Text = _tab == Tab.Inbox ? "Inbox zero. Suspicious." : "You have sent nothing. Productivity: questionable." };
            empty.Modulate = new Color(1, 1, 1, 0.5f);
            _listBox.AddChild(empty);
            return;
        }
        foreach (var m in items)
        {
            var row = new VBoxContainer();
            var head = new Label { Text = $"{m.From} → {m.To}:  {m.Subject}" };
            head.AddThemeFontSizeOverride("font_size", 14);
            head.Modulate = Color.FromHtml("ffd76a");
            row.AddChild(head);
            var body = new Label
            {
                Text = m.Body,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(700, 0),
            };
            body.AddThemeFontSizeOverride("font_size", 14);
            row.AddChild(body);
            var sep = new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1) };
            row.AddChild(sep);
            _listBox.AddChild(row);
        }
    }

    /// <summary>Deterministic capture hook: fills the compose form and sends.</summary>
    public void ComposeTo(string npcName, string subject, string body)
    {
        if (!IsOpen) Open();
        _tab = Tab.Compose;
        Refresh();
        int idx = -1;
        for (int i = 0; i < _recipient.ItemCount; i++)
            if (_recipient.GetItemText(i) == npcName) { idx = i; break; }
        if (idx < 0) return;
        _recipient.Select(idx);
        _subject.Text = subject;
        _body.Text = body;
        OnSend();
    }

    private void OnSend()
    {
        var gm = GameMode.Instance;
        if (gm == null || _recipient.Selected < 0 || string.IsNullOrWhiteSpace(_body.Text)) return;
        string to = _recipient.GetItemText(_recipient.Selected);
        string subject = string.IsNullOrWhiteSpace(_subject.Text) ? "(no subject)" : _subject.Text;

        MailStore.Send(new MailMsg(MailStore.PlayerAddress, to, subject, _body.Text, 0f, FromPlayer: true));
        var npc = gm.Npcs.Find(n => n.NpcName == to);
        if (npc != null)
        {
            string context = Personas.ContextLine(npc, gm);
            NpcChatService.EmailAsync(npc, _body.Text, context);
            gm.Toast($"Mail sent to {to}. The wheels of corporate bureaucracy begin turning.", ToastKind.Info);
        }
        _subject.Text = "";
        _body.Text = "";
        _tab = Tab.Sent;
        Refresh();
    }
}
