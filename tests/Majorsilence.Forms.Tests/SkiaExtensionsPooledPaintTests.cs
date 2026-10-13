using System;
using System.Threading;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The SKColor canvas helpers share one paint per thread instead of making one per call (performance plan F15). A
    // helper must draw the same pixels whatever ran on the thread before it: the oracle draws it on a new thread,
    // where the shared paint starts fresh, as every call's paint used to. Called through SkiaExtensions by name: an
    // SKCanvas instance method with the same shape (DrawBitmap) would otherwise win.
    public class SkiaExtensionsPooledPaintTests
    {
        private static readonly SKBitmap Sample = MakeSample ();

        private static readonly (string Name, Action<SKCanvas> Draw)[] helpers = [
            ("FillRectangle", c => SkiaExtensions.FillRectangle (c, 3, 4, 20, 9, new SKColor (200, 30, 30, 160))),
            ("DrawRectangle", c => SkiaExtensions.DrawRectangle (c, 2, 2, 30, 20, SKColors.Navy, 3)),
            ("DrawLine diagonal", c => SkiaExtensions.DrawLine (c, 1.5f, 2, 40, 33.5f, SKColors.Green, 2)),
            ("DrawLine vertical", c => SkiaExtensions.DrawLine (c, 10.5f, 2, 10.5f, 30, SKColors.Green, 1)),
            ("DrawPath", c => { using var p = new SKPath (); p.MoveTo (2, 30); p.QuadTo (20, 0, 38, 30); SkiaExtensions.DrawPath (c, p, SKColors.Purple, 2); }),
            ("DrawCircle", c => SkiaExtensions.DrawCircle (c, 20, 20, 12, SKColors.Black, 2)),
            ("FillCircle", c => SkiaExtensions.FillCircle (c, 20, 20, 9, SKColors.Orange)),
            ("DrawRoundedRectangle", c => SkiaExtensions.DrawRoundedRectangle (c, 3, 3, 34, 24, SKColors.Teal, 6, 6, 2)),
            ("FillRoundedRectangle", c => SkiaExtensions.FillRoundedRectangle (c, 3, 3, 34, 24, SKColors.Teal, 6, 6, 3)),
            ("DrawBitmap disabled", c => SkiaExtensions.DrawBitmap (c, Sample, 2, 2, disabled: true)),
            ("DrawBitmap", c => SkiaExtensions.DrawBitmap (c, Sample, 2, 2)),
        ];

        private static SKBitmap MakeSample ()
        {
            var bitmap = new SKBitmap (16, 16);
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    bitmap.SetPixel (x, y, new SKColor ((byte)(x * 16), (byte)(y * 16), 128));
            return bitmap;
        }

        [Fact]
        public void A_helper_draws_the_same_whatever_ran_before_it ()
        {
            foreach (var (nextName, next) in helpers) {
                var expected = OnNewThread (next);

                foreach (var (previousName, previous) in helpers) {
                    using (var scratch = new SKBitmap (48, 40))
                    using (var canvas = new SKCanvas (scratch))
                        previous (canvas);

                    var actual = Render (next);
                    Assert.True (expected.AsSpan ().SequenceEqual (actual), $"{nextName} after {previousName}");
                }
            }
        }

        private static byte[] Render (Action<SKCanvas> draw)
        {
            using var bitmap = new SKBitmap (48, 40);
            bitmap.Erase (SKColors.White);
            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Translate (0.5f, 0.25f);
                draw (canvas);
            }
            return bitmap.Bytes;
        }

        private static byte[] OnNewThread (Action<SKCanvas> draw)
        {
            byte[]? pixels = null;
            var thread = new Thread (() => pixels = Render (draw));
            thread.Start ();
            thread.Join ();
            return pixels!;
        }
    }
}
