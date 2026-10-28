using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // An editable combo selects its text however it gets the focus, a click included. Measured on WinForms
    // (.NET 10) with a DropDown combo showing "Overview" and a text box focused: a press in the combo's text,
    // a press past the text and a press on the drop-down button each left SelectionStart 0 and
    // SelectionLength 8, as tabbing in did. A press once the combo had the focus put the caret where it
    // landed (SelectionStart 2, SelectionLength 0). Picking "Failures" from the open list, or pressing Up in
    // the focused combo with the caret placed, left the new text selected -- after SelectedIndexChanged,
    // during which nothing was selected yet.
    [Collection ("Headless")]
    public class ComboBoxClickFocusSelectionTests
    {
        private static (Form form, ComboBox combo) Build ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { Size = new System.Drawing.Size (400, 300) };
            var box = new TextBox { TabIndex = 0, Left = 10, Top = 10 };
            var combo = new ComboBox { TabIndex = 1, Left = 10, Top = 50, Width = 230 };
            combo.Items.AddRange (new object[] { "Overview", "Failures" });
            form.Controls.Add (box);
            form.Controls.Add (combo);
            form.Show ();
            combo.SelectedIndex = 0;
            box.Select ();
            return (form, combo);
        }

        [Theory]
        [InlineData (12)]    // in the text
        [InlineData (150)]   // past the text
        public void A_click_that_focuses_the_combo_selects_its_text (int x)
        {
            var (form, combo) = Build ();

            using (form) {
                HeadlessInput.Click (form, WindowPoint.In (combo, x, combo.Height / 2));

                Assert.True (combo.Focused || combo.EditRegion.Focused);
                Assert.Equal (0, combo.SelectionStart);
                Assert.Equal (8, combo.SelectionLength);
            }
        }

        [Fact]
        public void A_press_on_the_drop_down_button_that_focuses_the_combo_selects_its_text ()
        {
            var (form, combo) = Build ();

            using (form) {
                HeadlessInput.Click (form, WindowPoint.In (combo, combo.Width - 8, combo.Height / 2));
                combo.DroppedDown = false;

                Assert.Equal (0, combo.SelectionStart);
                Assert.Equal (8, combo.SelectionLength);
            }
        }

        [Fact]
        public void A_pick_from_the_list_selects_the_picked_text ()
        {
            var (form, combo) = Build ();

            using (form) {
                HeadlessInput.Click (form, WindowPoint.In (combo, combo.Width - 8, combo.Height / 2));
                Assert.True (combo.DroppedDown);
                var during = -1;
                combo.SelectedIndexChanged += (s, e) => during = combo.SelectionLength;

                var list = combo.PopupListBox;
                var item = list.GetItemRectangle (1);
                var point = new System.Drawing.Point (item.Left + 10, item.Top + item.Height / 2);
                list.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, point.X, point.Y, 0));
                list.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, point.X, point.Y, 0));

                Assert.Equal ("Failures", combo.Text);
                Assert.False (combo.DroppedDown);
                Assert.Equal (0, during);
                Assert.Equal (0, combo.SelectionStart);
                Assert.Equal (8, combo.SelectionLength);
            }
        }

        [Fact]
        public void An_arrow_key_in_a_focused_combo_selects_the_new_text ()
        {
            var (form, combo) = Build ();

            using (form) {
                combo.SelectedIndex = 1;
                HeadlessInput.Click (form, WindowPoint.In (combo, 150, combo.Height / 2));
                HeadlessInput.Click (form, WindowPoint.In (combo, 12, combo.Height / 2));
                Assert.Equal (0, combo.SelectionLength);

                combo.EditRegion.RaiseKeyDown (new KeyEventArgs (Keys.Up));
                combo.EditRegion.RaiseKeyUp (new KeyEventArgs (Keys.Up));

                Assert.Equal ("Overview", combo.Text);
                Assert.Equal (0, combo.SelectionStart);
                Assert.Equal (8, combo.SelectionLength);
            }
        }

        [Fact]
        public void A_click_in_a_focused_combo_places_the_caret ()
        {
            var (form, combo) = Build ();

            using (form) {
                HeadlessInput.Click (form, WindowPoint.In (combo, 150, combo.Height / 2));

                // Away from the first press, so the two are not taken for a double click.
                HeadlessInput.Click (form, WindowPoint.In (combo, 12, combo.Height / 2));

                Assert.Equal (0, combo.SelectionLength);
                Assert.InRange (combo.SelectionStart, 0, 2);
            }
        }
    }
}
