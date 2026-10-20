using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class LabelAnchorAutoSizeTests
    {
        // Upstream sizes an AutoSize label only once it has a parent (CommonProperties.ShouldSelfSize), so a designer
        // label keeps its designer size until it is added and is anchored with the designer's gaps. Here it was sized at
        // its text while still unparented, so an AutoSize label anchored right was added with a wider right-hand gap
        // and then, when the form's font arrived, grew leftwards from it.
        // A form's controls sit in its client area, an implicit child of the window's root, so the font cascade over
        // the root's public children stopped there: Form.Font = 12pt told no control on the form that its font changed.
        [Fact]
        public void A_forms_font_change_reaches_its_controls ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var label = new Label { Text = "x" };
            form.Controls.Add (label);
            var fonts = 0;
            label.FontChanged += (s, e) => fonts++;

            form.Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);

            Assert.Equal (1, fonts);
            Assert.Equal (12f, label.Font.SizeInPoints);
        }

        [Fact]
        public void A_right_anchored_auto_size_label_keeps_its_designer_left_edge_as_it_grows ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var label = new Label ();

            // InitializeComponent's order.
            form.SuspendLayout ();
            label.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label.AutoSize = true;
            label.Location = new Point (625, 40);
            label.Size = new Size (381, 60);
            label.Text = "Notice: Removing a posting can take several minutes\r\nand can cause performance issues for multiple users\r\nwhile the posting is being removed.";
            form.ClientSize = new Size (1056, 528);
            form.Controls.Add (label);
            form.Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);
            form.ResumeLayout (false);
            form.PerformLayout ();
            form.Show ();

            // The label sized itself, as upstream's does, so it kept the designer's left edge and grew to the right
            // (upstream's notice sits where the designer put it). It was moved left by the anchor layout, which sized it
            // from the gap it had at the old font: x = 507. The right-hand gap is not asserted: it depends on how wide
            // the platform's stand-in for Microsoft Sans Serif sets the text.
            Assert.Equal (625, label.Left);
            Assert.True (label.Width > 263, $"the label grew to the form's font ({label.Width})");
        }
    }
}
