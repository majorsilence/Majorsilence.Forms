using System.Drawing.Imaging;
using WF = System.Windows.Forms;

namespace ThemeStudio.WinForms
{
    // Theme Studio for real System.Windows.Forms: write a Majorsilence.Forms CSS theme and watch it
    // apply to native WinForms controls as you type (see docs/theming-winforms.md).
    //
    //   dotnet run --project samples/ThemeStudio.WinForms                          -- start from the Light theme
    //   dotnet run --project samples/ThemeStudio.WinForms -- Themes/graphite.css   -- open (and watch) a file
    //   dotnet run --project samples/ThemeStudio.WinForms -- --screenshot out.png [theme.css]
    //
    // The last form renders the preview panel with Control.DrawToBitmap and exits -- the WinForms
    // equivalent of the Avalonia head's --render-headless (there is no headless WinForms; a desktop
    // session is still needed, and the window is shown off-screen at zero opacity for the capture).
    internal static class Program
    {
        [STAThread]
        private static int Main (string[] args)
        {
            WF.Application.SetHighDpiMode (WF.HighDpiMode.PerMonitorV2);
            WF.Application.EnableVisualStyles ();
            WF.Application.SetCompatibleTextRenderingDefault (false);

            var screenshotIndex = Array.IndexOf (args, "--screenshot");

            if (screenshotIndex >= 0) {
                if (screenshotIndex + 1 >= args.Length) {
                    Console.Error.WriteLine ("usage: --screenshot <out.png> [theme.css]");
                    return 2;
                }

                var output = args[screenshotIndex + 1];
                var theme = screenshotIndex + 2 < args.Length ? Path.GetFullPath (args[screenshotIndex + 2]) : null;
                return Screenshot (output, theme);
            }

            var path = args.Length > 0 && !args[0].StartsWith ('-') ? Path.GetFullPath (args[0]) : null;

            using var form = new MainForm (path);
            WF.Application.Run (form);
            return 0;
        }

        private static int Screenshot (string output, string? theme)
        {
            using var form = new MainForm (theme);

            // DrawToBitmap prints child windows, which only exist once the form has been shown; show it
            // where nobody can see it.
            form.ShowForScreenshot ();

            using var bitmap = form.CapturePreview ();
            bitmap.Save (output, ImageFormat.Png);
            form.Close ();

            Console.WriteLine ($"Rendered Theme Studio (WinForms, {(theme is null ? "Light" : Path.GetFileName (theme))}) → {output} ({bitmap.Width}x{bitmap.Height}).");

            foreach (var diagnostic in form.LastDiagnostics)
                Console.WriteLine (diagnostic);

            return form.LastHadErrors ? 1 : 0;
        }
    }
}
