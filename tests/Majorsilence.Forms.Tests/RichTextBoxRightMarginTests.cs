using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 — RichTextBox.RightMargin, the one member of the paragraph-model cluster (TXT-31) that is
    // separable from it.
    //
    // The other seven need per-paragraph layout, and RichTextBox shares TextBox's pipeline, which
    // builds ONE TextBlock for the whole document with a single alignment and wrap width. Per-paragraph
    // alignment and indents mean one block per paragraph, which moves caret positioning, hit-testing
    // and scrolling -- a text-subsystem change, not a sweep entry. RightMargin is different: it is the
    // wrap width itself, which that one block already has.
    [Collection ("Headless")]
    public class RichTextBoxRightMarginTests
    {
        private static RichTextBox Shown (out Form form)
        {
            HeadlessRenderer.Use ();

            var box = new RichTextBox { Width = 400, Height = 120, Multiline = true, WordWrap = true };
            box.Text = string.Join (" ", System.Linq.Enumerable.Repeat ("wrap", 60));

            form = new Form { Width = 500, Height = 220 };
            form.Controls.Add (box);
            form.Show ();
            PaintSurface.Render (box).Dispose ();

            return box;
        }

        [Fact]
        public void A_right_margin_narrows_the_wrap_width ()
        {
            using var box = Shown (out var form);

            try {
                var wide = box.WrapWidth;

                box.RightMargin = 120;

                Assert.Equal (120, box.WrapWidth);
                Assert.True (box.WrapWidth < wide, $"not narrower: {wide} -> {box.WrapWidth}");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Narrower_wrapping_makes_the_text_taller ()
        {
            // What the property is for, observed through the content rather than the field: wrapping
            // the same text into a narrower column takes more lines.
            using var box = Shown (out var form);

            try {
                // The laid-out text block's height: wrapping the same text into a narrower column
                // takes more lines, which is the observable consequence rather than the field.
                var before = box.document.GetTextBlock ().MeasuredHeight;

                box.RightMargin = 100;
                PaintSurface.Render (box).Dispose ();

                var after = box.document.GetTextBlock ().MeasuredHeight;

                Assert.True (after > before, $"the text did not re-wrap: {before} -> {after}");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Zero_means_wrap_to_the_control ()
        {
            // GUARD, and upstream's meaning of zero: it is the default, so every RichTextBox in
            // existence must keep wrapping to its own width.
            using var box = Shown (out var form);

            try {
                Assert.Equal (0, box.RightMargin);
                Assert.Equal (box.PaddedClientRectangle.Width, box.WrapWidth);

                box.RightMargin = 100;
                box.RightMargin = 0;

                Assert.Equal (box.PaddedClientRectangle.Width, box.WrapWidth);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_negative_margin_is_rejected ()
        {
            // Upstream throws; this stored it silently.
            using var box = new RichTextBox ();

            Assert.Throws<System.ArgumentOutOfRangeException> (() => box.RightMargin = -1);
        }
    }
}
