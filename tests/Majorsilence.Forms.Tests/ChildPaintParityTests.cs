using System.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// In WinForms every child control is its own HWND and repaints itself, so a parent may override
// OnPaint and never call base without its children disappearing -- an extremely common idiom in
// custom-control code. Majorsilence.Forms paints children from the parent's surface, so the child
// pass has to live outside OnPaint (in Control.PaintChildren, driven by RaisePaint) where user code
// cannot suppress it. These tests pin that behaviour and the resulting z-order.
public class ChildPaintParityTests
{
    private static readonly SKColor ParentFill = new SKColor (255, 0, 0);

    // Paints itself solid red. CallBase decides whether it also chains to base.OnPaint -- neither
    // choice may affect whether the children show up.
    private sealed class SolidControl : Control
    {
        public bool CallBase { get; init; }

        protected override void OnPaint (PaintEventArgs e)
        {
            if (CallBase)
                base.OnPaint (e);

            e.Canvas.Clear (ParentFill);
        }
    }

    // Fraction of pixels inside the child's bounds that are NOT the parent's fill colour. The rect
    // is inset a little to absorb child border rounding.
    private static double NonParentFillRatio (SKBitmap bmp, Rectangle childBounds)
    {
        var rect = Rectangle.Inflate (childBounds, -4, -4);
        var total = 0;
        var differing = 0;

        for (var y = rect.Top; y < rect.Bottom; y++) {
            for (var x = rect.Left; x < rect.Right; x++) {
                if (x < 0 || y < 0 || x >= bmp.Width || y >= bmp.Height)
                    continue;

                total++;
                if (bmp.GetPixel (x, y) != ParentFill)
                    differing++;
            }
        }

        Assert.True (total > 0, "sampled an empty region");
        return (double)differing / total;
    }

    [Theory]
    [InlineData (true)]
    [InlineData (false)]
    public void ChildIsPainted_WhetherOrNotOverrideChainsToBase (bool callBase)
    {
        var parent = new SolidControl { CallBase = callBase, Width = 200, Height = 120 };
        var child = new Button { Text = "Child", Left = 30, Top = 30, Width = 120, Height = 50 };
        parent.Controls.Add (child);

        using var bmp = PaintSurface.RenderOnForm (parent);

        var bounds = new Rectangle (child.Left, child.Top, child.Width, child.Height);

        // The button is grey-on-white with text; essentially none of it should be the parent's red.
        Assert.True (NonParentFillRatio (bmp, bounds) > 0.9,
            $"child was overpainted by its parent (callBase: {callBase})");
    }

    [Fact]
    public void ChildPaintsAboveParentsOwnDrawing ()
    {
        // The parent chains to base and THEN fills red over everything. In WinForms the child HWND
        // still occludes the parent's client-area painting, so the child must survive.
        var parent = new SolidControl { CallBase = true, Width = 220, Height = 140 };
        var child = new Button { Text = "On top", Left = 40, Top = 40, Width = 120, Height = 60 };
        parent.Controls.Add (child);

        using var bmp = PaintSurface.RenderOnForm (parent);

        var bounds = new Rectangle (child.Left, child.Top, child.Width, child.Height);

        Assert.True (NonParentFillRatio (bmp, bounds) > 0.9, "parent's own drawing covered the child");
    }

    [Fact]
    public void ParentWithoutChildren_StillPaintsItself ()
    {
        var parent = new SolidControl { CallBase = true, Width = 60, Height = 40 };

        using var bmp = PaintSurface.RenderOnForm (parent);

        Assert.Equal (ParentFill, bmp.GetPixel (30, 20));
    }
}
