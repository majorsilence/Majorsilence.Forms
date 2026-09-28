namespace Majorsilence.Forms.Notifications
{
    /// <summary>
    /// How intrusively a <see cref="NotificationChannel"/>'s notifications should interrupt the user --
    /// Android's own three-way split (a channel's importance, not a per-notification setting there).
    /// </summary>
    public enum NotificationImportance
    {
        /// <summary>No sound, and minimised in the notification list -- Android's <c>IMPORTANCE_LOW</c>.</summary>
        Low,

        /// <summary>Makes a sound, shown in the notification list -- Android's <c>IMPORTANCE_DEFAULT</c>.</summary>
        Default,

        /// <summary>
        /// Makes a sound and interrupts with a heads-up banner -- Android's <c>IMPORTANCE_HIGH</c>. What an
        /// alert app's own alarm channel needs.
        /// </summary>
        High,
    }
}
