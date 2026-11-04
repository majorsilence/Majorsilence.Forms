using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported app's text is laid out on GDI's pixel grid, as upstream's TextRenderer lays it out: each glyph advances by
    // its hinted advance, a whole number of pixels, and each kerning pair by whole pixels. Measured on WinForms (.NET 10)
    // with TextRenderer.MeasureText (NoPadding) on every prefix of each caption (probe wftextwidth): 8.25pt Microsoft Sans
    // Serif "Clear Fields" is 54px at 96 DPI and 116px at 192 DPI, where RichTextKit's linear advances made it 58.74px and
    // 117.48px. A caption that fit its button in the designer wrapped here.
    [Collection ("Headless")]
    public class GdiTextGridTests
    {
        // The probe's widths, by caption, family, weight and pixel size.
        private static readonly Dictionary<(string Text, string Family, bool Bold, int Size), int> gdi_widths = new () {
            [("Clear Fields", "Microsoft Sans Serif", false, 11)] = 54,
            [("Clear Fields", "Microsoft Sans Serif", false, 16)] = 83,
            [("Clear Fields", "Microsoft Sans Serif", false, 17)] = 90,
            [("Clear Fields", "Microsoft Sans Serif", false, 22)] = 116,
            [("Clear Fields", "Microsoft Sans Serif", false, 24)] = 131,
            [("Clear Fields", "Microsoft Sans Serif", false, 32)] = 170,
            [("Customer Number:", "Microsoft Sans Serif", false, 11)] = 87,
            [("Customer Number:", "Microsoft Sans Serif", false, 16)] = 133,
            [("Customer Number:", "Microsoft Sans Serif", false, 22)] = 185,
            [("Customer Number:", "Microsoft Sans Serif", false, 32)] = 272,
            [("Postal/Zip", "Microsoft Sans Serif", true, 11)] = 59,
            [("Postal/Zip", "Microsoft Sans Serif", true, 16)] = 80,
            [("Postal/Zip", "Microsoft Sans Serif", true, 22)] = 107,
            [("Postal/Zip", "Microsoft Sans Serif", true, 32)] = 153,
            [("Clear Fields", "Segoe UI", false, 12)] = 60,
            [("Clear Fields", "Segoe UI", false, 18)] = 89,
            [("Clear Fields", "Segoe UI", false, 24)] = 122,
            // Kerned: A-V, V-A, A-T, T-A and T-o are each a whole pixel or two closer.
            [("AVATAR Tolerance", "Segoe UI", false, 12)] = 95,
            [("AVATAR Tolerance", "Segoe UI", false, 18)] = 141,
            [("AVATAR Tolerance", "Segoe UI", false, 24)] = 189,
            [("Postal/Zip", "Segoe UI", false, 12)] = 54,
            [("Postal/Zip", "Segoe UI", false, 18)] = 79,
            [("Postal/Zip", "Segoe UI", false, 24)] = 105,
        };

        public static TheoryData<string, string, float, bool> Captions => new () {
            { "Clear Fields", "Microsoft Sans Serif", 8.25f, false },
            { "Clear Fields", "Microsoft Sans Serif", 12f, false },
            { "Customer Number:", "Microsoft Sans Serif", 8.25f, false },
            { "Customer Number:", "Microsoft Sans Serif", 12f, false },
            { "Postal/Zip", "Microsoft Sans Serif", 8.25f, true },
            { "Postal/Zip", "Microsoft Sans Serif", 12f, true },
            { "Clear Fields", "Segoe UI", 9f, false },
            { "AVATAR Tolerance", "Segoe UI", 9f, false },
            { "Postal/Zip", "Segoe UI", 9f, false },
        };

        [Theory]
        [MemberData (nameof (Captions))]
        public void A_ported_caption_measures_as_upstream_TextRenderer (string text, string family, float points, bool bold)
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            try {
                var (face, size) = FontOf (family, points, bold);
                var width = TextMeasurer.MeasureText (text, face, size).Width;

                // Whole pixels on every platform: the grid is GDI's, whatever the fonts.
                Assert.Equal (System.Math.Round (width), width);

                if (System.OperatingSystem.IsWindows ())
                    Assert.Equal (gdi_widths[(text, family, bold, size)], width);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // The caption a ported app's 8.25pt button gives exactly upstream's text width stays on one line; it wrapped to
        // "Clear" over "Fields".
        [Fact]
        public void A_caption_in_a_field_as_wide_as_upstream_measures_it_does_not_wrap ()
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            try {
                var (face, size) = FontOf ("Microsoft Sans Serif", 8.25f, false);
                var one_line = TextMeasurer.MeasureText ("Clear Fields", face, size).Width;
                var line = TextMeasurer.MeasureText ("X", face, size).Height;

                if (System.OperatingSystem.IsWindows ())
                    Assert.Equal (gdi_widths[("Clear Fields", "Microsoft Sans Serif", false, size)], one_line);

                var wrapped = TextMeasurer.MeasureText ("Clear Fields", face, size, new Size ((int) one_line, int.MaxValue));
                Assert.Equal (line, wrapped.Height);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // Each character is drawn where upstream draws it: the caret stops (TextRenderer.MeasureText of each prefix) of
        // 8.25pt "Clear Fields".
        [Fact]
        public void A_ported_caption_places_each_glyph_on_upstream_pen_positions ()
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            try {
                var (face, size) = FontOf ("Microsoft Sans Serif", 8.25f, false);
                var block = TextMeasurer.CreateTextBlock ("Clear Fields", face, size, new Size (1000, int.MaxValue));
                var stops = new List<int> ();

                for (var i = 1; i <= "Clear Fields".Length; i++) {
                    var x = block.GetCaretInfo (new Topten.RichTextKit.CaretPosition (i)).CaretXCoord;
                    Assert.True (System.Math.Abs (x - System.Math.Round (x)) < 0.1f, $"stop {i} at {x}");
                    stops.Add ((int) System.Math.Round (x));
                }

                if (System.OperatingSystem.IsWindows ()) {
                    var expected = size switch {
                        11 => new[] { 7, 9, 15, 21, 24, 27, 33, 35, 41, 43, 49, 54 },
                        22 => new[] { 16, 21, 33, 45, 52, 58, 71, 76, 88, 93, 105, 116 },
                        _ => null,
                    };

                    if (expected is not null)
                        Assert.Equal (expected, stops);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // GDI centres each line on a whole pixel, rounding odd slack down (DT_CENTER halves it in integer arithmetic): a
        // centred caption is drawn exactly as the same caption left aligned floor(slack / 2) pixels in. Here a line with
        // odd slack sat half a pixel right, every stem smeared across two columns; a one-line button's "Show" lost a whole
        // row of ink to it.
        [Theory]
        [InlineData ("Show Emails", 0)]
        [InlineData ("Show Emails", 1)]
        [InlineData ("Clear Fields", 0)]
        [InlineData ("Clear Fields", 1)]
        public void A_centred_line_sits_on_a_whole_pixel (string text, int extra)
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            try {
                var (face, size) = FontOf ("Microsoft Sans Serif", 8.25f, false);
                var width = (int) TextMeasurer.MeasureText (text, face, size).Width;
                var field = width + 9 + extra;     // odd slack, then even
                var height = 3 * (int) TextMeasurer.MeasureText ("X", face, size).Height;

                using var centred = Draw (face, size, text, new Rectangle (0, 0, field, height), ContentAlignment.MiddleCenter, field, height);
                using var left = Draw (face, size, text, new Rectangle ((field - width) / 2, 0, width + 1, height), ContentAlignment.MiddleLeft, field, height);

                for (var y = 0; y < height; y++)
                    for (var x = 0; x < field; x++)
                        Assert.True (centred.GetPixel (x, y) == left.GetPixel (x, y), $"pixel ({x}, {y})");
            } finally {
                Application.SetDefaultFont (null!);
            }

            static SKBitmap Draw (SKTypeface face, int size, string text, Rectangle bounds, ContentAlignment alignment, int width, int height)
            {
                var bitmap = new SKBitmap (width, height);
                using var canvas = new SKCanvas (bitmap);
                canvas.Clear (SKColors.White);
                canvas.DrawText (text, face, size, bounds, SKColors.Black, alignment);
                return bitmap;
            }
        }

        // An app that never chose a font keeps RichTextKit's linear advances: the theme's look is unchanged.
        [Fact]
        public void Text_keeps_its_linear_advances_without_a_default_font ()
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            var (face, size) = FontOf ("Microsoft Sans Serif", 8.25f, false);
            var ported = TextMeasurer.MeasureText ("Clear Fields", face, size).Width;
            Application.SetDefaultFont (null!);

            var width = TextMeasurer.MeasureText ("Clear Fields", face, size).Width;

            using var font = new SKFont (face, size) { LinearMetrics = true, Hinting = SKFontHinting.None, Subpixel = true };
            Assert.Equal (font.MeasureText ("Clear Fields"), width, 0.5f);

            // The cache is not shared between the two: text laid out for the ported app is not reused.
            if (System.OperatingSystem.IsWindows ())
                Assert.NotEqual (ported, width);
        }

        // The device pixel size of a control's font, at the headless scale, and its typeface.
        private static (SKTypeface Face, int Size) FontOf (string family, float points, bool bold)
        {
            using var form = new Form ();
            var label = new Label {
                Font = new Majorsilence.Forms.Drawing.Font (family, points, bold ? Majorsilence.Forms.Drawing.FontStyle.Bold : Majorsilence.Forms.Drawing.FontStyle.Regular),
            };
            form.Controls.Add (label);

            return (label.GetEffectiveFont (), label.LogicalToDeviceUnits (label.GetEffectiveFontSize ()));
        }
    }
}
