namespace Majorsilence.Forms
{
    /// <summary>
    /// Hands Majorsilence.Forms objects back to a host app that already owns an Avalonia
    /// <see cref="Avalonia.Application"/>, so they can be used like any other native Avalonia object
    /// instead of (or alongside) the usual <see cref="Form.Show"/>/<see cref="MajorsilenceFormsPresenter"/>
    /// flow. The reverse direction -- Majorsilence.Forms hosting Avalonia as its rendering backend -- is
    /// unaffected; these are purely additive.
    /// </summary>
    public static class AvaloniaHostInterop
    {
        /// <summary>
        /// Gets the real <see cref="Avalonia.Controls.Window"/> that backs this <paramref name="form"/>.
        /// A Form's backend window is created eagerly in its constructor (before <see cref="Form.Show"/>
        /// is ever called), so the returned window already exists and already tracks the Form's
        /// <see cref="Form.Text"/>/icon/size. The host app owns showing it from here on -- assign it to
        /// <c>IClassicDesktopStyleApplicationLifetime.MainWindow</c>, set its <c>Owner</c> and call
        /// <c>Show()</c>/<c>ShowDialog(owner)</c>, etc. -- and Majorsilence's own Load/Shown/
        /// <see cref="Application.OpenForms"/> bookkeeping still runs correctly the first time the window
        /// actually becomes visible, whichever side triggered that.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The Form was not created under the Avalonia backend.
        /// </exception>
        public static Avalonia.Controls.Window ToAvaloniaWindow (this Form form)
        {
            ArgumentNullException.ThrowIfNull (form);

            if (form.Backend is not Avalonia.Controls.Window window)
                throw new InvalidOperationException (
                    "This Form was not created under the Avalonia backend. Ensure " +
                    "Majorsilence.Forms.Backends.Platform.Backend is an AvaloniaPlatformBackend " +
                    "(and Avalonia is bootstrapped) before constructing the Form.");

            // The host may show this window itself (rather than through Form.Show()/ShowDialog()), in
            // which case Majorsilence's own "just became visible" bookkeeping needs a trigger from the
            // Avalonia side instead. EnsureShownBookkeeping is idempotent, so this is harmless even when
            // the Form is also shown the usual way.
            window.Opened += (_, _) => form.EnsureShownBookkeeping ();

            return window;
        }

        /// <summary>
        /// Wraps a Majorsilence.Forms <paramref name="control"/> in a <see cref="MajorsilenceFormsPresenter"/>
        /// so it can be dropped into a host Avalonia visual tree like any other native control. Equivalent
        /// to <c>new MajorsilenceFormsPresenter { Content = control }</c>.
        /// </summary>
        public static Avalonia.Controls.Control ToAvaloniaControl (this Control control)
        {
            ArgumentNullException.ThrowIfNull (control);
            return new MajorsilenceFormsPresenter { Content = control };
        }
    }
}
