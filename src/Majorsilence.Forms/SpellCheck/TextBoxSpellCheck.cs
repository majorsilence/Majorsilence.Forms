namespace Majorsilence.Forms.SpellCheck
{
    /// <summary>
    /// Associates a <see cref="SpellChecker"/> with a <see cref="TextBox"/>, enabling wavy-underline
    /// misspelling rendering and the "suggestions + Add to Dictionary" right-click menu on that control.
    /// Mirrors the attach/detach-via-static-dictionary pattern used by
    /// <c>Majorsilence.Forms.Telerik.RadContextMenuManager</c>, and is the mechanism
    /// <c>Majorsilence.Forms.Telerik.RadSpellChecker.AutoSpellCheckControl</c> is built on.
    /// </summary>
    /// <remarks>
    /// Zero overhead when detached: <see cref="TextBox"/>'s paint path only tokenizes and looks up
    /// misspelled ranges when a checker is attached, and only re-tokenizes when the text actually
    /// changes (the result is cached until the next <c>TextChanged</c>).
    /// </remarks>
    public static class TextBoxSpellCheck
    {
        private static readonly Dictionary<TextBox, Attachment> _attachments = new ();

        private sealed class Attachment
        {
            internal required SpellChecker Checker;
            internal required EventHandler TextChangedHandler;
            internal required EventHandler<MouseEventArgs> ClickHandler;
            internal required EventHandler<System.ComponentModel.CancelEventArgs> MenuOpeningHandler;
            internal List<MisspelledRange>? CachedRanges;
            internal int LastClickedCharIndex = -1;
        }

        /// <summary>
        /// Attaches <paramref name="checker"/> to <paramref name="textBox"/>, enabling squiggle rendering
        /// and the misspelling right-click menu. Passing null detaches any previously-attached checker.
        /// </summary>
        public static void SetSpellChecker (TextBox textBox, SpellChecker? checker)
        {
            ArgumentNullException.ThrowIfNull (textBox);

            if (_attachments.Remove (textBox, out var existing)) {
                textBox.TextChanged -= existing.TextChangedHandler;
                textBox.MouseUp -= existing.ClickHandler;

                if (textBox.ContextMenu != null)
                    textBox.ContextMenu.Opening -= existing.MenuOpeningHandler;
            }

            if (checker is null)
                return;

            var attachment = new Attachment {
                Checker = checker,
                TextChangedHandler = null!,
                ClickHandler = null!,
                MenuOpeningHandler = null!,
            };

            attachment.TextChangedHandler = (_, _) => attachment.CachedRanges = null;

            attachment.ClickHandler = (_, e) => {
                if (e.Button == MouseButtons.Right)
                    attachment.LastClickedCharIndex = textBox.GetSpellCheckCharIndexFromPosition (e.Location);
            };

            attachment.MenuOpeningHandler = (_, _) => BuildContextMenuItems (textBox, attachment);

            textBox.TextChanged += attachment.TextChangedHandler;
            textBox.MouseUp += attachment.ClickHandler;

            // The context menu is created lazily so an existing user-assigned ContextMenu (if any) is
            // preserved and simply gains the spelling items; PopulateOpeningHandler re-subscribes if the
            // ContextMenu instance is replaced later.
            textBox.ContextMenu ??= new ContextMenu ();
            textBox.ContextMenu.Opening += attachment.MenuOpeningHandler;

            _attachments[textBox] = attachment;
        }

        /// <summary>Gets the <see cref="SpellChecker"/> currently attached to <paramref name="textBox"/>, or null.</summary>
        public static SpellChecker? GetSpellChecker (TextBox textBox) =>
            _attachments.TryGetValue (textBox, out var attachment) ? attachment.Checker : null;

        // Returns (and lazily computes/caches) the misspelled word ranges for the TextBox's current text.
        internal static IReadOnlyList<MisspelledRange> GetMisspelledRanges (TextBox textBox)
        {
            if (!_attachments.TryGetValue (textBox, out var attachment))
                return [];

            if (attachment.CachedRanges != null)
                return attachment.CachedRanges;

            var ranges = new List<MisspelledRange> ();
            var text = textBox.Text;

            foreach (var token in SpellChecker.Tokenize (text)) {
                if (attachment.Checker.IsMisspelled (token.Text, token.IsSentenceStart))
                    ranges.Add (new MisspelledRange (token.Text, token.Start, token.End));
            }

            attachment.CachedRanges = ranges;
            return ranges;
        }

        // Builds the "suggestions" + "Add to Dictionary" items on the TextBox's ContextMenu, based on
        // whichever misspelled word was under the pointer at the most recent right-click.
        private static void BuildContextMenuItems (TextBox textBox, Attachment attachment)
        {
            var menu = textBox.ContextMenu!;

            // Remove any spelling items this handler added on a previous opening.
            for (var i = menu.Items.Count - 1; i >= 0; i--) {
                if (menu.Items[i].Tag is SpellCheckMenuMarker)
                    menu.Items.RemoveAt (i);
            }

            if (attachment.LastClickedCharIndex < 0)
                return;

            var ranges = GetMisspelledRanges (textBox);
            MisspelledRange? hit = null;

            foreach (var range in ranges) {
                if (attachment.LastClickedCharIndex >= range.Start && attachment.LastClickedCharIndex < range.End) {
                    hit = range;
                    break;
                }
            }

            if (hit is null)
                return;

            var word = hit.Value.Word;
            var suggestions = attachment.Checker.GetSuggestions (word);
            var insertAt = 0;

            foreach (var suggestion in suggestions) {
                var replacement = suggestion;
                var range = hit.Value;

                var item = new MenuItem (replacement, onClick: (_, _) => ReplaceWord (textBox, range, replacement)) {
                    Tag = SpellCheckMenuMarker.Instance,
                };

                menu.Items.Insert (insertAt++, item);
            }

            if (suggestions.Count == 0) {
                menu.Items.Insert (insertAt++, new MenuItem ("(No suggestions)") {
                    Enabled = false,
                    Tag = SpellCheckMenuMarker.Instance,
                });
            }

            menu.Items.Insert (insertAt++, new MenuSeparatorItem { Tag = SpellCheckMenuMarker.Instance });

            menu.Items.Insert (insertAt, new MenuItem ("Add to Dictionary", onClick: (_, _) => {
                attachment.Checker.AddToDictionary (word);
                attachment.CachedRanges = null;
                textBox.Invalidate ();
            }) {
                Tag = SpellCheckMenuMarker.Instance,
            });
        }

        private static void ReplaceWord (TextBox textBox, MisspelledRange range, string replacement)
        {
            var text = textBox.Text;

            if (range.Start < 0 || range.End > text.Length)
                return;

            textBox.Text = text[..range.Start] + replacement + text[range.End..];
            textBox.SelectionStart = range.Start + replacement.Length;
            textBox.SelectionLength = 0;
        }

        // Marks a MenuItem as one this class added, so it can find-and-remove just its own items on
        // the next Opening without disturbing menu items the application added itself.
        private sealed class SpellCheckMenuMarker
        {
            internal static readonly SpellCheckMenuMarker Instance = new ();
        }
    }
}
