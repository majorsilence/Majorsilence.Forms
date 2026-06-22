using Microsoft.UI.Xaml;
using SkiaSharp;

namespace Majorsilence.Forms.Uno
{
    /// <summary>
    /// One-way bridge that makes the (global) Majorsilence.Forms <see cref="Theme"/> follow the host Uno
    /// application's theme: light/dark (the element's <see cref="FrameworkElement.ActualTheme"/>) plus the
    /// system accent colour. The embedding presenter calls <see cref="FollowHost"/>; thereafter the
    /// Majorsilence theme re-syncs whenever the host element's theme changes.
    ///
    /// Because Majorsilence's Theme is process-global, this affects every Majorsilence surface in the process.
    /// </summary>
    public static class MajorsilenceFormsTheme
    {
        private static FrameworkElement? _subscribed;
        private static ElementTheme _lastApplied = (ElementTheme) (-1);

        /// <summary>
        /// Syncs the Majorsilence.Forms theme to the host element's current theme and keeps it in sync with
        /// future theme changes. Safe to call repeatedly (e.g. each paint); only re-applies on change.
        /// </summary>
        public static void FollowHost (FrameworkElement element)
        {
            if (element is null)
                return;

            Apply (element);

            if (!ReferenceEquals (_subscribed, element)) {
                _subscribed = element;
                element.ActualThemeChanged += (s, _) => Apply (s);
            }
        }

        private static void Apply (FrameworkElement element)
        {
            var theme = element.ActualTheme;
            if (theme == _lastApplied)
                return;

            _lastApplied = theme;

            Theme.BeginUpdate ();
            try {
                Theme.SetBuiltInTheme (theme == ElementTheme.Dark ? BuiltInTheme.Dark : BuiltInTheme.Light);

                if (TryGetAccent (out var accent))
                    Theme.AccentColor = accent;
            } finally {
                Theme.EndUpdate ();
            }
        }

        private static bool TryGetAccent (out SKColor accent)
        {
            try {
                if (Microsoft.UI.Xaml.Application.Current?.Resources is { } res
                    && res.TryGetValue ("SystemAccentColor", out var value)
                    && value is Windows.UI.Color c) {
                    accent = new SKColor (c.R, c.G, c.B, c.A);
                    return true;
                }
            } catch { }

            accent = default;
            return false;
        }
    }
}
