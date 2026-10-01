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
        /// <remarks>
        /// A CSS theme in effect (<see cref="Theme.CurrentStyleSheets"/>) is kept: on a variant change the
        /// matching built-in theme is put under it and the sheet re-applied, so the sheet's own tokens win
        /// and what it leaves unset follows the host. A sheet that <c>extends Light</c> or <c>Dark</c>
        /// re-applies that base, which pins its variant.
        /// </remarks>
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

        // Internal so a test can drive one variant change without the process-wide subscription.
        internal static void Apply (Avalonia.Application app)
        {
            var variant = app.ActualThemeVariant;

            // A CSS theme in effect wins over following the host (#104). SetBuiltInTheme clears the
            // stylesheet's rules and tokens, and applying a sheet through AvaloniaCssTheme itself sets
            // RequestedThemeVariant -- the very change that brings us here -- so following the host
            // naively undid every CSS theme the moment it was applied. Now the built-in base is swapped
            // under the sheet and the sheet re-applied on top: its own tokens (an --accent-color
            // included) win over the host's, what it leaves unset follows the host variant, and a sheet
            // that extends Light or Dark re-applies that base too, which pins its variant.
            var sheets = Theme.CurrentStyleSheets;

            // Batch the full palette swap + accent override (+ sheet) into a single ThemeChanged repaint.
            Theme.BeginUpdate ();
            try {
                Theme.SetBuiltInTheme (variant == ThemeVariant.Dark ? BuiltInTheme.Dark : BuiltInTheme.Light);

                if (TryGetAccent (app, variant, out var accent))
                    Theme.AccentColor = accent;

                if (sheets.Count > 0)
                    Theme.ApplyStyleSheet (sheets[sheets.Count - 1]);
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
