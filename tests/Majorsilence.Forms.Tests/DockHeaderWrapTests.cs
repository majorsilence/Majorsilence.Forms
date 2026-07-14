using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // When a dock strip holds more tab headers than fit its width, the headers wrap into
    // additional rows (WinForms multiline tab behavior) and the selected window's content starts
    // below the whole band, so every tab stays visible and clickable.
    public class DockHeaderWrapTests
    {
        private static (Form form, DocumentTabStrip strip) BuildDock (int windowCount, int width)
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new System.Drawing.Size (width + 20, 400) };
            var dock = new RadDock { Left = 0, Top = 0, Width = width, Height = 360 };
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

        private static DockStrip.HeaderState GetHeaderState (DocumentTabStrip strip)
        {
            var field = typeof (DocumentTabStrip).GetField ("_headers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (DockStrip.HeaderState) field.GetValue (strip)!;
        }

        [Fact]
        public void Overflowing_headers_wrap_into_multiple_rows ()
        {
            var (form, strip) = BuildDock (windowCount: 14, width: 400);
            try {
                var state = GetHeaderState (strip);
                Assert.True (state.RowCount > 1, "14 wide headers in a 400px strip must wrap");

                // Every header stays within the strip's width.
                foreach (var (_, rect) in state.Rects)
                    Assert.True (rect.Right <= strip.ClientRectangle.Width,
                        $"header at {rect} crosses the strip's right edge");

                // At least one header sits on a later row.
                Assert.Contains (state.Rects, h => h.rect.Top >= DockStrip.HeaderHeight);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Content_starts_below_the_wrapped_band ()
        {
            var (form, strip) = BuildDock (windowCount: 14, width: 400);
            try {
                var state = GetHeaderState (strip);
                var selected = DockStrip.Windows (strip).Single (w => w.Visible);

                Assert.Equal (state.RowCount * DockStrip.HeaderHeight, selected.Top);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Second_row_tab_is_hit_testable ()
        {
            var (form, strip) = BuildDock (windowCount: 14, width: 400);
            try {
                var state = GetHeaderState (strip);
                var onSecondRow = state.Rects.First (h => h.rect.Top >= DockStrip.HeaderHeight);

                var hit = DockStrip.HitTest (state, new System.Drawing.Point (
                    onSecondRow.rect.Left + 5, onSecondRow.rect.Top + 5));

                Assert.Same (onSecondRow.win, hit);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Few_tabs_stay_on_one_row ()
        {
            var (form, strip) = BuildDock (windowCount: 2, width: 400);
            try {
                var state = GetHeaderState (strip);
                Assert.Equal (1, state.RowCount);
                Assert.All (state.Rects, h => Assert.Equal (0, h.rect.Top));
            } finally {
                form.Close ();
            }
        }
    }
}
