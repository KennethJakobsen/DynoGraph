using System.Collections.ObjectModel;
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
/// View-model for the dyno chart. Holds the HP/NM series (vs. speed) and the
/// auto-scaling axis state. Mutations must happen on the UI thread.
/// </summary>
public sealed partial class ChartViewModel : ObservableObject
{
    private readonly ObservableCollection<ObservablePoint> _hpPoints = new();
    private readonly ObservableCollection<ObservablePoint> _nmPoints = new();

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

        Series = new ISeries[]
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

    public ISeries[] Series { get; }
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

        // Grow axes if needed.
        var newX = NiceNumber.NextAxisMax(_xAxis.MaxLimit ?? _settings.DefaultSpeedMax, sample.SpeedKmh);
        if (newX != _xAxis.MaxLimit) _xAxis.MaxLimit = newX;

        var newHp = NiceNumber.NextAxisMax(_yAxisHp.MaxLimit ?? _settings.DefaultHpMax, sample.Hp);
        if (newHp != _yAxisHp.MaxLimit) _yAxisHp.MaxLimit = newHp;

        var newNm = NiceNumber.NextAxisMax(_yAxisNm.MaxLimit ?? _settings.DefaultNmMax, sample.Nm);
        if (newNm != _yAxisNm.MaxLimit) _yAxisNm.MaxLimit = newNm;

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
    /// Clears all data and resets axis limits to their defaults.
    /// Must be invoked on the UI thread.
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
    }

    /// <summary>
    /// Updates the cached defaults used by <see cref="Reset"/> and to floor axis growth.
    /// If no data has been plotted yet, the visible axes are reset to the new defaults.
    /// Must be invoked on the UI thread.
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
        }
    }
}
