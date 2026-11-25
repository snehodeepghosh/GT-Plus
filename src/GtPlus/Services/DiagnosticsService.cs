using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GtPlus.Models;

namespace GtPlus.Services;

/// <summary>
/// File › Debug › Save Diagnostic Report: one zip that holds what is needed to work out a problem on someone else's
/// machine. report.txt (app, system, graphics, screens, the open title and a decode check of every picture in it), this
/// session's and the last session's logs, and canvas-reference.png, the open title drawn by the CPU renderer so it can be
/// compared with what the screen shows. Nothing is sent anywhere; the user chooses where the zip goes
/// </summary>
public static class DiagnosticsService
{
    /// <summary>the graphics backend Avalonia actually started with, e.g. "Metal" or "OpenGL"; read through reflection
    /// because the interface is not public, so a failure only means "unknown"</summary>
    public static string GraphicsBackend()
    {
        try
        {
            var asm       = typeof(Visual).Assembly;
            var locator   = asm.GetType("Avalonia.AvaloniaLocator");
            var graphicsT = asm.GetType("Avalonia.Platform.IPlatformGraphics");
            var current   = locator?.GetProperty("Current")?.GetValue(null);
            var getService = current?.GetType().GetMethod("GetService", new[] { typeof(Type) });
            if (current is null || getService is null || graphicsT is null) return "unknown";
            var graphics = getService.Invoke(current, new object[] { graphicsT });
            if (graphics is null) return "Software (no GPU backend)";
            var name = graphics.GetType().Name;
            return name.Contains("Metal", StringComparison.OrdinalIgnoreCase) ? $"Metal ({name})"
                 : name.Contains("Gl", StringComparison.Ordinal)               ? $"OpenGL ({name})"
                 : name;
        }
        catch (Exception ex)
        {
            return "unknown (" + ex.GetType().Name + ")";
        }
    }

    public static void WriteReport(string zipPath, Window window, GtDocument? document, string? documentPath,
                                   GtAssetLibrary assets, Control? canvas)
    {
        var sb = new StringBuilder();
        void Line(string s = "") => sb.AppendLine(s);
        var inv = CultureInfo.InvariantCulture;

        Line("Optimal GT Designer diagnostic report");
        Line($"Created     {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Line();
        Line("[App]");
        Line($"Version     {AppVersion.Current}");
        Line($"Executable  {Environment.ProcessPath}");
        Line($"Log         {Logger.LogPath ?? "(not open)"}");
        Line($"Verbose     {Logger.Verbose}");
        Line();
        Line("[System]");
        Line($"OS          {RuntimeInformation.OSDescription}");
        if (OperatingSystem.IsMacOS()) Line($"macOS       {Run("sw_vers", "-productVersion")} (build {Run("sw_vers", "-buildVersion")})");
        Line($"Machine     {MachineModel()}");
        Line($"CPU         {CpuName()} - {Environment.ProcessorCount} cores, {RuntimeInformation.OSArchitecture}");
        Line($"Process     {RuntimeInformation.ProcessArchitecture}{(IsTranslated() ? " (running under Rosetta)" : "")}");
        Line($".NET        {Environment.Version}");
        Line($"Memory      {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MB available to the app, " +
             $"{Environment.WorkingSet / (1024 * 1024)} MB in use");
        Line($"Culture     {CultureInfo.CurrentCulture.Name}");
        Line();
        Line("[Graphics]");
        Line($"Backend     {GraphicsBackend()}");
        Line($"Forced      {Environment.GetEnvironmentVariable("OPTIMAL_GT_RENDERER") ?? "(no, OPTIMAL_GT_RENDERER not set)"}");
        if (window.Screens is { } screens)
            foreach (var s in screens.All)
                Line($"Screen      {s.Bounds.Width}x{s.Bounds.Height} at {s.Scaling.ToString("0.##", inv)}x{(s.IsPrimary ? ", primary" : "")}" +
                     (ReferenceEquals(s, screens.ScreenFromWindow(window)) ? ", this window" : ""));
        Line($"Window      {window.Bounds.Width:0}x{window.Bounds.Height:0} at {window.RenderScaling.ToString("0.##", inv)}x");
        Line();

        Line("[Title]");
        if (document is null) Line("No title open.");
        else
        {
            Line($"File        {documentPath ?? "(not saved)"}");
            Line($"Canvas      {document.Width}x{document.Height}");
            Line($"Layers      {document.Layers.Count}, objects {document.Layers.Sum(l => l.Elements.Count)}, storyboards {document.Storyboards.Count}");
            foreach (var layer in document.Layers)
            {
                Line($"  Layer \"{layer.Name}\" {(layer.Visible ? "" : "(hidden) ")}at {layer.Location.X},{layer.Location.Y} " +
                     $"{layer.Dimensions.Width}x{layer.Dimensions.Height}, {layer.Elements.Count} objects");
                foreach (var el in layer.Elements)
                    Line($"    {Kind(el),-9} \"{el.Name}\"{(el.Visible ? "" : " hidden")}{(el.Opacity < 1 ? $" opacity {el.Opacity:0.##}" : "")}" +
                         $"{(string.IsNullOrEmpty(el.MaskObject) ? "" : $" mask \"{el.MaskObject}\"")}" +
                         $" at {el.Location.X:0.#},{el.Location.Y:0.#} {el.Dimensions.Width:0.#}x{el.Dimensions.Height:0.#}" +
                         $"{(PictureOf(el) is { } pic ? $" -> {pic}" : "")}");
            }
        }
        Line();

        // every picture decoded exactly the way the canvas decodes it, so one that fails here is one the canvas cannot draw
        Line("[Pictures]");
        int ok = 0, failed = 0;
        foreach (var path in assets.Blobs.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var bytes = assets.Blobs[path];
            try
            {
                using var ms  = new MemoryStream(bytes);
                using var bmp = new Bitmap(ms);
                Line($"  ok      {path}  {bytes.Length:N0} bytes  {bmp.PixelSize.Width}x{bmp.PixelSize.Height}");
                ok++;
            }
            catch (Exception ex)
            {
                Line($"  FAILED  {path}  {bytes.Length:N0} bytes  {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }
        Line($"{ok} decoded, {failed} failed");
        Line();
        Line("canvas-reference.png is the title drawn by the CPU renderer. If it looks right while the screen does not, the");
        Line("graphics backend is at fault: start the app with OPTIMAL_GT_RENDERER=software (or metal, opengl) to compare.");

        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddText(zip, "report.txt", sb.ToString());
        AddFile(zip, Logger.LogPath, "optimal-gt-designer.log");
        AddFile(zip, Logger.PreviousLogPath, "optimal-gt-designer.previous.log");
        if (canvas is not null && document is not null) AddCanvasReference(zip, canvas);
        Logger.Info($"Diagnostic report saved: {zipPath} ({ok} pictures decoded, {failed} failed)");
    }

    private static void AddCanvasReference(ZipArchive zip, Control canvas)
    {
        try
        {
            var size = new PixelSize(Math.Max(1, (int)canvas.Bounds.Width), Math.Max(1, (int)canvas.Bounds.Height));
            using var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
            rtb.Render(canvas);
            using var entry = zip.CreateEntry("canvas-reference.png").Open();
            rtb.Save(entry);
        }
        catch (Exception ex)
        {
            AddText(zip, "canvas-reference-error.txt", ex.ToString());
        }
    }

    private static void AddText(ZipArchive zip, string name, string text)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    /// <summary>the log is still open for writing, so it is read shared rather than copied</summary>
    private static void AddFile(ZipArchive zip, string? path, string name)
    {
        if (path is null || !File.Exists(path)) return;
        try
        {
            using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = zip.CreateEntry(name).Open();
            src.CopyTo(dst);
        }
        catch (Exception ex) { AddText(zip, name + ".error.txt", ex.Message); }
    }

    private static string Kind(GtElement el) => el switch
    {
        GtTextBlock        => "Text",
        GtImageElement     => "Image",
        GtRectangleElement => "Rectangle",
        GtEllipseElement   => "Ellipse",
        _                  => el.GetType().Name.Replace("Gt", "").Replace("Element", ""),
    };

    private static string? PictureOf(GtElement el) => el switch
    {
        GtImageElement img                                      => img.BitmapSource,
        GtRectangleElement { Fill.Type: GtBrushType.Bitmap } r  => r.Fill.BitmapSource,
        GtEllipseElement   { Fill.Type: GtBrushType.Bitmap } e  => e.Fill.BitmapSource,
        _ => null,
    };

    private static string MachineModel()
    {
        if (OperatingSystem.IsMacOS()) return Run("sysctl", "-n hw.model");
        if (OperatingSystem.IsLinux() && File.Exists("/sys/devices/virtual/dmi/id/product_name"))
            return File.ReadAllText("/sys/devices/virtual/dmi/id/product_name").Trim();
        return Environment.MachineName.Length > 0 ? "(model not read on this platform)" : "unknown";
    }

    private static string CpuName()
    {
        if (OperatingSystem.IsMacOS()) return Run("sysctl", "-n machdep.cpu.brand_string");
        if (OperatingSystem.IsWindows()) return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
        if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            return File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name"))?.Split(':', 2)[1].Trim() ?? "unknown";
        return "unknown";
    }

    private static bool IsTranslated() => OperatingSystem.IsMacOS() && Run("sysctl", "-n sysctl.proc_translated") == "1";

    /// <summary>a short system command's first line of output; never throws</summary>
    private static string Run(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            });
            if (p is null) return "unknown";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            return output.Trim().Split('\n').FirstOrDefault()?.Trim() is { Length: > 0 } s ? s : "unknown";
        }
        catch { return "unknown"; }
    }
}
