namespace Majorsilence.Forms.Drawing
{
    public sealed class SolidBrush : Brush
    {
        public Color Color { get; }

        public SolidBrush(Color color) : base(color)
        {
            Color = color;
        }
    }
}
