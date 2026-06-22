using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Contains methods for drawing elements of controls.
    /// </summary>
    public static class ControlPaint
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
            var outer_radius = e.LogicalToDeviceUnits (8);
            var inner_radius = e.LogicalToDeviceUnits (5);
            var border_color = disabled ? Theme.ForegroundDisabledColor :
                               state == CheckState.Checked ? Theme.AccentColor2 :
                               Theme.BorderLowColor;

            e.Canvas.DrawCircle (origin.X, origin.Y, outer_radius, border_color, e.LogicalToDeviceUnits (1));

            if (state == CheckState.Checked)
                e.Canvas.FillCircle (origin.X, origin.Y, inner_radius, disabled ? Theme.ForegroundDisabledColor : Theme.AccentColor2);
        }

        // --- WinForms compatibility overloads taking Majorsilence.Forms.Graphics ---

        /// <summary>Draws a focus rectangle on the given graphics surface.</summary>
        public static void DrawFocusRectangle (Graphics graphics, Rectangle rectangle)
            => graphics.DrawFocusRectangle (rectangle);

        /// <summary>Draws a focus rectangle using foreground and background colors. Stub in Majorsilence.Forms.</summary>
        public static void DrawFocusRectangle (Graphics graphics, Rectangle rectangle, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => graphics.DrawFocusRectangle (rectangle);

        /// <summary>Draws a 3D border around a rectangle. Stub in Majorsilence.Forms.</summary>
        public static void DrawBorder3D (Graphics graphics, Rectangle rectangle) { }

        /// <summary>Draws a 3D border with the specified style. Stub in Majorsilence.Forms.</summary>
        public static void DrawBorder3D (Graphics graphics, Rectangle rectangle, Border3DStyle style) { }

        /// <summary>Draws a border around a rectangle. Stub in Majorsilence.Forms.</summary>
        public static void DrawBorder (Graphics graphics, Rectangle bounds, System.Drawing.Color color, ButtonBorderStyle style) { }

        /// <summary>Draws a border with different style on each side. Stub in Majorsilence.Forms.</summary>
        public static void DrawBorder (Graphics graphics, Rectangle bounds,
            System.Drawing.Color leftColor, int leftWidth, ButtonBorderStyle leftStyle,
            System.Drawing.Color topColor, int topWidth, ButtonBorderStyle topStyle,
            System.Drawing.Color rightColor, int rightWidth, ButtonBorderStyle rightStyle,
            System.Drawing.Color bottomColor, int bottomWidth, ButtonBorderStyle bottomStyle) { }

        /// <summary>Draws a button control. Stub in Majorsilence.Forms.</summary>
        public static void DrawButton (Graphics graphics, Rectangle rectangle, ButtonState state) { }

        /// <summary>Draws a button control using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawButton (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawButton (graphics, new Rectangle (x, y, width, height), state);

        /// <summary>Draws a standard check box. Stub in Majorsilence.Forms.</summary>
        public static void DrawCheckBox (Graphics graphics, Rectangle rectangle, ButtonState state) { }

        /// <summary>Draws a check box using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawCheckBox (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawCheckBox (graphics, new Rectangle (x, y, width, height), state);

        /// <summary>Draws a combo box drop-down button. Stub in Majorsilence.Forms.</summary>
        public static void DrawComboButton (Graphics graphics, Rectangle rectangle, ButtonState state) { }

        /// <summary>Draws a combo box button using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawComboButton (Graphics graphics, int x, int y, int width, int height, ButtonState state)
            => DrawComboButton (graphics, new Rectangle (x, y, width, height), state);

        /// <summary>Draws a menu glyph. Stub in Majorsilence.Forms.</summary>
        public static void DrawMenuGlyph (Graphics graphics, Rectangle rectangle, MenuGlyph glyph) { }

        /// <summary>Draws a radio button. Stub in Majorsilence.Forms.</summary>
        public static void DrawRadioButton (Graphics graphics, Rectangle rectangle, ButtonState state) { }

        /// <summary>Draws a scroll button. Stub in Majorsilence.Forms.</summary>
        public static void DrawScrollButton (Graphics graphics, Rectangle rectangle, ScrollButton button, ButtonState state) { }

        /// <summary>Draws a size grip. Stub in Majorsilence.Forms.</summary>
        public static void DrawSizeGrip (Graphics graphics, System.Drawing.Color backColor, Rectangle bounds) { }

        /// <summary>Draws a size grip using x/y/width/height. Stub in Majorsilence.Forms.</summary>
        public static void DrawSizeGrip (Graphics graphics, System.Drawing.Color backColor, int x, int y, int width, int height)
            => DrawSizeGrip (graphics, backColor, new Rectangle (x, y, width, height));

        /// <summary>Draws a string in its disabled/grayed state. Stub in Majorsilence.Forms.</summary>
        public static void DrawStringDisabled (Graphics graphics, string s, Majorsilence.Drawing.Font font, System.Drawing.Color color, RectangleF layoutRectangle, Majorsilence.Drawing.StringFormat? format) { }

#pragma warning disable CA1416
        /// <summary>Draws a string at the specified coordinates. Stub in Majorsilence.Forms.</summary>
        public static void DrawString (Graphics graphics, string s, Majorsilence.Drawing.Font font, System.Drawing.Color color, int x, int y)
        {
            using var brush = new Majorsilence.Drawing.SolidBrush (color);
            graphics.DrawString (s, font, brush, x, y);
        }

        /// <summary>Draws a string within the specified rectangle. Stub in Majorsilence.Forms.</summary>
        public static void DrawString (Graphics graphics, string s, Majorsilence.Drawing.Font font, System.Drawing.Color color, Rectangle layoutRectangle)
        {
            using var brush = new Majorsilence.Drawing.SolidBrush (color);
            graphics.DrawString (s, font, brush, new System.Drawing.RectangleF (layoutRectangle.X, layoutRectangle.Y, layoutRectangle.Width, layoutRectangle.Height));
        }

        /// <summary>Draws a string within the specified RectangleF with a StringFormat. Stub in Majorsilence.Forms.</summary>
        public static void DrawString (Graphics graphics, string s, Majorsilence.Drawing.Font font, System.Drawing.Color color, System.Drawing.RectangleF layoutRectangle, Majorsilence.Drawing.StringFormat format)
        {
            using var brush = new Majorsilence.Drawing.SolidBrush (color);
            graphics.DrawString (s, font, brush, layoutRectangle);
        }
#pragma warning restore CA1416

        /// <summary>Creates a color that is lighter than the given color.</summary>
        public static System.Drawing.Color Light (System.Drawing.Color baseColor, float percOfLightLight = 10f)
        {
            var r = Math.Min (255, baseColor.R + (int)(percOfLightLight * 2.55f));
            var g = Math.Min (255, baseColor.G + (int)(percOfLightLight * 2.55f));
            var b = Math.Min (255, baseColor.B + (int)(percOfLightLight * 2.55f));
            return System.Drawing.Color.FromArgb (baseColor.A, r, g, b);
        }

        /// <summary>Creates a color that is darker than the given color.</summary>
        public static System.Drawing.Color Dark (System.Drawing.Color baseColor, float percOfDarkDark = 10f)
        {
            var r = Math.Max (0, baseColor.R - (int)(percOfDarkDark * 2.55f));
            var g = Math.Max (0, baseColor.G - (int)(percOfDarkDark * 2.55f));
            var b = Math.Max (0, baseColor.B - (int)(percOfDarkDark * 2.55f));
            return System.Drawing.Color.FromArgb (baseColor.A, r, g, b);
        }

        /// <summary>Returns a color significantly lighter than the given base color.</summary>
        public static System.Drawing.Color LightLight (System.Drawing.Color baseColor) => Light (baseColor, 40f);

        /// <summary>Returns a color significantly darker than the given base color.</summary>
        public static System.Drawing.Color DarkDark (System.Drawing.Color baseColor) => Dark (baseColor, 40f);
    }
}
