using System;

namespace Majorsilence.Forms.Notifications
{
    /// <summary>Carries the id of the <see cref="LocalNotification"/> the user tapped, raised by <see cref="LocalNotifications.Tapped"/>.</summary>
    public sealed class NotificationTappedEventArgs : EventArgs
    {
        /// <summary>Initializes the event args with the tapped notification's id.</summary>
        public NotificationTappedEventArgs (int id) => Id = id;

        /// <summary>Gets the id of the notification that was tapped, as passed to <see cref="LocalNotifications.Show"/>.</summary>
        public int Id { get; }
    }
}
