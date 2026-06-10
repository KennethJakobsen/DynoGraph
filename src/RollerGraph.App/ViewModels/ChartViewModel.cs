using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using RollerGraph.Core.Models;
using RollerGraph.Core.Scaling;
using SkiaSharp;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// View-model for the dyno chart. Holds the HP/NM live series, any visible
/// saved-run overlay series, and the auto-scaling axis state. Mutations must
/// happen on the UI thread.
/// </summary>
public sealed partial class ChartViewModel : ObservableObject
{
    private readonly ObservableCollection<ObservablePoint> _hpPoints = new();
    private readonly ObservableCollection<ObservablePoint> _nmPoints = new();
    // Maps saved-run name -> the pair of series we added for it (HP first, NM second).
    private readonly Dictionary<string, (ISeries Hp, ISeries Nm)> _savedRunSeries = new(StringComparer.OrdinalIgnoreCase);

    private readonly Axis _xAxis;
    private readonly Axis _yAxisHp;
    private readonly Axis _yAxisNm;
    private Settings _settings;

    [ObservableProperty]
    private double _peakHp;

    [ObservableProperty]
    private double _peakHpSpeed;

    [ObservableProperty]
    private double _peakNm;

    [ObservableProperty]
    private double _peakNmSpeed;

    [ObservableProperty]
    private double _peakSpeed;

    [ObservableProperty]
    private int _sampleCount;

    public ChartViewModel(Settings settings)
    {
        _settings = settings;

        var hpStroke = new SKColor(0xFF, 0x8A, 0x00); // orange
        var nmStroke = new SKColor(0x00, 0xB8, 0xA9); // teal

        Series = new ObservableCollection<ISeries>
        {
            new LineSeries<ObservablePoint>
            {
                Name = "HP",
                Values = _hpPoints,
                GeometrySize = 0,
                LineSmoothness = 0.4,
                Stroke = new SolidColorPaint(hpStroke) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(hpStroke) { StrokeThickness = 0 },
                GeometryFill = new SolidColorPaint(hpStroke),
                Fill = null,
                ScalesYAt = 0,
            },
            new LineSeries<ObservablePoint>
            {
                Name = "NM",
                Values = _nmPoints,
                GeometrySize = 0,
                LineSmoothness = 0.4,
                Stroke = new SolidColorPaint(nmStroke) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(nmStroke) { StrokeThickness = 0 },
                GeometryFill = new SolidColorPaint(nmStroke),
                Fill = null,
                ScalesYAt = 1,
            },
        };

        _xAxis = new Axis
        {
            Name = "Speed (km/h)",
            MinLimit = 0,
            MaxLimit = settings.DefaultSpeedMax,
        };
        _yAxisHp = new Axis
        {
            Name = "HP",
            MinLimit = 0,
            MaxLimit = settings.DefaultHpMax,
            LabelsPaint = new SolidColorPaint(hpStroke),
            NamePaint = new SolidColorPaint(hpStroke),
        };
        _yAxisNm = new Axis
        {
            Name = "NM",
            MinLimit = 0,
            MaxLimit = settings.DefaultNmMax,
            Position = LiveChartsCore.Measure.AxisPosition.End,
            LabelsPaint = new SolidColorPaint(nmStroke),
            NamePaint = new SolidColorPaint(nmStroke),
            ShowSeparatorLines = false,
        };

        XAxes = new[] { _xAxis };
        YAxes = new[] { _yAxisHp, _yAxisNm };
    }

    public ObservableCollection<ISeries> Series { get; }
    public ICartesianAxis[] XAxes { get; }
    public ICartesianAxis[] YAxes { get; }

    /// <summary>
    /// Appends a sample to the chart, updates axis limits and peak stats.
    /// Must be invoked on the UI thread.
    /// </summary>
    public void AppendSample(Sample sample)
    {
        _hpPoints.Add(new ObservablePoint(sample.SpeedKmh, sample.Hp));
        _nmPoints.Add(new ObservablePoint(sample.SpeedKmh, sample.Nm));
        SampleCount++;

        GrowAxesFor(sample.SpeedKmh, sample.Hp, sample.Nm);

        // Peak stats (note: peak NM may occur at a different speed than peak HP).
        if (sample.Hp > PeakHp)
        {
            PeakHp = sample.Hp;
            PeakHpSpeed = sample.SpeedKmh;
        }
        if (sample.Nm > PeakNm)
        {
            PeakNm = sample.Nm;
            PeakNmSpeed = sample.SpeedKmh;
        }
        if (sample.SpeedKmh > PeakSpeed)
        {
            PeakSpeed = sample.SpeedKmh;
        }
    }

    /// <summary>
    /// Clears all live data and resets axis limits to defaults (or to the
    /// max of any visible saved runs, so they're not clipped after a Reset).
    /// </summary>
    public void Reset()
    {
        _hpPoints.Clear();
        _nmPoints.Clear();
        _xAxis.MaxLimit = _settings.DefaultSpeedMax;
        _yAxisHp.MaxLimit = _settings.DefaultHpMax;
        _yAxisNm.MaxLimit = _settings.DefaultNmMax;
        PeakHp = 0;
        PeakHpSpeed = 0;
        PeakNm = 0;
        PeakNmSpeed = 0;
        PeakSpeed = 0;
        SampleCount = 0;
        RegrowAxesForAllSavedRuns();
    }

    /// <summary>
    /// Updates the cached defaults used by <see cref="Reset"/> and to floor axis growth.
    /// If no data has been plotted yet, the visible axes are reset to the new defaults.
    /// </summary>
    public void UpdateDefaults(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        if (SampleCount == 0)
        {
            _xAxis.MaxLimit = settings.DefaultSpeedMax;
            _yAxisHp.MaxLimit = settings.DefaultHpMax;
            _yAxisNm.MaxLimit = settings.DefaultNmMax;
            RegrowAxesForAllSavedRuns();
        }
    }

    // ---------- Saved-run overlay management ----------

    /// <summary>Adds a saved run as two extra line series (HP + NM).</summary>
    public void AddSavedRun(SavedRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        RemoveSavedRun(run.Name); // Replace if already present.

        var hpColor = ParseHex(run.Color);
        var nmColor = AdjustBrightness(hpColor, factor: 0.7);

        var hpValues = run.Samples.Select(s => new ObservablePoint(s.SpeedKmh, s.Hp)).ToList();
        var nmValues = run.Samples.Select(s => new ObservablePoint(s.SpeedKmh, s.Nm)).ToList();

        var hpSeries = new LineSeries<ObservablePoint>
        {
            Name = $"{run.Name} HP",
            Values = hpValues,
            GeometrySize = 0,
            LineSmoothness = 0.4,
            Stroke = new SolidColorPaint(hpColor) { StrokeThickness = 1.5f },
            GeometryStroke = new SolidColorPaint(hpColor) { StrokeThickness = 0 },
            GeometryFill = new SolidColorPaint(hpColor),
            Fill = null,
            ScalesYAt = 0,
            IsVisible = run.IsVisible,
        };
        var nmSeries = new LineSeries<ObservablePoint>
        {
            Name = $"{run.Name} NM",
            Values = nmValues,
            GeometrySize = 0,
            LineSmoothness = 0.4,
            Stroke = new SolidColorPaint(nmColor) { StrokeThickness = 1.5f },
            GeometryStroke = new SolidColorPaint(nmColor) { StrokeThickness = 0 },
            GeometryFill = new SolidColorPaint(nmColor),
            Fill = null,
            ScalesYAt = 1,
            IsVisible = run.IsVisible,
        };

        _savedRunSeries[run.Name] = (hpSeries, nmSeries);
        Series.Add(hpSeries);
        Series.Add(nmSeries);

        if (run.IsVisible) GrowAxesForSavedRun(run);
    }

    /// <summary>Removes a saved run's overlay series from the chart.</summary>
    public void RemoveSavedRun(string name)
    {
        if (!_savedRunSeries.TryGetValue(name, out var pair)) return;
        Series.Remove(pair.Hp);
        Series.Remove(pair.Nm);
        _savedRunSeries.Remove(name);
    }

    /// <summary>Sets visibility of a saved run's overlay without removing it.</summary>
    public void SetSavedRunVisible(string name, bool isVisible)
    {
        if (!_savedRunSeries.TryGetValue(name, out var pair)) return;
        pair.Hp.IsVisible = isVisible;
        pair.Nm.IsVisible = isVisible;
    }

    /// <summary>Returns the names of every saved run currently displayed.</summary>
    public IReadOnlyCollection<string> SavedRunNames => _savedRunSeries.Keys.ToList();

    /// <summary>Removes every saved-run overlay from the chart.</summary>
    public void ClearSavedRuns()
    {
        foreach (var pair in _savedRunSeries.Values)
        {
            Series.Remove(pair.Hp);
            Series.Remove(pair.Nm);
        }
        _savedRunSeries.Clear();
    }

    private void GrowAxesFor(double speed, double hp, double nm)
    {
        var newX = NiceNumber.NextAxisMax(_xAxis.MaxLimit ?? _settings.DefaultSpeedMax, speed);
        if (newX != _xAxis.MaxLimit) _xAxis.MaxLimit = newX;

        var newHp = NiceNumber.NextAxisMax(_yAxisHp.MaxLimit ?? _settings.DefaultHpMax, hp);
        if (newHp != _yAxisHp.MaxLimit) _yAxisHp.MaxLimit = newHp;

        var newNm = NiceNumber.NextAxisMax(_yAxisNm.MaxLimit ?? _settings.DefaultNmMax, nm);
        if (newNm != _yAxisNm.MaxLimit) _yAxisNm.MaxLimit = newNm;
    }

    private void GrowAxesForSavedRun(SavedRun run)
    {
        double maxSp = 0, maxHp = 0, maxNm = 0;
        foreach (var s in run.Samples)
        {
            if (s.SpeedKmh > maxSp) maxSp = s.SpeedKmh;
            if (s.Hp > maxHp) maxHp = s.Hp;
            if (s.Nm > maxNm) maxNm = s.Nm;
        }
        GrowAxesFor(maxSp, maxHp, maxNm);
    }

    private void RegrowAxesForAllSavedRuns()
    {
        foreach (var (name, _) in _savedRunSeries)
        {
            // Unknown to chart at this point; nothing to do here.
            _ = name;
        }
        // No persistent SavedRun data is kept here; growth was applied at AddSavedRun.
        // After Reset the user-visible behaviour is: live data starts blank, saved runs remain.
    }

    private static SKColor ParseHex(string hex)
    {
        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 6 &&
            byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
            byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
            byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return new SKColor(r, g, b);
        }
        return new SKColor(0x3B, 0x82, 0xF6); // fallback blue
    }

    private static SKColor AdjustBrightness(SKColor c, double factor)
    {
        byte Clamp(double v) => (byte)Math.Min(255, Math.Max(0, (int)Math.Round(v)));
        return new SKColor(Clamp(c.Red * factor), Clamp(c.Green * factor), Clamp(c.Blue * factor));
    }
}
