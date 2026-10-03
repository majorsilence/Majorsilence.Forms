using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

[Collection ("Headless")]
public class RichListBoxTests
{
    private sealed record Post (string Title, string Excerpt);

    private const string Long = "The quick brown fox jumps over the lazy dog and keeps running through the long field until the sun goes down behind the hills, then rests under a tree.";

    private static RichListBox NewList (int maxLines = 0)
    {
        var list = new RichListBox { Location = new Point (10, 10), Size = new Size (360, 400) };
        list.ItemTemplate = item => {
            var post = (Post) item;
            return new[] { new ListItemLine (post.Title) { Bold = true, FontSize = 16 }, new ListItemLine (post.Excerpt) { MaxLines = maxLines } };
        };
        return list;
    }

    private static Form Host (RichListBox list)
    {
        var form = new Form { Size = new Size (400, 460) };
        form.Controls.Add (list);
        form.Show ();
        HeadlessRenderer.CapturePng (form);
        return form;
    }

    [Fact]
    public void Rows_are_as_tall_as_their_wrapped_text_needs ()
    {
        HeadlessRenderer.Use ();
        var list = NewList ();
        list.Items.Add (new Post ("Short", "Brief."));
        list.Items.Add (new Post ("Long", Long));
        using var form = Host (list);

        Assert.True (list.ItemHeightDeviceAt (1) > list.ItemHeightDeviceAt (0));
    }

    [Fact]
    public void MaxLines_caps_how_tall_a_line_can_make_its_row ()
    {
        HeadlessRenderer.Use ();
        var unlimited = NewList ();
        var capped = NewList (maxLines: 1);
        unlimited.Items.Add (new Post ("Post", Long));
        capped.Items.Add (new Post ("Post", Long));
        using var f1 = Host (unlimited);
        using var f2 = Host (capped);

        Assert.True (capped.ItemHeightDeviceAt (0) < unlimited.ItemHeightDeviceAt (0));
    }

    [Fact]
    public void Narrowing_the_list_re_measures_because_the_text_wraps_more ()
    {
        HeadlessRenderer.Use ();
        var list = NewList ();
        list.Items.Add (new Post ("Post", Long));
        using var form = Host (list);
        var wide = list.ItemHeightDeviceAt (0);

        list.Width = 200;
        HeadlessRenderer.CapturePng (form);

        Assert.True (list.ItemHeightDeviceAt (0) > wide);
    }

    [Fact]
    public void RefreshItems_measures_again_when_an_items_text_changed_without_the_count_changing ()
    {
        HeadlessRenderer.Use ();
        var list = NewList ();
        var text = "Brief.";
        list.ItemTemplate = _ => new[] { new ListItemLine (text) };
        list.Items.Add (new Post ("a", "b"));
        using var form = Host (list);
        var before = list.ItemHeightDeviceAt (0);

        text = Long + " " + Long;
        list.RefreshItems ();
        HeadlessRenderer.CapturePng (form);

        Assert.True (list.ItemHeightDeviceAt (0) > before);
    }

    [Fact]
    public void Without_a_template_each_item_shows_its_text_on_one_row ()
    {
        HeadlessRenderer.Use ();
        var list = new RichListBox { Location = new Point (10, 10), Size = new Size (360, 200) };
        list.Items.Add ("plain item");
        using var form = Host (list);

        Assert.True (list.ItemHeightDeviceAt (0) >= list.LogicalToDeviceUnits (list.ItemHeight));
    }

    [Fact]
    public void Selection_still_works_through_ListBox ()
    {
        HeadlessRenderer.Use ();
        var list = NewList ();
        list.Items.Add (new Post ("a", "one"));
        list.Items.Add (new Post ("b", "two"));
        using var form = Host (list);

        list.SelectedIndex = 1;

        Assert.Equal ("b", ((Post) list.SelectedItem!).Title);
    }

    [Fact]
    public void The_selected_row_is_drawn_highlighted_and_the_others_are_not ()
    {
        HeadlessRenderer.Use ();
        var list = NewList ();
        list.Items.Add (new Post ("a", "one"));
        list.Items.Add (new Post ("b", "two"));
        using var form = Host (list);
        list.SelectedIndex = 1;

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form));
        var scale = (double) bitmap.Width / form.Width;
        var caption = form.Height - form.ClientSize.Height;
        SKColor InCard (int row)
        {
            // Just inside the card's left padding, clear of the text, at a quarter of the way down the row.
            var top = 0;
            for (var i = 0; i < row; i++)
                top += list.DeviceToLogicalUnits (list.ItemHeightDeviceAt (i));
            var y = list.Top + top + list.DeviceToLogicalUnits (list.ItemHeightDeviceAt (row)) / 4;
            return bitmap.GetPixel ((int) ((list.Left + 6) * scale), (int) ((y + caption) * scale));
        }

        Assert.NotEqual (InCard (0), InCard (1));
        Assert.InRange (InCard (0).Red, Theme.ControlLowColor.Red - 3, Theme.ControlLowColor.Red + 3);
    }

    [Theory]
    [InlineData ("corner")]
    [InlineData ("padding")]
    [InlineData ("spacing")]
    public void Negative_metrics_are_rejected (string which)
    {
        HeadlessRenderer.Use ();
        var list = new RichListBox ();

        Assert.Throws<ArgumentOutOfRangeException> (() => {
            switch (which) {
                case "corner": list.ItemCornerRadius = -1; break;
                case "padding": list.ItemPadding = -1; break;
                default: list.ItemSpacing = -1; break;
            }
        });
    }
}
