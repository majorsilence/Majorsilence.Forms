using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.7 (LST-02 P0, LST-16) and W5.8 (LST-03 P0, LST-04 P0, LST-06, LST-09): a CheckedListBox that
    // could not be ticked, and every selection path except the SelectedIndex setter changing the
    // selection without telling anyone. Both are silent failures -- the state is right, the
    // notification never happens -- so these tests count events as much as they read state.
    [Collection ("Headless")]
    public class ListSelectionAndCheckTests
    {
        // HeadlessRenderer.Click drives a WINDOW; these tests exercise the control directly, so the
        // mouse pair goes in through the control's own entry point. Coordinates are logical, as a real
        // MouseEventArgs carries.
        private static void ClickAt (Control control, int x, int y)
        {
            var e = new MouseEventArgs (MouseButtons.Left, 1, x, y, 0);

            control.RaiseMouseDown (e);
            control.RaiseMouseUp (e);
        }

        private static ListBox Listed (params string[] items)
        {
            HeadlessRenderer.Use ();
            var box = new ListBox { Width = 200, Height = 200 };

            foreach (var item in items)
                box.Items.Add (item);

            return box;
        }

        private static CheckedListBox Checked (params string[] items)
        {
            HeadlessRenderer.Use ();
            var box = new CheckedListBox { Width = 200, Height = 200 };

            foreach (var item in items)
                box.Items.Add (item);

            return box;
        }

        // ── LST-03: SelectedItem announces ──────────────────────────────────────────────────────

        [Fact]
        public void Setting_SelectedItem_raises_the_selection_events ()
        {
            using var box = Listed ("a", "b", "c");
            var index_changed = 0;
            var value_changed = 0;
            box.SelectedIndexChanged += (_, _) => index_changed++;
            box.SelectedValueChanged += (_, _) => value_changed++;

            // The commonest way LOB code selects programmatically. It went through the collection's
            // internal setter and raised nothing, so the handler that loads the detail panel never ran.
            box.SelectedItem = "b";

            Assert.Equal (1, box.SelectedIndex);
            Assert.Equal (1, index_changed);
            Assert.Equal (1, value_changed);
        }

        // A guard, not a proof: it cannot fail against the old behaviour, which also left the selection
        // alone. It is here so a future "throw on an unknown value" does not slip in.
        [Fact]
        public void Setting_SelectedItem_to_an_absent_value_leaves_the_selection_alone ()
        {
            // Deliberately preserved: a designer sets SelectedValue before the items are populated, and
            // a bound editor writes back a value the current filter excluded. Throwing turned both into
            // a crash inside InitializeComponent.
            using var box = Listed ("a", "b");
            box.SelectedIndex = 0;
            var raised = 0;
            box.SelectedIndexChanged += (_, _) => raised++;

            box.SelectedItem = "not in the list";

            Assert.Equal (0, box.SelectedIndex);
            Assert.Equal (0, raised);
        }

        // ── LST-04: every multi-select path announces ───────────────────────────────────────────

        [Fact]
        public void SetSelected_and_ClearSelected_each_announce_once ()
        {
            using var box = Listed ("a", "b", "c");
            box.SelectionMode = SelectionMode.MultiSimple;
            var raised = 0;
            box.SelectedIndexChanged += (_, _) => raised++;

            box.SetSelected (0, true);
            box.SetSelected (2, true);
            box.ClearSelected ();

            Assert.Equal (3, raised);
            Assert.Empty (box.SelectedIndices);
        }

        [Fact]
        public void Re_selecting_what_is_already_selected_announces_nothing ()
        {
            // The snapshot comparison in ChangeSelection, not merely "did a mutator run": WinForms
            // raises on a real change only.
            using var box = Listed ("a", "b");
            box.SelectionMode = SelectionMode.MultiSimple;
            box.SetSelected (0, true);

            var raised = 0;
            box.SelectedIndexChanged += (_, _) => raised++;

            box.SetSelected (0, true);

            Assert.Equal (0, raised);
        }

        [Fact]
        public void A_multi_select_click_announces_exactly_once ()
        {
            // Once, not twice: the input handler is wrapped in ChangeSelection and its branches also
            // assign SelectedIndex, which raises on its own outside a batch. Double-reporting is the
            // trap W5.6 hit when ListViewItem.Selected became the choke point.
            using var box = Listed ("a", "b", "c");
            box.SelectionMode = SelectionMode.MultiExtended;
            var raised = 0;
            box.SelectedIndexChanged += (_, _) => raised++;

            ClickAt (box, 10, box.ScaledItemHeight + 2);

            Assert.Equal (1, raised);
            Assert.Single (box.SelectedIndices);
        }

        [Fact]
        public void Collapsing_the_SelectionMode_announces_the_items_it_drops ()
        {
            using var box = Listed ("a", "b", "c");
            box.SelectionMode = SelectionMode.MultiSimple;
            box.SetSelected (0, true);
            box.SetSelected (1, true);

            var raised = 0;
            box.SelectedIndexChanged += (_, _) => raised++;

            box.SelectionMode = SelectionMode.None;

            Assert.Equal (1, raised);
            Assert.Empty (box.SelectedIndices);
        }

        // ── LST-06 / LST-09: the ComboBox ───────────────────────────────────────────────────────

        [Fact]
        public void Clearing_a_combo_selection_announces_it ()
        {
            HeadlessRenderer.Use ();
            using var combo = new ComboBox ();
            combo.Items.Add ("a");
            combo.Items.Add ("b");
            combo.SelectedIndex = 1;

            var raised = 0;
            combo.SelectedIndexChanged += (_, _) => raised++;

            // A "Clear filter" button. The raise was guarded by `if (index > -1)`, so this announced
            // nothing and dependent controls kept showing the old choice (LST-06).
            combo.SelectedIndex = -1;

            Assert.Equal (1, raised);
            Assert.Equal (string.Empty, combo.Text);
        }

        [Fact]
        public void A_combo_selection_raises_TextChanged ()
        {
            HeadlessRenderer.Use ();
            using var combo = new ComboBox ();
            combo.Items.Add ("alpha");
            combo.Items.Add ("beta");

            var raised = 0;
            combo.TextChanged += (_, _) => raised++;

            combo.SelectedIndex = 1;

            // Nothing wrote base.Text, and Control.Text is the only thing that raises TextChanged, so
            // TextChanged never fired for a combo at all -- which is what validation, dirty-tracking
            // and a Binding on Text all listen to (LST-09).
            Assert.Equal (1, raised);
            Assert.Equal ("beta", combo.Text);
        }

        // ── LST-02: the CheckedListBox can be ticked ────────────────────────────────────────────

        [Fact]
        public void A_click_on_the_glyph_toggles_the_check ()
        {
            using var box = Checked ("a", "b");
            var checks = 0;
            box.ItemCheck += (_, _) => checks++;

            var row = box.GetItemRectangle (1);
            var glyph = box.GlyphBounds (row);

            // Clicks arrive in logical units; the glyph rectangle is device.
            ClickAt (box, glyph.Left + glyph.Width / 2, glyph.Top + glyph.Height / 2);

            Assert.True (box.GetItemChecked (1));
            Assert.Equal (1, checks);
        }

        [Fact]
        public void CheckOnClick_toggles_from_a_click_anywhere_on_the_row ()
        {
            using var box = Checked ("a", "b");
            box.CheckOnClick = true;

            ClickAt (box, 150, box.ScaledItemHeight + 2);

            Assert.True (box.GetItemChecked (1));
        }

        [Fact]
        public void ItemCheck_can_veto_a_click ()
        {
            using var box = Checked ("a", "b");
            box.CheckOnClick = true;
            box.ItemCheck += (_, e) => e.NewValue = CheckState.Unchecked;

            ClickAt (box, 150, 2);

            Assert.False (box.GetItemChecked (0));
        }

        [Fact]
        public void A_checked_item_draws_a_glyph_where_a_plain_list_box_draws_nothing ()
        {
            // The visual half of LST-02. Compared against a plain ListBox with the same item rather
            // than against "some ink exists": the row's text and selection are drawn by both, so only
            // the difference in the GLYPH COLUMN is evidence of a check box.
            using var form = new Form { Size = new Size (300, 200) };
            form.UseSystemDecorations = false;

            // An EMPTY item label, deliberately: a plain list box insets its text by 4px and so draws
            // ink inside the glyph column for any visible caption, which made the first version of this
            // test pass against a list box drawing no glyph at all. With no text, the only thing that
            // can put ink in that column is the check box.
            var checkedBox = Checked ("");
            checkedBox.SetItemChecked (0, true);
            checkedBox.Left = 0;
            checkedBox.Top = 0;

            var plainBox = Listed ("");
            plainBox.Left = 0;
            plainBox.Top = 0;

            form.Controls.Add (checkedBox);
            form.Controls.Add (plainBox);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            // The glyph's own rectangle, not the whole column: the control paints a 1px border down
            // its left edge, so a column starting at x=0 counts that border as ink and the comparison
            // stops discriminating (which is how the first version of this test read 109 "ink" pixels
            // from a list box drawing no glyph).
            var glyph = checkedBox.GlyphBounds (checkedBox.GetItemRectangle (0));

            Assert.True (InkIn (checkedBox, glyph) > 0,
                "a checked item should draw a glyph before its text");
            Assert.Equal (0, InkIn (plainBox, glyph));

            form.Close ();
        }

        private static int InkIn (ListBox box, Rectangle area)
        {
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap)buffer.Invoke (box, null)!;
            var background = bitmap.GetPixel (area.Right + 40, box.ScaledItemHeight * 3);
            var ink = 0;

            for (var x = area.Left; x < System.Math.Min (area.Right, bitmap.Width); x++)
                for (var y = area.Top; y < System.Math.Min (area.Bottom, bitmap.Height); y++) {
                    var pixel = bitmap.GetPixel (x, y);

                    if (pixel.Alpha > 0 && pixel != background)
                        ink++;
                }

            return ink;
        }

        // ── LST-16: the wrapper does not leak ───────────────────────────────────────────────────

        [Fact]
        public void SelectedItem_is_the_object_that_was_added ()
        {
            HeadlessRenderer.Use ();
            using var box = new CheckedListBox ();
            var role = new Role ("admin");
            box.Items.Add (role);

            box.SelectedIndex = 0;

            // Items are stored in CheckedListBoxItem wrappers so there is somewhere to keep the check
            // state, and the base class read straight through them: `(Role)clb.SelectedItem` threw
            // InvalidCastException (LST-16).
            Assert.Same (role, box.SelectedItem);
        }

        [Fact]
        public void SelectedItem_can_be_assigned_the_object_that_was_added ()
        {
            HeadlessRenderer.Use ();
            using var box = new CheckedListBox ();
            var first = new Role ("user");
            var second = new Role ("admin");
            box.Items.Add (first);
            box.Items.Add (second);

            box.SelectedItem = second;

            // The setter looked the value up among wrappers, found nothing, and was silently ignored.
            Assert.Equal (1, box.SelectedIndex);
            Assert.Same (second, box.SelectedItem);
        }

        private sealed record Role (string Name);
    }
}
