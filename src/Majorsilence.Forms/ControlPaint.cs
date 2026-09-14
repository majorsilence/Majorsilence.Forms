using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Contains methods for drawing elements of controls.
    /// </summary>
    public static partial class ControlPaint
    {
        /// <summary>
        /// Draws an arrow glyph, as seen on ComboBoxes and TreeView dropdowns.
        /// </summary>
        public static void DrawArrowGlyph (PaintEventArgs e, Rectangle rectangle, SKColor color, ArrowDirection direction)
        {
            var lines = e.LogicalToDeviceUnits (4);

            switch (direction) {
                case ArrowDirection.Left: {
                        var y = rectangle.Y + (rectangle.Height / 2);
                        var x = rectangle.X + (rectangle.Width / 2) - e.LogicalToDeviceUnits (2);

                        for (var i = 0; i < lines; i++)
                            e.Canvas.DrawLine (x + i, y - i, x + i, y + i + 1, color);

                        break;
                    }
                case ArrowDirection.Up: {
                        var y = rectangle.Y + (rectangle.Height / 2) - e.LogicalToDeviceUnits (2);
                        var x = rectangle.X + (rectangle.Width / 2);

                        for (var i = 0; i < lines; i++)
                            e.Canvas.DrawLine (x - i, y + i, x + i + 1, y + i, color);

                        break;
                    }
                case ArrowDirection.Right: {
                        var y = rectangle.Y + (rectangle.Height / 2);
                        var x = rectangle.X + (rectangle.Width / 2) - e.LogicalToDeviceUnits (1);

                        for (var i = 0; i < lines; i++)
                            e.Canvas.DrawLine (x + i, y - (lines - 1 - i), x + i, y + lines - i, color);

                        break;
                    }
                case ArrowDirection.Down: {
                        var y = rectangle.Y + (rectangle.Height / 2) - e.LogicalToDeviceUnits (1);
                        var x = rectangle.X + (rectangle.Width / 2);

                        for (var i = 0; i < lines; i++)
                            e.Canvas.DrawLine (x - (lines - 1 - i), y + i, x + lines - i, y + i, color);

                        break;
                    }
            }
        }

        /// <summary>
        /// Draws a CheckBox glyph.
        /// </summary>
        public static void DrawCheckBox (PaintEventArgs e, Rectangle rectangle, CheckState state, bool disabled = false)
        {
            var color = disabled ? Theme.ForegroundDisabledColor
                            : state == CheckState.Checked && !disabled ? Theme.AccentColor
                            : Theme.BorderLowColor;
            var unit_1 = e.LogicalToDeviceUnits (1);

            // Draw the border
            e.Canvas.DrawRectangle (rectangle, color, unit_1);

            // Draw the checked glyph if needed
            if (state == CheckState.Checked) {
                var unit_2 = e.LogicalToDeviceUnits (2);
                var unit_5 = e.LogicalToDeviceUnits (5);
                var fill_bounds = new Rectangle (rectangle.Left + 1 + unit_2, rectangle.Top + 1 + unit_2, rectangle.Width - unit_5, rectangle.Height - unit_5);

                e.Canvas.FillRectangle (fill_bounds, color);
            }

            // Draw the indeterminate glyph if needed
            if (state == CheckState.Indeterminate) {
                var unit_2 = e.LogicalToDeviceUnits (2);
                var unit_5 = e.LogicalToDeviceUnits (5);
                var center_y = rectangle.GetCenter ().Y;

                var fill_bounds = new Rectangle (rectangle.Left + 1 + unit_2, center_y, rectangle.Width - unit_5, unit_1 + unit_2);

                e.Canvas.FillRectangle (fill_bounds, color);
            }
        }

        /// <summary>
        /// Draws a close glyph, as seen on FormTitleBar.
        /// </summary>
        public static void DrawCloseGlyph (PaintEventArgs e, Rectangle rectangle)
        {
            e.Canvas.DrawLine (rectangle.X, rectangle.Y, rectangle.Right, rectangle.Bottom, Theme.ForegroundColorOnAccent);
            e.Canvas.DrawLine (rectangle.X, rectangle.Bottom, rectangle.Right, rectangle.Y, Theme.ForegroundColorOnAccent);
        }

        /// <summary>
        /// Draws a maximize glyph, as seen on FormTitleBar.
        /// </summary>
        public static void DrawMaximizeGlyph (PaintEventArgs e, Rectangle rectangle)
        {
            e.Canvas.DrawRectangle (rectangle, Theme.ForegroundColorOnAccent);
        }

        /// <summary>
        /// Draws a restore glyph, as seen on FormTitleBar when the window is maximized.
        /// </summary>
        public static void DrawRestoreGlyph (PaintEventArgs e, Rectangle rectangle)
        {
            var color = Theme.ForegroundColorOnAccent;
            var offset = e.LogicalToDeviceUnits (2);

            var back = new Rectangle (
                rectangle.X + offset,
                rectangle.Y,
                rectangle.Width - offset - 1,
                rectangle.Height - offset - 1);

            var front = new Rectangle (
                rectangle.X,
                rectangle.Y + offset,
                rectangle.Width - offset,
                rectangle.Height - offset);

            // Draw "front" "window"
            e.Canvas.DrawRectangle (front, color);

            // Draw "back" "window"
            using var path = new SKPath ();

            path.MoveTo (back.Left, front.Top);
            path.LineTo (back.Left, back.Top);
            path.LineTo (back.Right, back.Top);
            path.LineTo (back.Right, back.Bottom);
            path.LineTo (front.Right, back.Bottom);

            e.Canvas.DrawPath (path, color);
        }

        /// <summary>
        /// Draws a minimize glyph, as seen on FormTitleBar.
        /// </summary>
        public static void DrawMinimizeGlyph (PaintEventArgs e, Rectangle rectangle)
        {
            e.Canvas.DrawLine (rectangle.X, rectangle.Y, rectangle.Right, rectangle.Y, Theme.ForegroundColorOnAccent);
        }

        /// <summary>
        /// Draws a RadioButton glyph.
        /// </summary>
        public static void DrawRadioButton (PaintEventArgs e, Point origin, CheckState state, bool disabled = false)
        {
            // Sized for the 13px GDI-parity glyph box (see RadioButtonRenderer.GlyphSize).
            var outer_radius = e.LogicalToDeviceUnits (6);
            var inner_radius = e.LogicalToDeviceUnits (3);
            var border_color = disabled ? Theme.ForegroundDisabledColor :
                               state == CheckState.Checked ? Theme.AccentColor2 :
                               Theme.BorderLowColor;

            e.Canvas.DrawCircle (origin.X, origin.Y, outer_radius, border_color, e.LogicalToDeviceUnits (1));

            if (state == CheckState.Checked)
                e.Canvas.FillCircle (origin.X, origin.Y, inner_radius, disabled ? Theme.ForegroundDisabledColor : Theme.AccentColor2);
        }

        // --- WinForms compatibility overloads taking Majorsilence.Forms.Drawing.Graphics ---

        /// <summary>Draws a focus rectangle on the given graphics surface.</summary>
        public static void DrawFocusRectangle (Graphics graphics, Rectangle rectangle)
            => graphics.DrawFocusRectangle (rectangle);

        /// <summary>Draws a focus rectangle using the given foreground and background colours.</summary>
        public static void DrawFocusRectangle (Graphics graphics, Rectangle rectangle, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => graphics.DrawFocusRectangle (rectangle, foreColor, backColor);

        /// <inheritdoc cref="DrawBorder3D(Graphics,Rectangle)"/>
        public static void DrawBorder3D (Graphics graphics, int x, int y, int width, int height)
            => DrawBorder3D (graphics, new Rectangle (x, y, width, height));

        /// <inheritdoc cref="DrawBorder3D(Graphics,Rectangle,Border3DStyle)"/>
        public static void DrawBorder3D (Graphics graphics, int x, int y, int width, int height, Border3DStyle style)
            => DrawBorder3D (graphics, new Rectangle (x, y, width, height), style);

        /// <inheritdoc cref="DrawBorder3D(Graphics,Rectangle,Border3DStyle,Border3DSide)"/>
        public static void DrawBorder3D (Graphics graphics, int x, int y, int width, int height, Border3DStyle style, Border3DSide sides)
            => DrawBorder3D (graphics, new Rectangle (x, y, width, height), style, sides);

        /// <summary>Draws a button control using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawButton (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawButton (graphics, new Rectangle (x, y, width, height), state);

        /// <summary>Draws a check box using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawCheckBox (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawCheckBox (graphics, new Rectangle (x, y, width, height), state);

        /// <summary>Draws a combo box button using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawComboButton (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawComboButton (graphics, new Rectangle (x, y, width, height), state);

        /// <inheritdoc cref="DrawMenuGlyph(Graphics,Rectangle,MenuGlyph)"/>
        public static void DrawMenuGlyph (Graphics graphics, int x, int y, int width, int height, MenuGlyph glyph)
            => DrawMenuGlyph (graphics, new Rectangle (x, y, width, height), glyph);

        /// <inheritdoc cref="DrawMenuGlyph(Graphics,Rectangle,MenuGlyph,System.Drawing.Color,System.Drawing.Color)"/>
        public static void DrawMenuGlyph (Graphics graphics, int x, int y, int width, int height, MenuGlyph glyph,
            System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => DrawMenuGlyph (graphics, new Rectangle (x, y, width, height), glyph, foreColor, backColor);

        /// <summary>
        /// Draws a grid of single-pixel dots spaced <paramref name="cellSize"/> apart within
        /// <paramref name="area"/>, in a color that contrasts with <paramref name="backColor"/>
        /// (matches System.Windows.Forms.ControlPaint.DrawGrid, used for design-surface alignment
        /// grids). Unlike most other members of this class, this one is a real implementation, not
        /// a stub -- report/form designers rely on it for visible drag-and-drop alignment dots.
        /// </summary>
        public static void DrawGrid (Graphics graphics, Rectangle area, System.Drawing.Size cellSize, System.Drawing.Color backColor)
        {
            if (cellSize.Width <= 0 || cellSize.Height <= 0)
                return;

            // Simple luminance-based contrast pick, same idea as WinForms' internal dot color.
            int luminance = (backColor.R * 299 + backColor.G * 587 + backColor.B * 114) / 1000;
            var dotColor = luminance > 128 ? System.Drawing.Color.Black : System.Drawing.Color.White;
            using var dotBrush = new Majorsilence.Forms.Drawing.SolidBrush (dotColor);

            for (int y = area.Top; y < area.Bottom; y += cellSize.Height)
                for (int x = area.Left; x < area.Right; x += cellSize.Width)
                    graphics.FillRectangle (dotBrush, x, y, 1, 1);
        }

        /// <inheritdoc cref="DrawRadioButton(Graphics,Rectangle,ButtonState)"/>
        public static void DrawRadioButton (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawRadioButton (graphics, new Rectangle (x, y, width, height), state);

        /// <inheritdoc cref="DrawScrollButton(Graphics,Rectangle,ScrollButton,ButtonState)"/>
        public static void DrawScrollButton (Graphics graphics, int x, int y, int width, int height, ScrollButton button, ButtonState state)
            => DrawScrollButton (graphics, new Rectangle (x, y, width, height), button, state);

        /// <summary>Draws a size grip using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawSizeGrip (Graphics graphics, System.Drawing.Color backColor, int x, int y, int width, int height)
            => DrawSizeGrip (graphics, backColor, new Rectangle (x, y, width, height));

        /// <summary>Draws a string greyed out, the way a disabled control's text is drawn.</summary>
        /// <remarks>Unlike the StringFormat overload above this one draws: it lightens the colour
        /// towards the background and hands the text to TextRenderer, which is what the disabled
        /// state actually looks like.</remarks>
        public static void DrawStringDisabled (Majorsilence.Forms.Drawing.IDeviceContext dc, string s,
            Majorsilence.Forms.Drawing.Font font, System.Drawing.Color color, Rectangle layoutRectangle, TextFormatFlags format)
            => TextRenderer.DrawText (dc, s, font, layoutRectangle, LightenForDisabled (color), format);

        // Halfway to white is what GDI+ does for a grayed string, and it stays legible on the light
        // and dark themes alike because it moves towards the caller's colour rather than a constant.
        private static System.Drawing.Color LightenForDisabled (System.Drawing.Color color)
            => System.Drawing.Color.FromArgb (color.A, (color.R + 255) / 2, (color.G + 255) / 2, (color.B + 255) / 2);

#pragma warning disable CA1416
        /// <summary>Draws a string at the specified coordinates. Stub in Majorsilence.Forms.</summary>
        public static void DrawString (Graphics graphics, string s, Majorsilence.Forms.Drawing.Font font, System.Drawing.Color color, int x, int y)
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (color);
            graphics.DrawString (s, font, brush, x, y);
        }

        /// <summary>Draws a string within the specified rectangle. Stub in Majorsilence.Forms.</summary>
        public static void DrawString (Graphics graphics, string s, Majorsilence.Forms.Drawing.Font font, System.Drawing.Color color, Rectangle layoutRectangle)
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (color);
            graphics.DrawString (s, font, brush, new System.Drawing.RectangleF (layoutRectangle.X, layoutRectangle.Y, layoutRectangle.Width, layoutRectangle.Height));
        }

        /// <summary>Draws a string within the specified RectangleF with a StringFormat. Stub in Majorsilence.Forms.</summary>
        public static void DrawString (Graphics graphics, string s, Majorsilence.Forms.Drawing.Font font, System.Drawing.Color color, System.Drawing.RectangleF layoutRectangle, Majorsilence.Forms.Drawing.StringFormat format)
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (color);
            graphics.DrawString (s, font, brush, layoutRectangle);
        }
#pragma warning restore CA1416

        // GFX-02. Two independent bugs, both fixed by going through HLSColor:
        //
        //  * The parameter is a 0.0-1.0 FRACTION, not a 0-100 percentage. Existing WinForms code calls
        //    ControlPaint.Light (c, 0.5f) -- the documented form -- and got +1 per channel here, a
        //    visually identical colour, so every hand-rolled bevel collapsed to flat.
        //  * Linear RGB addition desaturates towards white or black. Moving luminosity in HLS keeps the
        //    hue: Dark (Color.Red) is (128,0,0), not (230,0,0).
        //
        // Separate single-argument overloads rather than a default parameter, because the single-argument
        // FORM has to exist for reflection and delegate binding -- a default parameter is not an overload.

        /// <summary>Returns a lighter shade of the given colour.</summary>
        public static System.Drawing.Color Light (System.Drawing.Color baseColor) => new HLSColor (baseColor).Lighter (0.5f);

        /// <summary>Returns a lighter shade of the given colour, by the given fraction (0.0-1.0).</summary>
        public static System.Drawing.Color Light (System.Drawing.Color baseColor, float percOfLightLight)
            => new HLSColor (baseColor).Lighter (percOfLightLight);

        /// <summary>Returns a darker shade of the given colour.</summary>
        public static System.Drawing.Color Dark (System.Drawing.Color baseColor) => new HLSColor (baseColor).Darker (0.5f);

        /// <summary>Returns a darker shade of the given colour, by the given fraction (0.0-1.0).</summary>
        public static System.Drawing.Color Dark (System.Drawing.Color baseColor, float percOfDarkDark)
            => new HLSColor (baseColor).Darker (percOfDarkDark);

        /// <summary>Returns a colour significantly lighter than the given base colour.</summary>
        public static System.Drawing.Color LightLight (System.Drawing.Color baseColor) => new HLSColor (baseColor).Lighter (1.0f);

        /// <summary>Returns a colour significantly darker than the given base colour.</summary>
        public static System.Drawing.Color DarkDark (System.Drawing.Color baseColor) => new HLSColor (baseColor).Darker (1.0f);
    }
}
