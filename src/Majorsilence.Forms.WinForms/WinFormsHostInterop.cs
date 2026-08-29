using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// Hands Majorsilence.Forms objects back to a host app that already runs classic
    /// System.Windows.Forms, so they can be used like any other native WinForms object instead of
    /// (or alongside) the usual <see cref="MF.Form.Show"/>/<see cref="MajorsilenceFormsPresenter"/>
    /// flow. The WinForms counterpart of <c>AvaloniaHostInterop</c>/<c>UnoHostInterop</c>.
    /// </summary>
    public static class WinFormsHostInterop
    {
        /// <summary>
        /// Wraps a Majorsilence.Forms <paramref name="control"/> in a
        /// <see cref="MajorsilenceFormsPresenter"/> so it can be dropped into a host WinForms form or
        /// container like any other native control. Equivalent to
        /// <c>new MajorsilenceFormsPresenter { Content = control }</c>.
        /// </summary>
        public static WF.Control ToWinFormsControl (this MF.Control control)
        {
            Guard.ThrowIfNull (control);
            return new MajorsilenceFormsPresenter { Content = control };
        }

        /// <summary>
        /// Gets the real <see cref="WF.Form"/> that backs this <paramref name="form"/>. A Form's
        /// backend window is created eagerly in its constructor (before <see cref="MF.Form.Show"/> is
        /// ever called), so the returned form already exists and already tracks the Form's
        /// <see cref="MF.Form.Text"/>/icon/size. The host app owns showing it from here on — set its
        /// <c>Owner</c>, call <c>Show()</c>/<c>ShowDialog(owner)</c>, etc. — and Majorsilence's own
        /// Load/Shown/<see cref="MF.Application.OpenForms"/> bookkeeping still runs correctly the first
        /// time the window actually becomes visible, whichever side triggered that.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The Form was not created under the WinForms backend.
        /// </exception>
        public static WF.Form ToWinFormsForm (this MF.Form form)
        {
            Guard.ThrowIfNull (form);

            if (form.Backend is not WinFormsWindowHost host)
                throw new InvalidOperationException (
                    "This Form was not created under the WinForms backend. Ensure " +
                    "Majorsilence.Forms.Backends.Platform.Backend is a WinFormsPlatformBackend " +
                    "before constructing the Form.");

            var native = host.NativeForm;

            // The host may show this form itself (rather than through Form.Show()/ShowDialog()), in
            // which case Majorsilence's own "just became visible" bookkeeping needs a trigger from the
            // WinForms side instead. EnsureShownBookkeeping is idempotent, so this is harmless even
            // when the Form is also shown the usual way.
            native.Shown += (_, _) => form.EnsureShownBookkeeping ();

            return native;
        }
    }
}
