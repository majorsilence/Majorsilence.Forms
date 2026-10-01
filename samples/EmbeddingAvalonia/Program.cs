using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;

namespace EmbeddingAvalonia;

// A host-owned Avalonia desktop app that embeds Majorsilence.Forms content via MajorsilenceFormsPresenter.
//
//   dotnet run --project samples/EmbeddingAvalonia                                -- run on the desktop
//   dotnet run --project samples/EmbeddingAvalonia -- --theme Themes/ocean.css    -- ...with a CSS theme applied
//   dotnet run --project samples/EmbeddingAvalonia -- --render-headless out.png [--theme file.css]
//
// --theme applies one stylesheet to both halves of the window -- the native Avalonia controls (through
// Majorsilence.Forms.Theming.Avalonia) and the embedded Majorsilence.Forms scene. --render-headless draws
// the window offscreen with Avalonia.Headless and Skia, writes a PNG and exits: how docs/theming-avalonia.md's
// screenshot is made, and how a script can look at the result without a display.
public static class Program
{
    internal static string? ThemePath { get; private set; }

    [STAThread]
    public static int Main (string[] args)
    {
        var theme = Array.IndexOf (args, "--theme");
        if (theme >= 0 && theme + 1 < args.Length)
            ThemePath = Path.GetFullPath (args[theme + 1]);

        var render = Array.IndexOf (args, "--render-headless");
        if (render >= 0) {
            if (render + 1 >= args.Length) {
                Console.Error.WriteLine ("usage: --render-headless <out.png> [--theme file.css]");
                return 2;
            }

            return RenderHeadless (Path.GetFullPath (args[render + 1]));
        }

        BuildAvaloniaApp ().StartWithClassicDesktopLifetime (args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp ()
        => AppBuilder.Configure<App> ()
            .UsePlatformDetect ()
            .WithInterFont ()
            .UseSkia ()
            .LogToTrace ();

    private static int RenderHeadless (string output)
    {
        // Real Skia drawing rather than the headless no-op renderer, so the frame can be captured.
        AppBuilder.Configure<App> ()
            .UseSkia ()
            .WithInterFont ()
            .UseHeadless (new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting ();

        var window = new MainWindow ();
        window.Show ();

        // A few passes so layout, the presenter's first frame and the theme have all settled.
        for (var i = 0; i < 3; i++) {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs ();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick ();
        }

        using var frame = window.CaptureRenderedFrame ();
        if (frame is null) {
            Console.Error.WriteLine ("No frame was rendered.");
            return 1;
        }

        frame.Save (output, new Avalonia.Media.Imaging.PngBitmapEncoderOptions ());
        Console.WriteLine ($"Rendered EmbeddingAvalonia ({(ThemePath is null ? "host theme" : Path.GetFileName (ThemePath))}) -> {output}");
        return 0;
    }
}
