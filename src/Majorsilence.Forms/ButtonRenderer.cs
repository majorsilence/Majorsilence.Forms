using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.VisualStyles;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Draws a button in the current visual style — the stand-in for
    /// <c>System.Windows.Forms.ButtonRenderer</c>.
    /// </summary>
    /// <remarks>
    /// A control library that draws its own button faces calls this rather than hosting a real
    /// <see cref="Button"/>. There is no msstyles engine here (see
    /// <see cref="VisualStyles.VisualStyleInformation"/>), so this draws with the theme's own colours —
    /// which is exactly what WinForms does when visual styles are off, and gives a button that matches
    /// the rest of the UI rather than nothing at all.
    ///
    /// In the root namespace, as WinForms has it. Note the sibling renderers this library already had
    /// (<c>CheckBoxRenderer</c>, <c>ComboBoxRenderer</c>, <c>ProgressBarRenderer</c>, …) live under
    /// <c>Majorsilence.Forms.VisualStyles</c> instead, which is a divergence: upstream they are all in
    /// <c>System.Windows.Forms</c>, so migrated code that reaches for one under a plain
    /// <c>using Majorsilence.Forms;</c> will not find it. Moving them is a public-API change and is
    /// tracked separately rather than folded in here.
    /// </remarks>
    public static class ButtonRenderer
    {
        /// <summary>Gets or sets whether the renderer follows the application's visual-style setting.</summary>
        /// <remarks>Stored only: with no msstyles engine there is no second rendering path to switch to.</remarks>
        public static bool RenderMatchingApplicationState { get; set; } = true;

        /// <summary>Draws a button face in the given state.</summary>
        public static void DrawButton (Graphics g, Rectangle bounds, PushButtonState state)
            => DrawButton (g, bounds, string.Empty, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                           false, state);

        /// <inheritdoc cref="DrawButton(Graphics, Rectangle, PushButtonState)"/>
        public static void DrawButton (Graphics g, Rectangle bounds, bool focused, PushButtonState state)
            => DrawButton (g, bounds, string.Empty, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                           focused, state);

        /// <inheritdoc cref="DrawButton(Graphics, Rectangle, PushButtonState)"/>
        public static void DrawButton (Graphics g, Rectangle bounds, string? buttonText,
            Majorsilence.Forms.Drawing.Font? font, bool focused, PushButtonState state)
            => DrawButton (g, bounds, buttonText, font,
                           TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter, focused, state);

        /// <inheritdoc cref="DrawButton(Graphics, Rectangle, PushButtonState)"/>
        public static void DrawButton (Graphics g, Rectangle bounds, string? buttonText,
            Majorsilence.Forms.Drawing.Font? font, TextFormatFlags flags, bool focused, PushButtonState state)
        {
            Guard.ThrowIfNull (g);

            var pressed = state == PushButtonState.Pressed;
            var hot = state == PushButtonState.Hot;
            var disabled = state == PushButtonState.Disabled;

            using (var face = new SolidBrush (StyleColors.Surface (hot, pressed)))
                g.FillRectangle (face, bounds);

            using (var border = new Pen (StyleColors.Border))
                g.DrawRectangle (border, new Rectangle (bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1));

            // The default button carries a heavier border upstream; the state is the only signal for it.
            if (state == PushButtonState.Default) {
                using var accent = new Pen (StyleColors.Border, 2);
                g.DrawRectangle (accent, new Rectangle (bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3));
            }

            if (!string.IsNullOrEmpty (buttonText))
                TextRenderer.DrawText (g, buttonText, font ?? SystemFonts.DefaultFont, bounds,
                    disabled ? StyleColors.GlyphDisabled : StyleColors.Glyph, flags);

            if (focused)
                g.DrawFocusRectangle (Rectangle.Inflate (bounds, -3, -3));
        }

        /// <summary>Paints the background of <paramref name="childControl"/>'s parent behind it.</summary>
        /// <remarks>
        /// How a control with a transparent background gets something under it. Paints the nearest
        /// opaque ancestor's back colour, the same approach as
        /// <c>Control.PaintTransparentBackground</c>: re-running the parent's whole paint is neither
        /// needed nor safe here, because the parent has already painted beneath this control in the
        /// current frame.
        /// </remarks>
        public static void DrawParentBackground (Graphics g, Rectangle bounds, Control childControl)
        {
            Guard.ThrowIfNull (g);

            var color = childControl?.BackColor ?? SystemColors.Control;

            for (var ancestor = childControl?.Parent; ancestor is not null; ancestor = ancestor.Parent) {
                if (ancestor.BackColor.A != 0) {
                    color = ancestor.BackColor;
                    break;
                }
            }

            using var brush = new SolidBrush (color);
            g.FillRectangle (brush, bounds);
        }

        /// <summary>Returns whether the point is inside the button's drawn face.</summary>
        public static bool IsBackgroundPartiallyTransparent (PushButtonState state) => false;
    }
}
