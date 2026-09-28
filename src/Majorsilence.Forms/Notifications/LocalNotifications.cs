using System;

namespace Majorsilence.Forms.Notifications
{
    /// <summary>
    /// Real local notifications where the platform has a notification centre to post to (Android; iOS is
    /// tracked separately) and a reported no-op everywhere else (desktop, browser, Headless) -- register
    /// item F14's mobile half. Every member is fire-and-forget and never throws: a missing backend, a
    /// denied permission, or a platform failure all degrade to silently doing nothing, the same contract
    /// <see cref="Haptics"/> already uses for its own cues.
    /// </summary>
    public static class LocalNotifications
    {
        /// <summary>Gets whether the active backend can actually post anything -- true on Android, false everywhere else.</summary>
        public static bool IsSupported => Backends.Platform.Backend is Backends.INotificationBackend;

        /// <summary>
        /// Gets whether posting a notification is currently allowed. Always <c>false</c> where
        /// <see cref="IsSupported"/> is <c>false</c> -- the same conservative default every other
        /// capability member on this page uses when there is nothing real to answer from.
        /// </summary>
        public static bool IsPermissionGranted
            => Backends.Platform.Backend is Backends.INotificationBackend backend && backend.IsPermissionGranted;

        /// <summary>
        /// Asks the user for the runtime notification permission where the platform has one (Android 13+).
        /// A no-op everywhere else, including an Android backend that already knows the answer. The result
        /// arrives later as <see cref="PermissionChanged"/>, never a return value here.
        /// </summary>
        public static void RequestPermission ()
        {
            if (Backends.Platform.Backend is Backends.INotificationBackend backend)
                backend.RequestPermission ();
        }

        /// <summary>Raised, on the UI thread, after <see cref="RequestPermission"/>'s prompt is answered, or when the permission changes from the system settings while running.</summary>
        public static event EventHandler? PermissionChanged;

        /// <summary>
        /// Called by a backend to report that <see cref="IsPermissionGranted"/> may have changed. Public,
        /// like <see cref="Application.RaiseSuspended"/> -- the only caller lives in a separate backend
        /// assembly, not this one.
        /// </summary>
        public static void RaisePermissionChanged () => PermissionChanged?.Invoke (null, EventArgs.Empty);

        /// <summary>
        /// Registers a notification channel. A no-op where <see cref="IsSupported"/> is <c>false</c> --
        /// safe to call unconditionally during startup, the same "never worth a crash" contract every
        /// other member here uses.
        /// </summary>
        public static void RegisterChannel (NotificationChannel channel)
        {
            Guard.ThrowIfNull (channel);

            if (Backends.Platform.Backend is Backends.INotificationBackend backend)
                backend.RegisterChannel (channel.Id, channel.Name, channel.Description, channel.Importance, channel.Sound);
        }

        /// <summary>Posts (or replaces, if <paramref name="id"/> is already showing) a notification. A no-op where <see cref="IsSupported"/> is <c>false</c>.</summary>
        public static void Show (int id, LocalNotification notification)
        {
            Guard.ThrowIfNull (notification);

            if (Backends.Platform.Backend is Backends.INotificationBackend backend)
                backend.Show (id, notification.ChannelId, notification.Title, notification.Text, notification.Ongoing, notification.FullScreen);
        }

        /// <summary>Dismisses a notification posted with the given <paramref name="id"/>, if still showing. A no-op where <see cref="IsSupported"/> is <c>false</c>, or the id is not showing.</summary>
        public static void Cancel (int id)
        {
            if (Backends.Platform.Backend is Backends.INotificationBackend backend)
                backend.Cancel (id);
        }

        /// <summary>Raised, on the UI thread, when the user taps a posted notification.</summary>
        public static event EventHandler<NotificationTappedEventArgs>? Tapped;

        /// <summary>
        /// Called by a backend when a notification is tapped. Public, like <see cref="Application.RaiseSuspended"/>
        /// and <see cref="WindowBase.RaiseBackRequested"/> -- the only caller lives in a separate backend
        /// assembly, not this one.
        /// </summary>
        public static void RaiseTapped (int id) => Tapped?.Invoke (null, new NotificationTappedEventArgs (id));
    }
}
