using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Characterisation tests for layout/positioning defects seen on the Avalonia backend that do not
    // occur on real WinForms. Each asserts the invariant the upstream control guarantees, so a failure
    // here names the defect rather than describing a screenshot.
    // Same collection as the other tests that touch HeadlessRenderer.ChromeOffset: it is a global
    // static, so running alongside them means a title bar appears halfway through a measurement.
    [Collection ("Headless")]
    public class MacQaLayoutTests
    {
        private static Form ShowFormAt (int x, int y, int w = 600, int h = 400)
        {
            HeadlessRenderer.Use ();
            var form = new Form { Size = new Size (w, h), StartPosition = FormStartPosition.Manual };
            form.Show ();
            form.Location = new Point (x, y);
            return form;
        }

        // Showing a form that is already shown must do nothing. It used to run the whole first-show
        // path again and give one form a SECOND window surface: input goes to the newer one on top
        // while the controls keep painting into the first, so the top window looks like an empty
        // shadow of the one beneath it and typing into it appears down there. Application code calls
        // Show twice quite ordinarily -- a factory that shows the form plus a configure callback that
        // also calls Show -- and upstream that is harmless.
        [Fact]
        public void Showing_an_already_shown_form_does_not_raise_a_second_window ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (300, 200) };
            form.Show ();
            form.Show ();
            form.Show ();

            var host = (Majorsilence.Forms.Headless.HeadlessWindowHost) form.Backend;
            Assert.Equal (1, host.ShowCount);
        }

        // A bottom-aligned tab strip must sit flush against the bottom of its TabControl from the
        // FIRST layout, not from the first thing that happens to trigger a second one.
        //
        // The strip settles its own wrap -- and its own height -- inside OnLayout, which the parent
        // runs as part of its layout pass, after the dock pass has already placed the strip from the
        // height it had on entry. Early passes run before the TabControl is sized, so the tabs wrap
        // against a zero width and the strip arrives three rows (93px) tall; the dock pass then puts
        // it at Bottom-93 and the strip promptly shrinks to one 31px row, leaving the headers floating
        // 62px above the bottom edge. The parent never re-docks it: SetBoundsCore's request lands
        // while the parent is mid-pass, so it only sets LayoutDeferred, and the parent clears that
        // flag as the pass unwinds. Clicking a tab was simply the first thing to force a fresh
        // top-level layout, which made a pure layout defect look like a selection one.
        [Fact]
        public void Bottom_aligned_tab_headers_sit_on_the_bottom_edge_before_any_selection_change ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (600, 400) };
            var tabs = new TabControl { Dock = DockStyle.Fill, Alignment = TabAlignment.Bottom };
            tabs.TabPages.Add (new TabPage ("Invoices"));
            tabs.TabPages.Add (new TabPage ("Details"));
            tabs.TabPages.Add (new TabPage ("Address"));
            form.Controls.Add (tabs);
            form.Show ();

            Assert.Equal (tabs.Height, tabs.GetTabRect (0).Bottom);
        }

        // A layout that was requested, deferred and then abandoned still has to run, because only this
        // control's own layout pass can position the strip and, left alone, nothing ever asks for that
        // pass at a moment it can run.
        //
        // This is the designer's shape: the bounds are assigned inside SuspendLayout/ResumeLayout(false),
        // so the requests are recorded as LayoutDeferred rather than performed, and ResumeLayout(false)
        // by contract does not perform them on the way out. A form's closing PerformLayout lays out the
        // FORM's children rather than ours, and a TabControl given designer bounds is never resized
        // again, so OnResize -- the usual trigger -- never fires either. Resizing the window does not
        // reach it.
        //
        // Undocked, the strip keeps what LayoutTabs gave it, measured against the strip's own 600x31
        // default instead of the container: for a bottom-aligned strip that is near the TOP, at
        // (0, 13, 600, 18) on an 864-wide control, with the page showing through to its right. The
        // first selection change was the first thing to run the pass, which moved the whole header row
        // to the bottom -- so this presented as a click bug rather than a layout one.
        //
        // No form and no parent layout here on purpose: that is the whole point.
        [Fact]
        public void An_abandoned_layout_still_docks_the_header_strip ()
        {
            HeadlessRenderer.Use ();

            using var tabs = new TabControl { Alignment = TabAlignment.Bottom, ItemSize = new Size (80, 18) };
            tabs.SuspendLayout ();
            tabs.TabPages.Add (new TabPage ("Invoices"));
            tabs.TabPages.Add (new TabPage ("Details"));
            tabs.TabPages.Add (new TabPage ("Address"));
            tabs.Size = new Size (864, 399);
            tabs.ResumeLayout (false);

            tabs.CreateControl ();

            Assert.Equal (tabs.Height, tabs.GetTabRect (0).Bottom);
            Assert.Equal (tabs.Width, tabs.TabStrip.Width);
        }

        // The same abandoned pass, in the two other shapes that carry an implicit docked child. A
        // Ribbon holds a tab strip like a TabControl's; a WebBrowser holds a Dock=Fill view host.
        // Neither is placed by anything but its owner's layout, so both came out at their constructed
        // defaults -- the strip 600 wide whatever the Ribbon's width, the host a 0x0 nothing.
        //
        // Not every implicit child is affected, and the difference is worth recording: a scroll bar is
        // created hidden, the dock pass correctly skips hidden children, and showing one lays it out
        // then and there. Those self-correct at the only moment they matter. These do not.
        [Fact]
        public void A_ribbon_built_the_designer_way_still_sizes_its_tab_strip ()
        {
            HeadlessRenderer.Use ();

            using var ribbon = new Ribbon ();
            ribbon.SuspendLayout ();
            ribbon.Size = new Size (400, 300);
            ribbon.ResumeLayout (false);

            ribbon.CreateControl ();

            var strip = ribbon.Controls.GetAllControls (true).OfType<TabStrip> ().Single ();

            Assert.Equal (ribbon.Width, strip.Width);
        }

        [Fact]
        public void A_web_browser_built_the_designer_way_still_fills_its_host ()
        {
            HeadlessRenderer.Use ();

            using var browser = new WebBrowser ();
            browser.SuspendLayout ();
            browser.Size = new Size (400, 300);
            browser.ResumeLayout (false);

            browser.CreateControl ();

            var host = browser.Controls.GetAllControls (true).Single (c => c.Dock == DockStyle.Fill);

            Assert.Equal (new Size (400, 300), host.Size);
        }

        // ...and selecting a tab must not move them, in either direction.
        [Fact]
        public void Selecting_a_tab_does_not_move_a_bottom_aligned_header_strip ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (600, 400) };
            var tabs = new TabControl { Dock = DockStyle.Fill, Alignment = TabAlignment.Bottom };
            tabs.TabPages.Add (new TabPage ("Invoices"));
            tabs.TabPages.Add (new TabPage ("Details"));
            tabs.TabPages.Add (new TabPage ("Address"));
            form.Controls.Add (tabs);
            form.Show ();

            var before = tabs.GetTabRect (1);
            tabs.SelectedIndex = 1;

            Assert.Equal (before, tabs.GetTabRect (1));
        }

        // The same for the other edge whose position is derived from the strip's own measurement: a
        // right-aligned strip takes the width of its widest tab, so a self-resize there has to hold
        // the right edge the dock pass anchored it to.
        [Fact]
        public void Right_aligned_tab_headers_sit_on_the_right_edge_before_any_selection_change ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (600, 400) };
            var tabs = new TabControl { Dock = DockStyle.Fill, Alignment = TabAlignment.Right };
            tabs.TabPages.Add (new TabPage ("Invoices"));
            tabs.TabPages.Add (new TabPage ("Details"));
            form.Controls.Add (tabs);
            form.Show ();

            Assert.Equal (tabs.Width, tabs.GetTabRect (0).Right);
        }

        // Hiding and showing again is a real show, not a repeat: the guard must key on current
        // visibility rather than "has ever been shown".
        [Fact]
        public void Hiding_then_showing_again_shows_the_window_again ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (300, 200) };
            form.Show ();
            form.Hide ();
            form.Show ();

            var host = (Majorsilence.Forms.Headless.HeadlessWindowHost) form.Backend;
            Assert.Equal (2, host.ShowCount);
        }

        // The calendar must open anchored to the field: its left edge on the field's left edge, its top
        // on the field's bottom. Detached placement (the popup landing near the screen origin, or at a
        // The same anchoring must hold when the platform draws a title bar: the popup is placed in screen
        // Same guarantee for a field inside a container: a GroupBox is what the reported forms use, and
        // A GroupBox's caption occupies the top of its own border, so its client area must start BELOW
        // the caption. A child placed at the top of the client area overlapping the caption/border is
        // the reported defect ("Select Levy Types" drawing through its own frame).
        [Fact]
        public void GroupBox_client_area_starts_below_its_caption ()
        {
            using var form = ShowFormAt (0, 0);
            var group = new GroupBox { Location = new Point (10, 10), Size = new Size (200, 120), Text = "Select Levy Types" };
            form.Controls.Add (group);

            var client = group.DisplayRectangle;

            Assert.True (client.Y > 0, $"client area starts at Y={client.Y}; it must clear the caption");
            Assert.True (client.Height < group.Height, "client area must be shorter than the box");
        }

        // A control added to a GroupBox is positioned relative to that box's client area. Its position
        // in the form is the box's own position plus its client origin plus the child's Location --
        // which is what a drop-down, a hit test and a native overlay all rely on.
        [Fact]
        public void A_child_of_a_GroupBox_reports_its_position_through_the_container ()
        {
            using var form = ShowFormAt (0, 0);
            var group = new GroupBox { Location = new Point (30, 40), Size = new Size (200, 120) };
            form.Controls.Add (group);

            var child = new TextBox { Location = new Point (12, 18), Size = new Size (80, 22) };
            group.Controls.Add (child);

            Assert.Equal (group.PointToScreen (child.Location), child.PointToScreen (Point.Empty));
        }
    }
}
