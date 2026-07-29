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

        /// <summary>Initializes a new TextureBrush using the specified image.</summary>
        public TextureBrush (Image image) : this (image, WrapMode.Tile) { }

        /// <summary>Initializes a new TextureBrush using the specified image and wrap mode.</summary>
        public TextureBrush (Image image, WrapMode wrapMode)
        {
            bitmap = image?.GetSKBitmap ();
            WrapMode = wrapMode;
        }

        /// <summary>Gets or sets the wrap mode that controls how the texture tiles.</summary>
        public WrapMode WrapMode { get; set; }

        internal override SKPaint CreatePaint ()
        {
            if (bitmap is null)
                return new SKPaint { Color = SKColors.Transparent, Style = SKPaintStyle.Fill };

            var tile = WrapMode switch {
                WrapMode.Clamp => SKShaderTileMode.Clamp,
                WrapMode.TileFlipX or WrapMode.TileFlipY or WrapMode.TileFlipXY => SKShaderTileMode.Mirror,
                _ => SKShaderTileMode.Repeat,
            };

            return new SKPaint {
                Shader = SKShader.CreateBitmap (bitmap, tile, tile),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
        }
    }
}
