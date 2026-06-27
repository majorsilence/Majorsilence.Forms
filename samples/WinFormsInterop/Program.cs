using WF = System.Windows.Forms;
using Majorsilence.Forms.Interop;

namespace WinFormsInterop;

internal static class Program
{
    // This sample runs as a WinForms-hosted app to demonstrate Direction B (WF → MF).
    // The WinForms shell opens Majorsilence.Forms windows; those MF windows can in turn
    // open legacy WinForms forms, demonstrating the full round-trip (Direction A: MF → WF).
    //
    // On Windows, Avalonia's Win32 backend hooks into the existing Win32 message pump,
    // so a single WF.Application.Run call services both toolkits.
    [STAThread]
    static void Main()
    {
        WF.Application.EnableVisualStyles();
        WF.Application.SetCompatibleTextRenderingDefault(false);
        WF.Application.SetHighDpiMode(WF.HighDpiMode.PerMonitorV2);

        // Initialize Majorsilence.Forms (Avalonia) so it shares this Win32 pump.
        // Call this before any Majorsilence window is created.
        WindowsFormsInterop.InitializeMajorsilence();

        WF.Application.Run(new WinFormsShellForm());
    }
}
