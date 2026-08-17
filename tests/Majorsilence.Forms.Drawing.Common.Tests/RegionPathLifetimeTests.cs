using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Building a <see cref="Region"/> from a <see cref="GraphicsPath"/> leaves that path usable afterwards.
/// </summary>
/// <remarks>
/// The region takes the path's geometry; it does not take ownership of it. Disposing it instead freed a
/// native path the caller still held, and the next use of that path was a use-after-free that took the
/// process down inside Skia — reproduced as a SIGSEGV mid-drag in a docking layout, where one guide's
/// path is turned into a region on every mouse move and reused for the next one.
/// </remarks>
public class RegionPathLifetimeTests
{
    private static GraphicsPath Diamond ()
    {
        var path = new GraphicsPath ();
        path.AddPolygon (new[] { new Point (50, 0), new Point (100, 50), new Point (50, 100), new Point (0, 50) });
        return path;
    }

    [Fact]
    public void The_path_survives_being_made_into_a_region ()
    {
        using var path = Diamond ();

        using (var region = new Region (path))
            Assert.False (region.IsEmpty ());

        // Still usable: the region borrowed the geometry, it did not consume the path.
        Assert.False (path.GetBounds ().IsEmpty);
    }

    [Fact]
    public void The_path_survives_repeated_region_building ()
    {
        // The docking-drag shape: the same guide path becomes a region on every mouse move.
        using var path = Diamond ();

        for (var i = 0; i < 50; i++)
            using (var region = new Region (path))
                Assert.False (region.IsEmpty ());

        Assert.False (path.GetBounds ().IsEmpty);
    }

    [Fact]
    public void The_path_survives_being_unioned_into_a_region ()
    {
        using var path = Diamond ();
        using var region = new Region (new Rectangle (0, 0, 10, 10));

        for (var i = 0; i < 50; i++)
            region.Union (path);

        Assert.False (path.GetBounds ().IsEmpty);
        Assert.False (region.IsEmpty ());
    }

    [Fact]
    public void A_region_from_a_path_covers_the_shape_and_not_the_space_around_it ()
    {
        // Guards the bounded-clip optimisation: clipping the rasterisation to the path's own bounds must
        // not change what the region covers.
        using var path = Diamond ();
        using var region = new Region (path);

        Assert.True (region.IsVisible (new PointF (50, 50)));    // centre of the diamond
        Assert.False (region.IsVisible (new PointF (2, 2)));     // corner, outside the diamond
        Assert.False (region.IsVisible (new PointF (500, 500))); // far away
    }
}
