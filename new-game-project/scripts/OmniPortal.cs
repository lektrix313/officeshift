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
    private enum Tab { Inbox, Sent, Compose, Contracts, Composer }
    private Tab _tab = Tab.Inbox;

    private VBoxContainer _contractsBox = null!;
    private OptionButton _objType = null!;
    private OptionButton _objNpc = null!;
    private OptionButton _objZone = null!;
    private LineEdit _missionTitle = null!;
    private VBoxContainer _composerBox = null!;

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
        AddTab(tabs, "CONTRACTS", Tab.Contracts);
        AddTab(tabs, "COMPOSER", Tab.Composer);

        _listBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        box.AddChild(_listBox);

        _contractsBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false };
        box.AddChild(_contractsBox);

        _composerBox = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
        };
        _composerBox.AddThemeConstantOverride("separation", 6);
        _objType = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        foreach (var t in new[] { "STEAL_BLUEPRINTS", "PHOTO_WHITEBOARD", "LURE_NPC", "KNOCKOUT_NPC", "GHOST" })
            _objType.AddItem(t);
        _composerBox.AddChild(MakeLabeledRow("Objective:", _objType));
        _objNpc = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        _composerBox.AddChild(MakeLabeledRow("Target NPC (lure/bonk):", _objNpc));
        _objZone = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        foreach (var z in new[] { "breakroom", "server", "printer", "reception", "closet" })
            _objZone.AddItem(z);
        _composerBox.AddChild(MakeLabeledRow("Zone (lure):", _objZone));
        _missionTitle = new LineEdit { PlaceholderText = "Contract title", CustomMinimumSize = new Vector2(0, 30) };
        _composerBox.AddChild(_missionTitle);
        var build = new Button { Text = "POST TO BOARD", CustomMinimumSize = new Vector2(160, 34) };
        build.Pressed += OnBuildContract;
        _composerBox.AddChild(build);
        box.AddChild(_composerBox);

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
        _objNpc.Clear();
        if (GameMode.Instance != null)
        {
            foreach (var n in GameMode.Instance.Npcs)
            {
                _recipient.AddItem(n.NpcName);
                _objNpc.AddItem(n.NpcName);
            }
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
        _composeBox.Visible = _tab == Tab.Compose;
        _contractsBox.Visible = _tab == Tab.Contracts;
        _composerBox.Visible = _tab == Tab.Composer;
        _listBox.Visible = _tab is Tab.Inbox or Tab.Sent;

        if (_tab == Tab.Contracts) { BuildContractsList(); return; }
        if (_tab == Tab.Composer) return;

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

    private void BuildContractsList()
    {
        _header.Text = "OMNIPORTAL — contract board";
        foreach (var child in _contractsBox.GetChildren()) child.QueueFree();

        foreach (var c in MissionManager.Loaded)
        {
            var row = new VBoxContainer();
            var head = new Label { Text = $"{c.Title}  [{c.Id}]" };
            head.AddThemeFontSizeOverride("font_size", 15);
            head.Modulate = c.Id == GameMode.Instance?.Active.Id ? Color.FromHtml("39d97a") : Color.FromHtml("ffd76a");
            row.AddChild(head);
            var brief = new Label { Text = c.Brief, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(700, 0) };
            brief.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(brief);
            var accept = new Button { Text = "ACCEPT CONTRACT" };
            var id = c.Id;
            accept.Pressed += () =>
            {
                GameMode.Instance?.AcceptContractById(id);
                Refresh();
            };
            row.AddChild(accept);
            var sep = new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1) };
            row.AddChild(sep);
            _contractsBox.AddChild(row);
        }
    }

    private void OnBuildContract()
    {
        var gm = GameMode.Instance;
        if (gm == null) return;
        string type = _objType.GetItemText(_objType.Selected < 0 ? 0 : _objType.Selected);
        string npc = _objNpc.GetItemText(_objNpc.Selected < 0 ? 0 : _objNpc.Selected);
        string zone = _objZone.GetItemText(_objZone.Selected < 0 ? 0 : _objZone.Selected);
        string title = string.IsNullOrWhiteSpace(_missionTitle.Text) ? $"Custom: {type.ToLowerInvariant().Replace('_', ' ')}" : _missionTitle.Text;

        var objectives = new List<MissionObjective>();
        if (type == "LURE_NPC") objectives.Add(new MissionObjective("LURE_NPC", npc, zone));
        else if (type == "KNOCKOUT_NPC") objectives.Add(new MissionObjective("KNOCKOUT_NPC", npc));
        else objectives.Add(new MissionObjective(type));

        var contract = new MissionContract(
            $"USER-{DateTime.Now:HHmmss}",
            title,
            $"A {type.ToLowerInvariant().Replace('_', ' ')} contract targeting {npc}, hand-crafted by an employee with too much initiative.",
            "Contract complete. HR files it under 'initiative'.",
            objectives);
        string path = MissionManager.SaveUserContract(contract);
        gm.Toast($"Contract posted to the board from {path}. It is now canon.", ToastKind.Success);
        _tab = Tab.Contracts;
        Refresh();
    }

    private static HBoxContainer MakeLabeledRow(string labelText, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var l = new Label { Text = labelText };
        l.AddThemeFontSizeOverride("font_size", 14);
        row.AddChild(l);
        row.AddChild(control);
        return row;
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
