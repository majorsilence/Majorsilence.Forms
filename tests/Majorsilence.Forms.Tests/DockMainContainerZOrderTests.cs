using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The compat dock has no SplitPanel engine: sibling tool strips keep their designer bounds
    // while the main document container fills the whole dock, so they always overlap it. The
    // container must end up frontmost (z-index 0) or a sibling strip serialized BEFORE it (a
    // designer-common shape for a top-docked tool strip row) paints over the document tab band.
    public class DockMainContainerZOrderTests
    {
        [Fact]
        public void Main_container_moves_to_front_of_z_order_on_layout ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (400, 300) };

            var dock = new RadDock { Left = 0, Top = 0, Width = 380, Height = 260 };
            var toolStrip = new ToolTabStrip { Left = 5, Top = 5, Width = 360, Height = 24 };
            toolStrip.Controls.Add (new ToolWindow { Name = "toolA", Text = "Tool A" });
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            strip.Controls.Add (new DocumentWindow { Name = "docA", Text = "Alpha" });
            strip.Controls.Add (new DocumentWindow { Name = "docB", Text = "Bravo" });
            container.Controls.Add (strip);

            // Serialized shape under test: the tool strip is added BEFORE the main container, so
            // without the fix it sits at a lower index (= topmost) and occludes the tab band.
            dock.Controls.Add (toolStrip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);

            form.Show ();
            HeadlessRenderer.CapturePng (form); // drives the initial fill + layout

            Assert.Equal (0, dock.Controls.GetChildIndex (container));
            Assert.True (dock.Controls.GetChildIndex (toolStrip) > 0);
        }
    }
}
