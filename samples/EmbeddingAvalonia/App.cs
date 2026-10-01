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
        // resource that the Majorsilence.Forms theme bridge picks up.
        Styles.Add (new FluentTheme ());
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted ()
    {
        // One stylesheet for both halves of the window: AvaloniaCssTheme writes the Fluent resources and
        // styles for the native controls and applies the same sheet to Majorsilence.Forms' Theme.
        if (Program.ThemePath is { } path)
            Majorsilence.Forms.Theming.Avalonia.AvaloniaCssTheme.Apply (System.IO.File.ReadAllText (path));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow ();

        base.OnFrameworkInitializationCompleted ();
    }
}
