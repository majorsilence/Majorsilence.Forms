using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Holds the active <see cref="IPlatformBackend"/>. If unset, it is resolved automatically to the
    /// Avalonia backend (<c>Majorsilence.Forms.Avalonia</c> assembly) when present. Assign a different
    /// backend (e.g. a Uno backend) before the first window is created or
    /// <see cref="Majorsilence.Forms.Application.Run(Form)"/> is called.
    /// </summary>
    public static class Platform
    {
        private static IPlatformBackend? _backend;

        // The core no longer references any concrete backend; the default is discovered by name so the
        // Avalonia (or Uno) backend assembly stays an independent, swappable dependency.
        private const string DefaultBackendTypeName = "Majorsilence.Forms.Backends.AvaloniaPlatformBackend, Majorsilence.Forms.Avalonia";

        /// <summary>Gets or sets the active platform backend.</summary>
        public static IPlatformBackend Backend {
            get => _backend ??= ResolveDefaultBackend ();
            set => _backend = value;
        }

        /// <summary>
        /// Gets the backend that has already been assigned, or null when none has been. Unlike
        /// <see cref="Backend"/> this never resolves — or throws for — a default, so a caller that is
        /// about to install its own backend can inspect what is there without tripping over the
        /// "no platform backend is configured" error it exists to prevent.
        /// </summary>
        public static IPlatformBackend? ConfiguredBackend => _backend;

        private static IPlatformBackend ResolveDefaultBackend ()
        {
            var type = Type.GetType (DefaultBackendTypeName);

            if (type is null)
                throw new InvalidOperationException (
                    "No platform backend is configured. Reference a backend package (e.g. Majorsilence.Forms.Avalonia) " +
                    "or set Majorsilence.Forms.Backends.Platform.Backend before creating a window.");

            return (IPlatformBackend) Activator.CreateInstance (type)!;
        }
    }
}
