using Majorsilence.Forms.Printing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Pins the numeric values of the printing enums corrected in Phase 2 of docs/gdi-gap-plan.md.
    ///
    /// All three of these enums previously used implicit values (0, 1, 2, ...) that happened not to match
    /// GDI+. That is invisible at compile time and only shows up as a wrong paper size, tray, or
    /// resolution once a value has been persisted and read back — the exact silent-corruption failure the
    /// value check in tools/Majorsilence.Forms.GdiDiff now guards against wholesale.
    /// </summary>
    public class PrintingEnumValueTests
    {
        [Theory]
        [InlineData (PaperKind.Custom, 0)]
        [InlineData (PaperKind.Letter, 1)]
        [InlineData (PaperKind.Tabloid, 3)]
        [InlineData (PaperKind.Ledger, 4)]
        [InlineData (PaperKind.Legal, 5)]      // was 2
        [InlineData (PaperKind.A3, 8)]         // was 4
        [InlineData (PaperKind.A4, 9)]         // was 3
        public void PaperKind_values_match_GDI_plus (PaperKind kind, int expected)
            => Assert.Equal (expected, (int)kind);

        [Fact]
        public void PaperKind_values_are_unique ()
        {
            // The pre-Phase-2 implicit numbering collided once the full upstream set was added
            // (A4 was 3, the same as Tabloid). Aliases are not expected in this enum, unlike RotateFlipType.
            var values = Enum.GetValues<PaperKind> ().Select (v => (int)v).ToArray ();
            Assert.Equal (values.Length, values.Distinct ().Count ());
        }

        [Theory]
        [InlineData (PaperSourceKind.Upper, 1)]
        [InlineData (PaperSourceKind.Lower, 2)]
        [InlineData (PaperSourceKind.Middle, 3)]
        [InlineData (PaperSourceKind.Manual, 4)]           // was 3
        [InlineData (PaperSourceKind.Envelope, 5)]         // was 4
        [InlineData (PaperSourceKind.AutomaticFeed, 7)]    // was 0
        [InlineData (PaperSourceKind.Custom, 257)]         // was 5
        public void PaperSourceKind_values_match_GDI_plus (PaperSourceKind kind, int expected)
            => Assert.Equal (expected, (int)kind);

        [Theory]
        [InlineData (PrinterResolutionKind.High, -4)]
        [InlineData (PrinterResolutionKind.Medium, -3)]
        [InlineData (PrinterResolutionKind.Low, -2)]
        [InlineData (PrinterResolutionKind.Draft, -1)]
        [InlineData (PrinterResolutionKind.Custom, 0)]
        public void PrinterResolutionKind_values_match_GDI_plus (PrinterResolutionKind kind, int expected)
        {
            // GDI+ numbers these negatively (they are Win32 DMRES_* sentinels) with Custom at zero;
            // they were previously 0..4.
            Assert.Equal (expected, (int)kind);
        }

        [Fact]
        public void PrintRange_CurrentPage_matches_GDI_plus ()
            => Assert.Equal (4194304, (int)PrintRange.CurrentPage);
    }
}
