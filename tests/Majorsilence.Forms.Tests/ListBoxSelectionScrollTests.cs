using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A selection set in code is scrolled into view the least distance, as upstream's LB_SETCURSEL and LB_SETSEL leave it.
    // Measured on WinForms (.NET 10), a ten-row list of forty items: selected at 25 before it is shown it opens with 16 at
    // the top; set to 30 once shown, 21; back to 2, 2; a MultiExtended list set to 30 shows 21, and SetSelected (35) 26.
    [Collection ("Headless")]
    public class ListBoxSelectionScrollTests
    {
        private static ListBox Make (Form form, int x, SelectionMode mode)
        {
            var list = new ListBox { Location = new Point (x, 10), Size = new Size (110, 160), SelectionMode = mode };
            for (var i = 0; i < 40; i++)
                list.Items.Add ("Item " + i);
            form.Controls.Add (list);
            return list;
        }

        [Fact]
        public void A_selection_set_in_code_is_scrolled_into_view ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new Size (500, 400) };
            var before = Make (form, 10, SelectionMode.One);
            before.SelectedIndex = 25;
            var after = Make (form, 130, SelectionMode.One);
            var multi = Make (form, 250, SelectionMode.MultiExtended);
            form.Show ();

            var rows = before.VisibleItemCount;
            Assert.InRange (rows, 2, 39);

            Assert.Equal (25 - rows + 1, before.TopIndex);

            after.SelectedIndex = 30;
            Assert.Equal (30 - rows + 1, after.TopIndex);

            after.SelectedIndex = 2;
            Assert.Equal (2, after.TopIndex);

            multi.SelectedIndex = 30;
            Assert.Equal (30 - rows + 1, multi.TopIndex);

            multi.SetSelected (35, true);
            Assert.Equal (35 - rows + 1, multi.TopIndex);
        }
    }
}
