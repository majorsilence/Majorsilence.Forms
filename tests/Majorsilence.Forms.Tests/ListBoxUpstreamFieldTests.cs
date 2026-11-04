using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // In a ported app (one that chose its font), a ListBox is the native field WinForms draws. Measured on WinForms
    // (.NET 10) under the Windows 11 theme, a 120 x 120 list box of 20px rows:
    //   - Fixed3D (the default): a #828790 line, then a white one whatever the BackColor, the client 116 square from
    //     (2, 2); with IntegralHeight 104 high, or 84 asked for 100;
    //   - FixedSingle: a #7A7A7A line (#CCCCCC disabled), the client 118 square from (1, 1); with IntegralHeight 102;
    //   - None: no line, the client the whole list box; with IntegralHeight 120;
    //   - the face the BackColor, white unless set, enabled or not; the scroll bar inside the white line;
    //   - IntegralHeight turned off gives back the height asked for.
    // Here the client was one pixel in whatever the BorderStyle, inside a #ABABAB line for FixedSingle; a disabled list
    // box's face was grey; the scroll bar covered the white line; and the height IntegralHeight snapped to stayed when
    // it was turned off, or the BorderStyle changed.
    [Collection ("Headless")]
    public class ListBoxUpstreamFieldTests
    {
        private static readonly SKColor Outline3D = new SKColor (0x82, 0x87, 0x90);
        private static readonly SKColor OutlineSingle = new SKColor (0x7A, 0x7A, 0x7A);
        private static readonly SKColor OutlineSingleDisabled = new SKColor (0xCC, 0xCC, 0xCC);

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;
            public Rectangle Client;
            public Color Back;
            public int Height;
            public int ItemHeight;

            // The pixel at a logical point of the list box, sampling the top-left device pixel of that logical pixel.
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round ((10 + x) * Scale), (int) Math.Round ((10 + y) * Scale));
        }

        // A 120 x 120 list box of three 20px rows (or as many as asked), IntegralHeight off unless set.
        private static Shot Capture (Action<ListBox>? set = null, int items = 3)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (140, 140), FormBorderStyle = FormBorderStyle.None, BackColor = Color.Magenta };
                var list = new ListBox { Location = new Point (10, 10), Size = new Size (120, 120), IntegralHeight = false };
                set?.Invoke (list);
                form.Controls.Add (list);

                for (var i = 0; i < items; i++)
                    list.Items.Add ("item " + i);

                form.Show ();

                return new Shot {
                    Bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 140, 140)),
                    Scale = list.LogicalToDeviceUnits (100) / 100f,
                    Client = list.ClientRectangle,
                    Back = list.BackColor,
                    Height = list.Height,
                    ItemHeight = list.ItemHeight,
                };
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // The outline round the list box's edge, and what lies just inside it, at the middle of each side.
        private static void Edges (Shot shot, SKColor outline, SKColor inside)
        {
            var bottom = shot.Height - 1;

            Assert.Equal (outline, shot.At (0, 90));
            Assert.Equal (outline, shot.At (119, 90));
            Assert.Equal (outline, shot.At (60, 0));
            Assert.Equal (outline, shot.At (60, bottom));
            Assert.Equal (inside, shot.At (1, 90));
            Assert.Equal (inside, shot.At (118, 90));
            Assert.Equal (inside, shot.At (60, bottom - 1));
        }

        [Fact]
        public void A_Fixed3D_list_box_is_a_white_field_two_pixels_deep ()
        {
            var shot = Capture ();

            Assert.Equal (new Rectangle (2, 2, 116, 116), shot.Client);
            Edges (shot, Outline3D, SKColors.White);
        }

        [Fact]
        public void The_line_inside_a_Fixed3D_outline_stays_white_on_a_coloured_face ()
        {
            var shot = Capture (l => l.BackColor = Color.FromArgb (200, 230, 200));

            Edges (shot, Outline3D, SKColors.White);
            Assert.Equal (new SKColor (200, 230, 200), shot.At (3, 90));
        }

        [Fact]
        public void A_FixedSingle_list_box_has_a_grey_line_one_pixel_deep ()
        {
            var shot = Capture (l => l.BorderStyle = BorderStyle.FixedSingle);

            Assert.Equal (new Rectangle (1, 1, 118, 118), shot.Client);
            Edges (shot, OutlineSingle, SKColors.White);
            Edges (Capture (l => { l.BorderStyle = BorderStyle.FixedSingle; l.Enabled = false; }), OutlineSingleDisabled, SKColors.White);
        }

        [Fact]
        public void A_borderless_list_box_is_all_client ()
        {
            var shot = Capture (l => l.BorderStyle = BorderStyle.None);

            Assert.Equal (new Rectangle (0, 0, 120, 120), shot.Client);
            Assert.Equal (SKColors.White, shot.At (0, 90));
        }

        [Fact]
        public void A_disabled_list_box_keeps_its_white_face ()
        {
            var shot = Capture (l => l.Enabled = false);

            Edges (shot, Outline3D, SKColors.White);
            Assert.Equal (SKColors.White, shot.At (60, 90));
            Assert.Equal (Color.White.ToArgb (), shot.Back.ToArgb ());
        }

        [Fact]
        public void The_scroll_bar_sits_inside_the_white_line ()
        {
            var shot = Capture (items: 30);

            Assert.Equal (SKColors.White, shot.At (118, 90));
            Assert.NotEqual (SKColors.White, shot.At (117, 90));
        }

        [Theory]
        [InlineData (BorderStyle.Fixed3D, 2, 120, 104)]
        [InlineData (BorderStyle.Fixed3D, 2, 100, 84)]
        [InlineData (BorderStyle.FixedSingle, 1, 120, 102)]
        [InlineData (BorderStyle.None, 0, 120, 120)]
        public void IntegralHeight_snaps_to_whole_rows_inside_the_edge (BorderStyle border, int edge, int asked, int expected)
        {
            var shot = Capture (l => { l.IntegralHeight = true; l.Height = asked; l.BorderStyle = border; });

            // The 20px rows are Microsoft Sans Serif's, so only Windows has the measured heights; elsewhere the row
            // height differs, but the list box still snaps to whole rows inside the edge.
            Assert.Equal (2 * edge + (asked - 2 * edge) / shot.ItemHeight * shot.ItemHeight, shot.Height);

            if (OperatingSystem.IsWindows ())
                Assert.Equal (expected, shot.Height);
        }

        [Fact]
        public void Turning_IntegralHeight_off_gives_back_the_height_asked_for ()
            => Assert.Equal (120, Capture (l => { l.IntegralHeight = true; l.IntegralHeight = false; }).Height);
    }
}
