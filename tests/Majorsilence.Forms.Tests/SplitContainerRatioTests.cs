using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A split with no fixed panel keeps the share it was given, as upstream keeps _ratioWidth: a resize
    // recomputes the distance from that share. It used to scale the distance the previous resize left,
    // which compounded every clamp -- ReportDesigner's New Report SQL tab (a 203 of 612 split, on a
    // tab page laid out before it was shown) opened with Panel1 at 755 of 786.
    [Collection ("Headless")]
    public class SplitContainerRatioTests
    {
        // The designer's shape: BeginInit, size and distance from the resx, EndInit, in a tab page
        // that is not the selected one, on a form scaled from the classic 5x13 base size.
        private static (Form Form, SplitContainer Split, TabControl Tabs) Dialog ()
        {
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (620, 340),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var info = new TabPage ("Info");
            var sql = new TabPage ("SQL");
            var split = new SplitContainer ();

            ((ISupportInitialize) split).BeginInit ();
            split.Dock = DockStyle.Fill;
            split.Size = new Size (612, 300);
            split.SplitterDistance = 203;
            ((ISupportInitialize) split).EndInit ();

            sql.Controls.Add (split);
            tabs.TabPages.Add (info);
            tabs.TabPages.Add (sql);
            form.Controls.Add (tabs);
            form.AutoScaleBaseSize = new Size (5, 13);

            return (form, split, tabs);
        }

        [Fact]
        public void A_split_keeps_its_share_through_layout_and_scaling ()
        {
            var (form, split, tabs) = Dialog ();

            using (form) {
                form.Show ();
                tabs.SelectedIndex = 1;
                HeadlessRenderer.CapturePng (form, form.ClientSize.Width, form.ClientSize.Height);

                var share = (double) split.SplitterDistance / split.Width;
                Assert.InRange (share, 203.0 / 612 - 0.02, 203.0 / 612 + 0.02);
            }
        }

        [Fact]
        public void A_resize_after_a_clamp_restores_the_share ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (700, 300) };
            var split = new SplitContainer { Size = new Size (600, 200) };
            form.Controls.Add (split);
            form.Show ();
            split.SplitterDistance = 200;

            // Squeezed until Panel2's minimum clamps the split, then given its room back.
            split.Width = 120;
            form.PerformLayout ();
            split.Width = 600;
            form.PerformLayout ();

            Assert.Equal (200, split.SplitterDistance);
        }
    }
}
