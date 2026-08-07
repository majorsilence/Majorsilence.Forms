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

        // CapturePng returns the client area, so the strip's own bounds are already the right
        // coordinates to scan -- there is no title bar above them to skip past.
        var background = bmp.GetPixel (bmp.Width - 10, bmp.Height - 10);
        var contentFound = false;
        for (var y = strip.Top; y < strip.Top + strip.Height && !contentFound; y++)
            for (var x = strip.Left; x < Math.Min (strip.Left + strip.Width, bmp.Width); x++)
                if (bmp.GetPixel (x, y) != background) { contentFound = true; break; }

        Assert.True (contentFound, "ToolStrip with buttons rendered as an empty bar.");
    }

    // Regression: ToolStripItem.Text was a plain auto-property, so assigning it changed nothing on
    // screen. A status-bar label updated from a key handler kept showing its old text until something
    // else happened to invalidate the control -- a caret-position indicator looked like it lagged a
    // keystroke behind, catching up only when the next character repainted the form for its own reasons.
    [Fact]
    public void Changing_an_item_Text_repaints_the_strip ()
    {
        var form = new Form ();
        var strip = new StatusStrip { Dock = DockStyle.Bottom, Height = 24 };
        var label = new ToolStripStatusLabel { Text = "Ln 1, Col 1" };
        strip.Items.Add (label);
        form.Controls.Add (strip);

        var before = HeadlessRenderer.CapturePng (form, 400, 200);

        label.Text = "Ln 9, Col 7";

        var after = HeadlessRenderer.CapturePng (form, 400, 200);

        Assert.False (before.AsSpan ().SequenceEqual (after),
            "changing a status label's Text left the rendered strip identical");
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
