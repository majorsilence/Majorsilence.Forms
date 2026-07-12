using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class TabStripWrapProbeTests
    {
        [Fact]
        public void Dump_wrapped_tabcontrol_png ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (420, 400) };
            var tc = new TabControl { Left = 0, Top = 0, Width = 400, Height = 360 };
            for (var i = 0; i < 12; i++)
                tc.TabPages.Add (new TabPage ($"Long Page Caption {i}") { BackColor = System.Drawing.Color.Red });
            form.Controls.Add (tc);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            var png = HeadlessRenderer.CapturePng (form); // second cycle: height growth settles
            System.IO.File.WriteAllBytes (@"A:\source\repos\MfHarness\shots\probe_tabwrap.png", png);

            var stripField = typeof (TabControl).GetField ("tab_strip",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var strip = (TabStrip) stripField.GetValue (tc)!;
            var page = tc.SelectedTabPage!;

            Assert.True (tc.RowCount > 1,
                $"rows={tc.RowCount} stripHeight={strip.Height} stripBounds={strip.Bounds} pageTop={page.Top}");
            Assert.True (strip.Height >= tc.RowCount * 31,
                $"strip must grow to fit rows: rows={tc.RowCount} stripHeight={strip.Height} pageTop={page.Top}");
            Assert.True (page.Top >= strip.Height,
                $"page must sit below the strip: stripHeight={strip.Height} pageTop={page.Top}");
        }
    }
}
