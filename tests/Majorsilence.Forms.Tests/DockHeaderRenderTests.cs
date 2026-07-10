using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The dock's document tab strip must render its header row (and offset the selected window below
    // it) when it holds more than one document window -- mirroring how a designer-serialized dock
    // shows its documents as tabs. Repro for a harness finding where the selected window rendered at
    // the strip's top with no header band visible despite LayoutTabs having positioned it at y=26.
    public class DockHeaderRenderTests
    {
        [Fact]
        public void Dock_with_two_documents_renders_header_band_above_selected_window ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (400, 300), Text = "dock" };

            var dock = new RadDock { Left = 0, Top = 0, Width = 380, Height = 260 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var docA = new DocumentWindow { Name = "docA", Text = "Alpha", BackColor = System.Drawing.Color.Red };
            var docB = new DocumentWindow { Name = "docB", Text = "Bravo", BackColor = System.Drawing.Color.Lime };

            strip.Controls.Add (docA);
            strip.Controls.Add (docB);
            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);

            form.Show ();

            var png = HeadlessRenderer.CapturePng (form);
            using var bmp = SKBitmap.Decode (png);

            // Layout state: the selected window must sit below the 26px header band.
            Assert.True (docA.Visible);
            Assert.False (docB.Visible);
            Assert.Equal (26, docA.Top);

            // Rendered state: sample inside the header band (strip-local y ~= 13). The form's title
            // bar offsets client coordinates; find the dock's top by scanning for the first row that
            // is not the title bar / form background: instead, sample relative to the red content --
            // locate the first red row, then check the band ABOVE it is NOT red (it must be the
            // header row, not more content).
            int firstRedY = -1;
            for (var y = 0; y < bmp.Height && firstRedY < 0; y++) {
                var c = bmp.GetPixel (30, y);
                if (c.Red > 200 && c.Green < 80 && c.Blue < 80)
                    firstRedY = y;
            }

            Assert.True (firstRedY > 0, "selected document window (red) never rendered");

            var above = bmp.GetPixel (30, firstRedY - 13);
            Assert.False (above.Red > 200 && above.Green < 80 && above.Blue < 80,
                $"header band missing: pixel 13px above the content is still content-red (firstRedY={firstRedY})");
        }
    }
}
