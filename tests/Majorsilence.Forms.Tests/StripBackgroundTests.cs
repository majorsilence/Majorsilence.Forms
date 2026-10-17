using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class StripBackgroundTests
    {
        [Fact]
        public void A_menu_bar_and_a_tool_strip_keep_their_own_background_on_a_coloured_form ()
        {
            // As upstream's strips paint their own background whatever the form's colour. A form with
            // BackColor = DarkGray (as an MDI parent sets for its workspace) drew both dark grey.
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300), BackColor = Color.DarkGray };
            var tools = new ToolStrip { Dock = DockStyle.Top };
            tools.Items.Add (new ToolStripLabel ("Close"));
            var menu = new MenuStrip { Dock = DockStyle.Top };
            menu.Items.Add (new ToolStripMenuItem ("Employees"));
            form.Controls.Add (tools);
            form.Controls.Add (menu);
            form.Show ();

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));
            var scale = bitmap.Width / 400f;
            var dark = new SKColor (Color.DarkGray.R, Color.DarkGray.G, Color.DarkGray.B);

            Assert.NotEqual (dark, bitmap.GetPixel ((int) (380 * scale), (int) ((menu.Top + menu.Height / 2) * scale)));
            Assert.NotEqual (dark, bitmap.GetPixel ((int) (380 * scale), (int) ((tools.Top + tools.Height / 2) * scale)));

            // ...and a colour set on the strip itself is still used.
            menu.BackColor = Color.Red;
            using var red = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));
            Assert.Equal (SKColors.Red, red.GetPixel ((int) (380 * scale), (int) ((menu.Top + menu.Height / 2) * scale)));
        }

        [Fact]
        public void A_tool_strip_panel_keeps_its_own_background_beside_a_short_strip ()
        {
            // As upstream's tool strip panel, painted by the renderer whatever its parent's colour: a strip in
            // a ToolStripContainer is only as wide as its buttons, and the panel beside it showed the form's
            // dark grey.
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300), BackColor = Color.DarkGray };
            var container = new ToolStripContainer { Dock = DockStyle.Top, Height = 60 };
            var strip = new ToolStrip ();
            strip.Items.Add (new ToolStripLabel ("Close"));
            container.TopToolStripPanel.Controls.Add (strip);
            form.Controls.Add (container);
            form.Show ();

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));
            var scale = bitmap.Width / 400f;
            var panel = container.TopToolStripPanel;

            Assert.True (panel.Height > 0, "precondition: the panel holds the strip");
            Assert.Equal (Theme.BackgroundColor, bitmap.GetPixel ((int) (390 * scale), (int) ((panel.Top + panel.Height / 2) * scale)));
        }
    }
}
