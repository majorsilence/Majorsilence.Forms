using System;
using System.IO;
using System.Linq;
using System.Text;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Tests for the XML-defined custom theme support added to the static Theme class. Because Theme
    // is global state, each test resets to a known built-in theme and unregisters anything it
    // registers so tests don't leak into one another.
    public class ThemeXmlTests : IDisposable
    {
        public ThemeXmlTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);

            foreach (var name in Theme.RegisteredThemes.ToList ())
                Theme.UnregisterTheme (name);

            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        [Fact]
        public void RegisterTheme_ReturnsName_AndIsRegistered ()
        {
            var name = Theme.RegisterTheme ("<Theme name='Ocean'><AccentColor>#FF1E90FF</AccentColor></Theme>");

            Assert.Equal ("Ocean", name);
            Assert.True (Theme.IsThemeRegistered ("Ocean"));
            Assert.Contains ("Ocean", Theme.RegisteredThemes);
        }

        [Fact]
        public void ApplyTheme_SetsColorsAndFontSize ()
        {
            Theme.RegisterTheme (@"
                <Theme name='Ocean'>
                    <AccentColor>#FF1E90FF</AccentColor>
                    <BackgroundColor>10,25,41</BackgroundColor>
                    <FontSize>17</FontSize>
                </Theme>");

            Theme.ApplyTheme ("Ocean");

            Assert.Equal (SKColor.Parse ("#FF1E90FF"), Theme.AccentColor);
            Assert.Equal (new SKColor (10, 25, 41), Theme.BackgroundColor);
            Assert.Equal (17, Theme.FontSize);
        }

        [Fact]
        public void ApplyTheme_WithBuiltInBase_StartsFromBaseThenOverrides ()
        {
            Theme.RegisterTheme (@"
                <Theme name='DarkOcean' base='Dark'>
                    <AccentColor>#FF1E90FF</AccentColor>
                </Theme>");

            Theme.ApplyTheme ("DarkOcean");

            // The override wins...
            Assert.Equal (SKColor.Parse ("#FF1E90FF"), Theme.AccentColor);
            // ...and an un-overridden property comes from the Dark base, not the Light starting state.
            Assert.Equal (SKColor.Parse ("#FF282828"), Theme.BackgroundColor);
        }

        [Fact]
        public void ApplyTheme_WithRegisteredThemeAsBase_LayersOverrides ()
        {
            Theme.RegisterTheme ("<Theme name='Base' base='Dark'><AccentColor>10,10,10</AccentColor></Theme>");
            Theme.RegisterTheme ("<Theme name='Derived' base='Base'><ForegroundColor>1,2,3</ForegroundColor></Theme>");

            Theme.ApplyTheme ("Derived");

            Assert.Equal (new SKColor (10, 10, 10), Theme.AccentColor);   // from Base
            Assert.Equal (new SKColor (1, 2, 3), Theme.ForegroundColor);  // from Derived
            Assert.Equal (SKColor.Parse ("#FF282828"), Theme.BackgroundColor); // from Dark via Base
        }

        [Fact]
        public void ApplyTheme_RaisesThemeChangedExactlyOnce ()
        {
            Theme.RegisterTheme (@"
                <Theme name='Multi'>
                    <AccentColor>1,1,1</AccentColor>
                    <BackgroundColor>2,2,2</BackgroundColor>
                    <ForegroundColor>3,3,3</ForegroundColor>
                </Theme>");

            var count = 0;
            EventHandler handler = (_, _) => count++;
            Theme.ThemeChanged += handler;
            try {
                Theme.ApplyTheme ("Multi");
            } finally {
                Theme.ThemeChanged -= handler;
            }

            Assert.Equal (1, count);
        }

        [Fact]
        public void ParseFont_AppliesFamilyAndWeight ()
        {
            Theme.RegisterTheme (@"
                <Theme name='Fonts'>
                    <UIFont family='Courier New' />
                    <UIFontBold>Courier New</UIFontBold>
                </Theme>");

            Theme.ApplyTheme ("Fonts");

            Assert.Equal ("Courier New", Theme.UIFont.FamilyName);
            Assert.True (Theme.UIFontBold.IsBold);
        }

        [Fact]
        public void LoadFromXml_AppliesWithoutRegistering ()
        {
            Theme.LoadFromXml ("<Theme><AccentColor>5,6,7</AccentColor></Theme>");

            Assert.Equal (new SKColor (5, 6, 7), Theme.AccentColor);
            Assert.Empty (Theme.RegisteredThemes);
        }

        [Fact]
        public void RegisterThemeFromStream_Works ()
        {
            using var stream = new MemoryStream (Encoding.UTF8.GetBytes (
                "<Theme name='Streamed'><AccentColor>9,9,9</AccentColor></Theme>"));

            var name = Theme.RegisterThemeFromStream (stream);
            Theme.ApplyTheme (name);

            Assert.Equal ("Streamed", name);
            Assert.Equal (new SKColor (9, 9, 9), Theme.AccentColor);
        }

        [Fact]
        public void RegisterTheme_SameName_Replaces ()
        {
            Theme.RegisterTheme ("<Theme name='Dup'><AccentColor>1,1,1</AccentColor></Theme>");
            Theme.RegisterTheme ("<Theme name='Dup'><AccentColor>2,2,2</AccentColor></Theme>");

            Theme.ApplyTheme ("Dup");

            Assert.Single (Theme.RegisteredThemes, n => n == "Dup");
            Assert.Equal (new SKColor (2, 2, 2), Theme.AccentColor);
        }

        [Fact]
        public void UnregisterTheme_RemovesIt ()
        {
            Theme.RegisterTheme ("<Theme name='Temp'><AccentColor>1,1,1</AccentColor></Theme>");

            Assert.True (Theme.UnregisterTheme ("Temp"));
            Assert.False (Theme.IsThemeRegistered ("Temp"));
            Assert.False (Theme.UnregisterTheme ("Temp"));
        }

        [Fact]
        public void RegisterTheme_MissingName_Throws () =>
            Assert.Throws<ThemeXmlException> (() =>
                Theme.RegisterTheme ("<Theme><AccentColor>1,1,1</AccentColor></Theme>"));

        [Fact]
        public void RegisterTheme_UnknownProperty_Throws () =>
            Assert.Throws<ThemeXmlException> (() =>
                Theme.RegisterTheme ("<Theme name='Bad'><NotAColor>1,1,1</NotAColor></Theme>"));

        [Fact]
        public void RegisterTheme_InvalidColor_Throws () =>
            Assert.Throws<ThemeXmlException> (() =>
                Theme.RegisterTheme ("<Theme name='Bad'><AccentColor>not-a-color</AccentColor></Theme>"));

        [Fact]
        public void RegisterTheme_WrongRootElement_Throws () =>
            Assert.Throws<ThemeXmlException> (() =>
                Theme.RegisterTheme ("<Palette name='Bad'></Palette>"));

        [Fact]
        public void ApplyTheme_UnknownName_Throws () =>
            Assert.Throws<ArgumentException> (() => Theme.ApplyTheme ("DoesNotExist"));

        [Fact]
        public void ApplyTheme_UnknownBase_Throws ()
        {
            Theme.RegisterTheme ("<Theme name='BadBase' base='Nope'><AccentColor>1,1,1</AccentColor></Theme>");

            Assert.Throws<ThemeXmlException> (() => Theme.ApplyTheme ("BadBase"));
        }

        [Fact]
        public void ApplyTheme_CyclicBase_Throws ()
        {
            Theme.RegisterTheme ("<Theme name='A' base='B'><AccentColor>1,1,1</AccentColor></Theme>");
            Theme.RegisterTheme ("<Theme name='B' base='A'><AccentColor>2,2,2</AccentColor></Theme>");

            Assert.Throws<ThemeXmlException> (() => Theme.ApplyTheme ("A"));
        }
    }
}
