using System;
using System.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// TextRenderer.DrawText used to accept TextFormatFlags and discard them, always drawing at the
// layout rectangle's top-left. Ported WinForms code that centres a caption inside a rectangle --
// the single most common use of the flags -- silently came out top-left aligned. These tests pin
// the alignment behaviour by measuring where the ink actually lands.
public class TextFormatFlagsTests
{
    private const int Width = 240;
    private const int Height = 80;

    private sealed class TextControl : Control
    {
        public TextFormatFlags Flags { get; init; }

        protected override void OnPaintBackground (PaintEventArgs e) => e.Canvas.Clear (SKColors.White);

        protected override void OnPaint (PaintEventArgs e)
            => TextRenderer.DrawText (e.Graphics, "Ab", Font, new Rectangle (0, 0, Width, Height),
                Color.Black, Color.Empty, Flags);
    }

    // Bounding box of the drawn glyphs. Antialiasing means "not white" is the only reliable ink test.
    private static Rectangle InkBounds (TextFormatFlags flags)
    {
        var control = new TextControl { Flags = flags, Width = Width, Height = Height };
        using var bmp = PaintSurface.Render (control);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (var y = 0; y < bmp.Height; y++) {
            for (var x = 0; x < bmp.Width; x++) {
                var p = bmp.GetPixel (x, y);
                if (p.Red > 240 && p.Green > 240 && p.Blue > 240)
                    continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        Assert.True (maxX >= 0, $"no text was drawn for flags {flags}");
        return Rectangle.FromLTRB (minX, minY, maxX + 1, maxY + 1);
    }

    [Fact]
    public void HorizontalCenter_CentresTextInTheRectangle ()
    {
        var ink = InkBounds (TextFormatFlags.HorizontalCenter);
        var left = ink.Left;
        var right = Width - ink.Right;

        Assert.True (Math.Abs (left - right) <= 2, $"not centred: {left}px left, {right}px right");
        Assert.True (left > 4, "text is still pinned to the left edge");
    }

    [Fact]
    public void Right_AlignsTextToTheRightEdge ()
    {
        var ink = InkBounds (TextFormatFlags.Right);

        Assert.True (Width - ink.Right <= 3, $"right edge is {Width - ink.Right}px short");
        Assert.True (ink.Left > Width / 2, "text did not move right");
    }

    [Fact]
    public void Left_StaysAtTheLeftEdge ()
    {
        var ink = InkBounds (TextFormatFlags.Left);
        Assert.True (ink.Left <= 3, $"left-aligned text started at {ink.Left}px");
    }

    [Fact]
    public void VerticalCenter_CentresTextVertically ()
    {
        var ink = InkBounds (TextFormatFlags.VerticalCenter);
        var top = ink.Top;
        var bottom = Height - ink.Bottom;

        Assert.True (Math.Abs (top - bottom) <= 4, $"not centred: {top}px above, {bottom}px below");
        Assert.True (top > 4, "text is still pinned to the top edge");
    }

    [Fact]
    public void Bottom_AlignsTextToTheBottomEdge ()
    {
        var ink = InkBounds (TextFormatFlags.Bottom);
        Assert.True (ink.Top > Height / 2, $"text did not move down (top at {ink.Top}px)");
    }

    [Fact]
    public void CombinedCenterFlags_CentreOnBothAxes ()
    {
        var ink = InkBounds (TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        Assert.True (Math.Abs (ink.Left - (Width - ink.Right)) <= 2, "not centred horizontally");
        Assert.True (Math.Abs (ink.Top - (Height - ink.Bottom)) <= 4, "not centred vertically");
    }
}
