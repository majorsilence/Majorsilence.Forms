using System;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// TextRenderer.MeasureText is the call WinForms layout is built on -- Label.AutoSize,
// Button.GetPreferredSize, ToolStripItem sizing, column auto-fit, DataGridView cell preferred size --
// and it was measuring at the font's POINT size in a path that wants PIXELS (finding GFX-25). For the
// default 9pt UI font that is 9px instead of 12px: every measured string about a quarter too small in
// both axes.
//
// It went unnoticed because nothing tied the two halves together. DrawText draws through
// Graphics.DrawString, which already used the pixel size, so measure and draw disagreed by a third and
// text was clipped on the right and bottom of anything sized from the measurement. These tests pin the
// relationship rather than either number, so neither half can drift again.
public class TextMeasurementParityTests
{
    private static Majorsilence.Forms.Drawing.Font NewFont (float points = 9f)
        => new ("Arial", points);

    [Fact]
    public void MeasureText_and_MeasureString_agree_on_the_same_string ()
    {
        using var font = NewFont ();
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (200, 80);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);

        const string Text = "Hello world";

        // NoPadding, because the GDI slack TextRenderer adds is the one intended difference between
        // the two (GFX-26) and it is asserted separately.
        var renderer = TextRenderer.MeasureText (
            Text, font, new Size (int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        var graphicsSize = graphics.MeasureString (Text, font);

        // Ceiling on one side and float on the other, so allow a pixel of rounding either way.
        Assert.True (Math.Abs (renderer.Width - graphicsSize.Width) <= 1.5f,
            $"TextRenderer says {renderer.Width}px wide, Graphics says {graphicsSize.Width}px -- "
            + "the two measure the same font and must agree apart from padding");

        Assert.True (Math.Abs (renderer.Height - graphicsSize.Height) <= 1.5f,
            $"TextRenderer says {renderer.Height}px tall, Graphics says {graphicsSize.Height}px");
    }

    [Fact]
    public void A_measured_string_scales_with_the_fonts_point_size ()
    {
        // The direct expression of the bug: measuring at the point size made the result independent of
        // the point-to-pixel conversion, so a 9pt and a 12pt font were measured 9px and 12px apart
        // rather than 12px and 16px. Ratios rather than absolutes, so this says nothing about which
        // font the machine actually resolved.
        using var small = NewFont (9f);
        using var large = NewFont (18f);

        var box = new Size (int.MaxValue, int.MaxValue);
        var smallSize = TextRenderer.MeasureText ("Hello", small, box, TextFormatFlags.NoPadding);
        var largeSize = TextRenderer.MeasureText ("Hello", large, box, TextFormatFlags.NoPadding);

        var ratio = largeSize.Width / (double) smallSize.Width;

        Assert.True (ratio > 1.8 && ratio < 2.2,
            $"doubling the point size should roughly double the measured width; ratio was {ratio:F2}");
    }

    [Fact]
    public void Measured_text_is_wide_enough_for_the_text_that_gets_drawn ()
    {
        // The consequence that reached users: a control sized from MeasureText clipped its own caption,
        // because DrawText renders through the Graphics path at the correct pixel size. Asserted
        // against real ink rather than against another measurement.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = NewFont (12f);
        const string Text = "Wide caption text";

        var measured = TextRenderer.MeasureText (
            Text, font, new Size (int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (measured.Width + 40, measured.Height + 40);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
        using var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.Black);

        graphics.Clear (Color.White);
        graphics.DrawString (Text, font, brush, 0, 0);

        var inkRight = RightmostInk (bitmap);

        Assert.True (inkRight <= measured.Width,
            $"drawn text reaches x={inkRight} but was measured at {measured.Width}px -- anything sized "
            + "from the measurement clips its own text");
    }

    [Fact]
    public void DrawString_into_a_rectangle_wraps_to_its_width ()
    {
        // GDI+ draws this overload with StringFormat.GenericDefault, whose flags do not include
        // NoWrap. This used to clip to the rectangle and draw one unbounded line, so a paragraph, a
        // wrapped cell value or a tooltip body came out as a single clipped line -- while
        // MeasureString wrapped correctly, so an app that measured to size its box then drew into it
        // got a tall empty box with one line at the top (finding GFX-06).
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = NewFont (10f);
        const string Text = "aaa bbb ccc ddd eee fff";

        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (300, 200);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
        using var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.Black);

        graphics.Clear (Color.White);
        graphics.DrawString (Text, font, brush, new RectangleF (0, 0, 60, 200));

        var lineHeight = graphics.MeasureString ("A", font).Height;
        var inkBottom = BottommostInk (bitmap);

        Assert.True (inkBottom > lineHeight * 2,
            $"text drawn into a 60px-wide box reached only y={inkBottom} with a line height of "
            + $"{lineHeight:F0} -- it did not wrap");

        // And it stayed inside the box rather than running off to the right.
        Assert.True (RightmostInk (bitmap) <= 60,
            "wrapped text must stay within the layout rectangle");
    }

    [Fact]
    public void DrawText_wraps_when_MeasureText_says_it_will ()
    {
        // The pair that must agree. MeasureText wraps only for WordBreak (GFX-27); DrawText ignored
        // the flag entirely and drew one line (GFX-28), so a caller who sized a box from the
        // measurement got a tall box with a single line in it -- the same disagreement, one level up,
        // as the DrawString case above.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = NewFont (10f);
        const string Text = "aaa bbb ccc ddd eee fff";
        var box = new Rectangle (0, 0, 60, 200);

        var measured = TextRenderer.MeasureText (
            Text, font, box.Size, TextFormatFlags.WordBreak);

        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (300, 200);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);

        graphics.Clear (Color.White);
        TextRenderer.DrawText (graphics, Text, font, box, Color.Black, Color.Empty, TextFormatFlags.WordBreak);

        var lineHeight = graphics.MeasureString ("A", font).Height;

        Assert.True (measured.Height > lineHeight * 2, "WordBreak should measure several lines");
        Assert.True (BottommostInk (bitmap) > lineHeight * 2,
            "WordBreak should DRAW several lines too, not one");
    }

    [Fact]
    public void DrawText_with_EndEllipsis_stops_short_of_the_edge ()
    {
        // What Label.AutoEllipsis, ToolStripItem, ListView and every truncating cell renderer ask for.
        // The flag was accepted and unread, so text was hard-clipped mid-glyph and a user could not
        // tell truncated text from text that merely ends oddly.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = NewFont (10f);
        const string Text = "a very long caption that will not fit";
        var box = new Rectangle (0, 0, 80, 30);

        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (200, 60);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);

        graphics.Clear (Color.White);
        TextRenderer.DrawText (graphics, Text, font, box, Color.Black, Color.Empty,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        // Ellipsised text ends inside the box; hard-clipped text runs right up to the boundary.
        var inkRight = RightmostInk (bitmap);

        Assert.True (inkRight > 0, "something should have been drawn");
        Assert.True (inkRight <= box.Right,
            $"ink reached x={inkRight}, past the {box.Right}px box");
        Assert.True (BottommostInk (bitmap) <= box.Bottom,
            "SingleLine must not wrap into extra lines");
    }

    [Fact]
    public void A_controls_rendered_text_is_the_size_its_font_asks_for ()
    {
        // Style.FontSize is in PIXELS -- Theme.FontSize is 14 -- but Control.Font's setter assigned it
        // from SizeInPoints, so every control DREW its text about a quarter too small: 9px for the
        // default 9pt font instead of 12px. Same defect as GFX-25 on the measuring side, in the path
        // that actually renders, and the one visible in a running application as tiny captions.
        //
        // Measured against real ink rather than against the style, so it cannot pass by agreeing with
        // the same wrong number twice.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var form = new Form { Size = new Size (300, 120) };
        var label = new Label {
            Text = "Hxy",
            Font = new Majorsilence.Forms.Drawing.Font ("Arial", 24f),
            Location = new Point (10, 10),
            Size = new Size (200, 60),
            AutoSize = false,
        };
        form.Controls.Add (label);
        form.Show ();

        using var bitmap = SkiaSharp.SKBitmap.Decode (Majorsilence.Forms.Headless.HeadlessRenderer.CapturePng (form));

        var top = -1;
        var bottom = -1;

        for (var y = 0; y < bitmap.Height; y++) {
            for (var x = 10; x < 210 && x < bitmap.Width; x++) {
                var pixel = bitmap.GetPixel (x, y);

                if (pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128) {
                    if (top < 0) top = y;
                    bottom = y;
                    break;
                }
            }
        }

        Assert.True (top >= 0, "the label drew no text at all");

        // "Hxy" at 24pt is 32px; cap-height plus descender is roughly two thirds of that. At the old
        // point-sized 24px it would be about a quarter smaller, well under this floor.
        var inkHeight = bottom - top + 1;

        // 24pt is 32px at 96 DPI, and "Hxy" (cap height plus descender) inks 30px of that. Rendered
        // at the point size instead it is 24px and inks 22px, so the threshold sits between the two
        // measured values rather than being picked out of the air.
        Assert.True (inkHeight >= 26,
            $"24pt text drew only {inkHeight}px of ink, against 30px when it is sized correctly -- "
            + "it is being rendered at the point size rather than the pixel size");

        form.Close ();
    }

    [Fact]
    public void A_control_with_no_font_of_its_own_draws_the_same_size_as_one_with_the_default_font ()
    {
        // Almost no control sets a Font, so almost every control took the fallback in
        // Control.GetEffectiveFontSize -- and that fallback was (int) SystemFonts.DefaultFontSize,
        // which is 8.25 POINTS truncated to 8, handed to the renderers as a PIXEL size. The result
        // was that the overwhelmingly common case drew smaller than the rare explicitly-fonted one:
        // 8px of ink against 10px. That is the tiny-text look in a running application, so the
        // assertion is that the two paths agree rather than that either hits some absolute number.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var form = new Form { Size = new Size (420, 220) };
        var inherited = new Label { Text = "Hxy", Location = new Point (10, 10), Size = new Size (300, 30) };
        var explicitly_fonted = new Label {
            Text = "Hxy", Location = new Point (10, 60), Size = new Size (300, 30),
            Font = new Majorsilence.Forms.Drawing.Font ("Arial", 8.25f),
        };

        form.Controls.Add (inherited);
        form.Controls.Add (explicitly_fonted);
        form.Show ();

        using var bitmap = SkiaSharp.SKBitmap.Decode (Majorsilence.Forms.Headless.HeadlessRenderer.CapturePng (form));

        // Each band comes from the label's own device-space rectangle rather than a hard-coded
        // row range: the capture is in device pixels, and off macOS the custom title bar pushes
        // both labels down by its height. That is exactly what WindowPoint exists to get right.
        var inherited_ink = InkHeight (bitmap, DeviceBounds (inherited));
        var explicit_ink = InkHeight (bitmap, DeviceBounds (explicitly_fonted));

        Assert.True (inherited_ink > 0 && explicit_ink > 0,
            $"nothing was drawn: inherited={inherited_ink} explicit={explicit_ink}");

        Assert.True (System.Math.Abs (inherited_ink - explicit_ink) <= System.Math.Ceiling (form.Scaling),
            $"a Label with no Font drew {inherited_ink}px of ink where the same Label with an "
            + $"explicit 8.25pt font drew {explicit_ink}px -- the inherited path is resolving the "
            + "default font's POINT size as a pixel size");

        form.Close ();
    }

    // A control's own rectangle in the device-pixel, window-space coordinates a capture is in.
    private static Rectangle DeviceBounds (Control control)
    {
        var topLeft = WindowPoint.DeviceIn (control, 0, 0);
        var bottomRight = WindowPoint.DeviceIn (control, control.Width, control.Height);

        return Rectangle.FromLTRB (topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    // The height in pixels of the dark ink inside a device-space band, or 0 when it is blank.
    private static int InkHeight (SkiaSharp.SKBitmap bitmap, Rectangle band)
    {
        var top = -1;
        var bottom = -1;

        for (var y = band.Top; y < band.Bottom && y < bitmap.Height; y++) {
            for (var x = band.Left; x < band.Right && x < bitmap.Width; x++) {
                var pixel = bitmap.GetPixel (x, y);

                if (pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128) {
                    if (top < 0)
                        top = y;

                    bottom = y;
                    break;
                }
            }
        }

        return top < 0 ? 0 : bottom - top + 1;
    }

    // The y of the lowest non-white pixel, or -1 when nothing was drawn.
    private static int BottommostInk (Majorsilence.Forms.Drawing.Bitmap bitmap)
    {
        for (var y = bitmap.Height - 1; y >= 0; y--) {
            for (var x = 0; x < bitmap.Width; x++) {
                var pixel = bitmap.GetPixel (x, y);

                if (pixel.R < 200 || pixel.G < 200 || pixel.B < 200)
                    return y;
            }
        }

        return -1;
    }

    // The x of the rightmost non-white pixel, or -1 when nothing was drawn.
    private static int RightmostInk (Majorsilence.Forms.Drawing.Bitmap bitmap)
    {
        for (var x = bitmap.Width - 1; x >= 0; x--) {
            for (var y = 0; y < bitmap.Height; y++) {
                var pixel = bitmap.GetPixel (x, y);

                if (pixel.R < 200 || pixel.G < 200 || pixel.B < 200)
                    return x;
            }
        }

        return -1;
    }
}
