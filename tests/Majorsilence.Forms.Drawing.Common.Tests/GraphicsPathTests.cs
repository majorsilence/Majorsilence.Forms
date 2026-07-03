using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class GraphicsPathTests
{
    private static void AssertRectApprox(RectangleF expected, RectangleF actual, float tolerance = 1e-3f)
    {
        Assert.True(MathF.Abs(actual.X      - expected.X)      <= tolerance, $"X: {actual.X} != {expected.X}");
        Assert.True(MathF.Abs(actual.Y      - expected.Y)      <= tolerance, $"Y: {actual.Y} != {expected.Y}");
        Assert.True(MathF.Abs(actual.Width  - expected.Width)  <= tolerance, $"W: {actual.Width} != {expected.Width}");
        Assert.True(MathF.Abs(actual.Height - expected.Height) <= tolerance, $"H: {actual.Height} != {expected.Height}");
    }

    [Fact]
    public void DefaultConstructor_EmptyPath_ZeroPointCount()
    {
        using var path = new GraphicsPath();
        Assert.Equal(0, path.PointCount);
    }

    [Fact]
    public void FillMode_Constructor_RoundTrips()
    {
        using var pathAlt = new GraphicsPath(FillMode.Alternate);
        using var pathWnd = new GraphicsPath(FillMode.Winding);
        Assert.Equal(FillMode.Alternate, pathAlt.FillMode);
        Assert.Equal(FillMode.Winding,  pathWnd.FillMode);
    }

    [Fact]
    public void AddRectangle_GetBounds_MatchesInput()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(10f, 20f, 100f, 50f));
        var bounds = path.GetBounds();
        AssertRectApprox(new RectangleF(10f, 20f, 100f, 50f), bounds);
    }

    [Fact]
    public void AddRectangle_IntOverload_GetBounds_MatchesInput()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new Rectangle(5, 15, 80, 40));
        var bounds = path.GetBounds();
        AssertRectApprox(new RectangleF(5f, 15f, 80f, 40f), bounds);
    }

    [Fact]
    public void AddEllipse_GetBounds_ApproximatesBoundingRect()
    {
        using var path = new GraphicsPath();
        path.AddEllipse(new RectangleF(0f, 0f, 100f, 60f));
        var bounds = path.GetBounds();
        // Ellipse bounds should be at most the enclosing rectangle
        Assert.True(bounds.X      >= -0.01f);
        Assert.True(bounds.Y      >= -0.01f);
        Assert.True(bounds.Width  <=  100.01f);
        Assert.True(bounds.Height <=   60.01f);
    }

    [Fact]
    public void AddLine_IncreasesPointCount()
    {
        using var path = new GraphicsPath();
        path.AddLine(0f, 0f, 100f, 100f);
        Assert.True(path.PointCount >= 2);
    }

    [Fact]
    public void AddLines_MultiplePoints_SetsBounds()
    {
        using var path = new GraphicsPath();
        path.AddLines(new[]
        {
            new PointF(0f,  0f),
            new PointF(50f, 50f),
            new PointF(100f, 0f)
        });
        var bounds = path.GetBounds();
        Assert.True(bounds.Width  >= 100f - 0.01f);
        Assert.True(bounds.Height >=  50f - 0.01f);
    }

    [Fact]
    public void IsVisible_PointInsideRectangle_ReturnsTrue()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0f, 0f, 100f, 100f));
        Assert.True(path.IsVisible(50f, 50f));
    }

    [Fact]
    public void IsVisible_PointOutsideRectangle_ReturnsFalse()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0f, 0f, 100f, 100f));
        Assert.False(path.IsVisible(200f, 200f));
    }

    [Fact]
    public void Reset_ClearsPath()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0f, 0f, 100f, 100f));
        path.Reset();
        Assert.Equal(0, path.PointCount);
    }

    [Fact]
    public void AddRectangles_MultipleBoundsUnion()
    {
        using var path = new GraphicsPath();
        path.AddRectangles(new[]
        {
            new RectangleF(0f,   0f, 10f, 10f),
            new RectangleF(90f, 90f, 10f, 10f)
        });
        var bounds = path.GetBounds();
        Assert.True(bounds.Width  >= 100f - 0.01f);
        Assert.True(bounds.Height >= 100f - 0.01f);
    }

    [Fact]
    public void GetBounds_WithTranslateMatrix_ShiftsBounds()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0f, 0f, 50f, 30f));

        using var m = new Matrix();
        m.Translate(100f, 200f);

        var bounds = path.GetBounds(m);
        AssertRectApprox(new RectangleF(100f, 200f, 50f, 30f), bounds);
    }

    [Fact]
    public void Transform_TranslatesPathPoints()
    {
        using var path = new GraphicsPath();
        path.AddRectangle(new RectangleF(0f, 0f, 10f, 10f));

        using var m = new Matrix();
        m.Translate(5f, 5f);
        path.Transform(m);

        var bounds = path.GetBounds();
        Assert.True(bounds.X >= 4.99f && bounds.X <= 5.01f);
        Assert.True(bounds.Y >= 4.99f && bounds.Y <= 5.01f);
    }

    [Fact]
    public void PathPoints_ReflectsAddedPoints()
    {
        using var path = new GraphicsPath();
        path.AddLine(0f, 0f, 10f, 10f);
        var pts = path.PathPoints;
        Assert.True(pts.Length >= 2);
    }

    [Fact]
    public void CloseFigure_DoesNotThrow()
    {
        using var path = new GraphicsPath();
        path.AddLine(0f, 0f, 10f, 0f);
        path.AddLine(10f, 0f, 10f, 10f);
        path.CloseFigure(); // should not throw
        Assert.True(path.PointCount >= 2);
    }
}
