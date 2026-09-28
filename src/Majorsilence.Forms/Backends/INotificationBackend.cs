namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend that can post real local notifications
    /// (Android's <c>NotificationManager</c>, channels and full-screen intents). Discovered via
    /// <c>Platform.Backend as INotificationBackend</c>, the same optional-capability pattern
    /// <see cref="IAudioBackend"/> and <see cref="IHapticsBackend"/> use -- a backend with nothing to offer
    /// (desktop, browser, Headless) simply does not implement it, the same "no supported-but-does-nothing
    /// middle state" reasoning <see cref="IHapticsBackend"/>'s own remarks document.
    /// </summary>
    public interface INotificationBackend
    {
        /// <summary>
        /// Gets whether posting a notification is currently allowed -- always <c>true</c> below Android 13
        /// (API 33), where no runtime permission exists for this; re-read fresh from the platform each
        /// call, never cached, so it reflects a result <see cref="RequestPermission"/> already brought back
        /// or a change made from the system settings while the app was not running.
        /// </summary>
        bool IsPermissionGranted { get; }

        /// <summary>
        /// Asks the user for the runtime notification permission (Android 13+ only -- a no-op, not an
        /// error, everywhere <see cref="IsPermissionGranted"/> is already meaningless or already
        /// answered). Fire-and-forget: the result arrives later as
        /// <see cref="Notifications.LocalNotifications.PermissionChanged"/>, never a return value here.
        /// </summary>
        void RequestPermission ();

        /// <summary>
        /// Registers a notification channel, creating it on the platform if it does not already exist.
        /// Idempotent -- re-registering an existing channel id never changes its already-created settings
        /// (the platform's own rule, not enforced here: see <see cref="Notifications.NotificationChannel.Id"/>).
        /// </summary>
        /// <remarks>
        /// Takes the <see cref="Notifications.NotificationChannel"/>'s fields individually rather than the
        /// object itself -- like <see cref="IAudioBackend.PlayTrack"/> takes an <c>AudioUsage</c>, a
        /// <c>volume</c> and a <c>loop</c> rather than an <c>AudioPlayer</c> -- so
        /// <see cref="Notifications.LocalNotifications.RegisterChannel"/> reads every field itself before
        /// forwarding here. That read is what keeps each field off `StoredOnlyPropertyBaselineTests`:
        /// the only real reader otherwise lives in a backend assembly the core assembly's own scan never
        /// sees.
        /// </remarks>
        void RegisterChannel (string id, string name, string? description, Notifications.NotificationImportance importance, bool sound);

        /// <summary>Posts (or replaces, if <paramref name="id"/> is already showing) a notification. See <see cref="RegisterChannel"/>'s remarks for why this takes fields, not a <see cref="Notifications.LocalNotification"/>.</summary>
        void Show (int id, string channelId, string title, string text, bool ongoing, bool fullScreen);

        /// <summary>Dismisses a notification posted with the given <paramref name="id"/>, if still showing. Never throws for an id that is not.</summary>
        void Cancel (int id);
    }
}
