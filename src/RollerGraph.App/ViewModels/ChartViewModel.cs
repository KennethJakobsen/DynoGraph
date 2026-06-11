using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using RollerGraph.App.Charting;
using RollerGraph.Core.Models;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Coordinates the live chart's observable state. Holds peak/sample stats
/// and forwards mutation requests to an <see cref="IChartRenderer"/>.
/// Owns no charting-framework types directly so the view-model stays portable.
/// Mutations must happen on the UI thread.
/// </summary>
public sealed partial class ChartViewModel : ObservableObject
{
    private readonly IChartRenderer _renderer;

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

    /// <summary>Convenience constructor using the default LiveCharts renderer.</summary>
    public ChartViewModel(Settings settings) : this(new LiveChartsChartRenderer(settings))
    {
    }

    public ChartViewModel(IChartRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    // Surfaces required for AXAML binding. They are owned by the renderer
    // but exposed here so the View only binds to ChartViewModel.
    public System.Collections.ObjectModel.ObservableCollection<ISeries> Series => _renderer.Series;
    public ICartesianAxis[] XAxes => _renderer.XAxes;
    public ICartesianAxis[] YAxes => _renderer.YAxes;

    /// <summary>Appends a sample to the chart and updates peak stats. UI thread only.</summary>
    public void AppendSample(Sample sample) => AppendSample(sample, sample);

    /// <summary>Appends a plot sample and updates peak stats from the measurement sample. UI thread only.</summary>
    public void AppendSample(Sample sample, Sample measurementSample)
    {
        _renderer.AppendLivePoint(sample);
        SampleCount++;

        // Peak stats (note: peak NM may occur at a different speed than peak HP).
        if (measurementSample.Hp > PeakHp)
        {
            PeakHp = measurementSample.Hp;
            PeakHpSpeed = measurementSample.SpeedKmh;
            _renderer.UpdateLivePeakPoint(measurementSample);
        }
        if (measurementSample.Nm > PeakNm)
        {
            PeakNm = measurementSample.Nm;
            PeakNmSpeed = measurementSample.SpeedKmh;
        }
        if (measurementSample.SpeedKmh > PeakSpeed)
        {
            PeakSpeed = measurementSample.SpeedKmh;
        }
    }

    /// <summary>Clears live data and resets axes/peak stats.</summary>
    public void Reset()
    {
        _renderer.ResetLive();
        PeakHp = 0;
        PeakHpSpeed = 0;
        PeakNm = 0;
        PeakNmSpeed = 0;
        PeakSpeed = 0;
        SampleCount = 0;
    }

    /// <summary>Updates the renderer's idea of default axis maxima.</summary>
    public void UpdateDefaults(Settings settings)
    {
        _renderer.UpdateDefaults(settings);
    }

    public void AddSavedRun(SavedRun run) => _renderer.AddOverlay(run);
    public void RemoveSavedRun(string name) => _renderer.RemoveOverlay(name);
    public void SetSavedRunVisible(string name, bool isVisible) => _renderer.SetOverlayVisible(name, isVisible);
    public void ClearSavedRuns() => _renderer.ClearOverlays();

    public ChartSnapshotStats ToSnapshotStats() =>
        new(PeakHp, PeakHpSpeed, PeakNm, PeakNmSpeed, PeakSpeed);
}
