using Majorsilence.Forms;
using PointOfSale.Client.Services;

namespace PointOfSale.Client;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var settings = AppSettings.Load(args);
        var api = new ApiClient(settings.ApiBaseUrl);

        // The framework ships a BuiltInTheme.PointOfSale built for exactly this scenario:
        // deep navy canvas, amber accent (selection/focus), green accent2 (used below for
        // "go" actions like Tender/Log In), high-contrast borders — a real register-terminal look.
        Theme.SetBuiltInTheme(BuiltInTheme.PointOfSale);

        // This is a touchscreen POS — bump the framework's 14pt/12pt defaults so text and
        // touch targets are legible/tappable at arm's length, not desktop-density.
        Theme.FontSize = 26;
        Theme.ItemFontSize = 24;

        Application.Run(new MainForm(settings, api));
    }
}
