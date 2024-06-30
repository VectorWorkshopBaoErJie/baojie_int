using Godot;
using System;
using System.Linq;
using GodotInk;
using System.IO;
using System.Reflection;
using System.Diagnostics;

public partial class Game : VBoxContainer
{
    private static readonly StringName CHOICE_INDEX_META = "Index";

    private Button startButton = null!;
    private Button stopButton = null!;
    private Button clearButton = null!;

    private Label storyNameLabel = null!;
    private VBoxContainer storyText = null!;
    private VBoxContainer storyChoices = null!;
    private ScrollContainer scroll = null!;

    // private EditorFileDialog fileDialog = null!;

    private string storyPath = "";
    private InkStory? story;
    private bool storyStarted;

    private string backupSave = "";

    public override void _Ready()
    {
        base._Ready();

        CustomMinimumSize = new Vector2(0.0f, 228.0f);

        story = GD.Load<InkStory>("res://Examples/TheIntercept.ink");

        // 初始化按钮        
        startButton = GetNode<Button>("Container/Left/Top/StartButton");
        stopButton = GetNode<Button>("Container/Left/Top/StopButton");
        clearButton = GetNode<Button>("Container/Left/Top/ClearButton");

        // 绑定UI事件
        startButton.Pressed += StartStory;
        stopButton.Pressed += StopStory;
        clearButton.Pressed += () => ClearStory(false);

        // 初始化底部文本
        storyText = GetNode<VBoxContainer>("Container/Left/Scroll/Margin/StoryText");
        storyChoices = GetNode<VBoxContainer>("Container/Right/StoryChoices");
        scroll = GetNode<ScrollContainer>("Container/Left/Scroll");


        // Update UI.
        UpdateTop();
    }

    private void UpdateTop()
    {


        bool hasStory = story != null;
        if (startButton != null)
        {
            // storyNameLabel.Text = hasStory ? storyPath : string.Empty;

            startButton.Visible = hasStory && !storyStarted;
            stopButton.Visible = hasStory && storyStarted;
            clearButton.Visible = hasStory;
            clearButton.Disabled = storyText.GetChildCount() <= 0;

            storyChoices.GetParent<Control>().Visible = hasStory;
        }

    }

    private void StartStory()
    {
        if (story == null) return;

        storyStarted = true;
        ContinueStory();

        UpdateTop();
    }

    private void StopStory()
    {
        StopStory(false);
    }

    private void StopStory(bool setStoryToNull)
    {
        storyStarted = false;

        try
        {
            story?.ResetState();
        }
        catch (ObjectDisposedException)
        {
            story = null;
        }

        if (setStoryToNull)
            story = null;

        ClearStory(true);
    }

    private void ClearStory(bool clearChoices)
    {
        RemoveAllStoryContent();
        if (clearChoices)
            RemoveAllChoices();

        UpdateTop();
    }

    private void ContinueStory()
    {
        if (story == null) return;
        if (!story.CanContinue) return;

        string currentText = story.ContinueMaximally().Trim();

        if (currentText.Length > 0)
        {
            var newLine = new Label()
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = currentText,
            };
            AddToStory(newLine);

            if (story.CurrentTags.Count > 0)
            {
                newLine = new Label()
                {
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = $"# {string.Join(", ", story.CurrentTags)}",
                };
                newLine.AddThemeColorOverride("font_color", GetThemeColor("font_color_disabled", "Button"));
                AddToStory(newLine);
            }
        }

        foreach (InkChoice choice in story.CurrentChoices)
        {
            Button button = new() { Text = choice.Text };
            button.SetMeta(CHOICE_INDEX_META, choice.Index);

            button.Connect(BaseButton.SignalName.Pressed, Callable.From(ClickChoice));

            storyChoices.AddChild(button);
        }

        backupSave = storyStarted ? story.SaveState() : "";
    }

    private void ClickChoice()
    {
        if (storyChoices.GetChildren().OfType<Button>().First(b => b.ButtonPressed) is not { } button) return;
        if (!button.HasMeta(CHOICE_INDEX_META)) return;

        try
        {
            ClickChoice(button.GetMeta(CHOICE_INDEX_META).As<int>());
        }
        catch
        {
            story?.LoadState(backupSave);
            try
            {
                ClickChoice(button.GetMeta(CHOICE_INDEX_META).As<int>());
            }
            catch
            {
                StopStory(true);
            }
        }
    }

    private void ClickChoice(int idx)
    {
        if (story == null) return;

        story.ChooseChoiceIndex(idx);

        RemoveAllChoices();
        AddToStory(new HSeparator());

        ContinueStory();
    }

    private async void AddToStory(CanvasItem item)
    {
        storyText.AddChild(item);
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");
        scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
    }

    private void RemoveAllStoryContent()
    {
        foreach (Node n in storyText.GetChildren())
            n.QueueFree();
    }

    private void RemoveAllChoices()
    {
        foreach (Button button in storyChoices.GetChildren().OfType<Button>())
            button.QueueFree();
    }

    public void WhenInkResourceReimported()
    {
        StopStory(true);
    }
}
