using System;
using System.Collections.Generic;
using System.Drawing;

namespace Majorsilence.Forms
{
    // AutoCompleteMode.Suggest and the suggest half of SuggestAppend (LST-07's remainder).
    //
    // The combo's items ARE its drop-down ListBox's items (see Items), so the filtered list cannot be
    // shown by narrowing the drop-down: that would mean deleting the control's own items and putting
    // them back. Upstream does not do that either. Its suggestions come from the shell's auto-complete
    // object (IAutoComplete2, wired up in ComboBox.SetAutoComplete, ComboBox/ComboBox.cs), which draws
    // them in a window of its own under the edit control, apart from the combo's list. So does this: a
    // second popup with a plain ListBox holding the matching STRINGS -- a presentation list built from
    // the source on each keystroke -- while Items, SelectedIndex and the drop-down are left alone.
    //
    // The popup does not take activation, as a tool tip does not: the caret stays in the edit region and
    // typing carries on filtering, which is how the shell's suggestion window behaves.
    public partial class ComboBox
    {
        private PopupWindow? suggest_popup;
        private ListBox? suggest_list;

        // What the user had typed when arrowing into the suggestions began; Escape puts it back.
        private string? suggest_typed;

        // Set while the keyboard moves the highlight, so the list's SelectedIndexChanged is not taken
        // as a click that accepts a suggestion.
        private bool suggest_navigating;

        private readonly List<string> suggestions = [];

        /// <summary>The suggestions the last keystroke produced, in source order.</summary>
        internal IReadOnlyList<string> Suggestions => suggestions;

        /// <summary>Whether the suggestion popup is showing.</summary>
        internal bool SuggestionsShown => suggest_popup?.Visible == true;

        /// <summary>The list inside the suggestion popup, for tests; null until first shown.</summary>
        internal ListBox? SuggestionList => suggest_list;

        private bool SuggestsAsYouType
            => IsEditable && AutoCompleteMode is AutoCompleteMode.Suggest or AutoCompleteMode.SuggestAppend;

        // Every entry in the configured source that starts with what was typed, each once, in source
        // order. The same sources FindCompletion reads, so Append and Suggest never disagree about what
        // matches.
        private void CollectSuggestions (string typed)
        {
            suggestions.Clear ();

            if (typed.Length == 0)
                return;

            var seen = new HashSet<string> (StringComparer.CurrentCultureIgnoreCase);

            void Consider (string? candidate)
            {
                if (!string.IsNullOrEmpty (candidate) && candidate!.StartsWith (typed, StringComparison.CurrentCultureIgnoreCase) && seen.Add (candidate))
                    suggestions.Add (candidate);
            }

            if (AutoCompleteSource == AutoCompleteSource.ListItems) {
                foreach (var item in Items)
                    Consider (GetItemText (item));
            } else if (AutoCompleteSource == AutoCompleteSource.CustomSource) {
                foreach (string? candidate in AutoCompleteCustomSource)
                    Consider (candidate);
            }
        }

        // The user changed the text: refilter, and show, move or hide the popup to match.
        private void UpdateSuggestions ()
        {
            if (!SuggestsAsYouType) {
                CloseSuggestions ();
                return;
            }

            suggest_typed = null;
            CollectSuggestions (edit.Text);

            if (suggestions.Count == 0) {
                CloseSuggestions ();
                return;
            }

            // A control on no window has nowhere to show a popup; the list is still computed, which is
            // what Suggestions reports.
            if (FindWindow () is not WindowBase window)
                return;

            // The two lists are never open together: the suggestions replace the drop-down.
            DroppedDown = false;

            if (suggest_list is null) {
                suggest_list = new ListBox { Dock = DockStyle.Fill, SelectItemOnMouseUp = true, ShowHover = true };
                suggest_list.SelectedIndexChanged += SuggestionList_SelectedIndexChanged;
            }

            suggest_navigating = true;

            try {
                suggest_list.Items.Clear ();

                foreach (var suggestion in suggestions)
                    suggest_list.Items.Add (suggestion);

                suggest_list.SelectedIndex = -1;
            } finally {
                suggest_navigating = false;
            }

            if (suggest_popup is null || suggest_popup.IsDisposed) {
                suggest_popup = new PopupWindow (window, activates: false);
                suggest_popup.Controls.Add (suggest_list);
            }

            var rows = Math.Max (1, Math.Min (suggestions.Count, MaxDropDownItems));
            suggest_popup.Size = new Size (Width, rows * suggest_list.ItemHeight + 2);

            if (!suggest_popup.Visible)
                suggest_popup.Show (this, 1, Height);
        }

        private void CloseSuggestions ()
        {
            suggest_typed = null;

            if (suggest_popup?.Visible == true)
                suggest_popup.Hide ();
        }

        // Keys while the suggestions are open. Up/Down move the highlight and show it in the edit
        // region, Enter takes it, Escape closes the list and puts back what was typed -- the shell
        // auto-complete window's keys. Answers whether the key was the suggestions'.
        private bool HandleSuggestionKey (KeyEventArgs e)
        {
            if (!SuggestionsShown || suggest_list is null || e.Alt || e.Handled)
                return false;

            switch (e.KeyCode) {
                case Keys.Down:
                case Keys.Up: {
                    var count = suggest_list.Items.Count;
                    var index = suggest_list.SelectedIndex;

                    index = e.KeyCode == Keys.Down
                        ? (index + 1 >= count ? -1 : index + 1)
                        : (index < 0 ? count - 1 : index - 1);

                    suggest_typed ??= edit.Text;

                    suggest_navigating = true;

                    try {
                        suggest_list.SelectedIndex = index;
                    } finally {
                        suggest_navigating = false;
                    }

                    // Off either end of the list, back to what was typed, as the shell's window does.
                    ShowInEdit (index < 0 ? suggest_typed : suggestions[index]);
                    e.Handled = true;
                    return true;
                }

                case Keys.Enter:
                    CloseSuggestions ();

                    // Not handled: the combo's own Enter (OnKeyUp) still commits the text, which
                    // selects the matching item.
                    return true;

                case Keys.Escape:
                    if (suggest_typed is not null)
                        ShowInEdit (suggest_typed);

                    CloseSuggestions ();
                    e.Handled = true;
                    return true;
            }

            return false;
        }

        // Writes text into the edit region without refiltering: the list being walked must not change
        // under the highlight.
        private void ShowInEdit (string text)
        {
            syncing_edit_text = true;

            try {
                edit.Text = text;
            } finally {
                syncing_edit_text = false;
            }

            base.Text = text;
            edit.SelectionStart = text.Length;
            edit.SelectionLength = 0;
        }

        // A click on a suggestion takes it.
        private void SuggestionList_SelectedIndexChanged (object? sender, EventArgs e)
        {
            if (suggest_navigating || suggest_list is null || suggest_list.SelectedIndex < 0)
                return;

            AcceptSuggestion (suggest_list.SelectedIndex);
        }

        /// <summary>Takes the suggestion at <paramref name="index"/>: it becomes the text, and an item
        /// whose text it is becomes the selection, as a commit with Enter does.</summary>
        internal void AcceptSuggestion (int index)
        {
            if (index < 0 || index >= suggestions.Count)
                return;

            var text = suggestions[index];

            CloseSuggestions ();
            Text = text;
            edit.SelectionStart = text.Length;
            edit.SelectionLength = 0;
        }
    }
}
