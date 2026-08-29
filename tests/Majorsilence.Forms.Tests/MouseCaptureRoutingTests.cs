using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A control that captures the mouse on MouseDown must keep receiving moves until the button comes
    // up -- including while the pointer is over one of its own children. Routing by hit-test instead
    // hands the move to the child and silently ends the gesture, which is what broke dragging a window
    // by a custom title bar: the drag died the moment the pointer crossed the caption buttons.
    public class MouseCaptureRoutingTests
    {
        // Builds a form with a "title bar" panel carrying two buttons, like a custom-chrome app.
        private static (Form Form, Panel Bar, Button Child) Chrome ()
        {
            HeadlessRenderer.Use ();

            var form = new Form {
                ClientSize = new System.Drawing.Size (300, 200),
                FormBorderStyle = FormBorderStyle.None,
            };
            var bar = new Panel { Left = 0, Top = 0, Width = 300, Height = 40 };
            var child = new Button { Left = 200, Top = 5, Width = 40, Height = 30, Text = "x" };

            bar.Controls.Add (child);
            form.Controls.Add (bar);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            return (form, bar, child);
        }

        [Fact]
        public void A_capturing_control_keeps_the_moves_when_the_pointer_crosses_a_child ()
        {
            var (form, bar, _) = Chrome ();
            using (form) {
                var moves = 0;
                bar.MouseMove += (s, e) => moves++;

                // Press on bare panel, then drag straight across the button sitting on it.
                HeadlessRenderer.MouseDown (form, 100, 20);
                HeadlessRenderer.MouseMove (form, 220, 20, MouseButtons.Left);

                Assert.Equal (1, moves);
            }
        }

        [Fact]
        public void A_capturing_control_gets_the_release_wherever_the_pointer_ends_up ()
        {
            var (form, bar, _) = Chrome ();
            using (form) {
                var ups = 0;
                bar.MouseUp += (s, e) => ups++;

                HeadlessRenderer.MouseDown (form, 100, 20);
                HeadlessRenderer.MouseUp (form, 220, 20);

                Assert.Equal (1, ups);
            }
        }

        [Fact]
        public void Capture_is_released_so_the_next_gesture_routes_normally ()
        {
            var (form, bar, child) = Chrome ();
            using (form) {
                HeadlessRenderer.MouseDown (form, 100, 20);
                HeadlessRenderer.MouseUp (form, 100, 20);

                Assert.False (bar.Capture);

                // With the capture gone, a press on the button must reach the button.
                var childDowns = 0;
                child.MouseDown += (s, e) => childDowns++;
                HeadlessRenderer.MouseDown (form, 220, 20);
                HeadlessRenderer.MouseUp (form, 220, 20);

                Assert.Equal (1, childDowns);
            }
        }

        // The other half of the rule: a child that took the capture still owns it. Only the control
        // that actually captured should swallow the events, not every ancestor.
        [Fact]
        public void A_capturing_child_still_wins_over_its_parent ()
        {
            var (form, bar, child) = Chrome ();
            using (form) {
                var childMoves = 0;
                var barMoves = 0;
                child.MouseMove += (s, e) => childMoves++;
                bar.MouseMove += (s, e) => barMoves++;

                HeadlessRenderer.MouseDown (form, 220, 20);
                HeadlessRenderer.MouseMove (form, 100, 20, MouseButtons.Left);

                Assert.Equal (1, childMoves);
                Assert.Equal (0, barMoves);
            }
        }

        // End-to-end: the gesture a custom title bar is built from -- record the press point, move the
        // window by the delta on every move -- has to survive crossing the caption buttons.
        [Fact]
        public void A_window_drag_from_a_custom_title_bar_survives_crossing_its_buttons ()
        {
            var (form, bar, _) = Chrome ();
            using (form) {
                var last = System.Drawing.Point.Empty;
                bar.MouseDown += (s, e) => last = new System.Drawing.Point (e.X, e.Y);
                bar.MouseMove += (s, e) => {
                    if (e.Button == MouseButtons.Left) {
                        form.Left += e.X - last.X;
                        form.Top += e.Y - last.Y;
                    }
                };

                form.Left = 0;
                form.Top = 0;

                HeadlessRenderer.MouseDown (form, 100, 20);
                HeadlessRenderer.MouseMove (form, 220, 30, MouseButtons.Left);

                Assert.Equal (120, form.Left);
                Assert.Equal (10, form.Top);
            }
        }

        [Fact]
        public void A_click_handler_that_shows_a_modal_does_not_keep_the_mouse_captured ()
        {
            // Regression: found running ReportDesigner. A button captures on mouse-down; its Click
            // handler opened a modal dialog and blocked in the nested loop, so the mouse-up that
            // drops the capture never ran. Every release inside the modal was then routed straight
            // back to the still-captured button, re-firing its Click -- clicking OK on the dialog
            // opened another copy instead of closing it. Mouse-up (which releases capture) must run
            // before Click, as WinForms does.
            var (form, _, child) = Chrome ();
            using (form) {
                var clicks = 0;
                child.Click += (_, _) =>
                {
                    clicks++;
                    if (clicks == 1)
                    {
                        var dlg = new Form ();
                        var t = dlg.ShowDialogAsync (form);
                        Assert.False (child.Capture, "capture must be released before the Click handler runs");
                        dlg.DialogResult = DialogResult.OK;
                        _ = t;
                    }
                };

                HeadlessRenderer.MouseDown (form, 220, 20);
                HeadlessRenderer.MouseUp (form, 220, 20);

                Assert.Equal (1, clicks);
                Assert.False (child.Capture);
            }
        }
    }
}
