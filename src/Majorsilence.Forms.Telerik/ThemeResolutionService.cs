using System;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik's ThemeResolutionService: the theme every Telerik control paints with. As in Telerik,
    /// <see cref="ApplicationThemeName"/>, when set, wins over each control's own <c>ThemeName</c>, and a control's
    /// own name applies only while it is empty.
    /// </summary>
    /// <remarks>
    /// The themes painted are <c>"Fluent"</c> and <c>"FluentDark"</c>, and only by <see cref="RadGridView"/> so far.
    /// Any other name (or none) leaves a control in this library's theme colours, as before.
    /// </remarks>
    public static class ThemeResolutionService
    {
        private static string application_theme_name = string.Empty;
        private static Color application_accent_color = Color.Empty;

        /// <summary>
        /// Gets or sets the theme every Telerik control paints with, whatever its own <c>ThemeName</c> says.
        /// Empty (the default) leaves each control to its own <c>ThemeName</c>.
        /// </summary>
        public static string ApplicationThemeName {
            get => application_theme_name;
            set {
                value ??= string.Empty;
                if (value == application_theme_name)
                    return;

                application_theme_name = value;
                OnApplicationThemeChanged ();
            }
        }

        /// <summary>
        /// Gets or sets the colour a theme's accent takes. <see cref="Color.Empty"/> (the default) keeps the theme's
        /// own accent (Fluent's cyan-teal).
        /// </summary>
        /// <remarks>
        /// Not in Telerik, whose Fluent theme has no accent setting. An app recolours it by moving the hue of each of
        /// the theme's accent colours (hue 160 to 201) to this colour's hue, keeping their saturation and lightness,
        /// and so does this: Fluent's <c>#0099BC</c> becomes <c>#0073BC</c> for an accent of <c>#00578E</c>. An accent
        /// whose own hue lies in that range keeps the theme's colours.
        /// </remarks>
        public static Color ApplicationAccentColor {
            get => application_accent_color;
            set {
                if (value == application_accent_color)
                    return;

                application_accent_color = value;
                OnApplicationThemeChanged ();
            }
        }

        /// <summary>Raised when <see cref="ApplicationThemeName"/> or <see cref="ApplicationAccentColor"/> changes.</summary>
        public static event EventHandler? ApplicationThemeChanged;

        /// <summary>The theme a control whose own theme name is <paramref name="controlThemeName"/> paints with.</summary>
        internal static string ResolveThemeName (string? controlThemeName)
            => string.IsNullOrEmpty (application_theme_name) ? controlThemeName ?? string.Empty : application_theme_name;

        private static void OnApplicationThemeChanged ()
        {
            ApplicationThemeChanged?.Invoke (null, EventArgs.Empty);

            // Telerik restyles every open control at once.
            foreach (var form in Application.OpenForms.ToArray ())
                form.Invalidate (true);
        }
    }
}
