using Avalonia;
#if BROWSER
using Avalonia.Browser;
#endif

namespace Majorsilence.Forms;

/// <summary>
/// Handles one-time Avalonia 12 platform initialization.
/// Must be called before any window or platform service is accessed.
/// </summary>
internal static class AvaloniaBootstrap
{
    private static bool _initialized;
    private static readonly object _lock = new();

    /// <summary>Whether platform bootstrap has completed (on either the desktop or browser path).</summary>
    internal static bool IsInitialized => _initialized;

#if ANDROID
    // Android has no synchronous bootstrap of its own to run here: UsePlatformDetect() (the desktop path
    // just below) needs Avalonia.Desktop, which isn't referenced on this TFM, and there's no windowless
    // AppBuilder.Setup equivalent for Android besides the one AvaloniaMainActivity already performs.
    // AvaloniaMainActivity<TApp> (Avalonia.Android) always runs AppBuilder.Configure<TApp>().UseAndroid()
    // ....SetupWithLifetime(this) itself before this can ever be reached -- Application.RunAndroid, the
    // only caller, is only ever invoked from MainActivity.OnCreate after base.OnCreate() -- so
    // Avalonia.Application.Current is guaranteed already set here; there is nothing left to bootstrap.
    internal static void EnsureInitialized ()
    {
        if (_initialized)
            return;

        lock (_lock) {
            if (Avalonia.Application.Current is null)
                throw new InvalidOperationException (
                    "The Avalonia Android backend must be bootstrapped by AvaloniaMainActivity (Avalonia.Android) before any Majorsilence.Forms window is created.");

            _initialized = true;
        }
    }
#elif !BROWSER
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
#else
    private static Task? _initializing;

    // Unlike the desktop SetupWithoutStarting() above, the browser platform bootstraps asynchronously
    // (StartBrowserAppAsync attaches to the host <div>, negotiates the WebGL/Skia canvas, and only then
    // completes) and must not be run more than once concurrently, hence the shared in-flight Task rather
    // than just a bool-guarded lock.
    internal static Task EnsureInitializedBrowserAsync (string mainDivId)
    {
        if (_initialized)
            return Task.CompletedTask;

        lock (_lock) {
            if (_initialized)
                return Task.CompletedTask;

            return _initializing ??= InitializeBrowserCoreAsync (mainDivId);
        }
    }

    private static async Task InitializeBrowserCoreAsync (string mainDivId)
    {
        await AppBuilder.Configure<MinimalApp> ()
            .StartBrowserAppAsync (mainDivId)
            .ConfigureAwait (true);

        _initialized = true;
    }
#endif

    private sealed class MinimalApp : Avalonia.Application { }
}
