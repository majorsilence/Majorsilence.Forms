using System.Drawing;
using Majorsilence.Forms;
using Timer = Majorsilence.Forms.Timer;

namespace ControlGallery.Panels
{
    // "Matrix"-style binary code rain. Stresses Majorsilence.Forms.Drawing's Graphics.DrawString with
    // a high volume of small draw calls per frame, using per-glyph alpha blending for the fade trail.
    public class BinaryRainPanel : BasePanel
    {
        private readonly Timer timer = new () { Interval = 90 };
        private readonly RainCanvas canvas;
        private readonly Button startStopButton;

        public BinaryRainPanel ()
        {
            canvas = new RainCanvas { Left = 10, Top = 50, Width = 860, Height = 550 };
            canvas.Style.Border.Width = 1;

            startStopButton = Controls.Add (new Button { Text = "Stop", Left = 10, Top = 10, Width = 90, Height = 30 });
            startStopButton.Click += (o, e) => {
                timer.Enabled = !timer.Enabled;
                startStopButton.Text = timer.Enabled ? "Stop" : "Start";
            };

            Controls.Add (new Label { Text = "Speed:", Left = 115, Top = 17, Width = 55 });
            var speedTrack = Controls.Add (new TrackBar {
                Left = 170, Top = 10, Width = 200, Minimum = 1, Maximum = 20, Value = 12, TickFrequency = 5
            });
            speedTrack.ValueChanged += (o, e) => timer.Interval = Math.Max (20, 220 - (speedTrack.Value * 10));

            timer.Tick += (o, e) => canvas.Tick ();

            Controls.Add (canvas);
        }

        // Timers only run while this page is the one on screen -- not while merely cached or
        // pre-warmed in the background -- so opening the page (re)starts the rain.
        public override void LoadPanel ()
        {
            timer.Start ();
            startStopButton.Text = "Stop";
        }

        public override void UnloadPanel ()
        {
            timer.Stop ();
            startStopButton.Text = "Start";
        }

        private sealed class RainCanvas : Control
        {
            private const int CharWidth = 14;
            private const int CharHeight = 18;

            private readonly Majorsilence.Forms.Drawing.Font font = new (Majorsilence.Forms.Drawing.FontFamily.GenericMonospace, 13);
            private readonly SolidBrush brush = new (Color.Lime);

            private int cols;
            private int rows;
            private double[] headPosition = Array.Empty<double> ();
            private double[] speed = Array.Empty<double> ();
            private int[] trailLength = Array.Empty<int> ();
            private char[][] dropChars = Array.Empty<char[]> ();
            private readonly Random rnd = Random.Shared;

            public RainCanvas ()
            {
                Style.BackgroundColor = SkiaSharp.SKColors.Black;
            }

            // Width/Height are still default until the object initializer that constructs this
            // control finishes running, so the column grid is built lazily on first use instead.
            private void EnsureGrid ()
            {
                if (cols > 0)
                    return;

                cols = Math.Max (1, Width / CharWidth);
                rows = Math.Max (1, Height / CharHeight);

                headPosition = new double[cols];
                speed = new double[cols];
                trailLength = new int[cols];
                dropChars = new char[cols][];

                for (var c = 0; c < cols; c++)
                    ResetColumn (c, true);
            }

            private void ResetColumn (int c, bool initial)
            {
                trailLength[c] = rnd.Next (8, 24);
                speed[c] = 0.4 + (rnd.NextDouble () * 0.9);
                headPosition[c] = initial ? -rnd.Next (0, rows) : -rnd.Next (1, rows / 2 + 1);

                var chars = new char[trailLength[c]];

                for (var i = 0; i < chars.Length; i++)
                    chars[i] = rnd.Next (2) == 0 ? '0' : '1';

                dropChars[c] = chars;
            }

            public void Tick ()
            {
                EnsureGrid ();

                for (var c = 0; c < cols; c++) {
                    headPosition[c] += speed[c];

                    // Flicker the leading glyph each tick, like real matrix-rain digits.
                    dropChars[c][0] = rnd.Next (2) == 0 ? '0' : '1';

                    if (headPosition[c] - trailLength[c] > rows)
                        ResetColumn (c, false);
                }

                Invalidate ();
            }

            protected override void OnPaint (PaintEventArgs e)
            {
                EnsureGrid ();

                var g = e.Graphics;

                for (var c = 0; c < cols; c++) {
                    var head = (int)Math.Floor (headPosition[c]);
                    var chars = dropChars[c];
                    var len = trailLength[c];

                    for (var i = 0; i < len; i++) {
                        var row = head - i;

                        if (row < 0 || row >= rows)
                            continue;

                        var fraction = 1.0 - (i / (double)len);
                        var alpha = (byte)Math.Clamp (fraction * 255, 0, 255);

                        brush.Color = i == 0
                            ? Color.White
                            : Color.FromArgb (alpha, 40, 220, 90);

                        g.DrawString (chars[i].ToString (), font, brush, c * CharWidth, row * CharHeight);
                    }
                }
            }
        }
    }
}
