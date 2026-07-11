using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // When a dock strip holds more tab headers than fit its width, the header row must scroll
    // behind right-pinned arrow buttons (like real dock tab strips) instead of letting headers run
    // off the strip's right edge unreachably.
    public class DockHeaderOverflowTests
    {
        private static (Form form, DocumentTabStrip strip) BuildDock (int windowCount, int width)
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new System.Drawing.Size (width + 20, 300) };
            var dock = new RadDock { Left = 0, Top = 0, Width = width, Height = 260 };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();

            for (var i = 0; i < windowCount; i++)
                strip.Controls.Add (new DocumentWindow { Name = $"doc{i}", Text = $"Document Number {i}" });

            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);

            form.Show ();
            HeadlessRenderer.CapturePng (form); // force a paint so header state is computed

            return (form, strip);
        }

        [Fact]
        public void Overflowing_headers_show_scroll_arrows ()
        {
            var (form, strip) = BuildDock (windowCount: 14, width: 400);
            try {
                var state = GetHeaderState (strip);
                Assert.True (state.HasOverflow, "14 wide headers in a 400px strip must overflow");
                Assert.False (state.RightArrow.IsEmpty);
                Assert.True (state.RightArrow.Right <= strip.ClientRectangle.Width);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Arrow_click_scrolls_the_header_row ()
        {
            var (form, strip) = BuildDock (windowCount: 14, width: 400);
            try {
                var state = GetHeaderState (strip);
                var firstLeftBefore = state.Rects[0].rect.Left;

                var consumed = DockStrip.HandleArrowClick (strip, state,
                    new System.Drawing.Point (state.RightArrow.Left + 3, 10));
                Assert.True (consumed, "clicking the right arrow must be consumed by the strip");
                Assert.True (state.ScrollOffset > 0);

                HeadlessRenderer.CapturePng (form); // repaint recomputes rects at the new offset
                Assert.True (state.Rects[0].rect.Left < firstLeftBefore,
                    "headers must shift left after scrolling right");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Few_tabs_do_not_show_arrows ()
        {
            var (form, strip) = BuildDock (windowCount: 2, width: 400);
            try {
                var state = GetHeaderState (strip);
                Assert.False (state.HasOverflow);
                Assert.True (state.RightArrow.IsEmpty);
                Assert.Equal (0, state.ScrollOffset);
            } finally {
                form.Close ();
            }
        }

        private static DockStrip.HeaderState GetHeaderState (DocumentTabStrip strip)
        {
            var field = typeof (DocumentTabStrip).GetField ("_headers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (DockStrip.HeaderState) field.GetValue (strip)!;
        }
    }
}
