using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's message box is as big as its message. This one was a fixed 400x200 (stepped up by
    // newline count), so a one-line error sat in empty space and a long unbroken line ran off the edge.
    [Collection ("Headless")]
    public class MessageBoxSizeTests
    {
        private static Label MessageLabel (MessageBoxForm box) => box.Controls.GetAllControls ().OfType<Label> ().First ();

        // The buttons live in their own band (a panel), which GetAllControls does not descend into.
        private static System.Collections.Generic.List<Button> Buttons (MessageBoxForm box) =>
            box.Controls.OfType<Panel> ().Single ().Controls.OfType<Button> ().ToList ();

        [Fact]
        public void A_short_message_gets_a_small_dialog ()
        {
            HeadlessRenderer.Use ();
            using var box = new MessageBoxForm ("Title", "Done.", MessageBoxButtons.OK);
            box.Show ();

            Assert.True (box.ClientSize.Width < 400, $"width {box.ClientSize.Width}");
            Assert.True (box.ClientSize.Height < 155, $"height {box.ClientSize.Height}");
        }

        [Fact]
        public void A_long_line_wraps_and_the_dialog_grows_to_hold_it ()
        {
            HeadlessRenderer.Use ();
            var message = string.Join (" ", Enumerable.Repeat ("The configuration file could not be read.", 30));
            using var box = new MessageBoxForm ("Title", message, MessageBoxButtons.OK);
            box.Show ();

            var label = MessageLabel (box);
            var needed = label.GetPreferredSize (new Size (label.Width - label.Padding.Horizontal, 0));

            Assert.True (box.ClientSize.Width <= 600, $"width {box.ClientSize.Width}: the line should wrap");
            Assert.True (label.Height >= needed.Height, $"the label is {label.Height}px but its wrapped text needs {needed.Height}");
        }

        // Upstream's message box puts its buttons at the right of a band of their own, under the message
        // on a window-coloured area, with the text centred against the glyph. This one centred the
        // buttons, painted everything control-coloured and hung the text from the top.
        [Fact]
        public void The_layout_follows_the_system_message_box ()
        {
            HeadlessRenderer.Use ();
            using var box = new MessageBoxForm ("Title", "Done.", MessageBoxButtons.OKCancel, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, 0);
            box.Show ();

            var buttons = Buttons (box);
            var band = buttons[0].Parent!;
            Assert.Equal (box.ClientSize.Width, band.Width);
            Assert.Equal (band.Width - 15, buttons[1].Right);
            Assert.True (buttons[0].Right < buttons[1].Left, "OK comes before Cancel");
            Assert.Equal (new Size (75, 23), buttons[0].Size);
            Assert.Equal (42, band.Height);

            var label = MessageLabel (box);
            Assert.Equal (ContentAlignment.MiddleLeft, label.TextAlign);
            Assert.Equal (Theme.ControlLowColor, label.Style.BackgroundColor);
            Assert.Equal (32 + 2 * 23, label.Height);
        }

        [Fact]
        public void The_buttons_always_fit ()
        {
            HeadlessRenderer.Use ();
            using var box = new MessageBoxForm ("Title", "?", MessageBoxButtons.YesNoCancel);
            box.Show ();

            var buttons = Buttons (box);
            Assert.Equal (3, buttons.Count);
            Assert.All (buttons, b => Assert.True (b.Left >= 0 && b.Right <= box.ClientSize.Width, $"{b.Text} at {b.Bounds}"));
        }
    }
}
