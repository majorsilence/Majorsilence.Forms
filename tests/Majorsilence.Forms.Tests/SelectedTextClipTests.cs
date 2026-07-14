using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Regression: a DropDownList ComboBox (and any center/bottom-aligned text) whose font line height
// exceeds the control's height must NOT have its glyph caps sliced off the top. Reported from the
// live net10.0 TownSuite app: DropDownList combos with a larger font (Microsoft Sans Serif 12pt in a
// ~28px-tall box) rendered the selected item with the top of every letter shaved -- "Customer Code"
// looked like its caps were cut, reading as a clipped first character. Root cause: vertical centering
// in SkiaTextExtensions computed a NEGATIVE Y offset when the measured line height was taller than the
// control, painting the text above bounds.Top where canvas.Clip(bounds) shaved the caps. WinForms/GDI+
// never top-clips: it keeps the caps and lets the descenders overflow the bottom. Fixed by clamping the
// vertical origin so it is never above bounds.Top (SkiaTextExtensions.VerticalTextOrigin).
public class SelectedTextClipTests
{
    // ── Unit: the clamp itself (deterministic, font-independent) ──────────────────────────────────

    [Theory]
    // Text that fits: centered/bottomed exactly as before (offset stays positive).
    [InlineData (SKTextAlign.Center, /*top*/10, /*height*/30, /*measured*/16, /*expected*/17)] // 10 + (30-16)/2
    [InlineData (SKTextAlign.Right,  /*top*/10, /*height*/30, /*measured*/16, /*expected*/24)] // bottom - measured = 40-16
    [InlineData (SKTextAlign.Left,   /*top*/10, /*height*/30, /*measured*/16, /*expected*/10)] // top-aligned
    // Text taller than the control: origin clamped to the top instead of going negative.
    [InlineData (SKTextAlign.Center, /*top*/10, /*height*/12, /*measured*/28, /*expected*/10)] // would be 10+(-8)=2 -> clamp 10
    [InlineData (SKTextAlign.Right,  /*top*/10, /*height*/12, /*measured*/28, /*expected*/10)] // would be 22-28=-6 -> clamp 10
    public void VerticalTextOrigin_NeverPushesTextAboveTop (SKTextAlign vertical, int top, int height, int measured, int expected)
    {
        var bounds = new System.Drawing.Rectangle (0, top, 200, height);

        var y = SkiaTextExtensions.VerticalTextOrigin (vertical, bounds, measured);

        Assert.Equal (expected, y);
        Assert.True (y >= bounds.Top, "text origin must never be above the top of its bounds");
    }

    // ── Behavioural: the rendered combo keeps its caps ────────────────────────────────────────────

    [Fact]
    public void DropDownList_TooShortForFont_KeepsCapsInsteadOfSlicingTop ()
    {
        // A control deliberately far too short for its font forces the (previously negative) centering
        // offset. Before the fix the visible window showed the dense MIDDLE band of the glyphs jammed
        // against the top edge (caps sliced). After the fix the text is top-clamped, so the caps sit
        // fully inside the control with clearance above them and only the bottom overflows (clipped).
        var form = new Form { UseSystemDecorations = true };
        var cbo = new ComboBox {
            Left = 12, Top = 12, Width = 208, Height = 12,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font ("Microsoft Sans Serif", 24f, FontStyle.Regular)
        };
        cbo.Items.Add ("Customer Code");
        cbo.SelectedItem = "Customer Code";
        form.Controls.Add (cbo);

        var png = HeadlessRenderer.CapturePng (form, 260, 80);
        using var bmp = SKBitmap.Decode (png);

        // Count dark glyph ink per row across the text region (inside the combo, left of the glyph button).
        int x0 = cbo.Left + 3, x1 = cbo.Left + cbo.Width - 24;
        int y0 = cbo.Top, y1 = cbo.Top + cbo.Height;
        var rowInk = new int[y1 - y0];
        for (var y = y0; y < y1; y++) {
            var ink = 0;
            for (var x = x0; x < x1; x++) {
                var p = bmp.GetPixel (x, y);
                if (p.Red < 110 && p.Green < 110 && p.Blue < 110) ink++;
            }
            rowInk[y - y0] = ink;
        }

        var peak = 0;
        foreach (var v in rowInk) if (v > peak) peak = v;
        Assert.True (peak > 5, "the selected item text should render (found almost no glyph ink).");

        // First row carrying real glyph ink. With the caps sliced at the top (the bug) this is at the
        // very first row(s); with the fix the caps are pushed down by the font's ascent/clamp, leaving
        // empty rows above them.
        var firstInkRow = -1;
        for (var i = 0; i < rowInk.Length; i++)
            if (rowInk[i] > 3) { firstInkRow = i; break; }

        Assert.True (firstInkRow >= 2,
            $"selected text caps are clipped against the top edge (first inked row = {firstInkRow}); " +
            $"row-ink profile = [{string.Join (",", rowInk)}].");
    }

    [Fact]
    public void DropDownList_TypicalSize_RendersSelectedItem ()
    {
        // The everyday case (28px box, 12pt font) must still render the selected item cleanly.
        var form = new Form { UseSystemDecorations = true };
        var cbo = new ComboBox {
            Left = 10, Top = 10, Width = 208, Height = 28,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font ("Microsoft Sans Serif", 12f, FontStyle.Regular)
        };
        cbo.Items.Add ("Customer Code");
        cbo.SelectedItem = "Customer Code";
        form.Controls.Add (cbo);

        var png = HeadlessRenderer.CapturePng (form, 260, 60);
        using var bmp = SKBitmap.Decode (png);

        var ink = 0;
        for (var y = cbo.Top; y < cbo.Top + cbo.Height; y++)
            for (var x = cbo.Left + 3; x < cbo.Left + cbo.Width - 24; x++) {
                var p = bmp.GetPixel (x, y);
                if (p.Red < 110 && p.Green < 110 && p.Blue < 110) ink++;
            }

        Assert.True (ink > 30, "the selected item text should render in a normal-size DropDownList.");
    }
}
