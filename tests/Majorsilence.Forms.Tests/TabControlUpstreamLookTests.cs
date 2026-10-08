using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets WinForms'
    // themed tab control: tabs sized to caption and TabControl.Padding (6, 3 by default) starting 2px in,
    // and the page at upstream's DisplayRectangle (4, row + 4, width - 8, height - row - 8). The theme's
    // edge-to-edge page put every control on a tab page 4px left of where the designer placed it.
    [Collection ("Headless")]
    public class TabControlUpstreamLookTests
    {
        private static void WithChosenFont (bool choose, System.Action<TabControl> check, System.Action<TabControl>? configure = null)
        {
            try {
                if (choose)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
                var tabs = new TabControl { Location = new Point (10, 10), Size = new Size (300, 200) };
                tabs.TabPages.Add (new TabPage ("General"));
                tabs.TabPages.Add (new TabPage ("Maps"));
                configure?.Invoke (tabs);
                form.Controls.Add (tabs);
                form.Show ();
                HeadlessRenderer.CapturePng (form, 400, 300);

                check (tabs);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void The_page_sits_at_upstream_s_display_rectangle ()
            => WithChosenFont (true, tabs => {
                var row = tabs.GetTabRect (0);
                Assert.Equal (new Point (2, 2), row.Location);
                Assert.Equal (tabs.Font.Height + 4, row.Height);   // 20px for Segoe UI 9pt

                var top = row.Bottom + 2;
                Assert.Equal (new Rectangle (4, top, 300 - 8, 200 - top - 4), tabs.TabPages[0].Bounds);
            });

        [Fact]
        public void A_tab_is_as_wide_as_its_caption_and_the_default_padding ()
            => WithChosenFont (true, tabs => {
                var caption = TextMeasurer.MeasureText ("General", tabs.TabStrip.GetEffectiveFont (), tabs.TabStrip.GetEffectiveFontSize ()).Width;
                Assert.Equal ((int) System.Math.Ceiling (caption) + 12, tabs.GetTabRect (0).Width);
                Assert.Equal (new Point (0, 0), tabs.Padding);   // the property keeps what was set
            });

        [Fact]
        public void A_padding_set_by_the_designer_is_used ()
            => WithChosenFont (true, tabs => {
                var caption = TextMeasurer.MeasureText ("General", tabs.TabStrip.GetEffectiveFont (), tabs.TabStrip.GetEffectiveFontSize ()).Width;
                Assert.Equal ((int) System.Math.Ceiling (caption) + 20, tabs.GetTabRect (0).Width);
            }, tabs => tabs.Padding = new Point (10, 3));

        [Fact]
        public void Without_a_chosen_font_the_page_fills_the_control_under_the_strip ()
            => WithChosenFont (false, tabs => {
                Assert.Equal (0, tabs.TabPages[0].Left);
                Assert.Equal (300, tabs.TabPages[0].Width);
            });
    }
}
