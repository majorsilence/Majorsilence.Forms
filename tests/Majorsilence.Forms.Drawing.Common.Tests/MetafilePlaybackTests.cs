using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // Metafile playback, tested by rendering real record streams and reading the resulting pixels.
    //
    // Asserting on pixels rather than on parser internals is deliberate: a player can decode every
    // field correctly and still draw the wrong picture, because what a metafile means depends on the
    // device-context state a record inherits. The pixel is the only thing that proves both.
    public class MetafilePlaybackTests
    {
        // COLORREF is 0x00BBGGRR -- the byte order that trips up every metafile reader once.
        private const uint Red = 0x000000FF;
        private const uint Green = 0x0000FF00;   // RGB(0,255,0) -- Skia calls this Lime, not Green
        private const uint Blue = 0x00FF0000;

        private static SKColor PixelAt (Metafile metafile, int x, int y)
        {
            var bitmap = metafile.GetSKBitmap () ?? throw new InvalidOperationException ("no raster");
            return bitmap.GetPixel (x, y);
        }

        [Fact]
        public void An_emf_rectangle_is_filled_with_the_selected_brush ()
        {
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)                 // NULL_PEN, so only the fill shows
                .Rectangle (0, 0, 100, 100)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Red, PixelAt (metafile, 50, 50));
        }

        [Fact]
        public void A_colorref_is_read_blue_green_red_not_red_green_blue ()
        {
            // If the byte order were wrong this test would still pass with a fill -- but in blue.
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 40, 40)
                .Build (0, 0, 40, 40);

            using var metafile = new Metafile (new MemoryStream (bytes));
            var pixel = PixelAt (metafile, 20, 20);

            Assert.Equal (255, pixel.Red);
            Assert.Equal (0, pixel.Blue);
        }

        [Fact]
        public void An_emf_leaves_untouched_pixels_transparent ()
        {
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Green)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 20, 20)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Lime, PixelAt (metafile, 10, 10));
            Assert.Equal (0, PixelAt (metafile, 80, 80).Alpha);
        }

        [Fact]
        public void An_emf_polygon_is_filled ()
        {
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Blue)
                .SelectObject (1)
                .SelectStock (8)
                .Polygon16 ((0, 0), (100, 0), (100, 100), (0, 100))
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Blue, PixelAt (metafile, 50, 50));
        }

        [Fact]
        public void An_emf_line_is_drawn_with_the_selected_pen ()
        {
            var bytes = new EmfBuilder ()
                .CreatePen (1, Red, 10)
                .SelectObject (1)
                .MoveTo (0, 50)
                .LineTo (100, 50)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.True (PixelAt (metafile, 50, 50).Red > 200);
            Assert.Equal (0, PixelAt (metafile, 50, 10).Alpha);
        }

        [Fact]
        public void A_null_brush_fills_nothing_but_still_outlines ()
        {
            var bytes = new EmfBuilder ()
                .CreatePen (1, Red, 6)
                .SelectObject (1)
                .SelectStock (5)                 // NULL_BRUSH
                .Rectangle (10, 10, 90, 90)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            // Hollow in the middle, drawn on the edge -- which is what a null brush means.
            Assert.Equal (0, PixelAt (metafile, 50, 50).Alpha);
            Assert.True (PixelAt (metafile, 50, 10).Red > 150);
        }

        [Fact]
        public void A_selected_object_persists_until_another_is_selected ()
        {
            // The point of the object table: the second rectangle names no brush, so it must inherit
            // the one selected before the first.
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 50, 100)
                .Rectangle (50, 0, 100, 100)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Red, PixelAt (metafile, 25, 50));
            Assert.Equal (SKColors.Red, PixelAt (metafile, 75, 50));
        }

        [Fact]
        public void Restore_dc_puts_back_the_brush_that_was_selected_before_the_save ()
        {
            // A leak across SaveDC/RestoreDC is the classic metafile player bug: everything after the
            // restore draws in the colour the saved block chose.
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .CreateBrush (2, Green)
                .SelectObject (1)
                .SelectStock (8)
                .SaveDc ()
                .SelectObject (2)
                .Rectangle (0, 0, 50, 100)
                .RestoreDc (-1)
                .Rectangle (50, 0, 100, 100)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Lime, PixelAt (metafile, 25, 50));
            Assert.Equal (SKColors.Red, PixelAt (metafile, 75, 50));
        }

        [Fact]
        public void A_world_transform_moves_what_follows_it ()
        {
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .SetWorldTransform (1, 0, 0, 1, 50, 0)   // translate right by 50
                .Rectangle (0, 0, 40, 40)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Red, PixelAt (metafile, 70, 20));
            Assert.Equal (0, PixelAt (metafile, 20, 20).Alpha);
        }

        [Fact]
        public void A_window_to_viewport_mapping_scales_what_follows_it ()
        {
            // The metafile declares a 50x50 logical space displayed over 100x100 device units, so a
            // logical 25x25 rectangle covers the top-left quarter.
            var bytes = new EmfBuilder ()
                .SetWindowOrg (0, 0)
                .SetWindowExt (50, 50)
                .SetViewportOrg (0, 0)
                .SetViewportExt (100, 100)
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 25, 25)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Red, PixelAt (metafile, 25, 25));
            Assert.Equal (0, PixelAt (metafile, 75, 75).Alpha);
        }

        [Fact]
        public void An_unknown_record_is_skipped_rather_than_thrown_on ()
        {
            var bytes = new EmfBuilder ()
                .Record (999, new byte[8])       // no such record type
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 100, 100)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            // The picture still draws, and the unknown record is reported rather than hidden.
            Assert.Equal (SKColors.Red, PixelAt (metafile, 50, 50));
            Assert.True (metafile.UnsupportedRecordCount > 0);
        }

        [Fact]
        public void A_truncated_metafile_draws_what_it_could_read ()
        {
            var full = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 100, 100)
                .Build (0, 0, 100, 100);

            // Clipboard metafiles arrive truncated often enough that losing the picture entirely
            // would be the wrong behaviour.
            var truncated = full.Take (full.Length - 12).ToArray ();

            using var metafile = new Metafile (new MemoryStream (truncated));

            Assert.Equal (SKColors.Red, PixelAt (metafile, 50, 50));
        }

        [Fact]
        public void An_emf_with_no_records_is_a_transparent_raster_rather_than_a_failure ()
        {
            var bytes = new EmfBuilder ().Build (0, 0, 32, 32);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (32, metafile.Width);
            Assert.Equal (0, PixelAt (metafile, 16, 16).Alpha);
        }

        [Fact]
        public void Text_is_drawn_in_the_current_text_colour ()
        {
            var bytes = new EmfBuilder ()
                .SetBkMode (1)                   // TRANSPARENT, so no opaque box behind the glyphs
                .SetTextColor (Red)
                .ExtTextOutW (2, 30, "IIIIIIII")
                .Build (0, 0, 100, 50);

            using var metafile = new Metafile (new MemoryStream (bytes));
            var bitmap = metafile.GetSKBitmap ()!;

            {
                var reds = 0;

                for (var y = 0; y < bitmap.Height; y++) {
                    for (var x = 0; x < bitmap.Width; x++) {
                        var pixel = bitmap.GetPixel (x, y);

                        if (pixel.Alpha > 128 && pixel.Red > 128 && pixel.Green < 128)
                            reds++;
                    }
                }

                Assert.True (reds > 0, "no text pixels were drawn in the text colour");
            }
        }

        // ── WMF ─────────────────────────────────────────────────────────────────

        [Fact]
        public void A_wmf_rectangle_is_filled_with_the_selected_brush ()
        {
            var bytes = new WmfBuilder ()
                .SetWindowOrg (0, 0)
                .SetWindowExt (100, 100)
                .CreateBrush (Red)
                .SelectObject (0)
                .CreatePen (Red, 0, 5)           // PS_NULL, so only the fill shows
                .SelectObject (1)
                .Rectangle (0, 0, 100, 100)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Red, PixelAt (metafile, 50, 50));
        }

        [Fact]
        public void A_wmf_rectangle_is_read_bottom_right_first ()
        {
            // WMF stores a rectangle as bottom, right, top, left. Reading it in EMF's order would
            // put this rectangle in the wrong quarter -- and still draw something, which is why the
            // assertion checks both a filled and an empty corner.
            var bytes = new WmfBuilder ()
                .SetWindowOrg (0, 0)
                .SetWindowExt (100, 100)
                .CreateBrush (Green)
                .SelectObject (0)
                .CreatePen (Green, 0, 5)
                .SelectObject (1)
                .Rectangle (0, 0, 40, 40)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Lime, PixelAt (metafile, 20, 20));
            Assert.Equal (0, PixelAt (metafile, 80, 80).Alpha);
        }

        [Fact]
        public void A_wmf_object_table_is_indexed_in_creation_order ()
        {
            // WMF selects objects by table index, not by a handle the record names. Creating two
            // brushes and selecting the second must pick the second.
            var bytes = new WmfBuilder ()
                .SetWindowOrg (0, 0)
                .SetWindowExt (100, 100)
                .CreateBrush (Red)
                .CreateBrush (Blue)
                .CreatePen (Red, 0, 5)
                .SelectObject (2)                // the pen
                .SelectObject (1)                // the second brush
                .Rectangle (0, 0, 100, 100)
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Blue, PixelAt (metafile, 50, 50));
        }

        [Fact]
        public void A_wmf_polygon_is_filled ()
        {
            var bytes = new WmfBuilder ()
                .SetWindowOrg (0, 0)
                .SetWindowExt (100, 100)
                .CreateBrush (Blue)
                .SelectObject (0)
                .CreatePen (Blue, 0, 5)
                .SelectObject (1)
                .Polygon ((0, 0), (100, 0), (100, 100), (0, 100))
                .Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (SKColors.Blue, PixelAt (metafile, 50, 50));
        }

        [Fact]
        public void A_wmf_reports_itself_as_a_windows_metafile ()
        {
            var bytes = new WmfBuilder ().SetWindowExt (100, 100).Build (0, 0, 100, 100);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.True (metafile.GetMetafileHeader ().IsWmfPlaceable ());
            Assert.Same (ImageFormat.Wmf, metafile.RawFormat);
        }

        // ── Rasterisation ───────────────────────────────────────────────────────

        [Fact]
        public void A_metafile_re_renders_when_drawn_at_a_larger_size ()
        {
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .SelectStock (8)
                .Rectangle (0, 0, 10, 10)
                .Build (0, 0, 10, 10);

            using var metafile = new Metafile (new MemoryStream (bytes));

            Assert.Equal (10, metafile.Width);

            // Being vector data, asking for a bigger size must re-render rather than enlarge the
            // pixels of the first rasterisation.
            metafile.PrepareForDraw (200, 200);

            Assert.Equal (200, metafile.Width);
            Assert.Equal (SKColors.Red, PixelAt (metafile, 100, 100));
        }

        [Fact]
        public void A_metafile_reports_how_many_records_it_read ()
        {
            var bytes = new EmfBuilder ()
                .CreateBrush (1, Red)
                .SelectObject (1)
                .Rectangle (0, 0, 10, 10)
                .Build (0, 0, 10, 10);

            using var metafile = new Metafile (new MemoryStream (bytes));

            // Three records plus the header and EOF the builder always emits.
            Assert.Equal (5, metafile.RecordCount);
            Assert.Equal (0, metafile.UnsupportedRecordCount);
        }
    }
}
