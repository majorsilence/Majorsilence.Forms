using System;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>The standard message glyphs: information, warning, error and question.</summary>
    internal enum MessageGlyph
    {
        Information,
        Warning,
        Error,
        Question,
        Shield,
    }

    /// <summary>
    /// Draws the message glyphs a <see cref="ToolTip"/>'s <see cref="ToolTipIcon"/> and a
    /// <see cref="MessageBox"/>'s <see cref="MessageBoxIcon"/> show (W6 mechanisms). Drawn, as the rest
    /// of the chrome is, rather than taken from the OS: an information or question disc in blue, a
    /// warning triangle in amber, an error disc in red, each with a white mark.
    /// </summary>
    internal static class MessageGlyphs
    {
        internal static readonly SKColor InformationColor = new (0x20, 0x70, 0xD0);
        internal static readonly SKColor WarningColor = new (0xF0, 0xA0, 0x20);
        internal static readonly SKColor ErrorColor = new (0xD0, 0x30, 0x30);

        internal static MessageGlyph? For (ToolTipIcon icon) => icon switch {
            ToolTipIcon.Info => MessageGlyph.Information,
            ToolTipIcon.Warning => MessageGlyph.Warning,
            ToolTipIcon.Error => MessageGlyph.Error,
            _ => null,
        };

        internal static MessageGlyph? For (MessageBoxIcon icon) => icon switch {
            MessageBoxIcon.Error => MessageGlyph.Error,
            MessageBoxIcon.Warning => MessageGlyph.Warning,
            MessageBoxIcon.Question => MessageGlyph.Question,
            MessageBoxIcon.Information => MessageGlyph.Information,
            _ => null,
        };

        internal static readonly SKColor ShieldColor = new (0x40, 0x60, 0x90);

        internal static SKColor ColorOf (MessageGlyph glyph) => glyph switch {
            MessageGlyph.Warning => WarningColor,
            MessageGlyph.Error => ErrorColor,
            MessageGlyph.Shield => ShieldColor,
            _ => InformationColor,
        };

        /// <summary>The glyph a <see cref="TaskDialogIcon"/> stands for, or null for None and the image-backed ones.</summary>
        internal static MessageGlyph? For (TaskDialogIcon? icon) => icon?.Name switch {
            nameof (TaskDialogIcon.Information) => MessageGlyph.Information,
            nameof (TaskDialogIcon.Warning) => MessageGlyph.Warning,
            nameof (TaskDialogIcon.Error) => MessageGlyph.Error,
            nameof (TaskDialogIcon.Shield) or nameof (TaskDialogIcon.ShieldBlueBar) or nameof (TaskDialogIcon.ShieldGrayBar)
                or nameof (TaskDialogIcon.ShieldErrorRedBar) or nameof (TaskDialogIcon.ShieldSuccessGreenBar)
                or nameof (TaskDialogIcon.ShieldWarningYellowBar) => MessageGlyph.Shield,
            _ => null,
        };

        internal static void Draw (SKCanvas canvas, MessageGlyph glyph, Rectangle box)
        {
            if (box.Width <= 0 || box.Height <= 0)
                return;

            using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ColorOf (glyph) };
            using var ink = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, Color = SKColors.White, StrokeWidth = Math.Max (1f, box.Width / 7f), StrokeCap = SKStrokeCap.Round };
            var cx = box.Left + box.Width / 2f;
            var cy = box.Top + box.Height / 2f;
            var r = Math.Min (box.Width, box.Height) / 2f;

            switch (glyph) {
                case MessageGlyph.Warning: {
                    using var tri = new SKPath ();
                    tri.MoveTo (cx, box.Top);
                    tri.LineTo (box.Right, box.Bottom);
                    tri.LineTo (box.Left, box.Bottom);
                    tri.Close ();
                    canvas.DrawPath (tri, fill);
                    canvas.DrawLine (cx, cy - r * 0.3f, cx, cy + r * 0.25f, ink);
                    canvas.DrawPoint (cx, cy + r * 0.6f, ink);
                    break;
                }
                case MessageGlyph.Error:
                    canvas.DrawCircle (cx, cy, r, fill);
                    canvas.DrawLine (cx - r * 0.4f, cy - r * 0.4f, cx + r * 0.4f, cy + r * 0.4f, ink);
                    canvas.DrawLine (cx - r * 0.4f, cy + r * 0.4f, cx + r * 0.4f, cy - r * 0.4f, ink);
                    break;
                case MessageGlyph.Shield: {
                    // A crest: square shoulders narrowing to a point, with a white chevron.
                    using var crest = new SKPath ();
                    crest.MoveTo (box.Left, box.Top);
                    crest.LineTo (box.Right, box.Top);
                    crest.LineTo (box.Right, cy + r * 0.2f);
                    crest.LineTo (cx, box.Bottom);
                    crest.LineTo (box.Left, cy + r * 0.2f);
                    crest.Close ();
                    canvas.DrawPath (crest, fill);
                    canvas.DrawLine (cx - r * 0.4f, cy - r * 0.1f, cx - r * 0.1f, cy + r * 0.25f, ink);
                    canvas.DrawLine (cx - r * 0.1f, cy + r * 0.25f, cx + r * 0.45f, cy - r * 0.45f, ink);
                    break;
                }
                case MessageGlyph.Question: {
                    canvas.DrawCircle (cx, cy, r, fill);
                    using var hook = new SKPath ();
                    hook.MoveTo (cx - r * 0.35f, cy - r * 0.25f);
                    hook.CubicTo (cx - r * 0.35f, cy - r * 0.75f, cx + r * 0.4f, cy - r * 0.75f, cx + r * 0.3f, cy - r * 0.2f);
                    hook.LineTo (cx, cy + r * 0.1f);
                    hook.LineTo (cx, cy + r * 0.25f);
                    canvas.DrawPath (hook, ink);
                    canvas.DrawPoint (cx, cy + r * 0.6f, ink);
                    break;
                }
                default:
                    canvas.DrawCircle (cx, cy, r, fill);
                    canvas.DrawLine (cx, cy - r * 0.1f, cx, cy + r * 0.5f, ink);
                    canvas.DrawPoint (cx, cy - r * 0.5f, ink);
                    break;
            }
        }
    }
}
