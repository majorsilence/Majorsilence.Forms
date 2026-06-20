using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace EmbeddingAvalonia;

public sealed class App : Application
{
    public override void Initialize ()
    {
        // The host owns the theme — Fluent provides light/dark variants and the SystemAccentColor
        // resource that the Continuum.Forms theme bridge picks up.
        Styles.Add (new FluentTheme ());
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted ()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow ();

        base.OnFrameworkInitializationCompleted ();
    }
}
