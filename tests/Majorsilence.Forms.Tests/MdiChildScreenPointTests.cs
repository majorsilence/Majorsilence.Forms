using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A form drawn inside another window -- an MDI child, a panel-hosted form -- has no on-screen window of its own, so
    // its controls' screen points must come from the frame drawing it. They came from the form's own backend window,
    // which is never shown and sits wherever it was last put before hosting: (0, 0) for a CenterScreen MDI child, so
    // its combo box's list opened at the MDI client's top-left. Measured on WinForms (.NET 10, 96 DPI, an 800 x 600
    // parent whose client is 796 x 596): a 300 x 200 CenterScreen child sits at (248, 198) in the client, its client
    // area starts at the client's screen origin plus that plus its frame, a combo at (20, 30) in it is that plus
    // (20, 30) on screen, the list opens with its top-left at the combo's bottom-left, and PointToClient undoes
    // PointToScreen. The same holds for a Manual child. Not gated on ported mode: a misplaced popup is wrong in
    // every mode.
    [Collection ("Headless")]
    public class MdiChildScreenPointTests
    {
        private static (Form parent, MdiClient client) Container ()
        {
            HeadlessRenderer.Use ();
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);
            return (parent, parent.MdiClientControl!);
        }

        // Where a point in the child's client area should be on screen: through the MDI client, which is part of
        // a real window, the frame's place in it and the frame's border and caption.
        private static Point Expected (MdiClient client, Form child, Point inChild)
        {
            var frame = child.MdiHost!;
            var origin = frame.ContentOrigin;
            return client.PointToScreen (new Point (frame.Left + origin.X + inChild.X, frame.Top + origin.Y + inChild.Y));
        }

        private static void Check (bool ported, FormStartPosition position)
        {
            if (ported)
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            try {
                var (parent, client) = Container ();
                var child = new Form { MdiParent = parent, Size = new Size (300, 200), StartPosition = position };
                if (position == FormStartPosition.Manual)
                    child.Location = new Point (100, 80);
                var combo = new ComboBox { Location = new Point (20, 30), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
                for (var i = 0; i < 5; i++)
                    combo.Items.Add ("Item " + i);
                child.Controls.Add (combo);
                child.Show ();
                HeadlessRenderer.CapturePng (parent, 900, 700);

                var frame = child.MdiHost!;
                if (position == FormStartPosition.CenterScreen)
                    Assert.Equal (new Point ((client.ViewportWidth - frame.Width) / 2, (client.ViewportHeight - frame.Height) / 2), child.Location);
                else
                    Assert.Equal (new Point (100, 80), child.Location);

                Assert.Equal (Expected (client, child, Point.Empty), child.PointToScreen (Point.Empty));
                Assert.Equal (Expected (client, child, new Point (20, 30)), combo.PointToScreen (Point.Empty));
                Assert.Equal (Expected (client, child, new Point (25, 37)), combo.PointToScreen (new Point (5, 7)));
                Assert.Equal (new Point (20, 30), child.PointToClient (combo.PointToScreen (Point.Empty)));
                Assert.Equal (new Point (5, 7), combo.PointToClient (combo.PointToScreen (new Point (5, 7))));

                var popups = PopupWindow.ShownPopups.Length;
                combo.DroppedDown = true;
                try {
                    Assert.Equal (popups + 1, PopupWindow.ShownPopups.Length);
                    var popup = PopupWindow.ShownPopups.Last ();
                    var under = combo.PointToScreen (new Point (0, combo.Height));
                    Assert.InRange (popup.Location.X - under.X, 0, (int) Math.Ceiling (parent.DesktopScaling));
                    Assert.Equal (under.Y, popup.Location.Y);
                } finally {
                    combo.DroppedDown = false;
                }

                parent.Close ();
            } finally {
                if (ported)
                    Application.SetDefaultFont (null!);
            }
        }

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void CenterScreen_child_reports_and_opens_popups_where_it_is_drawn (bool ported) => Check (ported, FormStartPosition.CenterScreen);

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void Manual_child_reports_and_opens_popups_where_it_is_drawn (bool ported) => Check (ported, FormStartPosition.Manual);

        [Fact]
        public void Panel_hosted_form_reports_screen_points_through_its_panel ()
        {
            HeadlessRenderer.Use ();
            var outer = new Form ();
            var panel = new Panel { Location = new Point (40, 50), Size = new Size (400, 300) };
            outer.Controls.Add (panel);
            var inner = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Location = new Point (15, 25), Size = new Size (200, 150) };
            var button = new Button { Location = new Point (7, 9) };
            inner.Controls.Add (button);
            panel.Controls.Add (inner);
            inner.Show ();
            HeadlessRenderer.CapturePng (outer, 600, 500);

            Assert.Equal (panel.PointToScreen (new Point (15 + 7, 25 + 9)), button.PointToScreen (Point.Empty));
            Assert.Equal (new Point (7, 9), inner.PointToClient (button.PointToScreen (Point.Empty)));

            outer.Close ();
        }
    }
}
