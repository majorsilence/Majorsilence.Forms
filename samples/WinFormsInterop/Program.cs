using Majorsilence.Forms;

namespace WinFormsInterop;

internal static class Program
{
    // Same bootstrap the migrated TownSuite Majorsilence apps use.
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SpikeForm());
    }
}
