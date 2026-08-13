using System.Drawing;
using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Laying a window out to a negative size must clamp, not throw.
/// </summary>
/// <remarks>
/// WinForms passes the size to SetWindowPos, which treats a negative extent as zero, so WinForms code
/// computes and assigns negative sizes routinely and survives -- a docking pane whose available area
/// has collapsed does exactly that. The Avalonia backend rejects a negative Width with
/// ArgumentException, which took the whole application down from inside a layout pass.
/// </remarks>
public class FormNegativeSizeTests
{
    [Fact]
    public void Negative_width_clamps_to_zero_instead_of_throwing ()
    {
        using var form = new Form ();
        form.Size = new Size (-14, 200);

        Assert.Equal (0, form.Size.Width);
    }

    [Fact]
    public void Negative_height_clamps_to_zero_instead_of_throwing ()
    {
        using var form = new Form ();
        form.Size = new Size (200, -83);

        Assert.Equal (0, form.Size.Height);
    }

    [Fact]
    public void Both_dimensions_negative_clamps_both ()
    {
        using var form = new Form ();
        form.Size = new Size (-1, -1);

        Assert.Equal (new Size (0, 0), form.Size);
    }

    [Fact]
    public void Negative_bounds_clamp_without_throwing ()
    {
        // The path DockPane.SetContentBounds takes: assign a whole rectangle at once.
        using var form = new Form ();
        form.Bounds = new Rectangle (12, 6, -18, -83);

        Assert.Equal (0, form.Size.Width);
        Assert.Equal (0, form.Size.Height);
    }

    [Fact]
    public void Negative_width_via_the_Width_property_clamps ()
    {
        using var form = new Form ();
        form.Width = -5;

        Assert.Equal (0, form.Width);
    }

    [Fact]
    public void A_positive_size_is_still_applied_unchanged ()
    {
        using var form = new Form ();
        form.Size = new Size (321, 234);

        Assert.Equal (new Size (321, 234), form.Size);
    }
}
