using System.Diagnostics;
using System.Runtime.InteropServices;
using RollerGraph.App.Services;

namespace RollerGraph.App.Printing;

/// <summary>
/// Windows print launcher: invokes the shell "print" verb. Falls back to
/// "open" if the verb isn't registered (e.g. no default printer).
/// </summary>
public sealed class WindowsPrintLauncher : IPrintLauncher
{
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public Task<ChartPrintResult> LaunchAsync(string pngFilePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pngFilePath,
            Verb = "print",
            UseShellExecute = true,
        };
        try
        {
            Process.Start(psi);
            return Task.FromResult(new ChartPrintResult(
                ChartPrintOutcome.Sent,
                "Sent to default printer"));
        }
        catch
        {
            Process.Start(new ProcessStartInfo { FileName = pngFilePath, UseShellExecute = true });
            return Task.FromResult(new ChartPrintResult(
                ChartPrintOutcome.OpenedInViewer,
                "Opened in default viewer - use Ctrl+P to print"));
        }
    }
}
