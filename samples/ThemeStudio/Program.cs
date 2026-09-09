using System;
using System.IO;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;

namespace ThemeStudio
{
    // Theme Studio: write a Majorsilence.Forms CSS theme and watch it apply as you type.
    //
    //   dotnet run --project samples/ThemeStudio                        -- start from the Light theme
    //   dotnet run --project samples/ThemeStudio -- Themes/ocean.css    -- open (and watch) a file
    //   dotnet run --project samples/ThemeStudio -- --render-headless out.png [theme.css] [width height] [--tab N]
    //
    // The last form renders the preview offscreen and exits, which is how a script or a coding
    // assistant can see what a theme looks like without a display. See docs/theming.md.
    internal static class Program
    {
        [STAThread]
        private static int Main (string[] args)
        {
            var renderIndex = Array.IndexOf (args, "--render-headless");

            if (renderIndex >= 0) {
                if (renderIndex + 1 >= args.Length) {
                    Console.Error.WriteLine ("usage: --render-headless <out.png> [theme.css] [width height]");
                    return 2;
                }

                var output = args[renderIndex + 1];
                string? theme = null;
                var width = 1280;
                var height = 860;
                var next = renderIndex + 2;

                if (next < args.Length && !int.TryParse (args[next], out _) && !args[next].StartsWith ("--", StringComparison.Ordinal))
                    theme = args[next++];

                if (next + 1 < args.Length && int.TryParse (args[next], out var w) && int.TryParse (args[next + 1], out var h)) {
                    width = w;
                    height = h;
                }

                var tabIndex = Array.IndexOf (args, "--tab");
                var tab = tabIndex >= 0 && tabIndex + 1 < args.Length && int.TryParse (args[tabIndex + 1], out var t) ? t : 0;

                return RenderHeadless (output, theme, width, height, tab);
            }

            var path = args.Length > 0 && !args[0].StartsWith ('-') ? Path.GetFullPath (args[0]) : null;

            Application.Run (new MainForm (path));
            return 0;
        }

        private static int RenderHeadless (string output, string? theme, int width, int height, int tab)
        {
            HeadlessRenderer.Use ();

            var form = new MainForm (theme is null ? null : Path.GetFullPath (theme));
            form.SelectPreviewTab (tab);

            // Two passes: the first lays out, the second paints with the layout settled.
            HeadlessRenderer.CapturePng (form, width, height);
            var png = HeadlessRenderer.CapturePng (form, width, height);

            File.WriteAllBytes (output, png);
            Console.WriteLine ($"Rendered Theme Studio ({(theme is null ? "Light" : Path.GetFileName (theme))}) → {output} ({width}x{height}).");

            foreach (var diagnostic in form.LastDiagnostics)
                Console.WriteLine (diagnostic);

            return form.LastHadErrors ? 1 : 0;
        }
    }
}
