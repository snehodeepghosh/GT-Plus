using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GtPlus.Services;

/// <summary>
/// timestamped lines to the console and a log file. The log lives in the system's usual place for app logs, never in the
/// app bundle (which may not be writable): ~/Library/Logs/Optimal GT Designer on macOS, %LOCALAPPDATA%\Optimal GT
/// Designer\Logs on Windows, ~/.local/state/optimal-gt-designer on Linux. Each launch starts a new log and keeps the last
/// one as <c>.previous.log</c>, so a session that crashed can still be read after a restart
/// </summary>
public static class Logger
{
    private static StreamWriter? _file;
    private static readonly object Gate = new();

    /// <summary>the file this session writes to; null when it could not be opened</summary>
    public static string? LogPath { get; private set; }

    /// <summary>the last session's log, when there is one</summary>
    public static string? PreviousLogPath { get; private set; }

    /// <summary>Debug lines are written only while this is on (File › Debug › Verbose Logging, or OPTIMAL_GT_DEBUG=1)</summary>
    public static bool Verbose { get; set; } = Environment.GetEnvironmentVariable("OPTIMAL_GT_DEBUG") is "1" or "true";

    public static string LogDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Logs", "Optimal GT Designer");
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Optimal GT Designer", "Logs");
            var state = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            return Path.Combine(string.IsNullOrEmpty(state) ? Path.Combine(home, ".local", "state") : state, "optimal-gt-designer");
        }
    }

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path     = Path.Combine(LogDirectory, "optimal-gt-designer.log");
            var previous = Path.Combine(LogDirectory, "optimal-gt-designer.previous.log");
            if (File.Exists(path)) File.Copy(path, previous, overwrite: true);
            PreviousLogPath = File.Exists(previous) ? previous : null;
            _file   = new StreamWriter(path, append: false) { AutoFlush = true };
            LogPath = path;
            Write("INFO ", $"Log started - {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Could not open log file: {ex.Message}");
        }

        Write("INFO ", $"Optimal GT Designer {AppVersion.Current} on {RuntimeInformation.OSDescription} " +
                       $"({RuntimeInformation.OSArchitecture}, process {RuntimeInformation.ProcessArchitecture}), " +
                       $".NET {Environment.Version}");

        // anything that escapes a handler is written with its stack before the process goes down
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Error("Unhandled exception" + (e.IsTerminating ? " - the app is closing" : ""), e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);
    public static void Debug(string msg) { if (Verbose) Write("DEBUG", msg); }

    public static void Error(string msg, Exception? ex = null)
    {
        // the full exception, inner ones and stack included: a message alone rarely says where it went wrong
        Write("ERROR", ex is null ? msg : $"{msg}\n{Indent(ex.ToString())}");
    }

    private static string Indent(string text) => "        " + text.Replace("\n", "\n        ");

    private static void Write(string level, string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {msg}";
        Console.WriteLine(line);
        lock (Gate)
        {
            try { _file?.WriteLine(line); }
            catch { /* logging must never crash the app */ }
        }
    }
}
