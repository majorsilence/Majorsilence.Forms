using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Xunit;
using SD = System.Drawing;

namespace Majorsilence.Forms.Tests;

// GFX-40, GFX-04, GFX-05: members whose meaning hangs on colour identity -- ColorTranslator.ToHtml's
// keyword branch and ControlPaint's HLS short-circuit -- and the two ControlPaint members whose whole
// point is their high-contrast branch.
//
// The BCL's own System.Drawing.ColorTranslator lives in System.Drawing.Primitives and runs on every
// platform, so it serves as the oracle: its keyword table is not restated here.
[Collection ("Headless")]
public class ColorIdentityAndContrastTests
{
    [Fact]
    public void A_real_KnownColor_Control_takes_the_HLS_short_circuit ()
    {
        // Upstream's HLSColor tests identity (ToKnownColor), not ARGB, so a KnownColor.Control a designer
        // file or resx deserialised is shaded to exactly ControlLight whatever its channels. The ARGB-only
        // test missed it wherever the BCL's KnownColor table differs from these values (on Linux and
        // macOS its Control is 236,233,216). On Windows the two agree, so there this is a guard.
        var known = Color.FromKnownColor (KnownColor.Control);

        Assert.Equal (SystemColors.ControlLight.ToArgb (), ControlPaint.Light (known, 0f).ToArgb ());
        Assert.Equal (SystemColors.ControlDark.ToArgb (), ControlPaint.Dark (known, 0f).ToArgb ());
    }

    public static IEnumerable<object[]> HtmlCases ()
    {
        foreach (var known in Enum.GetValues<KnownColor> ())
            yield return [SD.Color.FromKnownColor (known).ToArgb (), (int)known];

        // Unnamed colours, opaque and not: upstream drops alpha and never emits #AARRGGBB.
        yield return [Color.FromArgb (255, 1, 2, 3).ToArgb (), 0];
        yield return [Color.FromArgb (128, 255, 0, 0).ToArgb (), 0];
    }

    [Theory]
    [MemberData (nameof (HtmlCases))]
    public void ToHtml_matches_System_Drawing (int argb, int known)
    {
        var color = known == 0 ? Color.FromArgb (argb) : Color.FromKnownColor ((KnownColor)known);

        Assert.Equal (SD.ColorTranslator.ToHtml (color), Majorsilence.Forms.Drawing.ColorTranslator.ToHtml (color));
    }

    [Fact]
    public void ToHtml_names_a_system_colour_by_its_CSS_keyword ()
    {
        // The finding's own cases, spelled out so a reader need not run the oracle to see the point.
        Assert.Equal ("Red", Majorsilence.Forms.Drawing.ColorTranslator.ToHtml (Color.Red));
        Assert.Equal ("buttonface", Majorsilence.Forms.Drawing.ColorTranslator.ToHtml (Color.FromKnownColor (KnownColor.Control)));
        Assert.Equal ("buttonface", ColorTranslator.ToHtml (Color.FromKnownColor (KnownColor.Control)));
        Assert.Equal ("LightGrey", Majorsilence.Forms.Drawing.ColorTranslator.ToHtml (Color.LightGray));
    }

    [Fact]
    public void ContrastControlDark_is_the_window_frame_under_high_contrast ()
    {
        try {
            SystemInformation.HighContrastOverride = true;
            Assert.Equal (SystemColors.WindowFrame, ControlPaint.ContrastControlDark);

            SystemInformation.HighContrastOverride = false;
            Assert.Equal (SystemColors.ControlDark, ControlPaint.ContrastControlDark);
        } finally {
            SystemInformation.HighContrastOverride = null;
        }
    }

    [Fact]
    public void DrawStringDisabled_through_a_device_context_is_engraved ()
    {
        // Upstream draws LightLight (color) one pixel down-right, then Dark (color) on top. This drew one
        // pass "halfway to white", which on a dark theme made disabled text brighter than enabled text.
        // A big font, so glyph interiors are fully covered and carry the exact pass colours.
        var color = Color.Red;
        var colours = RenderDisabled (color);

        Assert.Contains (ControlPaint.Dark (color).ToArgb (), colours);
        Assert.Contains (ControlPaint.LightLight (color).ToArgb (), colours);
    }

    [Fact]
    public void DrawStringDisabled_uses_GrayText_alone_under_high_contrast ()
    {
        HashSet<int> colours;

        try {
            SystemInformation.HighContrastOverride = true;
            colours = RenderDisabled (Color.Red);
        } finally {
            SystemInformation.HighContrastOverride = null;
        }

        Assert.Contains (Majorsilence.Forms.Drawing.SystemColorPalette.Resolve (SystemColors.GrayText).ToArgb (), colours);
        Assert.DoesNotContain (ControlPaint.Dark (Color.Red).ToArgb (), colours);
    }

    [Fact]
    public void DrawStringDisabled_on_a_Graphics_honours_the_format ()
    {
        // The Graphics overload dropped its StringFormat, so right-aligned disabled text drew at the left.
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 12f);
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (300, 40);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
        using var format = new Majorsilence.Forms.Drawing.StringFormat { Alignment = Majorsilence.Forms.Drawing.StringAlignment.Far };

        graphics.Clear (Color.White);
        ControlPaint.DrawStringDisabled (graphics, "abc", font, Color.Black, new RectangleF (0, 0, 300, 40), format);

        Assert.True (LeftmostInk (bitmap) > 150, "Far alignment should put the text at the right");
    }

    private static HashSet<int> RenderDisabled (Color color)
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

        using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 28f, Majorsilence.Forms.Drawing.FontStyle.Bold);
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (200, 60);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);

        graphics.Clear (Color.Black);
        ControlPaint.DrawStringDisabled (graphics, "IIII", font, color, new Rectangle (4, 4, 190, 50), TextFormatFlags.Default);

        var colours = new HashSet<int> ();
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                colours.Add (bitmap.GetPixel (x, y).ToArgb ());

        return colours;
    }

    private static int LeftmostInk (Majorsilence.Forms.Drawing.Bitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
            for (var y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel (x, y).R < 200)
                    return x;

        return -1;
    }
}
