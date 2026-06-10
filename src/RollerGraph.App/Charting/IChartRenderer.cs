using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using RollerGraph.Core.Models;

namespace RollerGraph.App.Charting;

/// <summary>
/// Abstraction over the actual chart implementation. Concrete renderers
/// (LiveChartsCore, Oxyplot, mocks for tests, ...) translate semantic
/// operations into framework-specific objects. <see cref="ChartViewModel"/>
/// depends only on this interface and never on a specific charting library.
///
/// The LiveCharts collections are still exposed via <see cref="Series"/>,
/// <see cref="XAxes"/>, <see cref="YAxes"/> because the Avalonia binding
/// surface requires them - but any renderer can populate them however it likes.
/// </summary>
public interface IChartRenderer
{
    /// <summary>The series collection bound by the View.</summary>
    ObservableCollection<ISeries> Series { get; }

    /// <summary>The X axis array bound by the View.</summary>
    ICartesianAxis[] XAxes { get; }

    /// <summary>The Y axis array bound by the View.</summary>
    ICartesianAxis[] YAxes { get; }

    /// <summary>Add a single live sample point to the live HP series.</summary>
    void AppendLivePoint(Sample sample);

    /// <summary>Clear all live points and snap the axis limits back to the defaults.</summary>
    void ResetLive();

    /// <summary>Recompute the default axis maxima from the given settings.</summary>
    void UpdateDefaults(Settings settings);

    /// <summary>Add (or replace) a saved-run overlay as two series (HP + NM).</summary>
    void AddOverlay(SavedRun run);

    /// <summary>Remove the named overlay from the chart, if present.</summary>
    void RemoveOverlay(string name);

    /// <summary>Show or hide an overlay without removing it.</summary>
    void SetOverlayVisible(string name, bool isVisible);

    /// <summary>Remove every overlay.</summary>
    void ClearOverlays();
}
