using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Exercises the backend-neutral model behind RadTabbedForm's title-bar tabs: the item collection,
    // selection, reorder, and the cross-window content move that tear-off / re-attach rely on. The
    // title-bar drag UI itself only runs on custom-chrome platforms, but these operations are what the
    // drag handlers call, so pinning them here guards detach/re-attach regardless of platform.
    public class RadTabbedFormTests
    {
        [Fact]
        public void Items_Add_AddsHostPageAndContentPanel ()
        {
            using var form = new RadTabbedForm ();

            var tab = form.TabbedFormControl.Items.Add ("General");

            Assert.Equal (1, form.TabbedFormControl.Items.Count);
            Assert.Equal ("General", tab.Text);
            Assert.NotNull (tab.ContentPanel);
            // The page is hosted by the content TabControl.
            Assert.Same (form.TabbedFormControl.Host, tab.TabPage.Parent);
        }

        [Fact]
        public void SelectedItem_RoundTrips ()
        {
            using var form = new RadTabbedForm ();
            var a = form.TabbedFormControl.Items.Add ("A");
            var b = form.TabbedFormControl.Items.Add ("B");

            form.TabbedFormControl.SelectedItem = b;

            Assert.Same (b, form.TabbedFormControl.SelectedTab);
            Assert.True (b.IsSelected);
            Assert.False (a.IsSelected);
        }

        [Fact]
        public void Items_Move_ReordersCollection ()
        {
            using var form = new RadTabbedForm ();
            var a = form.TabbedFormControl.Items.Add ("A");
            var b = form.TabbedFormControl.Items.Add ("B");
            var c = form.TabbedFormControl.Items.Add ("C");

            // Drag-to-reorder moves the model item; the strip rebuilds from this order.
            form.TabbedFormControl.Items.Move (a, 2);

            Assert.Same (b, form.TabbedFormControl.Items[0]);
            Assert.Same (c, form.TabbedFormControl.Items[1]);
            Assert.Same (a, form.TabbedFormControl.Items[2]);
        }

        [Fact]
        public void MoveTabBetweenForms_ReparentsContentToTarget ()
        {
            using var source = new RadTabbedForm ();
            using var target = new RadTabbedForm ();

            var tab = source.TabbedFormControl.Items.Add ("Doc");
            var content = new Button { Text = "payload" };
            tab.ContentPanel.Controls.Add (content);

            // This is exactly what the title-bar drag does on a re-attach / tear-off drop.
            source.TabbedFormControl.Items.Remove (tab);
            target.TabbedFormControl.Items.Add (tab);

            Assert.Equal (0, source.TabbedFormControl.Items.Count);
            Assert.Equal (1, target.TabbedFormControl.Items.Count);
            Assert.Same (tab, target.TabbedFormControl.Items[0]);

            // The content (and its child controls) followed the tab to the target window.
            Assert.Same (target.TabbedFormControl.Host, tab.TabPage.Parent);
            Assert.Same (content, tab.ContentPanel.Controls[0]);
            Assert.Same (target, content.FindForm ());
        }

        [Fact]
        public void DragTab_AcrossNeighbour_ReordersTabs ()
        {
            // End-to-end drag through the real input pipeline: press tab A, drag onto tab C, release.
            // The strip presents the headers on every platform (in the title bar on custom chrome,
            // docked below it on native chrome), so this exercises the actual reorder handlers.
            using var form = new RadTabbedForm ();
            form.TabbedFormControl.Items.Add ("AAAA");
            form.TabbedFormControl.Items.Add ("BBBB");
            form.TabbedFormControl.Items.Add ("CCCC");

            // Force a layout/paint pass so the strip lays out its tabs and Bounds are populated.
            HeadlessRenderer.CapturePng (form, 600, 400);

            var strip = form.TabStrip;
            var a = strip.Tabs[0].Bounds;
            var c = strip.Tabs[2].Bounds;
            Assert.True (a.Width > 0, "Tabs did not lay out — Bounds are empty.");

            // Tab bounds are strip-relative; the injected coordinates are form-client, so add the
            // strip's offset within the form.
            var ox = strip.Bounds.Left;
            var oy = strip.Bounds.Top;
            var y = oy + a.Top + a.Height / 2;
            HeadlessRenderer.MouseDown (form, ox + a.Left + a.Width / 2, y);
            HeadlessRenderer.MouseMove (form, ox + c.Left + c.Width / 2, y, MouseButtons.Left);
            HeadlessRenderer.MouseUp (form, ox + c.Left + c.Width / 2, y);

            // "AAAA" started first; after dragging it past the others it should land last.
            Assert.Equal ("BBBB", form.TabbedFormControl.Items[0].Text);
            Assert.Equal ("AAAA", form.TabbedFormControl.Items[2].Text);
        }

        [Fact]
        public void Items_Clear_RemovesAllPages ()
        {
            using var form = new RadTabbedForm ();
            form.TabbedFormControl.Items.Add ("A");
            form.TabbedFormControl.Items.Add ("B");

            form.TabbedFormControl.Items.Clear ();

            Assert.Equal (0, form.TabbedFormControl.Items.Count);
            Assert.Equal (0, form.TabbedFormControl.Host.TabPages.Count);
        }
    }
}
