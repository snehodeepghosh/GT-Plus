using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GtPlus.Views;

/// <summary>works out how long an ImageSequence clip has to run for its frames to play at a given rate: an ImageSequence animation fits the whole sequence into its Duration, so the clip length is frameCount / fps</summary>
public partial class SequenceLengthWindow : Window
{
    private readonly int _frameCount;

    /// <summary>true for an ImageSequenceLoop, which vMix times by Speed instead of Duration</summary>
    private readonly bool _loop;

    /// <summary>frame rate the user settled on, so the caller can offer it again next time</summary>
    public double Fps { get; private set; }

    /// <summary>clip length in seconds the frame count and frame rate imply</summary>
    public double Duration => _frameCount / Math.Max(1.0, Fps);

    /// <summary>the ImageSequenceLoop Speed that shows one sequence frame per title frame: vMix moves the position Speed / frames each frame and shows frame round((frames - 1) * position), so the step must be 1 / (frames - 1)</summary>
    public double LoopSpeed => _frameCount <= 1 ? 1.0 : Math.Round(_frameCount / (_frameCount - 1.0), 4);

    // parameterless ctor for the XAML runtime loader, the app always uses the one below
    public SequenceLengthWindow() : this("Sequence", 1, 60, 1) { }

    /// <param name="objectName">the animated object, shown so a multi-clip edit is unambiguous</param>
    /// <param name="frameCount">frames behind the sequence's anchor</param>
    /// <param name="defaultFps">frame rate to start on, 60 unless the user has already picked one</param>
    /// <param name="currentDuration">the clip's present length, shown for comparison</param>
    /// <param name="loop">an ImageSequenceLoop: the dialog then explains the Speed it sets rather than a Duration</param>
    /// <param name="currentSpeed">the loop's present Speed, shown for comparison</param>
    public SequenceLengthWindow(string objectName, int frameCount, double defaultFps, double currentDuration,
                                bool loop = false, double currentSpeed = 1)
    {
        _loop = loop;
        InitializeComponent();

        _frameCount = Math.Max(1, frameCount);
        Fps         = defaultFps;

        HeaderText.Text  = string.IsNullOrEmpty(objectName) ? "Image sequence" : objectName;
        FramesText.Text  = $"{_frameCount} frame{(_frameCount == 1 ? "" : "s")} in this sequence";
        CurrentText.Text = loop ? $"Speed is currently {Fmt(currentSpeed)}." : $"Clip is currently {Fmt(currentDuration)} s long.";

        FpsBox.Value = (decimal)defaultFps;
        UpdateResult();
    }

    private void Fps_Changed(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (FpsBox.Value is { } value) Fps = (double)value;
        UpdateResult();
    }

    private void UpdateResult()
    {
        if (ResultText is null) return;
        ResultText.Text = _loop
            ? $"Speed {Fmt(LoopSpeed)} plays one frame per title frame: each loop lasts {_frameCount} ÷ {Fmt(Fps)} fps = {Fmt(Duration)} s"
            : $"{_frameCount} ÷ {Fmt(Fps)} fps = {Fmt(Duration)} s";
    }

    private static string Fmt(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        if (FpsBox.Value is { } value) Fps = (double)value;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
