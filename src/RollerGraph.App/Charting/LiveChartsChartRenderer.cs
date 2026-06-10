using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using RollerGraph.Core.Models;
using RollerGraph.Core.Scaling;
using SkiaSharp;

namespace RollerGraph.App.Charting;

/// <summary>
/// LiveChartsCore + SkiaSharp implementation of <see cref="IChartRenderer"/>.
/// This is the single place in the App that constructs LiveCharts series,
/// axes and paints - everywhere else depends on <see cref="IChartRenderer"/>.
/// </summary>
public sealed class LiveChartsChartRenderer : IChartRenderer
{
    private static readonly SKColor HpStroke = new(0xFF, 0x8A, 0x00); // orange
    private static readonly SKColor NmStroke = new(0x00, 0xB8, 0xA9); // teal

    private readonly ObservableCollection<ObservablePoint> _hpPoints = new();
    private readonly ObservableCollection<ObservablePoint> _nmPoints = new();
    private readonly Dictionary<string, (ISeries Hp, ISeries Nm)> _overlays =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Axis _xAxis;
    private readonly Axis _yAxisHp;
    private readonly Axis _yAxisNm;
    private Settings _settings;

    public LiveChartsChartRenderer(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        Series = new ObservableCollection<ISeries>
        {
            BuildLiveSeries("HP", _hpPoints, HpStroke, scalesYAt: 0, thickness: 2),
            BuildLiveSeries("NM", _nmPoints, NmStroke, scalesYAt: 1, thickness: 2),
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
            LabelsPaint = new SolidColorPaint(HpStroke),
            NamePaint = new SolidColorPaint(HpStroke),
        };
        _yAxisNm = new Axis
        {
            Name = "NM",
            MinLimit = 0,
            MaxLimit = settings.DefaultNmMax,
            Position = LiveChartsCore.Measure.AxisPosition.End,
            LabelsPaint = new SolidColorPaint(NmStroke),
            NamePaint = new SolidColorPaint(NmStroke),
            ShowSeparatorLines = false,
        };

        XAxes = new[] { _xAxis };
        YAxes = new[] { _yAxisHp, _yAxisNm };
    }

    public ObservableCollection<ISeries> Series { get; }
    public ICartesianAxis[] XAxes { get; }
    public ICartesianAxis[] YAxes { get; }

    public void AppendLivePoint(Sample sample)
    {
        _hpPoints.Add(new ObservablePoint(sample.SpeedKmh, sample.Hp));
        _nmPoints.Add(new ObservablePoint(sample.SpeedKmh, sample.Nm));
        GrowAxesFor(sample.SpeedKmh, sample.Hp, sample.Nm);
    }

    public void ResetLive()
    {
        _hpPoints.Clear();
        _nmPoints.Clear();
        _xAxis.MaxLimit = _settings.DefaultSpeedMax;
        _yAxisHp.MaxLimit = _settings.DefaultHpMax;
        _yAxisNm.MaxLimit = _settings.DefaultNmMax;
    }

    public void UpdateDefaults(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        if (_hpPoints.Count == 0)
        {
            _xAxis.MaxLimit = settings.DefaultSpeedMax;
            _yAxisHp.MaxLimit = settings.DefaultHpMax;
            _yAxisNm.MaxLimit = settings.DefaultNmMax;
        }
    }

    public void AddOverlay(SavedRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        RemoveOverlay(run.Name); // replace if present

        var hpColor = ColorTools.ParseHex(run.Color);
        var nmColor = ColorTools.AdjustBrightness(hpColor, factor: 0.7);

        var hpValues = run.Samples.Select(s => new ObservablePoint(s.SpeedKmh, s.Hp)).ToList();
        var nmValues = run.Samples.Select(s => new ObservablePoint(s.SpeedKmh, s.Nm)).ToList();

        var hpSeries = BuildOverlaySeries($"{run.Name} HP", hpValues, hpColor, scalesYAt: 0, isVisible: run.IsVisible);
        var nmSeries = BuildOverlaySeries($"{run.Name} NM", nmValues, nmColor, scalesYAt: 1, isVisible: run.IsVisible);

        _overlays[run.Name] = (hpSeries, nmSeries);
        Series.Add(hpSeries);
        Series.Add(nmSeries);

        if (run.IsVisible) GrowAxesForOverlay(run);
    }

    public void RemoveOverlay(string name)
    {
        if (!_overlays.TryGetValue(name, out var pair)) return;
        Series.Remove(pair.Hp);
        Series.Remove(pair.Nm);
        _overlays.Remove(name);
    }

    public void SetOverlayVisible(string name, bool isVisible)
    {
        if (!_overlays.TryGetValue(name, out var pair)) return;
        pair.Hp.IsVisible = isVisible;
        pair.Nm.IsVisible = isVisible;
    }

    public void ClearOverlays()
    {
        foreach (var pair in _overlays.Values)
        {
            Series.Remove(pair.Hp);
            Series.Remove(pair.Nm);
        }
        _overlays.Clear();
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

    private void GrowAxesForOverlay(SavedRun run)
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

    private static LineSeries<ObservablePoint> BuildLiveSeries(
        string name, ObservableCollection<ObservablePoint> values, SKColor color, int scalesYAt, float thickness)
    {
        return new LineSeries<ObservablePoint>
        {
            Name = name,
            Values = values,
            GeometrySize = 0,
            LineSmoothness = 0.4,
            Stroke = new SolidColorPaint(color) { StrokeThickness = thickness },
            GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 0 },
            GeometryFill = new SolidColorPaint(color),
            Fill = null,
            ScalesYAt = scalesYAt,
        };
    }

    private static LineSeries<ObservablePoint> BuildOverlaySeries(
        string name, IReadOnlyList<ObservablePoint> values, SKColor color, int scalesYAt, bool isVisible)
    {
        return new LineSeries<ObservablePoint>
        {
            Name = name,
            Values = values,
            GeometrySize = 0,
            LineSmoothness = 0.4,
            Stroke = new SolidColorPaint(color) { StrokeThickness = 1.5f },
            GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 0 },
            GeometryFill = new SolidColorPaint(color),
            Fill = null,
            ScalesYAt = scalesYAt,
            IsVisible = isVisible,
        };
    }
}
