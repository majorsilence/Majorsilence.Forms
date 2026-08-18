namespace EmbeddingWinForms;

// A host-owned classic WinForms desktop app that embeds Majorsilence.Forms content via
// MajorsilenceFormsPresenter / ToWinFormsControl(). This is the incremental-migration shape for a
// WinForms app (or a WinForms control library's consumers): the app stays WinForms, individual
// controls move to Majorsilence.Forms one at a time, and when everything is ported the host swaps
// to the Avalonia or Uno backend and goes cross-platform.
//
// Run on Windows: `dotnet run --project samples/EmbeddingWinForms`.
public static class Program
{
    [System.STAThread]
    public static void Main ()
    {
        System.Windows.Forms.Application.SetHighDpiMode (System.Windows.Forms.HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles ();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault (false);
        System.Windows.Forms.Application.Run (new MainForm ());
    }
}
