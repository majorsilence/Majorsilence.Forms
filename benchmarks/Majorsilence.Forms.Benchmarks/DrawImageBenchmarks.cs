using System.Drawing;
using BenchmarkDotNet.Attributes;

namespace Majorsilence.Forms.Benchmarks;

// A report page drawing a picture: Graphics.DrawImage of a logo and of a full-page photo onto a 2000x1500 page
// bitmap made by Graphics.FromImage, and the photo through a Graphics over a canvas, as a control paints; and copying
// an image.
[MemoryDiagnoser]
public class DrawImageBenchmarks
{
    private Bitmap page = null!;
    private Graphics g = null!;
    private Bitmap logo = null!;
    private Bitmap photo = null!;
    private SkiaSharp.SKCanvas canvas = null!;
    private Graphics onCanvas = null!;
    private ImageList images = null!;

    [GlobalSetup]
    public void Setup ()
    {
        page = new Bitmap (2000, 1500);
        g = Graphics.FromImage (page);
        logo = new Bitmap (200, 80);
        photo = new Bitmap (1600, 1200);
        using (var lg = Graphics.FromImage (logo))
            lg.Clear (Color.SteelBlue);
        using (var pg = Graphics.FromImage (photo))
            pg.Clear (Color.Coral);
        canvas = new SkiaSharp.SKCanvas (new Bitmap (2000, 1500).GetSKBitmap ()!);
        onCanvas = new Graphics (canvas);
        images = new ImageList { ImageSize = new Size (16, 16) };
        images.Images.Add (new Bitmap (16, 16));
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        g.Dispose ();
        page.Dispose ();
        logo.Dispose ();
        photo.Dispose ();
        onCanvas.Dispose ();
        canvas.Dispose ();
    }

    [Benchmark]
    public void Logo () => g.DrawImage (logo, new Rectangle (100, 100, 200, 80));

    [Benchmark]
    public void Photo () => g.DrawImage (photo, new Rectangle (200, 150, 1600, 1200));

    // Copies an image: Image.Clone, and reading an icon back from an ImageList (each read hands out a copy).
    [Benchmark]
    public void ClonePhoto () => ((Image)photo.Clone ()).Dispose ();

    [Benchmark]
    public void ReadImageListIcon () => images.Images[0].Dispose ();

    [Benchmark]
    public void PhotoOnACanvas () => onCanvas.DrawImage (photo, new Rectangle (200, 150, 1600, 1200));
}
