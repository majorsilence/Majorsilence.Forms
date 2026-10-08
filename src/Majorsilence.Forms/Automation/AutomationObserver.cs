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

            lock (live)
                live.Add (this);
        }

        // Every observer alive, so Control.AccessibilityNotifyClients can reach the ones watching the
        // control's window (W6 mechanisms).
        private static readonly System.Collections.Generic.List<AutomationObserver> live = new ();

        internal static void Notify (Control control, AccessibleEvents accEvent)
        {
            AutomationObserver[] observers;

            lock (live)
                observers = live.ToArray ();

            if (observers.Length == 0)
                return;

            var window = control.FindWindow ();

            foreach (var observer in observers) {
                if (!ReferenceEquals (observer._window, window) && !ReferenceEquals (observer._window.adapter, control))
                    continue;

                switch (accEvent) {
                case AccessibleEvents.Focus:
                    observer.FocusChanged?.Invoke (observer, observer.FindElement (control));
                    break;
                case AccessibleEvents.ValueChange:
                case AccessibleEvents.NameChange:
                case AccessibleEvents.StateChange:
                case AccessibleEvents.Selection:
                    observer.ValueChanged?.Invoke (observer, observer.FindElement (control));
                    break;
                }
            }
        }

        // A live region's change (Label.LiveSetting, ToolStripStatusLabel.LiveSetting), from
        // LiveAnnouncer.LiveRegionChanged: delivered to each observer of the owner's window with a
        // LiveRegionChanged handler. True when one took it. A tool strip item is not a Control, so its
        // window is its strip's; the tree already carries an element for it (AutomationProvider.BuildItems),
        // which is what the UI Automation bridge raises the event on.
        internal static bool NotifyLiveRegionChanged (object owner)
        {
            var window = owner switch {
                Control control => control.FindWindow (),
                MenuItem item => item.OwnerControl?.FindWindow (),
                _ => null,
            };

            if (window is null)
                return false;

            AutomationObserver[] observers;

            lock (live)
                observers = live.ToArray ();

            var delivered = false;

            foreach (var observer in observers) {
                if (!ReferenceEquals (observer._window, window) || observer.LiveRegionChanged is not { } handler)
                    continue;

                handler (observer, observer.FindElement (owner));
                delivered = true;
            }

            return delivered;
        }

        /// <summary>
        /// Raised when a live region in the window -- a <see cref="Label"/> or
        /// <see cref="ToolStripStatusLabel"/> whose <c>LiveSetting</c> is not
        /// <see cref="AutomationLiveSetting.Off"/> -- changes, so a screen reader announces it. The element
        /// is the label's, carrying its <see cref="AutomationElement.LiveSetting"/>.
        /// </summary>
        public event EventHandler<AutomationElement?>? LiveRegionChanged;

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

        // Locates the snapshot element backed by the given live control or item (exact reference match).
        private AutomationElement? FindElement (object source) =>
            Root.Self ().FirstOrDefault (e => ReferenceEquals (e.Source, source));

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;

            lock (live)
                live.Remove (this);

            _window.adapter.SelectedControlChanged -= OnSelectedControlChanged;
            DetachValueSource ();
        }
    }
}
