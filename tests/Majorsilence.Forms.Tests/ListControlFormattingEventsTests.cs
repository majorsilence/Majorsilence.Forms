using System;
using System.Globalization;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the dead-event sweep (RC-5). ListControl declared FormattingEnabledChanged,
    // FormatInfoChanged and FormatStringChanged behind a `#pragma warning disable CS0067` -- the
    // compiler noticed nothing raised them and the warning was suppressed rather than the events
    // wired. A data-bound control watching for a formatting change never heard about one.
    //
    // dotnet/winforms' ListControl raises each from its own setter, in the same shape: compare, store,
    // RefreshItems (), raise. There is no RefreshItems on this ListControl, and the three properties
    // are also unread by the display path here -- a separate gap that stays in the stored-only
    // baseline. The event is the half this finding is about, and it stands on its own: it is what a
    // binding layer subscribes to.
    public class ListControlFormattingEventsTests
    {
        // ListControl is abstract; ComboBox is the concrete one every data-binding test uses.
        private static ComboBox Control () => new ();

        [Fact]
        public void FormattingEnabled_notifies_on_change ()
        {
            var control = Control ();
            var raised = 0;
            control.FormattingEnabledChanged += (_, _) => raised++;

            control.FormattingEnabled = true;

            Assert.Equal (1, raised);
            Assert.True (control.FormattingEnabled);
        }

        [Fact]
        public void FormatString_notifies_on_change ()
        {
            var control = Control ();
            var raised = 0;
            control.FormatStringChanged += (_, _) => raised++;

            control.FormatString = "C2";

            Assert.Equal (1, raised);
            Assert.Equal ("C2", control.FormatString);
        }

        [Fact]
        public void FormatInfo_notifies_on_change ()
        {
            var control = Control ();
            var raised = 0;
            control.FormatInfoChanged += (_, _) => raised++;

            control.FormatInfo = CultureInfo.InvariantCulture;

            Assert.Equal (1, raised);
            Assert.Same (CultureInfo.InvariantCulture, control.FormatInfo);
        }

        // Re-assigning the same value is not a change. Without this the event fires on every
        // designer-generated assignment, which is the opposite of useful for a handler that rebinds.
        [Theory]
        [InlineData ("FormattingEnabled")]
        [InlineData ("FormatString")]
        [InlineData ("FormatInfo")]
        public void Re_assigning_the_same_value_does_not_notify (string which)
        {
            var control = Control ();
            var raised = 0;

            switch (which) {
                case "FormattingEnabled":
                    control.FormattingEnabled = true;
                    control.FormattingEnabledChanged += (_, _) => raised++;
                    control.FormattingEnabled = true;
                    break;

                case "FormatString":
                    control.FormatString = "C2";
                    control.FormatStringChanged += (_, _) => raised++;
                    control.FormatString = "C2";
                    break;

                default:
                    control.FormatInfo = CultureInfo.InvariantCulture;
                    control.FormatInfoChanged += (_, _) => raised++;
                    control.FormatInfo = CultureInfo.InvariantCulture;
                    break;
            }

            Assert.Equal (0, raised);
        }

        // Upstream collapses null to empty BEFORE comparing (`value ??= string.Empty`), so assigning
        // null to a format string that is already empty is not a change -- and the property never
        // returns null, whatever is assigned.
        [Fact]
        public void FormatString_treats_null_as_empty ()
        {
            var control = Control ();
            var raised = 0;
            control.FormatStringChanged += (_, _) => raised++;

            control.FormatString = null!;

            Assert.Equal (string.Empty, control.FormatString);
            Assert.Equal (0, raised);

            control.FormatString = "C2";
            control.FormatString = null!;

            Assert.Equal (string.Empty, control.FormatString);
            Assert.Equal (2, raised);
        }

        // The three are independent: setting one must not fire the others, which is the mistake a
        // shared raiser would make.
        [Fact]
        public void The_three_events_are_independent ()
        {
            var control = Control ();
            var order = string.Empty;

            control.FormattingEnabledChanged += (_, _) => order += "E";
            control.FormatStringChanged += (_, _) => order += "S";
            control.FormatInfoChanged += (_, _) => order += "I";

            control.FormattingEnabled = true;
            control.FormatString = "C2";
            control.FormatInfo = CultureInfo.InvariantCulture;

            Assert.Equal ("ESI", order);
        }
    }
}
