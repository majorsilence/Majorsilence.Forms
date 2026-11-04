using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's scroll bar under Windows 11, measured at 96 DPI with a WinForms probe: 17px thick
    // (SystemInformation.VerticalScrollBarWidth and HorizontalScrollBarHeight, and the bar every ListBox,
    // TreeView, multiline TextBox, AutoScroll Panel and DataGridView shows), 17px arrows, a white line
    // along the leading edge, the 2px thumb centred on the 16px track beside it, and no thumb drawn
    // shorter than 17px. A ported app (one that chose its font) gets that bar; the theme's 15px bar was
    // 2px narrower, so every list was 2px wider inside. Apps that never chose a font keep the 15px bar.
    [Collection ("Headless")]
    public class ScrollBarUpstreamWidthTests
    {
        private static T Ported<T> (bool chooseFont, Func<T> body)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));

                HeadlessRenderer.Use ();
                return body ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static VerticalScrollBar VerticalBar (Control host)
            => host.Controls.GetAllControls ().OfType<VerticalScrollBar> ().Single ();

        private static HorizontalScrollBar HorizontalBar (Control host)
            => host.Controls.GetAllControls ().OfType<HorizontalScrollBar> ().Single ();

        [Fact]
        public void A_scroll_bar_is_17px_thick_by_default ()
        {
            var (v, h) = Ported (true, () => (new VScrollBar ().Size, new HScrollBar ().Size));

            Assert.Equal (new Size (17, 80), v);   // upstream's VScrollBar and HScrollBar defaults
            Assert.Equal (new Size (80, 17), h);
        }

        [Fact]
        public void Without_a_chosen_font_a_scroll_bar_keeps_the_theme_thickness ()
        {
            var (v, h) = Ported (false, () => (new VScrollBar ().Size, new HScrollBar ().Size));

            Assert.Equal (15, v.Width);
            Assert.Equal (15, h.Height);
        }

        public static TheoryData<string> Hosts => new () { "ListBox", "TreeView", "TextBox", "DataGridView", "ListView" };

        private static Control MakeHost (string kind)
        {
            switch (kind) {
                case "ListBox": {
                    var lb = new ListBox { Size = new Size (150, 200), IntegralHeight = false };
                    for (var i = 0; i < 100; i++)
                        lb.Items.Add ("item " + i);
                    return lb;
                }
                case "TreeView": {
                    var tv = new TreeView { Size = new Size (150, 200) };
                    for (var i = 0; i < 40; i++)
                        tv.Nodes.Add ("node " + i);
                    return tv;
                }
                case "TextBox":
                    return new TextBox { Size = new Size (150, 200), Multiline = true, ScrollBars = ScrollBars.Vertical };
                case "DataGridView": {
                    var dgv = new DataGridView { Size = new Size (300, 200) };
                    dgv.Columns.Add ("a", "A");
                    for (var i = 0; i < 50; i++)
                        dgv.Rows.Add ("x");
                    return dgv;
                }
                default: {
                    var lv = new ListView { Size = new Size (150, 200), View = View.List };
                    for (var i = 0; i < 100; i++)
                        lv.Items.Add ("item " + i);
                    return lv;
                }
            }
        }

        [Theory]
        [MemberData (nameof (Hosts))]
        public void A_controls_vertical_bar_is_17px_wide (string kind)
        {
            var width = Ported (true, () => {
                using var form = new Form { ClientSize = new Size (400, 300) };
                var host = MakeHost (kind);
                form.Controls.Add (host);
                form.Show ();
                return VerticalBar (host).Width;
            });

            Assert.Equal (17, width);
        }

        [Theory]
        [MemberData (nameof (Hosts))]
        public void Without_a_chosen_font_a_controls_vertical_bar_stays_15px (string kind)
        {
            var width = Ported (false, () => {
                using var form = new Form { ClientSize = new Size (400, 300) };
                var host = MakeHost (kind);
                form.Controls.Add (host);
                form.Show ();
                return VerticalBar (host).Width;
            });

            Assert.Equal (15, width);
        }

        [Theory]
        [InlineData (true, 17)]
        [InlineData (false, 15)]
        public void A_panels_auto_scroll_bars_take_the_bar_thickness (bool chooseFont, int expected)
        {
            var (v, h) = Ported (chooseFont, () => {
                using var form = new Form { ClientSize = new Size (400, 300) };
                var panel = new Panel { Size = new Size (150, 200), AutoScroll = true };
                panel.Controls.Add (new Button { Size = new Size (400, 600) });
                form.Controls.Add (panel);
                form.Show ();
                var vb = VerticalBar (panel);
                var hb = HorizontalBar (panel);
                return (vb.Visible ? vb.Width : -1, hb.Visible ? hb.Height : -1);
            });

            Assert.Equal (expected, v);
            Assert.Equal (expected, h);
        }

        [Theory]
        [InlineData (true, 17)]
        [InlineData (false, 15)]
        public void The_arrows_are_as_long_as_the_bar_is_thick (bool chooseFont, int expected)
        {
            var arrow = Ported (chooseFont, () => {
                using var form = new Form { ClientSize = new Size (100, 300) };
                var bar = new VScrollBar { Height = 200 };
                form.Controls.Add (bar);
                form.Show ();
                return RenderManager.GetRenderer<ScrollBarRenderer> ()!.GetDecrementArrowBounds (bar).Height / bar.ScaleFactor.Height;
            });

            Assert.Equal (expected, arrow);
        }

        // The drawn bar, column by column across a ListBox's right side, in logical pixels: the white edge
        // line, then the track, with the thumb in the middle two columns of the track.
        [Fact]
        public void A_listbox_draws_upstreams_bar ()
        {
            var columns = Ported (true, () => {
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 220), BackColor = Color.Magenta };
                var lb = new ListBox { Location = new Point (10, 10), Size = new Size (150, 200), IntegralHeight = false };
                for (var i = 0; i < 100; i++)
                    lb.Items.Add ("item " + i);
                form.Controls.Add (lb);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 200, 220));
                var scale = bitmap.Width / 200f;
                var bar = VerticalBar (lb);
                var origin = form.PointToClient (bar.PointToScreen (Point.Empty));
                var left = (int) Math.Round (origin.X * scale);

                // The thumb sits at the top of the track, 17..33 logical; read the rows at 25 and at 150.
                char Kind (int x, int y) {
                    var c = bitmap.GetPixel (x, y);
                    return c == SKColors.White ? 'e' : c == ScrollBar.UpstreamTrack ? 't' : c == ScrollBar.UpstreamThumb ? 'T' : '?';
                }

                var top = (int) Math.Round (origin.Y * scale);
                var thumbRow = top + (int) (25 * scale);
                var trackRow = top + (int) (150 * scale);
                var s = (int) Math.Round (scale);

                return (string.Concat (Enumerable.Range (0, 17).Select (i => Kind (left + i * s, thumbRow))),
                        string.Concat (Enumerable.Range (0, 17).Select (i => Kind (left + i * s, trackRow))));
            });

            Assert.Equal ("etttttttTTttttttt", columns.Item1);
            Assert.Equal ("e" + new string ('t', 16), columns.Item2);
        }

        // A long list's thumb hit-tests 8px long, as upstream's does, but is drawn 17px: centred on the
        // short one mid-track, and against the arrow at the end of the track (probes/wfscrollw).
        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void The_shortest_thumb_drawn_is_17px (bool atEnd)
        {
            var (thumbTop, thumbLength, hitCentre, trackEnd) = Ported (true, () => {
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (100, 220) };
                var bar = new VScrollBar { Location = new Point (10, 10), Height = 196, Maximum = 20000, LargeChange = 10 };
                form.Controls.Add (bar);
                form.Show ();
                bar.Value = atEnd ? bar.Maximum - bar.LargeChange + 1 : 10000;

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 100, 220));
                var scale = bitmap.Width / 100f;
                var x = (int) Math.Round ((bar.Left + 8) * scale);
                var rows = Enumerable.Range ((int) Math.Round (bar.Top * scale), bar.ScaledHeight)
                    .Where (y => bitmap.GetPixel (x, y) is var c && c != ScrollBar.UpstreamTrack && c != SKColors.White)
                    .ToArray ();
                var hit = RenderManager.GetRenderer<ScrollBarRenderer> ()!.GetThumbDragBounds (bar);
                return ((rows.First () - bar.Top * scale) / scale, rows.Length / scale, (hit.Y + (hit.Height + 1) / 2f) / scale, 196 - 17);
            });

            Assert.Equal (17, thumbLength, 1.1);   // anti-aliased caps round to a pixel either way
            if (atEnd)
                Assert.Equal (trackEnd, thumbTop + thumbLength, 1.1);
            else
                Assert.Equal (hitCentre, thumbTop + thumbLength / 2, 1.1);
        }
    }
}
