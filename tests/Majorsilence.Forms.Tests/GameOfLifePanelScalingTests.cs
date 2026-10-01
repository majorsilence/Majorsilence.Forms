using System.Linq;
using System.Reflection;
using ControlGallery.Panels;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Issue #291: a custom OnPaint draws in device pixels, not the logical units everything else in
    // the framework uses, unless it scales itself -- and samples/ControlGallery/Panels/GameOfLifePanel.cs,
    // the repo's own canonical custom-painting sample, did not, so its cells rendered at a fraction of
    // their intended size on any scaled display (about a third, at Android's ~2.75x).
    //
    // PaintSurface mirrors how Control.PaintChildren really paints a control: a back buffer sized for
    // the control at the given scale, an untransformed canvas, OnPaint told the scale through
    // PaintEventArgs.Scaling. It touches no window and no backend, so (like the other PaintSurface-based
    // tests in this file's neighbourhood, e.g. ChildPaintParityTests) this needs no [Collection ("Headless")].
    public sealed class GameOfLifePanelScalingTests
    {
        // LimeGreen (the live-cell colour) against a black background: the green channel is well clear
        // of red and blue either way, so this does not depend on knowing the background pixel's exact
        // value.
        private static bool IsLiveCellPixel (SKColor pixel) => pixel.Green > pixel.Red + 50 && pixel.Green > pixel.Blue + 50;

        // LifeCanvas is private to GameOfLifePanel -- nothing outside the sample needs it -- so the
        // instance is reached the way the panel itself wires it up: through its own Controls
        // collection, the panel's constructor having already given it its real Width/Height/Style.
        private static (Control Canvas, int CellSize) SingleLiveCell (int cellX, int cellY)
        {
            var panel = new GameOfLifePanel ();
            var canvas = panel.Controls.Single (c => c.GetType ().Name == "LifeCanvas");
            var canvasType = canvas.GetType ();

            var cellSize = (int)canvasType
                .GetField ("CellSize", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue (null)!;

            // The constructor already ran Randomize (0.25); replace that board with one deterministic
            // live cell so there is exactly one thing to measure.
            var cellsField = canvasType.GetField ("cells", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var cells = (bool[,])cellsField.GetValue (canvas)!;

            for (var x = 0; x < cells.GetLength (0); x++)
                for (var y = 0; y < cells.GetLength (1); y++)
                    cells[x, y] = false;

            cells[cellX, cellY] = true;

            return (canvas, cellSize);
        }

        [Fact]
        public void Cell_paints_at_its_scaled_device_position ()
        {
            var (canvas, cellSize) = SingleLiveCell (3, 3);

            // The live cell's logical fill rectangle is [(3*cellSize)+1, (3*cellSize)+1+cellSize-1) on
            // each axis; this point sits comfortably inside it, away from both edges.
            var logicalCentre = (3 * cellSize) + (cellSize / 2);

            using var atScale1 = PaintSurface.Render (canvas, scaling: 1f);
            using var atScale2 = PaintSurface.Render (canvas, scaling: 2f);

            // Scale 1: device pixels equal logical units, so this holds whether or not OnPaint scales
            // -- a baseline check that the cell painted at all.
            Assert.True (IsLiveCellPixel (atScale1.GetPixel (logicalCentre, logicalCentre)),
                "the live cell did not paint at scale 1");

            // Scale 2: true only once OnPaint scales its drawing by e.Scaling. Unscaled, the cell
            // paints at the same raw pixel offset regardless of scale, and this device position is
            // still background -- this is the assertion that fails against the pre-fix sample.
            Assert.True (IsLiveCellPixel (atScale2.GetPixel (logicalCentre * 2, logicalCentre * 2)),
                "the live cell did not reach its scaled device position at scale 2 -- OnPaint is not scaling");

            // And the OLD (wrong) position is clear at scale 2: the cell moved to its correct spot: it
            // did not also keep painting its former one.
            Assert.False (IsLiveCellPixel (atScale2.GetPixel (logicalCentre, logicalCentre)),
                "the cell is still painting at its unscaled position as well as its scaled one");
        }

        [Fact]
        public void Cell_is_the_same_on_screen_size_at_every_scale ()
        {
            var (canvas, cellSize) = SingleLiveCell (3, 3);
            var logicalCentre = (3 * cellSize) + (cellSize / 2);

            using var atScale1 = PaintSurface.Render (canvas, scaling: 1f);
            using var atScale2 = PaintSurface.Render (canvas, scaling: 2f);

            var widthAt1 = LiveCellRunWidth (atScale1, logicalCentre, logicalCentre);
            var widthAt2 = LiveCellRunWidth (atScale2, logicalCentre * 2, logicalCentre * 2);

            // Same on-screen (logical) size means the DEVICE-pixel footprint is proportional to scale
            // -- a cell that came out about a third of its scale-1 size at scale 2 (the bug this issue
            // describes) misses this by far more than a rounding error.
            Assert.InRange (widthAt2, (widthAt1 * 2) - 1, (widthAt1 * 2) + 1);
        }

        // The contiguous run of live-cell-coloured pixels through (x, y), in both directions along the
        // row -- the rendered width of one cell, in device pixels.
        private static int LiveCellRunWidth (SKBitmap bitmap, int x, int y)
        {
            var left = x;
            while (left > 0 && IsLiveCellPixel (bitmap.GetPixel (left - 1, y)))
                left--;

            var right = x;
            while (right < bitmap.Width - 1 && IsLiveCellPixel (bitmap.GetPixel (right + 1, y)))
                right++;

            return (right - left) + 1;
        }
    }
}
