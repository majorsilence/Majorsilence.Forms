using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Anchored controls on a tab page that is not shown follow their parents as the form is resized, as upstream's do.
    // Measured on WinForms (.NET 10), the designer sequence below -- a 1309 x 701 form with a tab control anchored all
    // round, a nested tab control on its second (unselected) page and a group anchored Bottom|Left|Right on the nested
    // page -- resized to 1226 x 808 in Load, then the second page selected: outer 1210 x 703, page 1202 x 670, inner
    // 1184 x 664, nested page 1176 x 631, the group at y = 470 (6px above the page's bottom, as designed).
    [Collection ("Headless")]
    public class AnchoredOnHiddenTabPageTests
    {
        [Theory]
        [InlineData (false, false)]
        [InlineData (true, false)]
        [InlineData (false, true)]
        [InlineData (true, true)]
        public void Anchored_controls_on_an_unselected_page_follow_a_resize (bool startDisabled, bool upstreamLook)
        {
            HeadlessRenderer.Use ();
            if (upstreamLook)
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            try {
            using var f = new Form ();
            var outer = new TabControl (); var info = new TabPage ("Information"); var taxPage = new TabPage ("Tax Information");
            var inner = new TabControl (); var assess = new TabPage ("Tax Assessment Values");
            var group = new GroupBox { Text = "School Declaration" };
            var grid = new Panel ();
            f.SuspendLayout (); outer.SuspendLayout (); taxPage.SuspendLayout (); inner.SuspendLayout (); assess.SuspendLayout ();
            outer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            outer.Controls.Add (info); outer.Controls.Add (taxPage);
            outer.Location = new Point (8, 67); outer.Size = new Size (1293, 596);
            taxPage.Controls.Add (inner);
            taxPage.Location = new Point (4, 29); taxPage.Size = new Size (1264, 589);
            inner.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            inner.Controls.Add (assess);
            inner.Location = new Point (3, 3); inner.Size = new Size (1246, 583);
            assess.Controls.Add (grid); assess.Controls.Add (group);
            assess.Location = new Point (4, 29); assess.Size = new Size (1238, 550);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.Location = new Point (12, 40); grid.Size = new Size (1100, 340);
            group.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            group.Location = new Point (12, 389); group.Size = new Size (1214, 155);
            f.ClientSize = new Size (1309, 701);
            f.Controls.Add (outer);
            if (startDisabled)
                outer.Enabled = false;
            assess.ResumeLayout (false); inner.ResumeLayout (false); taxPage.ResumeLayout (false); outer.ResumeLayout (false); f.ResumeLayout (false);
            f.Load += (_, _) => f.ClientSize = new Size (1226, 808);
            f.Show ();
            outer.Enabled = true;
            outer.SelectedIndex = 1;

            // Each anchored control keeps the margins it was designed with, against the page's final size: the grid 12
            // from the left, 126 from the right and 170 from the bottom of the 1238 x 550 page, the group 6 from its bottom
            // and 12 from either side -- as WinForms, where the page ends 1176 x 631 and the group at y = 470.
            Assert.Equal (assess.Width - 12 - 126, grid.Width);
            Assert.Equal (assess.Height - 40 - 170, grid.Height);
            Assert.Equal (assess.Height - 6 - 155, group.Top);
            Assert.Equal (assess.Width - 12 - 12, group.Width);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
