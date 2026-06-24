using System;
using System.Linq;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// Watches a window for the live changes that OS accessibility bridges must announce — keyboard focus
    /// moving between controls, and the focused control's value changing — and republishes them as
    /// <see cref="AutomationElement"/>-level events. Backend-neutral: a platform bridge (e.g. the Windows
    /// UI Automation bridge) subscribes to these and raises the corresponding native automation events,
    /// which is what drives screen-reader announcements and focus-following magnifiers.
    ///
    /// Focus is observed at the framework's single focus choke-point (the control adapter), so one
    /// subscription covers the whole window. Value changes are observed on the currently focused control —
    /// the dominant "announce as the user edits this field" case; tracking every control's value is a
    /// later phase. Dispose to detach.
    /// </summary>
    public sealed class AutomationObserver : IDisposable
    {
        private readonly WindowBase _window;
        private Control? _valueSource;
        private bool _disposed;

        /// <summary>Starts observing the given window. Dispose to stop.</summary>
        public AutomationObserver (WindowBase window)
        {
            _window = window ?? throw new ArgumentNullException (nameof (window));
            _window.adapter.SelectedControlChanged += OnSelectedControlChanged;
        }

        /// <summary>Raised when keyboard focus moves to a control (or to null when focus is cleared).</summary>
        public event EventHandler<AutomationElement?>? FocusChanged;

        /// <summary>Raised when the focused control's value (text, checked state, …) changes.</summary>
        public event EventHandler<AutomationElement?>? ValueChanged;

        /// <summary>A fresh snapshot of the window's automation tree.</summary>
        public AutomationElement Root => AutomationProvider.BuildTree (_window);

        private void OnSelectedControlChanged (object? sender, Control? control)
        {
            // Move value-change tracking to the newly focused control.
            DetachValueSource ();
            AttachValueSource (control);

            FocusChanged?.Invoke (this, control != null ? FindElement (control) : null);
        }

        private void OnValueChanged (object? sender, EventArgs e)
        {
            if (sender is Control c)
                ValueChanged?.Invoke (this, FindElement (c));
        }

        private void AttachValueSource (Control? control)
        {
            if (control == null)
                return;

            _valueSource = control;
            control.TextChanged += OnValueChanged;
            if (control is CheckBox cb)
                cb.CheckedChanged += OnValueChanged;
            else if (control is RadioButton rb)
                rb.CheckedChanged += OnValueChanged;
        }

        private void DetachValueSource ()
        {
            if (_valueSource == null)
                return;

            _valueSource.TextChanged -= OnValueChanged;
            if (_valueSource is CheckBox cb)
                cb.CheckedChanged -= OnValueChanged;
            else if (_valueSource is RadioButton rb)
                rb.CheckedChanged -= OnValueChanged;
            _valueSource = null;
        }

        // Locates the snapshot element backed by the given live control (exact reference match).
        private AutomationElement? FindElement (Control control) =>
            Root.Self ().FirstOrDefault (e => ReferenceEquals (e.Source, control));

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;

            _window.adapter.SelectedControlChanged -= OnSelectedControlChanged;
            DetachValueSource ();
        }
    }
}
