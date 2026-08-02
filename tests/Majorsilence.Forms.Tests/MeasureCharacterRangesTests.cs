using Xunit;

// Bitmap/Font come from GlobalDrawingAliases.cs, already pinned to the fork's types. CharacterRange and
// StringFormat are not globally aliased and are ambiguous against the referenced real
// System.Drawing.Common, so they are pinned here. RectangleF is deliberately the real BCL primitive.
using RectangleF = System.Drawing.RectangleF;
using CharacterRange = Majorsilence.Forms.Drawing.CharacterRange;
using MSStringFormat = Majorsilence.Forms.Drawing.StringFormat;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers <c>Graphics.MeasureCharacterRanges</c>, added in Phase 1 of docs/gdi-gap-plan.md.
    ///
    /// Before it, <c>StringFormat.SetMeasurableCharacterRanges</c> stored ranges that nothing ever read —
    /// its own doc comment pointed at this method, which did not exist. That made the stored data dead,
    /// and is the "present but hollow" failure mode the phase targets.
    ///
    /// Assertions are deliberately relative (ordering, containment, non-zero extents) rather than exact
    /// pixel values: glyph advances differ between the bundled fallback fonts and whatever is installed
    /// on a given machine, so hardcoded widths would be flaky rather than correct.
    /// </summary>
    public class MeasureCharacterRangesTests
    {
        private const string Text = "Hello wonderful world";

        private static Graphics NewGraphics () => Graphics.FromImage (new Bitmap (400, 200));

        private static MSStringFormat FormatWith (params CharacterRange[] ranges)
        {
            var format = new MSStringFormat ();
            format.SetMeasurableCharacterRanges (ranges);
            return format;
        }

        [Fact]
        public void Returns_empty_when_no_ranges_were_set ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);

            var regions = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), new MSStringFormat ());

            Assert.Empty (regions);
        }

        [Fact]
        public void Returns_empty_when_the_format_is_null ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);

            Assert.Empty (g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), null));
        }

        [Fact]
        public void Returns_one_region_per_range ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            var format = FormatWith (new CharacterRange (0, 5), new CharacterRange (6, 9), new CharacterRange (16, 5));

            var regions = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), format);

            Assert.Equal (3, regions.Length);
            Assert.All (regions, r => Assert.False (r.GetBounds ().IsEmpty));
        }

        [Fact]
        public void Ranges_are_ordered_left_to_right_and_do_not_overlap ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            // "Hello" then "wonderful" — adjacent words on one unwrapped line.
            var format = FormatWith (new CharacterRange (0, 5), new CharacterRange (6, 9));

            var regions = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), format);
            var first = regions[0].GetBounds ();
            var second = regions[1].GetBounds ();

            Assert.True (first.Width > 0, "first range should have a measurable width");
            Assert.True (second.Width > 0, "second range should have a measurable width");
            Assert.True (second.Left >= first.Right - 1f,
                $"the later range should start at or after the earlier one ends ({second.Left} vs {first.Right})");
        }

        [Fact]
        public void A_range_covering_all_the_text_is_at_least_as_wide_as_any_sub_range ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            var whole = FormatWith (new CharacterRange (0, Text.Length));
            var part = FormatWith (new CharacterRange (0, 5));

            var wholeBounds = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), whole)[0].GetBounds ();
            var partBounds = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), part)[0].GetBounds ();

            Assert.True (wholeBounds.Width > partBounds.Width,
                $"whole-text width ({wholeBounds.Width}) should exceed a 5-character prefix ({partBounds.Width})");
        }

        [Fact]
        public void Regions_are_offset_by_the_layout_rectangle_origin ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            var format = FormatWith (new CharacterRange (0, 5));

            var atOrigin = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), format)[0].GetBounds ();
            var offset = g.MeasureCharacterRanges (Text, font, new RectangleF (100, 40, 400, 200), format)[0].GetBounds ();

            Assert.Equal (atOrigin.Left + 100f, offset.Left, 1);
            Assert.Equal (atOrigin.Top + 40f, offset.Top, 1);
        }

        [Fact]
        public void A_range_that_wraps_across_lines_spans_more_than_one_line ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            var format = FormatWith (new CharacterRange (0, Text.Length));

            // Wide enough for a word or two, narrow enough to force wrapping.
            var unwrapped = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 0, 200), format)[0].GetBounds ();
            var wrapped = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 60, 200), format)[0].GetBounds ();

            Assert.True (wrapped.Height > unwrapped.Height,
                $"wrapped text should occupy more vertical space ({wrapped.Height}) than one line ({unwrapped.Height})");
            Assert.True (wrapped.Width <= unwrapped.Width + 1f,
                "wrapped text should not be wider than the same text on a single line");
        }

        [Fact]
        public void Explicit_newlines_start_a_new_line_even_without_wrapping ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            const string twoLines = "first\nsecond";
            // One range per line.
            var format = FormatWith (new CharacterRange (0, 5), new CharacterRange (6, 6));

            var regions = g.MeasureCharacterRanges (twoLines, font, new RectangleF (0, 0, 0, 200), format);

            var top = regions[0].GetBounds ();
            var bottom = regions[1].GetBounds ();
            Assert.True (bottom.Top > top.Top,
                $"the range after the newline should sit lower ({bottom.Top}) than the one before it ({top.Top})");
        }

        [Fact]
        public void Empty_text_yields_one_empty_region_per_range ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            var format = FormatWith (new CharacterRange (0, 0), new CharacterRange (0, 0));

            var regions = g.MeasureCharacterRanges ("", font, new RectangleF (0, 0, 400, 200), format);

            Assert.Equal (2, regions.Length);
            Assert.All (regions, r => Assert.True (r.GetBounds ().IsEmpty));
        }

        [Fact]
        public void A_range_outside_the_text_produces_an_empty_region ()
        {
            using var g = NewGraphics ();
            using var font = new Font ("Arial", 12f);
            // Well past the end of the string — must not throw, and must measure nothing.
            var format = FormatWith (new CharacterRange (Text.Length + 10, 5));

            var regions = g.MeasureCharacterRanges (Text, font, new RectangleF (0, 0, 400, 200), format);

            Assert.Single (regions);
            Assert.True (regions[0].GetBounds ().IsEmpty);
        }
    }
}
