using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Imaging;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // The design-time converters. Every test is a round-trip: a converter that parses but does not
    // format loses the value the next time the designer saves, and that is invisible one way.
    public class DrawingConverterTests
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Fact]
        public void A_font_round_trips_through_its_designer_text ()
        {
            var converter = new FontConverter ();
            var font = new Font ("Segoe UI", 9f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point);

            var text = Assert.IsType<string> (converter.ConvertTo (null, Invariant, font, typeof (string)));
            var back = Assert.IsType<Font> (converter.ConvertFrom (null, Invariant, text));

            Assert.Equal (font.Name, back.Name);
            Assert.Equal (font.Size, back.Size);
            Assert.Equal (font.Style, back.Style);
            Assert.Equal (font.Unit, back.Unit);
        }

        [Fact]
        public void A_font_text_names_its_size_unit_and_style ()
        {
            var converter = new FontConverter ();
            var font = new Font ("Arial", 12f, FontStyle.Bold, GraphicsUnit.Point);

            Assert.Equal ("Arial, 12pt, style=Bold", converter.ConvertTo (null, Invariant, font, typeof (string)));
        }

        [Fact]
        public void A_plain_font_text_carries_no_style_clause ()
        {
            var converter = new FontConverter ();

            Assert.Equal ("Arial, 12pt",
                converter.ConvertTo (null, Invariant, new Font ("Arial", 12f), typeof (string)));
        }

        [Theory]
        [InlineData ("Arial, 12px", GraphicsUnit.Pixel)]
        [InlineData ("Arial, 12pt", GraphicsUnit.Point)]
        [InlineData ("Arial, 12in", GraphicsUnit.Inch)]
        [InlineData ("Arial, 12mm", GraphicsUnit.Millimeter)]
        [InlineData ("Arial, 12doc", GraphicsUnit.Document)]
        public void A_font_size_suffix_selects_the_unit (string text, GraphicsUnit expected)
        {
            var font = Assert.IsType<Font> (new FontConverter ().ConvertFrom (null, Invariant, text));

            Assert.Equal (expected, font.Unit);
            Assert.Equal (12f, font.Size);
        }

        [Fact]
        public void A_family_name_containing_the_separator_survives ()
        {
            // The family is not simply "everything before the first comma": a font really can be
            // called "Arial, Bold", and splitting naively would silently rename it.
            var font = Assert.IsType<Font> (new FontConverter ().ConvertFrom (null, Invariant, "Arial, Bold, 12pt"));

            Assert.Equal ("Arial, Bold", font.Name);
            Assert.Equal (12f, font.Size);
        }

        [Fact]
        public void An_empty_font_string_converts_to_null ()
        {
            Assert.Null (new FontConverter ().ConvertFrom (null, Invariant, "  "));
        }

        [Fact]
        public void A_font_can_be_built_from_designer_property_values ()
        {
            var converter = new FontConverter ();
            var values = new System.Collections.Hashtable {
                ["Name"] = "Verdana", ["Size"] = 11f, ["Unit"] = GraphicsUnit.Point, ["Bold"] = true,
            };

            var font = Assert.IsType<Font> (converter.CreateInstance (null, values));

            Assert.Equal ("Verdana", font.Name);
            Assert.Equal (11f, font.Size);
            Assert.True (font.Style.HasFlag (FontStyle.Bold));
        }

        [Fact]
        public void The_font_unit_converter_omits_the_units_that_are_not_font_sizes ()
        {
            var values = new FontConverter.FontUnitConverter ().GetStandardValues (null).Cast<GraphicsUnit> ().ToArray ();

            Assert.Contains (GraphicsUnit.Point, values);
            Assert.DoesNotContain (GraphicsUnit.World, values);
            Assert.DoesNotContain (GraphicsUnit.Display, values);
        }

        [Fact]
        public void The_font_name_converter_accepts_names_outside_its_list ()
        {
            using var converter = new FontConverter.FontNameConverter ();

            Assert.False (converter.GetStandardValuesExclusive (null));
            Assert.Equal ("A Font That Is Not Installed",
                converter.ConvertFrom (null, Invariant, "  A Font That Is Not Installed  "));
        }

        [Fact]
        public void An_image_format_round_trips_by_name ()
        {
            var converter = new ImageFormatConverter ();

            Assert.Equal ("Png", converter.ConvertTo (null, Invariant, ImageFormat.Png, typeof (string)));
            Assert.Same (ImageFormat.Png, converter.ConvertFrom (null, Invariant, "Png"));
            Assert.Same (ImageFormat.Jpeg, converter.ConvertFrom (null, Invariant, "jpeg"));
        }

        [Fact]
        public void An_unknown_image_format_name_is_rejected ()
        {
            Assert.Throws<FormatException> (
                () => new ImageFormatConverter ().ConvertFrom (null, Invariant, "Sixel"));
        }

        [Fact]
        public void An_image_round_trips_through_its_encoded_bytes ()
        {
            var converter = new ImageConverter ();
            using var source = new Bitmap (8, 4);

            var bytes = Assert.IsType<byte[]> (converter.ConvertTo (null, Invariant, source, typeof (byte[])));
            using var back = Assert.IsType<Bitmap> (converter.ConvertFrom (null, Invariant, bytes));

            Assert.Equal (source.Width, back.Width);
            Assert.Equal (source.Height, back.Height);
        }

        [Fact]
        public void An_absent_image_converts_to_an_empty_array ()
        {
            var converter = new ImageConverter ();

            Assert.Empty (Assert.IsType<byte[]> (converter.ConvertTo (null, Invariant, null, typeof (byte[]))));
            Assert.Equal ("(none)", converter.ConvertTo (null, Invariant, null, typeof (string)));
        }

        [Fact]
        public void An_icon_converts_to_an_image ()
        {
            var converter = new IconConverter ();

            Assert.True (converter.CanConvertTo (null, typeof (Image)));
            Assert.Equal ("(none)", converter.ConvertTo (null, Invariant, null, typeof (string)));
        }
    }
}
