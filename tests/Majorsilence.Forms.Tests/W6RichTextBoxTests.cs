using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, thirteenth chunk: the RichTextBox options that were stored and read by nothing --
// the baseline shift, protected ranges (and the Protected event), URL detection, the formatting
// shortcuts, word-granular drag selection, the selection margin, and ContentsResized.
[Collection ("Headless")]
public class W6RichTextBoxTests
{
    private static RichTextBox Rich (string? text = null, bool multiline = false)
    {
        HeadlessRenderer.Use ();
        var box = new RichTextBox { Width = 300, Height = 120, Multiline = multiline };

        if (text is not null)
            box.Text = text;

        return box;
    }

    // ── SelectionCharOffset ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SelectionCharOffset_is_per_range_and_reaches_the_painted_runs ()
    {
        using var box = Rich ("x2 and y");

        Assert.Equal (0, box.SelectionCharOffset);

        box.Select (1, 1);
        box.SelectionCharOffset = 5;

        Assert.Equal (5, box.SelectionCharOffset);

        // It is a property of the range, not of the control: the rest is still on the baseline.
        box.Select (0, 1);
        Assert.Equal (0, box.SelectionCharOffset);

        // And it reaches the text pipeline as a superscript run.
        var spans = box.Colorizer! (box.Text).ToList ();
        var raised = Assert.Single (spans, s => s.CharOffset > 0);
        Assert.Equal (1, raised.Start);
        Assert.Equal (1, raised.Length);

        box.Select (1, 1);
        box.SelectionCharOffset = -5;
        Assert.Equal (-5, box.SelectionCharOffset);
        Assert.Single (box.Colorizer! (box.Text), s => s.CharOffset < 0);
    }

    // ── SelectionProtected / Protected ──────────────────────────────────────────────────────────────

    [Fact]
    public void Typing_over_protected_text_is_refused_and_raises_Protected ()
    {
        using var box = Rich ("keep me");
        var refused = 0;
        box.Protected += (_, _) => refused++;

        box.Select (0, 4);
        box.SelectionProtected = true;

        Assert.True (box.SelectionProtected);
        Assert.True (box.RangeIsProtected (0, 4));
        Assert.False (box.RangeIsProtected (5, 2));

        // Typing over the protected run changes nothing and announces the refusal.
        box.Select (0, 4);
        box.RaiseKeyPress (new KeyPressEventArgs ('z'));

        Assert.Equal ("keep me", box.Text);
        Assert.Equal (1, refused);

        // Deleting into it is refused too.
        box.Select (4, 0);
        box.RaiseKeyDown (new KeyEventArgs (Keys.Back));

        Assert.Equal ("keep me", box.Text);
        Assert.Equal (2, refused);

        // Unprotected text still edits, and says nothing.
        box.Select (5, 2);
        box.RaiseKeyPress (new KeyPressEventArgs ('z'));

        Assert.Equal ("keep z", box.Text);
        Assert.Equal (2, refused);
    }

    // ── DetectUrls ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DetectUrls_finds_links_and_a_click_reports_the_one_under_the_pointer ()
    {
        using var box = Rich ("see https://example.test/docs now");

        Assert.True (box.DetectUrls);
        var link = Assert.Single (box.DetectedUrls);
        Assert.Equal ("https://example.test/docs", box.Text.Substring (link.Start, link.Length));

        Assert.Equal ("https://example.test/docs", box.LinkAt (link.Start + 3));
        Assert.Null (box.LinkAt (0));

        // A bare www host counts, as upstream's autodetection takes it.
        box.Text = "go to www.example.test today";
        Assert.Equal ("www.example.test", box.LinkAt (7));

        box.DetectUrls = false;
        Assert.Empty (box.DetectedUrls);
        Assert.Null (box.LinkAt (7));
    }

    [Fact]
    public void Clicking_a_detected_link_raises_LinkClicked ()
    {
        using var form = new Form { Size = new Size (400, 200) };
        var box = Rich ("https://example.test/a");
        form.Controls.Add (box);
        form.Show ();
        PaintSurface.RenderOnForm (box).Dispose ();

        var clicked = new List<string> ();
        box.LinkClicked += (_, e) => clicked.Add (e.LinkText);

        // A point inside the link's text.
        var at = box.GetPositionFromCharIndex (4);
        box.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, at.X + 1, at.Y + 2, 0));

        Assert.Equal ("https://example.test/a", Assert.Single (clicked));

        form.Close ();
    }

    // ── RichTextShortcutsEnabled ────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_formatting_shortcuts_toggle_the_selection_and_can_be_turned_off ()
    {
        using var box = Rich ("format me");
        box.Select (0, 6);

        Assert.False (box.SelectionBold);

        box.RaiseKeyDown (new KeyEventArgs (Keys.B | Keys.Control));
        Assert.True (box.SelectionBold);

        box.RaiseKeyDown (new KeyEventArgs (Keys.I | Keys.Control));
        Assert.True (box.SelectionItalic);

        box.RaiseKeyDown (new KeyEventArgs (Keys.U | Keys.Control));
        Assert.True (box.SelectionUnderline);

        // Pressing again toggles back off.
        box.RaiseKeyDown (new KeyEventArgs (Keys.B | Keys.Control));
        Assert.False (box.SelectionBold);

        box.RichTextShortcutsEnabled = false;
        box.RaiseKeyDown (new KeyEventArgs (Keys.B | Keys.Control));
        Assert.False (box.SelectionBold);
    }

    // ── AutoWordSelection ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AutoWordSelection_grows_a_drag_to_whole_words ()
    {
        using var form = new Form { Size = new Size (400, 200) };
        var box = Rich ("alpha beta gamma");
        form.Controls.Add (box);
        form.Show ();
        PaintSurface.RenderOnForm (box).Dispose ();

        // A drag that ends mid-word: without the option the selection stops where it stopped.
        box.Select (7, 2);
        box.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, 5, 5, 0));

        Assert.Equal (7, box.SelectionStart);
        Assert.Equal (2, box.SelectionLength);

        box.AutoWordSelection = true;
        box.Select (7, 2);
        box.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, 5, 5, 0));

        // "beta" whole, from 6 to 10.
        Assert.Equal (6, box.SelectionStart);
        Assert.Equal (4, box.SelectionLength);
        Assert.Equal ("beta", box.SelectedText);

        form.Close ();
    }

    // ── ShowSelectionMargin ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShowSelectionMargin_reserves_a_strip_and_a_click_in_it_selects_the_line ()
    {
        using var form = new Form { Size = new Size (400, 220) };
        var box = Rich ("first line\nsecond line\n", multiline: true);
        form.Controls.Add (box);
        form.Show ();
        PaintSurface.RenderOnForm (box).Dispose ();

        Assert.Equal (0, box.ScaledSelectionMargin);
        var wide = box.WrapWidth;
        var origin = box.TextOrigin.X;

        box.ShowSelectionMargin = true;
        PaintSurface.RenderOnForm (box).Dispose ();

        // The strip takes width from the text and pushes its origin right.
        Assert.True (box.ScaledSelectionMargin > 0);
        Assert.Equal (wide - box.ScaledSelectionMargin, box.WrapWidth);
        Assert.Equal (origin + box.ScaledSelectionMargin, box.TextOrigin.X);

        // A click in the strip selects the whole line rather than moving the caret.
        // GetPositionFromCharIndex answers in logical units, the space the mouse is in (RC-8).
        var second = box.GetPositionFromCharIndex (12);
        box.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 1, second.Y + 2, 0));

        Assert.Equal ("second line\n", box.SelectedText);

        form.Close ();
    }

    // ── ContentsResized ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ContentsResized_reports_the_laid_out_size_when_it_changes ()
    {
        using var form = new Form { Size = new Size (400, 220) };
        var box = Rich ("one", multiline: true);
        form.Controls.Add (box);
        form.Show ();

        var seen = new List<Rectangle> ();
        box.ContentsResized += (_, e) => seen.Add (e.NewRectangle);

        PaintSurface.RenderOnForm (box).Dispose ();
        Assert.NotEmpty (seen);
        var first = seen[^1];
        Assert.True (first.Width > 0 && first.Height > 0, $"measured {first}");

        // Painting again with the same text says nothing more.
        var count = seen.Count;
        PaintSurface.RenderOnForm (box).Dispose ();
        Assert.Equal (count, seen.Count);

        // More lines means a taller content rectangle, and that is reported.
        box.Text = "one\ntwo\nthree\nfour";
        PaintSurface.RenderOnForm (box).Dispose ();

        Assert.True (seen.Count > count, "a changed content size was not reported");
        Assert.True (seen[^1].Height > first.Height, $"{seen[^1]} should be taller than {first}");

        form.Close ();
    }
}
