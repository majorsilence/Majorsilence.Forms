using System;

namespace Modern.Forms.Backends
{
    /// <summary>
    /// Holds the active <see cref="IPlatformBackend"/>. If unset, it is resolved automatically to the
    /// Avalonia backend (<c>Modern.Forms.Avalonia</c> assembly) when present. Assign a different
    /// backend (e.g. a Uno backend) before the first window is created or
    /// <see cref="Modern.Forms.Application.Run(Form)"/> is called.
    /// </summary>
    public static class Platform
    {
        private static IPlatformBackend? _backend;

        // The core no longer references any concrete backend; the default is discovered by name so the
        // Avalonia (or Uno) backend assembly stays an independent, swappable dependency.
        private const string DefaultBackendTypeName = "Modern.Forms.Backends.AvaloniaPlatformBackend, Modern.Forms.Avalonia";

        /// <summary>Gets or sets the active platform backend.</summary>
        public static IPlatformBackend Backend {
            get => _backend ??= ResolveDefaultBackend ();
            set => _backend = value;
        }

        private static IPlatformBackend ResolveDefaultBackend ()
        {
            var type = Type.GetType (DefaultBackendTypeName);

            if (type is null)
                throw new InvalidOperationException (
                    "No platform backend is configured. Reference a backend package (e.g. Modern.Forms.Avalonia) " +
                    "or set Modern.Forms.Backends.Platform.Backend before creating a window.");

            return (IPlatformBackend) Activator.CreateInstance (type)!;
        }
    }
}
