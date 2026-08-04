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

namespace Majorsilence.Forms.Drawing.Text
{
    /// <summary>Specifies a generic font family to resolve rather than a specific installed typeface. Matches <c>System.Drawing.Text.GenericFontFamilies</c>, including its numeric values.</summary>
    public enum GenericFontFamilies
    {
        /// <summary>Serif.</summary>
        Serif = 0,
        /// <summary>Sans serif.</summary>
        SansSerif = 1,
        /// <summary>Monospace.</summary>
        Monospace = 2,
    }
}
