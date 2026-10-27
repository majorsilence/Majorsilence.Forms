using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Upstream's ToolStripSplitStackLayout stretches only an auto-sized item across the strip; an item with
// AutoSize off keeps its Size and is centred in the strip's display rectangle. Measured from WinForms
// (.NET 10), each case below.
public class ToolStripFixedSizeItemTests
{
    [Fact]
    public void A_fixed_size_button_keeps_its_height_beside_a_stretched_one ()
    {
        // WinForms: in the 83px strip it sizes round it, the 150 x 80 button sits at y = 1 and the
        // auto-sized button beside it is stretched to 80 (the strip less its 1 + 2px margin).
        var strip = new ToolStrip { Dock = DockStyle.Top, AutoSize = false, Height = 83 };
        var fixed_button = new ToolStripButton ("Fixed") { AutoSize = false, Size = new Size (150, 80) };
        var auto_button = new ToolStripButton ("Auto");
        strip.Items.Add (fixed_button);
        strip.Items.Add (auto_button);
        using var form = new Form { ClientSize = new Size (500, 300) };
        form.Controls.Add (strip);
        using var laid_out = PaintSurface.Render (strip);

        Assert.Equal (new Size (150, 80), fixed_button.Size);
        AssertCentred (strip.ClientRectangle.Top, strip.ClientRectangle.Bottom, fixed_button.Bounds.Top, fixed_button.Bounds.Bottom);
        Assert.True (auto_button.Height >= 80 - 1, $"the auto-sized button should be stretched, was {auto_button.Height}");
    }

    [Fact]
    public void A_fixed_size_button_sized_before_it_reaches_the_strip_keeps_its_size ()
    {
        // The designer's order: the size is set, then the item is added to a strip that has not been
        // laid out round it yet. Stretched to that strip, the item's Size (its bounds) was lost.
        var strip = new ToolStrip { Dock = DockStyle.Top };
        var button = new ToolStripButton ("Fixed") { AutoSize = false, Size = new Size (150, 80) };
        strip.Items.Add (button);
        using var form = new Form { ClientSize = new Size (500, 300) };
        form.Controls.Add (strip);
        using var laid_out = PaintSurface.Render (strip);

        Assert.Equal (new Size (150, 80), button.Size);
        Assert.Equal (new Size (150, 80), button.GetPreferredSize (Size.Empty));
    }

    [Fact]
    public void A_fixed_size_button_is_centred_down_a_taller_strip ()
    {
        // WinForms: in a 120px strip a 40px button sits at y = 40.
        var strip = new ToolStrip { Dock = DockStyle.Top, AutoSize = false, Height = 120 };
        var button = new ToolStripButton ("Fixed") { AutoSize = false, Size = new Size (150, 40) };
        strip.Items.Add (button);
        using var form = new Form { ClientSize = new Size (500, 300) };
        form.Controls.Add (strip);
        using var laid_out = PaintSurface.Render (strip);

        Assert.Equal (new Size (150, 40), button.Size);
        AssertCentred (strip.ClientRectangle.Top, strip.ClientRectangle.Bottom, button.Bounds.Top, button.Bounds.Bottom);
    }

    [Fact]
    public void A_fixed_size_button_is_centred_across_a_vertical_strip ()
    {
        // WinForms: in a 119px-wide display rectangle a 60px button sits at x = 29.
        var strip = new ToolStrip { Dock = DockStyle.Left, LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow, AutoSize = false, Width = 120 };
        var button = new ToolStripButton ("V") { AutoSize = false, Size = new Size (60, 30) };
        strip.Items.Add (button);
        using var form = new Form { ClientSize = new Size (500, 300) };
        form.Controls.Add (strip);
        using var laid_out = PaintSurface.Render (strip);

        Assert.Equal (new Size (60, 30), button.Size);
        AssertCentred (strip.ClientRectangle.Left, strip.ClientRectangle.Right, button.Bounds.Left, button.Bounds.Right);
    }

    // Centred, to the pixel the strip's rounding allows: the spare room splits evenly, any odd pixel
    // below (or after), as LayoutUtils.VAlign leaves it.
    private static void AssertCentred (int start, int end, int item_start, int item_end)
    {
        var before = item_start - start;
        var after = end - item_end;
        Assert.True (after - before is 0 or 1 or -1, $"item {item_start}..{item_end} is not centred in {start}..{end}");
    }
}
