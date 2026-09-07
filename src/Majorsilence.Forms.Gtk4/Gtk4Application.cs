using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Convenience entry point for the GTK 4 backend. Unlike the default (Avalonia) backend it is
    /// selected explicitly — call <see cref="Use"/> once before creating any window, then drive the
    /// app the usual way (<c>Majorsilence.Forms.Application.Run (new MainForm ())</c>).
    /// </summary>
    public static class Gtk4Application
    {
        /// <summary>Installs <see cref="Gtk4PlatformBackend"/> as the active platform backend (idempotent).</summary>
        public static void Use ()
        {
            // ConfiguredBackend, not Backend: the Backend getter resolves (and throws for) a default
            // backend, which is exactly the case this method exists to handle.
            if (Platform.ConfiguredBackend is not Gtk4PlatformBackend)
                Platform.Backend = new Gtk4PlatformBackend ();
        }
    }
}
