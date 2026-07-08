using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: closing a shown Form raises FormClosing (cancellable) then FormClosed, once each,
    // in that order -- for ordinary (non-dialog, non-MDI) forms too, not only dialogs. A handler cancelling
    // in FormClosing aborts the close and FormClosed must not fire.
    public class FormClosingClosedOrderTests
    {
        [Fact]
        public void Close_raises_FormClosing_then_FormClosed_once ()
        {
            var order = new List<string> ();
            using var form = new Form { Width = 200, Height = 150 };
            form.FormClosing += (s, e) => order.Add ("FormClosing");
            form.FormClosed += (s, e) => order.Add ("FormClosed");

            form.Show ();
            form.Close ();

            Assert.Equal (new[] { "FormClosing", "FormClosed" }, order);
        }

        [Fact]
        public void FormClosing_cancel_aborts_close_and_suppresses_FormClosed ()
        {
            var closedFired = false;
            using var form = new Form { Width = 200, Height = 150 };
            form.FormClosing += (s, e) => e.Cancel = true;
            form.FormClosed += (s, e) => closedFired = true;

            form.Show ();
            form.Close ();

            Assert.False (closedFired);
        }
    }
}
