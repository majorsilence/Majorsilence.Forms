using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>One shadow of a <c>box-shadow</c> list.</summary>
    internal readonly record struct ShadowSpec (int OffsetX, int OffsetY, int Blur, int Spread, bool Inset, uint Argb);

    /// <summary>A resolved <c>outline</c>: a ring drawn outside a control by its parent.</summary>
    internal readonly record struct OutlineSpec (int Width, bool Dashed, Color Color, int Offset);

    /// <summary>
    /// Draws what System.Windows.Forms has no property for -- blurred shadows, gradients -- with SkiaSharp (the library the
    /// Majorsilence.Forms controls paint with, so the result matches theirs) and hands it over as a <see cref="Bitmap"/> the
    /// control or its parent can paint. A bitmap is rebuilt only when what it shows changes, never per paint.
    /// </summary>
    internal static class EffectRenderer
    {
        public static IReadOnlyList<ShadowSpec> ReadShadows (ThemeCssValue? value)
        {
            var list = new List<ShadowSpec> ();

            if (value is null)
                return list;

            void Add (ThemeCssValue v)
            {
                if (v.Kind == ThemeCssValueKind.BoxShadow)
                    list.Add (new ShadowSpec (v.Pixels, v.OffsetY, v.Blur, v.Spread, v.Inset, v.Argb));
            }

            if (value.Kind == ThemeCssValueKind.List)
                foreach (var item in value.Items)
                    Add (item);
            else
                Add (value);

            return list;
        }

        private static SKRoundRect Box (SKRect rect, (int TopLeft, int TopRight, int BottomRight, int BottomLeft) corners, float grow = 0)
        {
            var box = new SKRoundRect ();
            float R (int radius) => radius <= 0 ? 0 : Math.Max (0, radius + grow);
            box.SetRectRadii (rect, new[] {
                new SKPoint (R (corners.TopLeft), R (corners.TopLeft)), new SKPoint (R (corners.TopRight), R (corners.TopRight)),
                new SKPoint (R (corners.BottomRight), R (corners.BottomRight)), new SKPoint (R (corners.BottomLeft), R (corners.BottomLeft)) });
            return box;
        }

        // CSS: a blur radius of B is a Gaussian with a standard deviation of B / 2.
        private static SKMaskFilter? Blur (int blur) => blur > 0 ? SKMaskFilter.CreateBlur (SKBlurStyle.Normal, blur / 2f) : null;

        /// <summary>
        /// The outer shadows of a control, behind it: a bitmap larger than the control by <paramref name="pad"/> on every side, with
        /// the control's own shape cut out (a translucent face must not show its shadow through it). Null when none is outer.
        /// </summary>
        public static Bitmap? OuterShadow (Size size, (int, int, int, int) corners, IReadOnlyList<ShadowSpec> shadows, out int pad)
        {
            pad = 0;
            var outer = new List<ShadowSpec> ();
            foreach (var shadow in shadows)
                if (!shadow.Inset)
                    outer.Add (shadow);

            if (outer.Count == 0 || size.Width <= 0 || size.Height <= 0)
                return null;

            foreach (var s in outer)
                pad = Math.Max (pad, Math.Max (Math.Abs (s.OffsetX), Math.Abs (s.OffsetY)) + Math.Max (0, s.Spread) + s.Blur);
            pad += 2;

            using var bitmap = new SKBitmap (size.Width + pad * 2, size.Height + pad * 2, SKColorType.Bgra8888, SKAlphaType.Premul);
            bitmap.Erase (SKColors.Transparent);
            using var canvas = new SKCanvas (bitmap);

            var rect = new SKRect (pad, pad, pad + size.Width, pad + size.Height);
            var radii = (corners.Item1, corners.Item2, corners.Item3, corners.Item4);
            using (var face = Box (rect, radii))
                canvas.ClipRoundRect (face, SKClipOperation.Difference, true);

            // The first shadow of the list is on top, so paint from the last.
            for (var i = outer.Count - 1; i >= 0; i--) {
                var s = outer[i];
                var shadowRect = new SKRect (rect.Left + s.OffsetX - s.Spread, rect.Top + s.OffsetY - s.Spread, rect.Right + s.OffsetX + s.Spread, rect.Bottom + s.OffsetY + s.Spread);
                if (shadowRect.Width <= 0 || shadowRect.Height <= 0)
                    continue;

                using var round = Box (shadowRect, radii, s.Spread);
                using var filter = Blur (s.Blur);
                using var paint = new SKPaint { Color = new SKColor (s.Argb), IsAntialias = true, MaskFilter = filter };
                canvas.DrawRoundRect (round, paint);
            }

            return ToBitmap (bitmap);
        }

        /// <summary>The inset shadows of a control, as a bitmap the size of the control to paint over its face. Null when none is inset.</summary>
        public static Bitmap? InsetShadow (Size size, (int, int, int, int) corners, IReadOnlyList<ShadowSpec> shadows)
        {
            var inset = new List<ShadowSpec> ();
            foreach (var shadow in shadows)
                if (shadow.Inset)
                    inset.Add (shadow);

            if (inset.Count == 0 || size.Width <= 0 || size.Height <= 0)
                return null;

            using var bitmap = new SKBitmap (size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            bitmap.Erase (SKColors.Transparent);
            using var canvas = new SKCanvas (bitmap);

            var rect = new SKRect (0, 0, size.Width, size.Height);
            var radii = (corners.Item1, corners.Item2, corners.Item3, corners.Item4);
            using (var face = Box (rect, radii))
                canvas.ClipRoundRect (face, SKClipOperation.Intersect, true);

            for (var i = inset.Count - 1; i >= 0; i--) {
                var s = inset[i];

                // The shadow is cast by everything outside the (offset, shrunk) box: a frame with the box cut out of it.
                var reach = s.Blur * 3 + Math.Abs (s.OffsetX) + Math.Abs (s.OffsetY) + s.Spread + 8;
                var hole = new SKRect (rect.Left + s.OffsetX + s.Spread, rect.Top + s.OffsetY + s.Spread, rect.Right + s.OffsetX - s.Spread, rect.Bottom + s.OffsetY - s.Spread);
                if (hole.Width < 0)
                    hole = new SKRect (hole.MidX, hole.Top, hole.MidX, hole.Bottom);
                if (hole.Height < 0)
                    hole = new SKRect (hole.Left, hole.MidY, hole.Right, hole.MidY);

                using var path = new SKPath { FillType = SKPathFillType.EvenOdd };
                path.AddRect (new SKRect (rect.Left - reach, rect.Top - reach, rect.Right + reach, rect.Bottom + reach));
                using (var inner = Box (hole, radii, -s.Spread))
                    path.AddRoundRect (inner);

                using var filter = Blur (s.Blur);
                using var paint = new SKPaint { Color = new SKColor (s.Argb), IsAntialias = true, MaskFilter = filter };
                canvas.DrawPath (path, paint);
            }

            return ToBitmap (bitmap);
        }

        /// <summary>The background colour with the gradient layers over it (the first layer on top), at the given size.</summary>
        public static Bitmap Gradient (Size size, Color back, IReadOnlyList<ThemeCssGradient> layers)
        {
            using var bitmap = new SKBitmap (Math.Max (1, size.Width), Math.Max (1, size.Height), SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas (bitmap);
            canvas.Clear (new SKColor ((uint) back.ToArgb ()));

            for (var i = layers.Count - 1; i >= 0; i--) {
                using var layer = layers[i].Render (bitmap.Width, bitmap.Height);
                if (layer.Width > 0)
                    canvas.DrawBitmap (layer, 0, 0);
            }

            return ToBitmap (bitmap);
        }

        // Skia's Bgra8888 premultiplied pixels are System.Drawing's Format32bppPArgb, byte for byte.
        private static Bitmap ToBitmap (SKBitmap source)
        {
            var bitmap = new Bitmap (source.Width, source.Height, PixelFormat.Format32bppPArgb);
            var data = bitmap.LockBits (new Rectangle (0, 0, source.Width, source.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);

            try {
                var row = new byte[source.Width * 4];
                var pixels = source.GetPixels ();

                for (var y = 0; y < source.Height; y++) {
                    Marshal.Copy (pixels + y * source.RowBytes, row, 0, row.Length);
                    Marshal.Copy (row, 0, data.Scan0 + y * data.Stride, row.Length);
                }
            } finally {
                bitmap.UnlockBits (data);
            }

            return bitmap;
        }
    }
}
