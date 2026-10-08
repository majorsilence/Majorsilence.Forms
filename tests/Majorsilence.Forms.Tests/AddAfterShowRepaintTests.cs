using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A control added to a window that is already on screen has to reach the screen without anything
    // else causing a repaint. The new control is dirty, but the window was never told it had something
    // to paint: the desktop Avalonia window polls for dirty controls every frame and hid that, while the
    // browser/mobile single view and the embedded presenter paint only when asked, so a control added
    // from a Timer.Tick stayed off screen -- and out of the browser's accessibility DOM -- until something
    // unrelated repainted. Upstream needs nothing here: the child is its own HWND and gets WM_PAINT.
    [Collection ("Headless")]
    public class AddAfterShowRepaintTests
    {
        private static (Form Form, HeadlessWindowHost Host) ShowForm ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (300, 200) };
            form.Show ();
            // Paint once, so what follows is a change to a window that has already been drawn.
            HeadlessRenderer.CapturePng (form, 300, 200);
            return (form, (HeadlessWindowHost) form.Backend);
        }

        [Fact]
        public void Adding_to_a_shown_form_asks_the_window_to_repaint ()
        {
            var (form, host) = ShowForm ();
            using var _ = form;
            var before = host.InvalidateCount;

            form.Controls.Add (new Panel { Location = new Point (10, 10), Size = new Size (60, 40) });

            Assert.True (host.InvalidateCount > before, "The window backend was never asked to repaint for the added control.");
        }

        [Fact]
        public void Adding_inside_a_nested_container_asks_the_window_to_repaint ()
        {
            var (form, host) = ShowForm ();
            using var _ = form;
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);
            HeadlessRenderer.CapturePng (form, 300, 200);
            var before = host.InvalidateCount;

            panel.Controls.Add (new Button { Text = "Late", Location = new Point (10, 10) });

            Assert.True (host.InvalidateCount > before, "The window backend was never asked to repaint for the added control.");
        }

        [Fact]
        public void Adding_does_not_raise_Invalidated_on_the_parent ()
        {
            // Upstream raises no Invalidated for an add; the repaint goes to the window, not through the event.
            var (form, _) = ShowForm ();
            using var __ = form;
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);
            var raised = 0;
            panel.Invalidated += (_, _) => raised++;

            panel.Controls.Add (new Button { Text = "Late", Location = new Point (10, 10) });

            Assert.Equal (0, raised);
        }
    }
}
