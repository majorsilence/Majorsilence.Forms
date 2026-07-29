using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
// See GraphicsPathTests for why these are aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class PenAlignmentTests
{
    [Fact]
    public void Pen_DefaultsToCenterAlignment()
    {
        using var pen = new Pen(Color.Black);
        Assert.Equal(PenAlignment.Center, pen.Alignment);
    }

    [Fact]
    public void Pen_Alignment_RoundTrips()
    {
        using var pen = new Pen(Color.Black) { Alignment = PenAlignment.Inset };
        Assert.Equal(PenAlignment.Inset, pen.Alignment);
    }

    [Fact]
    public void Clone_CarriesAlignmentAndCustomCaps()
    {
        var cap = new AdjustableArrowCap(5f, 5f);
        using var pen = new Pen(Color.Black, 2f)
        {
            Alignment = PenAlignment.Outset,
            CustomEndCap = cap,
        };

        using var clone = pen.Clone();

        Assert.Equal(PenAlignment.Outset, clone.Alignment);
        Assert.Same(cap, clone.CustomEndCap);
    }

    [Fact]
    public void CustomStartCap_DrivesTheSkiaStrokeCap()
    {
        using var pen = new Pen(Color.Black, 4f)
        {
            StartCap = LineCap.Flat,
            CustomStartCap = new CustomLineCap(null, null, LineCap.Round),
        };

        using var paint = pen.CreatePaint();
        Assert.Equal(SkiaSharp.SKStrokeCap.Round, paint.StrokeCap);
    }
}

public class CustomLineCapTests
{
    [Fact]
    public void Constructor_StoresPathsAndBaseCap()
    {
        using var fill = new GraphicsPath();
        fill.AddRectangle(new RectangleF(0, 0, 4, 4));
        using var stroke = new GraphicsPath();

        using var cap = new CustomLineCap(fill, stroke, LineCap.Square, 1.5f);

        Assert.Same(fill, cap.FillPath);
        Assert.Same(stroke, cap.StrokePath);
        Assert.Equal(LineCap.Square, cap.BaseCap);
        Assert.Equal(1.5f, cap.BaseInset);
    }

    [Fact]
    public void Defaults_MatchGdiPlus()
    {
        using var cap = new CustomLineCap(null, null);

        Assert.Equal(LineCap.Flat, cap.BaseCap);
        Assert.Equal(0f, cap.BaseInset);
        Assert.Equal(LineJoin.Miter, cap.StrokeJoin);
        Assert.Equal(1f, cap.WidthScale);
    }

    [Fact]
    public void SetStrokeCaps_RoundTripsThroughGetStrokeCaps()
    {
        using var cap = new CustomLineCap(null, null);
        cap.SetStrokeCaps(LineCap.Round, LineCap.Triangle);

        cap.GetStrokeCaps(out var start, out var end);
        Assert.Equal(LineCap.Round, start);
        Assert.Equal(LineCap.Triangle, end);
    }

    [Fact]
    public void Clone_CopiesEverySetting()
    {
        using var cap = new CustomLineCap(null, null, LineCap.Square, 2f)
        {
            StrokeJoin = LineJoin.Round,
            WidthScale = 3f,
        };
        cap.SetStrokeCaps(LineCap.Round, LineCap.Round);

        using var clone = (CustomLineCap)cap.Clone();

        Assert.Equal(LineCap.Square, clone.BaseCap);
        Assert.Equal(2f, clone.BaseInset);
        Assert.Equal(LineJoin.Round, clone.StrokeJoin);
        Assert.Equal(3f, clone.WidthScale);
        clone.GetStrokeCaps(out var start, out _);
        Assert.Equal(LineCap.Round, start);
    }

    [Fact]
    public void Dispose_DropsThePaths()
    {
        using var fill = new GraphicsPath();
        var cap = new CustomLineCap(fill, null);
        cap.Dispose();

        Assert.Null(cap.FillPath);
    }

    [Fact]
    public void AdjustableArrowCap_BuildsARealTriangleOutline()
    {
        using var cap = new AdjustableArrowCap(6f, 8f);

        Assert.Equal(6f, cap.Width);
        Assert.Equal(8f, cap.Height);
        Assert.True(cap.Filled);
        Assert.NotNull(cap.FillPath);

        var bounds = cap.FillPath!.GetBounds();
        Assert.Equal(6f, bounds.Width, 3);
        Assert.Equal(8f, bounds.Height, 3);
    }

    [Fact]
    public void AdjustableArrowCap_ResizingRebuildsTheOutline()
    {
        using var cap = new AdjustableArrowCap(2f, 2f);
        cap.Width = 10f;
        cap.Height = 20f;

        var bounds = cap.FillPath!.GetBounds();
        Assert.Equal(10f, bounds.Width, 3);
        Assert.Equal(20f, bounds.Height, 3);
    }

    [Fact]
    public void AdjustableArrowCap_Clone_ProducesAnotherArrowCap()
    {
        using var cap = new AdjustableArrowCap(4f, 5f, false) { MiddleInset = 1.5f };
        using var clone = (AdjustableArrowCap)cap.Clone();

        Assert.Equal(4f, clone.Width);
        Assert.Equal(5f, clone.Height);
        Assert.False(clone.Filled);
        Assert.Equal(1.5f, clone.MiddleInset);
    }
}

public class GraphicsPathIteratorTests
{
    [Fact]
    public void EmptyPath_HasNoPointsOrSubpaths()
    {
        using var path = new GraphicsPath();
        using var iterator = new GraphicsPathIterator(path);

        Assert.Equal(0, iterator.Count);
        Assert.Equal(0, iterator.SubpathCount);
        Assert.Equal(0, iterator.NextSubpath(out _, out _, out _));
    }

    [Fact]
    public void NullPath_IsTreatedAsEmpty()
    {
        using var iterator = new GraphicsPathIterator(null);
        Assert.Equal(0, iterator.Count);
    }

    [Fact]
    public void LinePath_CountsEveryPoint()
    {
        using var path = new GraphicsPath();
        path.AddLine(0, 0, 10, 0);
        path.AddLine(10, 0, 10, 10);

        using var iterator = new GraphicsPathIterator(path);

        Assert.Equal(3, iterator.Count);
        Assert.Equal(1, iterator.SubpathCount);
    }

    [Fact]
    public void TwoFigures_ReportTwoSubpaths()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0, 0, 10, 10));
        path.AddRectangle(new RectangleF(20, 20, 10, 10));

        using var iterator = new GraphicsPathIterator(path);

        Assert.Equal(2, iterator.SubpathCount);

        var first = iterator.NextSubpath(out var start1, out var end1, out var closed1);
        var second = iterator.NextSubpath(out var start2, out var end2, out var closed2);

        Assert.True(first > 0);
        Assert.True(second > 0);
        Assert.True(closed1);
        Assert.True(closed2);
        Assert.Equal(0, start1);
        Assert.True(start2 > end1);
        Assert.True(end2 >= start2);

        Assert.Equal(0, iterator.NextSubpath(out _, out _, out _));
    }

    [Fact]
    public void OpenFigure_IsNotReportedAsClosed()
    {
        using var path = new GraphicsPath();
        path.AddLine(0, 0, 10, 10);

        using var iterator = new GraphicsPathIterator(path);
        iterator.NextSubpath(out _, out _, out var closed);

        Assert.False(closed);
    }

    [Fact]
    public void NextSubpath_CopiesTheFigureIntoAPath()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0, 0, 10, 10));
        path.AddRectangle(new RectangleF(50, 50, 10, 10));

        using var iterator = new GraphicsPathIterator(path);
        using var copy = new GraphicsPath();

        var count = iterator.NextSubpath(copy, out _);

        Assert.True(count > 0);
        var bounds = copy.GetBounds();
        Assert.Equal(0f, bounds.Left, 3);
        Assert.Equal(10f, bounds.Width, 3);
    }

    [Fact]
    public void HasCurve_IsTrueOnlyForBezierSegments()
    {
        using var lines = new GraphicsPath();
        lines.AddRectangle(new RectangleF(0, 0, 10, 10));
        using var lineIterator = new GraphicsPathIterator(lines);
        Assert.False(lineIterator.HasCurve());

        using var curved = new GraphicsPath();
        curved.AddBezier(new PointF(0, 0), new PointF(5, 10), new PointF(10, 10), new PointF(15, 0));
        using var curveIterator = new GraphicsPathIterator(curved);
        Assert.True(curveIterator.HasCurve());
    }

    [Fact]
    public void Ellipse_NormalizesItsConicsToBezierPoints()
    {
        using var path = new GraphicsPath();
        path.AddEllipse(new RectangleF(0, 0, 20, 10));

        using var iterator = new GraphicsPathIterator(path);

        // Skia stores an oval as conics; the iterator elevates them so GDI+ callers only ever see
        // Start / Bezier point types, in groups of three.
        Assert.True(iterator.HasCurve());

        var points = new PointF[iterator.Count];
        var types = new byte[iterator.Count];
        iterator.Enumerate(ref points, ref types);

        Assert.Equal((byte)PathPointType.Start, (byte)(types[0] & (byte)PathPointType.PathTypeMask));
        Assert.All(types.Skip(1), t => Assert.Equal(
            (byte)PathPointType.Bezier, (byte)(t & (byte)PathPointType.PathTypeMask)));
        Assert.Equal(0, (types.Length - 1) % 3);
    }

    [Fact]
    public void Enumerate_ReturnsEveryPointAndType()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0, 0, 10, 10));

        using var iterator = new GraphicsPathIterator(path);
        var points = new PointF[iterator.Count];
        var types = new byte[iterator.Count];

        var copied = iterator.Enumerate(ref points, ref types);

        Assert.Equal(iterator.Count, copied);
        Assert.Equal((byte)PathPointType.Start, (byte)(types[0] & (byte)PathPointType.PathTypeMask));
        Assert.Contains(types, t => (t & (byte)PathPointType.CloseSubpath) != 0);
    }

    [Fact]
    public void CopyData_HonoursTheRequestedRange()
    {
        using var path = new GraphicsPath();
        path.AddLine(0, 0, 10, 0);
        path.AddLine(10, 0, 10, 10);

        using var iterator = new GraphicsPathIterator(path);
        var points = new PointF[2];
        var types = new byte[2];

        var copied = iterator.CopyData(ref points, ref types, 1, 2);

        Assert.Equal(2, copied);
        Assert.Equal(new PointF(10, 0), points[0]);
        Assert.Equal(new PointF(10, 10), points[1]);
    }

    [Fact]
    public void CopyData_MismatchedArrays_Throws()
    {
        using var path = new GraphicsPath();
        path.AddLine(0, 0, 10, 0);

        using var iterator = new GraphicsPathIterator(path);
        var points = new PointF[2];
        var types = new byte[3];

        Assert.Throws<ArgumentException>(() => iterator.CopyData(ref points, ref types, 0, 1));
    }

    [Fact]
    public void NextMarker_ReturnsTheWholePathOnceWhenNoMarkersExist()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0, 0, 10, 10));

        using var iterator = new GraphicsPathIterator(path);

        var count = iterator.NextMarker(out var start, out var end);
        Assert.Equal(iterator.Count, count);
        Assert.Equal(0, start);
        Assert.Equal(iterator.Count - 1, end);

        Assert.Equal(0, iterator.NextMarker(out _, out _));
    }

    [Fact]
    public void NextPathType_WalksTheCurrentSubpath()
    {
        using var path = new GraphicsPath();
        path.AddLine(0, 0, 10, 0);
        path.AddLine(10, 0, 10, 10);

        using var iterator = new GraphicsPathIterator(path);
        iterator.NextSubpath(out _, out _, out _);

        var count = iterator.NextPathType(out var pathType, out var start, out var end);

        Assert.Equal(3, count);
        Assert.Equal((byte)PathPointType.Line, pathType);
        Assert.Equal(0, start);
        Assert.Equal(2, end);
        Assert.Equal(0, iterator.NextPathType(out _, out _, out _));
    }

    [Fact]
    public void Rewind_RestartsEveryCursor()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0, 0, 10, 10));
        path.AddRectangle(new RectangleF(20, 20, 10, 10));

        using var iterator = new GraphicsPathIterator(path);
        iterator.NextSubpath(out _, out _, out _);
        iterator.NextSubpath(out _, out _, out _);
        iterator.NextMarker(out _, out _);

        iterator.Rewind();

        Assert.True(iterator.NextSubpath(out _, out _, out _) > 0);
        Assert.True(iterator.NextMarker(out _, out _) > 0);
    }
}
