using Godot;

/// <summary>
/// Police interview (bible: body found → police called → everyone interviewed).
/// Three typed answers, judged by a safeguarded filter (length + suspicious
/// keywords) against the accumulated case. 2+ holds up → released with the
/// case burning hotter; fewer → arrested ending.
/// </summary>
public partial class PoliceInterview : CanvasLayer
{
    public bool IsOpen { get; private set; }

    private Label _header = null!;
    private Label _question = null!;
    private Label _progress = null!;
    private TextEdit _answer = null!;
    private string _victim = "";
    private string _spotName = "";
    private int _index;
    private int _passes;

    private static readonly string[] SuspiciousWords =
    {
        "kill", "murder", "dead", "body", "i did it", "buried", "shredded them",
        "sorry about", "whoops", "accidentally on purpose",
    };

    public override void _Ready()
    {
        Layer = 12;
        Visible = false;

        var dim = new ColorRect { Color = new Color(0.01f, 0.01f, 0.02f, 0.94f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(720, 420);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.08f, 0.98f),
            BorderColor = Color.FromHtml("4a7dc0"),
            BorderWidthLeft = 4,
            BorderWidthRight = 4,
            BorderWidthTop = 4,
            BorderWidthBottom = 4,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 14,
            ContentMarginBottom = 14,
        });
        AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

        _header = new Label { Text = "POLICE INTERVIEW" };
        _header.AddThemeFontSizeOverride("font_size", 26);
        _header.Modulate = Color.FromHtml("4a7dc0");
        box.AddChild(_header);

        _progress = new Label { Text = "Question 1 of 3" };
        _progress.AddThemeFontSizeOverride("font_size", 13);
        _progress.Modulate = new Color(1, 1, 1, 0.55f);
        box.AddChild(_progress);

        _question = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 60),
        };
        _question.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(_question);

        _answer = new TextEdit
        {
            PlaceholderText = "Answer carefully. They read everything…",
            CustomMinimumSize = new Vector2(0, 140),
        };
        box.AddChild(_answer);

        var submit = new Button { Text = "SUBMIT STATEMENT", CustomMinimumSize = new Vector2(200, 36) };
        submit.Pressed += OnSubmit;
        box.AddChild(submit);
    }

    /// <summary>Builds the question set from the discovery context.</summary>
    public void Prepare(string victim, string spotName)
    {
        _victim = victim;
        _spotName = spotName;
        _index = 0;
        _passes = 0;
    }

    public void Open()
    {
        IsOpen = true;
        Visible = true;
        _index = 0;
        _passes = 0;
        ShowQuestion();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        IsOpen = false;
        Visible = false;
    }

    private void ShowQuestion()
    {
        _progress.Text = $"Question {_index + 1} of 3";
        _question.Text = _index switch
        {
            0 => $"Where were you between 09:00 and 09:30? The office was watching. Mostly.",
            1 => $"{_victim} has been found in the {_spotName}. Explain why that is not as bad as it sounds.",
            _ => "Final question. Describe your working relationship with {_victim}. Choose every word like it matters.".Replace("{_victim}", _victim),
        };
        _answer.Text = "";
    }

    /// <summary>Deterministic capture hook: submits the current answer without keyboard focus.</summary>
    public void SubmitForCapture(string answer)
    {
        if (!IsOpen) return;
        _answer.Text = answer;
        OnSubmit();
    }

    private void OnSubmit()
    {
        string answer = _answer.Text.Trim();
        string low = answer.ToLowerInvariant();

        bool plausible = answer.Length > 40
            && !SuspiciousWords.Any(low.Contains)
            && (low.Contains("meeting") || low.Contains("desk") || low.Contains("coffee")
                || low.Contains("printer") || low.Contains("email") || low.Contains("break")
                || low.Contains("was") || low.Contains("were"));

        if (plausible) _passes++;

        _index++;
        if (_index < 3) ShowQuestion();
        else
        {
            Close();
            GameMode.Instance?.OnInterviewResolved(_passes, 3);
        }
    }

    public override void _Input(InputEvent e)
    {
        if (!IsOpen) return;
        // no escaping a police interview
        if (e.IsActionPressed("ui_cancel")) GetViewport().SetInputAsHandled();
    }
}
