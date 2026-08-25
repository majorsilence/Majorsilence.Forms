using System.Drawing;
using Majorsilence.Forms.Drawing;
using StringAlignment = Majorsilence.Forms.Drawing.StringAlignment;
using StringFormat = Majorsilence.Forms.Drawing.StringFormat;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GraphicsPath.AddString's rectangle overloads ignored the StringFormat and laid text out from the
    // rectangle's top-left corner. GDI+ aligns it inside the rectangle, and a library that renders all
    // its text as filled glyph outlines passes a centred format for every caption -- so card titles
    // rode above their divider and button labels hugged the top-left corner of the button.
    public class AddStringAlignmentTests
    {
        private const string Text = "Button";
        private static readonly RectangleF Box = new (0, 0, 400, 100);

        private static RectangleF InkBounds (StringAlignment? horizontal, StringAlignment? vertical)
        {
            using var path = new GraphicsPath ();
            using var family = new Majorsilence.Forms.Drawing.FontFamily ("Segoe UI");

            StringFormat? format = null;

            if (horizontal is not null || vertical is not null)
                format = new StringFormat {
                    Alignment = horizontal ?? StringAlignment.Near,
                    LineAlignment = vertical ?? StringAlignment.Near,
                };

            path.AddString (Text, family, (int)Majorsilence.Forms.Drawing.FontStyle.Regular, 16f, Box, format);
            format?.Dispose ();

            return path.GetBounds ();
        }

        [Fact]
        public void Near_alignment_stays_at_the_top_left ()
        {
            var bounds = InkBounds (StringAlignment.Near, StringAlignment.Near);

            Assert.True (bounds.Left < 4, $"expected the left edge, got {bounds.Left}");
            Assert.True (bounds.Top < 8, $"expected the top edge, got {bounds.Top}");
        }

        [Fact]
        public void A_null_format_behaves_as_Near_which_is_GDI_plus_default ()
        {
            var explicitNear = InkBounds (StringAlignment.Near, StringAlignment.Near);
            var implied = InkBounds (null, null);

            Assert.Equal (explicitNear.Left, implied.Left, 1);
            Assert.Equal (explicitNear.Top, implied.Top, 1);
        }

        [Fact]
        public void Center_alignment_centres_the_text_horizontally ()
        {
            var bounds = InkBounds (StringAlignment.Center, StringAlignment.Near);
            var centre = bounds.Left + (bounds.Width / 2);

            // Within a couple of pixels of the box's own centre; the old behaviour left it at the far
            // left, which is many times further out than this tolerance.
            Assert.True (System.Math.Abs (centre - (Box.Width / 2)) < 3,
                $"text centre {centre} is not near the box centre {Box.Width / 2}");
        }

        [Fact]
        public void Center_line_alignment_centres_the_text_vertically ()
        {
            var near = InkBounds (StringAlignment.Center, StringAlignment.Near);
            var centred = InkBounds (StringAlignment.Center, StringAlignment.Center);

            // This is the one that made captions ride against the top of their box.
            Assert.True (centred.Top > near.Top + 20,
                $"vertically centred text at {centred.Top} barely moved from the top at {near.Top}");
        }

        [Fact]
        public void Far_alignment_pushes_the_text_to_the_right_and_bottom ()
        {
            var near = InkBounds (StringAlignment.Near, StringAlignment.Near);
            var far = InkBounds (StringAlignment.Far, StringAlignment.Far);

            Assert.True (far.Right > near.Right + 20, "Far alignment did not move the text right");
            Assert.True (far.Top > near.Top + 40, "Far line alignment did not move the text down");
            Assert.True (far.Right <= Box.Right + 2, "Far alignment overshot the box");
        }

        [Fact]
        public void Alignment_measures_with_the_fallback_faces_actually_used ()
        {
            // Centring has to be measured run by run with the same substituted faces AddString draws
            // with, or mixed-script text is centred against the wrong width.
            using var path = new GraphicsPath ();
            using var family = new Majorsilence.Forms.Drawing.FontFamily ("Segoe UI");
            using var format = new StringFormat { Alignment = StringAlignment.Center };

            path.AddString ("Chart 图表控件", family,
                (int)Majorsilence.Forms.Drawing.FontStyle.Regular, 16f, Box, format);

            var bounds = path.GetBounds ();
            var centre = bounds.Left + (bounds.Width / 2);

            Assert.True (System.Math.Abs (centre - (Box.Width / 2)) < 6,
                $"mixed-script text centre {centre} is off from {Box.Width / 2}");
        }
    }
}
