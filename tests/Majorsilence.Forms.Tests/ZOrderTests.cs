using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: within a Controls collection, INDEX 0 IS TOPMOST. A designer's
    // InitializeComponent adds the intended-topmost control first, BringToFront moves a control to
    // index 0, SendToBack to the end, and dock layout processes Count-1..0 so index 0 docks last.
    // Painting ascending collection order instead put later-added siblings on top, visually
    // inverting every overlap (found: a UserControl added after an overlapping sibling label
    // painted over the label's first characters).
    public class ZOrderTests
    {
        // Visible requires a shown ancestor window (Control.Visible walks the parent chain), so
        // hit-testing tests host the panel in a shown Form and close it before returning to avoid
        // polluting Application.OpenForms for other tests.
        private static (Form form, Panel panel) CreateShownPanel ()
        {
            var form = new Form ();
            form.Show ();
            var panel = new Panel { Left = 0, Top = 0, Width = 200, Height = 100 };
            form.Controls.Add (panel);
            return (form, panel);
        }

        [Fact]
        public void First_added_child_is_topmost_for_hit_testing ()
        {
            var (form, panel) = CreateShownPanel ();
            try {
                var top = new Panel { Left = 0, Top = 0, Width = 100, Height = 100 };
                var bottom = new Panel { Left = 50, Top = 0, Width = 100, Height = 100 };
                panel.Controls.Add (top);
                panel.Controls.Add (bottom);

                // In the overlap zone (x=50..100) the FIRST added (index 0) child wins.
                Assert.Same (top, panel.GetChildAtPoint (new Point (75, 50)));
                // Outside the topmost child, the sibling is found.
                Assert.Same (bottom, panel.GetChildAtPoint (new Point (125, 50)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void BringToFront_makes_control_topmost ()
        {
            var (form, panel) = CreateShownPanel ();
            try {
                var a = new Panel { Left = 0, Top = 0, Width = 100, Height = 100 };
                var b = new Panel { Left = 50, Top = 0, Width = 100, Height = 100 };
                panel.Controls.Add (a);
                panel.Controls.Add (b);

                b.BringToFront ();

                Assert.Equal (0, panel.Controls.GetChildIndex (b));
                Assert.Same (b, panel.GetChildAtPoint (new Point (75, 50)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void SendToBack_makes_control_bottommost ()
        {
            var (form, panel) = CreateShownPanel ();
            try {
                var a = new Panel { Left = 0, Top = 0, Width = 100, Height = 100 };
                var b = new Panel { Left = 50, Top = 0, Width = 100, Height = 100 };
                panel.Controls.Add (a);
                panel.Controls.Add (b);

                a.SendToBack ();

                Assert.Equal (panel.Controls.Count - 1, panel.Controls.GetChildIndex (a));
                Assert.Same (b, panel.GetChildAtPoint (new Point (75, 50)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Paint_order_is_reverse_of_collection_order ()
        {
            using var panel = new Panel { Width = 200, Height = 100 };
            var top = new Panel ();
            var bottom = new Panel ();
            panel.Controls.Add (top);
            panel.Controls.Add (bottom);

            // Explicit children paint in reverse collection order: bottom (index 1) first, top
            // (index 0) last, so index 0 ends up visually on top.
            var explicitOrder = panel.Controls.GetControlsPaintOrder ()
                .Where (c => c == top || c == bottom).ToList ();
            Assert.Equal (new Control[] { bottom, top }, explicitOrder);

            // Implicit chrome (scrollbars, size grip) paints after every explicit child so it
            // stays on top of content.
            var all = panel.Controls.GetControlsPaintOrder ().ToList ();
            Assert.True (all.IndexOf (top) < all.Count - 1 || all.Count == 2,
                "implicit chrome, when present, must paint after explicit children");
            Assert.Equal (top, all.Where (c => c == top || c == bottom).Last ());
        }
    }
}
