using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// A window with a <see cref="WindowBase.Region"/> is SHAPED: it paints only inside the region, and the
/// rest of it is left clear so whatever is behind shows through.
/// </summary>
/// <remarks>
/// Region was stored and never read, which produced the opposite of its purpose. A drag overlay is a
/// full-screen, input-transparent window whose region is a handful of small guide shapes; ignoring the
/// region painted the whole thing, so starting a drag in a docking layout dropped a screen-sized opaque
/// rectangle over the desktop and left the user dragging blind.
///
/// The other half — telling the backend to stop filling an opaque backdrop behind the clip — is a
/// platform concern and is verified by eye on a real backend; what is asserted here is the clip itself.
/// </remarks>
[Collection ("Headless")]
public class WindowRegionTests
{
    private const int Size = 100;

    private static SKBitmap Render (Rectangle? shape)
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = Size, Height = Size, BackColor = Color.Red };

        if (shape is { } rect)
            form.Region = new Majorsilence.Forms.Drawing.Region (rect);

        var png = HeadlessRenderer.CapturePng (form, Size, Size);
        return SKBitmap.Decode (png);
    }

    [Fact]
    public void Without_a_region_the_whole_window_is_painted ()
    {
        using var bitmap = Render (null);

        Assert.True (bitmap.GetPixel (Size / 2, Size / 2).Alpha > 0);
        Assert.True (bitmap.GetPixel (5, 5).Alpha > 0);
    }

    [Fact]
    public void Nothing_is_painted_outside_the_region ()
    {
        using var bitmap = Render (new Rectangle (10, 10, 30, 30));

        Assert.Equal (0, bitmap.GetPixel (80, 80).Alpha);
        Assert.Equal (0, bitmap.GetPixel (5, 5).Alpha);
    }

    [Fact]
    public void The_region_itself_is_painted ()
    {
        using var bitmap = Render (new Rectangle (10, 10, 30, 30));

        Assert.True (bitmap.GetPixel (25, 25).Alpha > 0, "the inside of the region should be painted");
    }

    [Fact]
    public void An_empty_region_paints_nothing_at_all ()
    {
        // How a drag overlay starts life: constructed with an empty region so it shows nothing until it
        // has guides to draw.
        using var bitmap = Render (Rectangle.Empty);

        for (var y = 0; y < Size; y += 10)
            for (var x = 0; x < Size; x += 10)
                Assert.Equal (0, bitmap.GetPixel (x, y).Alpha);
    }

    [Fact]
    public void Clearing_the_region_restores_a_fully_painted_window ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = Size, Height = Size, BackColor = Color.Red };
        form.Region = new Majorsilence.Forms.Drawing.Region (new Rectangle (10, 10, 30, 30));
        HeadlessRenderer.CapturePng (form, Size, Size);

        form.Region = null;

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, Size, Size));

        Assert.True (bitmap.GetPixel (80, 80).Alpha > 0);
    }

    [Fact]
    public void The_region_reads_back_as_assigned ()
    {
        using var form = new Form { Width = Size, Height = Size };
        var region = new Majorsilence.Forms.Drawing.Region (new Rectangle (1, 2, 3, 4));

        form.Region = region;

        Assert.Same (region, form.Region);
    }
}
