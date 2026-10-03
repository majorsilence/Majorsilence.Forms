using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

[Collection ("Headless")]
public class CardTests
{
    [Fact]
    public void A_new_card_is_a_padded_rounded_bordered_panel ()
    {
        HeadlessRenderer.Use ();
        using var card = new Card ();

        Assert.IsAssignableFrom<Panel> (card);
        Assert.Equal (new Padding (Card.DefaultPaddingSize), card.Padding);
        Assert.Equal (Card.DefaultCornerRadius, card.CornerRadius);
        Assert.True (card.Style.Border.GetTopLeftRadius () > 0);
        Assert.True (card.Style.Border.Top.GetWidth () > 0);
    }

    [Fact]
    public void CornerRadius_is_ordinary_style_and_rejects_a_negative_value ()
    {
        HeadlessRenderer.Use ();
        using var card = new Card { CornerRadius = 4 };

        Assert.Equal (4, card.Style.Border.GetRadius ());
        card.CornerRadius = 0;
        Assert.False (card.Style.Border.HasRadius);
        Assert.Throws<ArgumentOutOfRangeException> (() => card.CornerRadius = -1);
    }

    [Fact]
    public void Cards_do_not_share_style_so_restyling_one_leaves_the_others ()
    {
        HeadlessRenderer.Use ();
        using var a = new Card ();
        using var b = new Card ();

        a.CornerRadius = 2;

        Assert.Equal (Card.DefaultCornerRadius, b.CornerRadius);
    }

    [Fact]
    public void A_card_draws_rounded_corners_and_a_filled_surface ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 260) };
        var card = new Card { Location = new Point (20, 20), Size = new Size (200, 120), CornerRadius = 24 };
        form.Controls.Add (card);
        form.Show ();

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form));
        var scale = (double) bitmap.Width / form.Width;
        var caption = form.Height - form.ClientSize.Height;
        SKColor At (int x, int y) => bitmap.GetPixel ((int) (x * scale), (int) ((y + caption) * scale));

        // The very corner of a 24px-radius card is outside the curve, so it shows the form behind it; the middle shows the card.
        var corner = At (card.Left + 1, card.Top + 1);
        var middle = At (card.Left + card.Width / 2, card.Top + card.Height - 10);

        Assert.NotEqual (middle, corner);
        Assert.InRange (middle.Red, Theme.ControlLowColor.Red - 2, Theme.ControlLowColor.Red + 2);
        Assert.InRange (corner.Red, form.BackColor.R - 2, form.BackColor.R + 2);
    }

    // ── Wrapping already works with standard controls; pin it so the recipe in docs/mobile-layout.md stays true ──

    private const string Long = "The quick brown fox jumps over the lazy dog and keeps running through the long field until the sun goes down behind the hills.";

    [Fact]
    public void A_label_reports_a_taller_preferred_height_the_narrower_it_is_asked_to_wrap ()
    {
        HeadlessRenderer.Use ();
        using var label = new Label { Text = Long, AutoSize = false };

        var wide = label.GetPreferredSize (new Size (600, 0));
        var narrow = label.GetPreferredSize (new Size (150, 0));

        Assert.True (narrow.Height > wide.Height * 2, $"narrow {narrow} should be much taller than wide {wide}");
        Assert.True (narrow.Width < wide.Width, $"narrow {narrow} should be narrower than wide {wide}");
    }

    [Fact]
    public void An_auto_sized_label_with_a_maximum_width_wraps_once_it_is_laid_out ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var wrapped = new Label { Text = Long, AutoSize = true, MaximumSize = new Size (200, 0), Location = new Point (10, 10) };
        var single = new Label { Text = "x", AutoSize = true, Location = new Point (10, 150) };
        form.Controls.Add (wrapped);
        form.Controls.Add (single);
        form.Show ();
        HeadlessRenderer.CapturePng (form);

        Assert.True (wrapped.Width <= 200, $"width {wrapped.Width}");
        Assert.True (wrapped.Height > single.Height * 2, $"wrapped {wrapped.Size} vs one line {single.Size}");
    }

    [Fact]
    public void A_top_down_flow_panel_without_wrapping_sizes_labels_to_its_width_and_wraps_their_text ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 400) };
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Location = new Point (0, 0), Size = new Size (300, 300) };
        var longLabel = new Label { Text = Long, AutoSize = true };
        var shortLabel = new Label { Text = "short", AutoSize = true };
        flow.Controls.Add (longLabel);
        flow.Controls.Add (shortLabel);
        form.Controls.Add (flow);
        form.Show ();
        HeadlessRenderer.CapturePng (form);

        Assert.True (longLabel.Width <= flow.ClientSize.Width);
        Assert.True (longLabel.Height > shortLabel.Height, "the long label wrapped onto more lines than the short one");
    }
}
