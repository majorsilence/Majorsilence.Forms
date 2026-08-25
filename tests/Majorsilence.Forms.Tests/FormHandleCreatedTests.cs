using System;
using System.Collections.Generic;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WindowBase.OnHandleCreated existed, was documented as "raised when the backend window is shown",
    // and was never called -- while the HandleCreated EVENT did fire, because that is forwarded to the
    // internal adapter control. So overriding the method looked correct, compiled, and silently did
    // nothing. It is the standard place ported code does its window-level setup once the window exists,
    // so whatever the override was responsible for simply never happened: found in a control library
    // where every popup's rendering was gated on a flag set in that override, leaving all of them blank.
    public class FormHandleCreatedTests
    {
        private const string HandleCreatedCall = "OnHandleCreated";
        private const string ShownCall = "OnShown";

        private sealed class RecordingForm : Form
        {
            public readonly List<string> Calls = [];
            public bool HandleReportedCreated;

            protected override void OnHandleCreated (EventArgs e)
            {
                Calls.Add (HandleCreatedCall);
                HandleReportedCreated = IsHandleCreated;
                base.OnHandleCreated (e);
            }

            protected override void OnShown (EventArgs e)
            {
                Calls.Add (ShownCall);
                base.OnShown (e);
            }
        }

        [Fact]
        public void Showing_a_form_raises_OnHandleCreated ()
        {
            using var form = new RecordingForm ();
            form.Show ();

            Assert.Contains (HandleCreatedCall, form.Calls);
        }

        [Fact]
        public void OnHandleCreated_precedes_OnShown ()
        {
            using var form = new RecordingForm ();
            form.Show ();

            // The WinForms order: the handle exists before the form is announced as shown.
            Assert.Equal (
                [HandleCreatedCall, ShownCall],
                form.Calls);
        }

        [Fact]
        public void IsHandleCreated_is_already_true_inside_the_callback ()
        {
            using var form = new RecordingForm ();
            form.Show ();

            // Overrides check this to decide whether window-level work is safe yet, so it must not
            // report false while the notification for it is being delivered.
            Assert.True (form.HandleReportedCreated);
        }

        [Fact]
        public void OnHandleCreated_is_raised_once_across_repeated_shows ()
        {
            using var form = new RecordingForm ();
            form.Show ();
            form.Show ();

            Assert.Single (form.Calls.FindAll (c => c == HandleCreatedCall));
        }
    }
}
