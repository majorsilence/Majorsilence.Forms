using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend that can put an icon in the operating
    /// system's notification area (the Windows tray, the macOS menu bar, a Linux StatusNotifier host).
    /// Discovered via <c>Platform.Backend as ITrayIconBackend</c>, the same optional-capability pattern
    /// <see cref="IHapticsBackend"/> and <see cref="INotificationBackend"/> use: a backend whose platform
    /// has no tray (GTK 4, browser, Android, iOS, terminal) simply does not implement it, and
    /// <see cref="NotifyIcon"/> reports that through <c>System.Diagnostics.Trace</c> when an application
    /// asks for a visible icon.
    /// </summary>
    /// <remarks>
    /// Per application rather than per window, because a tray icon belongs to no window: the classic
    /// "minimise to tray" application hides every window and leaves only the icon. So this sits beside
    /// <see cref="IPlatformBackend"/>, not on <see cref="IWindowBackend"/>.
    /// </remarks>
    public interface ITrayIconBackend
    {
        /// <summary>
        /// Creates the native icon for <paramref name="owner"/>, initially hidden. Returns null when the
        /// running platform turns out to have no tray after all, which <see cref="NotifyIcon"/> reports
        /// the same way as a backend without this interface.
        /// </summary>
        /// <remarks>
        /// The "push" side is the backend calling the owner's neutral handlers -- the tray's own mouse
        /// messages and balloon notifications -- exactly as a window backend calls
        /// <c>WindowBase.HandlePointerPressed</c>; no platform type crosses the seam.
        /// </remarks>
        ITrayIconHandle? CreateTrayIcon (NotifyIcon owner);
    }

    /// <summary>
    /// One native notification-area icon, created by <see cref="ITrayIconBackend.CreateTrayIcon"/>. The
    /// "pull" side: the operations <see cref="NotifyIcon"/> invokes on it. Disposing removes the icon.
    /// </summary>
    public interface ITrayIconHandle : IDisposable
    {
        /// <summary>Shows or removes the icon. <see cref="NotifyIcon"/> only asks for it while it has an icon image, as upstream's <c>UpdateIcon</c> only adds one then.</summary>
        void SetVisible (bool visible);

        /// <summary>Sets the icon image from PNG bytes, or clears it when null.</summary>
        void SetIcon (byte[]? iconPng);

        /// <summary>Sets the tooltip text shown when the pointer rests on the icon.</summary>
        void SetText (string text);

        /// <summary>
        /// Gives the backend the menu to show on its own, for a platform that only offers a native tray
        /// menu and never reports the right-click itself (Avalonia's <c>TrayIcon.Menu</c>). A backend that
        /// reports right-clicks to <see cref="NotifyIcon"/> ignores this: the owner then shows
        /// <see cref="NotifyIcon.ContextMenuStrip"/> itself, as upstream's <c>ShowContextMenu</c> does.
        /// </summary>
        void SetContextMenu (ContextMenuStrip? menu);

        /// <summary>
        /// Shows a balloon / native notification. Returns false when the platform has no way to show
        /// one, so <see cref="NotifyIcon"/> can report it instead of pretending.
        /// </summary>
        bool ShowBalloonTip (int timeout, string title, string text, ToolTipIcon icon);
    }
}
