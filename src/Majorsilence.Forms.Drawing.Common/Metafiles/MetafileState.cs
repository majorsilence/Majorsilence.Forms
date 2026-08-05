using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging.Metafiles
{
    // The GDI device-context state a metafile player has to keep.
    //
    // Metafile records are not self-contained drawing commands: "Rectangle" means "fill this box with
    // whatever brush is currently selected and outline it with the current pen", and the pen was
    // chosen by a record possibly hundreds earlier. Playing a metafile back is therefore mostly about
    // maintaining this state correctly -- a player that got every drawing record right but leaked
    // state across a SaveDC would produce a picture wrong in ways that look like a rendering bug.

    /// <summary>A pen selected into a device context.</summary>
    internal sealed class MetaPen
    {
        internal SKColor Color = SKColors.Black;
        internal float Width = 1f;
        internal int Style;

        /// <summary>Gets whether this pen draws nothing (PS_NULL).</summary>
        internal bool IsNull => Style == 5;

        /// <summary>The dash pattern for this pen's style, or null for a solid line.</summary>
        /// <remarks>Scaled by the pen width, as GDI does: a dashed hairline and a dashed fat line
        /// have the same number of dashes per inch, not the same dash length in pixels.</remarks>
        internal float[]? DashPattern
        {
            get {
                var unit = Math.Max (Width, 1f);

                return Style switch {
                    1 => [unit * 6, unit * 3],                                  // PS_DASH
                    2 => [unit, unit * 2],                                      // PS_DOT
                    3 => [unit * 6, unit * 2, unit, unit * 2],                  // PS_DASHDOT
                    4 => [unit * 6, unit * 2, unit, unit * 2, unit, unit * 2],  // PS_DASHDOTDOT
                    _ => null,
                };
            }
        }
    }

    /// <summary>A brush selected into a device context.</summary>
    internal sealed class MetaBrush
    {
        internal SKColor Color = SKColors.White;
        internal int Style;
        internal int Hatch;
        internal SKBitmap? Pattern;

        /// <summary>Gets whether this brush fills nothing (BS_NULL / BS_HOLLOW).</summary>
        internal bool IsNull => Style == 1;
    }

    /// <summary>A font selected into a device context.</summary>
    internal sealed class MetaFont
    {
        internal string Name = "Arial";
        internal float Height = 12f;
        internal bool Bold;
        internal bool Italic;
        internal bool Underline;
        internal bool Strikeout;

        /// <summary>The escapement, in tenths of a degree counter-clockwise.</summary>
        internal int Escapement;
    }

    /// <summary>The part of a device context that <c>SaveDC</c> and <c>RestoreDC</c> preserve.</summary>
    internal sealed class MetaDeviceContext
    {
        internal MetaPen Pen = new ();
        internal MetaBrush Brush = new ();
        internal MetaFont Font = new ();

        internal SKColor TextColor = SKColors.Black;
        internal SKColor BackColor = SKColors.White;
        internal int BackMode = 2;      // OPAQUE = 2, TRANSPARENT = 1
        internal int TextAlign;
        internal int PolyFillMode = 1;  // ALTERNATE = 1, WINDING = 2
        internal int MapMode = 1;       // MM_TEXT

        internal SKPoint Current;
        internal SKPoint WindowOrigin;
        internal SKSize WindowExtent = new (1, 1);
        internal SKPoint ViewportOrigin;

        // Defaults to the device extent, not to 1x1: a metafile that sets a window extent and no
        // viewport extent -- which is most of them -- means "map my window onto the whole device".
        // Defaulting to one collapses the entire picture into a single pixel.
        internal SKSize ViewportExtent = new (1, 1);
        internal bool ExtentsSet;

        internal SKMatrix World = SKMatrix.Identity;

        internal MetaDeviceContext Clone () => new () {
            // The pen, brush and font are immutable once selected -- a metafile creates a new object
            // rather than mutating a selected one -- so sharing them across a save is safe and keeps
            // a deeply nested SaveDC from copying the whole object table each time.
            Pen = Pen,
            Brush = Brush,
            Font = Font,
            TextColor = TextColor,
            BackColor = BackColor,
            BackMode = BackMode,
            TextAlign = TextAlign,
            PolyFillMode = PolyFillMode,
            MapMode = MapMode,
            Current = Current,
            WindowOrigin = WindowOrigin,
            WindowExtent = WindowExtent,
            ViewportOrigin = ViewportOrigin,
            ViewportExtent = ViewportExtent,
            ExtentsSet = ExtentsSet,
            World = World,
        };

        /// <summary>The logical-to-page transform implied by the map mode and window/viewport pair.</summary>
        internal SKMatrix MapTransform
        {
            get {
                // The metric map modes are fixed scales from logical units to physical ones, stated
                // at 96 dots per inch because that is the resolution the rasteriser works in.
                var (scale, flip) = MapMode switch {
                    2 => (96f / 254f, true),        // MM_LOMETRIC   0.1 mm
                    3 => (96f / 2540f, true),       // MM_HIMETRIC   0.01 mm
                    4 => (96f / 100f, true),        // MM_LOENGLISH  0.01 in
                    5 => (96f / 1000f, true),       // MM_HIENGLISH  0.001 in
                    6 => (96f / 1440f, true),       // MM_TWIPS      1/1440 in
                    _ => (1f, false),
                };

                if (flip)
                    return SKMatrix.CreateScale (scale, -scale);

                // MM_TEXT with no extents set is the identity; anything else maps the window onto
                // the viewport, which is how a metafile states its own coordinate space.
                if (!ExtentsSet || WindowExtent.Width == 0 || WindowExtent.Height == 0)
                    return SKMatrix.Identity;

                var sx = ViewportExtent.Width / WindowExtent.Width;
                var sy = ViewportExtent.Height / WindowExtent.Height;

                return SKMatrix.CreateTranslation (ViewportOrigin.X, ViewportOrigin.Y)
                    .PreConcat (SKMatrix.CreateScale (sx, sy))
                    .PreConcat (SKMatrix.CreateTranslation (-WindowOrigin.X, -WindowOrigin.Y));
            }
        }
    }

    /// <summary>The GDI stock objects a metafile can select without creating them.</summary>
    internal static class StockObjects
    {
        internal static MetaBrush? Brush (int index) => index switch {
            0 => new MetaBrush { Color = SKColors.White },
            1 => new MetaBrush { Color = new SKColor (0xC0, 0xC0, 0xC0) },
            2 => new MetaBrush { Color = new SKColor (0x80, 0x80, 0x80) },
            3 => new MetaBrush { Color = new SKColor (0x40, 0x40, 0x40) },
            4 => new MetaBrush { Color = SKColors.Black },
            5 => new MetaBrush { Style = 1 },
            _ => null,
        };

        internal static MetaPen? Pen (int index) => index switch {
            6 => new MetaPen { Color = SKColors.White },
            7 => new MetaPen { Color = SKColors.Black },
            8 => new MetaPen { Style = 5 },
            _ => null,
        };

        internal static MetaFont? Font (int index) => index switch {
            // OEM_FIXED, ANSI_FIXED and SYSTEM_FIXED are the monospaced ones; the rest are the UI face.
            10 or 11 or 16 => new MetaFont { Name = "Courier New", Height = 12f },
            12 or 13 or 17 => new MetaFont { Name = "Arial", Height = 12f },
            _ => null,
        };
    }
}
