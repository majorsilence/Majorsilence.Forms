using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the menus WinForms' default
    // ToolStripProfessionalRenderer draws under Windows 11, from its colour table there (ProfessionalColorTable, as
    // measured): a menu bar shaded left to right from Control to near-white; a hovered item in the selection blue inside
    // the accent; the open item a near-white box in a grey line that runs on round its drop-down; and a near-white
    // drop-down with a shaded image margin, grey separators and GrayText for a disabled item. The theme's flat greys
    // stood for all of these. Apps that have not chosen a font keep the theme's menus.
    internal static class MenuUpstreamLook
    {
        internal static readonly SKColor BarBegin = new SKColor (0xF0, 0xF0, 0xF0);          // MenuStripGradientBegin
        internal static readonly SKColor BarEnd = new SKColor (0xFC, 0xFC, 0xFC);            // MenuStripGradientEnd
        internal static readonly SKColor PressedBegin = new SKColor (0xFC, 0xFC, 0xFC);      // MenuItemPressedGradientBegin
        internal static readonly SKColor PressedEnd = new SKColor (0xF8, 0xF8, 0xF8);        // MenuItemPressedGradientEnd
        internal static readonly SKColor Border = new SKColor (0x80, 0x80, 0x80);            // MenuBorder
        internal static readonly SKColor DropDownBackground = new SKColor (0xFD, 0xFD, 0xFD); // ToolStripDropDownBackground
        internal static readonly SKColor MarginBegin = new SKColor (0xFC, 0xFC, 0xFC);       // ImageMarginGradientBegin
        internal static readonly SKColor MarginMiddle = new SKColor (0xF8, 0xF8, 0xF8);      // ImageMarginGradientMiddle
        internal static readonly SKColor MarginEnd = new SKColor (0xF0, 0xF0, 0xF0);         // ImageMarginGradientEnd
        internal static readonly SKColor Separator = new SKColor (0xBD, 0xBD, 0xBD);         // SeparatorDark
        internal static readonly SKColor SelectedFill = new SKColor (0xB3, 0xD7, 0xF3);      // MenuItemSelectedGradient
        internal static readonly SKColor SelectedBorder = new SKColor (0x00, 0x78, 0xD7);    // MenuItemBorder

        internal static bool Applies => ControlPaint.UsesUpstreamGlyphs;

        internal static SKColor DisabledText => SystemColors.GrayText.ToSKColor ();

        // Fills a rectangle with a linear gradient through the colours, evenly spaced, across it or down it.
        internal static void FillGradient (PaintEventArgs e, Rectangle rect, bool horizontal, params SKColor[] colours)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            var end = horizontal ? new SKPoint (rect.Right, rect.Top) : new SKPoint (rect.Left, rect.Bottom);
            using var shader = SKShader.CreateLinearGradient (new SKPoint (rect.Left, rect.Top), end, colours, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader };
            e.Canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        // A one-pixel (one device unit per logical pixel) line round the inside of a rectangle.
        internal static void Frame (PaintEventArgs e, Rectangle rect, SKColor colour)
        {
            var px = e.LogicalToDeviceUnits (1);
            using var paint = new SKPaint { Color = colour };
            e.Canvas.DrawRect (SKRect.Create (rect.Left, rect.Top, rect.Width, px), paint);
            e.Canvas.DrawRect (SKRect.Create (rect.Left, rect.Bottom - px, rect.Width, px), paint);
            e.Canvas.DrawRect (SKRect.Create (rect.Left, rect.Top, px, rect.Height), paint);
            e.Canvas.DrawRect (SKRect.Create (rect.Right - px, rect.Top, px, rect.Height), paint);
        }

        // A selected item: the selection blue inside the accent.
        internal static void Selection (PaintEventArgs e, Rectangle rect)
        {
            using (var paint = new SKPaint { Color = SelectedFill })
                e.Canvas.DrawRect (SKRect.Create (rect.Left, rect.Top, rect.Width, rect.Height), paint);

            Frame (e, rect, SelectedBorder);
        }
    }
}
