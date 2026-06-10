using RollerGraph.App.Services;

namespace RollerGraph.App.Printing;

/// <summary>
/// Selects the first launcher whose <see cref="IPrintLauncher.IsSupported"/>
/// returns true. Adding support for a new OS is a one-line change: register
/// another launcher and the dispatcher will pick it up automatically.
/// </summary>
public sealed class PlatformPrintLauncher : IPrintLauncher
{
    private readonly IReadOnlyList<IPrintLauncher> _launchers;

    public PlatformPrintLauncher(IEnumerable<IPrintLauncher> launchers)
    {
        ArgumentNullException.ThrowIfNull(launchers);
        _launchers = launchers.ToArray();
    }

    /// <summary>Constructs the default dispatcher with macOS, Linux and Windows launchers.</summary>
    public static PlatformPrintLauncher Default() => new(new IPrintLauncher[]
    {
        new MacPrintLauncher(),
        new LinuxPrintLauncher(),
        new WindowsPrintLauncher(),
    });

    public bool IsSupported => _launchers.Any(l => l.IsSupported);

    public Task<ChartPrintResult> LaunchAsync(string pngFilePath)
    {
        foreach (var launcher in _launchers)
        {
            if (launcher.IsSupported)
                return launcher.LaunchAsync(pngFilePath);
        }
        return Task.FromResult(new ChartPrintResult(
            ChartPrintOutcome.SnapshotOnly,
            $"Saved snapshot to {pngFilePath}"));
    }
}
