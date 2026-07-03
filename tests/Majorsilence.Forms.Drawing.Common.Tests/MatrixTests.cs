using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class MatrixTests
{
    private static void AssertPointsEqual(PointF expected, PointF actual, float tolerance = 1e-4f)
    {
        Assert.True(MathF.Abs(actual.X - expected.X) <= tolerance,
            $"X: expected {expected.X} but got {actual.X}");
        Assert.True(MathF.Abs(actual.Y - expected.Y) <= tolerance,
            $"Y: expected {expected.Y} but got {actual.Y}");
    }

    [Fact]
    public void Default_IsIdentity()
    {
        using var m = new Matrix();
        Assert.True(m.IsIdentity);
        Assert.True(m.IsInvertible);
    }

    [Fact]
    public void Identity_TransformPoints_LeavesPointsUnchanged()
    {
        using var m = new Matrix();
        var pts = new[] { new PointF(3f, 7f) };
        m.TransformPoints(pts);
        AssertPointsEqual(new PointF(3f, 7f), pts[0]);
    }

    [Fact]
    public void Translate_ShiftsPointsByOffset()
    {
        using var m = new Matrix();
        m.Translate(10f, 5f);

        var pts = new[] { new PointF(0f, 0f), new PointF(1f, 1f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(10f, 5f), pts[0]);
        AssertPointsEqual(new PointF(11f, 6f), pts[1]);
    }

    [Fact]
    public void Scale_ScalesPointsFromOrigin()
    {
        using var m = new Matrix();
        m.Scale(2f, 3f);

        var pts = new[] { new PointF(1f, 1f), new PointF(5f, 4f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(2f, 3f),   pts[0]);
        AssertPointsEqual(new PointF(10f, 12f), pts[1]);
    }

    [Fact]
    public void Rotate90_RotatesPointCorrectly()
    {
        // 90° CW in screen coords (y-down): (1,0)→(0,1) and (0,1)→(-1,0)
        using var m = new Matrix();
        m.Rotate(90f);

        var pts = new[] { new PointF(1f, 0f), new PointF(0f, 1f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(0f,  1f), pts[0]);
        AssertPointsEqual(new PointF(-1f, 0f), pts[1]);
    }

    [Fact]
    public void Rotate180_NegatesPoint()
    {
        using var m = new Matrix();
        m.Rotate(180f);

        var pts = new[] { new PointF(3f, 4f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(-3f, -4f), pts[0]);
    }

    [Fact]
    public void Invert_TranslateMatrix_ReturnsInverseTranslation()
    {
        using var m = new Matrix();
        m.Translate(10f, 5f);
        bool ok = m.Invert();

        Assert.True(ok);

        var pts = new[] { new PointF(0f, 0f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(-10f, -5f), pts[0]);
    }

    [Fact]
    public void Invert_ScaleMatrix_ReturnsInverseScale()
    {
        using var m = new Matrix();
        m.Scale(2f, 4f);
        m.Invert();

        var pts = new[] { new PointF(2f, 4f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(1f, 1f), pts[0]);
    }

    [Fact]
    public void MatrixOrder_Append_ConcatensesAsM_Times_N()
    {
        // Append uses SKMatrix.Concat(_matrix, s) = _matrix * s.
        // In column-vector convention, _matrix * s applied to a point P gives _matrix(s(P)):
        //   (0,0) → Scale(2,2) → (0,0) → Translate(10,5) → (10,5)
        //   (1,0) → Scale(2,2) → (2,0) → Translate(10,5) → (12,5)
        using var m = new Matrix();
        m.Translate(10f, 5f);
        m.Scale(2f, 2f, MatrixOrder.Append);

        var pts = new[] { new PointF(0f, 0f), new PointF(1f, 0f) };
        m.TransformPoints(pts);

        AssertPointsEqual(new PointF(10f, 5f), pts[0]);
        AssertPointsEqual(new PointF(12f, 5f), pts[1]);
    }

    [Fact]
    public void Clone_ProducesIndependentCopy()
    {
        using var original = new Matrix();
        original.Translate(5f, 3f);
        using var copy = original.Clone();

        copy.Translate(100f, 100f);

        var origPts = new[] { new PointF(0f, 0f) };
        original.TransformPoints(origPts);
        AssertPointsEqual(new PointF(5f, 3f), origPts[0]); // original unchanged
    }

    [Fact]
    public void Elements_Identity_ReturnsSixElementArray()
    {
        using var m = new Matrix();
        float[] e = m.Elements;
        Assert.Equal(6, e.Length);
        // GDI+ order: [M11=ScaleX, M12=SkewY, M21=SkewX, M22=ScaleY, dx, dy]
        Assert.Equal(1f, e[0]); // ScaleX = 1
        Assert.Equal(0f, e[1]); // SkewY  = 0
        Assert.Equal(0f, e[2]); // SkewX  = 0
        Assert.Equal(1f, e[3]); // ScaleY = 1
        Assert.Equal(0f, e[4]); // TransX = 0
        Assert.Equal(0f, e[5]); // TransY = 0
    }

    [Fact]
    public void Reset_AfterTranslate_RestoresIdentity()
    {
        using var m = new Matrix();
        m.Translate(99f, 99f);
        m.Reset();

        Assert.True(m.IsIdentity);
    }

    [Fact]
    public void TransformPoints_NullArray_DoesNotThrow()
    {
        using var m = new Matrix();
        m.Translate(5f, 5f);
        PointF[]? pts = null;
        m.TransformPoints(pts!); // should be a no-op
    }

    [Fact]
    public void Constructor_ExplicitElements_SetsCorrectly()
    {
        // GDI+ constructor: (M11, M12, M21, M22, dx, dy) = (1,0,0,1,10,5) = translate(10,5)
        using var m = new Matrix(1f, 0f, 0f, 1f, 10f, 5f);
        var pts = new[] { new PointF(0f, 0f) };
        m.TransformPoints(pts);
        AssertPointsEqual(new PointF(10f, 5f), pts[0]);
    }
}
