using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // In a ported app (one that chose its font), a combo box drops down upstream's native list window. Measured on
    // WinForms (.NET 10) under the Windows 11 theme, a 150px combo of 20px rows, DropDownList and DropDown alike:
    //   - the list hangs from the combo's left edge, as wide as the combo (or DropDownWidth where wider), whatever
    //     its items' text;
    //   - it is up to 30 rows tall plus a pixel top and bottom, whatever MaxDropDownItems says; a DropDownHeight
    //     other than the default is the height;
    //   - a one-pixel #7A7A7A line round a face of the combo's BackColor, white unless set;
    //   - the highlighted row filled with #0078D7 under white text; other rows' text the combo's ForeColor, its
    //     first ink four pixels in;
    //   - the highlight follows the mouse (no hover colour of its own) and stays where the mouse left;
    //   - a DropDownList's list draws a dotted focus cue round the highlighted row: every other pixel the inverse of
    //     the highlight (#FF8728), the top-left one left. A DropDown's draws none.
    // Here the list sat a pixel right of the combo, widened to its longest item plus a scroll bar, stopped at
    // MaxDropDownItems rows, inside a #828790 line, with a hover colour of its own.
    [Collection ("Headless")]
    public class ComboBoxUpstreamDropDownTests
    {
        private static readonly SKColor Outline = new SKColor (0x7A, 0x7A, 0x7A);
        private static readonly SKColor Highlight = new SKColor (0x00, 0x78, 0xD7);
        private static readonly SKColor FocusDot = new SKColor (0xFF, 0x87, 0x28);

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;
            public Size PopupSize;
            public int PopupLeft;
            public int ComboLeft;
            public int RowHeight;

            // The row's top, in logical pixels of the popup: the one-pixel line, then the list's rows.
            public int Row (int index) => 1 + index * RowHeight;

            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round (x * Scale), (int) Math.Round (y * Scale));

            // The first logical column, from the client's left, in rows top..bottom, where a pixel passes the test.
            public int FirstInk (int top, int bottom, Func<SKColor, bool> ink)
            {
                for (var x = 1; x < PopupSize.Width - 1; x++)
                    for (var y = top; y < bottom; y++)
                        if (ink (At (x, y)))
                            return x - 1;

                return -1;
            }
        }

        // A 150px combo of the given items, dropped down with the third selected (when there is one).
        private static Shot DropDown (bool ported = true, ComboBoxStyle style = ComboBoxStyle.DropDownList, int items = 12, Action<ComboBox>? set = null, Action<ComboBox>? open = null)
        {
            try {
                if (ported)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (400, 400) };
                var combo = new ComboBox { Location = new Point (10, 10), Width = 150, DropDownStyle = style };

                for (var i = 0; i < items; i++)
                    combo.Items.Add ("Item number " + i);

                form.Controls.Add (combo);
                set?.Invoke (combo);
                form.Show ();

                if (items > 2 && combo.SelectedIndex < 0)
                    combo.SelectedIndex = 2;

                combo.DroppedDown = true;
                open?.Invoke (combo);

                var popup = PopupWindow.ShownPopups.Last ();

                try {
                    return new Shot {
                        Bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (popup)),
                        Scale = combo.LogicalToDeviceUnits (100) / 100f,
                        PopupSize = popup.Size,
                        PopupLeft = popup.Location.X,
                        ComboLeft = form.PointToScreen (combo.Location).X,
                        RowHeight = combo.PopupListBox.ItemHeight,
                    };
                } finally {
                    combo.DroppedDown = false;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void The_list_hangs_from_the_combo_as_wide_as_it_and_shows_every_row_up_to_thirty ()
        {
            var shot = DropDown ();

            Assert.Equal (new Size (150, 12 * shot.RowHeight + 2), shot.PopupSize);
            Assert.Equal (shot.ComboLeft, shot.PopupLeft);
        }

        [Fact]
        public void MaxDropDownItems_does_not_limit_the_rows_thirty_do ()
        {
            var rows = DropDown (items: 40).RowHeight;

            Assert.Equal (30 * rows + 2, DropDown (items: 40).PopupSize.Height);
            Assert.Equal (30 * rows + 2, DropDown (items: 40, set: c => c.MaxDropDownItems = 4).PopupSize.Height);
            Assert.Equal (12 * rows + 2, DropDown (set: c => c.MaxDropDownItems = 20).PopupSize.Height);
            Assert.Equal (3 * rows + 2, DropDown (items: 3).PopupSize.Height);
        }

        [Fact]
        public void A_set_DropDownHeight_and_DropDownWidth_size_the_list ()
        {
            Assert.Equal (150, DropDown (set: c => c.DropDownHeight = 150).PopupSize.Height);
            Assert.Equal (250, DropDown (set: c => c.DropDownWidth = 250).PopupSize.Width);
        }

        [Fact]
        public void A_long_item_does_not_widen_the_list ()
        {
            var shot = DropDown (items: 3, set: c => c.Items.Add ("A much much longer item than the combo is wide"));

            Assert.Equal (new Size (150, 4 * shot.RowHeight + 2), shot.PopupSize);
        }

        [Theory]
        [InlineData (ComboBoxStyle.DropDownList)]
        [InlineData (ComboBoxStyle.DropDown)]
        public void The_list_is_a_white_face_in_a_grey_line_its_selected_row_highlighted (ComboBoxStyle style)
        {
            var shot = DropDown (style: style);
            var w = shot.PopupSize.Width;
            var h = shot.PopupSize.Height;

            Assert.Equal (Outline, shot.At (0, 100));
            Assert.Equal (Outline, shot.At (w - 1, 100));
            Assert.Equal (Outline, shot.At (75, 0));
            Assert.Equal (Outline, shot.At (75, h - 1));
            Assert.Equal (SKColors.White, shot.At (1, 100));
            Assert.Equal (SKColors.White, shot.At (w - 2, 100));
            Assert.Equal (SKColors.White, shot.At (140, shot.Row (0) + 10));

            // The selected row, the whole width of the client, and nothing above or below it.
            Assert.Equal (Highlight, shot.At (140, shot.Row (2) + 10));
            Assert.Equal (Highlight, shot.At (3, shot.Row (2) + 1));
            Assert.Equal (SKColors.White, shot.At (140, shot.Row (2) - 1));
            Assert.Equal (SKColors.White, shot.At (140, shot.Row (3)));
        }

        [Fact]
        public void A_DropDownList_draws_a_dotted_focus_cue_round_the_highlighted_row ()
        {
            var shot = DropDown ();
            var top = shot.Row (2);
            var bottom = top + shot.RowHeight - 1;
            var right = shot.PopupSize.Width - 2;

            // Every other pixel of the row's edge is the highlight's inverse, starting one along from the top left. The
            // dots are a checkerboard from that corner, so which edge pixels carry one depends on the row height and
            // the list's width (Microsoft Sans Serif's 20px rows on Windows, a substitute font's elsewhere).
            SKColor Edge (int x, int y) => ((x - 1) + (y - top)) % 2 == 1 ? FocusDot : Highlight;

            Assert.Equal (Highlight, shot.At (1, top));
            Assert.Equal (FocusDot, shot.At (2, top));
            Assert.Equal (Highlight, shot.At (3, top));
            Assert.Equal (FocusDot, shot.At (1, top + 1));
            Assert.Equal (Edge (1, bottom), shot.At (1, bottom));
            Assert.Equal (Edge (2, bottom), shot.At (2, bottom));
            Assert.Equal (Edge (3, bottom), shot.At (3, bottom));
            Assert.Equal (Edge (right, top + 1), shot.At (right, top + 1));
            Assert.Equal (Edge (right, top + 2), shot.At (right, top + 2));
            if (OperatingSystem.IsWindows ()) {
                Assert.Equal (FocusDot, shot.At (1, bottom));
                Assert.Equal (Highlight, shot.At (right, top + 1));
            }

            // Inside the edge, the highlight.
            Assert.Equal (Highlight, shot.At (2, top + 1));
        }

        [Fact]
        public void A_DropDown_draws_no_focus_cue ()
        {
            var shot = DropDown (style: ComboBoxStyle.DropDown);

            Assert.Equal (Highlight, shot.At (2, shot.Row (2)));
            Assert.Equal (Highlight, shot.At (1, shot.Row (2) + 1));
            Assert.Equal (Highlight, shot.At (3, shot.Row (3) - 1));
        }

        [Fact]
        public void The_highlight_follows_the_mouse_and_stays_where_it_left ()
        {
            var shot = DropDown (open: combo => {
                var list = combo.PopupListBox;
                list.RaiseMouseMove (new MouseEventArgs (MouseButtons.None, 0, 30, 10, 0));
                list.RaiseMouseLeave (EventArgs.Empty);
            });

            Assert.Equal (Highlight, shot.At (140, shot.Row (0) + 10));
            Assert.Equal (SKColors.White, shot.At (140, shot.Row (2) + 10));
            Assert.Equal (FocusDot, shot.At (2, shot.Row (0)));
        }

        [Fact]
        public void Text_is_the_ForeColor_on_the_BackColor_and_white_on_the_highlight ()
        {
            var green = new SKColor (200, 230, 200);
            var shot = DropDown (items: 5, set: c => {
                c.BackColor = Color.FromArgb (200, 230, 200);
                c.ForeColor = Color.Red;
            });

            Assert.Equal (green, shot.At (1, shot.Row (4) + 10));
            Assert.Equal (green, shot.At (140, shot.Row (0) + 10));

            // A plain row's text is red; the highlighted row's is white.
            Assert.True (shot.FirstInk (shot.Row (0), shot.Row (1), c => c.Red > 200 && c.Green < 100 && c.Blue < 100) >= 0);
            Assert.True (shot.FirstInk (shot.Row (2) + 2, shot.Row (3) - 2, c => c.Red > 230 && c.Green > 230 && c.Blue > 230) >= 0);
        }

        [Fact]
        public void Text_starts_four_pixels_in_as_upstream_s ()
        {
            if (!OperatingSystem.IsWindows ())
                return;

            var shot = DropDown ();

            Assert.Equal (4, shot.FirstInk (shot.Row (3) + 2, shot.Row (4) - 2, c => c.Red + c.Green + c.Blue < 384));
            Assert.Equal (4, shot.FirstInk (shot.Row (2) + 2, shot.Row (3) - 2, c => c.Red + c.Green + c.Blue > 600));
        }

        [Fact]
        public void An_app_that_never_chose_a_font_keeps_the_theme_s_drop_down ()
        {
            var shot = DropDown (ported: false);

            Assert.NotEqual (Outline, shot.At (0, 50));
            Assert.True (shot.PopupLeft > shot.ComboLeft);
            Assert.True (shot.PopupSize.Width > 150);
            Assert.True (shot.PopupSize.Height < 12 * shot.RowHeight);
        }
    }
}
