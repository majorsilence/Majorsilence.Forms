#if SINGLEVIEW
using Avalonia;
using Avalonia.Input.TextInput;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The <see cref="TextInputMethodClient"/> the single-view host hands Avalonia while a
    /// Majorsilence.Forms text control is focused, so Avalonia's Android / iOS / browser platform raises
    /// the on-screen keyboard and positions its candidate strip. Majorsilence.Forms owns text editing and
    /// caret movement itself, so this client only reports where the caret is — it does not do preedit
    /// composition or surrounding-text queries (the platform IME still delivers committed text through
    /// the normal <c>OnTextInput</c> path the host already forwards to <c>WindowBase.HandleTextInput</c>).
    /// </summary>
    internal sealed class MajorsilenceFormsTextInputClient : TextInputMethodClient
    {
        private readonly MajorsilenceFormsSingleViewHost _host;
        private readonly WindowBase _owner;

        internal MajorsilenceFormsTextInputClient (MajorsilenceFormsSingleViewHost host, WindowBase owner)
        {
            _host = host;
            _owner = owner;
        }

        /// <inheritdoc/>
        public override Visual TextViewVisual => _host;

        /// <inheritdoc/>
        public override bool SupportsPreedit => false;

        /// <inheritdoc/>
        public override bool SupportsSurroundingText => false;

        /// <inheritdoc/>
        public override string SurroundingText => string.Empty;

        /// <inheritdoc/>
        public override global::Avalonia.Input.TextInput.TextSelection Selection { get; set; }

        /// <inheritdoc/>
        public override Rect CursorRectangle {
            get {
                var r = _owner.TryGetCaretRectangleLogical ();
                return r is { } rect
                    ? new Rect (rect.X, rect.Y, System.Math.Max (1, rect.Width), System.Math.Max (1, rect.Height))
                    : default;
            }
        }

        /// <summary>Tells the IME the caret rectangle may have changed (focus moved, text edited).</summary>
        internal void NotifyCursorMoved () => RaiseCursorRectangleChanged ();
    }
}
#endif
