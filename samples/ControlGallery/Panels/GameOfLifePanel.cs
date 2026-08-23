using System.Drawing;
using Majorsilence.Forms;
using Timer = Majorsilence.Forms.Timer;

namespace ControlGallery.Panels
{
    // Conway's Game of Life. Stresses Majorsilence.Forms.Drawing's Graphics.FillRectangle with a
    // large number of fill calls per frame, driven by a Timer, plus click/drag editing of the grid.
    public class GameOfLifePanel : BasePanel
    {
        private readonly Timer timer = new () { Interval = 120 };
        private readonly LifeCanvas canvas;
        private readonly Button startStopButton;
        private readonly Label generationLabel;

        public GameOfLifePanel ()
        {
            canvas = new LifeCanvas { Left = 10, Top = 50, Width = 860, Height = 550 };
            canvas.Style.Border.Width = 1;
            canvas.Randomize (0.25);

            generationLabel = new Label { Text = "Generation: 0", Left = 665, Top = 17, Width = 180 };

            startStopButton = Controls.Add (new Button { Text = "Start", Left = 10, Top = 10, Width = 90, Height = 30 });
            startStopButton.Click += (o, e) => {
                timer.Enabled = !timer.Enabled;
                startStopButton.Text = timer.Enabled ? "Stop" : "Start";
            };

            var stepButton = Controls.Add (new Button { Text = "Step", Left = 110, Top = 10, Width = 90, Height = 30 });
            stepButton.Click += (o, e) => Advance ();

            var randomizeButton = Controls.Add (new Button { Text = "Randomize", Left = 210, Top = 10, Width = 100, Height = 30 });
            randomizeButton.Click += (o, e) => {
                canvas.Randomize (0.25);
                generationLabel.Text = "Generation: 0";
            };

            var clearButton = Controls.Add (new Button { Text = "Clear", Left = 320, Top = 10, Width = 80, Height = 30 });
            clearButton.Click += (o, e) => {
                canvas.Clear ();
                generationLabel.Text = "Generation: 0";
            };

            Controls.Add (new Label { Text = "Speed:", Left = 415, Top = 17, Width = 55 });

            var speedTrack = Controls.Add (new TrackBar {
                Left = 470,
                Top = 10,
                Width = 180,
                Minimum = 1,
                Maximum = 20,
                Value = 10,
                TickFrequency = 5
            });
            speedTrack.ValueChanged += (o, e) => timer.Interval = Math.Max (20, 550 - (speedTrack.Value * 25));

            Controls.Add (generationLabel);

            Controls.Add (new Label {
                Text = "Click or drag on the grid to toggle cells.",
                Left = 10,
                Top = 610,
                Width = 400
            });

            timer.Tick += (o, e) => Advance ();

            Controls.Add (canvas);
        }

        private void Advance ()
        {
            canvas.Step ();
            generationLabel.Text = $"Generation: {canvas.Generation}";
        }

        public override void UnloadPanel ()
        {
            timer.Stop ();
            startStopButton.Text = "Start";
        }

        private sealed class LifeCanvas : Control
        {
            private const int CellSize = 10;
            private const int Cols = 86;
            private const int Rows = 55;

            private bool[,] cells = new bool[Cols, Rows];
            private bool isPainting;
            private bool paintValue;

            public int Generation { get; private set; }

            public LifeCanvas ()
            {
                Style.BackgroundColor = SkiaSharp.SKColors.Black;
            }

            public void Randomize (double density)
            {
                var rnd = Random.Shared;

                for (var x = 0; x < Cols; x++)
                    for (var y = 0; y < Rows; y++)
                        cells[x, y] = rnd.NextDouble () < density;

                Generation = 0;
                Invalidate ();
            }

            public void Clear ()
            {
                cells = new bool[Cols, Rows];
                Generation = 0;
                Invalidate ();
            }

            public void Step ()
            {
                var next = new bool[Cols, Rows];

                for (var x = 0; x < Cols; x++) {
                    for (var y = 0; y < Rows; y++) {
                        var neighbors = CountNeighbors (x, y);
                        var alive = cells[x, y];

                        next[x, y] = alive
                            ? neighbors == 2 || neighbors == 3
                            : neighbors == 3;
                    }
                }

                cells = next;
                Generation++;
                Invalidate ();
            }

            private int CountNeighbors (int x, int y)
            {
                var count = 0;

                for (var dx = -1; dx <= 1; dx++) {
                    for (var dy = -1; dy <= 1; dy++) {
                        if (dx == 0 && dy == 0)
                            continue;

                        // Toroidal wrap so patterns like gliders travel off one edge and back in the other.
                        var nx = (x + dx + Cols) % Cols;
                        var ny = (y + dy + Rows) % Rows;

                        if (cells[nx, ny])
                            count++;
                    }
                }

                return count;
            }

            protected override void OnMouseDown (MouseEventArgs e)
            {
                base.OnMouseDown (e);

                var (cx, cy) = ToCell (e.X, e.Y);

                if (cx < 0 || cy < 0)
                    return;

                isPainting = true;
                paintValue = !cells[cx, cy];
                cells[cx, cy] = paintValue;
                Capture = true;
                Invalidate ();
            }

            protected override void OnMouseMove (MouseEventArgs e)
            {
                base.OnMouseMove (e);

                if (!isPainting)
                    return;

                var (cx, cy) = ToCell (e.X, e.Y);

                if (cx < 0 || cy < 0)
                    return;

                if (cells[cx, cy] != paintValue) {
                    cells[cx, cy] = paintValue;
                    Invalidate ();
                }
            }

            protected override void OnMouseUp (MouseEventArgs e)
            {
                base.OnMouseUp (e);

                isPainting = false;
                Capture = false;
            }

            private static (int x, int y) ToCell (int px, int py)
            {
                var cx = px / CellSize;
                var cy = py / CellSize;

                if (cx < 0 || cx >= Cols || cy < 0 || cy >= Rows)
                    return (-1, -1);

                return (cx, cy);
            }

            protected override void OnPaint (PaintEventArgs e)
            {
                var g = e.Graphics;

                for (var x = 0; x < Cols; x++) {
                    for (var y = 0; y < Rows; y++) {
                        if (!cells[x, y])
                            continue;

                        g.FillRectangle (Brushes.LimeGreen, (x * CellSize) + 1, (y * CellSize) + 1, CellSize - 1, CellSize - 1);
                    }
                }
            }
        }
    }
}
