using System;
using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="Application.UiScale"/> zooms the whole UI on top of the display's own scale factor,
/// without changing any font size or designer coordinate.
/// </summary>
/// <remarks>
/// It exists for the case DPI detection cannot serve: a large, dense monitor the OS reports at scale
/// 1.0, where WinForms' classic 8.25pt default renders at its true, very small physical size.
/// </remarks>
[Collection ("Headless")]
public sealed class UiScaleTests : IDisposable
{
    private readonly double original = Application.UiScale;

    public void Dispose () => Application.UiScale = original;

    [Fact]
    public void Defaults_to_one_so_nothing_changes_for_anyone ()
    {
        Assert.Equal (1.0, Application.UiScale);
    }

    [Fact]
    public void Multiplies_the_window_scale_factor ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };

        var baseline = form.Scaling;
        Application.UiScale = 2.0;

        Assert.Equal (baseline * 2.0, form.Scaling);
    }

    [Fact]
    public void Drives_DeviceDpi_so_LogicalToDeviceUnits_grows_with_it ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var control = new Panel ();
        form.Controls.Add (control);

        var before = control.LogicalToDeviceUnits (100);
        Application.UiScale = 2.0;
        var after = control.LogicalToDeviceUnits (100);

        Assert.Equal (before * 2, after);
    }

    [Fact]
    public void Leaves_the_desktop_scale_factor_alone ()
    {
        // PointToScreen converts through DesktopScaling / Scaling; zooming both would cancel out and
        // screen coordinates would drift by the zoom factor.
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };

        var desktop = form.DesktopScaling;
        Application.UiScale = 2.0;

        Assert.Equal (desktop, form.DesktopScaling);
    }

    [Fact]
    public void Screen_coordinates_still_round_trip_when_zoomed ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var panel = new Panel { Left = 50, Top = 40, Width = 200, Height = 100 };
        form.Controls.Add (panel);
        HeadlessRenderer.CapturePng (form, 400, 300);

        Application.UiScale = 2.0;
        form.PerformLayout ();

        var local = new Point (20, 10);
        var round = panel.PointToClient (panel.PointToScreen (local));

        Assert.Equal (local, round);
    }

    [Fact]
    public void Font_sizes_themselves_are_untouched ()
    {
        // The whole point: the numbers stay as written, only their device size changes.
        var before = SystemFonts.DefaultFont.Size;
        Application.UiScale = 2.0;

        Assert.Equal (before, SystemFonts.DefaultFont.Size);
    }

    [Theory]
    [InlineData (0.0)]
    [InlineData (-1.0)]
    public void Rejects_a_non_positive_scale (double bad)
    {
        Assert.Throws<ArgumentOutOfRangeException> (() => Application.UiScale = bad);
    }

    [Fact]
    public void Setting_the_same_value_again_is_harmless ()
    {
        Application.UiScale = 1.5;
        Application.UiScale = 1.5;

        Assert.Equal (1.5, Application.UiScale);
    }
}
