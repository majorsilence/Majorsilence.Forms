using System.Drawing;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Benchmarks;

// Full-bitmap pixel writes, the shape of work the Fractals panel's Mandelbrot renderer does once
// per click/slider change (a fresh escape-time value for every pixel of an 860x550 canvas). That
// panel currently uses Bitmap.SetPixel per pixel and takes roughly 1-2 seconds to render; this
// compares it against Bitmap.LockBits' bulk buffer write for the same total pixel count, to put a
// number on whether switching is worth it.
[MemoryDiagnoser]
public class BitmapFillBenchmarks
{
    [Params (256, 512, 860)]
    public int Size { get; set; }

    private Bitmap bitmap = null!;

    [GlobalSetup]
    public void Setup () => bitmap = new Bitmap (Size, Size);

    [GlobalCleanup]
    public void Cleanup () => bitmap.Dispose ();

    [Benchmark (Baseline = true)]
    public void SetPixel ()
    {
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                bitmap.SetPixel (x, y, Color.FromArgb (255, x & 0xFF, y & 0xFF, 128));
    }

    [Benchmark]
    public void LockBits ()
    {
        var rect = new Rectangle (0, 0, Size, Size);
        var data = bitmap.LockBits (rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buffer = new byte[data.Stride * Size];

        for (var y = 0; y < Size; y++) {
            var row = y * data.Stride;

            for (var x = 0; x < Size; x++) {
                var i = row + (x * 4);
                buffer[i + 0] = 128;               // B
                buffer[i + 1] = (byte)(y & 0xFF);  // G
                buffer[i + 2] = (byte)(x & 0xFF);  // R
                buffer[i + 3] = 255;                // A
            }
        }

        Marshal.Copy (buffer, 0, data.Scan0, buffer.Length);
        bitmap.UnlockBits (data);
    }
}
