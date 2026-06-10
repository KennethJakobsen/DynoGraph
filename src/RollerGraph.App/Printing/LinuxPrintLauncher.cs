using System.Diagnostics;
using System.Runtime.InteropServices;
using RollerGraph.App.Services;

namespace RollerGraph.App.Printing;

/// <summary>Linux print launcher: opens the PNG in the default image viewer.</summary>
public sealed class LinuxPrintLauncher : IPrintLauncher
{
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public Task<ChartPrintResult> LaunchAsync(string pngFilePath)
    {
        var psi = new ProcessStartInfo("xdg-open", pngFilePath) { UseShellExecute = false };
        Process.Start(psi);
        return Task.FromResult(new ChartPrintResult(
            ChartPrintOutcome.OpenedInViewer,
            "Opened in default viewer - use Ctrl+P to print"));
    }
}
