using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using RollerGraph.App.Charting;
using RollerGraph.App.ViewModels;
using RollerGraph.Core.Models;
using Shouldly;

namespace RollerGraph.App.Tests;

public class ChartViewModelTests
{
    [Fact]
    public void AppendSample_UsesSmoothedSampleForLineAndRawSampleForPeakMarker()
    {
        var renderer = new CapturingChartRenderer();
        var chart = new ChartViewModel(renderer);
        var smoothed = new Sample(1, 30, 40, 5.5, DateTime.UtcNow);
        var measurement = new Sample(1, 29, 43, 6.7, DateTime.UtcNow);

        chart.AppendSample(smoothed, measurement);

        renderer.LastLivePoint.ShouldBe(smoothed);
        renderer.LastPeakPoint.ShouldBe(measurement);
        chart.PeakHp.ShouldBe(6.7);
        chart.PeakHpSpeed.ShouldBe(29);
    }

    private sealed class CapturingChartRenderer : IChartRenderer
    {
        public ObservableCollection<ISeries> Series { get; } = new();
        public ICartesianAxis[] XAxes { get; } = Array.Empty<ICartesianAxis>();
        public ICartesianAxis[] YAxes { get; } = Array.Empty<ICartesianAxis>();
        public Sample? LastLivePoint { get; private set; }
        public Sample? LastPeakPoint { get; private set; }

        public void AppendLivePoint(Sample sample) => LastLivePoint = sample;
        public void UpdateLivePeakPoint(Sample sample) => LastPeakPoint = sample;
        public void ResetLive() { }
        public void UpdateDefaults(Settings settings) { }
        public void AddOverlay(SavedRun run) { }
        public void RemoveOverlay(string name) { }
        public void SetOverlayVisible(string name, bool isVisible) { }
        public void ClearOverlays() { }
    }
}
