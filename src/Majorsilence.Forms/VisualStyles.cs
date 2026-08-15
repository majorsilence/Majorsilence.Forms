using System.Drawing;
using Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.VisualStyles
{
    // System.Windows.Forms.VisualStyles is the WinForms wrapper over the Windows theming API (uxtheme).
    // A ported control library reaches for it to draw the pieces the framework does not expose as
    // controls -- a scrollbar's arrows, track and thumb; a check or radio glyph rendered inside a
    // custom-painted item -- and until now there was nothing here to map it to, so any such library
    // simply did not compile.
    //
    // These are real renderers, not stubs: they paint through the same Theme the rest of this layer
    // uses, so a control drawn with them matches everything around it on every platform. What they do
    // NOT do is reproduce a specific Windows visual style, which is the one thing uxtheme was for --
    // there is no OS theme to ask on macOS or Linux. Anything genuinely tied to a Windows theme handle
    // (VisualStyleRenderer's HTHEME, part/state ids) stays out of scope; uxtheme is a documented
    // non-goal in the same way the Win32 message pump is.

    /// <summary>The state of a scroll bar track or thumb.</summary>
    public enum ScrollBarState
    {
        /// <summary>Normal.</summary>
        Normal = 1,
        /// <summary>The pointer is over it.</summary>
        Hot = 2,
        /// <summary>Being pressed.</summary>
        Pressed = 3,
        /// <summary>Disabled.</summary>
        Disabled = 4,
    }

    /// <summary>The state of one of a scroll bar's arrow buttons.</summary>
    public enum ScrollBarArrowButtonState
    {
        /// <summary>Up arrow, normal.</summary>
        UpNormal = 1,
        /// <summary>Up arrow, pointer over.</summary>
        UpHot = 2,
        /// <summary>Up arrow, pressed.</summary>
        UpPressed = 3,
        /// <summary>Up arrow, disabled.</summary>
        UpDisabled = 4,
        /// <summary>Down arrow, normal.</summary>
        DownNormal = 5,
        /// <summary>Down arrow, pointer over.</summary>
        DownHot = 6,
        /// <summary>Down arrow, pressed.</summary>
        DownPressed = 7,
        /// <summary>Down arrow, disabled.</summary>
        DownDisabled = 8,
        /// <summary>Left arrow, normal.</summary>
        LeftNormal = 9,
        /// <summary>Left arrow, pointer over.</summary>
        LeftHot = 10,
        /// <summary>Left arrow, pressed.</summary>
        LeftPressed = 11,
        /// <summary>Left arrow, disabled.</summary>
        LeftDisabled = 12,
        /// <summary>Right arrow, normal.</summary>
        RightNormal = 13,
        /// <summary>Right arrow, pointer over.</summary>
        RightHot = 14,
        /// <summary>Right arrow, pressed.</summary>
        RightPressed = 15,
        /// <summary>Right arrow, disabled.</summary>
        RightDisabled = 16,
    }

    /// <summary>The state of a check box glyph.</summary>
    public enum CheckBoxState
    {
        /// <summary>Unchecked, normal.</summary>
        UncheckedNormal = 1,
        /// <summary>Unchecked, pointer over.</summary>
        UncheckedHot = 2,
        /// <summary>Unchecked, pressed.</summary>
        UncheckedPressed = 3,
        /// <summary>Unchecked, disabled.</summary>
        UncheckedDisabled = 4,
        /// <summary>Checked, normal.</summary>
        CheckedNormal = 5,
        /// <summary>Checked, pointer over.</summary>
        CheckedHot = 6,
        /// <summary>Checked, pressed.</summary>
        CheckedPressed = 7,
        /// <summary>Checked, disabled.</summary>
        CheckedDisabled = 8,
        /// <summary>Indeterminate, normal.</summary>
        MixedNormal = 9,
        /// <summary>Indeterminate, pointer over.</summary>
        MixedHot = 10,
        /// <summary>Indeterminate, pressed.</summary>
        MixedPressed = 11,
        /// <summary>Indeterminate, disabled.</summary>
        MixedDisabled = 12,
    }

    /// <summary>The state of a radio button glyph.</summary>
    public enum RadioButtonState
    {
        /// <summary>Unchecked, normal.</summary>
        UncheckedNormal = 1,
        /// <summary>Unchecked, pointer over.</summary>
        UncheckedHot = 2,
        /// <summary>Unchecked, pressed.</summary>
        UncheckedPressed = 3,
        /// <summary>Unchecked, disabled.</summary>
        UncheckedDisabled = 4,
        /// <summary>Checked, normal.</summary>
        CheckedNormal = 5,
        /// <summary>Checked, pointer over.</summary>
        CheckedHot = 6,
        /// <summary>Checked, pressed.</summary>
        CheckedPressed = 7,
        /// <summary>Checked, disabled.</summary>
        CheckedDisabled = 8,
    }

    // Shared palette. Drawing through Theme is what keeps these consistent with every control this
    // library paints itself, and is what makes them work identically on all three platforms.
    internal static class StyleColors
    {
        public static Color Surface (bool hot, bool pressed) =>
            pressed ? Theme.ControlLowColor.ToDrawingColor ()
            : hot ? Theme.ControlHighColor.ToDrawingColor ()
            : Theme.ControlMidColor.ToDrawingColor ();

        public static Color Border => Theme.BorderLowColor.ToDrawingColor ();
        public static Color Glyph => Theme.ForegroundColor.ToDrawingColor ();
        public static Color GlyphDisabled => Theme.ForegroundDisabledColor.ToDrawingColor ();
        public static Color Field => Theme.ControlLowColor.ToDrawingColor ();
        public static Color Accent => Theme.AccentColor.ToDrawingColor ();
    }

    /// <summary>Draws the pieces of a scroll bar.</summary>
    public static class ScrollBarRenderer
    {
        /// <summary>
        /// Gets whether these renderers can draw. Always true: they paint through the theme rather than
        /// an OS visual style, so unlike upstream there is no "visual styles are disabled" case.
        /// </summary>
        public static bool IsSupported => true;

        /// <summary>Draws an arrow button in the given state.</summary>
        public static void DrawArrowButton (Graphics g, Rectangle bounds, ScrollBarArrowButtonState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            var pressed = state is ScrollBarArrowButtonState.UpPressed or ScrollBarArrowButtonState.DownPressed
                or ScrollBarArrowButtonState.LeftPressed or ScrollBarArrowButtonState.RightPressed;
            var hot = state is ScrollBarArrowButtonState.UpHot or ScrollBarArrowButtonState.DownHot
                or ScrollBarArrowButtonState.LeftHot or ScrollBarArrowButtonState.RightHot;
            var disabled = state is ScrollBarArrowButtonState.UpDisabled or ScrollBarArrowButtonState.DownDisabled
                or ScrollBarArrowButtonState.LeftDisabled or ScrollBarArrowButtonState.RightDisabled;

            using (var back = new SolidBrush (StyleColors.Surface (hot, pressed)))
                g.FillRectangle (back, bounds);

            var direction = state switch {
                <= ScrollBarArrowButtonState.UpDisabled => ArrowDirection.Up,
                <= ScrollBarArrowButtonState.DownDisabled => ArrowDirection.Down,
                <= ScrollBarArrowButtonState.LeftDisabled => ArrowDirection.Left,
                _ => ArrowDirection.Right,
            };

            DrawArrowGlyph (g, bounds, direction, disabled ? StyleColors.GlyphDisabled : StyleColors.Glyph);
        }

        /// <summary>Draws the track above the thumb of a vertical scroll bar.</summary>
        public static void DrawUpperVerticalTrack (Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack (g, bounds);

        /// <summary>Draws the track below the thumb of a vertical scroll bar.</summary>
        public static void DrawLowerVerticalTrack (Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack (g, bounds);

        /// <summary>Draws the track left of the thumb of a horizontal scroll bar.</summary>
        public static void DrawLeftHorizontalTrack (Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack (g, bounds);

        /// <summary>Draws the track right of the thumb of a horizontal scroll bar.</summary>
        public static void DrawRightHorizontalTrack (Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack (g, bounds);

        /// <summary>Draws the thumb of a vertical scroll bar.</summary>
        public static void DrawVerticalThumb (Graphics g, Rectangle bounds, ScrollBarState state) => DrawThumb (g, bounds, state);

        /// <summary>Draws the thumb of a horizontal scroll bar.</summary>
        public static void DrawHorizontalThumb (Graphics g, Rectangle bounds, ScrollBarState state) => DrawThumb (g, bounds, state);

        /// <summary>Draws the grip lines on a vertical scroll bar's thumb.</summary>
        public static void DrawVerticalThumbGrip (Graphics g, Rectangle bounds, ScrollBarState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            var midY = bounds.Y + (bounds.Height / 2);
            var x1 = bounds.X + 3;
            var x2 = bounds.Right - 3;

            using var pen = new Pen (StyleColors.Border);
            for (var offset = -3; offset <= 3; offset += 3)
                g.DrawLine (pen, x1, midY + offset, x2, midY + offset);
        }

        /// <summary>Draws the grip lines on a horizontal scroll bar's thumb.</summary>
        public static void DrawHorizontalThumbGrip (Graphics g, Rectangle bounds, ScrollBarState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            var midX = bounds.X + (bounds.Width / 2);
            var y1 = bounds.Y + 3;
            var y2 = bounds.Bottom - 3;

            using var pen = new Pen (StyleColors.Border);
            for (var offset = -3; offset <= 3; offset += 3)
                g.DrawLine (pen, midX + offset, y1, midX + offset, y2);
        }

        /// <summary>Gets the size of the box where the two scroll bars meet.</summary>
        public static Size GetSizeBoxSize (Graphics g, ScrollBarState state) => new Size (16, 16);

        /// <summary>Gets the size of a scroll bar's arrow button.</summary>
        public static Size GetSizeBoxSize (Graphics g, ScrollBarArrowButtonState state) => new Size (16, 16);

        private static void DrawTrack (Graphics g, Rectangle bounds)
        {
            ArgumentNullException.ThrowIfNull (g);

            using var back = new SolidBrush (StyleColors.Field);
            g.FillRectangle (back, bounds);
        }

        private static void DrawThumb (Graphics g, Rectangle bounds, ScrollBarState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            using (var back = new SolidBrush (StyleColors.Surface (state == ScrollBarState.Hot, state == ScrollBarState.Pressed)))
                g.FillRectangle (back, bounds);

            using var pen = new Pen (StyleColors.Border);
            g.DrawRectangle (pen, bounds);
        }

        private enum ArrowDirection { Up, Down, Left, Right }

        private static void DrawArrowGlyph (Graphics g, Rectangle bounds, ArrowDirection direction, Color color)
        {
            // A small solid triangle, built from shrinking horizontal (or vertical) runs -- the fill
            // primitives are enough for a glyph this size, and it scales with the button.
            var size = Math.Max (3, Math.Min (bounds.Width, bounds.Height) / 3);
            var cx = bounds.X + (bounds.Width / 2);
            var cy = bounds.Y + (bounds.Height / 2);

            using var brush = new SolidBrush (color);

            for (var i = 0; i < size; i++) {
                switch (direction) {
                    case ArrowDirection.Up:
                        g.FillRectangle (brush, cx - i, cy + (size / 2) - i, (i * 2) + 1, 1);
                        break;
                    case ArrowDirection.Down:
                        g.FillRectangle (brush, cx - i, cy - (size / 2) + i, (i * 2) + 1, 1);
                        break;
                    case ArrowDirection.Left:
                        g.FillRectangle (brush, cx + (size / 2) - i, cy - i, 1, (i * 2) + 1);
                        break;
                    default:
                        g.FillRectangle (brush, cx - (size / 2) + i, cy - i, 1, (i * 2) + 1);
                        break;
                }
            }
        }
    }

    /// <summary>The state a push button is drawn in.</summary>
    /// <remarks>Values match <c>System.Windows.Forms.VisualStyles.PushButtonState</c>, which is
    /// 1-based -- code that persists one as an int must round-trip to the same button.</remarks>
    public enum PushButtonState
    {
        /// <summary>The normal, unpressed state.</summary>
        Normal = 1,
        /// <summary>The pointer is over the button.</summary>
        Hot = 2,
        /// <summary>The button is pressed.</summary>
        Pressed = 3,
        /// <summary>The button cannot be clicked.</summary>
        Disabled = 4,
        /// <summary>The button is the form's default.</summary>
        Default = 5,
    }

    /// <summary>Draws a check box glyph.</summary>
    public static class CheckBoxRenderer
    {
        /// <inheritdoc cref="ScrollBarRenderer.IsSupported"/>
        public static bool IsSupported => true;

        /// <summary>Gets the size of the check glyph.</summary>
        public static Size GetGlyphSize (Graphics g, CheckBoxState state) => new Size (13, 13);

        /// <summary>Draws the check glyph at the given location.</summary>
        public static void DrawCheckBox (Graphics g, Point glyphLocation, CheckBoxState state)
            => DrawCheckBox (g, glyphLocation, Rectangle.Empty, string.Empty, null, false, state);

        /// <summary>Draws the check glyph, with optional text beside it.</summary>
        public static void DrawCheckBox (Graphics g, Point glyphLocation, Rectangle textBounds, string? text,
            Majorsilence.Forms.Drawing.Font? font, bool focused, CheckBoxState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            var size = GetGlyphSize (g, state);
            var box = new Rectangle (glyphLocation, size);
            var disabled = state is CheckBoxState.UncheckedDisabled or CheckBoxState.CheckedDisabled or CheckBoxState.MixedDisabled;
            var isChecked = state is CheckBoxState.CheckedNormal or CheckBoxState.CheckedHot
                or CheckBoxState.CheckedPressed or CheckBoxState.CheckedDisabled;

            using (var back = new SolidBrush (StyleColors.Field))
                g.FillRectangle (back, box);

            using (var pen = new Pen (StyleColors.Border))
                g.DrawRectangle (pen, box);

            if (isChecked) {
                // A tick, drawn as two strokes.
                using var check = new Pen (disabled ? StyleColors.GlyphDisabled : StyleColors.Glyph, 2);
                g.DrawLine (check, box.X + 3, box.Y + 6, box.X + 5, box.Y + 9);
                g.DrawLine (check, box.X + 5, box.Y + 9, box.X + 10, box.Y + 3);
            }

            if (!string.IsNullOrEmpty (text) && !textBounds.IsEmpty)
                TextRenderer.DrawText (g, text, font ?? SystemFonts.DefaultFont, textBounds, disabled ? StyleColors.GlyphDisabled : StyleColors.Glyph);
        }
    }

    /// <summary>Draws a radio button glyph.</summary>
    public static class RadioButtonRenderer
    {
        /// <inheritdoc cref="ScrollBarRenderer.IsSupported"/>
        public static bool IsSupported => true;

        /// <summary>Gets the size of the radio glyph.</summary>
        public static Size GetGlyphSize (Graphics g, RadioButtonState state) => new Size (13, 13);

        /// <summary>Draws the radio glyph at the given location.</summary>
        public static void DrawRadioButton (Graphics g, Point glyphLocation, RadioButtonState state)
            => DrawRadioButton (g, glyphLocation, Rectangle.Empty, string.Empty, null, false, state);

        /// <summary>Draws the radio glyph, with optional text beside it.</summary>
        public static void DrawRadioButton (Graphics g, Point glyphLocation, Rectangle textBounds, string? text,
            Majorsilence.Forms.Drawing.Font? font, bool focused, RadioButtonState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            var size = GetGlyphSize (g, state);
            var box = new Rectangle (glyphLocation, size);
            var disabled = state is RadioButtonState.UncheckedDisabled or RadioButtonState.CheckedDisabled;
            var isChecked = state is RadioButtonState.CheckedNormal or RadioButtonState.CheckedHot
                or RadioButtonState.CheckedPressed or RadioButtonState.CheckedDisabled;

            using (var back = new SolidBrush (StyleColors.Field))
                g.FillEllipse (back, box.X, box.Y, box.Width, box.Height);

            using (var pen = new Pen (StyleColors.Border))
                g.DrawEllipse (pen, box.X, box.Y, box.Width, box.Height);

            if (isChecked) {
                using var dot = new SolidBrush (disabled ? StyleColors.GlyphDisabled : StyleColors.Accent);
                g.FillEllipse (dot, box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8);
            }

            if (!string.IsNullOrEmpty (text) && !textBounds.IsEmpty)
                TextRenderer.DrawText (g, text, font ?? SystemFonts.DefaultFont, textBounds, disabled ? StyleColors.GlyphDisabled : StyleColors.Glyph);
        }
    }
}
