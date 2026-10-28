using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The selected node as upstream draws it, measured on WinForms (Windows 11 theme):
    //   - with the focus: the label filled with Highlight (#0078D7), its text HighlightText (white), and with
    //     focus cues a one-pixel checkerboard of black and #FF8728 round the label;
    //   - without the focus and with HideSelection off: Control (#F0F0F0) behind ControlText;
    //   - without the focus and with HideSelection on: no highlight, but still the selected image.
    // The colours are a ported app's (one that chose its font with Application.SetDefaultFont); the theme drew
    // its own selection colour. The selected image is every app's: here a tree that lost the focus swapped its
    // selected image for the normal one.
    [Collection ("Headless")]
    public class TreeViewUpstreamSelectionTests
    {
        private static readonly SKColor Highlight = new SKColor (0x00, 0x78, 0xD7);
        private static readonly SKColor Control = new SKColor (0xF0, 0xF0, 0xF0);
        private static readonly SKColor Inverse = new SKColor (0xFF, 0x87, 0x28);
        private static readonly SKColor Red = new SKColor (0xFF, 0x00, 0x00);
        private static readonly SKColor Green = new SKColor (0x00, 0xFF, 0x00);

        private static SKBitmap Solid (SKColor colour)
        {
            var bitmap = new SKBitmap (16, 16);
            using (var canvas = new SKCanvas (bitmap))
                canvas.Clear (colour);
            return bitmap;
        }

        // Counts of each colour of interest in the capture.
        private sealed class Counts
        {
            public int Highlight, Control, White, Inverse, Red, Green;
        }

        private static Counts Render (bool chooseFont, bool focused, bool hideSelection = true, bool cues = false)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

                HeadlessRenderer.Use ();

                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 80) };
                var images = new ImageList { ImageSize = new Size (16, 16) };
                images.Images.Add (Solid (Red));
                images.Images.Add (Solid (Green));

                var tree = new TreeView {
                    Location = new Point (0, 0), Size = new Size (240, 80), BorderStyle = BorderStyle.None,
                    ImageList = images, ImageIndex = 0, SelectedImageIndex = 1, HideSelection = hideSelection, TabIndex = 1,
                };
                tree.Nodes.Add ("System Settings");
                tree.Nodes.Add ("User Account Setup");

                var button = new Button { Text = "x", Location = new Point (250, 10), Size = new Size (40, 24), TabIndex = 0 };

                form.Controls.Add (tree);
                form.Controls.Add (button);
                form.Show ();

                tree.SelectedNode = tree.Nodes[0];
                if (focused)
                    tree.Focus ();
                else
                    button.Focus ();
                form.ShowFocusCues = cues;

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 80));
                var counts = new Counts ();
                var width = (int) (240 * bitmap.Width / 300f);

                for (var x = 0; x < width; x++)
                    for (var y = 0; y < bitmap.Height; y++) {
                        var c = bitmap.GetPixel (x, y);
                        if (c == Highlight) counts.Highlight++;
                        else if (c == Control) counts.Control++;
                        else if (c == SKColors.White) counts.White++;
                        else if (c == Inverse) counts.Inverse++;
                        else if (c == Red) counts.Red++;
                        else if (c == Green) counts.Green++;
                    }

                return counts;
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void With_the_focus_the_label_is_highlight_behind_white_text ()
        {
            var counts = Render (true, focused: true);

            Assert.True (counts.Highlight > 500, $"{counts.Highlight} highlight pixels");
            Assert.True (counts.White > 20, $"{counts.White} white text pixels");
            Assert.Equal (0, counts.Inverse);
        }

        [Fact]
        public void With_focus_cues_a_checkerboard_frames_the_label ()
        {
            var counts = Render (true, focused: true, cues: true);

            Assert.True (counts.Inverse > 50, $"{counts.Inverse} #FF8728 focus pixels");
        }

        [Fact]
        public void Without_the_focus_and_with_HideSelection_off_the_label_is_control_grey ()
        {
            var counts = Render (true, focused: false, hideSelection: false);

            Assert.True (counts.Control > 500, $"{counts.Control} #F0F0F0 pixels");
            Assert.Equal (0, counts.Highlight);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_hidden_selection_keeps_its_selected_image (bool chooseFont)
        {
            var counts = Render (chooseFont, focused: false, hideSelection: true);

            // One node selected (green), one not (red).
            Assert.True (counts.Green >= 200, $"{counts.Green} selected-image pixels");
            Assert.True (counts.Red >= 200, $"{counts.Red} image pixels");
            Assert.Equal (0, counts.Highlight);
        }
    }
}
