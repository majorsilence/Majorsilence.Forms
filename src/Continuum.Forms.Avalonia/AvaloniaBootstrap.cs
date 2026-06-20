using Avalonia;

namespace Continuum.Forms;

/// <summary>
/// Handles one-time Avalonia 12 platform initialization.
/// Must be called before any window or platform service is accessed.
/// </summary>
internal static class AvaloniaBootstrap
{
    private static bool _initialized;
    private static readonly object _lock = new();

    internal static void EnsureInitialized ()
    {
        if (_initialized)
            return;

        lock (_lock) {
            if (_initialized)
                return;

            // Avalonia's AppBuilder.Setup can only run once per process; if the platform has already
            // been configured (e.g. by a host app or a prior test in the same process), don't re-run it.
            if (Avalonia.Application.Current is null) {
                AppBuilder.Configure<MinimalApp> ()
                    .UsePlatformDetect ()
                    .UseSkia ()
                    .SetupWithoutStarting ();
            }

            _initialized = true;
        }
    }

    private sealed class MinimalApp : Avalonia.Application { }
}
