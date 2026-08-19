using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Control.GetPreferredSizeCore reports the bounds that were explicitly SET, so a text-entry control
    // whose height had been laid out to zero went on ASKING for zero -- a feedback loop, not a one-off bad
    // measurement. Krypton's KryptonTextBox and KryptonNumericUpDown host one of these and take their own
    // height from it, so the pair settled at 2px and every unsized text box in a ported form rendered as a
    // stray horizontal line next to its label. A text-entry control's height comes from its font; zero is
    // never a legitimate answer for one.
    public class PreferredHeightSizingTests
    {
        [Fact]
        public void A_single_line_TextBox_laid_out_to_zero_height_still_prefers_a_line_of_text ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox { Size = new Size (279, 0) };

            Assert.Equal (box.PreferredHeight, box.PreferredSize.Height);
        }

        [Fact]
        public void A_NumericUpDown_laid_out_to_zero_height_still_prefers_a_line_of_text ()
        {
            HeadlessRenderer.Use ();

            using var spinner = new NumericUpDown { Size = new Size (65, 0) };

            Assert.Equal (spinner.PreferredHeight, spinner.PreferredSize.Height);
        }

        [Fact]
        public void An_explicitly_sized_TextBox_keeps_the_height_it_was_given ()
        {
            HeadlessRenderer.Use ();

            // Only a height of zero is filled in, so a designer that sized the control still wins. That is
            // what keeps this fix from quietly resizing layouts that were already correct.
            using var box = new TextBox { Size = new Size (120, 40) };

            Assert.Equal (40, box.PreferredSize.Height);
        }

        [Fact]
        public void A_multiline_TextBox_is_left_to_its_container ()
        {
            HeadlessRenderer.Use ();

            // WinForms' AutoSize governs a text box's height alone, and only for a single-line one; a
            // multiline box is sized by whatever contains it, so zero stays zero here.
            using var box = new TextBox { Multiline = true, Size = new Size (279, 0) };

            Assert.Equal (0, box.PreferredSize.Height);
        }
    }
}
