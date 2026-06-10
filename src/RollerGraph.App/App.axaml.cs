using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RollerGraph.App.Services;
using RollerGraph.App.ViewModels;
using RollerGraph.App.Views;
using RollerGraph.Core.Models;

namespace RollerGraph.App;

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
            var dispatcher = new AvaloniaUiDispatcher();
            var store = SettingsStore.Default();
            var vm = new MainWindowViewModel(dispatcher, settingsStore: store);
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
