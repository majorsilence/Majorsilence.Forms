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

        // AccessibleEvents was missing 32 of upstream's 42 members. The values are Win32 WinEvent
        // codes, and they are not contiguous with the low block: the System* events run 1..23 while
        // the object events start at 0x8000, so anything that filled the gap by counting upward from
        // the last member it happened to have would be wrong for every value after it.
        [Theory]
        [InlineData (AccessibleEvents.SystemSound, 0x0001)]
        [InlineData (AccessibleEvents.SystemMinimizeEnd, 0x0017)]
        [InlineData (AccessibleEvents.Create, 0x8000)]
        [InlineData (AccessibleEvents.Reorder, 0x8004)]
        [InlineData (AccessibleEvents.SelectionWithin, 0x8009)]
        [InlineData (AccessibleEvents.DescriptionChange, 0x800D)]
        [InlineData (AccessibleEvents.ParentChange, 0x800F)]
        [InlineData (AccessibleEvents.AcceleratorChange, 0x8012)]
        public void AccessibleEvents_values_are_the_Win32_WinEvent_codes (AccessibleEvents value, int expected)
            => Assert.Equal (expected, (int)value);

        [Fact]
        public void AccessibleEvents_object_events_are_contiguous_from_Create ()
        {
            // Guards the splice itself: 19 consecutive codes from EVENT_OBJECT_CREATE, in order.
            AccessibleEvents[] ordered = [
                AccessibleEvents.Create, AccessibleEvents.Destroy, AccessibleEvents.Show,
                AccessibleEvents.Hide, AccessibleEvents.Reorder, AccessibleEvents.Focus,
                AccessibleEvents.Selection, AccessibleEvents.SelectionAdd,
                AccessibleEvents.SelectionRemove, AccessibleEvents.SelectionWithin,
                AccessibleEvents.StateChange, AccessibleEvents.LocationChange,
                AccessibleEvents.NameChange, AccessibleEvents.DescriptionChange,
                AccessibleEvents.ValueChange, AccessibleEvents.ParentChange,
                AccessibleEvents.HelpChange, AccessibleEvents.DefaultActionChange,
                AccessibleEvents.AcceleratorChange,
            ];

            for (var i = 0; i < ordered.Length; i++)
                Assert.Equal (0x8000 + i, (int)ordered[i]);
        }
    }
}
