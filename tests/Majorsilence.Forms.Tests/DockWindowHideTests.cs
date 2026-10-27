using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // DockWindow.Hide takes a window out of its dock, as upstream's does. Measured on Telerik (UI for WinForms
    // 2026.2.520) with documents w0..w3: Hide set the DockState to Hidden and removed the tab. Hiding the active
    // w2 made w3 active, raising w2.Leave and w3.Enter but no SelectedTabChanged; hiding the active w3, then
    // last, made w1 active. Show put a hidden window back as the first tab, TabbedDocument, and the active tab
    // stayed active.
    [Collection ("Headless")]
    public class DockWindowHideTests
    {
        private static (Form form, RadDock dock, DocumentTabStrip strip, DocumentWindow[] windows) BuildDock ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new System.Drawing.Size (500, 300) };
            var dock = new RadDock { Left = 0, Top = 0, Width = 480, Height = 260 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var windows = Enumerable.Range (0, 4).Select (i => new DocumentWindow { Name = "w" + i, Text = "Tab" + i, DockState = DockState.TabbedDocument }).ToArray ();

            foreach (var w in windows)
                strip.Controls.Add (w);
            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);

            form.Show ();
            HeadlessRenderer.CapturePng (form);

            return (form, dock, strip, windows);
        }

        private static string[] Tabs (DocumentTabStrip strip)
            => strip.Controls.OfType<DocumentWindow> ().Where (w => w.DockState != DockState.Hidden).Select (w => w.Name).ToArray ();

        [Fact]
        public void Hide_takes_the_tab_away_and_activates_the_next ()
        {
            var (form, dock, strip, w) = BuildDock ();
            try {
                w[0].Hide ();

                Assert.Equal (DockState.Hidden, w[0].DockState);
                Assert.Equal (new[] { "w1", "w2", "w3" }, Tabs (strip));
                Assert.Same (w[1], strip.SelectedTab);
                Assert.Same (w[1], dock.ActiveWindow);
                Assert.True (w[1].Visible);
                Assert.False (w[0].Visible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Hiding_the_active_middle_tab_activates_the_next_and_the_last_the_previous ()
        {
            var (form, dock, strip, w) = BuildDock ();
            try {
                dock.ActivateWindow (w[2]);
                var log = new System.Collections.Generic.List<string> ();
                foreach (var window in w) {
                    var name = window.Name;
                    window.Enter += (s, e) => log.Add (name + ".Enter");
                    window.Leave += (s, e) => log.Add (name + ".Leave");
                }
                dock.SelectedTabChanged += (s, e) => log.Add ("SelectedTabChanged");

                w[2].Hide ();

                Assert.Same (w[3], strip.SelectedTab);
                Assert.Equal (new[] { "w2.Leave", "w3.Enter" }, log);

                w[3].Hide ();

                Assert.Same (w[1], strip.SelectedTab);
                Assert.True (w[1].Visible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_hidden_window_does_not_come_back_with_a_tab_click_elsewhere ()
        {
            var (form, dock, strip, w) = BuildDock ();
            try {
                w[2].Hide ();
                dock.ActivateWindow (w[3]);

                Assert.False (w[2].Visible);
                Assert.Equal (new[] { "w0", "w1", "w3" }, Tabs (strip));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Show_puts_a_hidden_window_back_first_without_activating_it ()
        {
            var (form, dock, strip, w) = BuildDock ();
            try {
                w[2].Hide ();
                w[2].Show ();

                Assert.Equal (DockState.TabbedDocument, w[2].DockState);
                Assert.Equal (new[] { "w2", "w0", "w1", "w3" }, Tabs (strip));
                Assert.Same (w[0], strip.SelectedTab);
                Assert.False (w[2].Visible);
            } finally {
                form.Close ();
            }
        }
    }
}
