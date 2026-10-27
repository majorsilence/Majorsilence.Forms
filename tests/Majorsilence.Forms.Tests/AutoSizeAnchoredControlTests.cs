using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream lays an AutoSize control out at its preferred size whatever its anchors say: anchored to all four
    // sides, it moves with its parent but keeps its size. Measured on WinForms (.NET 10): a 12pt label, AutoSize,
    // anchored all round at (0, 3) in a 635 x 72 user control, stays 145 x 24 (its preferred size) when the
    // control is placed at 654 x 61, shrunk to 30 high or grown to 120. A label with AutoSize off beside it
    // stretches with the control: 9, then 0, then 68 high.
    [Collection ("Headless")]
    public class AutoSizeAnchoredControlTests
    {
        [Fact]
        public void An_AutoSize_label_anchored_all_round_keeps_its_preferred_size ()
        {
            HeadlessRenderer.Use ();
            var host = new UserControl ();
            host.SuspendLayout ();
            var auto = new Label {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoSize = true,
                Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f), Location = new Point (0, 3), Size = new Size (143, 20),
                Text = "Location/ Area/ Bin",
            };
            var fixedSize = new Label {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoSize = false,
                Location = new Point (200, 3), Size = new Size (143, 20), Text = "fixed",
            };
            host.Controls.Add (auto);
            host.Controls.Add (fixedSize);
            host.Size = new Size (635, 72);
            host.ResumeLayout (false);

            using var form = new Form { ClientSize = new Size (800, 300) };
            host.Location = new Point (2, 2);
            host.Size = new Size (654, 61);
            form.Controls.Add (host);
            form.Show ();

            var preferred = auto.PreferredSize;

            Assert.Equal (new Rectangle (new Point (0, 3), preferred), auto.Bounds);
            Assert.Equal (9, fixedSize.Height);

            host.Height = 30;
            Assert.Equal (new Rectangle (new Point (0, 3), preferred), auto.Bounds);
            Assert.Equal (0, fixedSize.Height);

            host.Height = 120;
            Assert.Equal (new Rectangle (new Point (0, 3), preferred), auto.Bounds);
            Assert.Equal (68, fixedSize.Height);
        }
    }
}
