using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TabControl tab headers wrap into additional rows when they overflow the strip's width
    // (multiline tab behavior), so every tab stays visible and clickable; the strip grows by
    // whole rows and the pages move below the whole band.
    public class TabStripWrapTests
    {
        private static TabControl BuildTabs (int pageCount, int width)
        {
            var tc = new TabControl { Width = width, Height = 400 };
            for (var i = 0; i < pageCount; i++)
                tc.TabPages.Add (new TabPage ($"Long Page Caption {i}"));
            return tc;
        }

        [Fact]
        public void Overflowing_tabs_wrap_into_multiple_rows ()
        {
            using var tc = BuildTabs (pageCount: 12, width: 400);

            tc.PerformLayout ();
            ForceTabLayout (tc);

            Assert.True (tc.RowCount > 1, $"12 wide tabs in a 400px strip must wrap (rows={tc.RowCount})");

            // Every tab stays within the strip's width, and at least one sits on a later row.
            for (var i = 0; i < tc.TabCount; i++)
                Assert.True (tc.GetTabRect (i).Right <= 400, $"tab {i} at {tc.GetTabRect (i)} crosses the right edge");

            Assert.True (tc.GetTabRect (tc.TabCount - 1).Top > 0, "last tab must sit on a wrapped row");
        }

        [Fact]
        public void Few_tabs_stay_on_one_row ()
        {
            using var tc = BuildTabs (pageCount: 2, width: 400);

            tc.PerformLayout ();
            ForceTabLayout (tc);

            Assert.Equal (1, tc.RowCount);
            Assert.Equal (0, tc.GetTabRect (1).Top);
        }

        // Tab layout normally runs on paint; drive it directly so the test needs no backend.
        private static void ForceTabLayout (TabControl tc)
        {
            var stripField = typeof (TabControl).GetField ("tab_strip",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var strip = (TabStrip) stripField.GetValue (tc)!;
            var layout = typeof (TabStrip).GetMethod ("LayoutTabs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            layout.Invoke (strip, null);
        }
    }
}
