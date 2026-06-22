using Avalonia.Media;
using Avalonia.Styling;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// One-way bridge that makes the (global) Majorsilence.Forms <see cref="Theme"/> follow the host
    /// Avalonia application's theme: light/dark variant plus the system accent colour. Call
    /// <see cref="FollowHost"/> once (the embedding presenter does this automatically); thereafter the
    /// Majorsilence theme re-syncs whenever the host's theme variant changes.
    ///
    /// Because Majorsilence's Theme is process-global, this affects every Majorsilence surface in the process.
    /// </summary>
    public static class MajorsilenceFormsTheme
    {
        private static bool _subscribed;

        /// <summary>
        /// Syncs the Majorsilence.Forms theme to the current host Avalonia application theme and keeps it in
        /// sync with future host theme-variant changes. Safe to call more than once.
        /// </summary>
        public static void FollowHost ()
        {
            var app = Avalonia.Application.Current;
            if (app is null)
                return;

            Apply (app);

            if (!_subscribed) {
                app.ActualThemeVariantChanged += (_, _) => Apply (Avalonia.Application.Current!);
                _subscribed = true;
            }
        }

        private static void Apply (Avalonia.Application app)
        {
            var variant = app.ActualThemeVariant;

            // Batch the full palette swap + accent override into a single ThemeChanged repaint.
            Theme.BeginUpdate ();
            try {
                Theme.SetBuiltInTheme (variant == ThemeVariant.Dark ? BuiltInTheme.Dark : BuiltInTheme.Light);

                if (TryGetAccent (app, variant, out var accent))
                    Theme.AccentColor = accent;
            } finally {
                Theme.EndUpdate ();
            }
        }

        private static bool TryGetAccent (Avalonia.Application app, ThemeVariant variant, out SKColor accent)
        {
            // FluentTheme exposes the OS accent as the "SystemAccentColor" resource.
            if (app.TryGetResource ("SystemAccentColor", variant, out var value) && value is Color c) {
                accent = new SKColor (c.R, c.G, c.B, c.A);
                return true;
            }

            accent = default;
            return false;
        }
    }
}
