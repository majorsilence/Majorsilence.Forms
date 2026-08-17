using System;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Identifies the category of a user-preference change, as
    /// <c>Microsoft.Win32.UserPreferenceCategory</c> does on Windows.
    /// </summary>
    public enum UserPreferenceCategory
    {
        /// <summary>An accessibility setting changed.</summary>
        Accessibility = 1,
        /// <summary>A system colour changed.</summary>
        Color = 2,
        /// <summary>A desktop setting, such as the wallpaper, changed.</summary>
        Desktop = 3,
        /// <summary>A general setting with no more specific category changed.</summary>
        General = 4,
        /// <summary>An icon setting, such as icon size or spacing, changed.</summary>
        Icon = 5,
        /// <summary>A keyboard setting, such as the repeat rate, changed.</summary>
        Keyboard = 6,
        /// <summary>A menu setting, such as the show delay, changed.</summary>
        Menu = 7,
        /// <summary>A mouse setting, such as the double-click time, changed.</summary>
        Mouse = 8,
        /// <summary>A system policy changed.</summary>
        Policy = 9,
        /// <summary>A power setting changed.</summary>
        Power = 10,
        /// <summary>A screen-saver setting changed.</summary>
        Screensaver = 11,
        /// <summary>A window setting, such as the caption metrics, changed.</summary>
        Window = 12,
        /// <summary>A locale setting, such as the date format, changed.</summary>
        Locale = 13,
        /// <summary>The visual style (theme) changed.</summary>
        VisualStyle = 14,
    }

    /// <summary>Carries the category of a user-preference change.</summary>
    public class UserPreferenceChangedEventArgs : EventArgs
    {
        /// <summary>Initializes the arguments with the category that changed.</summary>
        public UserPreferenceChangedEventArgs (UserPreferenceCategory category) => Category = category;

        /// <summary>Gets the category of user preference that changed.</summary>
        public UserPreferenceCategory Category { get; }
    }

    /// <summary>Handles <see cref="SystemEvents.UserPreferenceChanged"/>.</summary>
    public delegate void UserPreferenceChangedEventHandler (object sender, UserPreferenceChangedEventArgs e);

    /// <summary>
    /// Notifications about changes to the user's system-wide preferences — the cross-platform counterpart
    /// of <c>Microsoft.Win32.SystemEvents</c>, which ships in the Windows-only
    /// <c>System.Drawing.Common</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="UserPreferenceChanged"/> is raised for real: it is driven by
    /// <see cref="Theme.ThemeChanged"/>, so a themed control that rebuilds its palette when the desktop
    /// switches between light and dark — the reason a control library subscribes to this at all — keeps
    /// working. The category reported for that is <see cref="UserPreferenceCategory.Color"/>, matching what
    /// Windows sends for a colour-scheme change; <see cref="Raise"/> is available for a backend that can
    /// detect a change in another category.
    /// </remarks>
    public static class SystemEvents
    {
        private static bool subscribed;

        /// <summary>Raised when a user preference, such as the colour scheme, changes.</summary>
        public static event UserPreferenceChangedEventHandler? UserPreferenceChanged {
            add {
                Subscribe ();
                Handlers += value;
            }
            remove => Handlers -= value;
        }

        private static UserPreferenceChangedEventHandler? Handlers;

        // Attaching to the theme is deferred to the first subscriber so that merely referencing this class
        // does not start listening -- and so a process that never subscribes holds no theme handler.
        private static void Subscribe ()
        {
            if (subscribed)
                return;

            subscribed = true;
            Theme.ThemeChanged += (_, _) => Raise (UserPreferenceCategory.Color);
        }

        /// <summary>Raises <see cref="UserPreferenceChanged"/> for the given category.</summary>
        public static void Raise (UserPreferenceCategory category) =>
            Handlers?.Invoke (null!, new UserPreferenceChangedEventArgs (category));
    }
}
