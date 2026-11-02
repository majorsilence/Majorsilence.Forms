using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // An AutoSize label with no text is no wider than its padding and border, as upstream's (its GetPreferredSizeCore
    // measures an empty label as the extent of "0" with the width zeroed). Measured (.NET 10, Windows 11): a label
    // holding "12-31-2025" (90x24) emptied before or after its form showed, or set to null, was 0x23. Here it kept
    // the size it had, so a label whose text was cleared at run time went on covering whatever it sat on -- a
    // date label over a money field hid the field's right half.
    public class LabelEmptyAutoSizeTests
    {
        private static readonly Majorsilence.Forms.Drawing.Font Font = new ("Microsoft Sans Serif", 12f);

        private static (Form form, Label label) Shown (string? text)
        {
            var form = new Form { ClientSize = new Size (400, 200) };
            var label = new Label { AutoSize = true, Font = Font, Text = text, Location = new Point (10, 10) };
            form.Controls.Add (label);
            form.Show ();
            return (form, label);
        }

        [Fact]
        public void A_label_emptied_after_its_form_shows_is_no_wider_than_its_padding ()
        {
            var (form, label) = Shown ("12-31-2025");
            using (form) {
                Assert.True (label.Width > 0);

                label.Text = "";

                Assert.Equal (0, label.Width);
                Assert.True (label.Height > 0);
            }
        }

        [Fact]
        public void A_label_emptied_before_its_form_shows_is_no_wider_than_its_padding ()
        {
            using var form = new Form { ClientSize = new Size (400, 200) };
            var label = new Label { AutoSize = true, Font = Font, Text = "12-31-2025", Location = new Point (10, 10) };
            form.Controls.Add (label);

            label.Text = "";
            form.Show ();

            Assert.Equal (0, label.Width);
        }

        [Fact]
        public void A_label_set_to_null_is_no_wider_than_its_padding ()
        {
            var (form, label) = Shown ("12-31-2025");
            using (form) {
                label.Text = null;

                Assert.Equal (0, label.Width);
            }
        }

        [Fact]
        public void An_empty_label_is_one_line_of_its_font_tall_and_its_padding_wide ()
        {
            var (form, label) = Shown ("");
            using (form) {
                label.Padding = new Padding (2, 3, 4, 5);

                Assert.Equal (6, label.Width);
                Assert.True (label.Height > 8, $"{label.Height} should be a line of text tall plus the padding");
                Assert.Equal (label.PreferredSize, label.Size);
            }
        }

        [Fact]
        public void A_label_given_text_again_sizes_to_it ()
        {
            var (form, label) = Shown ("12-31-2025");
            using (form) {
                var sized = label.Size;
                label.Text = "";

                label.Text = "12-31-2025";

                Assert.Equal (sized, label.Size);
            }
        }
    }
}
