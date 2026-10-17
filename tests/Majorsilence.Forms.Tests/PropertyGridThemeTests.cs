using System;
using System.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The PropertyGrid's colours default to theme tokens. They used to default to fixed SystemColors, so a
    // dark theme kept a white grid (found theming the Reporting designer, majorsilence/Reporting#448).
    [Collection ("Headless")]
    public class PropertyGridThemeTests : IDisposable
    {
        public PropertyGridThemeTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static Color C (SKColor c) => Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue);

        [Fact]
        public void Colours_follow_the_theme ()
        {
            var grid = new PropertyGrid ();
            Theme.SetBuiltInTheme (BuiltInTheme.Dark);

            Assert.Equal (C (Theme.ControlLowColor), grid.ViewBackColor);
            Assert.Equal (C (Theme.ForegroundColor), grid.ViewForeColor);
            Assert.Equal (C (Theme.BackgroundColor), grid.HelpBackColor);
            Assert.Equal (C (Theme.ForegroundDisabledColor), grid.DisabledItemForeColor);
        }

        [Fact]
        public void Colours_follow_a_scope ()
        {
            var grid = new PropertyGrid ();
            var scope = new ThemeScope { ControlLowColor = SKColors.Magenta };

            using (scope.Enter ())
                Assert.Equal (C (SKColors.Magenta), grid.ViewBackColor);
            Assert.NotEqual (C (SKColors.Magenta), grid.ViewBackColor);
        }

        [Fact]
        public void A_set_colour_wins_and_Empty_returns_to_the_theme ()
        {
            var grid = new PropertyGrid { ViewBackColor = Color.Orange };
            Theme.SetBuiltInTheme (BuiltInTheme.Dark);
            Assert.Equal (Color.Orange, grid.ViewBackColor);

            grid.ViewBackColor = Color.Empty;
            Assert.Equal (C (Theme.ControlLowColor), grid.ViewBackColor);
        }
    }
}
