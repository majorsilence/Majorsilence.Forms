using System;
using System.Drawing;
using MFDrawing = Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms
{
    // GFX-01: twenty ControlPaint.Draw* overloads had literally empty bodies.
    //
    // This is *the* API an owner-drawn migrated control uses to paint its chrome. A custom cell painter,
    // an owner-drawn ListBox, a UserControl calling ControlPaint.DrawBorder (g, ClientRectangle, …), a
    // combo drawing its own drop arrow -- all rendered nothing at all. The control looked empty rather
    // than mis-styled, which is the visible and common failure. This library's own GroupBox frame went
    // through DrawVisualStyleBorder -> DrawBorder and so had no frame.
    //
    // Everything here is Skia primitives over Majorsilence.Forms.Drawing.Graphics: no glyph font, no
    // Win32 DrawFrameControl bitmap. The bevel colours come from the HLS Light/Dark family (GFX-02),
    // which had to be right first or every bevel would be a flat smear.
    public static partial class ControlPaint
    {
        // The classic four-colour 3D palette for a face colour: the two highlights and the two shadows
        // a Win32 bevel is built from.
        private static (Color LightLight, Color Light, Color Dark, Color DarkDark) BevelColors (Color face)
            => (LightLight (face), Light (face, 0f), Dark (face, 0f), DarkDark (face));

        private static void StrokeLine (Graphics graphics, Color color, int x1, int y1, int x2, int y2)
        {
            using var pen = new MFDrawing.Pen (color, 1);
            graphics.DrawLine (pen, x1, y1, x2, y2);
        }

        private static void Fill (Graphics graphics, Rectangle bounds, Color color)
        {
            using var brush = new MFDrawing.SolidBrush (color);
            graphics.FillRectangle (brush, bounds);
        }

        // ---------------- borders

        /// <summary>Draws a border of the given style and colour around a rectangle.</summary>
        public static void DrawBorder (Graphics graphics, Rectangle bounds, Color color, ButtonBorderStyle style)
            => DrawBorder (graphics, bounds, color, 1, style, color, 1, style, color, 1, style, color, 1, style);

        /// <summary>Draws a border whose four sides each have their own colour, width and style.</summary>
        public static void DrawBorder (Graphics graphics, Rectangle bounds,
            Color leftColor, int leftWidth, ButtonBorderStyle leftStyle,
            Color topColor, int topWidth, ButtonBorderStyle topStyle,
            Color rightColor, int rightWidth, ButtonBorderStyle rightStyle,
            Color bottomColor, int bottomWidth, ButtonBorderStyle bottomStyle)
        {
            Guard.ThrowIfNull (graphics);

            DrawBorderSide (graphics, bounds, Border3DSide.Left, leftColor, leftWidth, leftStyle);
            DrawBorderSide (graphics, bounds, Border3DSide.Top, topColor, topWidth, topStyle);
            DrawBorderSide (graphics, bounds, Border3DSide.Right, rightColor, rightWidth, rightStyle);
            DrawBorderSide (graphics, bounds, Border3DSide.Bottom, bottomColor, bottomWidth, bottomStyle);
        }

        private static void DrawBorderSide (Graphics graphics, Rectangle bounds, Border3DSide side,
            Color color, int width, ButtonBorderStyle style)
        {
            if (style == ButtonBorderStyle.None || width <= 0)
                return;

            // Inset and Outset are a two-tone bevel, not a line: one side takes the shadow and the
            // opposite side the highlight, which is what gives the rectangle its direction.
            var stroke = style switch {
                ButtonBorderStyle.Inset => side is Border3DSide.Left or Border3DSide.Top ? Dark (color, 0f) : LightLight (color),
                ButtonBorderStyle.Outset => side is Border3DSide.Left or Border3DSide.Top ? LightLight (color) : Dark (color, 0f),
                _ => color,
            };

            using var pen = new MFDrawing.Pen (stroke, width);

            if (style is ButtonBorderStyle.Dotted)
                pen.DashStyle = MFDrawing.Drawing2D.DashStyle.Dot;
            else if (style is ButtonBorderStyle.Dashed)
                pen.DashStyle = MFDrawing.Drawing2D.DashStyle.Dash;

            // Each side is drawn `width` lines thick, growing inward, so a thick border stays inside its
            // bounds rather than straddling them.
            for (var i = 0; i < width; i++) {
                switch (side) {
                    case Border3DSide.Left:
                        graphics.DrawLine (pen, bounds.Left + i, bounds.Top, bounds.Left + i, bounds.Bottom - 1);
                        break;
                    case Border3DSide.Top:
                        graphics.DrawLine (pen, bounds.Left, bounds.Top + i, bounds.Right - 1, bounds.Top + i);
                        break;
                    case Border3DSide.Right:
                        graphics.DrawLine (pen, bounds.Right - 1 - i, bounds.Top, bounds.Right - 1 - i, bounds.Bottom - 1);
                        break;
                    case Border3DSide.Bottom:
                        graphics.DrawLine (pen, bounds.Left, bounds.Bottom - 1 - i, bounds.Right - 1, bounds.Bottom - 1 - i);
                        break;
                }
            }
        }

        /// <summary>Draws a sunken 3D border around a rectangle.</summary>
        public static void DrawBorder3D (Graphics graphics, Rectangle rectangle)
            => DrawBorder3D (graphics, rectangle, Border3DStyle.Etched, Border3DSide.All);

        /// <inheritdoc cref="DrawBorder3D(Graphics,Rectangle)"/>
        public static void DrawBorder3D (Graphics graphics, Rectangle rectangle, Border3DStyle style)
            => DrawBorder3D (graphics, rectangle, style, Border3DSide.All);

        /// <summary>Draws a 3D border in the given style on the given sides.</summary>
        public static void DrawBorder3D (Graphics graphics, Rectangle rectangle, Border3DStyle style, Border3DSide sides)
        {
            Guard.ThrowIfNull (graphics);

            if (style == Border3DStyle.None || rectangle.Width <= 0 || rectangle.Height <= 0)
                return;

            var (lightLight, light, dark, darkDark) = BevelColors (SystemColors.Control);

            // The classic two-pixel bevel: an outer and an inner ring, each with a highlight edge and a
            // shadow edge. Which pair goes where is the whole difference between raised and sunken.
            var raised = style is Border3DStyle.Raised or Border3DStyle.RaisedInner or Border3DStyle.RaisedOuter or Border3DStyle.Bump;
            var outerTopLeft = raised ? lightLight : dark;
            var outerBottomRight = raised ? darkDark : lightLight;
            var innerTopLeft = raised ? light : darkDark;
            var innerBottomRight = raised ? dark : light;

            if (style == Border3DStyle.Flat) {
                outerTopLeft = outerBottomRight = innerTopLeft = innerBottomRight = dark;
            } else if (style == Border3DStyle.Etched) {
                outerTopLeft = dark;
                outerBottomRight = lightLight;
                innerTopLeft = lightLight;
                innerBottomRight = dark;
            }

            DrawBevelRing (graphics, rectangle, sides, outerTopLeft, outerBottomRight);

            var inner = Rectangle.Inflate (rectangle, -1, -1);

            if (inner.Width > 0 && inner.Height > 0)
                DrawBevelRing (graphics, inner, sides, innerTopLeft, innerBottomRight);
        }

        private static void DrawBevelRing (Graphics graphics, Rectangle bounds, Border3DSide sides, Color topLeft, Color bottomRight)
        {
            if (sides.HasFlag (Border3DSide.Top))
                StrokeLine (graphics, topLeft, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);

            if (sides.HasFlag (Border3DSide.Left))
                StrokeLine (graphics, topLeft, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);

            if (sides.HasFlag (Border3DSide.Bottom))
                StrokeLine (graphics, bottomRight, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);

            if (sides.HasFlag (Border3DSide.Right))
                StrokeLine (graphics, bottomRight, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        }

        // ---------------- button-like faces

        /// <summary>Draws a push button in the given state.</summary>
        public static void DrawButton (Graphics graphics, Rectangle rectangle, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);
            DrawButtonFace (graphics, rectangle, state);
        }

        // The shared face every button-like glyph sits on. Pushed swaps the bevel, which is what makes a
        // pressed button read as pressed; Flat is a single-line border with no bevel at all.
        private static void DrawButtonFace (Graphics graphics, Rectangle bounds, ButtonState state)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var face = SystemColors.Control;
            var (lightLight, _, dark, darkDark) = BevelColors (face);

            Fill (graphics, bounds, state.HasFlag (ButtonState.Checked) ? Light (face, 0f) : face);

            if (state.HasFlag (ButtonState.Flat)) {
                DrawBevelRing (graphics, bounds, Border3DSide.All, dark, dark);
                return;
            }

            var pushed = state.HasFlag (ButtonState.Pushed);

            DrawBevelRing (graphics, bounds, Border3DSide.All,
                pushed ? darkDark : lightLight,
                pushed ? lightLight : darkDark);

            var inner = Rectangle.Inflate (bounds, -1, -1);

            if (inner.Width > 0 && inner.Height > 0)
                DrawBevelRing (graphics, inner, Border3DSide.All, pushed ? dark : face, pushed ? face : dark);
        }

        /// <summary>Draws a check box in the given state.</summary>
        public static void DrawCheckBox (Graphics graphics, Rectangle rectangle, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);
            DrawCheckBoxCore (graphics, rectangle, state, mixed: false);
        }

        /// <summary>Draws a check box in its indeterminate state.</summary>
        public static void DrawMixedCheckBox (Graphics graphics, Rectangle rectangle, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);
            DrawCheckBoxCore (graphics, rectangle, state, mixed: true);
        }

        private static void DrawCheckBoxCore (Graphics graphics, Rectangle bounds, ButtonState state, bool mixed)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var (lightLight, _, dark, darkDark) = BevelColors (SystemColors.Control);
            var pushed = state.HasFlag (ButtonState.Pushed);

            // A check box's well is sunken, and a pushed one is filled with the face colour rather than
            // the window colour -- the same cue a pressed button gives.
            Fill (graphics, bounds, pushed || state.HasFlag (ButtonState.Inactive) ? SystemColors.Control : SystemColors.Window);
            DrawBevelRing (graphics, bounds, Border3DSide.All, dark, lightLight);

            var inner = Rectangle.Inflate (bounds, -1, -1);

            if (inner.Width > 0 && inner.Height > 0)
                DrawBevelRing (graphics, inner, Border3DSide.All, darkDark, SystemColors.Control);

            if (!state.HasFlag (ButtonState.Checked) && !mixed)
                return;

            var glyph = state.HasFlag (ButtonState.Inactive) ? SystemColors.GrayText : SystemColors.ControlText;
            var mark = Rectangle.Inflate (bounds, -3, -3);

            if (mark.Width <= 0 || mark.Height <= 0)
                return;

            if (mixed) {
                // Indeterminate is a filled block, not a tick.
                Fill (graphics, mark, glyph);
                return;
            }

            // A tick, as two strokes: down-right to the low point, then up-right.
            var midX = mark.Left + mark.Width / 3;
            var midY = mark.Bottom - 1 - mark.Height / 4;

            StrokeLine (graphics, glyph, mark.Left, mark.Top + mark.Height / 2, midX, midY);
            StrokeLine (graphics, glyph, midX, midY, mark.Right - 1, mark.Top);
        }

        /// <summary>Draws a radio button in the given state.</summary>
        public static void DrawRadioButton (Graphics graphics, Rectangle rectangle, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);

            if (rectangle.Width <= 0 || rectangle.Height <= 0)
                return;

            var (lightLight, _, dark, _) = BevelColors (SystemColors.Control);

            using (var fill = new MFDrawing.SolidBrush (state.HasFlag (ButtonState.Inactive) ? SystemColors.Control : SystemColors.Window))
                graphics.FillEllipse (fill, rectangle);

            using (var pen = new MFDrawing.Pen (dark, 1))
                graphics.DrawEllipse (pen, rectangle);

            // The highlight arc is approximated by a second ellipse inset on the bright side; a full
            // two-arc bevel needs arc drawing this Graphics does not carry.
            using (var pen = new MFDrawing.Pen (lightLight, 1))
                graphics.DrawEllipse (pen, Rectangle.Inflate (rectangle, -1, -1));

            if (!state.HasFlag (ButtonState.Checked))
                return;

            var dot = Rectangle.Inflate (rectangle, -rectangle.Width / 3, -rectangle.Height / 3);

            if (dot.Width <= 0 || dot.Height <= 0)
                return;

            using var glyph = new MFDrawing.SolidBrush (state.HasFlag (ButtonState.Inactive) ? SystemColors.GrayText : SystemColors.ControlText);
            graphics.FillEllipse (glyph, dot);
        }

        /// <summary>Draws a combo box's drop-down button.</summary>
        public static void DrawComboButton (Graphics graphics, Rectangle rectangle, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);
            DrawButtonFace (graphics, rectangle, state);
            DrawTriangle (graphics, rectangle, ArrowDirection.Down, state);
        }

        /// <summary>Draws a scroll bar's arrow button.</summary>
        public static void DrawScrollButton (Graphics graphics, Rectangle rectangle, ScrollButton button, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);
            DrawButtonFace (graphics, rectangle, state);

            DrawTriangle (graphics, rectangle, button switch {
                ScrollButton.Up => ArrowDirection.Up,
                ScrollButton.Down => ArrowDirection.Down,
                ScrollButton.Left => ArrowDirection.Left,
                _ => ArrowDirection.Right,
            }, state);
        }

        /// <summary>Draws a window caption button (close, minimise, maximise, restore, help).</summary>
        public static void DrawCaptionButton (Graphics graphics, Rectangle rectangle, CaptionButton button, ButtonState state)
        {
            Guard.ThrowIfNull (graphics);
            DrawButtonFace (graphics, rectangle, state);

            var glyph = Rectangle.Inflate (rectangle, -rectangle.Width / 4, -rectangle.Height / 4);

            if (glyph.Width <= 0 || glyph.Height <= 0)
                return;

            var ink = state.HasFlag (ButtonState.Inactive) ? SystemColors.GrayText : SystemColors.ControlText;
            var offset = state.HasFlag (ButtonState.Pushed) ? 1 : 0;
            glyph.Offset (offset, offset);

            switch (button) {
                case CaptionButton.Close:
                    StrokeLine (graphics, ink, glyph.Left, glyph.Top, glyph.Right - 1, glyph.Bottom - 1);
                    StrokeLine (graphics, ink, glyph.Right - 1, glyph.Top, glyph.Left, glyph.Bottom - 1);
                    break;

                case CaptionButton.Minimize:
                    StrokeLine (graphics, ink, glyph.Left, glyph.Bottom - 1, glyph.Right - 1, glyph.Bottom - 1);
                    break;

                case CaptionButton.Maximize:
                    DrawBevelRing (graphics, glyph, Border3DSide.All, ink, ink);
                    break;

                case CaptionButton.Restore: {
                    var back = new Rectangle (glyph.Left + glyph.Width / 4, glyph.Top, glyph.Width - glyph.Width / 4, glyph.Height - glyph.Height / 4);
                    var front = new Rectangle (glyph.Left, glyph.Top + glyph.Height / 4, glyph.Width - glyph.Width / 4, glyph.Height - glyph.Height / 4);
                    DrawBevelRing (graphics, back, Border3DSide.All, ink, ink);
                    DrawBevelRing (graphics, front, Border3DSide.All, ink, ink);
                    break;
                }

                case CaptionButton.Help:
                    // A question mark needs a font; a filled dot says "this button does something" and
                    // is honest about not being the glyph. Recorded rather than left blank.
                    Fill (graphics, Rectangle.Inflate (glyph, -glyph.Width / 3, -glyph.Height / 3), ink);
                    break;
            }
        }

        // ---------------- menu glyphs

        /// <summary>Draws a menu glyph (check mark, bullet or submenu arrow).</summary>
        public static void DrawMenuGlyph (Graphics graphics, Rectangle rectangle, MenuGlyph glyph)
            => DrawMenuGlyph (graphics, rectangle, glyph, SystemColors.MenuText, SystemColors.Menu);

        /// <summary>Draws a menu glyph in the given colours.</summary>
        public static void DrawMenuGlyph (Graphics graphics, Rectangle rectangle, MenuGlyph glyph,
            Color foreColor, Color backColor)
        {
            Guard.ThrowIfNull (graphics);

            if (rectangle.Width <= 0 || rectangle.Height <= 0)
                return;

            Fill (graphics, rectangle, backColor);

            var mark = Rectangle.Inflate (rectangle, -rectangle.Width / 4, -rectangle.Height / 4);

            if (mark.Width <= 0 || mark.Height <= 0)
                return;

            switch (glyph) {
                case MenuGlyph.Checkmark: {
                    var midX = mark.Left + mark.Width / 3;
                    var midY = mark.Bottom - 1 - mark.Height / 4;
                    StrokeLine (graphics, foreColor, mark.Left, mark.Top + mark.Height / 2, midX, midY);
                    StrokeLine (graphics, foreColor, midX, midY, mark.Right - 1, mark.Top);
                    break;
                }

                case MenuGlyph.Bullet: {
                    using var brush = new MFDrawing.SolidBrush (foreColor);
                    graphics.FillEllipse (brush, Rectangle.Inflate (mark, -mark.Width / 4, -mark.Height / 4));
                    break;
                }

                default:
                    DrawTriangle (graphics, rectangle, ArrowDirection.Right, ButtonState.Normal, foreColor);
                    break;
            }
        }

        // A solid triangle filling the middle of the bounds, drawn as a stack of lines so it needs no
        // path support. Pushed offsets it by a pixel, as the glyph on a pressed button does.
        private static void DrawTriangle (Graphics graphics, Rectangle bounds, ArrowDirection direction, ButtonState state, Color? color = null)
        {
            var ink = color ?? (state.HasFlag (ButtonState.Inactive) ? SystemColors.GrayText : SystemColors.ControlText);
            var size = Math.Max (2, Math.Min (bounds.Width, bounds.Height) / 3);
            var cx = bounds.Left + bounds.Width / 2 + (state.HasFlag (ButtonState.Pushed) ? 1 : 0);
            var cy = bounds.Top + bounds.Height / 2 + (state.HasFlag (ButtonState.Pushed) ? 1 : 0);

            for (var i = 0; i < size; i++) {
                switch (direction) {
                    case ArrowDirection.Down:
                        StrokeLine (graphics, ink, cx - size + i, cy - size / 2 + i, cx + size - i, cy - size / 2 + i);
                        break;
                    case ArrowDirection.Up:
                        StrokeLine (graphics, ink, cx - i, cy + size / 2 - i, cx + i, cy + size / 2 - i);
                        break;
                    case ArrowDirection.Right:
                        StrokeLine (graphics, ink, cx - size / 2 + i, cy - size + i, cx - size / 2 + i, cy + size - i);
                        break;
                    default:
                        StrokeLine (graphics, ink, cx + size / 2 - i, cy - i, cx + size / 2 - i, cy + i);
                        break;
                }
            }
        }

        // ---------------- designer and window furniture

        /// <summary>Draws the diagonal size grip in a window's bottom-right corner.</summary>
        public static void DrawSizeGrip (Graphics graphics, Color backColor, Rectangle bounds)
        {
            Guard.ThrowIfNull (graphics);

            var light = LightLight (backColor);
            var dark = Dark (backColor, 0f);

            // Three short diagonal ticks per 4px group, walking in from the corner. Each tick spans only
            // its own group -- drawing from the group's x all the way to the corner would smear a long
            // diagonal across the whole bounds, which is not a grip and puts ink nowhere near the corner.
            var reach = Math.Min (bounds.Width, bounds.Height);

            for (var offset = 1; offset + 3 < reach; offset += 4) {
                var x = bounds.Right - offset;
                var y = bounds.Bottom - offset;

                // No bounds clip here, deliberately: the loop condition (offset + 3 < reach) already
                // guarantees the furthest tick, at offset + 3, is inside the rectangle. A clip as well
                // was unreachable, and an unexercised guard reads as a live one to the next person.
                for (var tick = 0; tick < 3; tick++)
                    StrokeLine (graphics, tick == 2 ? light : dark,
                        x - 1 - tick, bounds.Bottom - 1, bounds.Right - 1, y - 1 - tick);
            }
        }

        /// <summary>Draws a designer grab handle.</summary>
        public static void DrawGrabHandle (Graphics graphics, Rectangle rectangle, bool primary, bool enabled)
        {
            Guard.ThrowIfNull (graphics);

            // Primary handles are filled white with a black edge; secondary ones invert that, which is
            // how a designer distinguishes the anchor selection from the rest.
            var fill = !enabled ? SystemColors.Control : primary ? Color.White : Color.Black;
            var edge = !enabled ? SystemColors.ControlDark : primary ? Color.Black : Color.White;

            Fill (graphics, rectangle, fill);
            DrawBevelRing (graphics, rectangle, Border3DSide.All, edge, edge);
        }

        /// <summary>Draws the grab handle of a container being resized in a designer.</summary>
        public static void DrawContainerGrabHandle (Graphics graphics, Rectangle bounds)
        {
            Guard.ThrowIfNull (graphics);

            Fill (graphics, bounds, Color.White);
            DrawBevelRing (graphics, bounds, Border3DSide.All, Color.Black, Color.Black);

            // The cross that distinguishes a container handle from a plain one.
            var midX = bounds.Left + bounds.Width / 2;
            var midY = bounds.Top + bounds.Height / 2;

            StrokeLine (graphics, Color.Black, midX, bounds.Top + 1, midX, bounds.Bottom - 2);
            StrokeLine (graphics, Color.Black, bounds.Left + 1, midY, bounds.Right - 2, midY);
        }

        /// <summary>Draws the dashed frame that marks a locked designer control.</summary>
        public static void DrawLockedFrame (Graphics graphics, Rectangle rectangle, bool primary)
        {
            Guard.ThrowIfNull (graphics);

            var ink = primary ? Color.White : Color.Black;

            DrawBevelRing (graphics, rectangle, Border3DSide.All, ink, ink);

            var inner = Rectangle.Inflate (rectangle, -1, -1);

            if (inner.Width > 0 && inner.Height > 0)
                DrawBevelRing (graphics, inner, Border3DSide.All, ink, ink);
        }

        /// <summary>Draws the frame around a selected designer control.</summary>
        public static void DrawSelectionFrame (Graphics graphics, bool active, Rectangle outsideRect, Rectangle insideRect, Color backColor)
        {
            Guard.ThrowIfNull (graphics);

            // The frame is the band between the two rectangles, hatched when inactive so a control that
            // is selected but not focused reads differently.
            var ink = active ? SystemColors.ControlText : Dark (backColor, 0f);

            DrawBevelRing (graphics, outsideRect, Border3DSide.All, ink, ink);

            if (insideRect.Width > 0 && insideRect.Height > 0)
                DrawBevelRing (graphics, insideRect, Border3DSide.All, ink, ink);
        }

        /// <summary>Draws a string greyed out, the way a disabled control's text is drawn.</summary>
        public static void DrawStringDisabled (Graphics graphics, string s, MFDrawing.Font font,
            Color color, RectangleF layoutRectangle, MFDrawing.StringFormat format)
        {
            Guard.ThrowIfNull (graphics);

            // Upstream's shape: the highlight offset by one, then the shadow on top. That is what gives
            // disabled text its engraved look rather than just a paler colour.
            var highlight = new RectangleF (layoutRectangle.X + 1, layoutRectangle.Y + 1, layoutRectangle.Width, layoutRectangle.Height);

            using (var light = new MFDrawing.SolidBrush (LightLight (color)))
                graphics.DrawString (s, font, light, highlight);

            using var dark = new MFDrawing.SolidBrush (Dark (color, 0f));
            graphics.DrawString (s, font, dark, layoutRectangle);
        }

        /// <summary>
        /// Draws one cell's grid lines for a <see cref="TableLayoutPanel"/>, in the style its
        /// <c>CellBorderStyle</c> asks for. Internal, as upstream's is.
        /// </summary>
        /// <remarks>
        /// LAY-22. Each cell owns its top and left edges only; the table's right and bottom edges are
        /// drawn once by the panel, or adjacent cells would paint every interior line twice -- visible
        /// as a doubled-width line under the Inset and Outset styles, which use two colours.
        /// </remarks>
        internal static void PaintTableCellBorder (TableLayoutPanelCellBorderStyle borderStyle, PaintEventArgs e, Rectangle bounds)
        {
            Guard.ThrowIfNull (e);

            if (borderStyle == TableLayoutPanelCellBorderStyle.None || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // Inset and Outset are the same pair of colours with the light and dark swapped, which is
            // the whole difference between a cell that looks sunken and one that looks raised.
            var (top_left, bottom_right) = borderStyle switch {
                TableLayoutPanelCellBorderStyle.Inset or TableLayoutPanelCellBorderStyle.InsetDouble
                    => (Theme.BorderLowColor, Theme.BorderHighColor),
                TableLayoutPanelCellBorderStyle.Outset or TableLayoutPanelCellBorderStyle.OutsetDouble
                    or TableLayoutPanelCellBorderStyle.OutsetPartial
                    => (Theme.BorderHighColor, Theme.BorderLowColor),
                _ => (Theme.BorderMidColor, Theme.BorderMidColor),
            };

            // Top and left.
            e.Canvas.DrawLine (bounds.X, bounds.Y, bounds.Right, bounds.Y, top_left);
            e.Canvas.DrawLine (bounds.X, bounds.Y, bounds.X, bounds.Bottom, top_left);

            var doubled = borderStyle is TableLayoutPanelCellBorderStyle.InsetDouble
                              or TableLayoutPanelCellBorderStyle.OutsetDouble
                              or TableLayoutPanelCellBorderStyle.OutsetPartial;

            if (!doubled)
                return;

            // The second line of a *Double style, in the opposite colour, one pixel in.
            e.Canvas.DrawLine (bounds.X + 1, bounds.Y + 1, bounds.Right - 1, bounds.Y + 1, bottom_right);
            e.Canvas.DrawLine (bounds.X + 1, bounds.Y + 1, bounds.X + 1, bounds.Bottom - 1, bottom_right);
        }

    }
}
