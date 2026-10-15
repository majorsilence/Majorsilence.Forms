using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Headless renders depended on the host: macOS forms had no custom title bar, and the UI and default fonts
// were whatever the machine resolved "Segoe UI"/"sans-serif" to, so two Linux machines rendered the same
// title hundreds of pixels apart and no baseline could be shared. Found by Recital's pixel tests
// (FRAMEWORK_GAPS GAP-003).
[Collection ("Headless")]
public sealed class HeadlessDeterministicTests
{
    private static SKTypeface Bundled (SKFontStyleWeight weight)
        => FontSubstitution.EmbeddedFaces ("Noto Sans").Single (f => f.FontWeight == (int) weight && f.FontSlant == SKFontStyleSlant.Upright);

    [Fact]
    public void Controls_draw_with_the_bundled_font ()
    {
        using (HeadlessRenderer.UseDeterministic ()) {
            using var form = new Form ();
            var tree = new TreeView ();
            form.Controls.Add (tree);

            Assert.Same (Bundled (SKFontStyleWeight.Normal), Theme.UIFont);
            Assert.Same (Bundled (SKFontStyleWeight.Bold), Theme.UIFontBold);
            // The bundled copy, not an installed Noto Sans of another version.
            Assert.Same (Bundled (SKFontStyleWeight.Normal), tree.GetEffectiveFont ());
        }
    }

    [Fact]
    public void Every_form_draws_the_custom_title_bar ()
    {
        using (HeadlessRenderer.UseDeterministic ()) {
            using var form = new Form ();
            Assert.False (form.UseSystemDecorations);
        }
    }

    [Fact]
    public void Disposing_restores_the_fonts_there_were ()
    {
        HeadlessRenderer.Use ();
        var ui = Theme.UIFont;
        var bold = Theme.UIFontBold;
        var tree = new TreeView ();
        var effective = tree.GetEffectiveFont ();

        HeadlessRenderer.UseDeterministic ().Dispose ();

        Assert.Same (ui, Theme.UIFont);
        Assert.Same (bold, Theme.UIFontBold);
        Assert.Same (effective, new TreeView ().GetEffectiveFont ());
        Assert.Null (Drawing.Text.PrivateFontRegistry.Resolve ("Noto Sans", SKFontStyle.Normal));
    }

    [Fact]
    public void A_render_is_the_same_whatever_the_machine_default_is ()
    {
        // Two different "machine" defaults stand in for two machines: deterministic mode must override both.
        static byte[] Render ()
        {
            using var form = new Form { Text = "Recital" };
            form.Controls.Add (new TreeView { Dock = DockStyle.Fill, Nodes = { "Chapter one", "Chapter two" } });
            return HeadlessRenderer.CapturePng (form, 300, 200);
        }

        HeadlessRenderer.Use ();
        Application.SetDefaultFont (new Font ("Liberation Mono", 9));
        byte[] first, second;
        try {
            using (HeadlessRenderer.UseDeterministic ())
                first = Render ();
        } finally {
            SystemFonts.SetDefaultFont (null);
        }

        Application.SetDefaultFont (new Font ("Liberation Serif", 11));
        try {
            using (HeadlessRenderer.UseDeterministic ())
                second = Render ();
        } finally {
            SystemFonts.SetDefaultFont (null);
        }

        Assert.Equal (first, second);
    }
}
