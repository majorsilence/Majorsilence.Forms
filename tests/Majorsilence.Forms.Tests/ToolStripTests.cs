using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Pins the ToolStrip facade's mirroring into the base MenuBase root collection -- the only
// collection LayoutItems, ToolBarRenderer, and MenuBase mouse hit-testing consume. Regression:
// found via a real migrated WinForms app (ReportDesigner.Forms) whose two designer ToolStrips
// (21 and 19 buttons) rendered as completely empty bars, because items added to the shadowing
// ToolStrip.Items collection never reached the base collection at all.
public class ToolStripTests
{
    [Fact]
    public void Items_Add_MirrorsIntoBaseCollection ()
    {
        var strip = new ToolStrip ();
        var button = new ToolStripButton { Name = "b1", Text = "Bold" };

        strip.Items.Add (button);

        Assert.Contains (button, ((ToolBar)strip).Items);
    }

    [Fact]
    public void Items_Remove_UnmirrorsFromBaseCollection ()
    {
        var strip = new ToolStrip ();
        var button = new ToolStripButton { Name = "b1", Text = "Bold" };
        strip.Items.Add (button);

        strip.Items.Remove (button);

        Assert.DoesNotContain (button, ((ToolBar)strip).Items);
    }

    [Fact]
    public void Items_Clear_UnmirrorsAllFromBaseCollection ()
    {
        var strip = new ToolStrip ();
        strip.Items.Add (new ToolStripButton { Text = "A" });
        strip.Items.Add (new ToolStripButton { Text = "B" });

        strip.Items.Clear ();

        Assert.Empty (((ToolBar)strip).Items);
    }

    [Fact]
    public void ToolStrip_WithButtons_RendersVisibleContent ()
    {
        // End-to-end: buttons in the facade collection must actually paint. Before the mirroring
        // fix this rendered a completely flat, empty bar.
        var form = new Form ();
        var strip = new ToolStrip { Dock = DockStyle.Top, Height = 30 };
        strip.Items.Add (new ToolStripButton { Text = "Bold" });
        strip.Items.Add (new ToolStripButton { Text = "Italic" });
        form.Controls.Add (strip);

        var png = HeadlessRenderer.CapturePng (form, 400, 100);

        using var bmp = SKBitmap.Decode (png);
        var background = bmp.GetPixel (390, 90);
        var contentFound = false;
        for (var y = strip.Top; y < strip.Top + strip.Height && !contentFound; y++)
            for (var x = 0; x < 200; x++)
                if (bmp.GetPixel (x, y + 34) != background) { contentFound = true; break; }   // +34 = form title bar

        Assert.True (contentFound, "ToolStrip with buttons rendered as an empty bar.");
    }

    [Fact]
    public void ItemClicked_StillRaised_ViaBaseClickPipeline ()
    {
        var strip = new ToolStrip ();
        var button = new ToolStripButton { Text = "Bold" };
        strip.Items.Add (button);
        ToolStripItem? clicked = null;
        strip.ItemClicked += (_, e) => clicked = e.ClickedItem;

        button.PerformClick ();

        Assert.Same (button, clicked);
    }
}
