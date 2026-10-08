using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// LST-61: <see cref="TabControl.HotTrack"/> gates the hovered-tab style, as upstream gates TCS_HOTTRACK,
/// and every built-in theme gives the hovered tab a background that differs from the strip's.
/// </summary>
[Collection ("Headless")]
public class TabControlHotTrackTests : IDisposable
{
    public TabControlHotTrackTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

    public void Dispose ()
    {
        GC.SuppressFinalize (this);

        foreach (var name in Theme.RegisteredThemes.ToList ())
            Theme.UnregisterTheme (name);

        Theme.SetBuiltInTheme (BuiltInTheme.Light);
    }

    // Renders the strip with the second tab cold and then hovered, and returns both plus the tab's
    // device-pixel box. The first render lays the strip out: tab bounds are empty until it has, so a
    // tab cannot be hovered before then.
    private static (SKBitmap Cold, SKBitmap Hot, Rectangle Band) Hover (bool hotTrack)
    {
        HeadlessRenderer.Use ();
        using var tabs = new TabControl { Size = new Size (300, 200), HotTrack = hotTrack };
        tabs.TabPages.Add ("One");
        tabs.TabPages.Add ("Two");
        var strip = tabs.TabStrip;
        PaintSurface.Render (strip).Dispose ();

        var cold = PaintSurface.Render (strip);
        strip.Tabs.HoveredIndex = 1;
        Assert.True (strip.Tabs[1].Hovered, "the premise: the second tab is hovered");
        var hot = PaintSurface.Render (strip);

        var band = strip.LogicalToDeviceUnits (strip.Tabs[1].Bounds);
        Assert.False (band.IsEmpty, "the premise: the tab has been laid out");

        return (cold, hot, band);
    }

    private static int DifferingPixels (SKBitmap a, SKBitmap b, Rectangle band)
    {
        var differing = 0;
        for (var y = band.Top; y < band.Bottom; y++)
            for (var x = band.Left; x < band.Right; x++)
                if (a.GetPixel (x, y) != b.GetPixel (x, y))
                    differing++;
        return differing;
    }

    // A pixel inside the tab but clear of its caption: the tab's background.
    private static SKColor Corner (SKBitmap bitmap, Rectangle band) => bitmap.GetPixel (band.Left + 2, band.Top + 2);

    [Fact]
    public void A_hovered_tab_paints_like_any_other_without_HotTrack ()
    {
        var (cold, hot, band) = Hover (hotTrack: false);
        using (cold)
        using (hot)
            Assert.Equal (0, DifferingPixels (cold, hot, band));
    }

    [Fact]
    public void A_hovered_tab_paints_differently_with_HotTrack ()
    {
        var (cold, hot, band) = Hover (hotTrack: true);
        using (cold)
        using (hot)
            Assert.True (DifferingPixels (cold, hot, band) > 0, "HotTrack changed no pixel of the hovered tab");
    }

    [Theory]
    [InlineData (BuiltInTheme.Light)]
    [InlineData (BuiltInTheme.Dark)]
    [InlineData (BuiltInTheme.Classic)]
    [InlineData (BuiltInTheme.Aero)]
    [InlineData (BuiltInTheme.PointOfSale)]
    [InlineData (BuiltInTheme.HotDog)]
    public void Every_built_in_theme_gives_a_hot_tracked_tab_its_own_background (BuiltInTheme theme)
    {
        Theme.SetBuiltInTheme (theme);

        var (cold, hot, band) = Hover (hotTrack: true);
        using (cold)
        using (hot)
            Assert.NotEqual (Corner (cold, band), Corner (hot, band));
    }

    [Fact]
    public void A_TabStrip_item_hover_rule_paints_the_hot_tracked_tab_and_only_that ()
    {
        var marker = new SKColor (0xfe, 0x01, 0x7f);   // a colour no theme uses
        Theme.LoadFromCss ("TabStrip::item:hover { background-color: #fe017f; }");

        var (cold, hot, band) = Hover (hotTrack: true);
        using (cold)
        using (hot) {
            Assert.Equal (marker, Corner (hot, band));
            Assert.NotEqual (marker, Corner (cold, band));
        }

        var (off_cold, off_hot, off_band) = Hover (hotTrack: false);
        using (off_cold)
        using (off_hot)
            Assert.NotEqual (marker, Corner (off_hot, off_band));
    }
}
