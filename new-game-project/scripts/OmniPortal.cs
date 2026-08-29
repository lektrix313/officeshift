using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class OmniPortal : CanvasLayer
{
    public bool IsOpen { get; private set; }
    private PanelContainer _window = null!;
    private Label _header = null!, _status = null!, _mailCount = null!, _provider = null!;
    private VBoxContainer _listBox = null!, _composeBox = null!, _reportBox = null!, _hearingBox = null!;
    private ScrollContainer _listScroll = null!;
    private OptionButton _recipient = null!, _reportTarget = null!, _reportType = null!;
    private LineEdit _subject = null!;
    private TextEdit _body = null!, _forgeBody = null!, _reportDetails = null!;
    private VBoxContainer _forgeBox = null!;
    private enum Tab { Inbox, Sent, Compose, Staff, Feed, Report, Hearing, Forge }
    private Tab _tab = Tab.Inbox;
    private NpcBrain? _forgeTarget;
    private string _computerId = "company";
    private NpcBrain? _computerOwner;

    public override void _Ready()
    {
        MailStore.MailArrived += OnMailArrived;
        Layer = 10; Visible = false;
        var dim = new ColorRect { Color = new Color(.01f, .02f, .04f, .84f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(dim);
        _window = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        _window.SetAnchorsPreset(Control.LayoutPreset.Center);
        _window.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.KeepWidth, 0);
        _window.AnchorLeft = .06f; _window.AnchorRight = .94f; _window.AnchorTop = .06f; _window.AnchorBottom = .94f;
        _window.OffsetLeft = 0; _window.OffsetRight = 0; _window.OffsetTop = 0; _window.OffsetBottom = 0;
        _window.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Color.FromHtml("111b27"), BorderColor = Color.FromHtml("0af0ff"), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 3, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 });
        AddChild(_window);
        var box = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 8); _window.AddChild(box);
        var title = new HBoxContainer(); _header = new Label { Text = "OMNIPORTAL — inbox", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _header.AddThemeFontSizeOverride("font_size", 20); _header.Modulate = Color.FromHtml("0af0ff"); title.AddChild(_header); _status = new Label { Text = "OFFLINE" }; _status.Modulate = Color.FromHtml("39d97a"); title.AddChild(_status); box.AddChild(title);
        _provider = new Label { Text = "LLM: " + NpcChatService.ProviderStatus, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _provider.Modulate = Color.FromHtml("8aa3b8"); box.AddChild(_provider);
        var tabs = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; tabs.AddThemeConstantOverride("separation", 3); box.AddChild(tabs);
        AddTab(tabs, "INBOX", Tab.Inbox); AddTab(tabs, "SENT", Tab.Sent); AddTab(tabs, "COMPOSE", Tab.Compose); AddTab(tabs, "STAFF", Tab.Staff);
        _listBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _listScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 120), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _listScroll.AddChild(_listBox); box.AddChild(_listScroll);
        BuildCompose(box); BuildForge(box); BuildReport(box); _hearingBox = new VBoxContainer { Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; box.AddChild(_hearingBox);
        _mailCount = new Label { Text = "" }; _mailCount.Modulate = new Color(1, 1, 1, .55f); box.AddChild(_mailCount); var hint = new Label { Text = "Esc / E: close  ·  Tab: switch sections  ·  Mailguard active" }; hint.Modulate = new Color(1, 1, 1, .5f); box.AddChild(hint);
    }

    private void BuildCompose(VBoxContainer parent)
    {
        _composeBox = new VBoxContainer { Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _composeBox.AddThemeConstantOverride("separation", 7);
        _recipient = new OptionButton { CustomMinimumSize = new Vector2(0, 32), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _composeBox.AddChild(MakeRow("TO", _recipient));
        _subject = new LineEdit { PlaceholderText = "Subject", CustomMinimumSize = new Vector2(0, 32) }; _composeBox.AddChild(_subject);
        _body = new TextEdit { PlaceholderText = "Type a harmless in-game workplace email…", SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 90) }; _composeBox.AddChild(_body);
        var send = new Button { Text = "SEND EMAIL", CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; send.Pressed += OnSend; _composeBox.AddChild(send); parent.AddChild(_composeBox);
    }
    private void BuildReport(VBoxContainer parent) { _reportBox = new VBoxContainer { Visible = false }; _reportTarget = new OptionButton(); _reportBox.AddChild(MakeRow("SUSPECT", _reportTarget)); _reportType = new OptionButton(); foreach (var x in new[] { "A KNOCKOUT", "A MURDER", "THEFT", "SABOTAGE", "HARASSMENT" }) _reportType.AddItem(x); _reportBox.AddChild(MakeRow("ALLEGATION", _reportType)); _reportDetails = new TextEdit { CustomMinimumSize = new Vector2(0, 100) }; _reportBox.AddChild(_reportDetails); var b = new Button { Text = "FILE REPORT", CustomMinimumSize = new Vector2(0, 40) }; b.Pressed += OnFileReport; _reportBox.AddChild(b); parent.AddChild(_reportBox); }
    private void BuildForge(VBoxContainer parent) { _forgeBox = new VBoxContainer { Visible = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; _forgeBody = new TextEdit { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; _forgeBox.AddChild(_forgeBody); var b = new Button { Text = "SEND AS THEM", CustomMinimumSize = new Vector2(0, 40) }; b.Pressed += OnForgeSend; _forgeBox.AddChild(b); parent.AddChild(_forgeBox); }
    private static HBoxContainer MakeRow(string text, Control control) { var row = new HBoxContainer(); row.AddChild(new Label { Text = text, CustomMinimumSize = new Vector2(90, 0) }); control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(control); return row; }
    private void AddTab(HBoxContainer parent, string label, Tab tab) { var b = new Button { Text = label, FocusMode = Control.FocusModeEnum.All, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; b.Pressed += () => { _tab = tab; Refresh(); }; parent.AddChild(b); }

    public void Open() { OpenForComputer("company", null); }
    public void OpenForComputer(string computerId, NpcBrain? owner)
    {
        _computerId = computerId; _computerOwner = owner; MailStore.ActiveMailbox = computerId;
        IsOpen = true; Visible = true; _tab = Tab.Inbox; _status.Text = owner == null ? "ONLINE · company computer" : $"ONLINE · {owner.NpcName}'s workstation · {owner.DeskId}"; _recipient.Clear(); _reportTarget.Clear(); if (GameMode.Instance != null) foreach (var n in GameMode.Instance.Npcs) { _recipient.AddItem(n.NpcName); if (n != GameMode.Instance.Guard) _reportTarget.AddItem(n.NpcName); } Refresh(); Input.MouseMode = Input.MouseModeEnum.Visible; }
    private void OnMailArrived(MailMsg mail)
    {
        if (IsOpen && mail.Mailbox == _computerId) CallDeferred(nameof(Refresh));
    }

    public override void _ExitTree() => MailStore.MailArrived -= OnMailArrived;

    private void FocusFirstControl() { if (IsOpen && _tab == Tab.Compose) _recipient.GrabFocus(); }
    public void Close() { IsOpen = false; Visible = false; }
    public void OpenStaffDirectory() { if (!IsOpen) Open(); _tab = Tab.Staff; Refresh(); }
    public void FileReportForCapture(string suspect, string allegation, string details) { GameMode.Instance?.FileAnonymousReport(suspect, allegation, details); }
    public override void _Input(InputEvent e) { if (!IsOpen) return; if (e.IsActionPressed("ui_cancel") || (e.IsActionPressed("interact") && GetViewport().GuiGetFocusOwner() == null)) { GetViewport().SetInputAsHandled(); GameMode.Instance?.CloseUI(); } }
    public void OpenForge(NpcBrain npc) { IsOpen = true; Visible = true; _tab = Tab.Forge; _forgeTarget = npc; Refresh(); Input.MouseMode = Input.MouseModeEnum.Visible; }
    public void SubmitForgeForCapture(string letter) { if (!IsOpen || _tab != Tab.Forge) return; _forgeBody.Text = letter; OnForgeSend(); }
    private void OnForgeSend() { if (_forgeTarget == null || string.IsNullOrWhiteSpace(_forgeBody.Text)) return; var target = _forgeTarget; var letter = _forgeBody.Text; _forgeTarget = null; GameMode.Instance?.CloseUI(); GameMode.Instance?.OnResignationSent(target, letter); }

    private void Refresh()
    {
        _provider.Text = "LLM: " + NpcChatService.ProviderStatus + " · " + NpcChatService.ConfigurationHint + (string.IsNullOrWhiteSpace(NpcChatService.LastError) ? "" : "  · request failed; using safe fallback");
        _listBox.Visible = _tab is Tab.Inbox or Tab.Sent or Tab.Staff or Tab.Feed; _composeBox.Visible = _tab == Tab.Compose; _forgeBox.Visible = _tab == Tab.Forge; _reportBox.Visible = _tab == Tab.Report; _hearingBox.Visible = _tab == Tab.Hearing; _mailCount.Text = $"{_computerId} mailbox · inbox {new List<MailMsg>(MailStore.Inbox).Count} · sent {new List<MailMsg>(MailStore.Sent).Count}"; _listScroll.ScrollVertical = 0;
        if (_tab == Tab.Staff) { BuildStaffList(); return; } if (_tab == Tab.Feed) { BuildFeedList(); return; } if (_tab == Tab.Report || _tab == Tab.Hearing || _tab == Tab.Forge) { if (_tab == Tab.Hearing) BuildHearing(); return; }
        foreach (var child in _listBox.GetChildren()) child.QueueFree(); _header.Text = _tab == Tab.Inbox ? "OMNIPORTAL — inbox" : "OMNIPORTAL — sent"; var items = new List<MailMsg>(_tab == Tab.Inbox ? MailStore.Inbox : MailStore.Sent); items.Reverse(); if (items.Count == 0) { _listBox.AddChild(new Label { Text = _tab == Tab.Inbox ? "Inbox zero. Suspicious." : "No sent messages yet." }); return; } foreach (var m in items) { var row = new VBoxContainer(); var head = new Label { Text = $"{m.From} → {m.To}  |  {m.Subject}" }; head.Modulate = Color.FromHtml("ffd76a"); row.AddChild(head); row.AddChild(new Label { Text = m.Body.Replace("\\n", "\n"), AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); _listBox.AddChild(row); }
    }
    private void BuildFeedList() { foreach (var child in _listBox.GetChildren()) child.QueueFree(); _header.Text = "OMNIPORTAL — office feed"; var gm = GameMode.Instance; if (gm == null) return; foreach (var memory in gm.OfficeFeed) _listBox.AddChild(new Label { Text = $"[{memory.Kind}] {memory.Subject} — {memory.Incident}\\n{memory.Narrative}", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); }
    private void BuildHearing() { foreach (var child in _hearingBox.GetChildren()) child.QueueFree(); var gm = GameMode.Instance; if (gm == null) return; _header.Text = $"OMNIPORTAL — HR hearing: {gm.CaseSuspectName}"; _hearingBox.AddChild(new Label { Text = "Challenge testimony in the fictional case, then appeal.", AutowrapMode = TextServer.AutowrapMode.WordSmart }); var appeal = new Button { Text = "FILE APPEAL" }; appeal.Pressed += gm.AppealCase; _hearingBox.AddChild(appeal); }
    private void OnFileReport() { var gm = GameMode.Instance; if (gm == null || _reportTarget.Selected < 0) return; gm.FileAnonymousReport(_reportTarget.GetItemText(_reportTarget.Selected), _reportType.GetItemText(_reportType.Selected < 0 ? 0 : _reportType.Selected), _reportDetails.Text); _tab = Tab.Feed; Refresh(); }
    private void BuildStaffList() { foreach (var child in _listBox.GetChildren()) child.QueueFree(); _header.Text = "OMNIPORTAL — staff directory"; var gm = GameMode.Instance; if (gm == null) return; foreach (var n in gm.Npcs) { var p = Personas.For(n.NpcName); var l = new Label { Text = $"{n.NpcName} — {n.Job} / {n.Department}\n{p.Traits}\nTell: {Personas.BehavioralTell(n.NpcName)}\n{p.Quirk}\n{SocialReadout(gm, n.NpcName)}", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; l.AddThemeFontSizeOverride("font_size", 13); _listBox.AddChild(l); _listBox.AddChild(new HSeparator()); } }

    /// <summary>What this NPC privately thinks of the room, and what they currently believe.</summary>
    private static string SocialReadout(GameMode gm, string holder)
    {
        var feelings = new List<string>();
        foreach (var (target, opinion) in gm.Ledger.OpinionsOf(holder))
        {
            if (target == MailStore.PlayerAddress || opinion.Label == "neutral") continue;
            feelings.Add($"{target}: {opinion.Label}");
        }
        var player = gm.Ledger.Of(holder, MailStore.PlayerAddress);
        var beliefs = gm.Ledger.ClaimsHeldBy(holder).Where(c => c.Confidence > 0.2f).Select(c => "  - " + c.Summary).ToList();
        var text = $"Thinks of you: {player.Label} ({player.Detail})";
        if (feelings.Count > 0) text += "\nFeels: " + string.Join(", ", feelings);
        if (beliefs.Count > 0) text += "\nBelieves:\n" + string.Join("\n", beliefs);
        return text;
    }

    public void OpenHearing() { if (!IsOpen) Open(); _tab = Tab.Hearing; Refresh(); }
    public void ComposeTo(string npcName, string subject, string body) { if (!IsOpen) Open(); _tab = Tab.Compose; Refresh(); for (int i = 0; i < _recipient.ItemCount; i++) if (_recipient.GetItemText(i) == npcName) { _recipient.Select(i); break; } _subject.Text = subject; _body.Text = body; OnSend(); }
    private void OnSend()
    {
        var gm = GameMode.Instance;
        if (gm == null || _recipient.Selected < 0 || string.IsNullOrWhiteSpace(_body.Text)) return;

        // Mailguard only stops genuinely out-of-fiction requests now. In-world espionage --
        // keycards, blackmail, the blueprints, the body in the closet -- is the whole game.
        if (NpcChatService.OutgoingBlocked(_body.Text))
        {
            gm.Toast("MAILGUARD blocked that one. Keep it in the fiction.", ToastKind.Warn);
            return;
        }

        string to = _recipient.GetItemText(_recipient.Selected);
        string subject = string.IsNullOrWhiteSpace(_subject.Text) ? "(no subject)" : _subject.Text;
        MailStore.Send(new MailMsg(MailStore.PlayerAddress, to, subject, _body.Text, MailStore.Stamp(), true, _computerId));

        var npc = gm.Npcs.Find(n => n.NpcName == to);
        if (npc != null)
        {
            // telling someone about a colleague plants a belief, weighted by what they
            // think of you -- lie to someone who distrusts you and it simply will not take
            var claim = ClaimParser.Extract(_body.Text, gm.Npcs.Select(n => n.NpcName), to);
            if (claim != null)
            {
                var planted = gm.Ledger.Tell(to, claim.Value.About, claim.Value.Kind, MailStore.PlayerAddress);
                if (planted != null)
                    gm.Toast($"{to} reads that. Believes it {planted.Confidence:P0}.", ToastKind.Info);
            }
            // the reply must come back to THIS workstation's mailbox, not the company one
            if (NpcChatService.EmailAsync(npc, subject, _body.Text, Personas.ContextLine(npc, gm), _computerId))
            {
                gm.Toast($"Mail sent to {to}. Reply queued for {NpcChatService.EmailCooldownSeconds:F0} seconds.", ToastKind.Info);
                _status.Text = $"QUEUED - {to} - reply in {NpcChatService.EmailCooldownSeconds:F0}s";
            }
            else
            {
                gm.Toast($"{to} is still reading your last one. Give them a minute.", ToastKind.Warn);
                _status.Text = $"THROTTLED - {to} is still replying";
            }
            _provider.Text = "LLM: " + NpcChatService.ProviderStatus;
        }

        _subject.Text = "";
        _body.Text = "";
        _tab = Tab.Sent;
        Refresh();
    }
}
