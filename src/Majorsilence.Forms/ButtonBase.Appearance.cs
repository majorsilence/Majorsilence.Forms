using System;

namespace Majorsilence.Forms
{
    // SMP-03/SMP-04 and SMP-05: the two properties that decide what a check box or radio button LOOKS
    // like, neither of which anything read.
    //
    // Appearance was a bare auto-property on both controls, explicitly marked a stub, and
    // `grep -rn 'Appearance\.Button' src/` found no reader at all -- so the segmented-control idiom
    // (a row of Appearance = Button radio buttons acting as a toolbar) came out as ordinary radio
    // circles with no pressed state. AppearanceChanged (SMP-04) could not fire either, because an
    // auto-property has nowhere to raise it from.
    //
    // FlatStyle/FlatAppearance were overridden on both controls purely to store: only Button folded
    // them into its style chain. A flat check box kept its themed 3-D frame and the designer's
    // FlatAppearance lines did nothing.
    //
    // ApplyFlatAppearance used to be private to Button; it lives here now so all three controls share
    // one definition, which is what the finding asks for.
    public abstract partial class ButtonBase
    {
        // Upstream draws the default button's frame a second pixel thick; see Button.IsDefault.
        private const int FlatBorderFallback = 1;

        /// <summary>
        /// Whether this control is currently in its "on" state -- checked, for the controls that have
        /// a check state. Drives <see cref="FlatButtonAppearance.CheckedBackColor"/> and the latched
        /// look of <see cref="Majorsilence.Forms.Appearance.Button"/>.
        /// </summary>
        internal virtual bool IsLatched => false;

        /// <summary>
        /// How this control draws itself: as its own glyph plus a caption, or as a toggle button.
        /// <see cref="Majorsilence.Forms.Appearance.Normal"/> for everything but
        /// <see cref="CheckBox"/> and <see cref="RadioButton"/>, which override it.
        /// </summary>
        internal virtual Appearance AppearanceCore => Appearance.Normal;

        /// <summary>
        /// Folds <see cref="FlatStyle"/>, <see cref="FlatAppearance"/> and
        /// <see cref="Majorsilence.Forms.Appearance"/> into the instance style, immediately before the
        /// style is handed to the renderer.
        /// </summary>
        /// <remarks>
        /// It cannot be done when the properties are set: <see cref="FlatAppearance"/> is a mutable
        /// object, so <c>control.FlatAppearance.BorderSize = 0</c> -- the line the designer emits --
        /// raises no notification at all. Resolving here means switching back to
        /// <see cref="FlatStyle.Standard"/> restores the themed border by clearing the override rather
        /// than by guessing what it used to be.
        /// </remarks>
        protected void ApplyFlatAppearance ()
        {
            var appearance_button = AppearanceCore == Appearance.Button;

            // Popup is flat until the pointer is over it, when it raises a Standard border.
            var is_flat = FlatStyle == FlatStyle.Flat
                       || (FlatStyle == FlatStyle.Popup && !(IsHovering && Enabled));

            if (!is_flat) {
                // Null lets the width fall back through the style chain to the themed default. A
                // glyph-less toggle button has no frame of its own to fall back to, so it borrows the
                // button one -- without it, Appearance.Button would be an invisible control.
                var standard = appearance_button ? FlatBorderFallback : (int?)null;

                Style.Border.Width = DefaultBorderWidth ?? standard;
                StyleHover.Border.Width = DefaultBorderWidth ?? standard;
            } else {
                Style.Border.Width = FlatAppearance.BorderSize;
                StyleHover.Border.Width = FlatAppearance.BorderSize;

                if (FlatAppearance.BorderColor != System.Drawing.Color.Empty) {
                    Style.Border.Color = FlatAppearance.BorderColor.ToSKColor ();
                    StyleHover.Border.Color = FlatAppearance.BorderColor.ToSKColor ();
                }

                if (FlatAppearance.MouseOverBackColor != System.Drawing.Color.Empty)
                    StyleHover.BackgroundColor = FlatAppearance.MouseOverBackColor.ToSKColor ();
            }

            ApplyLatchedBackground (appearance_button);
        }

        // The "on" background, from two sources that agree about what they mean: the application's own
        // CheckedBackColor, and -- for a toggle button, which has no glyph to carry the state -- the
        // accent the rest of the framework already uses for a pressed control. Both are cleared again
        // when the control goes off, so the style chain decides rather than a remembered colour.
        private void ApplyLatchedBackground (bool appearanceButton)
        {
            if (!IsLatched) {
                Style.BackgroundColor = null;
                Style.ForegroundColor = null;
                return;
            }

            if (FlatAppearance.CheckedBackColor != System.Drawing.Color.Empty) {
                Style.BackgroundColor = FlatAppearance.CheckedBackColor.ToSKColor ();
                return;
            }

            if (!appearanceButton) {
                Style.BackgroundColor = null;
                Style.ForegroundColor = null;
                return;
            }

            Style.BackgroundColor = Theme.AccentColor;
            Style.ForegroundColor = Theme.ForegroundColorOnAccent;
        }

        // Button widens its border when it is the form's default (SMP-07); nothing else does.
        private protected virtual int? DefaultBorderWidth => null;
    }
}
