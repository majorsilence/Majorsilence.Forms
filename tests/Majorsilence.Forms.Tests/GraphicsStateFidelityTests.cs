using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Text;
using Xunit;
using MFD = Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.Tests;

// GFX-08: InterpolationMode and TextRenderingHint were stored and never read.
// GFX-16: Save/Restore and BeginContainer/EndContainer carried only Skia's matrix and clip, so the
// standard "save, set a quality mode, draw, restore" block leaked the mode.
// GFX-17: RenderingOrigin was stored and never read, so a hatch drawn in pieces restarted its pattern.
[Collection ("Headless")]
public class GraphicsStateFidelityTests
{
    // A 2x2 black-and-white checkerboard: scaling it up is where the resampling filter shows.
    private static MFD.Bitmap Checkerboard ()
    {
        var bitmap = new MFD.Bitmap (2, 2);
        bitmap.SetPixel (0, 0, Color.Black);
        bitmap.SetPixel (1, 1, Color.Black);
        bitmap.SetPixel (1, 0, Color.White);
        bitmap.SetPixel (0, 1, Color.White);
        return bitmap;
    }

    private static HashSet<int> Colours (MFD.Bitmap bitmap)
    {
        var colours = new HashSet<int> ();
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                colours.Add (bitmap.GetPixel (x, y).ToArgb ());
        return colours;
    }

    private static HashSet<int> ScaleCheckerboard (InterpolationMode mode)
    {
        using var source = Checkerboard ();
        using var target = new MFD.Bitmap (16, 16);
        using var g = MFD.Graphics.FromImage (target);

        g.InterpolationMode = mode;
        g.DrawImage (source, new Rectangle (0, 0, 16, 16));

        return Colours (target);
    }

    [Fact]
    public void NearestNeighbor_keeps_only_the_source_colours ()
    {
        // A guard rather than proof: Skia's default sampling is itself nearest, so the old code passed
        // this by accident. It pins the half of the mapping the next test cannot.
        Assert.Equal (2, ScaleCheckerboard (InterpolationMode.NearestNeighbor).Count);
    }

    [Theory]
    [InlineData (InterpolationMode.Default)]
    [InlineData (InterpolationMode.Bilinear)]
    [InlineData (InterpolationMode.HighQualityBilinear)]
    [InlineData (InterpolationMode.HighQualityBicubic)]
    public void A_filtering_mode_blends_between_source_pixels (InterpolationMode mode)
    {
        // GDI+'s Default is bilinear. The property was never read, so every mode drew nearest-neighbour:
        // a scaled photo came out blocky whatever the caller asked for.
        Assert.True (ScaleCheckerboard (mode).Count > 2, $"{mode} should produce intermediate shades");
    }

    private static HashSet<int> DrawTextWith (TextRenderingHint hint)
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = new MFD.Font ("Arial", 14f);
        using var target = new MFD.Bitmap (120, 40);
        using var g = MFD.Graphics.FromImage (target);
        using var brush = new MFD.SolidBrush (Color.Black);

        g.Clear (Color.White);
        g.TextRenderingHint = hint;
        g.DrawString ("Wave", font, brush, 4, 4);

        return Colours (target);
    }

    [Theory]
    [InlineData (TextRenderingHint.SingleBitPerPixel)]
    [InlineData (TextRenderingHint.SingleBitPerPixelGridFit)]
    public void SingleBitPerPixel_text_is_aliased (TextRenderingHint hint)
    {
        // Aliased glyphs are ink or paper and nothing between. The hint was ignored, so this text came
        // out antialiased with dozens of edge shades.
        Assert.Equal (2, DrawTextWith (hint).Count);
    }

    [Fact]
    public void AntiAlias_text_has_edge_shades ()
        => Assert.True (DrawTextWith (TextRenderingHint.AntiAlias).Count > 2);

    [Fact]
    public void The_hint_reaches_gradient_brushed_text_too ()
    {
        // The non-solid brush path draws through a bare SKFont rather than RichTextKit.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = new MFD.Font ("Arial", 14f);
        using var target = new MFD.Bitmap (120, 40);
        using var g = MFD.Graphics.FromImage (target);
        using var brush = new MFD.Drawing2D.HatchBrush (HatchStyle.Percent50, Color.Black, Color.Black);

        g.Clear (Color.White);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        g.DrawString ("Wave", font, brush, 4, 4);

        Assert.Equal (2, Colours (target).Count);
    }

    [Fact]
    public void Restore_puts_every_quality_setting_back ()
    {
        using var target = new MFD.Bitmap (4, 4);
        using var g = MFD.Graphics.FromImage (target);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.RenderingOrigin = new Point (3, 4);
        g.TextContrast = 7;

        var state = g.Save ();

        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.RenderingOrigin = Point.Empty;
        g.TextContrast = 0;

        g.Restore (state);

        Assert.Equal (SmoothingMode.AntiAlias, g.SmoothingMode);
        Assert.Equal (InterpolationMode.NearestNeighbor, g.InterpolationMode);
        Assert.Equal (TextRenderingHint.SingleBitPerPixel, g.TextRenderingHint);
        Assert.Equal (CompositingMode.SourceCopy, g.CompositingMode);
        Assert.Equal (CompositingQuality.HighQuality, g.CompositingQuality);
        Assert.Equal (PixelOffsetMode.Half, g.PixelOffsetMode);
        Assert.Equal (new Point (3, 4), g.RenderingOrigin);
        Assert.Equal (7, g.TextContrast);
    }

    [Fact]
    public void EndContainer_puts_the_quality_settings_back ()
    {
        using var target = new MFD.Bitmap (4, 4);
        using var g = MFD.Graphics.FromImage (target);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var container = g.BeginContainer ();
        g.SmoothingMode = SmoothingMode.None;
        g.EndContainer (container);

        Assert.Equal (SmoothingMode.AntiAlias, g.SmoothingMode);
    }

    [Fact]
    public void Restore_puts_the_page_unit_back_without_scaling_twice ()
    {
        // The page transform is a scale on the canvas, which the restore itself undoes; the unit has to
        // come back with it, and a later change must not stack on a stale record of the applied scale.
        using var target = new MFD.Bitmap (200, 200);
        using var g = MFD.Graphics.FromImage (target);
        using var red = new MFD.SolidBrush (Color.Red);

        var state = g.Save ();
        g.PageUnit = MFD.GraphicsUnit.Inch;
        g.Restore (state);

        Assert.Equal (MFD.GraphicsUnit.Pixel, g.PageUnit);

        g.Clear (Color.White);
        g.FillRectangle (red, 0, 0, 10, 10);

        Assert.Equal (Color.Red.ToArgb (), target.GetPixel (9, 9).ToArgb ());
        Assert.Equal (Color.White.ToArgb (), target.GetPixel (11, 11).ToArgb ());
    }

    [Fact]
    public void Restore_puts_a_non_rectangular_clip_back_as_a_shape ()
    {
        // The canvas frame restores Skia's clip, but the region Clip reports is tracked alongside it; it
        // came back as whatever the last SetClip left, here the replacing rectangle.
        using var target = new MFD.Bitmap (100, 100);
        using var g = MFD.Graphics.FromImage (target);
        using var ellipse = new GraphicsPath ();
        ellipse.AddEllipse (0, 0, 100, 100);

        g.SetClip (ellipse);
        var state = g.Save ();
        g.SetClip (new Rectangle (0, 0, 10, 10));
        g.Restore (state);

        using var clip = g.Clip;
        Assert.False (clip.IsVisible (2, 2), "the ellipse's corner is outside it");
        Assert.True (clip.IsVisible (50, 50));
    }

    [Fact]
    public void RenderingOrigin_phases_a_hatch_brush ()
    {
        // Two strips filled with the same hatch: moving the rendering origin by one pixel moves the
        // pattern with it. It was stored and never read, so both strips came out identical.
        using var hatch = new MFD.Drawing2D.HatchBrush (HatchStyle.Vertical, Color.Black, Color.White);

        int[] Row (Point origin)
        {
            using var target = new MFD.Bitmap (16, 4);
            using var g = MFD.Graphics.FromImage (target);
            g.RenderingOrigin = origin;
            g.FillRectangle (hatch, 0, 0, 16, 4);

            var row = new int[16];
            for (var x = 0; x < 16; x++)
                row[x] = target.GetPixel (x, 2).ToArgb ();
            return row;
        }

        var unshifted = Row (Point.Empty);
        var shifted = Row (new Point (1, 0));

        Assert.NotEqual (unshifted, shifted);
        Assert.Equal (unshifted[0..15], shifted[1..16]);
    }

    [Fact]
    public void SourceCopy_replaces_destination_pixels_when_drawing_an_image ()
    {
        // CompositingMode reached every shape paint but not images, so a translucent image stamped with
        // SourceCopy still blended over what was there.
        using var stamp = new MFD.Bitmap (4, 4);
        stamp.SetPixel (0, 0, Color.FromArgb (0, 0, 0, 0));

        using var target = new MFD.Bitmap (4, 4);
        using var g = MFD.Graphics.FromImage (target);

        g.Clear (Color.Red);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage (stamp, 0, 0);

        Assert.Equal (0, target.GetPixel (0, 0).A);
    }
}
