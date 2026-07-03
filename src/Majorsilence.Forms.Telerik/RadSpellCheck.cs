using Majorsilence.Forms.SpellCheck;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat spell checker. Backed by the dependency-free engine in
    /// <see cref="Majorsilence.Forms.SpellCheck"/>. Setting <see cref="AutoSpellCheckControl"/> attaches
    /// a <see cref="Majorsilence.Forms.SpellCheck.SpellChecker"/> to that <see cref="TextBox"/>, enabling
    /// wavy-underline misspelling rendering and the suggestions/"Add to Dictionary" right-click menu;
    /// setting it to null detaches.
    /// </summary>
    public class RadSpellChecker
    {
        private readonly Majorsilence.Forms.SpellCheck.SpellChecker _checker = new ();
        private TextBox? _autoSpellCheckControl;

        /// <summary>
        /// Gets or sets the <see cref="TextBox"/> this spell checker automatically checks as the user
        /// types. Assigning a new control detaches from the previous one; assigning null detaches
        /// entirely.
        /// </summary>
        public TextBox? AutoSpellCheckControl {
            get => _autoSpellCheckControl;
            set {
                if (ReferenceEquals (_autoSpellCheckControl, value))
                    return;

                if (_autoSpellCheckControl != null)
                    TextBoxSpellCheck.SetSpellChecker (_autoSpellCheckControl, null);

                _autoSpellCheckControl = value;

                if (_autoSpellCheckControl != null)
                    TextBoxSpellCheck.SetSpellChecker (_autoSpellCheckControl, _checker);
            }
        }

        /// <summary>Gets or sets how spell checking is triggered. Majorsilence.Forms always checks word-by-word as text changes; this is stored for API compatibility.</summary>
        public SpellCheckMode SpellCheckMode { get; set; } = SpellCheckMode.WordByWord;
    }

    /// <summary>Specifies how a <see cref="RadSpellChecker"/> checks text.</summary>
    public enum SpellCheckMode
    {
        /// <summary>Checks each word as it is typed (Majorsilence.Forms' only real mode - both values render the same underline-as-you-type behavior).</summary>
        WordByWord = 0,

        /// <summary>Checks the entire text at once, e.g. when triggered manually.</summary>
        AllAtOnce = 1,
    }

    /// <summary>
    /// Telerik-compat marker base for a spell-check dictionary attachable to a
    /// <c>DocumentSpellChecker</c> (see <c>Telerik/RadRichTextEditor.cs</c>, Phase 3).
    /// </summary>
    public abstract class RadDictionary
    {
    }

    /// <summary>
    /// Telerik-compat en-US dictionary carrier. Majorsilence.Forms' rich text editor uses the host
    /// browser's native spellcheck (see Phase 3's <c>RadRichTextEditor</c>), so this type exists only so
    /// <c>editor.SpellChecker.AddDictionary(new RadEn_USDictionary(), culture)</c> compiles; it carries no
    /// behavior of its own.
    /// </summary>
    public sealed class RadEn_USDictionary : RadDictionary
    {
    }
}
