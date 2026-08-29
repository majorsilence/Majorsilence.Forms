using System;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Watches a window's keyboard-focus choke-point and tells the backend when a text-editing control
    /// gains or loses focus, so a single-view backend (browser / Android / iOS) can raise or dismiss the
    /// platform's on-screen keyboard. One subscription covers the whole window — focus is observed at
    /// the framework's single choke-point, <see cref="ControlAdapter.SelectedControlChanged"/> (the same
    /// hook <see cref="Automation.AutomationObserver"/> uses).
    ///
    /// Inert on desktop: <see cref="IWindowBackend.SetTextInputActive"/> is a default no-op there, so
    /// this costs one event handler and nothing else. Created and disposed by <see cref="WindowBase"/>.
    /// </summary>
    internal sealed class SoftKeyboardObserver : IDisposable
    {
        private readonly WindowBase _window;
        private bool _active;
        private bool _disposed;

        internal SoftKeyboardObserver (WindowBase window)
        {
            _window = window ?? throw new ArgumentNullException (nameof (window));
            _window.adapter.SelectedControlChanged += OnSelectedControlChanged;
        }

        private void OnSelectedControlChanged (object? sender, Control? control)
        {
            var editable = control as TextBoxBase;
            var active = editable is { ReadOnly: false, Enabled: true };

            if (active == _active && !active)
                return;

            _active = active;
            try {
                _window.Backend.SetTextInputActive (active, active ? KindOf (editable!) : TextInputKind.Normal);
            } catch (Exception) {
                // A backend that can't drive its IME is not a framework error; never let it break focus.
            }
        }

        private static TextInputKind KindOf (TextBoxBase box) => box switch {
            { Multiline: true } => TextInputKind.Multiline,
            TextBox { PasswordChar: not '\0' } => TextInputKind.Password,
            _ => TextInputKind.Normal
        };

        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;

            _window.adapter.SelectedControlChanged -= OnSelectedControlChanged;

            // Focus is going away with the window; make sure a lingering keyboard goes too.
            if (_active) {
                try { _window.Backend.SetTextInputActive (false, TextInputKind.Normal); }
                catch (Exception) { }
            }
        }
    }
}
