using System;
using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// GDI+ figure-connection semantics: segments appended to an open figure connect to the current point
/// with an implicit line. The canonical consumer is a themed border built from four corner arcs, whose
/// edges exist ONLY as those implicit connections -- without them the path is four floating strokes
/// enclosing nothing, so filling it painted nothing and clipping to it clipped everything away (found
/// as invisible text on every rounded-corner Krypton control).
/// </summary>
public class GraphicsPathFigureTests
{
    [Fact]
    public void FourCornerArcs_EncloseTheRoundedRectangle ()
    {
        var path = new GraphicsPath ();
        float l = 0, t = 0, r = 515, b = 59, arc = 10;
        path.AddArc (l, t, arc, arc, 180f, 90f);
        path.AddArc (r - arc, t, arc, arc, 270f, 90f);
        path.AddArc (r - arc, b - arc, arc, arc, 0f, 90f);
        path.AddArc (l, b - arc, arc, arc, 90f, 90f);
        path.CloseFigure ();

        using var region = new Region (path);
        var bounds = region.GetBounds ();

        Assert.Equal (515, bounds.Width, 1.0);
        Assert.Equal (59, bounds.Height, 1.0);

        // And it survives the intersect a themed renderer performs before clipping to it.
        region.Intersect (new Region (new Rectangle (0, 0, 516, 60)));
        Assert.False (region.IsEmpty ());
    }

    [Fact]
    public void DisconnectedLineSegments_ConnectWithinAFigure ()
    {
        // Two AddLine calls whose endpoints do not abut: GDI+ inserts the connecting edge.
        var path = new GraphicsPath ();
        path.AddLine (0, 0, 100, 0);
        path.AddLine (100, 50, 0, 50);   // starts away from (100,0): implicit connector expected
        path.CloseFigure ();

        using var region = new Region (path);
        Assert.False (region.IsEmpty ());
        Assert.Equal (100, region.GetBounds ().Width, 1.0);
    }

    [Fact]
    public void StartFigure_BreaksTheConnection ()
    {
        var path = new GraphicsPath ();
        path.AddLine (0, 0, 100, 0);
        path.StartFigure ();
        path.AddLine (0, 50, 100, 50);

        // Two open one-dimensional strokes: no enclosed area.
        using var region = new Region (path);
        Assert.True (region.IsEmpty ());
    }
}
