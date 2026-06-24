using System;
using System.Collections.Generic;

namespace Majorsilence.Forms.WindowsUIAutomation
{
    /// <summary>
    /// Entry point for the Windows UI Automation accessibility bridge. Call <see cref="Enable"/> on a shown
    /// window to expose its controls to screen readers (Narrator/NVDA/JAWS) and focus-following magnifiers;
    /// the bridge detaches automatically when the window closes, or call <see cref="Disable"/>.
    ///
    /// Windows-only. The bridge reads the backend-neutral Majorsilence.Forms automation tree, so it works for
    /// any Windows host (Avalonia/Uno) that can supply a native window handle.
    /// </summary>
    public static class WindowsUIAutomation
    {
        private static readonly Dictionary<WindowBase, UiaBridge> Bridges = new ();

        /// <summary>
        /// Attaches the UI Automation bridge to a window. The window must already be shown (so it has a
        /// native handle). Safe to call more than once per window (subsequent calls are no-ops).
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
        /// <exception cref="NotSupportedException">The window has no native handle (not shown yet, or the backend can't provide one).</exception>
        public static void Enable (WindowBase window)
        {
            EnsureWindows ();
            ArgumentNullException.ThrowIfNull (window);

            if (Bridges.ContainsKey (window))
                return;

            var hwnd = window.PlatformHandle;
            if (hwnd == IntPtr.Zero)
                throw new NotSupportedException (
                    "The window has no native handle. Show the window before enabling UI Automation, " +
                    "and ensure the active backend exposes a platform handle on Windows.");

            var bridge = new UiaBridge (window, hwnd);
            Bridges[window] = bridge;
            window.Closed += OnWindowClosed;
        }

        /// <summary>Detaches the UI Automation bridge from a window. A no-op if it was never enabled.</summary>
        public static void Disable (WindowBase window)
        {
            EnsureWindows ();

            if (window == null || !Bridges.TryGetValue (window, out var bridge))
                return;

            window.Closed -= OnWindowClosed;
            bridge.Dispose ();
            Bridges.Remove (window);
        }

        private static void OnWindowClosed (object? sender, EventArgs e)
        {
            if (sender is WindowBase window)
                Disable (window);
        }

        private static void EnsureWindows ()
        {
            if (!OperatingSystem.IsWindows ())
                throw new PlatformNotSupportedException ("Windows UI Automation is only available on Windows.");
        }
    }
}
