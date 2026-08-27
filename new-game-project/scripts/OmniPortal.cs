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
    private enum Tab { Inbox, Sent, Compose, Contracts, Composer, Staff, Feed, Report, Hearing, Forge }
    private Tab _tab = Tab.Inbox;
    private NpcBrain? _forgeTarget;

    private Label _forgeHeader = null!;
    private TextEdit _forgeBody = null!;
    private VBoxContainer _forgeBox = null!;

    private VBoxContainer _contractsBox = null!;
    private OptionButton _objType = null!;
    private OptionButton _objNpc = null!;
    private OptionButton _objZone = null!;
    private LineEdit _missionTitle = null!;
    private VBoxContainer _composerBox = null!;
    private VBoxContainer _reportBox = null!;
    private OptionButton _reportTarget = null!;
    private OptionButton _reportType = null!;
    private TextEdit _reportDetails = null!;
    private VBoxContainer _hearingBox = null!;

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
        AddTab(tabs, "STAFF", Tab.Staff);
        AddTab(tabs, "FEED", Tab.Feed);
        AddTab(tabs, "REPORT", Tab.Report);
        AddTab(tabs, "HEARING", Tab.Hearing);

        _listBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var listScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 260),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        listScroll.AddChild(_listBox);
        box.AddChild(listScroll);

        _forgeBox = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
        };
        _forgeBox.AddThemeConstantOverride("separation", 6);
        _forgeHeader = new Label { Text = "" };
        _forgeHeader.AddThemeFontSizeOverride("font_size", 16);
        _forgeHeader.Modulate = Color.FromHtml("ff5a4a");
        _forgeBox.AddChild(_forgeHeader);
        _forgeBody = new TextEdit
        {
            PlaceholderText = "Write their resignation. Convince HR. Convince yourself.",
            CustomMinimumSize = new Vector2(0, 200),
        };
        _forgeBox.AddChild(_forgeBody);
        var forgeSend = new Button { Text = "SEND AS THEM", CustomMinimumSize = new Vector2(180, 34) };
        forgeSend.Pressed += OnForgeSend;
        _forgeBox.AddChild(forgeSend);
        box.AddChild(_forgeBox);

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

        _reportBox = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
        };
        _reportBox.AddThemeConstantOverride("separation", 6);
        var reportHeader = new Label { Text = "ANONYMOUS HR REPORT — select a story and make it official" };
        reportHeader.AddThemeFontSizeOverride("font_size", 16);
        reportHeader.Modulate = Color.FromHtml("ff8d6b");
        _reportBox.AddChild(reportHeader);
        _reportTarget = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        _reportBox.AddChild(MakeLabeledRow("Suspect:", _reportTarget));
        _reportType = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        foreach (var type in new[] { "A KNOCKOUT", "A MURDER", "THEFT", "SABOTAGE", "HARASSMENT" })
            _reportType.AddItem(type);
        _reportBox.AddChild(MakeLabeledRow("Allegation:", _reportType));
        _reportDetails = new TextEdit
        {
            PlaceholderText = "Add just enough detail for HR to believe it was someone else's idea.",
            CustomMinimumSize = new Vector2(0, 120),
        };
        _reportBox.AddChild(_reportDetails);
        var fileReport = new Button { Text = "FILE ANONYMOUS REPORT", CustomMinimumSize = new Vector2(240, 34) };
        fileReport.Pressed += OnFileReport;
        _reportBox.AddChild(fileReport);
        box.AddChild(_reportBox);

        _hearingBox = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
        };
        _hearingBox.AddThemeConstantOverride("separation", 6);
        box.AddChild(_hearingBox);

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
        _reportTarget.Clear();
        _reportDetails.Text = "";
        if (GameMode.Instance != null)
        {
            foreach (var n in GameMode.Instance.Npcs)
            {
                _recipient.AddItem(n.NpcName);
                _objNpc.AddItem(n.NpcName);
                if (n != GameMode.Instance.Guard) _reportTarget.AddItem(n.NpcName);
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

    /// <summary>Opens the diegetic staff directory used by the personality loop.</summary>
    public void OpenStaffDirectory()
    {
        IsOpen = true;
        Visible = true;
        _tab = Tab.Staff;
        _recipient.Clear();
        _objNpc.Clear();
        Refresh();
        Input.MouseMode = Input.MouseModeEnum.Visible;
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

    public void OpenForge(NpcBrain npc)
    {
        IsOpen = true;
        Visible = true;
        _tab = Tab.Forge;
        _forgeTarget = npc;
        var persona = Personas.For(npc.NpcName);
        _forgeHeader.Text = $"FORGE — {npc.NpcName}'s email ({persona.Quirk}) → boss@omnicore";
        _forgeBody.Text = "";
        Refresh();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    /// <summary>Deterministic capture hook: submits a forged resignation letter.</summary>
    public void SubmitForgeForCapture(string letter)
    {
        if (!IsOpen || _tab != Tab.Forge) return;
        _forgeBody.Text = letter;
        OnForgeSend();
    }

    private void OnForgeSend()
    {
        if (_forgeTarget == null || string.IsNullOrWhiteSpace(_forgeBody.Text)) return;
        var target = _forgeTarget;
        string letter = _forgeBody.Text;
        _forgeTarget = null;
        GameMode.Instance?.CloseUI();
        GameMode.Instance?.OnResignationSent(target, letter);
    }

    private void Refresh()
    {
        _composeBox.Visible = _tab == Tab.Compose;
        _contractsBox.Visible = _tab == Tab.Contracts;
        _composerBox.Visible = _tab == Tab.Composer;
        _reportBox.Visible = _tab == Tab.Report;
        _hearingBox.Visible = _tab == Tab.Hearing;
        _forgeBox.Visible = _tab == Tab.Forge;
        _listBox.Visible = _tab is Tab.Inbox or Tab.Sent or Tab.Staff or Tab.Feed;

        if (_tab == Tab.Contracts) { BuildContractsList(); return; }
        if (_tab == Tab.Staff) { BuildStaffList(); return; }
        if (_tab == Tab.Feed) { BuildFeedList(); return; }
        if (_tab == Tab.Hearing) { BuildHearing(); return; }
        if (_tab == Tab.Report || _tab == Tab.Composer || _tab == Tab.Forge) return;

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

    private void BuildStaffList()
    {
        _header.Text = $"OMNIPORTAL — staff directory / 19 coworkers + Agent Red";
        foreach (var child in _listBox.GetChildren()) child.QueueFree();

        var gm = GameMode.Instance;
        if (gm == null) return;
        var profileSummary = new Label
        {
            Text = $"YOUR COMPANY PROFILE — suspicion {gm.PlayerProfile.Suspicion:F0} ({gm.PlayerProfile.SuspicionBand}) | loyalty {gm.PlayerProfile.Loyalty:F0} ({gm.PlayerProfile.LoyaltyBand}) | work {gm.PlayerProfile.Work:F0} ({gm.PlayerProfile.WorkBand}) | trust {gm.PlayerProfile.CompanyTrust:F0}\n" +
                "Keep the job believable, look loyal, and remember: a perfect frame can still make the next company suspicious.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(700, 0),
        };
        profileSummary.AddThemeFontSizeOverride("font_size", 14);
        profileSummary.Modulate = Color.FromHtml("39d97a");
        _listBox.AddChild(profileSummary);
        foreach (var n in gm.Npcs)
        {
            var profile = n.Personality;
            var persona = Personas.For(n.NpcName);
            var row = new VBoxContainer();
            var head = new Label { Text = $"{n.NpcName}  —  {n.Job} / {n.Department}" };
            head.AddThemeFontSizeOverride("font_size", 16);
            head.Modulate = n == gm.Guard ? Color.FromHtml("ff8d6b") : Color.FromHtml("ffd76a");
            row.AddChild(head);
            var traits = new Label
            {
                Text = $"{persona.Traits}\n{profile.Summary}\nTell: {Personas.BehavioralTell(n.NpcName)}\n" +
                    $"Stats: focus {n.Stats.Focus:P0} | patience {n.Stats.Patience:P0} | resilience {n.Stats.StressResilience:P0} | comfort need {n.Stats.ComfortNeed:P0}\n" +
                    $"Observe: {n.PrimaryObservation} / {n.SecondaryObservation}\n" +
                    $"Hook: {n.StaffProfile.RPGHook}\n" +
                    $"Affinity: IT {n.Stats.ITAffinity:P0} | Facilities {n.Stats.FacilitiesAffinity:P0} | Security {n.Stats.SecurityAffinity:P0}\n" +
                    $"Stress {n.Stats.CurrentStress:P0} | comfort {n.Stats.CurrentComfort:P0}\n" +
                    $"Attitude: {(n.Attitude.Active ? n.Attitude.Kind.ToString() : "comfortable")} | strength {n.Attitude.Strength:P0} | {n.Attitude.RemainingSeconds:F0}s remaining\n" +
                    $"Reaction: {n.ActiveStimulus?.ToString() ?? "none"} | activation {n.ReactionActivation:F2} | cooldown {n.ReactionCooldownRemaining:F1}s\n" +
                    $"Action: {n.ReactionAction} | {n.ReactionText}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(700, 0),
            };
            traits.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(traits);
            var sep = new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1) };
            row.AddChild(sep);
            _listBox.AddChild(row);
        }
    }

    private void BuildFeedList()
    {
        _header.Text = "OMNIPORTAL — office feed / unofficial memory";
        foreach (var child in _listBox.GetChildren()) child.QueueFree();
        var gm = GameMode.Instance;
        if (gm == null) return;
        if (gm.OfficeFeed.Count == 0)
        {
            _listBox.AddChild(new Label { Text = "No incidents logged. The office is either calm or lying." });
            return;
        }
        foreach (var memory in gm.OfficeFeed)
        {
            var row = new VBoxContainer();
            string kind = memory.Kind switch
            {
                MemoryKind.Witness => "WITNESS",
                MemoryKind.Rumor => "RUMOR",
                MemoryKind.Forged => "ANONYMOUS",
                _ => "MEMORY",
            };
            var head = new Label { Text = $"[{kind}] {memory.Subject} — {memory.Incident}  ({(int)memory.Confidence}% confidence)" };
            head.AddThemeFontSizeOverride("font_size", 14);
            head.Modulate = memory.Kind == MemoryKind.Forged ? Color.FromHtml("ff8d6b") : Color.FromHtml("ffd76a");
            row.AddChild(head);
            var narrative = new Label
            {
                Text = memory.Narrative,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(700, 0),
            };
            narrative.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(narrative);
            var sep = new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1) };
            row.AddChild(sep);
            _listBox.AddChild(row);
        }
    }

    public void FileReportForCapture(string suspect, string allegation, string details)
    {
        if (!IsOpen) Open();
        _tab = Tab.Report;
        Refresh();
        int idx = -1;
        for (int i = 0; i < _reportTarget.ItemCount; i++)
            if (_reportTarget.GetItemText(i) == suspect) { idx = i; break; }
        if (idx < 0) return;
        _reportTarget.Select(idx);
        int typeIdx = 0;
        for (int i = 0; i < _reportType.ItemCount; i++)
            if (_reportType.GetItemText(i) == allegation) { typeIdx = i; break; }
        _reportType.Select(typeIdx);
        _reportDetails.Text = details;
        OnFileReport();
    }

    private void OnFileReport()
    {
        var gm = GameMode.Instance;
        if (gm == null || _reportTarget.Selected < 0) return;
        string suspect = _reportTarget.GetItemText(_reportTarget.Selected);
        string allegation = _reportType.GetItemText(_reportType.Selected < 0 ? 0 : _reportType.Selected);
        string details = _reportDetails.Text;
        gm.FileAnonymousReport(suspect, allegation, details);
        _reportDetails.Text = "";
        _tab = Tab.Feed;
        Refresh();
    }

    public void OpenHearing()
    {
        IsOpen = true;
        Visible = true;
        _tab = Tab.Hearing;
        Refresh();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void BuildHearing()
    {
        _header.Text = $"OMNIPORTAL — HR hearing: {GameMode.Instance?.CaseSuspectName} / {GameMode.Instance?.CaseAllegation}";
        foreach (var child in _hearingBox.GetChildren()) child.QueueFree();
        var gm = GameMode.Instance;
        if (gm == null) return;
        var intro = new Label
        {
            Text = "Contradictions weaken the case. Coaching makes a witness sound rehearsed. Appeal when the story no longer holds.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(700, 0),
        };
        intro.AddThemeFontSizeOverride("font_size", 14);
        _hearingBox.AddChild(intro);
        for (int i = 0; i < gm.CaseTestimonies.Count; i++)
        {
            var testimony = gm.CaseTestimonies[i];
            var row = new VBoxContainer();
            var statement = new Label
            {
                Text = $"[{i + 1}] {testimony.Statement}\nConfidence: {(int)testimony.Confidence}%" +
                    (testimony.Contradictory ? "  [LOCATION CONTRADICTION]" : ""),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(700, 0),
            };
            statement.AddThemeFontSizeOverride("font_size", 13);
            statement.Modulate = testimony.Contradictory ? Color.FromHtml("ff8d6b") : Colors.White;
            row.AddChild(statement);
            var actions = new HBoxContainer();
            var challenge = new Button { Text = "CHALLENGE", Disabled = testimony.Challenged };
            var index = i;
            challenge.Pressed += () => { gm.ChallengeTestimony(index); BuildHearing(); };
            actions.AddChild(challenge);
            var coach = new Button { Text = "COACH", Disabled = testimony.Coached };
            coach.Pressed += () => { gm.CoachTestimony(index); BuildHearing(); };
            actions.AddChild(coach);
            row.AddChild(actions);
            _hearingBox.AddChild(row);
        }
        var appeal = new Button { Text = "FILE APPEAL", CustomMinimumSize = new Vector2(180, 34) };
        appeal.Pressed += () => gm.AppealCase();
        _hearingBox.AddChild(appeal);
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
