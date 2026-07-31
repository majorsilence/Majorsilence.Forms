namespace Majorsilence.Forms.Uno
{
    /// <summary>
    /// Hands Majorsilence.Forms objects back to a host app that already owns a Uno.WinUI
    /// <see cref="Microsoft.UI.Xaml.Application"/>, so they can be used like any other native Uno object
    /// instead of (or alongside) the usual <see cref="Majorsilence.Forms.Form.Show"/>/
    /// <see cref="MajorsilenceFormsPresenter"/> flow. The reverse direction -- Majorsilence.Forms hosting
    /// Uno as its rendering backend -- is unaffected; these are purely additive.
    ///
    /// Unlike the Avalonia backend, a Uno <see cref="Microsoft.UI.Xaml.Window"/> has no native
    /// owner/modal-dialog relationship in this backend (<see cref="UnoWindowHost"/>'s own
    /// <c>ShowDialog</c> already just shows the window and ignores any owner): use
    /// <see cref="Majorsilence.Forms.Form.ShowDialog(Majorsilence.Forms.Form)"/> for modal behaviour
    /// regardless of how the window is hosted.
    /// </summary>
    public static class UnoHostInterop
    {
        /// <summary>
        /// Gets the real <see cref="Microsoft.UI.Xaml.Window"/> that backs this <paramref name="form"/>.
        /// A Form's backend window is created eagerly in its constructor (before
        /// <see cref="Majorsilence.Forms.Form.Show"/> is ever called), so the returned window already
        /// exists and already tracks the Form's <see cref="Majorsilence.Forms.Form.Text"/>/icon/size. The
        /// host app owns showing it from here on, and Majorsilence's own Load/Shown/
        /// <see cref="Majorsilence.Forms.Application.OpenForms"/> bookkeeping still runs correctly the
        /// first time the window actually becomes visible, whichever side triggered that.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The Form was not created under the Uno backend, or its native window has not been created
        /// (e.g. it is a popup rather than a top-level Form).
        /// </exception>
        public static Microsoft.UI.Xaml.Window ToUnoWindow (this Majorsilence.Forms.Form form)
        {
            ArgumentNullException.ThrowIfNull (form);

            if (form.Backend is not UnoWindowHost host || host.NativeWindow is null)
                throw new InvalidOperationException (
                    "This Form was not created under the Uno backend, or its native window has not " +
                    "been created yet. Ensure Majorsilence.Forms.Backends.Platform.Backend is a " +
                    "UnoPlatformBackend (with a running Uno Application/DispatcherQueue) before " +
                    "constructing the Form.");

            // The host may show this window itself (rather than through Form.Show()/ShowDialog()), in
            // which case Majorsilence's own "just became visible" bookkeeping needs a trigger from the
            // Uno side instead. Activated is the same lifecycle signal UnoWindowHost's own
            // WireLifecycle uses; EnsureShownBookkeeping is idempotent, so repeated activations (and
            // showing the Form the usual way too) are harmless.
            host.NativeWindow.Activated += (_, _) => form.EnsureShownBookkeeping ();

            return host.NativeWindow;
        }

        /// <summary>
        /// Wraps a Majorsilence.Forms <paramref name="control"/> in a <see cref="MajorsilenceFormsPresenter"/>
        /// so it can be dropped into a host Uno visual tree like any other native control. Equivalent to
        /// <c>new MajorsilenceFormsPresenter { Content = control }</c>.
        /// </summary>
        public static Microsoft.UI.Xaml.FrameworkElement ToUnoControl (this Majorsilence.Forms.Control control)
        {
            ArgumentNullException.ThrowIfNull (control);
            return new MajorsilenceFormsPresenter { Content = control };
        }
    }
}
