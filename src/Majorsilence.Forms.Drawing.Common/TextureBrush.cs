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
        private Matrix transform = new ();

        /// <summary>Initializes a new TextureBrush using the specified image.</summary>
        public TextureBrush (Image image) : this (image, WrapMode.Tile) { }

        /// <summary>Initializes a new TextureBrush using the specified image and wrap mode.</summary>
        public TextureBrush (Image image, WrapMode wrapMode)
        {
            Image = image;
            bitmap = image?.GetSKBitmap ();
            WrapMode = wrapMode;
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
            get => transform.Clone ();
            set => transform = value?.Clone () ?? new Matrix ();
        }

        /// <summary>Resets the texture transform to the identity.</summary>
        public void ResetTransform () => transform = new Matrix ();

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
        public TextureBrush Clone ()
        {
            var clone = new TextureBrush (Image!, WrapMode);
            clone.transform = transform.Clone ();
            return clone;
        }

        internal override SKPaint CreatePaint ()
        {
            if (bitmap is null)
                return new SKPaint { Color = SKColors.Transparent, Style = SKPaintStyle.Fill };

            var tile = WrapMode switch {
                WrapMode.Clamp => SKShaderTileMode.Clamp,
                WrapMode.TileFlipX or WrapMode.TileFlipY or WrapMode.TileFlipXY => SKShaderTileMode.Mirror,
                _ => SKShaderTileMode.Repeat,
            };

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
