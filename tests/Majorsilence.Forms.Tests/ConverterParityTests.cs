using System;
using System.ComponentModel;
using System.Globalization;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The converters designer and .resx code round-trips values through. A converter that parses but
    // does not format (or vice versa) silently loses the value on the next save, so every test here
    // is a round-trip rather than a one-way conversion.
    public class ConverterParityTests
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Theory]
        [InlineData ("50%", 0.5d)]
        [InlineData ("100%", 1d)]
        [InlineData ("0%", 0d)]
        public void Opacity_reads_a_percentage (string text, double expected)
        {
            var converter = new OpacityConverter ();

            Assert.Equal (expected, Assert.IsType<double> (converter.ConvertFrom (null, Invariant, text)), 5);
        }

        [Fact]
        public void Opacity_clamps_out_of_range_percentages ()
        {
            var converter = new OpacityConverter ();

            Assert.Equal (1d, Assert.IsType<double> (converter.ConvertFrom (null, Invariant, "400%")), 5);
            Assert.Equal (0d, Assert.IsType<double> (converter.ConvertFrom (null, Invariant, "-20%")), 5);
        }

        [Fact]
        public void Opacity_writes_a_percentage_back ()
        {
            var converter = new OpacityConverter ();

            Assert.Equal ("50%", converter.ConvertTo (null, Invariant, 0.5d, typeof (string)));
        }

        [Fact]
        public void Image_index_uses_none_for_minus_one ()
        {
            var converter = new ImageIndexConverter ();

            Assert.Equal ("(none)", converter.ConvertTo (null, Invariant, -1, typeof (string)));
            Assert.Equal (-1, converter.ConvertFrom (null, Invariant, "(none)"));
            Assert.Equal (3, converter.ConvertFrom (null, Invariant, "3"));
        }

        [Fact]
        public void Image_key_uses_none_for_the_empty_string ()
        {
            var converter = new ImageKeyConverter ();

            Assert.Equal ("(none)", converter.ConvertTo (null, Invariant, string.Empty, typeof (string)));
            Assert.Equal (string.Empty, converter.ConvertFrom (null, Invariant, "(none)"));
            Assert.Equal ("badge", converter.ConvertFrom (null, Invariant, "badge"));
        }

        [Fact]
        public void Tree_view_image_index_has_a_second_sentinel_for_default ()
        {
            var converter = new TreeViewImageIndexConverter ();

            // -1 is "no image"; -2 is "whatever the tree view's own default is". Two sentinels, so
            // the round-trip has to keep them apart or a node inherits an image it never had.
            Assert.Equal ("(default)", converter.ConvertTo (null, Invariant, -2, typeof (string)));
            Assert.Equal ("(none)", converter.ConvertTo (null, Invariant, -1, typeof (string)));
            Assert.Equal (-2, converter.ConvertFrom (null, Invariant, "(default)"));
            Assert.Equal (-1, converter.ConvertFrom (null, Invariant, "(none)"));
        }

        [Fact]
        public void Selection_range_round_trips_through_the_list_separator ()
        {
            var converter = new SelectionRangeConverter ();
            var range = new SelectionRange (new DateTime (2026, 1, 2), new DateTime (2026, 1, 9));

            var text = Assert.IsType<string> (converter.ConvertTo (null, Invariant, range, typeof (string)));
            var back = Assert.IsType<SelectionRange> (converter.ConvertFrom (null, Invariant, text));

            Assert.Equal (range.Start, back.Start);
            Assert.Equal (range.End, back.End);
        }

        [Fact]
        public void Cursor_converter_names_the_stock_cursors ()
        {
            var converter = new CursorConverter ();

            Assert.Equal (Cursors.Hand, converter.ConvertFrom (null, Invariant, "Hand"));
            Assert.Equal ("Hand", converter.ConvertTo (null, Invariant, Cursors.Hand, typeof (string)));
        }

        [Fact]
        public void Keys_converter_round_trips_a_modified_key ()
        {
            var converter = new KeysConverter ();
            const Keys shortcut = Keys.Control | Keys.Shift | Keys.S;

            var text = Assert.IsType<string> (converter.ConvertTo (null, Invariant, shortcut, typeof (string)));

            Assert.Equal ("Ctrl+Shift+S", text);
            Assert.Equal (shortcut, converter.ConvertFrom (null, Invariant, text));
        }

        [Fact]
        public void Keys_converter_orders_modifiers_the_way_winforms_does ()
        {
            var converter = new KeysConverter ();

            // Ctrl, then Shift, then Alt -- whatever order the flags were combined in.
            Assert.Equal ("Ctrl+Shift+Alt+F4",
                converter.ConvertTo (null, Invariant, Keys.Alt | Keys.Shift | Keys.Control | Keys.F4, typeof (string)));
        }

        [Fact]
        public void Keys_converter_compares_keys ()
        {
            var converter = new KeysConverter ();

            Assert.True (((System.Collections.IComparer) converter).Compare (Keys.A, Keys.B) < 0);
        }

        [Fact]
        public void Link_area_round_trips ()
        {
            var converter = new LinkArea.LinkAreaConverter ();
            var area = new LinkArea (4, 11);

            var text = Assert.IsType<string> (converter.ConvertTo (null, Invariant, area, typeof (string)));
            var back = Assert.IsType<LinkArea> (converter.ConvertFrom (null, Invariant, text));

            Assert.Equal (area.Start, back.Start);
            Assert.Equal (area.Length, back.Length);
        }
    }
}
