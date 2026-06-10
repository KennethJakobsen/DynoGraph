using System.Diagnostics;
using System.Runtime.InteropServices;
using RollerGraph.App.Services;

namespace RollerGraph.App.Printing;

/// <summary>macOS print launcher: opens the PNG in Preview so the user can hit Cmd+P.</summary>
public sealed class MacPrintLauncher : IPrintLauncher
{
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public Task<ChartPrintResult> LaunchAsync(string pngFilePath)
    {
        Process.Start("open", $"-a Preview \"{pngFilePath}\"");
        return Task.FromResult(new ChartPrintResult(
            ChartPrintOutcome.OpenedInViewer,
            "Opened in Preview - use Cmd+P to print"));
    }
}
