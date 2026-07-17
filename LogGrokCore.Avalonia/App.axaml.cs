using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LogGrokCore.Avalonia.ViewModels;
using LogGrokCore.Avalonia.Views;

namespace LogGrokCore.Avalonia;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(desktop.Args ?? [])
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
