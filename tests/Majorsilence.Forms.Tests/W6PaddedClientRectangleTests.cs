using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RC-8 (W6.3): PaddedClientRectangle is device, and it used to take the LOGICAL padding -- and, for a
// scrolling control, the scroll bars' logical sizes -- straight off the device client rectangle. At
// scale 1 the two agree, so only the MF_HEADLESS_SCALE=2 gate can make these fail; the neutralization
// round runs there.
[Collection ("Headless")]
public class W6PaddedClientRectangleTests
{
    [Fact]
    public void The_padding_is_taken_off_in_device_pixels ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var panel = new Panel { Bounds = new Rectangle (0, 0, 200, 100), Padding = new Padding (10, 6, 4, 2) };
        form.Controls.Add (panel);
        form.Show ();

        var client = panel.ClientRectangle;
        var padded = panel.PaddedClientRectangle;

        Assert.Equal (client.Left + panel.LogicalToDeviceUnits (10), padded.Left);
        Assert.Equal (client.Top + panel.LogicalToDeviceUnits (6), padded.Top);
        Assert.Equal (client.Width - panel.LogicalToDeviceUnits (14), padded.Width);
        Assert.Equal (client.Height - panel.LogicalToDeviceUnits (8), padded.Height);
    }

    [Fact]
    public void A_scrolling_controls_padded_area_stops_at_its_scroll_bars ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var box = new TextBox { Bounds = new Rectangle (0, 0, 200, 120), Multiline = true, ScrollBars = ScrollBars.Both, Padding = new Padding (3) };
        form.Controls.Add (box);
        form.Show ();

        var client = box.ClientRectangle;
        var padded = box.PaddedClientRectangle;

        Assert.True (box.VerticalScrollBar.Visible && box.HorizontalScrollBar.Visible, "PREMISE: both bars are shown");

        // The bars' own sizes are logical, like any child's; the rectangle is device.
        Assert.Equal (client.Width - box.LogicalToDeviceUnits (6) - box.LogicalToDeviceUnits (box.VerticalScrollBar.Width), padded.Width);
        Assert.Equal (client.Height - box.LogicalToDeviceUnits (6) - box.LogicalToDeviceUnits (box.HorizontalScrollBar.Height), padded.Height);

    }
}
