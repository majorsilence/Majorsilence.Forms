using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Pins the numeric values of WinForms enums that were found to disagree with
    /// <c>System.Windows.Forms</c> — see docs/winforms-gap-plan.md.
    ///
    /// These values are API, not implementation detail. Designer-generated code and <c>.resx</c>
    /// resources persist them as raw integers, so a wrong number is not a compile error but a silently
    /// different meaning on round-trip. <c>tools/Majorsilence.Forms.ApiDiff</c> now checks all 1,600-odd
    /// enum members mechanically on every CI run; this file names the ones that were actually wrong, so
    /// a regression identifies itself instead of appearing as an anonymous VALUE line.
    /// </summary>
    public class WinFormsEnumValueTests
    {
        // AccessibleRole.Default and None were transposed: Default was 0 and None -1.
        [Fact]
        public void AccessibleRole_Default_and_None_are_not_transposed ()
        {
            Assert.Equal (-1, (int)AccessibleRole.Default);
            Assert.Equal (0, (int)AccessibleRole.None);
        }

        // CloseReason.FormOwnerClosing and ApplicationExitCall were transposed, and
        // TaskManagerClosing was 1 instead of 4. FormClosingEventArgs.CloseReason is commonly
        // switched on, so a wrong number here changes which branch a shutdown handler takes.
        [Theory]
        [InlineData (CloseReason.None, 0)]
        [InlineData (CloseReason.WindowsShutDown, 1)]
        [InlineData (CloseReason.MdiFormClosing, 2)]
        [InlineData (CloseReason.UserClosing, 3)]
        [InlineData (CloseReason.TaskManagerClosing, 4)]
        [InlineData (CloseReason.FormOwnerClosing, 5)]
        [InlineData (CloseReason.ApplicationExitCall, 6)]
        public void CloseReason_values_match_WinForms (CloseReason reason, int expected)
            => Assert.Equal (expected, (int)reason);

        // AutoCompleteSource is a flags-style enum upstream (None = 128, CustomSource = 64); ours
        // numbered it sequentially from zero, so every persisted value read back as a different member.
        [Theory]
        [InlineData (AutoCompleteSource.FileSystem, 1)]
        [InlineData (AutoCompleteSource.HistoryList, 2)]
        [InlineData (AutoCompleteSource.RecentlyUsedList, 4)]
        [InlineData (AutoCompleteSource.AllUrl, 6)]
        [InlineData (AutoCompleteSource.AllSystemSources, 7)]
        [InlineData (AutoCompleteSource.FileSystemDirectories, 32)]
        [InlineData (AutoCompleteSource.CustomSource, 64)]
        [InlineData (AutoCompleteSource.None, 128)]
        public void AutoCompleteSource_values_match_WinForms (AutoCompleteSource source, int expected)
            => Assert.Equal (expected, (int)source);

        [Theory]
        [InlineData (Border3DStyle.Raised, 5)]
        [InlineData (Border3DStyle.Bump, 9)]
        [InlineData (Border3DStyle.Flat, 16394)]
        public void Border3DStyle_values_match_WinForms (Border3DStyle style, int expected)
            => Assert.Equal (expected, (int)style);

        [Theory]
        [InlineData (DateTimePickerFormat.Long, 1)]
        [InlineData (DateTimePickerFormat.Short, 2)]
        [InlineData (DateTimePickerFormat.Time, 4)]
        [InlineData (DateTimePickerFormat.Custom, 8)]
        public void DateTimePickerFormat_values_match_WinForms (DateTimePickerFormat format, int expected)
            => Assert.Equal (expected, (int)format);

        [Fact]
        public void Enum_members_that_upstream_does_not_have_kept_their_values ()
        {
            // The fix had to pin these explicitly. Left implicit, they would have shifted the moment
            // the members above them gained explicit numbers -- which is exactly the silent
            // renumbering the whole exercise is about, and is what a first attempt did.
            Assert.Equal (0, (int)CloseReason.None);
            Assert.Equal (0, (int)AccessibleRole.None);
        }
    }
}
