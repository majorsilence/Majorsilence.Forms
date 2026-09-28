using System;

namespace Majorsilence.Forms.Notifications
{
    /// <summary>
    /// A named category of notification -- Android has required one since API 26 (every notification
    /// posted there belongs to a channel, and the user mutes/configures importance and sound per channel,
    /// not per notification), so <see cref="LocalNotifications"/> models the same shape everywhere rather
    /// than inventing a per-notification importance/sound that Android could not actually honour.
    /// </summary>
    public sealed class NotificationChannel
    {
        /// <summary>Initializes a channel with the given id and display name.</summary>
        public NotificationChannel (string id, string name)
        {
            Id = id ?? throw new ArgumentNullException (nameof (id));
            Name = name ?? throw new ArgumentNullException (nameof (name));
        }

        /// <summary>
        /// Gets the channel's stable identifier -- once a backend has created a channel under a given id,
        /// re-registering the same id with different settings does not change it on Android (the platform's
        /// own rule: a channel's settings are the user's to change after creation, not the app's).
        /// </summary>
        public string Id { get; }

        /// <summary>Gets or sets the channel's display name, shown in the system notification settings.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets an optional longer description, shown in the system notification settings.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets how intrusively this channel's notifications interrupt the user.</summary>
        public NotificationImportance Importance { get; set; } = NotificationImportance.Default;

        /// <summary>Gets or sets whether this channel's notifications play the platform's default notification sound.</summary>
        public bool Sound { get; set; } = true;
    }
}
