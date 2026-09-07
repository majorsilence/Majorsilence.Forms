using System;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Hands Majorsilence.Forms objects back to a host app that already owns a GTK 4
    /// <c>Gtk.Application</c> / main loop, so they can be used like any other GTK object instead of (or
    /// alongside) the usual <see cref="MF.Form.Show"/> flow. The GTK 4 counterpart of
    /// <c>AvaloniaHostInterop</c> / <c>UnoHostInterop</c> / <c>WpfHostInterop</c>.
    /// </summary>
    public static class Gtk4HostInterop
    {
        /// <summary>
        /// Wraps a Majorsilence.Forms <paramref name="control"/> in a <see cref="MajorsilenceFormsPresenter"/>
        /// and returns its GTK widget, ready to drop into any GTK container. Equivalent to
        /// <c>new MajorsilenceFormsPresenter { Content = control }.Widget</c>. Use the presenter directly
        /// when you need <see cref="MajorsilenceFormsPresenter.Surface"/> or <see cref="IDisposable"/>.
        /// </summary>
        public static global::Gtk.Widget ToGtkWidget (this MF.Control control)
        {
            ArgumentNullException.ThrowIfNull (control);
            return new MajorsilenceFormsPresenter { Content = control }.Widget;
        }

        /// <summary>
        /// Gets the real <c>Gtk.Window</c> that backs this <paramref name="form"/>. A Form's backend
        /// window is created eagerly in its constructor (before <see cref="MF.Form.Show"/> is ever
        /// called), so the returned window already exists and already tracks the Form's
        /// <see cref="MF.Form.Text"/>/size. The host app owns showing it from here on — set its
        /// transient parent, call <c>Present()</c>, etc. — and Majorsilence's own
        /// Load/Shown/<see cref="MF.Application.OpenForms"/> bookkeeping still runs correctly the first
        /// time the window actually becomes visible, whichever side triggered that.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Form was not created under the GTK 4 backend.</exception>
        public static global::Gtk.Window ToGtkWindow (this MF.Form form)
        {
            ArgumentNullException.ThrowIfNull (form);

            if (form.Backend is not Gtk4WindowHost host)
                throw new InvalidOperationException (
                    "This Form was not created under the GTK 4 backend. Ensure " +
                    "Majorsilence.Forms.Backends.Platform.Backend is a Gtk4PlatformBackend " +
                    "(call Gtk4Application.Use ()) before constructing the Form.");

            var window = host.NativeWindow;

            // The host may present this window itself rather than through Form.Show(); give
            // Majorsilence's "just became visible" bookkeeping a trigger from the GTK side too.
            // EnsureShownBookkeeping is idempotent, so this is harmless when the Form is also shown
            // the usual way.
            window.OnRealize += (_, _) => form.EnsureShownBookkeeping ();

            return window;
        }
    }
}
