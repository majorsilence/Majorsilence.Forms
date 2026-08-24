using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// A brush that fills the interior of a shape with a tiled image. Cross-platform replacement for
    /// System.Drawing.TextureBrush, backed by an <see cref="SKShader"/> created from the image bitmap.
    /// </summary>
    public sealed class TextureBrush : Brush
    {
        private readonly SKBitmap? bitmap;
        private readonly BrushTransform transform = new ();

        /// <summary>Initializes a new TextureBrush using the specified image.</summary>
        public TextureBrush (Image image) : this (image, WrapMode.Tile) { }

        /// <summary>Initializes a new TextureBrush using the specified image and wrap mode.</summary>
        public TextureBrush (Image image, WrapMode wrapMode)
        {
            Image = image;
            bitmap = image?.GetSKBitmap ();
            WrapMode = wrapMode;
        }

        /// <summary>Initializes a new TextureBrush using the specified image, cropped to <paramref name="dstRect"/>.</summary>
        public TextureBrush (Image image, Rectangle dstRect) : this (image, dstRect, WrapMode.Tile) { }

        /// <summary>
        /// Initializes a new TextureBrush using the specified image, cropped to <paramref name="dstRect"/>,
        /// with the given wrap mode.
        /// </summary>
        public TextureBrush (Image image, Rectangle dstRect, WrapMode wrapMode)
        {
            Image = image;
            WrapMode = wrapMode;

            var full = image?.GetSKBitmap ();
            bitmap = full is null ? null : Crop (full, dstRect);
        }

        // GDI+ clamps the requested rectangle to the image bounds rather than throwing.
        private static SKBitmap Crop (SKBitmap source, Rectangle rect)
        {
#if NETSTANDARD2_0
            var x = Math.Max(0, Math.Min(rect.X, source.Width));
            var y = Math.Max(0, Math.Min(rect.Y, source.Height));
            var width = Math.Max(0, Math.Min(rect.Width, source.Width - x));
            var height = Math.Max(0, Math.Min(rect.Height, source.Height - y));
#else
            var x = Math.Clamp(rect.X, 0, source.Width);
            var y = Math.Clamp (rect.Y, 0, source.Height);
            var width = Math.Clamp (rect.Width, 0, source.Width - x);
            var height = Math.Clamp (rect.Height, 0, source.Height - y);
#endif
            if (x == 0 && y == 0 && width == source.Width && height == source.Height)
                return source;

            var cropped = new SKBitmap (System.Math.Max (width, 1), System.Math.Max (height, 1));
            using var canvas = new SKCanvas (cropped);
            canvas.DrawBitmap (source, new SKRect (x, y, x + width, y + height), new SKRect (0, 0, width, height));
            return cropped;
        }

        /// <summary>Gets the image this brush uses to fill shapes.</summary>
        public Image? Image { get; }

        /// <summary>Gets or sets the wrap mode that controls how the texture tiles.</summary>
        public WrapMode WrapMode { get; set; }

        /// <summary>
        /// Gets or sets a copy of the geometric transform applied to the texture before it is tiled.
        /// Assigning null resets it to the identity, matching <see cref="ResetTransform"/>.
        /// </summary>
        public Matrix Transform {
            get => transform.Get ();
            set => transform.Set (value);
        }

        /// <summary>Resets the texture transform to the identity.</summary>
        public void ResetTransform () => transform.Reset ();

        /// <summary>Multiplies the texture transform by <paramref name="matrix"/>.</summary>
        public void MultiplyTransform (Matrix matrix, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Multiply (matrix, order);

        /// <summary>Translates the texture transform by the specified offsets.</summary>
        public void TranslateTransform (float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Translate (dx, dy, order);

        /// <summary>Scales the texture transform by the specified factors.</summary>
        public void ScaleTransform (float sx, float sy, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Scale (sx, sy, order);

        /// <summary>Rotates the texture transform by the specified angle, in degrees.</summary>
        public void RotateTransform (float angle, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Rotate (angle, order);

        /// <summary>Creates an exact copy of this brush, including its texture transform.</summary>
#if NETSTANDARD2_0
        public override object Clone ()
#else
        public override TextureBrush Clone ()
#endif
        {
            var clone = new TextureBrush (Image!, WrapMode);
            clone.transform.Set (transform.Get ());
            return clone;
        }

        internal override SKPaint CreatePaint ()
        {
            if (bitmap is null)
                return new SKPaint { Color = SKColors.Transparent, Style = SKPaintStyle.Fill };

            var tile = WrapMode.ToSKTileMode ();

            // The texture transform is a *local* matrix on the shader: it moves the texture within the
            // filled shape, rather than transforming the shape itself.
            var shader = SKShader.CreateBitmap (bitmap, tile, tile, transform.ToSKMatrix ());

            return new SKPaint {
                Shader = shader,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
        }
    }
}
