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

namespace Majorsilence.Forms.Printing
{
    /// <summary>Specifies the kind of print operation that is occurring. Matches <c>System.Drawing.Printing.PrintAction</c>, including its numeric values.</summary>
    public enum PrintAction
    {
        /// <summary>Print to file.</summary>
        PrintToFile = 0,
        /// <summary>Print to preview.</summary>
        PrintToPreview = 1,
        /// <summary>Print to printer.</summary>
        PrintToPrinter = 2,
    }

    /// <summary>Specifies the units of measure used for printing. Matches <c>System.Drawing.Printing.PrinterUnit</c>, including its numeric values.</summary>
    public enum PrinterUnit
    {
        /// <summary>Display.</summary>
        Display = 0,
        /// <summary>Thousandths of an inch.</summary>
        ThousandthsOfAnInch = 1,
        /// <summary>Hundredths of a millimeter.</summary>
        HundredthsOfAMillimeter = 2,
        /// <summary>Tenths of a millimeter.</summary>
        TenthsOfAMillimeter = 3,
    }
}
