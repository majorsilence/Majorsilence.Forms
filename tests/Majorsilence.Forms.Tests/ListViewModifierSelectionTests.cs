using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Issue #96 (LST-27): ListView.OnMouseClick implements Ctrl-toggle and Shift-range selection, and
    // no test drove either branch -- W5.6 delivered the code and covered only the programmatic path.
    //
    // The issue expected this to be untestable as written -- both branches read the static
    // Control.ModifierKeys, and MouseEventArgs' constructor assigns that static from its own keyData.
    // That turns out to be the wrong way round: because the constructor ASSIGNS the static, passing
    // `keyData: Keys.Control` leaves the static agreeing with the args, so the original code was
    // drivable all along. Five of the tests below pass against either source and are the coverage the
    // issue actually asked for.
    //
    // Reading e.Modifiers is still the better question -- the modifiers belonging to THIS click rather
    // than whatever is held down when the handler runs -- and exactly one test here separates the two,
    // by resetting the static after building the args. That is the shape of a queued or replayed
    // event, and it is the only reason the change is worth making.
    [Collection ("Headless")]
    public class ListViewModifierSelectionTests
    {
        private static ListView Populated ()
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 260, Height = 200, View = View.Details, MultiSelect = true };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });

            foreach (var text in new[] { "one", "two", "three", "four" })
                view.Items.Add (new ListViewItem (text));

            return view;
        }

        // The centre of an item, in the logical units a mouse handler receives.
        private static Point Centre (ListView view, int index)
        {
            var bounds = view.Items[index].Bounds;

            return new Point (bounds.Left + 20, bounds.Top + bounds.Height / 2);
        }

        private static ListView Shown (out Form form)
        {
            var view = Populated ();

            form = new Form { Width = 400, Height = 300 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        [Fact]
        public void A_plain_click_selects_only_that_item ()
        {
            using var view = Shown (out var form);

            try {
                view.DriveClick (Centre (view, 1));
                view.DriveClick (Centre (view, 2));

                Assert.Single (view.SelectedItems);
                Assert.Same (view.Items[2], view.SelectedItems[0]);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Ctrl_click_adds_to_the_selection ()
        {
            using var view = Shown (out var form);

            try {
                view.DriveClick (Centre (view, 0));
                view.DriveClick (Centre (view, 2), Keys.Control);

                Assert.Equal (2, view.SelectedItems.Count);
                Assert.True (view.Items[0].Selected);
                Assert.True (view.Items[2].Selected);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Ctrl_click_on_a_selected_item_removes_it ()
        {
            // It toggles. Add-only would leave a Ctrl-click unable to deselect, which is half the
            // gesture.
            using var view = Shown (out var form);

            try {
                view.DriveClick (Centre (view, 0));
                view.DriveClick (Centre (view, 2), Keys.Control);
                view.DriveClick (Centre (view, 2), Keys.Control);

                Assert.Single (view.SelectedItems);
                Assert.True (view.Items[0].Selected);
                Assert.False (view.Items[2].Selected);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Shift_click_selects_the_range_from_the_anchor ()
        {
            using var view = Shown (out var form);

            try {
                view.DriveClick (Centre (view, 0));
                view.DriveClick (Centre (view, 2), Keys.Shift);

                Assert.Equal (3, view.SelectedItems.Count);
                Assert.True (view.Items[0].Selected);
                Assert.True (view.Items[1].Selected);
                Assert.True (view.Items[2].Selected);
                Assert.False (view.Items[3].Selected);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Shift_click_extends_backwards_too ()
        {
            using var view = Shown (out var form);

            try {
                view.DriveClick (Centre (view, 3));
                view.DriveClick (Centre (view, 1), Keys.Shift);

                Assert.Equal (3, view.SelectedItems.Count);
                Assert.True (view.Items[1].Selected);
                Assert.True (view.Items[3].Selected);
                Assert.False (view.Items[0].Selected);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_click_uses_its_own_modifiers_not_whatever_is_held_down_now ()
        {
            // The only test here that separates e.Modifiers from the static Control.ModifierKeys, and
            // the reason the change is worth making.
            //
            // MouseEventArgs' constructor ASSIGNS the static from its own keyData, so simply passing
            // Keys.Control leaves the static agreeing with the args and either source gives the same
            // answer. Constructing a second, modifier-less args afterwards resets the static to None
            // while the first args still carries Control -- which is exactly the shape of a real
            // queued or replayed event: the modifiers that belong to the click are not the ones held
            // down by the time it is handled.
            using var view = Shown (out var form);

            try {
                view.DriveClick (Centre (view, 0));

                var centre = Centre (view, 2);
                var ctrlClick = new MouseEventArgs (MouseButtons.Left, 1, centre.X, centre.Y, Point.Empty, keyData: Keys.Control);

                // Resets the static to None; ctrlClick still says Control.
                _ = new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty);

                Assert.Equal (Keys.None, Control.ModifierKeys);
                Assert.Equal (Keys.Control, ctrlClick.Modifiers);

                view.DriveClick (ctrlClick);

                Assert.Equal (2, view.SelectedItems.Count);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Neither_modifier_extends_the_selection_when_MultiSelect_is_off ()
        {
            // What MultiSelect = false is for (LST-17): both branches are gated on it, and a single-
            // select list that could be Ctrl-extended would be broken in a way no handler could undo.
            using var view = Shown (out var form);
            view.MultiSelect = false;

            try {
                view.DriveClick (Centre (view, 0));
                view.DriveClick (Centre (view, 2), Keys.Control);

                Assert.Single (view.SelectedItems);
                Assert.Same (view.Items[2], view.SelectedItems[0]);
            } finally {
                form.Close ();
            }
        }
    }
}
