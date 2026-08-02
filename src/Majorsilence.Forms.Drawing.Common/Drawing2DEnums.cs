// Data-only enums completed from upstream System.Drawing.Common as Phase 2 of docs/gdi-gap-plan.md.
//
// These carry no behavior: they exist so migrated code that references them compiles, and so the
// numeric values round-trip. The values are generated from the real assembly rather than transcribed,
// because designer-serialized and .resx code persists them as raw integers -- a wrong number here is a
// silent data-corruption bug, not a compile error.
//
// Members whose behavior is not yet honored by the rendering path are still declared: per the stub
// policy in COMPATIBILITY_MATRIX.md, migrated code should compile and run, and an enum value that is
// merely stored is exactly that policy applied to data.

namespace Majorsilence.Forms.Drawing.Drawing2D
{
    /// <summary>Specifies the shape used on both ends of each dash in a dashed line. Matches <c>System.Drawing.Drawing2D.DashCap</c>, including its numeric values.</summary>
    public enum DashCap
    {
        /// <summary>Flat.</summary>
        Flat = 0,
        /// <summary>Round.</summary>
        Round = 2,
        /// <summary>Triangle.</summary>
        Triangle = 3,
    }

    /// <summary>Specifies the type of fill a <see cref="Pen"/> uses. Matches <c>System.Drawing.Drawing2D.PenType</c>, including its numeric values.</summary>
    public enum PenType
    {
        /// <summary>Solid color.</summary>
        SolidColor = 0,
        /// <summary>Hatch fill.</summary>
        HatchFill = 1,
        /// <summary>Texture fill.</summary>
        TextureFill = 2,
        /// <summary>Path gradient.</summary>
        PathGradient = 3,
        /// <summary>Linear gradient.</summary>
        LinearGradient = 4,
    }

    /// <summary>Specifies the overall quality of rendering operations. Matches <c>System.Drawing.Drawing2D.QualityMode</c>, including its numeric values.</summary>
    public enum QualityMode
    {
        /// <summary>Invalid.</summary>
        Invalid = -1,
        /// <summary>Default.</summary>
        Default = 0,
        /// <summary>Low.</summary>
        Low = 1,
        /// <summary>High.</summary>
        High = 2,
    }

    /// <summary>Specifies the coordinate system a transform applies to. Matches <c>System.Drawing.Drawing2D.CoordinateSpace</c>, including its numeric values.</summary>
    public enum CoordinateSpace
    {
        /// <summary>World.</summary>
        World = 0,
        /// <summary>Page.</summary>
        Page = 1,
        /// <summary>Device.</summary>
        Device = 2,
    }

    /// <summary>Specifies the type of warp transformation applied to a path. Matches <c>System.Drawing.Drawing2D.WarpMode</c>, including its numeric values.</summary>
    public enum WarpMode
    {
        /// <summary>Perspective.</summary>
        Perspective = 0,
        /// <summary>Bilinear.</summary>
        Bilinear = 1,
    }

    /// <summary>Specifies whether queued drawing commands are flushed, and whether to wait for them to finish. Matches <c>System.Drawing.Drawing2D.FlushIntention</c>, including its numeric values.</summary>
    public enum FlushIntention
    {
        /// <summary>Flush.</summary>
        Flush = 0,
        /// <summary>Sync.</summary>
        Sync = 1,
    }
}
