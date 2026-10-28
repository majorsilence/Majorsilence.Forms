using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.SpellCheck;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadRichTextEditor's IsSpellCheckingEnabled, BorderColor, SelectionFill and ScaleFactor, and
// RadDocument.IsEmpty -- members Telerik code uses that the compat editor did not have. The expected
// values are Telerik's, measured on UI.for.WinForms 2026.2.520. Headless has no webview, so these run
// against the editor's plain-text fallback.
[Collection ("Headless")]
public class TelerikRichTextEditorMembersTests
{
    // ── RadDocument.IsEmpty ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData ("", true)]
    [InlineData ("<p></p>", true)]
    [InlineData ("<p></p><p></p>", true)]
    [InlineData ("<p></p> <p></p>", true)]
    [InlineData ("<p></p>\n<p></p>", true)]
    [InlineData ("<p>\n</p>", true)]
    [InlineData ("<div></div>", true)]
    [InlineData ("<ul><li></li></ul>", true)]
    [InlineData ("<hr>", true)]
    [InlineData ("<p><span style='color:red'></span></p>", true)]
    [InlineData ("<!-- c -->", true)]
    [InlineData ("<html><head><title>t</title></head><body></body></html>", true)]
    [InlineData ("<style>p{}</style>", true)]
    [InlineData (" ", true)]
    [InlineData ("\n", true)]
    [InlineData ("<p>a</p>", false)]
    [InlineData ("plain", false)]
    [InlineData ("<p> </p>", false)]
    [InlineData ("<p>\t</p>", false)]
    [InlineData ("<p>\r\n </p>", false)]
    [InlineData ("<span> </span>", false)]
    [InlineData ("<p><b> </b></p>", false)]
    [InlineData ("<p>&nbsp;</p>", false)]
    [InlineData ("&nbsp;", false)]
    [InlineData ("<p>&#32;</p>", false)]
    [InlineData ("<br>", false)]
    [InlineData ("<p><br></p>", false)]
    [InlineData ("<br><br>", false)]
    [InlineData ("<img src='x.png'>", false)]
    [InlineData ("<table><tr><td></td></tr></table>", false)]
    public void IsEmpty_reads_the_html_as_Telerik_imports_it (string html, bool empty)
        => Assert.Equal (empty, new RadDocument { Html = html }.IsEmpty);

    [Fact]
    public void An_editor_is_empty_until_text_is_put_in_it_and_empty_again_once_it_is_cleared ()
    {
        using var editor = new RadRichTextEditor ();

        Assert.True (editor.Document.IsEmpty);

        editor.Document = new RadDocument { Html = "<p>hello</p>" };
        Assert.False (editor.Document.IsEmpty);

        editor.FallbackTextBox!.Text = string.Empty;
        Assert.True (editor.Document.IsEmpty);
    }

    // ── IsSpellCheckingEnabled ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Spell_checking_is_off_by_default_and_marks_words_only_while_it_is_on ()
    {
        using var editor = new RadRichTextEditor ();
        var box = editor.FallbackTextBox!;

        Assert.False (editor.IsSpellCheckingEnabled);
        Assert.Null (TextBoxSpellCheck.GetSpellChecker (box));

        editor.IsSpellCheckingEnabled = true;
        Assert.True (editor.IsSpellCheckingEnabled);
        var checker = TextBoxSpellCheck.GetSpellChecker (box);
        Assert.NotNull (checker);

        editor.IsSpellCheckingEnabled = false;
        Assert.Null (TextBoxSpellCheck.GetSpellChecker (box));

        // Turned back on, it is the same checker -- words the user added to its dictionary are kept.
        editor.IsSpellCheckingEnabled = true;
        Assert.Same (checker, TextBoxSpellCheck.GetSpellChecker (box));
    }

    // ── BorderColor ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_editor_is_framed_in_a_border_of_BorderColor ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 400, Height = 300 };
        var editor = new RadRichTextEditor { Width = 200, Height = 120 };
        form.Controls.Add (editor);
        form.Show ();

        Assert.Equal (Color.FromArgb (156, 189, 232), editor.BorderColor);

        using (var frame = PaintSurface.Render (editor))
            Assert.Equal (new SKColor (156, 189, 232), frame.GetPixel (0, frame.Height / 2));

        editor.BorderColor = Color.Red;
        Assert.Equal (Color.Red.ToArgb (), editor.BorderColor.ToArgb ());

        using (var red = PaintSurface.Render (editor)) {
            Assert.Equal (SKColors.Red, red.GetPixel (0, red.Height / 2));
            Assert.Equal (SKColors.Red, red.GetPixel (red.Width - 1, red.Height / 2));
            Assert.Equal (SKColors.Red, red.GetPixel (red.Width / 2, 0));
            Assert.Equal (SKColors.Red, red.GetPixel (red.Width / 2, red.Height - 1));
        }

        // The document sits inside the frame, not over it.
        var box = editor.FallbackTextBox!;
        Assert.True (box.Left >= 1 && box.Top >= 1, $"fallback at {box.Bounds}");
        Assert.True (box.Right <= editor.Width - 1 && box.Bottom <= editor.Height - 1, $"fallback at {box.Bounds}");
    }

    // ── SelectionFill ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Selected_text_is_highlighted_in_SelectionFill ()
    {
        using var editor = new RadRichTextEditor ();
        var box = editor.FallbackTextBox!;

        Assert.Equal (Color.FromArgb (128, 78, 158, 255), editor.SelectionFill);
        Assert.Equal (new SKColor (78, 158, 255, 128), box.document.SelectionColor);

        editor.SelectionFill = Color.FromArgb (200, 10, 20, 30);

        Assert.Equal (Color.FromArgb (200, 10, 20, 30), editor.SelectionFill);
        Assert.Equal (new SKColor (10, 20, 30, 200), box.document.SelectionColor);
    }

    // ── ScaleFactor ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ScaleFactor_zooms_the_view_and_rejects_a_negative_factor ()
    {
        using var editor = new RadRichTextEditor ();
        var box = editor.FallbackTextBox!;

        Assert.Equal (new SizeF (1, 1), editor.ScaleFactor);
        Assert.Equal (1f, box.ZoomFactor);

        // The value a designer at 112 DPI writes.
        editor.ScaleFactor = new SizeF (1.16666663F, 1.15384614F);
        Assert.Equal (new SizeF (1.16666663F, 1.15384614F), editor.ScaleFactor);
        Assert.Equal (1.15384614F, box.ZoomFactor, 4);

        editor.ScaleFactor = new SizeF (2, 2);
        Assert.Equal (2f, box.ZoomFactor, 4);

        // Zero is taken (the fallback zooms as far out as it can); a negative side is not.
        editor.ScaleFactor = new SizeF (0, 0);
        Assert.Equal (new SizeF (0, 0), editor.ScaleFactor);
        Assert.Throws<ArgumentException> (() => editor.ScaleFactor = new SizeF (-1, 5));
        Assert.Equal (new SizeF (0, 0), editor.ScaleFactor);

        // The view's zoom is not the display's DPI factor, which the control still reports as before.
        Assert.Equal (new SizeF (1, 1), ((Control) editor).ScaleFactor);
    }
}
