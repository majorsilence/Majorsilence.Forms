using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The Android head that `dotnet new majorsilenceforms --IncludeAndroid` generates crashed on its very first frame: AvaloniaMainActivity is an
// androidx AppCompatActivity, and its setContentView throws "You need to use a Theme.AppCompat theme (or descendant) with this activity" under
// any other theme. The template's MainActivity named the plain framework theme "@android:style/Theme.NoTitleBar". Nothing compiles-checks a
// theme name, and CI's template job only BUILDS the generated project, so the crash was invisible until someone launched it.
//
// This reads the template's own source, so it cannot show that an emulator launches the app; it pins the specific mistake, which is enough
// to stop it coming back. samples/Gallery.Android documents the same failure and has the same fix.
public class AndroidTemplateThemeTests
{
    private static string TemplateAndroidDirectory ()
    {
        var dir = new DirectoryInfo (AppContext.BaseDirectory);
        while (dir is not null && !File.Exists (Path.Combine (dir.FullName, "Majorsilence.Forms.slnx")))
            dir = dir.Parent;

        Assert.NotNull (dir);
        return Path.Combine (dir!.FullName, "tools", "Majorsilence.Forms.Templates", "content", "majorsilenceforms", "MajorsilenceFormsApp.Android");
    }

    private static string ThemeNamedByMainActivity ()
    {
        var source = File.ReadAllText (Path.Combine (TemplateAndroidDirectory (), "MainActivity.cs"));
        var match = Regex.Match (source, "Theme\\s*=\\s*\"(?<theme>[^\"]+)\"");
        Assert.True (match.Success, "MainActivity.cs no longer names a Theme, so it would get the platform default, which is not an AppCompat theme either");
        return match.Groups["theme"].Value;
    }

    [Fact]
    public void TheActivitysTheme_IsNotAPlainFrameworkTheme ()
    {
        var theme = ThemeNamedByMainActivity ();

        Assert.False (theme.StartsWith ("@android:style/", StringComparison.Ordinal),
            $"MainActivity uses {theme}, a framework theme. AvaloniaMainActivity is an AppCompatActivity and throws on the first frame under any theme that does not descend from Theme.AppCompat");
        Assert.StartsWith ("@style/", theme);
    }

    [Fact]
    public void TheNamedStyle_IsDefinedInTheTemplate_AndDescendsFromAppCompat ()
    {
        var theme = ThemeNamedByMainActivity ();
        Assert.StartsWith ("@style/", theme);       // a "@android:style/..." theme is not defined by the template at all
        var name = theme["@style/".Length..];
        var styles = Path.Combine (TemplateAndroidDirectory (), "Resources", "values", "styles.xml");
        Assert.True (File.Exists (styles), $"MainActivity names the style {name} but the template has no Resources/values/styles.xml to define it");

        var style = XDocument.Load (styles).Descendants ("style").SingleOrDefault (s => (string?)s.Attribute ("name") == name);

        Assert.True (style is not null, $"styles.xml does not define a style called {name}");
        var parent = (string?)style!.Attribute ("parent") ?? "";
        Assert.True (parent.StartsWith ("Theme.AppCompat", StringComparison.Ordinal), $"style {name} has parent \"{parent}\", which is not Theme.AppCompat or a descendant");
    }

    // AvaloniaPlatformBackend.RaiseBackRequested is not automatic: the framework cannot discover the current Activity, so the app's own
    // MainActivity has to forward AvaloniaMainActivity.BackRequested. Without it a Form.BackRequested handler (closing a sheet, stepping back a
    // screen) never runs on Android and the back button always leaves the app. Like the theme, nothing compile-checks this, so pin it.
    [Fact]
    public void TheActivity_ForwardsTheBackButtonToTheFramework ()
    {
        var source = File.ReadAllText (Path.Combine (TemplateAndroidDirectory (), "MainActivity.cs"));

        Assert.Matches (@"BackRequested\s*\+=", source);
        Assert.Contains ("AvaloniaPlatformBackend.RaiseBackRequested", source);
        Assert.Matches (@"e\.Handled\s*=\s*AvaloniaPlatformBackend\.RaiseBackRequested", source);
    }
}
