using RollerGraph.Core.Adjustments;
using RollerGraph.Core.Models;
using RollerGraph.Core.Parsing;
using RollerGraph.Core.Smoothing;

namespace RollerGraph.Core.Pipeline;

/// <summary>
/// Outcome of running a single raw line through <see cref="SamplePipeline"/>.
/// Tells the caller whether to plot, ignore as noise, or count a bad line.
/// </summary>
public enum SamplePipelineOutcome
{
    /// <summary>The line was parsed, adjusted and passed all filters; <see cref="SamplePipelineResult.Sample"/> is set.</summary>
    Accepted,
    /// <summary>The line parsed but failed the MinSpeed filter; it should be silently dropped.</summary>
    FilteredOut,
    /// <summary>The line could not be parsed; the bad-line counter should be incremented.</summary>
    BadLine,
}

/// <summary>Per-line result returned by <see cref="SamplePipeline.Process"/>.</summary>
/// <param name="Outcome">Why the pipeline chose this verdict.</param>
/// <param name="Sample">The plot-ready sample when <see cref="Outcome"/> is <see cref="SamplePipelineOutcome.Accepted"/>.</param>
/// <param name="MeasurementSample">The adjusted, unsmoothed measurement sample used for peak stats.</param>
public readonly record struct SamplePipelineResult(
    SamplePipelineOutcome Outcome,
    Sample? Sample = null,
    Sample? MeasurementSample = null);

/// <summary>
/// Stateful per-session pipeline that turns raw CSV lines into ready-to-plot
/// <see cref="Sample"/> values. Combines parsing, per-channel adjustment,
/// MinSpeed filtering, and optional peak-preserving smoothing.
///
/// Single responsibility: transform one raw line into one pipeline result.
/// Owns no IO, no UI dependency, no concurrency primitives - call from any
/// thread you like, but the smoothing buffer state is not thread-safe so
/// one pipeline per logical session.
/// </summary>
public sealed class SamplePipeline
{
    private readonly Settings _settings;
    private readonly SampleAdjuster _adjuster;
    private SampleSmoother? _smoother;

    public SamplePipeline(Settings settings, SampleAdjuster adjuster)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(adjuster);
        _settings = settings;
        _adjuster = adjuster;
    }

    /// <summary>True when smoothing will be applied to the next accepted sample.</summary>
    public bool SmoothingEnabled { get; set; }

    /// <summary>Discards any in-flight smoothing window. Call on reset / source change.</summary>
    public void ResetSmoother() => _smoother = null;

    /// <summary>
    /// Run a single raw CSV line through the pipeline. The returned result
    /// tells the caller whether to plot, drop, or count as bad.
    /// </summary>
    public SamplePipelineResult Process(string? rawLine, DateTime receivedAtUtc)
    {
        var parsed = CsvLineParser.Parse(rawLine, receivedAtUtc);
        if (parsed is null)
            return new SamplePipelineResult(SamplePipelineOutcome.BadLine);

        var adjusted = _adjuster.Adjust(parsed.Value);

        // Drop physically-impossible negative readings on any plotted channel.
        // A roller can produce a brief negative HP/NM during decel and a
        // misbehaving sensor can spike negative on any channel; either way
        // these only confuse the chart and skew peak tracking.
        if (adjusted.SpeedKmh < 0 || adjusted.Nm < 0 || adjusted.Hp < 0)
            return new SamplePipelineResult(SamplePipelineOutcome.FilteredOut);

        if (adjusted.SpeedKmh < _settings.MinSpeedKmh)
            return new SamplePipelineResult(SamplePipelineOutcome.FilteredOut);

        var measurement = adjusted;

        if (SmoothingEnabled && _settings.SmoothingWindow > 1)
        {
            _smoother ??= new SampleSmoother(_settings.SmoothingWindow);
            adjusted = _smoother.Smooth(adjusted);
        }

        return new SamplePipelineResult(SamplePipelineOutcome.Accepted, adjusted, measurement);
    }
}
