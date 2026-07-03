using SkiaSharp;


namespace Majorsilence.Forms.Drawing
{
    public class Brush : IDisposable
    {
        protected SKPaint? Paint { get; set; }

        public Brush(Color color)
        {
            Paint = new SKPaint
            {
                Color = new SKColor((byte)color.R, (byte)color.G, (byte)color.B, (byte)color.A),
                Style = SKPaintStyle.Fill
            };
        }

        public virtual void Dispose()
        {
            Paint?.Dispose();
            Paint = null;
            GC.SuppressFinalize(this);
        }

        internal virtual SKPaint ToSkPaint()
        {
            return Paint!;
        }
    }
}
