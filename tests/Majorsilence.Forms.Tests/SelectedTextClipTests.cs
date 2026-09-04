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

    // ── The clamp, against a real font's own metrics ─────────────────────────────────────────────

    [Fact]
    public void RealComboFont_TooTallForShortBox_ClampsToTopInsteadOfCentring ()
    {
        // The synthetic [Theory] above proves the arithmetic; this proves the scenario is real for an
        // actual face + string + control geometry, so it cannot pass vacuously (the risk the fragile
        // bitmap version carried: a developer's Mac resolved a face whose leading happened to keep the
        // caps just inside the clip, hiding the bug). The measurement is taken exactly as
        // SkiaTextExtensions.DrawText takes it for the DropDownList's selected item -- same string,
        // same font size, MiddleLeft, single line -- so `measured` is the real line height the
        // renderer clamps against.
        var font = new Font ("Microsoft Sans Serif", 22f, FontStyle.Regular);
        var block = TextMeasurer.CreateTextBlock ("Customer Code", font.GetSKTypeface (),
            (int) System.Math.Round (font.PixelSize), new System.Drawing.Size (180, int.MaxValue),
            TextMeasurer.GetTextAlign (ContentAlignment.MiddleLeft), SKColors.Black,
            maxLines: 1, ellipsis: false);
        var measured = (int) block.MeasuredHeight;

        // A DropDownList a few pixels shorter than its font's line box -- the WinForms designer default
        // that triggered the bug in the field.
        var box = new System.Drawing.Rectangle (12, 12, 208, measured - 8);
        Assert.True (measured > box.Height,
            $"scenario invalid: line is {measured}px, box is {box.Height}px -- not too short for the font");

        // Un-clamped middle-alignment would start the text above the top edge, where canvas.Clip
        // shaves the caps ...
        Assert.True (box.Top + (box.Height - measured) / 2 < box.Top,
            "the un-clamped centre offset is not negative -- this case would not have shown the bug");

        // ... the clamp pins the origin to the top instead, so the caps stay inside and only the
        // descenders overflow the bottom (as in GDI+).
        Assert.Equal (box.Top,
            SkiaTextExtensions.VerticalTextOrigin (SKTextAlign.Center, box, measured));
    }

    // ── Behavioural: the rendered combo keeps its caps ────────────────────────────────────────────

    // Dark-pixel counts per row down the selected-item text column of a DropDownList combo, sampled
    // from the top of the control. `window` rows are read; the renderer's own canvas.Clip still limits
    // what actually paints to the control's height.
    private static int[] SelectedItemRowInk (int boxHeight, float fontPoints, int window)
    {
        // Height is a PARAMETER, not the fixed 20px main settled on. Both exist because of the same
        // flake: below roughly 14px the clip window is so small that whether any glyph ink lands in it
        // depends on the exact metrics of whatever face "Microsoft Sans Serif" resolves to, which
        // differs between CI Linux, CI macOS and a dev box, so an absolute ink assertion flaked with no
        // behaviour change. Widening the box to 20px buys enough margin to hide that; deriving the
        // height from the resolved face's own clearance above its caps (see the caller) removes the
        // font dependence instead of out-running it, which is why this takes an argument.
        var form = new Form { UseSystemDecorations = true };
        var cbo = new ComboBox {
            Left = 12, Top = 12, Width = 208, Height = boxHeight,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font ("Microsoft Sans Serif", fontPoints, FontStyle.Regular),
        };
        cbo.Items.Add ("Customer Code");
        cbo.SelectedItem = "Customer Code";
        form.Controls.Add (cbo);

        using var bmp = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 260, 12 + window + 60));

        var scale = form.Width > 0 ? bmp.Width / (double) form.Width : 1.0;
        int SX (int v) => (int) System.Math.Round (v * scale);

        int x0 = SX (cbo.Left + 3), x1 = SX (cbo.Left + cbo.Width - 24);
        int y0 = SX (cbo.Top), y1 = SX (cbo.Top + window);

        var rows = new int[System.Math.Max (0, y1 - y0)];
        for (var y = y0; y < y1 && y < bmp.Height; y++) {
            var ink = 0;
            for (var x = x0; x < x1 && x < bmp.Width; x++) {
                var p = bmp.GetPixel (x, y);
                if (p.Red < 110 && p.Green < 110 && p.Blue < 110) ink++;
            }
            rows[y - y0] = ink;
        }
        return rows;
    }

    private static int FirstInkRow (int[] rows)
    {
        for (var i = 0; i < rows.Length; i++)
            if (rows[i] > 3)
                return i;
        return -1;
    }

    [Fact]
    public void DropDownList_TooShortForFont_KeepsCapsInsteadOfSlicingTop ()
    {
        // End to end: a DropDownList shorter than its font's line height must draw the selected item
        // with its caps where the face puts them, not slid up under the top clip.
        //
        // Every threshold is derived from a reference render rather than hard-coded, so the test does
        // not depend on which face "Microsoft Sans Serif" resolves to or how much top leading it
        // carries -- the earlier hard-coded "24pt in a 12px box, peak > 5" broke the moment a face
        // with generous leading (Noto Sans) put every visible glyph row below a 12px clip.
        const float FontPoints = 22f;

        // Reference: the same text in a box that exactly fits its line, so the render is effectively
        // top-aligned. Its first inked row is the clearance the face keeps above its caps.
        var refFont = new Font ("Microsoft Sans Serif", FontPoints, FontStyle.Regular);
        var refBlock = TextMeasurer.CreateTextBlock ("Customer Code", refFont.GetSKTypeface (),
            (int) System.Math.Round (refFont.PixelSize), new System.Drawing.Size (180, int.MaxValue),
            TextMeasurer.GetTextAlign (ContentAlignment.MiddleLeft), SKColors.Black,
            maxLines: 1, ellipsis: false);
        var measured = (int) refBlock.MeasuredHeight;

        var reference = SelectedItemRowInk (boxHeight: measured, FontPoints, window: measured);
        var refFirst = FirstInkRow (reference);
        var refLast = System.Array.FindLastIndex (reference, v => v > 3);

        Assert.True (refFirst >= 0 && refLast > refFirst, "the reference render produced no selected-item text");

        var inkSpan = refLast - refFirst + 1;
        Assert.True (inkSpan > 6, $"reference glyph ink span is only {inkSpan}px -- not real text");

        if (refFirst < 3)
            Assert.Skip ($"the resolved face keeps only {refFirst}px above its caps -- too little to slice");

        // A box that cuts through the glyphs: past the caps' clearance (so a correctly clamped render
        // still shows them) but well short of the whole line (so the un-clamped centre offset is
        // sharply negative and would slide the caps up under the top clip).
        var shortHeight = refFirst + System.Math.Max (8, (measured - refFirst) / 2);
        Assert.True (shortHeight < measured, "scenario invalid: the line is not taller than the short box");

        var shortRows = SelectedItemRowInk (shortHeight, FontPoints, window: shortHeight);
        var shortFirst = FirstInkRow (shortRows);
        var shortPeak = shortRows.Length == 0 ? 0 : System.Linq.Enumerable.Max (shortRows);

        Assert.True (shortPeak > 3,
            $"the selected item did not render in the short box; row-ink = [{string.Join (",", shortRows)}]");

        // The clamp keeps the caps at the face's own clearance. The bug slid the whole block up by the
        // negative centre offset, landing the first ink near row 0 (or clipping it away entirely).
        var tolerance = System.Math.Max (2, refFirst / 3);
        Assert.True (shortFirst >= refFirst - tolerance,
            $"selected-text caps are sliced against the top edge: first inked row {shortFirst}, "
            + $"reference clearance {refFirst}; short row-ink = [{string.Join (",", shortRows)}]");
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

        // The sample window is the combo's LOGICAL bounds; the bitmap is device pixels, so scale it.
        var s = form.Width > 0 ? bmp.Width / (double)form.Width : 1.0;
        int SX (int v) => (int)System.Math.Round (v * s);

        var ink = 0;
        for (var y = SX (cbo.Top); y < SX (cbo.Top + cbo.Height); y++)
            for (var x = SX (cbo.Left + 3); x < SX (cbo.Left + cbo.Width - 24); x++) {
                var p = bmp.GetPixel (x, y);
                if (p.Red < 110 && p.Green < 110 && p.Blue < 110) ink++;
            }

        Assert.True (ink > 30, "the selected item text should render in a normal-size DropDownList.");
    }
}
