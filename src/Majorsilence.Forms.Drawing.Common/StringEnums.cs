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

namespace Majorsilence.Forms.Drawing
{
    /// <summary>Specifies how digits are substituted according to a locale or language. Matches <c>System.Drawing.StringDigitSubstitute</c>, including its numeric values.</summary>
    public enum StringDigitSubstitute
    {
        /// <summary>User.</summary>
        User = 0,
        /// <summary>None.</summary>
        None = 1,
        /// <summary>National.</summary>
        National = 2,
        /// <summary>Traditional.</summary>
        Traditional = 3,
    }

    /// <summary>Specifies the unit of measure used for a text layout rectangle. Matches <c>System.Drawing.StringUnit</c>, including its numeric values.</summary>
    public enum StringUnit
    {
        /// <summary>World.</summary>
        World = 0,
        /// <summary>Display.</summary>
        Display = 1,
        /// <summary>Pixel.</summary>
        Pixel = 2,
        /// <summary>Point.</summary>
        Point = 3,
        /// <summary>Inch.</summary>
        Inch = 4,
        /// <summary>Document.</summary>
        Document = 5,
        /// <summary>Millimeter.</summary>
        Millimeter = 6,
        /// <summary>Em.</summary>
        Em = 32,
    }
}
