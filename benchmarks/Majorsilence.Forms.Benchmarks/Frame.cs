using Majorsilence.Forms.Headless;

namespace Majorsilence.Forms.Benchmarks;

// Renders a headless window's current frame without encoding it. HeadlessRenderer.CapturePng also PNG-encodes the
// frame, which at 900x700 takes longer than painting it, so a paint benchmark through it mostly measured the codec.
internal static class Frame
{
    // As CapturePng (window, width, height): the host is sized first, or it renders at its default size.
    public static void Render (WindowBase window, int width = 900, int height = 700)
    {
        var host = (HeadlessWindowHost)window.Backend;
        host.Size = new System.Drawing.Size (width, height);
        using var image = host.Render ();
    }
}
