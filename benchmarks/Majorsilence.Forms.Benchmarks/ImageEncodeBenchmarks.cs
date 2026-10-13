using System.Drawing;
using BenchmarkDotNet.Attributes;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Benchmarks;

// Image.Save, the way a report engine exports a rendered page or chart: a Bitmap drawn on, then encoded to a
// stream. The time is mostly the codec's; what this library adds around it (a pixel snapshot before encoding, an
// intermediate encoded buffer copied to the stream) is the part that can change.
//
// Not in the allocation gate: an encode takes 5-90 ms, so few run per measurement and whatever the runtime
// allocates in the background meanwhile lands in the per-operation figure (320 to 1117 B between runs).
[MemoryDiagnoser]
public class ImageEncodeBenchmarks
{
    private Bitmap bitmap = null!;
    private MemoryStream stream = null!;

    [Params ("800x600", "2000x1500")]
    public string Size { get; set; } = "";

    [Params ("Png", "Jpeg")]
    public string Format { get; set; } = "";

    [GlobalSetup]
    public void Setup ()
    {
        var parts = Size.Split ('x');
        bitmap = new Bitmap (int.Parse (parts[0]), int.Parse (parts[1]));
        using (var g = Graphics.FromImage (bitmap)) {
            g.Clear (Color.White);
            using var brush = new SolidBrush (Color.SteelBlue);
            using var font = new Font ("Arial", 12f);
            for (var y = 0; y < bitmap.Height; y += 40) {
                g.FillRectangle (brush, 20, y + 5, (y * 7) % (bitmap.Width - 40), 20);
                g.DrawString ($"Row {y / 40}: quarterly results", font, Brushes.Black, 30, y + 8);
            }
        }
        stream = new MemoryStream (capacity: bitmap.Width * bitmap.Height * 4);
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        bitmap.Dispose ();
        stream.Dispose ();
    }

    [Benchmark]
    public long Save ()
    {
        stream.Position = 0;
        stream.SetLength (0);
        bitmap.Save (stream, Format == "Png" ? ImageFormat.Png : ImageFormat.Jpeg);
        return stream.Length;
    }
}
