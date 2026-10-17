using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class TabStripBackgroundTests
    {
        [Fact]
        public void Beside_the_tabs_is_what_the_tab_control_stands_on ()
        {
            // As upstream's tab control shows its parent there. The strip painted the theme's background
            // across its full width, a light band across a dark panel.
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb (28, 69, 113) };
            var tabs = new TabControl { Location = new Point (10, 10), Size = new Size (300, 200) };
            tabs.TabPages.Add (new TabPage ("To Post"));
            panel.Controls.Add (tabs);
            form.Controls.Add (panel);
            form.Show ();

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));
            var scale = bitmap.Width / 400f;
            var row = tabs.GetTabRect (0);
            var beside = bitmap.GetPixel ((int) ((tabs.Left + 280) * scale), (int) ((tabs.Top + row.Top + row.Height / 2) * scale));

            Assert.Equal (new SKColor (28, 69, 113), beside);

            // ...while the tab itself keeps a face of its own, so its caption is not drawn on the panel.
            var on_tab = bitmap.GetPixel ((int) ((tabs.Left + row.Left + 3) * scale), (int) ((tabs.Top + row.Top + 3) * scale));
            Assert.Equal (Theme.BackgroundColor, on_tab);
        }

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void Over_several_rows_the_selected_tabs_row_sits_next_to_the_page (bool upstream_look)
        {
            // As upstream's multi-row tab control places it. The selected first tab stayed in the top row,
            // and under the upstream look its box ran down over the row below, hiding the first tabs there.
            try {
                if (upstream_look)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
                var tabs = new TabControl { Location = new Point (0, 0), Size = new Size (200, 200), Multiline = true };
                for (var i = 0; i < 6; i++)
                    tabs.TabPages.Add (new TabPage ($"Page number {i}"));
                form.Controls.Add (tabs);
                form.Show ();
                HeadlessRenderer.CapturePng (form, 400, 300);

                Assert.True (tabs.RowCount > 1, "precondition: the tabs wrap");
                var bottom_row = tabs.GetTabRect (0).Top;
                Assert.True (tabs.GetTabRect (tabs.TabCount - 1).Top < bottom_row, "the selected first tab's row is nearest the page");

                tabs.SelectedIndex = tabs.TabCount - 1;
                HeadlessRenderer.CapturePng (form, 400, 300);

                Assert.Equal (bottom_row, tabs.GetTabRect (tabs.TabCount - 1).Top);
                Assert.True (tabs.GetTabRect (0).Top < bottom_row, "the first row moves away from the page");
            } finally {
                if (upstream_look)
                    Application.SetDefaultFont (null!);
            }
        }
    }
}
