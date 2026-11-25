using Avalonia;
using System;
using System.Linq;
using GtPlus.Services;

namespace GtPlus;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Logger.Init();
        Logger.Info($"GT+ starting - PID {Environment.ProcessId}");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        Logger.Info("GT+ exiting");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // macOS: Avalonia's default is OpenGL, which Apple has deprecated; on macOS 27.2 it stopped drawing many of a
            // title's pictures (an M4 Pro drew 6 of 17 bitmaps). Metal is Apple's own API; OpenGL and software stay as
            // fallbacks. OPTIMAL_GT_RENDERER=metal|opengl|software forces one, for diagnosing a machine
            .With(new AvaloniaNativePlatformOptions { RenderingMode = MacRenderingModes() })
            .WithInterFont()
            .LogToTrace();

    private static AvaloniaNativeRenderingMode[] MacRenderingModes()
    {
        var all = new[] { AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software };
        var forced = Environment.GetEnvironmentVariable("OPTIMAL_GT_RENDERER")?.Trim().ToLowerInvariant() switch
        {
            "metal"    => AvaloniaNativeRenderingMode.Metal,
            "opengl"   => AvaloniaNativeRenderingMode.OpenGl,
            "software" => AvaloniaNativeRenderingMode.Software,
            _          => (AvaloniaNativeRenderingMode?)null,
        };
        var modes = forced is { } f ? new[] { f }.Concat(all.Where(m => m != f)).ToArray() : all;
        Logger.Info("macOS rendering modes: " + string.Join(", ", modes));
        return modes;
    }
}
