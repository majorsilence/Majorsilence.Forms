using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Minimal docking-layout rendering: a migrated RadDock form (rdkMain -> DocumentContainer ->
    // DocumentTabStrip -> DocumentWindows) must actually show its content. Telerik's SizeInfo/tab-strip
    // layout engine isn't implemented in the compat layer, so before this the windows parented but sized
    // to nothing and overlapped (found opening frmMaintainCustomer -- "missing almost everything").
    public class DockingLayoutTests
    {
        private static (Form form, RadDock dock, DocumentWindow first, DocumentWindow second) BuildTree ()
        {
            var form = new Form { Width = 700, Height = 460 };
            var dock = new RadDock { Left = 0, Top = 40, Width = 690, Height = 380 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var first = new DocumentWindow ("Information");
            var second = new DocumentWindow ("Receivables");
            strip.Controls.Add (first);
            strip.Controls.Add (second);
            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);
            return (form, dock, first, second);
        }

        [Fact]
        public void Selected_document_window_fills_others_hidden ()
        {
            var (form, dock, first, second) = BuildTree ();
            using (form) {
                form.Show ();

                // The content chain filled: the first (selected) window is visible and sized to a real area,
                // roughly the dock width; the second (unselected tab) is hidden.
                Assert.True (first.Visible);
                Assert.False (second.Visible);
                Assert.True (first.Width > dock.Width / 2, $"selected window width {first.Width} should ~fill the {dock.Width}px dock");
                Assert.True (first.Height > 100, $"selected window height {first.Height} should ~fill the dock");
            }
        }

        [Fact]
        public void Single_document_window_has_no_header_and_fills ()
        {
            var form = new Form { Width = 500, Height = 400 };
            var dock = new RadDock { Left = 0, Top = 0, Width = 500, Height = 380 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var only = new DocumentWindow ("Only");
            strip.Controls.Add (only);
            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);

            using (form) {
                form.Show ();
                Assert.True (only.Visible);
                // No header row for a single tab -> the window starts at the top of the strip.
                Assert.True (only.Top <= strip.Top + 1, $"single-tab window top {only.Top} should sit at the strip top {strip.Top}");
            }
        }
    }
}
