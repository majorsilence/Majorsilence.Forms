using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // RadDock.ActivateWindow is the programmatic equivalent of clicking a tab header: the target
    // window's tab becomes selected (window shown, siblings hidden) and the standard activation
    // events fire (Enter on the new window, Leave on the old, SelectedTabChanged on the dock).
    public class RadDockActivateWindowTests
    {
        private static (Form form, RadDock dock, DocumentWindow a, DocumentWindow b) BuildDock ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new System.Drawing.Size (400, 300) };
            var dock = new RadDock { Left = 0, Top = 0, Width = 380, Height = 260 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var a = new DocumentWindow { Name = "docA", Text = "Alpha" };
            var b = new DocumentWindow { Name = "docB", Text = "Bravo" };

            strip.Controls.Add (a);
            strip.Controls.Add (b);
            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);

            form.Show ();
            HeadlessRenderer.CapturePng (form); // settle initial layout (docA selected)

            return (form, dock, a, b);
        }

        [Fact]
        public void ActivateWindow_selects_the_tab_and_swaps_visibility ()
        {
            var (form, dock, a, b) = BuildDock ();
            try {
                Assert.True (a.Visible);
                Assert.False (b.Visible);

                dock.ActivateWindow (b);

                Assert.False (a.Visible);
                Assert.True (b.Visible);
                Assert.Same (b, dock.ActiveWindow);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ActivateWindow_raises_SelectedTabChanged ()
        {
            var (form, dock, a, b) = BuildDock ();
            try {
                SelectedTabChangedEventArgs? seen = null;
                dock.SelectedTabChanged += (_, e) => seen = e;

                dock.ActivateWindow (b);

                Assert.NotNull (seen);
                Assert.Same (a, seen!.OldWindow);
                Assert.Same (b, seen.NewWindow);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Activating_the_already_active_window_is_a_no_op ()
        {
            var (form, dock, a, _) = BuildDock ();
            try {
                var raised = false;
                dock.SelectedTabChanged += (_, _) => raised = true;

                dock.ActivateWindow (a);

                Assert.False (raised);
                Assert.True (a.Visible);
            } finally {
                form.Close ();
            }
        }
    }
}
