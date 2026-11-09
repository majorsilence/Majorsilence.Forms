using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's ToolStripStatusLabel has six constructors: (), (text), (image), (text, image), (text, image, onClick) and
    // (text, image, onClick, name). MF had only the implicit one, so `New ToolStripStatusLabel("Ready")` did not compile.
    public class ToolStripStatusLabelConstructorTests
    {
        [Fact]
        public void A_status_label_can_be_built_from_its_text ()
        {
            var label = new ToolStripStatusLabel ("Ready");

            Assert.Equal ("Ready", label.Text);
            Assert.Null (label.Image);
        }

        [Fact]
        public void A_status_label_can_be_built_from_its_image ()
        {
            var image = new Majorsilence.Forms.Drawing.Bitmap (16, 16);
            var label = new ToolStripStatusLabel (image);

            Assert.Same (image, label.Image);
            Assert.Equal (string.Empty, label.Text);
        }

        [Fact]
        public void A_status_label_can_be_built_from_its_text_image_click_handler_and_name ()
        {
            var image = new Majorsilence.Forms.Drawing.Bitmap (16, 16);
            var clicks = 0;
            var label = new ToolStripStatusLabel ("Ready", image, (_, _) => clicks++, "statusReady");

            Assert.Equal ("Ready", label.Text);
            Assert.Same (image, label.Image);
            Assert.Equal ("statusReady", label.Name);

            label.PerformClick ();
            Assert.Equal (1, clicks);
        }

        [Fact]
        public void The_shorter_constructors_leave_the_rest_unset ()
        {
            var image = new Majorsilence.Forms.Drawing.Bitmap (16, 16);
            var with_image = new ToolStripStatusLabel ("Ready", image);
            var with_click = new ToolStripStatusLabel ("Ready", null, null);

            Assert.Equal (("Ready", image), (with_image.Text, with_image.Image));
            Assert.Equal (string.Empty, with_image.Name);
            Assert.Null (with_click.Image);
            Assert.Equal (string.Empty, new ToolStripStatusLabel ((string?) null).Text);
        }
    }
}
