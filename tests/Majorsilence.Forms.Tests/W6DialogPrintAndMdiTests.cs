using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Printing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, eleventh chunk: the task dialog's icon, links, footnote, progress bar, expander
// position, shield buttons, RTL and content sizing; printing through a real PrintController, with
// PrintAction, OriginAtMargins and a print preview that actually shows pages; SplitterIncrement; and
// the MDI window list.
[Collection ("Headless")]
public class W6DialogPrintAndMdiTests
{
    // ── TaskDialog ──────────────────────────────────────────────────────────────────────────────────

    private static Form Built (TaskDialogPage page)
    {
        HeadlessRenderer.Use ();
        return TaskDialog.Build (page, _ => { });
    }

    private static IEnumerable<T> Of<T> (Form form) => form.Controls.GetAllControls ().OfType<T> ();

    [Fact]
    public void The_page_icon_and_a_footnote_icon_are_drawn ()
    {
        using var plain = Built (new TaskDialogPage { Heading = "Plain" });
        Assert.DoesNotContain (Of<Control> (plain), c => c.GetType ().Name == "TaskDialogIconBox");

        using var form = Built (new TaskDialogPage {
            Heading = "Careful",
            Icon = TaskDialogIcon.Warning,
            Footnote = new TaskDialogFootnote ("small print") { Icon = TaskDialogIcon.Information },
        });

        var boxes = Of<Control> (form).Where (c => c.GetType ().Name == "TaskDialogIconBox").ToList ();
        Assert.Equal (2, boxes.Count);

        // The page's glyph is the warning triangle and the footnote's the information disc.
        using var page_icon = PaintSurface.Render (boxes[0]);
        Assert.True (CountIn (page_icon, MessageGlyphs.WarningColor) > 0, "no warning glyph");

        using var note_icon = PaintSurface.Render (boxes[1]);
        Assert.True (CountIn (note_icon, MessageGlyphs.InformationColor) > 0, "no information glyph");

        // The footnote text is shown at all now, which it never was.
        Assert.Contains (Of<Label> (form), l => l.Text == "small print");
    }

    [Fact]
    public void EnableLinks_turns_markup_into_links_and_raises_LinkClicked ()
    {
        var page = new TaskDialogPage { Text = "See the <a href=\"https://example.test/docs\">manual</a> first." };

        using (var plain = Built (page)) {
            // Off (the default), the markup is stripped but the text is a plain label.
            Assert.Empty (Of<LinkLabel> (plain));
            Assert.Contains (Of<Label> (plain), l => l.Text == "See the manual first.");
        }

        page.EnableLinks = true;
        var clicked = new List<string> ();
        page.LinkClicked += (_, e) => clicked.Add (e.LinkHref);

        using var form = Built (page);
        var label = Assert.Single (Of<LinkLabel> (form));

        Assert.Equal ("See the manual first.", label.Text);
        var link = Assert.Single (label.Links);
        Assert.Equal ("See the ".Length, link.Start);
        Assert.Equal ("manual".Length, link.Length);

        label.DriveLinkClick (link);
        Assert.Equal ("https://example.test/docs", Assert.Single (clicked));
    }

    [Fact]
    public void The_progress_bar_carries_its_range_value_and_marquee ()
    {
        using var form = Built (new TaskDialogPage {
            Text = "Working",
            ProgressBar = new TaskDialogProgressBar { Minimum = 10, Maximum = 50, Value = 30 },
        });

        var bar = Assert.Single (Of<ProgressBar> (form));
        Assert.Equal (10, bar.Minimum);
        Assert.Equal (50, bar.Maximum);
        Assert.Equal (30, bar.Value);
        Assert.Equal (ProgressBarStyle.Blocks, bar.Style);

        using var marquee = Built (new TaskDialogPage {
            ProgressBar = new TaskDialogProgressBar (TaskDialogProgressBarState.Marquee) { MarqueeSpeed = 40 },
        });

        var moving = Assert.Single (Of<ProgressBar> (marquee));
        Assert.Equal (ProgressBarStyle.Marquee, moving.Style);
        Assert.Equal (40, moving.MarqueeAnimationSpeed);
    }

    [Fact]
    public void ShowShieldIcon_draws_a_shield_on_the_button ()
    {
        using var form = Built (new TaskDialogPage {
            Buttons = { new TaskDialogButton ("Continue") { ShowShieldIcon = true }, new TaskDialogButton ("Cancel") },
        });

        var buttons = Of<Button> (form).ToList ();
        Assert.Equal (2, buttons.Count);

        using var shielded = PaintSurface.Render (buttons[0]);
        using var plain = PaintSurface.Render (buttons[1]);

        Assert.True (CountIn (shielded, MessageGlyphs.ShieldColor) > 0, "no shield on the elevating button");
        Assert.Equal (0, CountIn (plain, MessageGlyphs.ShieldColor));
    }

    [Fact]
    public void A_command_link_is_a_full_width_button_showing_its_description ()
    {
        using var form = Built (new TaskDialogPage {
            Text = "Pick one",
            Buttons = {
                new TaskDialogCommandLinkButton ("Upgrade now", "Takes about five minutes."),
                new TaskDialogButton ("Cancel"),
            },
        });

        var buttons = Of<Button> (form).ToList ();
        var link = buttons.First (b => b.Text == "Upgrade now");
        var plain = buttons.First (b => b.Text == "Cancel");

        // The command link spans the dialog and is taller; the plain button keeps the button row.
        Assert.True (link.Width > plain.Width * 2, $"command link is {link.Width} wide, plain is {plain.Width}");
        Assert.True (link.Height > plain.Height, $"command link is {link.Height} tall, plain is {plain.Height}");
        Assert.True (link.Top < plain.Top, "the command link should sit above the button row");

        // The description is drawn under the caption: the same button without one has less ink in its
        // lower half. Compared against a second dialog rather than against zero, because the caption
        // the base class centres bleeds into that half whether or not a description is there.
        using var without = Built (new TaskDialogPage {
            Text = "Pick one",
            Buttons = { new TaskDialogCommandLinkButton ("Upgrade now"), new TaskDialogButton ("Cancel") },
        });

        static int LowerInk (Button button)
        {
            using var painted = PaintSurface.Render (button);
            var background = painted.GetPixel (1, painted.Height - 2);
            var ink = 0;

            for (var y = painted.Height / 2; y < painted.Height - 2; y++)
                for (var x = 2; x < painted.Width - 2; x++)
                    if (painted.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }

        var described = LowerInk (link);
        var bare = LowerInk (Of<Button> (without).First (b => b.Text == "Upgrade now"));

        Assert.True (described > bare + 20, $"description ink {described} vs {bare} without one");
    }

    [Fact]
    public void The_expander_sits_before_or_after_the_footnote_as_Position_asks ()
    {
        TaskDialogPage Page (TaskDialogExpanderPosition position) => new TaskDialogPage {
            Text = "Body",
            Footnote = new TaskDialogFootnote ("small print"),
            Expander = new TaskDialogExpander ("details") { Position = position },
        };

        static (int toggle, int note) Tops (Form form)
            => (Of<Button> (form).First (b => b.Text!.Contains ("details")).Top,
                Of<Label> (form).First (l => l.Text == "small print").Top);

        using var after_text = Built (Page (TaskDialogExpanderPosition.AfterText));
        var (toggle_first, note_last) = Tops (after_text);
        Assert.True (toggle_first < note_last, $"expander at {toggle_first}, footnote at {note_last}");

        using var after_note = Built (Page (TaskDialogExpanderPosition.AfterFootnote));
        var (toggle_last, note_first) = Tops (after_note);
        Assert.True (toggle_last > note_first, $"expander at {toggle_last}, footnote at {note_first}");
    }

    [Fact]
    public void RightToLeftLayout_and_SizeToContent_shape_the_dialog ()
    {
        using var plain = Built (new TaskDialogPage { Heading = "Hi" });
        Assert.Equal (RightToLeft.No, plain.RightToLeft);

        using var mirrored = Built (new TaskDialogPage { Heading = "Hi", RightToLeftLayout = true });
        Assert.Equal (RightToLeft.Yes, mirrored.RightToLeft);

        // A short message sizes down to its text; the fixed width is what it used to be regardless.
        using var sized = Built (new TaskDialogPage { Heading = "Hi", SizeToContent = true });
        Assert.True (sized.Width < plain.Width, $"SizeToContent gave {sized.Width}, fixed is {plain.Width}");

        // A long message cannot grow past the fixed width.
        using var wide = Built (new TaskDialogPage { Heading = new string ('x', 400), SizeToContent = true });
        Assert.Equal (plain.Width, wide.Width);
    }

    // ── Printing ────────────────────────────────────────────────────────────────────────────────────

    private sealed class RecordingController : PrintController
    {
        internal readonly List<string> Calls = new ();
        internal PrintAction StartAction;

        public override void OnStartPrint (PrintDocument document, PrintEventArgs e)
        {
            StartAction = e.PrintAction;
            Calls.Add ("start");
        }

        public override Majorsilence.Forms.Drawing.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e)
        {
            Calls.Add ("page");
            return null;
        }

        public override void OnEndPage (PrintDocument document, PrintPageEventArgs e) => Calls.Add ("endpage");

        public override void OnEndPrint (PrintDocument document, PrintEventArgs e) => Calls.Add ("end");
    }

    private static PrintDocument TwoPageDocument (Action<PrintPageEventArgs>? draw = null)
    {
        var document = new PrintDocument ();
        var page = 0;

        document.PrintPage += (_, e) => {
            draw?.Invoke (e);
            e.HasMorePages = ++page < 2;
        };

        return document;
    }

    [Fact]
    public void Every_page_of_a_job_goes_through_the_PrintController ()
    {
        using var document = TwoPageDocument ();
        var controller = new RecordingController ();
        document.PrintController = controller;

        using var stream = new MemoryStream ();
        document.PrintToPdf (stream);

        Assert.Equal (new[] { "start", "page", "endpage", "page", "endpage", "end" }, controller.Calls);
        Assert.Equal (PrintAction.PrintToFile, controller.StartAction);
        Assert.True (stream.Length > 0, "no PDF was written");
    }

    [Fact]
    public void A_preview_controller_captures_the_pages_and_the_action_says_so ()
    {
        using var document = TwoPageDocument ();
        var preview = new PreviewPrintController ();
        PrintAction? seen = null;
        document.BeginPrint += (_, e) => seen = ((PrintEventArgs) e).PrintAction;
        document.PrintController = preview;

        document.RunThroughController (PrintAction.PrintToPreview);

        var pages = preview.GetPreviewPageInfo ();
        Assert.Equal (2, pages.Length);
        Assert.Equal (PrintAction.PrintToPreview, seen);
        Assert.True (pages[0].PhysicalSize.Width > 0);
    }

    [Fact]
    public void OriginAtMargins_moves_the_page_origin_to_the_margin_corner ()
    {
        Rectangle margins = default;
        var marks = new List<SKPoint> ();

        void Draw (PrintPageEventArgs e)
        {
            margins = e.MarginBounds;
            e.SkiaGraphics.Canvas.GetLocalClipBounds (out var _);
            marks.Add (e.SkiaGraphics.Canvas.TotalMatrix.MapPoint (0, 0));
        }

        using (var plain = TwoPageDocument (Draw)) {
            plain.DefaultPageSettings.Margins = new Margins (100, 100, 100, 100);
            plain.PrintController = new PreviewPrintController ();
            plain.RunThroughController (PrintAction.PrintToPreview);
        }

        var unshifted = marks[0];
        Assert.True (margins.Left > 0, "the margin bounds should start at the margin without the shift");

        marks.Clear ();

        using (var shifted = TwoPageDocument (Draw)) {
            shifted.DefaultPageSettings.Margins = new Margins (100, 100, 100, 100);
            shifted.OriginAtMargins = true;
            shifted.PrintController = new PreviewPrintController ();
            shifted.RunThroughController (PrintAction.PrintToPreview);
        }

        // The origin moved by exactly the margin, and the reported margin bounds are now relative to it.
        Assert.Equal (0, margins.Left);
        Assert.Equal (0, margins.Top);
        Assert.True (marks[0].X > unshifted.X, $"origin did not move: {unshifted} -> {marks[0]}");
    }

    [Fact]
    public void The_preview_control_lays_its_document_pages_out_in_a_grid ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 500, Height = 400 };
        using var document = TwoPageDocument ();
        var preview = new PrintPreviewControl { Width = 400, Height = 300 };
        form.Controls.Add (preview);
        form.Show ();

        Assert.Empty (preview.Pages);

        preview.Document = document;
        Assert.Equal (2, preview.Pages.Count);

        // One row, one column: only the first page has a box.
        Assert.False (preview.PageBoundsAt (0).IsEmpty);
        Assert.True (preview.PageBoundsAt (1).IsEmpty);

        preview.Columns = 2;
        var left = preview.PageBoundsAt (0);
        var right = preview.PageBoundsAt (1);
        Assert.False (right.IsEmpty);
        Assert.True (right.Left > left.Left, "the second page should sit to the right of the first");

        // AutoZoom fits the cell; a small fixed Zoom is smaller than that.
        Assert.True (preview.AutoZoom);
        var fitted = preview.PageBoundsAt (0).Width;
        preview.AutoZoom = false;
        preview.Zoom = 0.05;
        Assert.True (preview.PageBoundsAt (0).Width < fitted, "Zoom was ignored with AutoZoom off");

        // And it paints the pages rather than nothing.
        preview.AutoZoom = true;
        using var bitmap = PaintSurface.Render (preview);
        Assert.True (CountIn (bitmap, SKColors.White) > 0, "no page sheet was painted");
    }

    [Fact]
    public void The_preview_dialog_hosts_the_control_and_forwards_its_settings ()
    {
        HeadlessRenderer.Use ();
        using var document = TwoPageDocument ();
        using var dialog = new PrintPreviewDialog { Document = document, UseAntiAlias = false };

        Assert.Same (document, dialog.PrintPreviewControl.Document);
        Assert.False (dialog.PrintPreviewControl.UseAntiAlias);

        dialog.UseAntiAlias = true;
        Assert.True (dialog.PrintPreviewControl.UseAntiAlias);
        Assert.Equal (2, dialog.PrintPreviewControl.Pages.Count);
    }

    // ── SplitContainer ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SplitterIncrement_snaps_a_drag_to_whole_steps ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var split = new SplitContainer { Width = 300, Height = 200, SplitterDistance = 100 };
        form.Controls.Add (split);
        form.Show ();

        // The drag delta is DEVICE, as a backend delivers it; the splitter distance is logical (RC-8).
        void Drag (int logical) => split.DriveSplitterDrag (new Point (-split.LogicalToDeviceUnits (logical), 0));

        Drag (7);
        Assert.Equal (107, split.SplitterDistance);

        split.SplitterDistance = 100;
        split.SplitterIncrement = 10;

        // 7 from the start of the drag rounds down to 0 steps; 13 rounds down to one 10-pixel step.
        Drag (7);
        Assert.Equal (100, split.SplitterDistance);

        Drag (13);
        Assert.Equal (110, split.SplitterDistance);
    }

    // ── MDI window list ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_window_list_item_lists_the_MDI_children_and_activates_one ()
    {
        HeadlessRenderer.Use ();
        using var parent = new Form { IsMdiContainer = true, Width = 500, Height = 400 };
        var strip = new MenuStrip ();
        var window = new ToolStripMenuItem ("Window");
        window.DropDownItems.Add (new ToolStripMenuItem ("Cascade"));
        strip.Items.Add (window);
        parent.Controls.Add (strip);
        strip.MdiWindowListItem = window;
        parent.Show ();

        using var first = new Form { Text = "First", Size = new Size (200, 150), MdiParent = parent };
        using var second = new Form { Text = "Second", Size = new Size (200, 150), MdiParent = parent };
        first.Show ();
        second.Show ();

        window.PopulateMdiWindowList ();

        var entries = window.DropDownItems.Cast<MenuItem> ().Where (i => i.IsGeneratedWindowListEntry).ToList ();

        // A divider, then one entry per child; the application's own item is untouched.
        Assert.Equal ("Cascade", window.DropDownItems[0].Text);
        Assert.Equal (3, entries.Count);
        Assert.IsType<MenuSeparatorItem> (entries[0]);
        Assert.Equal (new[] { "First", "Second" }, entries.Skip (1).Select (i => i.Text).ToArray ());

        // The active child is ticked, and clicking the other one activates it.
        Assert.Same (second, parent.ActiveMdiChild);
        Assert.False (entries[1].Checked);
        Assert.True (entries[2].Checked);
        Assert.True (((ToolStripMenuItem) entries[1]).IsMdiWindowListEntry);

        entries[1].PerformClick ();
        Assert.Same (first, parent.ActiveMdiChild);

        // Re-opening rebuilds rather than appending, and a closed child drops out.
        second.Close ();
        window.PopulateMdiWindowList ();

        var after = window.DropDownItems.Cast<MenuItem> ().Where (i => i.IsGeneratedWindowListEntry).ToList ();
        Assert.Equal (2, after.Count);
        Assert.Equal ("First", after[1].Text);
    }

    [Fact]
    public void A_legacy_MdiList_item_is_filled_the_same_way ()
    {
        HeadlessRenderer.Use ();
        using var parent = new Form { IsMdiContainer = true, Width = 500, Height = 400 };
        var strip = new MenuStrip ();
        var window = new ToolStripMenuItem ("Window") { MdiList = true };
        strip.Items.Add (window);
        parent.Controls.Add (strip);
        parent.Show ();

        using var child = new Form { Text = "Only", Size = new Size (200, 150), MdiParent = parent };
        child.Show ();

        window.PopulateMdiWindowList ();

        var entry = Assert.Single (window.DropDownItems.Cast<MenuItem> (), i => i.IsGeneratedWindowListEntry);
        Assert.Equal ("Only", entry.Text);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static int CountIn (SKBitmap bitmap, SKColor colour)
    {
        var count = 0;

        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++) {
                var p = bitmap.GetPixel (x, y);

                if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue && p.Alpha == colour.Alpha)
                    count++;
            }

        return count;
    }
}
