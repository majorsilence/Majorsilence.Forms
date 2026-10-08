using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Majorsilence.Forms.Drawing;
using SkiaSharp;
using Xunit;
using SD = System.Drawing;

namespace Majorsilence.Forms.Tests;

// GFX-39: SystemColors members are real known colours, as upstream's are, and a system colour is
// painted with this library's palette (the light Windows 10 defaults), not with the runtime's channels
// for it -- which off Windows are the Windows XP table (Control = 236,233,216).
[Collection ("Headless")]
public class SystemColorPaletteTests
{
    // The literals SystemColors carried before GFX-39: what every system colour must still paint as.
    public static IEnumerable<object[]> Members () => [
        [nameof (SystemColors.Control), 240, 240, 240],
        [nameof (SystemColors.ControlDark), 160, 160, 160],
        [nameof (SystemColors.ControlDarkDark), 105, 105, 105],
        [nameof (SystemColors.ControlLight), 227, 227, 227],
        [nameof (SystemColors.Highlight), 0, 120, 215],
        [nameof (SystemColors.GrayText), 109, 109, 109],
        [nameof (SystemColors.WindowFrame), 100, 100, 100],
        [nameof (SystemColors.HotTrack), 0, 102, 204],
        [nameof (SystemColors.InactiveCaptionText), 67, 78, 84],
        [nameof (SystemColors.ScrollBar), 200, 200, 200],
    ];

    private static Color Member (string name)
        => (Color)typeof (SystemColors).GetProperty (name)!.GetValue (null)!;

    [Fact]
    public void SystemColors_Control_is_the_known_color ()
    {
        // The finding's own test, plus equality: a designer file or resx deserialises KnownColor.Control,
        // and `BackColor == SystemColors.Control` compares identity as well as ARGB.
        Assert.True (SystemColors.Control.IsSystemColor);
        Assert.True (SystemColors.Control.IsKnownColor);
        Assert.Equal ("Control", SystemColors.Control.Name);
        Assert.Equal (KnownColor.Control, SystemColors.Control.ToKnownColor ());
        Assert.Equal (Color.FromKnownColor (KnownColor.Control), SystemColors.Control);
        Assert.Equal (SD.SystemColors.Control, SystemColors.Control);
    }

    [Fact]
    public void Every_upstream_member_is_its_own_known_color ()
    {
        // Upstream's member list is the oracle: System.Drawing.SystemColors (System.Drawing.Primitives)
        // runs on every platform. Each member it has must be the identical known colour here.
        var upstream = typeof (SD.SystemColors).GetProperties ().Select (p => p.Name).ToHashSet ();

        foreach (var property in typeof (SystemColors).GetProperties ()) {
            var color = (Color)property.GetValue (null)!;

            if (upstream.Contains (property.Name)) {
                Assert.True (color.IsSystemColor, property.Name);
                Assert.Equal (property.Name, color.Name);
                Assert.Equal ((Color)typeof (SD.SystemColors).GetProperty (property.Name)!.GetValue (null)!, color);
            } else {
                // This library's own extensions have no known colour to be.
                Assert.False (color.IsSystemColor, property.Name);
            }
        }

        Assert.Contains (nameof (SystemColors.MenuBar), upstream);
        Assert.DoesNotContain (nameof (SystemColors.AlternateRow), upstream);
        Assert.DoesNotContain (nameof (SystemColors.ButtonText), upstream);
    }

    [Fact]
    public void The_extensions_keep_their_values ()
    {
        Assert.Equal (Color.FromArgb (240, 248, 255).ToArgb (), SystemColors.AlternateRow.ToArgb ());
        Assert.Equal (Color.Black.ToArgb (), SystemColors.ButtonText.ToArgb ());
    }

    [Fact]
    public void ToHtml_names_SystemColors_by_keyword_now ()
    {
        // GFX-40 could only reach the keyword branch for a deserialised KnownColor until this landed.
        Assert.Equal ("buttonface", Majorsilence.Forms.Drawing.ColorTranslator.ToHtml (SystemColors.Control));
        Assert.Equal ("highlight", Majorsilence.Forms.Drawing.ColorTranslator.ToHtml (SystemColors.Highlight));
    }

    [Theory]
    [MemberData (nameof (Members))]
    public void A_system_color_converts_to_the_palette_not_the_runtime_table (string name, int r, int g, int b)
    {
        var color = Member (name);

        Assert.Equal (new SKColor ((byte)r, (byte)g, (byte)b), color.ToSKColor ());
        Assert.Equal (Color.FromArgb (r, g, b).ToArgb (), SystemColorPalette.Resolve (color).ToArgb ());
    }

    [Fact]
    public void A_control_painted_with_KnownColor_Control_shows_the_palette_value ()
    {
        // On macOS and Linux the runtime's KnownColor.Control is the XP beige; on Windows it is the live
        // OS colour, which on a default scheme is the palette value already -- there this is a guard.
        var known = Color.FromKnownColor (KnownColor.Control);
        using var panel = new Panel { Width = 40, Height = 30, BackColor = known };
        using var bitmap = PaintSurface.RenderOnForm (panel);

        var centre = bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2);

        Assert.Equal (new SKColor (240, 240, 240), centre);
        if (!OperatingSystem.IsWindows ())
            Assert.NotEqual (new SKColor (known.R, known.G, known.B), centre);
    }

    [Fact]
    public void Pens_brushes_and_Clear_paint_the_palette ()
    {
        // The Drawing.Common half of the conversion: Pen and SolidBrush build their own SKPaint.
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (30, 10);

        using (var g = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap)) {
            g.Clear (SystemColors.Highlight);
            g.FillRectangle (SystemBrushes.Control, 10, 0, 10, 10);
            g.DrawLine (SystemPens.GrayText, 20, 5, 30, 5);
        }

        Assert.Equal (Color.FromArgb (0, 120, 215).ToArgb (), bitmap.GetPixel (5, 5).ToArgb ());
        Assert.Equal (Color.FromArgb (240, 240, 240).ToArgb (), bitmap.GetPixel (15, 5).ToArgb ());
        Assert.Equal (Color.FromArgb (109, 109, 109).ToArgb (), bitmap.GetPixel (25, 5).ToArgb ());
    }

    public static IEnumerable<object[]> ControlKinds () => [
        [typeof (Button)], [typeof (CheckBox)], [typeof (Label)], [typeof (Panel)], [typeof (TextBox)], [typeof (GroupBox)],
    ];

    [Theory]
    [MemberData (nameof (ControlKinds))]
    public void A_known_system_color_renders_exactly_as_the_old_literal_did (Type kind)
    {
        // Before GFX-39 SystemColors.X was Color.FromArgb (palette literal); a control coloured with it
        // must render pixel-for-pixel as one coloured with that literal.
        SKBitmap Render (Color back, Color fore)
        {
            var control = (Control)Activator.CreateInstance (kind)!;
            control.Width = 90;
            control.Height = 40;
            control.Text = "Text";
            control.BackColor = back;
            control.ForeColor = fore;
            return PaintSurface.RenderOnForm (control);
        }

        using var known = Render (SystemColors.ControlDark, SystemColors.GrayText);
        using var literal = Render (Color.FromArgb (160, 160, 160), Color.FromArgb (109, 109, 109));

        Assert.Equal (literal.Bytes, known.Bytes);
    }

    [Fact]
    public void ControlPaint_shades_a_system_color_from_the_palette ()
    {
        // HLS arithmetic reads channels; a system colour's own channels are the runtime's table. Dark
        // (Highlight) must be the shade of 0,120,215, as it was when Highlight was that literal.
        Assert.Equal (
            ControlPaint.Dark (Color.FromArgb (0, 120, 215)).ToArgb (),
            ControlPaint.Dark (SystemColors.Highlight).ToArgb ());
        Assert.Equal (
            ControlPaint.Light (Color.FromArgb (109, 109, 109), 0.3f).ToArgb (),
            ControlPaint.Light (SystemColors.GrayText, 0.3f).ToArgb ());
    }

    // Every place a System.Drawing.Color turns into an SKColor must go through SystemColorPalette
    // (directly, or via ColorCompatExtensions.ToSKColor), or a system colour paints the runtime's
    // channels. This fails on a new direct conversion so later code cannot quietly bypass the palette.
    private static readonly Regex direct_conversion = new (
        @"new\s*(SkiaSharp\.)?(SKColor)?\s*\(\s*[\w.\[\]]+\.R\s*,\s*[\w.\[\]]+\.G\s*,\s*[\w.\[\]]+\.B\b",
        RegexOptions.Compiled);

    // Files whose colours are not System.Drawing colours, or must not be resolved, with the reason.
    private static readonly Dictionary<string, string> allowed = new () {
        // The palette itself.
        ["src/Majorsilence.Forms.Drawing.Common/SystemColorPalette.cs"] = "the resolver",
        // Avalonia.Media.Color / Windows.UI.Color: the host's accent, not a System.Drawing colour.
        ["src/Majorsilence.Forms.Avalonia/MajorsilenceFormsTheme.cs"] = "Avalonia colour",
        ["src/Majorsilence.Forms.Uno/MajorsilenceFormsTheme.cs"] = "WinUI colour",
        // System.Windows.Media.Color: the WPF host's own background brush.
        ["src/Majorsilence.Forms.Wpf/MajorsilenceFormsPresenter.cs"] = "WPF colour",
        // Clears to the REAL WinForms host's BackColor so the embedded scene blends into a form the OS
        // paints with its live colours -- the runtime's channels are the right ones there.
        ["src/Majorsilence.Forms.WinForms/MajorsilenceFormsPresenter.cs"] = "real WinForms host colour",
    };

    [Fact]
    public void No_source_file_converts_a_Color_to_SKColor_directly ()
    {
        var root = RepoRoot ();
        var offenders = new List<string> ();

        foreach (var file in Directory.EnumerateFiles (Path.Combine (root, "src"), "*.cs", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath (root, file).Replace ('\\', '/');
            if (relative.Contains ("/obj/") || relative.Contains ("/bin/") || allowed.ContainsKey (relative))
                continue;

            var lines = File.ReadAllLines (file);
            for (var i = 0; i < lines.Length; i++)
                if (direct_conversion.IsMatch (lines[i]))
                    offenders.Add ($"{relative}:{i + 1}: {lines[i].Trim ()}");
        }

        Assert.True (offenders.Count == 0,
            "Convert through Color.ToSKColor () / SystemColorPalette, not new SKColor (c.R, c.G, c.B, …):\n"
            + string.Join ("\n", offenders));

        // The allow-list must not rot: each entry still exists and still needs its exemption.
        foreach (var path in allowed.Keys)
            Assert.True (direct_conversion.IsMatch (File.ReadAllText (Path.Combine (root, path))), path);
    }

    [Fact]
    public void The_guard_pattern_recognises_the_shapes_it_must_reject ()
    {
        // Guard on the guard: the forms the GFX-39 sweep replaced.
        Assert.Matches (direct_conversion, "new SKColor (c.R, c.G, c.B, c.A)");
        Assert.Matches (direct_conversion, "new SkiaSharp.SKColor (key.R, key.G, key.B, key.A)");
        Assert.Matches (direct_conversion, "=> new (c.R, c.G, c.B, c.A);");
        Assert.Matches (direct_conversion, "new SKColor (map.NewColor.R, map.NewColor.G, map.NewColor.B, map.NewColor.A)");
        Assert.DoesNotMatch (direct_conversion, "c.ToSKColor ()");
    }

    private static string RepoRoot ()
    {
        var dir = new DirectoryInfo (AppContext.BaseDirectory);
        while (dir is not null) {
            if (File.Exists (Path.Combine (dir.FullName, "Majorsilence.Forms.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException ($"could not locate the repository from {AppContext.BaseDirectory}");
    }
}
