using System.Drawing;
using Majorsilence.Forms;
using Timer = Majorsilence.Forms.Timer;

namespace ControlGallery.Panels
{
    // A step-through sorting algorithm visualizer. Stresses Majorsilence.Forms.Drawing's
    // Graphics.FillRectangle with per-bar highlight coloring, redrawn on every algorithm step.
    public class SortingVisualizerPanel : BasePanel
    {
        private readonly Timer timer = new () { Interval = 40 };
        private readonly SortCanvas canvas;
        private readonly ComboBox algorithmCombo;
        private readonly Button startPauseButton;
        private readonly Label statusLabel;
        private IEnumerator<(int a, int b, StepKind kind)>? steps;
        private int comparisons;
        private int swaps;

        public SortingVisualizerPanel ()
        {
            canvas = new SortCanvas { Left = 10, Top = 90, Width = 860, Height = 400 };
            canvas.Style.Border.Width = 1;
            canvas.Shuffle ();

            Controls.Add (new Label { Text = "Algorithm:", Left = 10, Top = 17, Width = 75 });
            algorithmCombo = Controls.Add (new ComboBox { Left = 90, Top = 10, Width = 180 });
            algorithmCombo.Items.AddRange (new[] { "Bubble Sort", "Selection Sort", "Insertion Sort", "Quick Sort" });
            algorithmCombo.SelectedIndex = 0;
            algorithmCombo.SelectedIndexChanged += (o, e) => ResetRun ();

            startPauseButton = Controls.Add (new Button { Text = "Start", Left = 285, Top = 10, Width = 90, Height = 30 });
            startPauseButton.Click += (o, e) => ToggleRun ();

            var shuffleButton = Controls.Add (new Button { Text = "Shuffle", Left = 385, Top = 10, Width = 90, Height = 30 });
            shuffleButton.Click += (o, e) => {
                ResetRun ();
                canvas.Shuffle ();
            };

            Controls.Add (new Label { Text = "Speed:", Left = 490, Top = 17, Width = 55 });
            var speedTrack = Controls.Add (new TrackBar {
                Left = 545, Top = 10, Width = 180, Minimum = 1, Maximum = 20, Value = 12, TickFrequency = 5
            });
            speedTrack.ValueChanged += (o, e) => timer.Interval = Math.Max (5, 220 - (speedTrack.Value * 10));

            statusLabel = Controls.Add (new Label { Text = "Comparisons: 0   Swaps: 0", Left = 10, Top = 55, Width = 400 });

            timer.Tick += (o, e) => Advance ();

            Controls.Add (canvas);
        }

        private void ToggleRun ()
        {
            if (timer.Enabled) {
                timer.Stop ();
                startPauseButton.Text = "Resume";
                return;
            }

            steps ??= GetAlgorithm ().GetEnumerator ();

            timer.Start ();
            startPauseButton.Text = "Pause";
        }

        private void Advance ()
        {
            if (steps is null || !steps.MoveNext ()) {
                timer.Stop ();
                steps = null;
                startPauseButton.Text = "Start";
                canvas.MarkSorted ();
                statusLabel.Text = $"Sorted!   Comparisons: {comparisons}   Swaps: {swaps}";
                return;
            }

            var (a, b, kind) = steps.Current;

            if (kind == StepKind.Compare)
                comparisons++;
            else
                swaps++;

            canvas.Highlight (a, b, kind);
            statusLabel.Text = $"Comparisons: {comparisons}   Swaps: {swaps}";
        }

        private void ResetRun ()
        {
            timer.Stop ();
            steps = null;
            comparisons = 0;
            swaps = 0;
            startPauseButton.Text = "Start";
            canvas.ClearHighlight ();
            statusLabel.Text = "Comparisons: 0   Swaps: 0";
        }

        private IEnumerable<(int, int, StepKind)> GetAlgorithm () => (algorithmCombo.SelectedItem as string) switch {
            "Selection Sort" => SelectionSort (canvas.Values),
            "Insertion Sort" => InsertionSort (canvas.Values),
            "Quick Sort" => QuickSort (canvas.Values, 0, canvas.Values.Length - 1),
            _ => BubbleSort (canvas.Values),
        };

        public override void UnloadPanel ()
        {
            timer.Stop ();
            steps = null;
            startPauseButton.Text = "Start";
        }

        public enum StepKind { Compare, Swap }

        private static void Swap (int[] a, int i, int j) => (a[i], a[j]) = (a[j], a[i]);

        private static IEnumerable<(int, int, StepKind)> BubbleSort (int[] a)
        {
            for (var i = 0; i < a.Length - 1; i++) {
                for (var j = 0; j < a.Length - i - 1; j++) {
                    yield return (j, j + 1, StepKind.Compare);

                    if (a[j] > a[j + 1]) {
                        Swap (a, j, j + 1);
                        yield return (j, j + 1, StepKind.Swap);
                    }
                }
            }
        }

        private static IEnumerable<(int, int, StepKind)> SelectionSort (int[] a)
        {
            for (var i = 0; i < a.Length - 1; i++) {
                var min = i;

                for (var j = i + 1; j < a.Length; j++) {
                    yield return (min, j, StepKind.Compare);

                    if (a[j] < a[min])
                        min = j;
                }

                if (min != i) {
                    Swap (a, i, min);
                    yield return (i, min, StepKind.Swap);
                }
            }
        }

        private static IEnumerable<(int, int, StepKind)> InsertionSort (int[] a)
        {
            for (var i = 1; i < a.Length; i++) {
                var j = i;

                while (j > 0) {
                    yield return (j - 1, j, StepKind.Compare);

                    if (a[j - 1] > a[j]) {
                        Swap (a, j - 1, j);
                        yield return (j - 1, j, StepKind.Swap);
                        j--;
                    } else {
                        break;
                    }
                }
            }
        }

        private static IEnumerable<(int, int, StepKind)> QuickSort (int[] a, int lo, int hi)
        {
            if (lo >= hi)
                yield break;

            var pivot = a[hi];
            var i = lo - 1;

            for (var j = lo; j < hi; j++) {
                yield return (j, hi, StepKind.Compare);

                if (a[j] < pivot) {
                    i++;

                    if (i != j) {
                        Swap (a, i, j);
                        yield return (i, j, StepKind.Swap);
                    }
                }
            }

            if (i + 1 != hi) {
                Swap (a, i + 1, hi);
                yield return (i + 1, hi, StepKind.Swap);
            }

            var p = i + 1;

            foreach (var step in QuickSort (a, lo, p - 1))
                yield return step;

            foreach (var step in QuickSort (a, p + 1, hi))
                yield return step;
        }

        private sealed class SortCanvas : Control
        {
            private const int Count = 64;
            private int highlightA = -1;
            private int highlightB = -1;
            private StepKind highlightKind;
            private bool sorted;

            public int[] Values { get; private set; } = new int[Count];

            public SortCanvas ()
            {
                Style.BackgroundColor = SkiaSharp.SKColors.White;
            }

            public void Shuffle ()
            {
                var rnd = Random.Shared;
                var values = Enumerable.Range (1, Count).ToArray ();

                for (var i = values.Length - 1; i > 0; i--) {
                    var j = rnd.Next (i + 1);
                    (values[i], values[j]) = (values[j], values[i]);
                }

                Values = values;
                sorted = false;
                ClearHighlight ();
            }

            public void Highlight (int a, int b, StepKind kind)
            {
                highlightA = a;
                highlightB = b;
                highlightKind = kind;
                Invalidate ();
            }

            public void ClearHighlight ()
            {
                highlightA = -1;
                highlightB = -1;
                Invalidate ();
            }

            public void MarkSorted ()
            {
                sorted = true;
                ClearHighlight ();
            }

            protected override void OnPaint (PaintEventArgs e)
            {
                var g = e.Graphics;
                var barWidth = Width / (float)Values.Length;
                var max = Values.Length;

                for (var i = 0; i < Values.Length; i++) {
                    var barHeight = (Values[i] / (float)max) * (Height - 10);
                    var x = i * barWidth;
                    var y = Height - barHeight;

                    var brush = sorted
                        ? Brushes.MediumSeaGreen
                        : (i == highlightA || i == highlightB)
                            ? (highlightKind == StepKind.Swap ? Brushes.Crimson : Brushes.Gold)
                            : Brushes.SteelBlue;

                    g.FillRectangle (brush, x, y, Math.Max (1, barWidth - 1), barHeight);
                }
            }
        }
    }
}
