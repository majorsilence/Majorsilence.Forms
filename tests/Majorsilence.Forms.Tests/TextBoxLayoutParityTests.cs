using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Parity for the three things a designer expects of a plain single-line TextBox: no border when
    // it asked for none, text aligned where it said, and text sitting on the control's centre line.
    // All three were stored-but-ignored properties, so a designer layout looked nothing like it did
    // in WinForms while every property still reported the right value.
    public class TextBoxLayoutParityTests
    {
        // Renders a form and returns the TextBox's own back buffer.
        private static SkiaSharp.SKBitmap Render (TextBox textBox)
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (320, 160) };
            form.Controls.Add (textBox);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SkiaSharp.SKBitmap) buffer.Invoke (textBox, null)!;
        }

        // Bounding box of pixels brighter than the (dark) background, or null when nothing was drawn.
        private static (int Left, int Top, int Right, int Bottom)? InkBounds (SkiaSharp.SKBitmap bitmap)
        {
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha == 0 || c.Red < 150 || c.Green < 150 || c.Blue < 150)
                        continue;

                    left = System.Math.Min (left, x);
                    top = System.Math.Min (top, y);
                    right = System.Math.Max (right, x);
                    bottom = System.Math.Max (bottom, y);
                }

            return right < 0 ? null : (left, top, right, bottom);
        }

        private static TextBox Borderless (HorizontalAlignment align) => new TextBox {
            Left = 10, Top = 20, Width = 240, Height = 60,
            BackColor = System.Drawing.Color.Black,
            ForeColor = System.Drawing.Color.White,
            BorderStyle = BorderStyle.None,
            Text = "0",
            TextAlign = align,
        };

        // The type's default style asks for a 1px frame, and BorderStyle only ever set a field, so a
        // borderless box kept its frame -- and kept the pixels of client area the frame occupies.
        [Fact]
        public void BorderStyle_None_removes_the_border_and_returns_its_client_area ()
        {
            using var bordered = new TextBox { Width = 240, Height = 60, BorderStyle = BorderStyle.Fixed3D };
            using var borderless = new TextBox { Width = 240, Height = 60, BorderStyle = BorderStyle.None };

            Assert.Equal (240, borderless.ClientRectangle.Width);
            Assert.Equal (60, borderless.ClientRectangle.Height);
            Assert.True (bordered.ClientRectangle.Width < borderless.ClientRectangle.Width,
                "a bordered box must give up client width to its frame");
        }

        [Fact]
        public void BorderStyle_None_paints_no_frame ()
        {
            using var textBox = Borderless (HorizontalAlignment.Left);
            var ink = InkBounds (Render (textBox));

            Assert.NotNull (ink);

            // Only the glyph should be lit. A frame would reach both far edges of the buffer.
            Assert.True (ink!.Value.Right < textBox.Width - 4,
                $"no border expected, but ink reaches the right edge (right={ink.Value.Right}, width={textBox.Width})");
        }

        // Right alignment laid the line out inside an unbounded width, which put every glyph past the
        // far end of the surface: the control painted completely blank.
        [Theory]
        [InlineData (HorizontalAlignment.Left)]
        [InlineData (HorizontalAlignment.Center)]
        [InlineData (HorizontalAlignment.Right)]
        public void Text_is_painted_for_every_alignment (HorizontalAlignment align)
        {
            using var textBox = Borderless (align);
            Assert.NotNull (InkBounds (Render (textBox)));
        }

        [Fact]
        public void TextAlign_Right_puts_the_text_against_the_right_edge ()
        {
            using var left = Borderless (HorizontalAlignment.Left);
            using var right = Borderless (HorizontalAlignment.Right);

            var leftInk = InkBounds (Render (left))!.Value;
            var rightInk = InkBounds (Render (right))!.Value;

            Assert.True (rightInk.Left > leftInk.Left,
                $"right-aligned text should start further right (left-aligned at {leftInk.Left}, right-aligned at {rightInk.Left})");
            Assert.True (right.Width - rightInk.Right < leftInk.Left + 4,
                "right-aligned text should sit near the right edge");
        }

        // A Win32 single-line EDIT centres its text vertically. Top-aligning it instead only shows on
        // a box taller than its font -- and there it tucks the glyphs under whatever shares the top
        // edge, which is how a calculator display ended up showing the bottom third of its digit.
        [Fact]
        public void Single_line_text_is_centred_vertically ()
        {
            using var textBox = Borderless (HorizontalAlignment.Left);
            var bmp = Render (textBox);
            var ink = InkBounds (bmp)!.Value;

            // The ink is measured in the back buffer's device pixels; the control's Height is logical.
            // Compare in bitmap space so this holds on a scaled display too.
            var height = bmp.Height > 0 ? bmp.Height : textBox.Height;

            var above = ink.Top;
            var below = height - 1 - ink.Bottom;

            Assert.True (above > 4, $"text should not hug the top edge (gap above: {above})");
            Assert.True (System.Math.Abs (above - below) <= 4,
                $"single-line text should be vertically centred (gap above: {above}, below: {below})");
        }

        [Fact]
        public void Multiline_text_still_starts_at_the_top ()
        {
            using var textBox = Borderless (HorizontalAlignment.Left);
            textBox.Multiline = true;

            var ink = InkBounds (Render (textBox))!.Value;

            Assert.True (ink.Top < textBox.Height / 4,
                $"multiline text starts at the top in WinForms (top ink at {ink.Top})");
        }
    }
}
