using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Covers the plain-Control member surface that Form only has because WindowBase declares it
    // (Form derives from WindowBase, not Control -- see COMPATIBILITY_MATRIX.md). Members that
    // forward to the root ControlAdapter are asserted through their real effect; the ones that are
    // stored stubs by design (no layout parent exists for a top-level window) get the get/set
    // round-trip the stub policy asks for.
    public class FormControlParityTests
    {
        [Fact]
        public void Control_layout_stubs_have_winforms_defaults ()
        {
            using var form = new Form ();

            Assert.Equal (AnchorStyles.Top | AnchorStyles.Left, form.Anchor);
            Assert.Equal (DockStyle.None, form.Dock);
            Assert.Equal (0, form.TabIndex);
            Assert.Equal (new Padding (3), form.Margin);
            Assert.Equal (Padding.Empty, form.Padding);
            Assert.Null (form.Region);
            Assert.Equal (RightToLeft.No, form.RightToLeft);
            Assert.False (form.AutoScroll);
            Assert.Null (form.Parent);
            Assert.Equal (FormCornerPreference.Default, form.FormCornerPreference);
        }

        [Fact]
        public void Anchor_Dock_TabIndex_Margin_roundtrip ()
        {
            using var form = new Form ();

            form.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            form.Dock = DockStyle.Fill;
            form.TabIndex = 7;
            form.Margin = new Padding (1, 2, 3, 4);

            Assert.Equal (AnchorStyles.Bottom | AnchorStyles.Right, form.Anchor);
            Assert.Equal (DockStyle.Fill, form.Dock);
            Assert.Equal (7, form.TabIndex);
            Assert.Equal (new Padding (1, 2, 3, 4), form.Margin);
        }

        [Fact]
        public void Region_roundtrips ()
        {
            using var form = new Form ();
            using var region = new Majorsilence.Forms.Drawing.Region (new Rectangle (0, 0, 10, 10));

            form.Region = region;

            Assert.Same (region, form.Region);

            form.Region = null;
            Assert.Null (form.Region);
        }

        [Fact]
        public void Padding_roundtrips ()
        {
            using var form = new Form ();

            form.Padding = new Padding (5, 6, 7, 8);

            Assert.Equal (new Padding (5, 6, 7, 8), form.Padding);
        }

        // Padding is a real forward to the root ControlAdapter (a ScrollableControl), whose
        // DisplayRectangle it deflates -- so, as in WinForms, it insets docked children.
        [Fact]
        public void Padding_insets_docked_children ()
        {
            using var form = new Form { UseSystemDecorations = true };
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);

            HeadlessRenderer.CapturePng (form, 400, 300);
            var unpadded = panel.Bounds;

            form.Padding = new Padding (10);
            form.PerformLayout ();
            var padded = panel.Bounds;

            Assert.Equal (unpadded.Left + 10, padded.Left);
            Assert.Equal (unpadded.Top + 10, padded.Top);
            Assert.Equal (unpadded.Width - 20, padded.Width);
            Assert.Equal (unpadded.Height - 20, padded.Height);
        }

        // RightToLeft forwards to the adapter, which is the parent of every child control, so a child
        // left on Inherit resolves through the form exactly as it would through a parent Control.
        [Fact]
        public void RightToLeft_is_inherited_by_child_controls ()
        {
            using var form = new Form ();
            var child = new Button ();
            form.Controls.Add (child);

            Assert.Equal (RightToLeft.No, child.RightToLeft);

            form.RightToLeft = RightToLeft.Yes;

            Assert.Equal (RightToLeft.Yes, form.RightToLeft);
            Assert.Equal (RightToLeft.Yes, child.RightToLeft);
        }

        // AutoScroll* forward to the root ScrollableControl, not to fresh backing fields: setting a
        // non-empty AutoScrollMinSize is what turns AutoScroll on there.
        [Fact]
        public void AutoScroll_members_forward_to_the_scrollable_root ()
        {
            using var form = new Form ();

            form.AutoScrollMinSize = new Size (1000, 800);

            Assert.Equal (new Size (1000, 800), form.AutoScrollMinSize);
            Assert.True (form.AutoScroll);

            form.SetAutoScrollMargin (4, 5);
            Assert.Equal (new Size (4, 5), form.AutoScrollMargin);

            form.AutoScroll = false;
            Assert.False (form.AutoScroll);
        }

        [Fact]
        public void FormCornerPreference_roundtrips_and_validates ()
        {
            using var form = new Form ();

            form.FormCornerPreference = FormCornerPreference.RoundSmall;
            Assert.Equal (FormCornerPreference.RoundSmall, form.FormCornerPreference);

            Assert.Throws<InvalidEnumArgumentException> (() => form.FormCornerPreference = (FormCornerPreference) 42);
        }

        // ── Parent ───────────────────────────────────────────────────────────────

        [Fact]
        public void Parent_is_the_MdiClient_while_hosted_as_an_mdi_child ()
        {
            using var parent = new Form { IsMdiContainer = true };
            using var child = new Form { Size = new Size (300, 200) };
            child.MdiParent = parent;

            Assert.Null (child.Parent);

            child.Show ();

            Assert.Same (parent.MdiClientControl, child.Parent);
            Assert.Same (parent, child.ParentForm);

            child.Close ();

            // The frame is gone, so the form is a plain top-level window again.
            Assert.Null (child.Parent);
        }

        [Fact]
        public void Parent_roundtrips_for_a_top_level_form ()
        {
            using var form = new Form ();
            var panel = new Panel ();

            form.Parent = panel;

            Assert.Same (panel, form.Parent);

            form.Parent = null;
            Assert.Null (form.Parent);
        }

        // ── MouseEnter / MouseLeave ──────────────────────────────────────────────
        // These are really wired: every backend reports pointer exit through HandlePointerExited, and
        // any pointer event arriving means the pointer is over the window.

        [Fact]
        public void MouseEnter_fires_once_per_entry_and_MouseLeave_on_exit ()
        {
            using var form = new Form ();
            var entered = 0;
            var left = 0;
            form.MouseEnter += (_, _) => entered++;
            form.MouseLeave += (_, _) => left++;

            form.HandlePointerMoved (MouseButtons.None, 10, 10, Keys.None);

            Assert.Equal (1, entered);
            Assert.Equal (0, left);

            // Moving within the window must not re-raise MouseEnter.
            form.HandlePointerMoved (MouseButtons.None, 20, 20, Keys.None);
            form.HandlePointerMoved (MouseButtons.None, 30, 30, Keys.None);

            Assert.Equal (1, entered);

            form.HandlePointerExited (MouseButtons.None, 30, 30, Keys.None);

            Assert.Equal (1, left);

            // A second exit with the pointer already outside is not a new leave.
            form.HandlePointerExited (MouseButtons.None, 30, 30, Keys.None);
            Assert.Equal (1, left);

            // Re-entry raises MouseEnter again.
            form.HandlePointerMoved (MouseButtons.None, 15, 15, Keys.None);
            Assert.Equal (2, entered);
        }

        [Fact]
        public void MouseEnter_fires_when_a_press_or_wheel_is_the_first_pointer_event ()
        {
            using var press_form = new Form ();
            var pressed_entered = 0;
            press_form.MouseEnter += (_, _) => pressed_entered++;

            press_form.HandlePointerPressed (MouseButtons.Left, 5, 5, Keys.None);
            Assert.Equal (1, pressed_entered);

            using var wheel_form = new Form ();
            var wheel_entered = 0;
            wheel_form.MouseEnter += (_, _) => wheel_entered++;

            wheel_form.HandlePointerWheel (MouseButtons.None, 5, 5, new Point (0, 120), Keys.None);
            Assert.Equal (1, wheel_entered);
        }

        [Fact]
        public void MouseEnter_fires_over_the_resize_border_chrome ()
        {
            // The window chrome is part of the window: entering over a resize edge (where
            // HandleMouseMove takes over the event) still counts as an entry.
            using var form = new Form ();
            Assert.True (form.Resizeable);

            var entered = 0;
            form.MouseEnter += (_, _) => entered++;

            form.HandlePointerMoved (MouseButtons.None, 0, 0, Keys.None);

            Assert.Equal (1, entered);
            Assert.True (form.IsMouseOver);
        }
    }
}
