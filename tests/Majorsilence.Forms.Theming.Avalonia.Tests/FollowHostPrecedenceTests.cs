using System;
using System.Threading.Tasks;
using Xunit;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    using global::Avalonia;
    using global::Avalonia.Styling;

    // #104: once a CSS theme is applied, MajorsilenceFormsTheme.FollowHost must not throw it away on the
    // next host variant change. It did: following the host was SetBuiltInTheme, which clears every
    // stylesheet -- and AvaloniaCssTheme.Apply sets RequestedThemeVariant itself, so the change arrived
    // the moment a theme was applied. These drive one variant change through the internal step rather
    // than FollowHost's process-wide subscription, which would outlive the test.
    public class FollowHostPrecedenceTests : IAsyncDisposable
    {
        public ValueTask DisposeAsync ()
        {
            GC.SuppressFinalize (this);
            return new ValueTask (Headless.Run (() => {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
            }));
        }

        private static SkiaSharp.SKColor BuiltIn (MF.BuiltInTheme theme, Func<SkiaSharp.SKColor> read)
        {
            MF.Theme.SetBuiltInTheme (theme);
            return read ();
        }

        [Fact]
        public Task A_sheet_without_extends_survives_a_variant_change_and_follows_it () => Headless.Run (() => {
            var dark = BuiltIn (MF.BuiltInTheme.Dark, () => MF.Theme.BackgroundColor);
            MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);

            MF.Theme.ApplyStyleSheet (MF.ThemeStyleSheet.Parse (":root { --accent-color: #123456; } Button { background-color: #FF00FF; }"));

            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            MF.MajorsilenceFormsTheme.Apply (Application.Current);

            Assert.NotEmpty (MF.Theme.CurrentStyleSheets);
            Assert.Equal (new SkiaSharp.SKColor (0xFF, 0x00, 0xFF), MF.Button.DefaultStyle.BackgroundColor);
            Assert.Equal (new SkiaSharp.SKColor (0x12, 0x34, 0x56), MF.Theme.AccentColor);   // the sheet's, not the host's
            Assert.Equal (dark, MF.Theme.BackgroundColor);                                   // what the sheet leaves follows the host
        });

        [Fact]
        public Task A_sheet_that_extends_a_built_in_theme_pins_its_variant () => Headless.Run (() => {
            var light = BuiltIn (MF.BuiltInTheme.Light, () => MF.Theme.BackgroundColor);

            MF.Theme.ApplyStyleSheet (MF.ThemeStyleSheet.Parse ("@theme \"Pinned\" extends Light; Button { background-color: #FF00FF; }"));

            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            MF.MajorsilenceFormsTheme.Apply (Application.Current);

            Assert.Equal (light, MF.Theme.BackgroundColor);
            Assert.Equal (new SkiaSharp.SKColor (0xFF, 0x00, 0xFF), MF.Button.DefaultStyle.BackgroundColor);
        });

        [Fact]
        public Task Without_a_sheet_the_host_variant_is_followed_as_before () => Headless.Run (() => {
            var dark = BuiltIn (MF.BuiltInTheme.Dark, () => MF.Theme.BackgroundColor);
            MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);

            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            MF.MajorsilenceFormsTheme.Apply (Application.Current);

            Assert.Equal (dark, MF.Theme.BackgroundColor);
            Assert.Empty (MF.Theme.CurrentStyleSheets);
        });
    }
}
