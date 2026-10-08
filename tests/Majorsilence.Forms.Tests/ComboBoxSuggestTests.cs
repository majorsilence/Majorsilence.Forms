using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LST-07's remainder: AutoCompleteMode.Suggest. The combo's items ARE its drop-down list's items, so
    // the suggestions cannot be shown by filtering the drop-down without deleting the control's own
    // items. Upstream draws them in a separate auto-complete window; so does this, from a presentation
    // list of strings that never touches Items. Before, Suggest did nothing at all and SuggestAppend
    // only appended.
    [Collection ("Headless")]
    public class ComboBoxSuggestTests
    {
        private static ComboBox Combo (out Form form, AutoCompleteMode mode = AutoCompleteMode.Suggest)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 300, Height = 200 };
            var combo = new ComboBox { Width = 160, Height = 28 };

            foreach (var item in new[] { "apple", "apricot", "banana" })
                combo.Items.Add (item);

            combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            combo.AutoCompleteMode = mode;
            form.Controls.Add (combo);
            form.Show ();

            return combo;
        }

        private static void Type (ComboBox combo, string characters)
        {
            foreach (var c in characters)
                combo.RaiseKeyPress (new KeyPressEventArgs (c));
        }

        private static void Press (ComboBox combo, Keys key)
        {
            combo.RaiseKeyDown (new KeyEventArgs (key));
            combo.RaiseKeyUp (new KeyEventArgs (key));
        }

        [Fact]
        public void Typing_shows_the_matching_entries_without_touching_Items ()
        {
            var combo = Combo (out var form);

            using (form) {
                Type (combo, "ap");

                Assert.True (combo.SuggestionsShown);
                Assert.Equal (new[] { "apple", "apricot" }, combo.Suggestions);
                Assert.Equal (new[] { "apple", "apricot" }, combo.SuggestionList!.Items.Cast<object> ().Select (o => o.ToString ()));

                // The control's own items and selection are untouched, and Suggest does not append.
                Assert.Equal (new[] { "apple", "apricot", "banana" }, combo.Items.Cast<object> ().Select (o => o.ToString ()));
                Assert.Equal (-1, combo.SelectedIndex);
                Assert.Equal ("ap", combo.Text);
                Assert.False (combo.DroppedDown);
            }
        }

        [Fact]
        public void Each_keystroke_refilters_and_no_match_closes_the_list ()
        {
            var combo = Combo (out var form);

            using (form) {
                Type (combo, "apr");
                Assert.Equal (new[] { "apricot" }, combo.Suggestions);
                Assert.True (combo.SuggestionsShown);

                Type (combo, "x");
                Assert.Empty (combo.Suggestions);
                Assert.False (combo.SuggestionsShown);
            }
        }

        [Fact]
        public void Arrows_walk_the_suggestions_and_Enter_takes_one ()
        {
            var combo = Combo (out var form);

            using (form) {
                Type (combo, "ap");

                Press (combo, Keys.Down);
                Assert.Equal ("apple", combo.EditRegion.Text);
                Assert.True (combo.SuggestionsShown, "walking the list does not refilter it away");

                Press (combo, Keys.Down);
                Assert.Equal ("apricot", combo.EditRegion.Text);
                Assert.Equal (-1, combo.SelectedIndex);   // still only shown, not committed

                Press (combo, Keys.Enter);
                Assert.False (combo.SuggestionsShown);
                Assert.Equal ("apricot", combo.Text);
                Assert.Equal (1, combo.SelectedIndex);
            }
        }

        [Fact]
        public void Escape_closes_the_list_and_puts_back_what_was_typed ()
        {
            var combo = Combo (out var form);

            using (form) {
                Type (combo, "ap");
                Press (combo, Keys.Down);
                Assert.Equal ("apple", combo.EditRegion.Text);

                Press (combo, Keys.Escape);
                Assert.False (combo.SuggestionsShown);
                Assert.Equal ("ap", combo.Text);
                Assert.Equal (-1, combo.SelectedIndex);
            }
        }

        [Fact]
        public void A_click_on_a_suggestion_takes_it ()
        {
            var combo = Combo (out var form);

            using (form) {
                Type (combo, "ap");

                // What the list's mouse-up selection does.
                combo.SuggestionList!.SelectedIndex = 1;

                Assert.False (combo.SuggestionsShown);
                Assert.Equal ("apricot", combo.Text);
                Assert.Equal (1, combo.SelectedIndex);
            }
        }

        [Fact]
        public void Text_set_from_code_never_pops_the_list ()
        {
            var combo = Combo (out var form);

            using (form) {
                combo.Text = "ap";

                Assert.False (combo.SuggestionsShown);
            }
        }

        [Fact]
        public void Opening_the_drop_down_closes_the_suggestions ()
        {
            var combo = Combo (out var form);

            using (form) {
                Type (combo, "ap");
                Assert.True (combo.SuggestionsShown);

                combo.DroppedDown = true;

                Assert.False (combo.SuggestionsShown);
                Assert.True (combo.DroppedDown);
                combo.DroppedDown = false;
            }
        }

        [Fact]
        public void SuggestAppend_both_appends_and_suggests ()
        {
            var combo = Combo (out var form, AutoCompleteMode.SuggestAppend);

            using (form) {
                Type (combo, "ap");

                Assert.Equal ("apple", combo.Text);
                Assert.Equal (2, combo.SelectionStart);
                Assert.True (combo.SuggestionsShown);
                Assert.Equal (new[] { "apple", "apricot" }, combo.Suggestions);
            }
        }

        [Fact]
        public void A_custom_source_is_suggested_from ()
        {
            var combo = Combo (out var form);

            using (form) {
                combo.AutoCompleteCustomSource.Add ("blueberry");
                combo.AutoCompleteCustomSource.Add ("banana");
                combo.AutoCompleteSource = AutoCompleteSource.CustomSource;

                Type (combo, "b");

                Assert.Equal (new[] { "blueberry", "banana" }, combo.Suggestions);
            }
        }

        [Fact]
        public void Append_alone_does_not_suggest ()
        {
            // A guard: Append completed inline before this change too, and must still not open a list.
            var combo = Combo (out var form, AutoCompleteMode.Append);

            using (form) {
                Type (combo, "ap");

                Assert.Equal ("apple", combo.Text);
                Assert.False (combo.SuggestionsShown);
            }
        }
    }
}
