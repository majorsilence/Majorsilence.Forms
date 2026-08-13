using System.Drawing;
using System.IO;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Printing;
using Xunit;
using SolidBrush = Majorsilence.Forms.Drawing.SolidBrush;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// PrintPageEventArgs must expose the same types WinForms does, because the WinForms printing model is
/// built on a control drawing a page with the routine it draws itself with -- the handler passes
/// e.Graphics and e.MarginBounds straight into code that also serves OnPaint.
/// </summary>
public class PrintPageEventArgsParityTests
{
    private static PrintPageEventArgs CapturePageArgs ()
    {
        var doc = new PrintDocument { DocumentName = "parity" };
        PrintPageEventArgs? captured = null;

        doc.PrintPage += (_, e) => {
            captured = e;
            e.HasMorePages = false;
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        Assert.NotNull (captured);
        return captured!;
    }

    [Fact]
    public void Graphics_is_the_WinForms_Graphics_type ()
    {
        // Not SkiaGraphics: a handler must be able to hand this to a method typed as Graphics -- the
        // same one the control's OnPaint uses.
        Assert.IsType<Graphics> (CapturePageArgs ().Graphics);
    }

    [Fact]
    public void The_underlying_Skia_surface_is_still_reachable ()
    {
        Assert.NotNull (CapturePageArgs ().SkiaGraphics);
    }

    [Fact]
    public void Both_surfaces_draw_onto_the_same_canvas ()
    {
        var args = CapturePageArgs ();
        Assert.Same (args.SkiaGraphics.Canvas, args.Graphics.Canvas);
    }

    [Fact]
    public void MarginBounds_and_PageBounds_are_integer_rectangles ()
    {
        var args = CapturePageArgs ();

        Assert.IsType<Rectangle> (args.MarginBounds);
        Assert.IsType<Rectangle> (args.PageBounds);
    }

    [Fact]
    public void MarginBounds_sits_inside_PageBounds ()
    {
        var args = CapturePageArgs ();

        Assert.True (args.PageBounds.Contains (args.MarginBounds),
            $"margins {args.MarginBounds} should sit inside the page {args.PageBounds}");
    }

    [Fact]
    public void A_handler_can_draw_through_the_WinForms_Graphics ()
    {
        var doc = new PrintDocument { DocumentName = "draw" };

        doc.PrintPage += (_, e) => {
            // Exactly the shape migrated code uses: a Graphics and a Rectangle, no Skia types.
            DrawPage (e.Graphics, e.MarginBounds);
            e.HasMorePages = false;
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        Assert.True (stream.Length > 0);
    }

    private static void DrawPage (Graphics graphics, Rectangle area)
        => graphics.FillRectangle (new SolidBrush (Color.CornflowerBlue), area);
}
