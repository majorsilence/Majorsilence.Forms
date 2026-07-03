using SkiaSharp;


namespace Majorsilence.Forms.Drawing.Drawing2D
{
    public sealed class HatchBrush : Brush
    {
        public Color ForegroundColor { get; }
        public Color BackgroundColor { get; }

        public HatchBrush(HatchStyle hatchStyle, Color foreColor, Color backColor)
            : base(foreColor)
        {
            ForegroundColor = foreColor;
            BackgroundColor = backColor;

            Paint?.Dispose();
            Paint = CreateHatchPaint(hatchStyle, foreColor.ToSkColor(), backColor.ToSkColor());
        }

        internal override SKPaint ToSkPaint() => Paint!;

        private static SKPaint CreateHatchPaint(HatchStyle style, SKColor fore, SKColor back)
        {
            const int tileSize = 8;
            using var bmp = new SKBitmap(tileSize, tileSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(back);

            using var fgLine = new SKPaint
            {
                Color = fore,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = false
            };
            using var fgFill = new SKPaint
            {
                Color = fore,
                Style = SKPaintStyle.Fill,
                IsAntialias = false
            };

            switch (style)
            {
                case HatchStyle.Horizontal:
                    DrawHorizontalLines(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.LightHorizontal:
                    DrawHorizontalLines(canvas, fgLine, tileSize, 6);
                    break;

                case HatchStyle.NarrowHorizontal:
                    DrawHorizontalLines(canvas, fgLine, tileSize, 3);
                    break;

                case HatchStyle.DarkHorizontal:
                    DrawHorizontalLines(canvas, fgLine, tileSize, 2);
                    break;

                case HatchStyle.Vertical:
                    DrawVerticalLines(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.LightVertical:
                    DrawVerticalLines(canvas, fgLine, tileSize, 6);
                    break;

                case HatchStyle.NarrowVertical:
                    DrawVerticalLines(canvas, fgLine, tileSize, 3);
                    break;

                case HatchStyle.DarkVertical:
                    DrawVerticalLines(canvas, fgLine, tileSize, 2);
                    break;

                case HatchStyle.ForwardDiagonal:
                case HatchStyle.LightDownwardDiagonal:
                    DrawForwardDiagonals(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.DarkDownwardDiagonal:
                    DrawForwardDiagonals(canvas, fgLine, tileSize, 2);
                    break;

                case HatchStyle.WideDownwardDiagonal:
                    DrawForwardDiagonals(canvas, fgLine, tileSize, 6);
                    break;

                case HatchStyle.BackwardDiagonal:
                case HatchStyle.LightUpwardDiagonal:
                    DrawBackwardDiagonals(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.DarkUpwardDiagonal:
                    DrawBackwardDiagonals(canvas, fgLine, tileSize, 2);
                    break;

                case HatchStyle.WideUpwardDiagonal:
                    DrawBackwardDiagonals(canvas, fgLine, tileSize, 6);
                    break;

                case HatchStyle.Cross:
                case HatchStyle.SmallGrid:
                    DrawHorizontalLines(canvas, fgLine, tileSize, 4);
                    DrawVerticalLines(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.DiagonalCross:
                case HatchStyle.Trellis:
                    DrawForwardDiagonals(canvas, fgLine, tileSize, 4);
                    DrawBackwardDiagonals(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.Percent05:
                    DrawDotGrid(canvas, fgFill, tileSize, spacing: 4, dotSize: 1);
                    break;

                case HatchStyle.Percent10:
                    DrawDotGrid(canvas, fgFill, tileSize, spacing: 3, dotSize: 1);
                    break;

                case HatchStyle.Percent20:
                    DrawDotGrid(canvas, fgFill, tileSize, spacing: 2, dotSize: 1);
                    break;

                case HatchStyle.Percent25:
                    DrawCheckerboard(canvas, fgFill, tileSize, checkSize: 2, filled: false);
                    break;

                case HatchStyle.Percent30:
                {
                    DrawHorizontalLines(canvas, fgLine, tileSize, 4);
                    DrawForwardDiagonals(canvas, fgLine, tileSize, 4);
                    break;
                }

                case HatchStyle.Percent40:
                    DrawForwardDiagonals(canvas, fgLine, tileSize, 2);
                    DrawBackwardDiagonals(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.Percent50:
                case HatchStyle.SmallCheckerBoard:
                    DrawCheckerboard(canvas, fgFill, tileSize, checkSize: 1, filled: false);
                    break;

                case HatchStyle.Percent60:
                    DrawCheckerboard(canvas, fgFill, tileSize, checkSize: 1, filled: true);
                    DrawHorizontalLines(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.Percent70:
                    canvas.Clear(fore);
                    DrawDotGrid(canvas, new SKPaint { Color = back, Style = SKPaintStyle.Fill, IsAntialias = false },
                        tileSize, spacing: 3, dotSize: 1);
                    break;

                case HatchStyle.Percent75:
                    canvas.Clear(fore);
                    DrawDotGrid(canvas, new SKPaint { Color = back, Style = SKPaintStyle.Fill, IsAntialias = false },
                        tileSize, spacing: 2, dotSize: 1);
                    break;

                case HatchStyle.Percent80:
                    canvas.Clear(fore);
                    DrawCheckerboard(canvas, new SKPaint { Color = back, Style = SKPaintStyle.Fill, IsAntialias = false },
                        tileSize, checkSize: 2, filled: false);
                    break;

                case HatchStyle.Percent90:
                    canvas.Clear(fore);
                    DrawDotGrid(canvas, new SKPaint { Color = back, Style = SKPaintStyle.Fill, IsAntialias = false },
                        tileSize, spacing: 4, dotSize: 1);
                    break;

                case HatchStyle.LargeCheckerBoard:
                    DrawCheckerboard(canvas, fgFill, tileSize, checkSize: 4, filled: false);
                    break;

                case HatchStyle.OutlinedDiamond:
                    DrawDiamond(canvas, fgLine, tileSize, filled: false);
                    break;

                case HatchStyle.SolidDiamond:
                    DrawDiamond(canvas, fgFill, tileSize, filled: true);
                    break;

                case HatchStyle.DottedGrid:
                    DrawDotGrid(canvas, fgFill, tileSize, spacing: 4, dotSize: 1);
                    DrawVerticalLines(canvas, fgLine, tileSize, 4);
                    DrawHorizontalLines(canvas, fgLine, tileSize, 4);
                    break;

                case HatchStyle.ZigZag:
                    DrawZigZag(canvas, fgLine, tileSize);
                    break;

                case HatchStyle.Wave:
                    DrawWave(canvas, fgLine, tileSize);
                    break;

                case HatchStyle.DashedHorizontal:
                    DrawDashedHorizontalLines(canvas, fgLine, tileSize);
                    break;

                case HatchStyle.DashedVertical:
                    DrawDashedVerticalLines(canvas, fgLine, tileSize);
                    break;

                case HatchStyle.DashedDownwardDiagonal:
                    DrawDashedForwardDiagonals(canvas, fgLine, tileSize);
                    break;

                case HatchStyle.DashedUpwardDiagonal:
                    DrawDashedBackwardDiagonals(canvas, fgLine, tileSize);
                    break;

                default:
                    DrawHorizontalLines(canvas, fgLine, tileSize, 4);
                    break;
            }

            using var image = SKImage.FromBitmap(bmp);
            var shader = SKShader.CreateImage(image, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            return new SKPaint { Style = SKPaintStyle.Fill, Shader = shader };
        }

        private static void DrawHorizontalLines(SKCanvas c, SKPaint p, int size, int spacing)
        {
            for (int y = 0; y < size; y += spacing)
                c.DrawLine(0, y, size, y, p);
        }

        private static void DrawVerticalLines(SKCanvas c, SKPaint p, int size, int spacing)
        {
            for (int x = 0; x < size; x += spacing)
                c.DrawLine(x, 0, x, size, p);
        }

        private static void DrawForwardDiagonals(SKCanvas c, SKPaint p, int size, int spacing)
        {
            for (int i = -size; i < size * 2; i += spacing)
                c.DrawLine(i, 0, i + size, size, p);
        }

        private static void DrawBackwardDiagonals(SKCanvas c, SKPaint p, int size, int spacing)
        {
            for (int i = -size; i < size * 2; i += spacing)
                c.DrawLine(i + size, 0, i, size, p);
        }

        private static void DrawDotGrid(SKCanvas c, SKPaint p, int size, int spacing, int dotSize)
        {
            using var rect = new SKRoundRect();
            for (int y = 0; y < size; y += spacing)
                for (int x = 0; x < size; x += spacing)
                    c.DrawRect(x, y, dotSize, dotSize, p);
        }

        private static void DrawCheckerboard(SKCanvas c, SKPaint p, int size, int checkSize, bool filled)
        {
            for (int y = 0; y < size; y += checkSize)
                for (int x = 0; x < size; x += checkSize)
                    if (((x / checkSize) + (y / checkSize)) % 2 == (filled ? 1 : 0))
                        c.DrawRect(x, y, checkSize, checkSize, p);
        }

        private static void DrawDiamond(SKCanvas c, SKPaint p, int size, bool filled)
        {
            int cx = size / 2, cy = size / 2;
            int r = size / 2 - 1;
            using var path = new SKPath();
            path.MoveTo(cx, cy - r);
            path.LineTo(cx + r, cy);
            path.LineTo(cx, cy + r);
            path.LineTo(cx - r, cy);
            path.Close();
            c.DrawPath(path, p);
        }

        private static void DrawZigZag(SKCanvas c, SKPaint p, int size)
        {
            int step = size / 2;
            for (int y = 0; y < size; y += step)
            {
                for (int x = 0; x < size; x += step)
                {
                    c.DrawLine(x, y + step / 2, x + step / 2, y, p);
                    c.DrawLine(x + step / 2, y, x + step, y + step / 2, p);
                }
            }
        }

        private static void DrawWave(SKCanvas c, SKPaint p, int size)
        {
            int step = size / 2;
            for (int y = 0; y < size; y += step)
            {
                using var path = new SKPath();
                path.MoveTo(0, y + step / 2);
                for (int x = 0; x < size; x += step)
                {
                    path.CubicTo(x + step / 4, y, x + 3 * step / 4, y + step, x + step, y + step / 2);
                }
                c.DrawPath(path, p);
            }
        }

        private static void DrawDashedHorizontalLines(SKCanvas c, SKPaint p, int size)
        {
            for (int y = 0; y < size; y += 4)
                for (int x = 0; x < size; x += 4)
                    c.DrawLine(x, y, x + 2, y, p);
        }

        private static void DrawDashedVerticalLines(SKCanvas c, SKPaint p, int size)
        {
            for (int x = 0; x < size; x += 4)
                for (int y = 0; y < size; y += 4)
                    c.DrawLine(x, y, x, y + 2, p);
        }

        private static void DrawDashedForwardDiagonals(SKCanvas c, SKPaint p, int size)
        {
            for (int i = -size; i < size * 2; i += 4)
                c.DrawLine(i, 0, i + 2, 2, p);
        }

        private static void DrawDashedBackwardDiagonals(SKCanvas c, SKPaint p, int size)
        {
            for (int i = -size; i < size * 2; i += 4)
                c.DrawLine(i + size, 0, i + size - 2, 2, p);
        }
    }
}
