using System;
using System.Drawing;
using System.IO;
using System.Text;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;
using Xunit;
using MFD = Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.Tests;

// The drawing-layer findings of #343 that are not about state or colour: text measurement and
// drawing (GFX-14, -19, -20, -28, -37), image placement (GFX-10, -21, -22), paths and regions
// (GFX-41, -42, -45), ImageAttributes (GFX-43) and icons (GFX-33, -34, -35).
[Collection ("Headless")]
public class DrawingTextAndImageParityTests
{
    private static MFD.Font NewFont (float points = 10f) => new ("Arial", points);

    private static (int Left, int Right, int Top, int Bottom) Ink (MFD.Bitmap bitmap)
    {
        int left = int.MaxValue, right = -1, top = int.MaxValue, bottom = -1;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++) {
                var p = bitmap.GetPixel (x, y);
                if (p.A > 0 && p.R < 160 && p.G < 160 && p.B < 160) {
                    left = Math.Min (left, x);
                    right = Math.Max (right, x);
                    top = Math.Min (top, y);
                    bottom = Math.Max (bottom, y);
                }
            }
        return (left, right, top, bottom);
    }

    // --- GFX-14 ---

    [Fact]
    public void MeasureString_reports_only_the_characters_that_fit ()
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        using var font = NewFont ();
        using var bitmap = new MFD.Bitmap (10, 10);
        using var g = MFD.Graphics.FromImage (bitmap);

        var text = string.Join (" ", Words (40));
        var lineHeight = g.MeasureString ("X", font).Height;
        var area = new SizeF (100, lineHeight * 2.5f);

        g.MeasureString (text, font, area, null, out var fitted, out var lines);

        // A partly visible third line counts, as GDI+ draws it clipped.
        Assert.Equal (3, lines);
        Assert.InRange (fitted, 1, text.Length - 1);

        // LineLimit takes whole lines only.
        using var limit = new MFD.StringFormat (MFD.StringFormatFlags.LineLimit);
        g.MeasureString (text, font, area, limit, out var fittedWhole, out var linesWhole);
        Assert.Equal (2, linesWhole);
        Assert.True (fittedWhole < fitted);
    }

    private static string[] Words (int count)
    {
        var words = new string[count];
        for (var i = 0; i < count; i++)
            words[i] = "word" + i;
        return words;
    }

    [Fact]
    public void A_pagination_loop_driven_by_charactersFitted_prints_everything_once ()
    {
        // The idiom the overload exists for. With charactersFitted == text.Length the loop ended after
        // one page and dropped the rest.
        using var font = NewFont ();
        using var bitmap = new MFD.Bitmap (10, 10);
        using var g = MFD.Graphics.FromImage (bitmap);

        var text = string.Join (" ", Words (60)) + "\nlast paragraph";
        var lineHeight = g.MeasureString ("X", font).Height;
        using var format = new MFD.StringFormat (MFD.StringFormatFlags.LineLimit);

        var printed = new StringBuilder ();
        var pages = 0;
        for (var rest = text; rest.Length > 0 && pages < 100; pages++) {
            g.MeasureString (rest, font, new SizeF (120, lineHeight * 3), format, out var fitted, out _);
            Assert.True (fitted > 0, "every page must make progress");
            printed.Append (rest, 0, fitted);
            rest = rest.Substring (fitted);
        }

        Assert.True (pages > 1, "the text should need several pages");
        Assert.Equal (text, printed.ToString ());
    }

    // --- GFX-19 ---

    [Theory]
    [InlineData (MFD.StringAlignment.Near)]
    [InlineData (MFD.StringAlignment.Center)]
    [InlineData (MFD.StringAlignment.Far)]
    public void DrawString_at_a_point_anchors_by_the_formats_alignment (MFD.StringAlignment alignment)
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        using var font = NewFont (14f);
        using var bitmap = new MFD.Bitmap (300, 80);
        using var g = MFD.Graphics.FromImage (bitmap);
        using var brush = new MFD.SolidBrush (Color.Black);
        using var format = new MFD.StringFormat { Alignment = alignment, LineAlignment = alignment };
        // Near is a guard (the old code drew top-left at the point too); Center and Far are the proof.

        g.Clear (Color.White);
        g.DrawString ("WWWW", font, brush, new PointF (150, 40), format);

        var ink = Ink (bitmap);
        var width = g.MeasureString ("WWWW", font).Width;

        // The ink's centre sits where the alignment puts the run relative to the anchor, plus GDI+'s
        // sixth-of-an-em padding on the anchor's side (right of a near anchor, left of a far one). Leaving
        // the padding out used 3px of the 4px tolerance, and Linux's glyph metrics took the rest.
        var factor = alignment == MFD.StringAlignment.Near ? 0f : alignment == MFD.StringAlignment.Center ? 0.5f : 1f;
        var pad = 14f * 96f / 72f / 6f;
        var expected = 150 + width * (0.5f - factor) + pad * (1 - 2 * factor);
        Assert.InRange ((ink.Left + ink.Right) / 2f, expected - 4, expected + 4);
        if (alignment == MFD.StringAlignment.Far)
            Assert.True (ink.Bottom <= 41, "LineAlignment.Far puts the text above the anchor");
    }

    [Theory]
    [InlineData (MFD.StringAlignment.Near, 1)]
    [InlineData (MFD.StringAlignment.Center, 0)]
    [InlineData (MFD.StringAlignment.Far, -1)]
    public void DrawString_at_a_point_keeps_the_leading_padding_on_the_anchors_side (MFD.StringAlignment alignment, int direction)
    {
        // GDI+ pads every non-typographic layout by a sixth of an em on each side, so a near-anchored run
        // starts that far right of the point, a far-anchored one ends that far left of it, and a centred
        // one does not move. GenericTypographic has no padding, which makes it the reference.
        // Center is the guard (zero either way); Near and Far are the proof.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        using var font = NewFont (14f);
        using var brush = new MFD.SolidBrush (Color.Black);

        int InkLeft (MFD.StringFormat format)
        {
            using var bitmap = new MFD.Bitmap (300, 80);
            using var g = MFD.Graphics.FromImage (bitmap);
            g.Clear (Color.White);
            format.Alignment = alignment;
            g.DrawString ("WWWW", font, brush, new PointF (150, 20), format);

            return Ink (bitmap).Left;
        }

        using var plain = new MFD.StringFormat ();
        using var typographic = MFD.StringFormat.GenericTypographic;
        var shift = InkLeft (plain) - InkLeft (typographic);
        var pad = 14f * 96f / 72f / 6f;

        Assert.InRange (shift, direction * pad - 1.5f, direction * pad + 1.5f);
    }

    [Fact]
    public void DrawString_at_a_point_strips_the_hotkey_prefix ()
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        using var font = NewFont (14f);
        using var brush = new MFD.SolidBrush (Color.Black);

        int Width (string text, MFD.Text.HotkeyPrefix prefix)
        {
            using var bitmap = new MFD.Bitmap (300, 60);
            using var g = MFD.Graphics.FromImage (bitmap);
            using var format = new MFD.StringFormat { HotkeyPrefix = prefix };
            g.Clear (Color.White);
            g.DrawString (text, font, brush, new PointF (10, 10), format);
            var ink = Ink (bitmap);
            return ink.Right - ink.Left;
        }

        Assert.Equal (Width ("Open", MFD.Text.HotkeyPrefix.None), Width ("&Open", MFD.Text.HotkeyPrefix.Hide));
    }

    // --- GFX-20 ---

    [Fact]
    public void MeasureString_with_NoWrap_measures_one_line ()
    {
        using var font = NewFont ();
        using var bitmap = new MFD.Bitmap (10, 10);
        using var g = MFD.Graphics.FromImage (bitmap);
        using var noWrap = new MFD.StringFormat (MFD.StringFormatFlags.NoWrap);

        var single = g.MeasureString ("aaa bbb ccc", font);
        var wrapped = g.MeasureString ("aaa bbb ccc", font, new SizeF (20, 100), new MFD.StringFormat ());
        var unwrapped = g.MeasureString ("aaa bbb ccc", font, new SizeF (20, 100), noWrap);
        var unwrappedByWidth = g.MeasureString ("aaa bbb ccc", font, 20, noWrap);

        Assert.True (wrapped.Height > single.Height, "without NoWrap the narrow box wraps");
        Assert.Equal (single.Height, unwrapped.Height);
        Assert.Equal (single.Width, unwrapped.Width);
        Assert.Equal (single.Height, unwrappedByWidth.Height);
    }

    // --- GFX-37 ---

    [Fact]
    public void GenericTypographic_carries_upstreams_flags ()
        => Assert.Equal (MFD.StringFormatFlags.LineLimit | MFD.StringFormatFlags.NoClip | MFD.StringFormatFlags.FitBlackBox,
            MFD.StringFormat.GenericTypographic.FormatFlags);

    // --- GFX-28 ---

    [Fact]
    public void PathEllipsis_keeps_the_file_name ()
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        using var font = NewFont ();
        using var bitmap = new MFD.Bitmap (10, 10);
        using var g = MFD.Graphics.FromImage (bitmap);

        const string Path = @"C:\Users\someone\Documents\Projects\Quarterly\report.txt";
        var width = (int)g.MeasureString (@"C:\Users...\report.txt", font).Width + 2;

        var shortened = TextRenderer.PathEllipsize (g, Path, font, width);

        Assert.EndsWith (@"\report.txt", shortened);
        Assert.Contains ("...", shortened);
        Assert.StartsWith (@"C:\", shortened);
        Assert.True (g.MeasureString (shortened, font).Width <= width);
        Assert.Same (Path, TextRenderer.PathEllipsize (g, Path, font, 10_000));
    }

    [Fact]
    public void DrawText_with_PathEllipsis_draws_the_file_name ()
    {
        // Rendered, so the flag is known to reach the pass: the end of a path-ellipsised run is the file
        // name's last glyphs, which sit at the right of the ink, while end truncation (what the flag fell
        // through to) ends in dots. Compared against drawing the expected shortened text directly.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        using var font = NewFont (12f);
        const string Path = @"C:\Users\someone\Documents\Projects\Quarterly\report.txt";

        MFD.Bitmap Render (string text, TextFormatFlags flags, int width)
        {
            var bitmap = new MFD.Bitmap (400, 40);
            using var g = MFD.Graphics.FromImage (bitmap);
            g.Clear (Color.White);
            TextRenderer.DrawText (g, text, font, new Rectangle (0, 0, width, 40), Color.Black, Color.Empty, flags | TextFormatFlags.NoPrefix);
            return bitmap;
        }

        using var probe = new MFD.Bitmap (10, 10);
        using var pg = MFD.Graphics.FromImage (probe);
        var width = (int)pg.MeasureString (@"C:\Users\...\report.txt", font).Width + 2;
        var expected = TextRenderer.PathEllipsize (pg, Path, font, width);

        using var drawn = Render (Path, TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine, width);
        using var reference = Render (expected, TextFormatFlags.SingleLine, width);

        for (var y = 0; y < 40; y++)
            for (var x = 0; x < 400; x++)
                Assert.Equal (reference.GetPixel (x, y).ToArgb (), drawn.GetPixel (x, y).ToArgb ());
    }

    // --- GFX-10 ---

    [Fact]
    public void A_bitmaps_resolution_is_the_graphics_dpi ()
    {
        using var bitmap = new MFD.Bitmap (10, 10);
        bitmap.SetResolution (300, 200);
        using var g = MFD.Graphics.FromImage (bitmap);

        Assert.Equal (300f, g.DpiX);
        Assert.Equal (200f, g.DpiY);

        using var plain = new MFD.Bitmap (10, 10);
        using var screen = MFD.Graphics.FromImage (plain);
        Assert.Equal (96f, screen.DpiX);
    }

    [Fact]
    public void A_page_inch_is_the_surfaces_inch ()
    {
        // PageUnit converted through a fixed 96, so on a 300-DPI bitmap an "inch" was a third of one.
        using var bitmap = new MFD.Bitmap (400, 10);
        bitmap.SetResolution (300, 300);
        using var g = MFD.Graphics.FromImage (bitmap);
        using var red = new MFD.SolidBrush (Color.Red);

        g.Clear (Color.White);
        g.PageUnit = MFD.GraphicsUnit.Inch;
        g.FillRectangle (red, 0, 0, 1, 0.02f);

        Assert.Equal (Color.Red.ToArgb (), bitmap.GetPixel (298, 2).ToArgb ());
        Assert.Equal (Color.White.ToArgb (), bitmap.GetPixel (302, 2).ToArgb ());
    }

    // --- GFX-21 / GFX-22 ---

    private static MFD.Bitmap Solid (int w, int h, Color c)
    {
        var b = new MFD.Bitmap (w, h);
        using var g = MFD.Graphics.FromImage (b);
        g.Clear (c);
        return b;
    }

    [Fact]
    public void A_fractional_destination_is_not_rounded ()
    {
        // Half a pixel to the right: with rounding the image lands whole on pixel 0 (or 1); unrounded and
        // bilinear, pixels 0 and 1 are both partly covered.
        using var white = Solid (1, 1, Color.White);
        using var target = Solid (4, 1, Color.Black);
        using var g = MFD.Graphics.FromImage (target);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage (white, new RectangleF (0.5f, 0, 1, 1), new RectangleF (0, 0, 1, 1), MFD.GraphicsUnit.Pixel);

        var p0 = target.GetPixel (0, 0).R;
        var p1 = target.GetPixel (1, 0).R;
        Assert.InRange (p0, 1, 254);
        Assert.InRange (p1, 1, 254);
    }

    [Fact]
    public void The_source_rectangle_is_in_its_unit ()
    {
        // A 96-DPI image: a one-inch source rectangle is the whole 96x96 image, not its top-left pixel.
        using var source = new MFD.Bitmap (96, 96);
        using (var sg = MFD.Graphics.FromImage (source)) {
            using var red = new MFD.SolidBrush (Color.Red);
            sg.Clear (Color.Blue);
            sg.FillRectangle (red, 0, 0, 1, 1);
        }

        using var target = new MFD.Bitmap (10, 10);
        using var g = MFD.Graphics.FromImage (target);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage (source, new Rectangle (0, 0, 10, 10), new Rectangle (0, 0, 1, 1), MFD.GraphicsUnit.Inch);

        Assert.Equal (Color.Blue.ToArgb (), target.GetPixel (9, 9).ToArgb ());
    }

    // --- GFX-41 / GFX-42 / GFX-45 ---

    private static GraphicsPath Donut (FillMode mode)
    {
        var path = new GraphicsPath (mode);
        path.AddRectangle (new Rectangle (0, 0, 100, 100));
        path.AddRectangle (new Rectangle (25, 25, 50, 50));
        return path;
    }

    [Fact]
    public void An_alternate_path_has_a_hole_when_hit_tested_and_as_a_region ()
    {
        using var alternate = Donut (FillMode.Alternate);
        using var winding = Donut (FillMode.Winding);

        Assert.False (alternate.IsVisible (50, 50));
        Assert.True (alternate.IsVisible (10, 10));
        Assert.True (winding.IsVisible (50, 50));

        using var region = new MFD.Region (alternate);
        Assert.False (region.IsVisible (50, 50));
        Assert.True (region.IsVisible (10, 10));
    }

    [Fact]
    public void GetBounds_is_the_curves_not_its_control_points ()
    {
        using var path = new GraphicsPath ();
        path.AddBezier (0, 0, 50, 200, 100, 200, 150, 0);

        var bounds = path.GetBounds ();

        // The control points reach y=200; a cubic through them peaks at 150.
        Assert.InRange (bounds.Bottom, 149f, 151f);
        Assert.Equal (150f, bounds.Width, 1f);
    }

    [Fact]
    public void AddPath_with_connect_joins_the_figures ()
    {
        using var first = new GraphicsPath ();
        first.AddLine (0, 0, 10, 0);
        using var second = new GraphicsPath ();
        second.AddLine (20, 0, 30, 0);

        using var connected = (GraphicsPath)first.Clone ();
        connected.AddPath (second, connect: true);
        using var separate = (GraphicsPath)first.Clone ();
        separate.AddPath (second, connect: false);

        static int Starts (GraphicsPath p)
        {
            var n = 0;
            foreach (var t in p.PathTypes)
                if ((t & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Start)
                    n++;
            return n;
        }

        Assert.Equal (1, Starts (connected));
        Assert.Equal (2, Starts (separate));
    }

    [Fact]
    public void CloseAllFigures_closes_every_figure ()
    {
        using var path = new GraphicsPath ();
        path.AddLines ([new PointF (0, 0), new PointF (10, 0), new PointF (10, 10)]);
        path.StartFigure ();
        path.AddLines ([new PointF (20, 0), new PointF (30, 0), new PointF (30, 10)]);

        path.CloseAllFigures ();

        var closes = 0;
        foreach (var t in path.PathTypes)
            if ((t & (byte)PathPointType.CloseSubpath) != 0)
                closes++;

        Assert.Equal (2, closes);
    }

    [Fact]
    public void Transforming_an_infinite_region_leaves_it_infinite ()
    {
        using var region = new MFD.Region ();
        using var matrix = new Matrix ();
        matrix.Translate (5, 5);

        region.Transform (matrix);

        Assert.True (region.IsInfinite ());
    }

    [Fact]
    public void Transforming_a_region_moves_it ()
    {
        // A guard on the bounded-clip change: the clip must not shave the transformed shape.
        using var region = new MFD.Region (new Rectangle (0, 0, 10, 10));
        using var matrix = new Matrix ();
        matrix.Translate (100, 50);

        region.Transform (matrix);

        Assert.Equal (new RectangleF (100, 50, 10, 10), region.GetBounds ());
    }

    // --- GFX-43 ---

    [Fact]
    public void SetNoOp_draws_the_image_unadjusted ()
    {
        using var red = Solid (4, 4, Color.Red);
        using var attributes = new ImageAttributes ();
        attributes.SetColorMatrix (new ColorMatrix { Matrix33 = 0f });   // fully transparent
        attributes.SetNoOp ();

        using var target = Solid (4, 4, Color.White);
        using var g = MFD.Graphics.FromImage (target);
        g.DrawImage (red, new Rectangle (0, 0, 4, 4), 0, 0, 4, 4, MFD.GraphicsUnit.Pixel, attributes);

        Assert.Equal (Color.Red.ToArgb (), target.GetPixel (2, 2).ToArgb ());

        // And the clone keeps the suspension.
        using var clone = (ImageAttributes)attributes.Clone ();
        Assert.True (clone.NoOp);
    }

    [Fact]
    public void SetThreshold_binarises_each_channel ()
    {
        using var source = Solid (2, 2, Color.FromArgb (200, 100, 30));
        using var attributes = new ImageAttributes ();
        attributes.SetThreshold (0.5f);

        using var target = Solid (2, 2, Color.White);
        using var g = MFD.Graphics.FromImage (target);
        g.DrawImage (source, new Rectangle (0, 0, 2, 2), 0, 0, 2, 2, MFD.GraphicsUnit.Pixel, attributes);

        Assert.Equal (Color.FromArgb (255, 0, 0).ToArgb (), target.GetPixel (1, 1).ToArgb ());
        Assert.Equal (0.5f, ((ImageAttributes)attributes.Clone ()).Threshold);
    }

    // --- GFX-33 / GFX-34 / GFX-35 ---

    private static byte[] Png (int size, SKColor color)
    {
        using var bitmap = new SKBitmap (size, size);
        bitmap.Erase (color);
        using var image = SKImage.FromBitmap (bitmap);
        using var data = image.Encode (SKEncodedImageFormat.Png, 100);
        return data.ToArray ();
    }

    // A two-frame ICO, 16x16 red and 32x32 blue, PNG-compressed.
    private static byte[] TwoFrameIco ()
    {
        var small = Png (16, SKColors.Red);
        var large = Png (32, SKColors.Blue);
        using var ms = new MemoryStream ();
        using var w = new BinaryWriter (ms);
        w.Write ((short)0); w.Write ((short)1); w.Write ((short)2);
        var offset = 6 + 16 * 2;
        foreach (var (size, png) in new[] { (16, small), (32, large) }) {
            w.Write ((byte)size); w.Write ((byte)size); w.Write ((byte)0); w.Write ((byte)0);
            w.Write ((short)1); w.Write ((short)32); w.Write (png.Length); w.Write (offset);
            offset += png.Length;
        }
        w.Write (small);
        w.Write (large);
        return ms.ToArray ();
    }

    [Theory]
    [InlineData (16, unchecked((int)0xFFFF0000))]
    [InlineData (32, unchecked((int)0xFF0000FF))]
    public void Icon_at_a_size_uses_that_embedded_frame (int size, int argb)
    {
        // The 16 case is the proof: the decoder picks the largest frame, which the old code rescaled. The
        // 32 case is that frame itself, so it is a guard.
        using var ico = new MFD.Icon (new MemoryStream (TwoFrameIco ()));
        using var sized = new MFD.Icon (ico, size, size);
        using var bitmap = sized.ToBitmap ();

        Assert.Equal (new Size (size, size), sized.Size);
        Assert.Equal (argb, bitmap.GetPixel (size / 2, size / 2).ToArgb ());
    }

    [Fact]
    public void Icon_Save_writes_the_ico_it_was_loaded_from ()
    {
        var data = TwoFrameIco ();
        using var icon = new MFD.Icon (new MemoryStream (data));
        using var saved = new MemoryStream ();

        icon.Save (saved);

        Assert.Equal (data, saved.ToArray ());
    }

    [Fact]
    public void Icon_Save_of_a_built_icon_writes_an_ico_container ()
    {
        var icon = MFD.SystemIcons.Application;
        using var saved = new MemoryStream ();
        icon.Save (saved);

        var bytes = saved.ToArray ();
        Assert.Equal (new byte[] { 0, 0, 1, 0 }, bytes[0..4]);

        saved.Position = 0;
        using var reloaded = new MFD.Icon (saved);
        Assert.Equal (icon.Size, reloaded.Size);
    }

    [Fact]
    public void A_missing_icon_file_throws ()
        => Assert.Throws<FileNotFoundException> (() => new MFD.Icon (Path.Combine (Path.GetTempPath (), Guid.NewGuid () + ".ico")));

    [Fact]
    public void Undecodable_icon_data_throws ()
        => Assert.Throws<ArgumentException> (() => new MFD.Icon (new MemoryStream ([1, 2, 3, 4])));

    // --- GFX-18 (already fixed; guard) ---

    [Fact]
    public void VisibleClipBounds_is_the_clip_and_an_empty_clip_reports_empty ()
    {
        // A guard: VisibleClipBounds aliases ClipBounds, which reads the canvas clip -- the same clip
        // PaintEventArgs.ClipRectangle is derived from -- and IsVisibleClipEmpty became real in W6.4.
        using var target = new MFD.Bitmap (100, 100);
        using var g = MFD.Graphics.FromImage (target);

        g.SetClip (new Rectangle (0, 0, 10, 10));
        Assert.Equal (10f, g.VisibleClipBounds.Width);
        Assert.False (g.IsVisibleClipEmpty);

        g.IntersectClip (new Rectangle (50, 50, 5, 5));
        Assert.True (g.IsVisibleClipEmpty);
    }
}
