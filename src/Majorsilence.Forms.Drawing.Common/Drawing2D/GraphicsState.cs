using SkiaSharp;


namespace Majorsilence.Drawing.Drawing2D
{
    public class GraphicsState
    {
        internal int SaveCount { get; }

        internal GraphicsState(SKCanvas canvas)
        {
            SaveCount = canvas.SaveCount;
        }
    }
}
