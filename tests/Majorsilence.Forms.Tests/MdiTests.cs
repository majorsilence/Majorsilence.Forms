using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests for emulated MDI: container/child tracking, activation/z-order, close, the
    // MdiChildActivate event, child geometry hosted through the frame, and the LayoutMdi arrangements.
    // No window is shown on screen — MDI children are hosted in the parent's client, so child.Show()
    // never creates an OS window (it is intercepted by Form.TryShowHosted).
    public class MdiTests
    {
        private static Form Child (int w = 300, int h = 200)
        {
            var f = new Form ();
            f.Size = new Size (w, h);   // designer-set client size, used to size the host frame
            return f;
        }

        [Fact]
        public void IsMdiContainer_creates_a_client ()
        {
            using var f = new Form ();
            Assert.False (f.IsMdiContainer);

            f.IsMdiContainer = true;

            Assert.True (f.IsMdiContainer);
            Assert.NotNull (f.MdiClientControl);
            Assert.Empty (f.MdiChildren);

            f.IsMdiContainer = false;
            Assert.False (f.IsMdiContainer);
            Assert.Null (f.MdiClientControl);
        }

        [Fact]
        public void IsMdiContainer_client_yields_to_already_docked_siblings ()
        {
            // Regression: found via a real WinForms-migrated designer app (ReportDesigner.Forms)
            // rendering as a totally blank window. Its InitializeComponent adds a MenuStrip/
            // ToolStrip/StatusStrip/content Panel (all Dock=Top/Bottom) *before* the Load handler
            // sets IsMdiContainer = true -- exactly the pattern here. Dock layout processes
            // children front-to-back by z-order (index 0 first) and Controls.Add appends at the
            // back, so left un-fixed the MDI client's Dock=Fill got computed against the whole
            // display rectangle before those already-added siblings claimed their own slice,
            // visually covering them entirely. IsMdiContainer's setter must BringToFront the MDI
            // client so it dock-processes last and only gets the leftover space.
            using var f = new Form ();
            f.ClientSize = new Size (1000, 600);
            var menu = new MenuStrip ();
            var status = new StatusStrip ();
            f.Controls.Add (menu);
            f.Controls.Add (status);

            f.Show ();
            f.IsMdiContainer = true;
            HeadlessRenderer.CapturePng (f, 1000, 600);

            var client = f.MdiClientControl!;
            Assert.Equal (menu.Bounds.Bottom, client.Top);
            Assert.Equal (f.ClientSize.Height - status.Height, client.Bottom);
        }

        [Fact]
        public void Showing_a_child_hosts_it_in_the_parent ()
        {
            using var parent = new Form { IsMdiContainer = true };
            using var child = Child ();
            child.MdiParent = parent;

            Assert.True (child.IsMdiChild);
            Assert.Same (parent, child.MdiParent);

            child.Show ();

            Assert.Contains (child, parent.MdiChildren);
            Assert.Same (child, parent.ActiveMdiChild);
            Assert.NotNull (child.MdiHost);
        }

        [Fact]
        public void Newest_child_is_active_and_ActivateMdiChild_changes_it ()
        {
            using var parent = new Form { IsMdiContainer = true };
            using var a = Child ();
            using var b = Child ();
            a.MdiParent = parent; a.Show ();
            b.MdiParent = parent; b.Show ();

            Assert.Same (b, parent.ActiveMdiChild);

            parent.ActivateMdiChild (a);
            Assert.Same (a, parent.ActiveMdiChild);
        }

        [Fact]
        public void MdiChildActivate_event_fires_on_activation ()
        {
            using var parent = new Form { IsMdiContainer = true };
            var count = 0;
            parent.MdiChildActivate += (_, _) => count++;

            using var a = Child ();
            a.MdiParent = parent; a.Show ();

            Assert.True (count >= 1);
        }

        [Fact]
        public void Closing_a_child_removes_it_and_activates_the_next ()
        {
            using var parent = new Form { IsMdiContainer = true };
            using var a = Child ();
            using var b = Child ();
            a.MdiParent = parent; a.Show ();
            b.MdiParent = parent; b.Show ();

            b.Close ();

            Assert.DoesNotContain (b, parent.MdiChildren);
            Assert.Contains (a, parent.MdiChildren);
            Assert.Same (a, parent.ActiveMdiChild);
        }

        [Fact]
        public void Child_size_round_trips_through_the_host_frame ()
        {
            using var parent = new Form { IsMdiContainer = true };
            using var child = Child (320, 240);
            child.MdiParent = parent;
            child.Show ();

            Assert.Equal (new Size (320, 240), child.Size);

            child.Size = new Size (400, 260);
            Assert.Equal (new Size (400, 260), child.Size);
        }

        [Fact]
        public void Children_are_cascaded_to_distinct_locations ()
        {
            using var parent = new Form { IsMdiContainer = true };
            using var a = Child ();
            using var b = Child ();
            a.MdiParent = parent; a.Show ();
            b.MdiParent = parent; b.Show ();

            Assert.NotEqual (a.Location, b.Location);
        }

        [Fact]
        public void LayoutMdi_tile_vertical_stacks_children_within_the_client ()
        {
            using var parent = new Form { IsMdiContainer = true };
            parent.MdiClientControl!.Dock = DockStyle.None;
            parent.MdiClientControl!.SetBounds (0, 0, 800, 600, BoundsSpecified.All);

            using var a = Child ();
            using var b = Child ();
            a.MdiParent = parent; a.Show ();
            b.MdiParent = parent; b.Show ();

            parent.LayoutMdi (MdiLayout.TileVertical);

            // Two children tiled vertically (WinForms semantics): same height, side by side, inside the
            // 800x600 client.
            Assert.Equal (a.Size.Height, b.Size.Height);
            Assert.True (a.Location.X < b.Location.X);
            Assert.True (b.Location.X + b.Size.Width <= 800);
            Assert.True (a.Size.Height <= 600);
        }

        [Fact]
        public void Oversized_child_is_clamped_to_the_client ()
        {
            using var parent = new Form { IsMdiContainer = true };
            parent.MdiClientControl!.Dock = DockStyle.None;
            parent.MdiClientControl!.SetBounds (0, 0, 400, 300, BoundsSpecified.All);

            // A child larger than the client (e.g. one that kept the default form size) must be clamped
            // so it stays within the parent's bounds rather than spilling out to the monitor width.
            using var child = Child (1080, 720);
            child.MdiParent = parent; child.Show ();

            var area = parent.MdiClientControl!.DisplayRectangle;
            Assert.True (child.MdiHost!.Width <= area.Width);
            Assert.True (child.MdiHost!.Height <= area.Height);
            Assert.True (child.Location.X >= 0);
            Assert.True (child.Location.Y >= 0);
        }

        [Fact]
        public void Maximizing_a_child_fills_the_client ()
        {
            using var parent = new Form { IsMdiContainer = true };
            parent.MdiClientControl!.Dock = DockStyle.None;
            parent.MdiClientControl!.SetBounds (0, 0, 800, 600, BoundsSpecified.All);

            using var child = Child ();
            child.MdiParent = parent; child.Show ();

            child.MdiHost!.Maximize ();

            Assert.Equal (FormWindowState.Maximized, child.MdiHost!.WindowState);
            Assert.Equal (new Point (0, 0), child.Location);
            // Content fills the client minus the frame chrome.
            var area = parent.MdiClientControl!.DisplayRectangle;
            Assert.Equal (area.Width - 2 * MdiChildWindow.FrameBorder, child.Size.Width);
            Assert.Equal (area.Height - MdiChildWindow.CaptionHeight - 2 * MdiChildWindow.FrameBorder, child.Size.Height);
        }
    }
}
