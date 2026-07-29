using System.Text.Json;
using System.Text.Json.Serialization;

namespace PointOfSale.Client.Services;

public enum TerminalMode
{
    Standard,
    Kiosk,
}

public sealed class AppSettings
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:5299";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TerminalMode Mode { get; set; } = TerminalMode.Standard;

    public string KioskPin { get; set; } = "0000";

    public static AppSettings Load(string[] args)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var settings = File.Exists(settingsPath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new AppSettings()
            : new AppSettings();

        // Command-line overrides let a physical terminal be provisioned without editing
        // appsettings.json: e.g. a self-checkout kiosk shortcut passes --mode=Kiosk.
        foreach (var arg in args)
        {
            if (arg.Equals("--kiosk", StringComparison.OrdinalIgnoreCase))
                settings.Mode = TerminalMode.Kiosk;
            else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<TerminalMode>(arg["--mode=".Length..], true, out var mode))
                settings.Mode = mode;
            else if (arg.StartsWith("--api-url=", StringComparison.OrdinalIgnoreCase))
                settings.ApiBaseUrl = arg["--api-url=".Length..];
        }

        return settings;
    }
}
