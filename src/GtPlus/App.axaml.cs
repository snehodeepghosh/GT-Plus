using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GtPlus.Views;

namespace GtPlus;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // an exception thrown on the UI thread is logged with its stack; the app still handles it the way it did before
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            GtPlus.Services.Logger.Error("Unhandled exception on the UI thread", e.Exception);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
