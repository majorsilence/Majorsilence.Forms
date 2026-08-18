using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The root ControlAdapter used to be sized only inside WindowBase.RenderFrame, so a window that had
    // not painted yet had a 0x0 client area and every docked or anchored child laid out against nothing.
    // WinForms has no such window: a form's client rectangle is real as soon as it has a size, which is
    // why WinForms code freely reads child geometry in a Load handler, and why a headless or
    // never-painted window is expected to lay out correctly all the same.
    public class PrePaintDockLayoutTests
    {
        [Fact]
        public void Docked_child_is_sized_by_Show_before_anything_paints ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new System.Drawing.Size (800, 450) };
            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            var fill = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (top);
            form.Controls.Add (fill);

            form.Show ();   // deliberately no CapturePng: nothing paints in this test

            Assert.Equal (800, top.Width);
            Assert.True (fill.Width == 800 && fill.Height > 0,
                $"Fill-docked child is {fill.Size} in an 800x450 form that has not painted yet.");
        }

        [Fact]
        public void Load_handler_sees_the_settled_size_of_a_docked_child ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new System.Drawing.Size (640, 480) };
            var docked = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (docked);

            var widthAtLoad = -1;
            form.Load += (_, _) => widthAtLoad = docked.Width;

            form.Show ();

            Assert.Equal (640, widthAtLoad);
        }

        [Fact]
        public void PerformLayout_on_an_unshown_window_lays_out_against_its_real_size ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new System.Drawing.Size (500, 300) };
            var docked = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (docked);

            form.PerformLayout ();

            Assert.Equal (500, docked.Width);
        }

        [Fact]
        public void The_windows_own_layout_pass_still_runs_during_Show ()
        {
            HeadlessRenderer.Use ();

            // Sizing the adapter before `shown` is set means the adapter cannot forward that pass to the
            // window (ControlAdapter.OnLayout gates on it), so the show sequence has to run one more pass
            // afterwards -- the pass the first painted frame used to be responsible for. A Form subclass
            // that decides anything in OnLayout (DockPanelSuite's FloatWindow sets its own Visible there)
            // depends on it.
            var form = new LayoutCountingForm { ClientSize = new System.Drawing.Size (300, 200) };

            using (form) {
                form.Show ();
                Assert.True (form.Layouts > 0, "The form's own OnLayout never ran during Show.");
            }
        }

        private sealed class LayoutCountingForm : Form
        {
            internal int Layouts;

            protected internal override void OnLayout (LayoutEventArgs e)
            {
                base.OnLayout (e);
                Layouts++;
            }
        }
    }
}
