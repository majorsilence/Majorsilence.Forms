using System.Drawing;
using System.Runtime.InteropServices;
using Majorsilence.Forms;
using Majorsilence.Forms.Drawing.Imaging;

namespace ControlGallery.Panels
{
    // A small fractal explorer covering three classic computer-science fractals: an escape-time
    // Mandelbrot set (stresses Bitmap.LockBits + Graphics.DrawImage with a large per-pixel render),
    // a recursively subdivided Sierpinski triangle, and a recursively subdivided Koch snowflake
    // (both stress Graphics.FillPolygon/DrawLine with a deep recursive call count).
    public class FractalsPanel : BasePanel
    {
        private readonly FractalCanvas canvas;
        private readonly ComboBox algorithmCombo;
        private readonly TrackBar depthTrack;
        private readonly Label depthLabel;
        private readonly Label hintLabel;

        public FractalsPanel ()
        {
            canvas = new FractalCanvas { Left = 10, Top = 90, Width = 860, Height = 550 };
            canvas.Style.Border.Width = 1;

            Controls.Add (new Label { Text = "Fractal:", Left = 10, Top = 17, Width = 55 });
            algorithmCombo = Controls.Add (new ComboBox { Left = 70, Top = 10, Width = 190 });
            algorithmCombo.Items.AddRange (new[] { "Mandelbrot Set", "Sierpinski Triangle", "Koch Snowflake" });

            Controls.Add (new Label { Text = "Detail:", Left = 280, Top = 17, Width = 50 });
            depthTrack = Controls.Add (new TrackBar { Left = 335, Top = 10, Width = 200, TickFrequency = 1 });
            depthLabel = Controls.Add (new Label { Text = "", Left = 545, Top = 17, Width = 40 });

            var resetButton = Controls.Add (new Button { Text = "Reset View", Left = 595, Top = 10, Width = 110, Height = 30 });
            resetButton.Click += (o, e) => {
                canvas.ResetView ();
                canvas.Invalidate ();
            };

            hintLabel = Controls.Add (new Label { Text = "", Left = 10, Top = 55, Width = 700 });

            algorithmCombo.SelectedIndexChanged += (o, e) => {
                var kind = (FractalKind)algorithmCombo.SelectedIndex;
                canvas.Algorithm = kind;
                canvas.ResetView ();
                ConfigureDepthRangeFor (kind);
                hintLabel.Text = HintFor (kind);
                canvas.Invalidate ();
            };

            depthTrack.ValueChanged += (o, e) => {
                canvas.Depth = depthTrack.Value;
                depthLabel.Text = DepthDisplayText (canvas.Algorithm, depthTrack.Value);
                canvas.Invalidate ();
            };

            algorithmCombo.SelectedIndex = 0;

            Controls.Add (canvas);
        }

        private void ConfigureDepthRangeFor (FractalKind kind)
        {
            var (min, max, def) = kind switch {
                FractalKind.Mandelbrot => (1, 12, 6),
                FractalKind.Sierpinski => (1, 8, 5),
                _ => (0, 6, 4),
            };

            depthTrack.Minimum = min;
            depthTrack.Maximum = max;
            depthTrack.Value = def;
            canvas.Depth = def;
            depthLabel.Text = DepthDisplayText (kind, def);
        }

        private static string DepthDisplayText (FractalKind kind, int value) =>
            kind == FractalKind.Mandelbrot ? (value * 30).ToString () : value.ToString ();

        private static string HintFor (FractalKind kind) => kind switch {
            FractalKind.Mandelbrot => "Left-click to zoom in, right-click to zoom out. Detail sets the max iteration count.",
            FractalKind.Sierpinski => "Recursive triangle subdivision. Detail sets the recursion depth.",
            _ => "Recursive edge subdivision of a triangle's outline. Detail sets the recursion depth.",
        };

        public enum FractalKind { Mandelbrot, Sierpinski, Koch }

        private sealed class FractalCanvas : Control
        {
            private double centerX = -0.5;
            private double centerY;
            private double viewWidth = 3.5;

            public FractalKind Algorithm { get; set; } = FractalKind.Mandelbrot;
            public int Depth { get; set; } = 6;

            public FractalCanvas ()
            {
                Style.BackgroundColor = SkiaSharp.SKColors.Black;
            }

            public void ResetView ()
            {
                centerX = -0.5;
                centerY = 0;
                viewWidth = 3.5;
            }

            protected override void OnMouseDown (MouseEventArgs e)
            {
                base.OnMouseDown (e);

                if (Algorithm != FractalKind.Mandelbrot)
                    return;

                var clickX = centerX + ((e.X - (Width / 2.0)) / Width * viewWidth);
                var clickY = centerY + ((e.Y - (Height / 2.0)) / Width * viewWidth);

                centerX = clickX;
                centerY = clickY;
                viewWidth *= e.Button == MouseButtons.Right ? 2.0 : 0.5;

                Invalidate ();
            }

            protected override void OnPaint (PaintEventArgs e)
            {
                switch (Algorithm) {
                    case FractalKind.Mandelbrot:
                        RenderMandelbrot (e.Graphics);
                        break;
                    case FractalKind.Sierpinski:
                        RenderSierpinski (e.Graphics);
                        break;
                    case FractalKind.Koch:
                        RenderKoch (e.Graphics);
                        break;
                }
            }

            private void RenderMandelbrot (Majorsilence.Forms.Drawing.Graphics g)
            {
                var maxIter = Depth * 30;
                using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (Width, Height);

                // A benchmark (benchmarks/Majorsilence.Forms.Benchmarks/BitmapFillBenchmarks.cs)
                // measured this bulk LockBits write at roughly 20x Bitmap.SetPixel's per-pixel cost
                // for a full-canvas fill (and a tenth of the allocation) -- SetPixel re-validates
                // the surface on every call, which is exactly what made this panel's render take
                // 1-2 seconds per click before this change.
                var data = bitmap.LockBits (new Rectangle (0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                var buffer = new byte[data.Stride * Height];

                for (var py = 0; py < Height; py++) {
                    var y0 = centerY + ((py - (Height / 2.0)) / Width * viewWidth);
                    var row = py * data.Stride;

                    for (var px = 0; px < Width; px++) {
                        var x0 = centerX + ((px - (Width / 2.0)) / Width * viewWidth);

                        double x = 0, y = 0;
                        var iter = 0;

                        while (((x * x) + (y * y) <= 4) && iter < maxIter) {
                            var xt = (x * x) - (y * y) + x0;
                            y = (2 * x * y) + y0;
                            x = xt;
                            iter++;
                        }

                        var color = iter == maxIter ? Color.Black : IterationColor (iter, maxIter);
                        var i = row + (px * 4);
                        buffer[i + 0] = color.B;
                        buffer[i + 1] = color.G;
                        buffer[i + 2] = color.R;
                        buffer[i + 3] = color.A;
                    }
                }

                Marshal.Copy (buffer, 0, data.Scan0, buffer.Length);
                bitmap.UnlockBits (data);

                g.DrawImage (bitmap, 0, 0);
            }

            private static Color IterationColor (int iter, int maxIter)
            {
                var hue = 360.0 * iter / Math.Max (1, maxIter) * 4; // a few extra hue cycles for banding
                return HsvToRgb (hue, 0.85, 1.0);
            }

            private static Color HsvToRgb (double h, double s, double v)
            {
                h = ((h % 360) + 360) % 360;

                var c = v * s;
                var x = c * (1 - Math.Abs (((h / 60.0) % 2) - 1));
                var m = v - c;

                var (r, g, b) = h switch {
                    < 60 => (c, x, 0.0),
                    < 120 => (x, c, 0.0),
                    < 180 => (0.0, c, x),
                    < 240 => (0.0, x, c),
                    < 300 => (x, 0.0, c),
                    _ => (c, 0.0, x),
                };

                return Color.FromArgb ((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
            }

            private void RenderSierpinski (Majorsilence.Forms.Drawing.Graphics g)
            {
                var top = new PointF (Width / 2f, 20);
                var left = new PointF (40, Height - 20);
                var right = new PointF (Width - 40, Height - 20);

                SubdivideSierpinski (g, top, left, right, Depth);
            }

            private void SubdivideSierpinski (Majorsilence.Forms.Drawing.Graphics g, PointF a, PointF b, PointF c, int depth)
            {
                if (depth <= 0) {
                    var t = Depth <= 0 ? 0 : 1 - (depth / (double)Depth);
                    var color = LerpColor (Color.FromArgb (30, 140, 90), Color.FromArgb (140, 255, 210), t);

                    using var brush = new SolidBrush (color);
                    g.FillPolygon (brush, new[] { a, b, c });
                    return;
                }

                var ab = Mid (a, b);
                var bc = Mid (b, c);
                var ca = Mid (c, a);

                SubdivideSierpinski (g, a, ab, ca, depth - 1);
                SubdivideSierpinski (g, ab, b, bc, depth - 1);
                SubdivideSierpinski (g, ca, bc, c, depth - 1);
            }

            private void RenderKoch (Majorsilence.Forms.Drawing.Graphics g)
            {
                var cx = Width / 2f;
                var cy = (Height / 2f) + 40;
                var r = Math.Min (Width, Height) * 0.38f;

                PointF Point (double angleDegrees)
                {
                    var rad = angleDegrees * Math.PI / 180.0;
                    return new PointF (cx + (float)(r * Math.Cos (rad)), cy + (float)(r * Math.Sin (rad)));
                }

                var p0 = Point (-90);
                var p1 = Point (150);
                var p2 = Point (30);

                using var pen = new Pen (Color.FromArgb (120, 255, 190), 1.5f);

                DrawKochEdge (g, pen, p0, p1, Depth);
                DrawKochEdge (g, pen, p1, p2, Depth);
                DrawKochEdge (g, pen, p2, p0, Depth);
            }

            private static void DrawKochEdge (Majorsilence.Forms.Drawing.Graphics g, Pen pen, PointF a, PointF b, int depth)
            {
                if (depth <= 0) {
                    g.DrawLine (pen, a, b);
                    return;
                }

                var dx = (b.X - a.X) / 3;
                var dy = (b.Y - a.Y) / 3;

                var p1 = new PointF (a.X + dx, a.Y + dy);
                var p3 = new PointF (a.X + (2 * dx), a.Y + (2 * dy));

                // Rotate the middle third by 60 degrees to bulge the bump outward.
                const double angle = Math.PI / 3;
                var rx = (dx * Math.Cos (angle)) - (dy * Math.Sin (angle));
                var ry = (dx * Math.Sin (angle)) + (dy * Math.Cos (angle));
                var peak = new PointF (p1.X + (float)rx, p1.Y + (float)ry);

                DrawKochEdge (g, pen, a, p1, depth - 1);
                DrawKochEdge (g, pen, p1, peak, depth - 1);
                DrawKochEdge (g, pen, peak, p3, depth - 1);
                DrawKochEdge (g, pen, p3, b, depth - 1);
            }

            private static PointF Mid (PointF a, PointF b) => new ((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

            private static Color LerpColor (Color a, Color b, double t)
            {
                t = Math.Clamp (t, 0, 1);

                return Color.FromArgb (
                    (int)(a.R + ((b.R - a.R) * t)),
                    (int)(a.G + ((b.G - a.G) * t)),
                    (int)(a.B + ((b.B - a.B) * t)));
            }
        }
    }
}
