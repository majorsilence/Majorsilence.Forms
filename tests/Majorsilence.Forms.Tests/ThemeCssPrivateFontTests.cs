using System;
using System.IO;
using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Text;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A font registered with PrivateFontCollection is unknown to the system font manager, and the CSS font-family resolver asked only
    // that manager. So `new Font ("Caladea", ...)` drew the font while `font-family: "Caladea"` silently fell back to the default face:
    // the theme could not name a bundled typeface. Every test names a family that exists only because a collection loaded it.
    [Collection ("Headless")]
    public class ThemeCssPrivateFontTests : IDisposable
    {
        private const string Missing = "No Such Family 4E5A9C";

        private static readonly string fontFile = Path.Combine (Majorsilence.Forms.Drawing.FontResourceLoader.GetFontDirectory (), "Caladea-Regular.ttf");

        // Registered on demand, not in the constructor, so a test can resolve a name before the font exists.
        private PrivateFontCollection? collection;

        public ThemeCssPrivateFontTests ()
        {
            Assert.True (File.Exists (fontFile), $"expected the bundled font at {fontFile}");
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            collection?.Dispose ();

            foreach (var name in Theme.RegisteredThemes.ToList ())
                Theme.UnregisterTheme (name);

            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        // The family name the bundled file declares, read without leaving it registered.
        private static string FamilyOfBundledFont ()
        {
            using var scratch = new PrivateFontCollection ();
            scratch.AddFontFile (fontFile);
            return scratch.Families[0].Name;
        }

        private string Register ()
        {
            collection = new PrivateFontCollection ();
            collection.AddFontFile (fontFile);
            return collection.Families[0].Name;
        }

        // ---- the theme tokens ---------------------------------------------------------------------

        [Fact]
        public void A_theme_font_token_resolves_a_private_family ()
        {
            var family = Register ();

            Theme.LoadFromCss ($":root {{ --ui-font: \"{family}\"; }}");

            Assert.Equal (family, Theme.UIFont.FamilyName);
        }

        [Fact]
        public void A_private_family_wins_over_a_fallback_listed_after_it ()
        {
            var family = Register ();

            Theme.LoadFromCss ($":root {{ --ui-font: \"{family}\", sans-serif; }}");

            Assert.Equal (family, Theme.UIFont.FamilyName);
        }

        [Fact]
        public void A_family_the_system_has_still_wins_when_it_is_listed_first ()
        {
            var installed = SystemFonts.DefaultTypeface.FamilyName;
            var family = Register ();

            Theme.LoadFromCss ($":root {{ --ui-font: \"{installed}\", \"{family}\"; }}");

            Assert.Equal (installed, Theme.UIFont.FamilyName);
        }

        [Fact]
        public void A_private_family_can_be_the_second_choice ()
        {
            var family = Register ();

            Theme.LoadFromCss ($":root {{ --ui-font: \"{Missing}\", \"{family}\"; }}");

            Assert.Equal (family, Theme.UIFont.FamilyName);
        }

        [Fact]
        public void A_family_nobody_has_still_falls_back_and_never_resolves_a_private_one ()
        {
            var family = Register ();

            Theme.LoadFromCss ($":root {{ --ui-font: \"{Missing}\"; }}");

            Assert.NotEqual (family, Theme.UIFont.FamilyName);
        }

        [Fact]
        public void A_family_registered_after_its_name_was_first_resolved_is_found_afterwards ()
        {
            // The resolver caches by family list. A cached fallback must not hide a font registered later, which is what
            // loading a bundled font after the first theme load would otherwise do.
            var family = FamilyOfBundledFont ();
            var list = new[] { family };

            var before = ThemeCssValues.GetTypeface (list, SKFontStyleWeight.Normal, SKFontStyleSlant.Upright);
            Assert.NotEqual (family, before.FamilyName);

            Register ();

            var after = ThemeCssValues.GetTypeface (list, SKFontStyleWeight.Normal, SKFontStyleSlant.Upright);
            Assert.Equal (family, after.FamilyName);
        }

        [Fact]
        public void A_disposed_collection_stops_resolving ()
        {
            var family = Register ();
            var list = new[] { family };
            Assert.Equal (family, ThemeCssValues.GetTypeface (list, SKFontStyleWeight.Normal, SKFontStyleSlant.Upright).FamilyName);

            collection!.Dispose ();
            collection = null;

            Assert.NotEqual (family, ThemeCssValues.GetTypeface (list, SKFontStyleWeight.Normal, SKFontStyleSlant.Upright).FamilyName);
        }

        // ---- a rule on a control ------------------------------------------------------------------

        private static byte[] Render (string? css, Font? labelFont)
        {
            HeadlessRenderer.Use ();
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
            if (css is not null)
                Theme.LoadFromCss (css);

            using var form = new Form { ClientSize = new System.Drawing.Size (420, 80) };
            var label = new Label { Text = "Hamburgefonstiv 0123", Left = 8, Top = 8, Width = 400, Height = 50, AutoSize = false };
            if (labelFont is not null)
                label.Font = labelFont;
            form.Controls.Add (label);
            form.Show ();

            return HeadlessRenderer.CapturePng (form, 420, 80);
        }

        [Fact]
        public void A_form_rule_draws_its_children_in_the_private_family ()
        {
            var family = Register ();

            // The relationship: naming the family in CSS draws what naming it in code draws, and not what an unresolvable name draws.
            var viaCss = Render ($"Form {{ font-family: \"{family}\"; font-size: 24px; }}", null);

            using var font = new Font (family, 24f, FontStyle.Regular, GraphicsUnit.Pixel);
            var viaApi = Render (null, font);
            var unresolvable = Render ($"Form {{ font-family: \"{Missing}\"; font-size: 24px; }}", null);

            Assert.NotEqual (unresolvable, viaApi);
            Assert.Equal (viaApi, viaCss);
        }

        [Fact]
        public void A_form_rule_with_a_private_family_first_and_a_fallback_after_it_draws_the_private_one ()
        {
            var family = Register ();

            var viaCss = Render ($"Form {{ font-family: \"{family}\", sans-serif; font-size: 24px; }}", null);

            using var font = new Font (family, 24f, FontStyle.Regular, GraphicsUnit.Pixel);
            Assert.Equal (Render (null, font), viaCss);
        }
    }
}
