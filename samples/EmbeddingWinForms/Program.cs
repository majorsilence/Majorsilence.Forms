using Majorsilence.Forms.Theming.WinForms;

namespace EmbeddingWinForms;

// A host-owned classic WinForms desktop app that embeds Majorsilence.Forms content via
// MajorsilenceFormsPresenter / ToWinFormsControl(). This is the incremental-migration shape for a
// WinForms app (or a WinForms control library's consumers): the app stays WinForms, individual
// controls move to Majorsilence.Forms one at a time, and when everything is ported the host swaps
// to the Avalonia or Uno backend and goes cross-platform.
//
// Both halves are themed from ONE stylesheet, Themes/graphite.css: WinFormsCssTheme applies it to
// the native controls (and to the embedded Majorsilence.Forms scene through Theme.ApplyStyleSheet),
// so the two do not drift apart visually. Pass --no-theme to see the untreated default look.
//
// Run on Windows: `dotnet run --project samples/EmbeddingWinForms`.
public static class Program
{
    [System.STAThread]
    public static void Main (string[] args)
    {
        System.Windows.Forms.Application.SetHighDpiMode (System.Windows.Forms.HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles ();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault (false);

        var theme = Path.Combine (AppContext.BaseDirectory, "Themes", "graphite.css");

        // Before the first form: Application.SetDefaultFont / SetColorMode only take effect then.
        if (!args.Contains ("--no-theme") && File.Exists (theme))
            WinFormsCssTheme.Apply (File.ReadAllText (theme));

        var main = new MainForm ();
        WinFormsCssTheme.Track (main);   // style it now; keep styling controls (and dialogs) added later
        System.Windows.Forms.Application.Run (main);
    }
}
