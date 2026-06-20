using System.Drawing;
using System.IO;
using System.Text;
using Continuum.Drawing;
using Continuum.Forms.Printing;
using Xunit;
using Font = Continuum.Drawing.Font;
using SolidBrush = Continuum.Drawing.SolidBrush;

namespace Continuum.Forms.Tests;

public class PrintDocumentTests
{
    [Fact]
    public void PrintToPdf_ProducesValidPdf ()
    {
        var doc = new PrintDocument { DocumentName = "test" };

        doc.PrintPage += (o, e) => {
            e.Graphics.FillRectangle (new SolidBrush (Color.CornflowerBlue), e.MarginBounds);
            e.HasMorePages = false;
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        Assert.True (stream.Length > 0);

        var header = Encoding.ASCII.GetString (stream.ToArray (), 0, 5);
        Assert.Equal ("%PDF-", header);
    }

    [Fact]
    public void PrintToPdf_RendersAllRequestedPages ()
    {
        var doc = new PrintDocument ();
        const int total = 3;
        var rendered = 0;

        doc.PrintPage += (o, e) => {
            rendered++;
            using var font = new Font ("Arial", 12f);
            e.Graphics.DrawString ($"Page {rendered}", font, new SolidBrush (Color.Black), new PointF (e.MarginBounds.Left, e.MarginBounds.Top));
            e.HasMorePages = rendered < total;
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        Assert.Equal (total, rendered);
        Assert.True (stream.Length > 0);
    }

    [Fact]
    public void PrintToPdf_HonorsCancel ()
    {
        var doc = new PrintDocument ();
        var rendered = 0;

        doc.PrintPage += (o, e) => {
            rendered++;
            e.HasMorePages = true;   // would loop forever...
            e.Cancel = true;         // ...but cancel stops after the first page
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        Assert.Equal (1, rendered);
    }

    [Fact]
    public void MarginBounds_AreInsidePageBounds ()
    {
        var doc = new PrintDocument ();
        RectangleF page = default;
        RectangleF margin = default;

        doc.PrintPage += (o, e) => {
            page = e.PageBounds;
            margin = e.MarginBounds;
            e.HasMorePages = false;
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        // US Letter at 96 DPI = 816 x 1056 px, 1" margins = 96 px inset.
        Assert.Equal (816f, page.Width, 3);
        Assert.Equal (1056f, page.Height, 3);
        Assert.Equal (96f, margin.Left, 3);
        Assert.Equal (96f, margin.Top, 3);
        Assert.True (margin.Width < page.Width);
        Assert.True (margin.Height < page.Height);
    }

    [Fact]
    public void Landscape_SwapsDimensions ()
    {
        var doc = new PrintDocument ();
        doc.DefaultPageSettings.Landscape = true;
        RectangleF page = default;

        doc.PrintPage += (o, e) => {
            page = e.PageBounds;
            e.HasMorePages = false;
        };

        using var stream = new MemoryStream ();
        doc.PrintToPdf (stream);

        Assert.True (page.Width > page.Height, "Landscape page should be wider than tall.");
    }
}
