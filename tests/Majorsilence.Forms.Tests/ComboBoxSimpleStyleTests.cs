using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LST-07's last remainder: ComboBoxStyle.Simple. Upstream's simple combo shows its list all the time,
    // as a child window under the edit control, inside the combo's own bounds (ComboBox/ComboBox.cs,
    // OnHandleCreated and ChildWndProc). Here the items live on the drop-down's ListBox, which sat in a
    // PopupWindow whatever the style, so a Simple combo looked and behaved like a DropDown one: no list
    // on screen, and Alt+Down opened a popup. The same list is now hosted inside the control while the
    // style is Simple, and moved back into the popup when it is not.
    [Collection ("Headless")]
    public class ComboBoxSimpleStyleTests
    {
        private static ComboBox Simple (out Form form, bool show = true)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 400, Height = 400 };
            var combo = new ComboBox { Left = 10, Top = 10, Width = 160 };

            foreach (var item in new[] { "apple", "apricot", "banana", "cherry", "damson" })
                combo.Items.Add (item);

            form.Controls.Add (combo);
            combo.DropDownStyle = ComboBoxStyle.Simple;

            if (show)
                form.Show ();

            return combo;
        }

        private static ListBox List (ComboBox combo) => combo.PopupListBox;

        private static Point RowCentre (ListBox list, int index)
        {
            var row = list.GetItemRectangle (index);

            return new Point (row.Left + (row.Width / 2), row.Top + (row.Height / 2));
        }

        [Fact]
        public void The_list_is_inside_the_control_under_the_edit_region ()
        {
            var combo = Simple (out var form);

            using (form) {
                var list = List (combo);

                Assert.Same (combo, list.Parent);
                Assert.True (list.Visible);
                Assert.DoesNotContain (list, combo.Controls.Cast<Control> ());   // implicit, as the edit region is

                var edit = combo.EditRegion.Bounds;
                var inside = new Rectangle (Point.Empty, combo.Size);

                Assert.True (inside.Contains (list.Bounds), $"list {list.Bounds} outside {inside}");
                Assert.True (list.Top >= edit.Bottom - 1, $"list {list.Bounds} overlaps the edit region {edit}");
                Assert.True (list.Height > 2 * list.ItemHeight, "the list has no room for its rows");

                // One line of text at the top, as the drop-down styles have, and the full width: there
                // is no drop-down button to make room for.
                Assert.True (edit.Height < combo.PreferredHeightOfOneLine ());
                Assert.True (edit.Right > combo.Width - ComboBox.DropDownGlyphWidth);
            }
        }

        [Fact]
        public void The_items_are_drawn_without_opening_anything ()
        {
            var combo = Simple (out var form);

            using (form) {
                using var with_items = PaintSurface.Render (combo);

                var list = List (combo);
                var row = list.GetItemRectangle (1);
                var area = new Rectangle (list.Left + row.Left, list.Top + row.Top, row.Width, row.Height);

                combo.Items.Clear ();
                using var empty = PaintSurface.Render (combo);

                var scale = combo.Scaling;
                var differing = 0;

                for (var x = (int)(area.Left * scale); x < (int)(area.Right * scale); x++)
                    for (var y = (int)(area.Top * scale); y < (int)(area.Bottom * scale); y++)
                        if (with_items.GetPixel (x, y) != empty.GetPixel (x, y))
                            differing++;

                Assert.True (differing > 0, $"the second row's text is not drawn inside the control");
                Assert.False (combo.DroppedDown);
            }
        }

        [Fact]
        public void DroppedDown_stays_false_and_opens_no_popup ()
        {
            var combo = Simple (out var form);

            using (form) {
                var opened = 0;
                combo.DropDown += (_, _) => opened++;

                combo.DroppedDown = true;
                combo.RaiseKeyDown (new KeyEventArgs (Keys.Down | Keys.Alt));
                combo.RaiseKeyUp (new KeyEventArgs (Keys.Down | Keys.Alt));

                Assert.False (combo.DroppedDown);
                Assert.Equal (0, opened);
                Assert.DoesNotContain (PopupWindow.ShownPopups, p => ReferenceEquals (p.ParentWindow, form));
                Assert.Same (combo, List (combo).Parent);
            }
        }

        [Fact]
        public void Down_selects_in_the_visible_list_and_commits_in_upstreams_order ()
        {
            // A guard: the keyboard path already reached this list through NavigateList while it sat
            // in the popup, so no earlier version could fail this. It pins upstream's Simple-style
            // order now that the list is on screen.
            var combo = Simple (out var form);

            using (form) {
                combo.SelectedIndex = 0;

                var order = string.Empty;
                combo.SelectionChangeCommitted += (_, _) => order += "commit ";
                combo.TextChanged += (_, _) => order += "text ";
                combo.SelectedIndexChanged += (_, _) => order += "index ";

                combo.RaiseKeyDown (new KeyEventArgs (Keys.Down));
                combo.RaiseKeyUp (new KeyEventArgs (Keys.Down));

                // Simple, Down arrow: CBN_SELENDOK, then CBN_SELCHANGE -- the text, then the index
                // (the notification table above upstream's WmReflectCommand).
                Assert.Equal ("commit text index ", order);
                Assert.Equal (1, combo.SelectedIndex);
                Assert.Equal (1, List (combo).SelectedIndex);
                Assert.Equal ("apricot", combo.Text);
                Assert.Equal ("apricot", combo.EditRegion.Text);
                Assert.False (combo.DroppedDown);
            }
        }

        [Fact]
        public void A_click_in_the_list_selects_commits_and_leaves_the_caret_in_the_edit_region ()
        {
            var combo = Simple (out var form);

            using (form) {
                var list = List (combo);
                var order = string.Empty;
                combo.SelectionChangeCommitted += (_, _) => order += "commit ";
                combo.SelectedIndexChanged += (_, _) => order += "index ";

                var point = RowCentre (list, 2);

                // Selected on the press, as a list box selects; the drop-down's pick-on-release is for a
                // popup that closes under the pointer, which this list never does.
                list.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, point.X, point.Y, 0));

                Assert.Equal (2, combo.SelectedIndex);
                Assert.Equal ("commit index ", order);
                Assert.Equal ("banana", combo.Text);

                list.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, point.X, point.Y, 0));

                Assert.Equal ("commit index ", order);
                Assert.True (combo.EditRegion.Focused, "the click took the focus away from the edit region");
                Assert.False (list.Focused);
                Assert.False (combo.DroppedDown);
            }
        }

        [Fact]
        public void A_programmatic_selection_does_not_commit ()
        {
            // A guard: SelectionChangeCommitted is the user's alone, and the list's own input is now
            // marked as the user's -- this pins that code setting SelectedIndex is not.
            var combo = Simple (out var form);

            using (form) {
                var commits = 0;
                combo.SelectionChangeCommitted += (_, _) => commits++;

                combo.SelectedIndex = 3;

                Assert.Equal (0, commits);
                Assert.Equal ("cherry", combo.Text);
            }
        }

        [Fact]
        public void Height_governs_the_list_and_is_remembered_across_style_changes ()
        {
            var combo = Simple (out var form);

            using (form) {
                combo.IntegralHeight = false;

                // Upstream's DefaultSimpleStyleHeight: a combo that becomes Simple without a height of
                // its own set while Simple is 150 tall.
                Assert.Equal (150, combo.Height);

                combo.Height = 120;
                var list = List (combo);
                Assert.Equal (120, combo.Height);
                Assert.Equal (combo.Height, list.Bottom);

                var shorter = list.Height;

                combo.Height = 180;
                Assert.Equal (combo.Height, list.Bottom);
                Assert.Equal (shorter + 60, list.Height);

                // To DropDown: the list goes back to the popup, the control gets back the height it had,
                // and the drop-down opens with the same items.
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                Assert.NotSame (combo, list.Parent);
                Assert.Equal (new ComboBox ().Height, combo.Height);

                combo.DroppedDown = true;
                Assert.True (combo.DroppedDown);
                Assert.IsType<PopupWindow> (list.FindWindow ());
                Assert.Equal (5, list.Items.Count);
                combo.DroppedDown = false;

                // And back to Simple: the height set while Simple is the one it comes back with.
                combo.DropDownStyle = ComboBoxStyle.Simple;
                Assert.Same (combo, list.Parent);
                Assert.Equal (180, combo.Height);
                Assert.Equal (combo.Height, list.Bottom);
            }
        }

        [Fact]
        public void A_height_set_before_becoming_Simple_is_not_the_Simple_height ()
        {
            // Upstream stores the requested height only while the style is Simple: the designer's
            // one-line Size on a DropDown combo is not the Simple combo's list height.
            HeadlessRenderer.Use ();
            using var combo = new ComboBox { Height = 40, IntegralHeight = false };

            combo.DropDownStyle = ComboBoxStyle.Simple;

            Assert.Equal (150, combo.Height);
        }

        [Fact]
        public void IntegralHeight_snaps_the_control_so_the_list_shows_whole_items ()
        {
            var combo = Simple (out var form);

            using (form) {
                var list = List (combo);
                var item = list.ItemHeight;

                // A height between two whole rows. Set a different one first: the height the control
                // snaps to from its default can equal the target at some fonts, and a set that changes
                // nothing is skipped (as upstream's SetBounds skips it), so the request would not be kept.
                combo.Height = 300;
                combo.Height = 120 + (item / 2);

                var chrome = list.Height - list.DeviceToLogicalUnits (list.DeviceClientRectangle.Height);
                Assert.Equal (0, (list.Height - chrome) % item);
                Assert.True (combo.Height <= 120 + (item / 2));
                Assert.Equal (combo.Height, list.Bottom);

                // Turning it off gives the control back the height that was asked for.
                combo.IntegralHeight = false;
                Assert.Equal (120 + (item / 2), combo.Height);
                Assert.Equal (combo.Height, list.Bottom);
            }
        }

        [Fact]
        public void Suggest_still_works_and_hangs_its_list_under_the_edit_region ()
        {
            var combo = Simple (out var form);

            using (form) {
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
                combo.AutoCompleteMode = AutoCompleteMode.Suggest;

                combo.RaiseKeyPress (new KeyPressEventArgs ('a'));
                combo.RaiseKeyPress (new KeyPressEventArgs ('p'));

                Assert.True (combo.SuggestionsShown);
                Assert.Equal (new[] { "apple", "apricot" }, combo.Suggestions);

                // The combo's own list is untouched and still where it was.
                Assert.Equal (5, combo.Items.Count);
                Assert.Same (combo, List (combo).Parent);

                // Under the edit region, over the list -- not under the foot of the whole control.
                var popup = Assert.Single (PopupWindow.ShownPopups, p => ReferenceEquals (p.ParentWindow, form));
                var under_edit = combo.PointToScreen (new Point (1, combo.EditRegion.Bottom));
                var under_control = combo.PointToScreen (new Point (1, combo.Height));

                Assert.True (popup.Location.Y < under_control.Y, $"popup at {popup.Location}, control foot at {under_control}");
                Assert.True (popup.Location.Y >= under_edit.Y, $"popup at {popup.Location}, edit foot at {under_edit}");

                combo.RaiseKeyDown (new KeyEventArgs (Keys.Down));
                combo.RaiseKeyDown (new KeyEventArgs (Keys.Enter));
                combo.RaiseKeyUp (new KeyEventArgs (Keys.Enter));

                Assert.Equal ("apple", combo.Text);
                Assert.Equal (0, combo.SelectedIndex);
            }
        }

        [Fact]
        public void The_accessibility_DOM_presents_the_list_as_a_listbox_the_combo_box_controls ()
        {
            var combo = Simple (out var form);

            using (form) {
                combo.AccessibleName = "Fruit";
                combo.SelectedIndex = 1;

                HeadlessRenderer.CapturePng (form, form.Width, form.Height);
                var nodes = AriaDom.Build (new WindowBase[] { form });

                var owner = Assert.Single (nodes, n => ReferenceEquals (n.Source, combo));
                var list = Assert.Single (nodes, n => ReferenceEquals (n.Source, combo.PopupListBox));

                Assert.Equal ("combobox", owner.Role);
                Assert.Equal ("listbox", list.Role);
                Assert.Equal (owner.Id, list.ParentId);
                Assert.Equal (list.ElementId, owner["aria-controls"]);

                // aria-expanded says whether what it controls is displayed, which here it always is.
                Assert.Equal ("true", owner["aria-expanded"]);
                Assert.Equal ("Fruit", list["aria-label"]);

                var options = nodes.Where (n => n.ParentId == list.Id).ToList ();
                Assert.Equal (new[] { "apple", "apricot", "banana", "cherry", "damson" }, options.Select (o => o.Text));
                Assert.Equal ("true", options[1]["aria-selected"]);

                // With the keyboard on the combo, the selected option is where the arrows are.
                combo.Focus ();
                nodes = AriaDom.Build (new WindowBase[] { form });
                var active = AriaDom.ActiveNode (nodes);
                Assert.NotNull (active);
                Assert.Same (combo.PopupListBox, nodes.Single (n => n.Id == active!.ParentId).Source);
            }
        }
    }

    internal static class ComboBoxSimpleTestExtensions
    {
        // The drop-down styles' one-line height, which a Simple combo's edit band matches.
        internal static int PreferredHeightOfOneLine (this ComboBox combo)
            => TextMeasurer.LogicalLineHeight (combo) + combo.Padding.Vertical + 6 + 1;
    }
}
