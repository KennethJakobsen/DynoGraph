using RollerGraph.App.Services;

namespace RollerGraph.App.Printing;

/// <summary>
/// Sends a rendered PNG to a platform-specific print pipeline. One concrete
/// launcher exists per supported OS, so adding a new platform means adding a
/// new <see cref="IPrintLauncher"/> implementation - existing ones stay
/// unmodified (Open/Closed).
/// </summary>
public interface IPrintLauncher
{
    /// <summary>True when this launcher can handle the current OS.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Hand off the given PNG file to the platform print pipeline.
    /// Returns a structured result describing what happened.
    /// </summary>
    Task<ChartPrintResult> LaunchAsync(string pngFilePath);
}
