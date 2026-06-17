using Avalonia;

namespace Modern.Forms;

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

            AppBuilder.Configure<MinimalApp> ()
                .UsePlatformDetect ()
                .UseSkia ()
                .SetupWithoutStarting ();

            _initialized = true;
        }
    }

    private sealed class MinimalApp : Avalonia.Application { }
}
