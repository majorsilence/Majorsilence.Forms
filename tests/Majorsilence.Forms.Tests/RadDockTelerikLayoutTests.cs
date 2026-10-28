using System.IO;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A layout saved by Telerik's own dock restores the tabs it describes. Measured on Telerik (UI for WinForms
    // 2026.2.520): the layout below was saved from documents w0..w3 with w3 moved first, w0's Text changed to
    // "Saved Text", w2 hidden and w1 active. Loaded into a dock holding w0..w3 in order (w0 active, w0's Text
    // "Designer Text") plus an "extra" document, it gave tabs w3, w0, w1, w2 with w0 reading "Saved Text" and
    // w1 active; w2, saved hidden, came back as a tab; extra was closed and disposed.
    [Collection ("Headless")]
    public class RadDockTelerikLayoutTests
    {
        private const string Layout =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<RadDock Orientation=\"Vertical\" CausesValidation=\"False\" TabIndex=\"0\" TabStop=\"False\"><Controls>" +
            "<Telerik.WinControls.UI.Docking.DocumentContainer IsMainDocumentContainer=\"True\" Orientation=\"Vertical\" CausesValidation=\"False\" Name=\"\">" +
            "<SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" SizeMode=\"Fill\" /><Controls>" +
            "<Telerik.WinControls.UI.Docking.DocumentTabStrip SelectedIndex=\"2\" Size=\"790, 490\" Location=\"0, 0\" CausesValidation=\"False\" Name=\"DockTabStrip1\" TabIndex=\"0\" TabStop=\"False\">" +
            "<SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls>" +
            "<Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=\"False\" CloseAction=\"Hide\" PreviousDockState=\"TabbedDocument\" Name=\"w3\" Size=\"778, 454\" Location=\"6, 30\" Font=\"Segoe UI, 9pt\" Text=\"Tab3\" />" +
            "<Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=\"False\" CloseAction=\"Hide\" PreviousDockState=\"TabbedDocument\" Name=\"w0\" Size=\"778, 454\" Location=\"6, 30\" Font=\"Segoe UI, 9pt\" Text=\"Saved Text\" />" +
            "<Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=\"False\" CloseAction=\"Hide\" PreviousDockState=\"TabbedDocument\" Name=\"w1\" Size=\"778, 454\" Location=\"6, 30\" Font=\"Segoe UI, 9pt\" Text=\"Tab1\" />" +
            "<Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=\"False\" CloseAction=\"Hide\" PreviousDockState=\"TabbedDocument\" DesiredDockState=\"Hidden\" Name=\"w2\" Location=\"0, 0\" Font=\"Segoe UI, 9pt\" Text=\"Tab2\" />" +
            "</Controls></Telerik.WinControls.UI.Docking.DocumentTabStrip></Controls></Telerik.WinControls.UI.Docking.DocumentContainer></Controls></RadDock>";

        private static (Form form, RadDock dock, DocumentTabStrip strip, DocumentWindow[] windows) BuildDock (params string[] names)
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new System.Drawing.Size (600, 300) };
            var dock = new RadDock { Left = 0, Top = 0, Width = 580, Height = 260 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var windows = names.Select (n => new DocumentWindow { Name = n, Text = n == "w0" ? "Designer Text" : n.Replace ("w", "Tab"), DockState = DockState.TabbedDocument }).ToArray ();

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

        private static void Load (RadDock dock) => dock.LoadFromXml (new MemoryStream (Encoding.UTF8.GetBytes (Layout)));

        private static string[] Tabs (DocumentTabStrip strip)
            => strip.Controls.OfType<DocumentWindow> ().Where (w => w.DockState != DockState.Hidden).Select (w => w.Name).ToArray ();

        [Fact]
        public void The_saved_text_order_and_selected_tab_are_restored ()
        {
            var (form, dock, strip, w) = BuildDock ("w0", "w1", "w2", "w3");
            try {
                Load (dock);

                Assert.Equal (new[] { "w3", "w0", "w1", "w2" }, Tabs (strip));
                Assert.Equal ("Saved Text", w[0].Text);
                Assert.Same (w[1], strip.SelectedTab);
                Assert.True (w[1].Visible);
                Assert.False (w[0].Visible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_window_the_layout_does_not_list_is_closed_and_disposed ()
        {
            var (form, dock, strip, w) = BuildDock ("w0", "w1", "w2", "w3", "extra");
            try {
                Load (dock);

                Assert.Null (w[4].Parent);
                Assert.True (w[4].IsDisposed);
                Assert.Equal (new[] { "w3", "w0", "w1", "w2" }, Tabs (strip));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_nested_dock_inside_a_window_is_left_to_its_own_layout ()
        {
            var (form, dock, strip, w) = BuildDock ("w0", "w1", "w2", "w3");
            try {
                var inner_dock = new RadDock { Dock = DockStyle.Fill };
                var inner_container = new DocumentContainer ();
                var inner_strip = new DocumentTabStrip ();
                var inner = new DocumentWindow { Name = "inner", Text = "Inner", DockState = DockState.TabbedDocument };
                inner_strip.Controls.Add (inner);
                inner_container.Controls.Add (inner_strip);
                inner_dock.Controls.Add (inner_container);
                inner_dock.MainDocumentContainer = inner_container;
                w[1].Controls.Add (inner_dock);

                Load (dock);

                Assert.False (inner.IsDisposed);
                Assert.Same (inner_strip, inner.Parent);
            } finally {
                form.Close ();
            }
        }
    }
}
