using System;
using MF = Majorsilence.Forms;
using WpfFrameworkElement = System.Windows.FrameworkElement;
using WpfWindow = System.Windows.Window;

namespace Majorsilence.Forms.Wpf
{
    /// <summary>
    /// Hands Majorsilence.Forms objects back to a host app that already owns a WPF
    /// <see cref="System.Windows.Application"/>, so they can be used like any other WPF object instead
    /// of (or alongside) the usual <see cref="MF.Form.Show"/>/<see cref="MajorsilenceFormsPresenter"/>
    /// flow. The WPF counterpart of <c>AvaloniaHostInterop</c>/<c>WinFormsHostInterop</c>.
    /// </summary>
    public static class WpfHostInterop
    {
        /// <summary>
        /// Wraps a Majorsilence.Forms <paramref name="control"/> in a
        /// <see cref="MajorsilenceFormsPresenter"/> so it can be dropped into a host WPF visual tree
        /// like any other element. Equivalent to <c>new MajorsilenceFormsPresenter { Content = control }</c>.
        /// </summary>
        public static WpfFrameworkElement ToWpfElement (this MF.Control control)
        {
            Guard.ThrowIfNull (control);
            return new MajorsilenceFormsPresenter { Content = control };
        }

        /// <summary>
        /// Gets the real <see cref="WpfWindow"/> that backs this <paramref name="form"/>. A Form's
        /// backend window is created eagerly in its constructor (before <see cref="MF.Form.Show"/> is
        /// ever called), so the returned window already exists and already tracks the Form's
        /// <see cref="MF.Form.Text"/>/icon/size. The host app owns showing it from here on — set its
        /// <c>Owner</c>, call <c>Show()</c>/<c>ShowDialog()</c>, etc. — and Majorsilence's own
        /// Load/Shown/<see cref="MF.Application.OpenForms"/> bookkeeping still runs correctly the first
        /// time the window actually becomes visible, whichever side triggered that.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The Form was not created under the WPF backend.
        /// </exception>
        public static WpfWindow ToWpfWindow (this MF.Form form)
        {
            Guard.ThrowIfNull (form);

            if (form.Backend is not WpfWindowHost host)
                throw new InvalidOperationException (
                    "This Form was not created under the WPF backend. Ensure " +
                    "Majorsilence.Forms.Backends.Platform.Backend is a WpfPlatformBackend " +
                    "before constructing the Form.");

            var native = host.NativeWindow;
            native.Loaded += (_, _) => form.EnsureShownBookkeeping ();
            return native;
        }
    }
}
