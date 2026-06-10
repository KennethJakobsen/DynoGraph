using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RollerGraph.App.Services;
using RollerGraph.App.ViewModels;
using RollerGraph.App.Views;
using RollerGraph.Core.Logging;
using RollerGraph.Core.Models;
using RollerGraph.Core.Serial;
using RollerGraph.Core.Storage;

namespace RollerGraph.App;

/// <summary>
/// Composition root. This is the ONLY place concrete Core/App implementations
/// are wired together; everything downstream (view-model, services, controllers)
/// depends on the interfaces.
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Core abstractions - all behind interfaces.
            IUiDispatcher dispatcher = new AvaloniaUiDispatcher();
            ISettingsStore settingsStore = SettingsStore.Default();
            ISavedRunStore runStore = FileSavedRunStore.Default();
            ISessionLogger logger = new CsvSessionLogger(CsvSessionLogger.DefaultLogDirectory());
            var serialFactory = new SystemSerialSourceFactory();   // also implements IPortEnumerator

            var vm = new MainWindowViewModel(
                dispatcher,
                settingsStore: settingsStore,
                runStore: runStore,
                logger: logger,
                portEnumerator: serialFactory,
                sourceFactory: serialFactory);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
