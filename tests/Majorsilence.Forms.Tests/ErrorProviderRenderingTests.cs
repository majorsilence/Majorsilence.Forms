using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.20d (finding SMP-51, P0): ErrorProvider rendered nothing at all. SetError wrote to a
    // dictionary that only GetError read, so the canonical WinForms validation affordance --
    // errorProvider1.SetError (txtName, "Required") in a Validating handler -- gave the user no
    // feedback of any kind, and a form refused to submit with nothing on screen explaining why.
    //
    // These tests assert the icon is PAINTED, in the right place, because "the state is right and
    // nothing is drawn" is exactly the defect.
    [Collection ("Headless")]
    public class ErrorProviderRenderingTests
    {
        // Rendered with RenderOnForm throughout, not Render: Control.Visible is ambient, so in a
        // detached tree every child reports false and the paint loop skips it -- which makes every
        // pixel assertion here pass by drawing nothing. RenderOnForm parents the panel to a form
        // first. The scale is passed explicitly for the other half of the same trap: an unhosted
        // control reports Scaling 0 and the helper then builds a 0x0 bitmap.

        // A parent with one child, sized so there is room for an icon on either side of the child.
        private static (Panel parent, TextBox child) Hosted (int left = 40)
        {
            HeadlessRenderer.Use ();
            var parent = new Panel { Width = 240, Height = 80 };
            var child = new TextBox { Left = left, Top = 20, Width = 100, Height = 24 };
            parent.Controls.Add (child);

            return (parent, child);
        }

        // Pixels of the icon's own colour inside a rectangle. Counting "not the background" would also
        // count the child control's own chrome, which is not what this is measuring.
        private static int IconPixels (SKBitmap bitmap, Rectangle area)
        {
            var colour = ErrorProvider.ErrorIconColor;
            var count = 0;

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue)
                        count++;
                }

            return count;
        }

        // The strip immediately to the right of the child, where a default-aligned icon belongs.
        private static Rectangle RightOf (Control child)
            => new Rectangle (child.Right, child.Top, ErrorProvider.IconSize + 8, child.Height);

        private static Rectangle LeftOf (Control child)
            => new Rectangle (child.Left - ErrorProvider.IconSize - 8, child.Top, ErrorProvider.IconSize + 8, child.Height);

        [Fact]
        public void Setting_an_error_paints_an_icon_beside_the_control ()
        {
            // The finding's own test.
            var (parent, child) = Hosted ();
            using var _parent = parent;
            using var provider = new ErrorProvider ();

            using (var before = PaintSurface.RenderOnForm (parent, 1f)) {
                Assert.Equal (parent.Width, before.Width);          // a 0x0 surface would pass everything
                Assert.Equal (0, IconPixels (before, RightOf (child)));
            }

            provider.SetError (child, "Required");

            using var after = PaintSurface.RenderOnForm (parent, 1f);

            Assert.True (IconPixels (after, RightOf (child)) > 0, "no error icon was painted");
        }

        [Fact]
        public void Clearing_the_error_removes_the_icon ()
        {
            var (parent, child) = Hosted ();
            using var _parent = parent;
            using var provider = new ErrorProvider ();
            provider.SetError (child, "Required");

            // An empty description is upstream's way of clearing one.
            provider.SetError (child, string.Empty);

            using var bitmap = PaintSurface.RenderOnForm (parent, 1f);

            Assert.Equal (0, IconPixels (bitmap, RightOf (child)));
        }

        [Fact]
        public void Clear_removes_every_icon ()
        {
            HeadlessRenderer.Use ();
            using var parent = new Panel { Width = 240, Height = 120 };
            var first = new TextBox { Left = 40, Top = 10, Width = 100, Height = 24 };
            var second = new TextBox { Left = 40, Top = 60, Width = 100, Height = 24 };
            parent.Controls.Add (first);
            parent.Controls.Add (second);
            using var provider = new ErrorProvider ();
            provider.SetError (first, "Required");
            provider.SetError (second, "Also required");

            using (var both = PaintSurface.RenderOnForm (parent, 1f)) {
                Assert.True (IconPixels (both, RightOf (first)) > 0);
                Assert.True (IconPixels (both, RightOf (second)) > 0);
            }

            provider.Clear ();

            using var cleared = PaintSurface.RenderOnForm (parent, 1f);

            Assert.Equal (0, IconPixels (cleared, RightOf (first)));
            Assert.Equal (0, IconPixels (cleared, RightOf (second)));
        }

        [Fact]
        public void The_icon_goes_on_the_side_the_alignment_asks_for ()
        {
            var (parent, child) = Hosted (left: 60);
            using var _parent = parent;
            using var provider = new ErrorProvider ();
            provider.SetError (child, "Required");
            provider.SetIconAlignment (child, ErrorIconAlignment.MiddleLeft);

            using var bitmap = PaintSurface.RenderOnForm (parent, 1f);

            Assert.True (IconPixels (bitmap, LeftOf (child)) > 0, "the icon should be left of the control");
            Assert.Equal (0, IconPixels (bitmap, RightOf (child)));
        }

        [Fact]
        public void IconPadding_moves_the_icon_away_from_the_control ()
        {
            // Asserted as a relationship: the padded icon starts further right than the unpadded one,
            // rather than at a particular pixel.
            var (parent, child) = Hosted ();
            using var _parent = parent;
            using var provider = new ErrorProvider ();
            provider.SetError (child, "Required");

            var unpadded = FirstIconColumn (parent, child);

            provider.SetIconPadding (child, 12);

            var padded = FirstIconColumn (parent, child);

            Assert.True (padded > unpadded,
                $"padded icon starts at {padded}, unpadded at {unpadded}");
            Assert.Equal (12, padded - unpadded);
        }

        // The leftmost column right of the child that carries icon pixels.
        private static int FirstIconColumn (Panel parent, Control child)
        {
            using var bitmap = PaintSurface.RenderOnForm (parent, 1f);

            for (var x = child.Right; x < bitmap.Width; x++)
                if (IconPixels (bitmap, new Rectangle (x, child.Top, 1, child.Height)) > 0)
                    return x;

            throw new Xunit.Sdk.XunitException ("no icon pixels found right of the control");
        }

        [Fact]
        public void A_hidden_control_shows_no_icon ()
        {
            // An icon floating beside an invisible control is worse than none: it points at nothing.
            var (parent, child) = Hosted ();
            using var _parent = parent;
            using var provider = new ErrorProvider ();
            provider.SetError (child, "Required");

            child.Visible = false;

            using var bitmap = PaintSurface.RenderOnForm (parent, 1f);

            Assert.Equal (0, IconPixels (bitmap, RightOf (child)));
        }

        [Fact]
        public void The_icon_is_drawn_over_the_container_contents_not_under_them ()
        {
            // Why this needs the adorner layer rather than the public Paint event: Paint fires before
            // PaintChildren, so an icon drawn from it lands under any control occupying that space.
            // A sibling is parked exactly where the icon goes.
            var (parent, child) = Hosted ();
            using var _parent = parent;
            var overlapping = new Panel { Left = child.Right, Top = child.Top, Width = 40, Height = child.Height };
            parent.Controls.Add (overlapping);
            using var provider = new ErrorProvider ();
            provider.SetError (child, "Required");

            using var bitmap = PaintSurface.RenderOnForm (parent, 1f);

            Assert.True (IconPixels (bitmap, RightOf (child)) > 0,
                "the sibling painted over the icon, so it is not on the adorner layer");
        }

        [Fact]
        public void GetError_still_reports_what_was_set ()
        {
            // GUARD, not proof: the dictionary half always worked -- it was the only half that did.
            // It pins that adding the rendering did not disturb the programmatic surface.
            var (parent, child) = Hosted ();
            using var _parent = parent;
            using var provider = new ErrorProvider ();

            provider.SetError (child, "Required");
            Assert.Equal ("Required", provider.GetError (child));

            provider.SetError (child, string.Empty);
            Assert.Equal (string.Empty, provider.GetError (child));
        }
    }
}
