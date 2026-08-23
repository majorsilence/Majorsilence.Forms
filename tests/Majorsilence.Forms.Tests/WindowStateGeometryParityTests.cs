using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // State and geometry members a WinForms Form inherits from Control. Form is not a Control here, so
    // none of them came for free and ported code calling them on a Form did not compile. Each forwards to
    // the root ControlAdapter, which is the window's client surface -- so these check the answers are about
    // the right rectangle and the right control tree, not merely that the members exist. (The parity test
    // only checks existence; a forward wired to the wrong object satisfies it and still lies.)
    public class WindowStateGeometryParityTests
    {
        [Fact]
        public void Contains_and_HasChildren_see_the_windows_own_control_tree ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            Assert.False (form.HasChildren);

            var panel = new Panel { Bounds = new Rectangle (0, 0, 100, 100) };
            var nested = new Button ();
            panel.Controls.Add (nested);
            form.Controls.Add (panel);

            Assert.True (form.HasChildren);
            Assert.True (form.Contains (panel));
            Assert.True (form.Contains (nested), "Contains must find a deeper descendant, not just a child.");
            Assert.False (form.Contains (new Button ()));
        }

        [Fact]
        public void Created_tracks_whether_the_window_has_been_shown ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            Assert.False (form.Created);

            form.Show ();

            Assert.True (form.Created);
            Assert.Equal (form.IsHandleCreated, form.Created);
        }

        [Fact]
        public void CreateControl_raises_OnCreateControl ()
        {
            HeadlessRenderer.Use ();

            using var form = new CreateCountingForm ();
            form.CreateControl ();

            Assert.Equal (1, form.Creates);
        }

        [Fact]
        public void GetStyle_reads_back_what_SetStyle_wrote ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            // SetStyle already forwarded to the adapter; without a GetStyle on the window side there was
            // no way to read the flag back, which is what made the pair unusable from ported code.
            form.SetStyle (ControlStyles.ResizeRedraw, true);
            Assert.True (form.GetStyle (ControlStyles.ResizeRedraw));

            form.SetStyle (ControlStyles.ResizeRedraw, false);
            Assert.False (form.GetStyle (ControlStyles.ResizeRedraw));
        }

        [Fact]
        public void PreferredSize_grows_with_the_windows_contents ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            form.Controls.Add (new Panel { Bounds = new Rectangle (0, 0, 120, 80) });
            form.Show ();

            Assert.True (form.PreferredSize.Width > 0 && form.PreferredSize.Height > 0,
                $"PreferredSize is {form.PreferredSize} for a window with a 120x80 child.");
        }

        [Fact]
        public void UseWaitCursor_round_trips ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            Assert.False (form.UseWaitCursor);

            form.UseWaitCursor = true;

            Assert.True (form.UseWaitCursor);
        }

        [Fact]
        public void The_dpi_conversions_are_inverses_of_each_other ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            Assert.Equal (96, form.DeviceToLogicalUnits (form.LogicalToDeviceUnits (96)));
        }

        private sealed class CreateCountingForm : Form
        {
            internal int Creates;

            protected override void OnCreateControl ()
            {
                base.OnCreateControl ();
                Creates++;
            }
        }
    }
}
