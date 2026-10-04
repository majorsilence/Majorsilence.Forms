using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Entry point for the terminal backend. Call <see cref="Use()"/> once before creating any window,
    /// then run the app the usual way (<c>Application.Run (new MainForm ())</c>). Ctrl+C exits.
    /// </summary>
    public static class TerminalApplication
    {
        /// <summary>Installs the terminal backend with default options (idempotent).</summary>
        public static void Use () => Use (new TerminalOptions ());

        /// <summary>Installs the terminal backend with the given options (idempotent).</summary>
        public static void Use (TerminalOptions options)
        {
            System.ArgumentNullException.ThrowIfNull (options);

            // ConfiguredBackend, not Backend: the getter resolves (and throws for) a default backend.
            if (Platform.ConfiguredBackend is not TerminalPlatformBackend)
                Platform.Backend = new TerminalPlatformBackend (options);
        }
    }
}
