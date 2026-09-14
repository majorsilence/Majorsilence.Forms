using System;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Windows.Forms.Design.Behavior;

namespace WinFormsCompatDemo;

// Compile-only coverage for the mappings and wrapper behavior added after the Reporting migration.
// This deliberately uses only ordinary WinForms namespaces: no Majorsilence.Forms type appears in
// the source, so a build proves the generator emits the required surface rather than relying on a
// consumer-side escape hatch.
internal static class CompatSurfaceCoverage
{
    internal static void Compile()
    {
        using var bitmap = new Bitmap(2, 3);
        using var brush = new SolidBrush(Color.Lime);
        using var pen = new Pen(Color.White);

        // Polymorphic storage of a concrete leaf as its real GDI+ base: pass 1 makes every subclass
        // derive from its real Majorsilence.Forms counterpart directly (SolidBrush : real.SolidBrush),
        // never from a compat Brush, so this assignment used to fail (CS0029) despite compiling
        // against nothing but real GDI+ type names. Brush is now a pass-1b wrapper specifically so
        // this works without SolidBrush itself changing at all -- see IsForcedPolymorphicRoot's
        // remarks for why Control couldn't get the same treatment.
        Brush polymorphicBrush = brush;
        GC.KeepAlive(polymorphicBrush);

        // Bitmap.Width/Height are inherited from Image; the wrapper must forward them as well as
        // members declared directly on Bitmap. Graphics has no public constructor, but its static
        // factory is still useful and is exposed through a static-only compat facade.
        var size = bitmap.Width + bitmap.Height;
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        graphics.DrawLine(pen, 0, 0, size, size);

        using var path = new GraphicsPath();
        path.AddLine(0, 0, 1, 1);
        var format = PixelFormat.Format32bppArgb;
        var hint = TextRenderingHint.AntiAlias;
        var document = new PrintDocument { DocumentName = format + hint.ToString() };

        // The design namespaces are emitted independently rather than leaking their original
        // Majorsilence.Forms namespaces into a consumer's source.
        var designer = new ComponentDesigner();
        var behavior = new Behavior();

        GC.KeepAlive(document);
        GC.KeepAlive(designer);
        GC.KeepAlive(behavior);
    }
}
