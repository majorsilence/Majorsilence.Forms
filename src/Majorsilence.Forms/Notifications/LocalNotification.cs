namespace Majorsilence.Forms.Notifications
{
    /// <summary>
    /// A single notification to post through <see cref="LocalNotifications.Show"/>. An alert app's own
    /// ongoing "still ringing" cue is exactly why <see cref="Ongoing"/> and <see cref="FullScreen"/> exist:
    /// a warning or informational cue is neither.
    /// </summary>
    public sealed class LocalNotification
    {
        /// <summary>Gets or sets the id of the <see cref="NotificationChannel"/> this notification belongs to.</summary>
        /// <remarks>Must already be registered through <see cref="LocalNotifications.RegisterChannel"/> -- an unregistered id degrades to the platform's own default channel rather than throwing.</remarks>
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>Gets or sets the notification's title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Gets or sets the notification's body text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this notification cannot be dismissed by swiping -- an alarm that is still
        /// ringing, not a cue the user has already seen. Cleared the normal way: <see cref="LocalNotifications.Cancel"/>.
        /// </summary>
        public bool Ongoing { get; set; }

        /// <summary>
        /// Gets or sets whether this notification should interrupt with a full-screen launch of the app
        /// (Android's full-screen intent) rather than waiting to be opened -- what an alarm needs, and
        /// nothing else does; false shows an ordinary banner/list entry instead.
        /// </summary>
        public bool FullScreen { get; set; }
    }
}
