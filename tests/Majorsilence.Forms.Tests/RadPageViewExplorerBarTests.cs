using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadPageView in its ExplorerBar mode is laid out, painted and driven as Telerik's is under its default theme.
    // Measured on UI for WinForms 2026.2.520 (.NET 10), a 271 x 843 page view set up as a designer file does: seven
    // pages, the first five with a 50px image, the second and third expanded (IsContentVisible), the third selected,
    // PageBackColor White, and every page but the last with a BackColor of its own:
    //   - the items stack last page first under a title band (rows 0 to 24), the first item's line on row 25 and each
    //     next one 64 lower; the item under an open page starts on that page's last row, 300 below its line;
    //   - an open page sits 4 in from the sides and 4 below its item's line, 244 x 292 beside a scroll bar, 261 wide
    //     without one; the scroll bar at (253, 26), 17 x 816, Maximum 1047 (the stack's 1048 less one), LargeChange 817;
    //   - the title band #E3EFFF to #B4D5FF inside a white line, the frame and item lines #6593CF, the items #E3EFFF
    //     to #C4DDFF over the top 40% then #ADD1FF to #C0DBFF, the selected item #FFD9AA..#FFBB6E then
    //     #FFAB3F..#FEE17A, the content white round the page;
    //   - clicking an item selects its page and then expands or collapses it (SelectedPageChanged, then PageExpanding
    //     and PageExpanded); expanding scrolls the item to the top, as far as the scroll bar goes; a wheel notch is 15px;
    //   - PageBackColor reaches every page with no BackColor of its own, pages added later included.
    // Here the mode did not exist: ViewMode, PageBackColor, Image and IsContentVisible were missing, and the designer
    // file did not compile.
    [Collection ("Headless")]
    public class RadPageViewExplorerBarTests
    {
        private static readonly Color PageBlue = Color.FromArgb (233, 240, 249);

        private static readonly string[] Captions = {
            "Inventory Email Templates", "Schedule Maintenance Email Template", "Service Request Email Templates",
            "Task Email Templates", "Work Order Email Templates", "Project Email Templates", "Financial" };

        private static Majorsilence.Forms.Drawing.Image Image (SKColor colour)
        {
            var bitmap = new SKBitmap (50, 50);
            bitmap.Erase (colour);
            return bitmap!;
        }

        private static readonly SKColor[] ImageColours = { SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.Orange, SKColors.Purple };

        // In a designer file's order: the pages added, then the view's properties, then each page's.
        private static RadPageView EmailTemplatesView (int height = 843, params int[] expanded)
        {
            var view = new RadPageView { Location = new Point (0, 0) };
            var pages = Captions.Select (c => new RadPageViewPage { Text = c }).ToArray ();

            foreach (var page in pages)
                view.Controls.Add (page);

            view.ItemSizeMode = PageViewItemSizeMode.EqualWidth | PageViewItemSizeMode.EqualHeight;
            view.PageBackColor = Color.White;
            view.SelectedPage = pages[2];
            view.Size = new Size (271, height);
            view.ViewMode = PageViewMode.ExplorerBar;

            for (var i = 0; i < pages.Length; i++) {
                if (i < 6)
                    pages[i].BackColor = PageBlue;
                if (i < 5)
                    pages[i].Image = Image (ImageColours[i]);
                if (expanded.Contains (i))
                    pages[i].IsContentVisible = true;
                pages[i].ItemSize = new SizeF (254f, 66f);
                pages[i].Controls.Add (new Button { Text = "btn" + i, Location = new Point (4, 10), Size = new Size (195, 48) });
            }

            return view;
        }

        private static T WithForm<T> (RadPageView view, Func<Form, T> body)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();

                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (view.Width, view.Height), BackColor = PageBlue };
                form.Controls.Add (view);
                form.Show ();
                view.PerformLayout ();

                return body (form);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static RadPageViewPage Page (RadPageView view, int index) => (RadPageViewPage) view.Pages[index];

        private static SKColor C (uint rgb) => new SKColor (0xFF000000 | rgb);

        private static void Near (SKColor expected, SKColor actual, int tolerance = 3)
            => Assert.True (Math.Abs (expected.Red - actual.Red) <= tolerance && Math.Abs (expected.Green - actual.Green) <= tolerance
                            && Math.Abs (expected.Blue - actual.Blue) <= tolerance, $"Expected about {expected}, got {actual}.");

        [Fact]
        public void The_mode_and_its_members_have_Telerik_s_values_and_defaults ()
        {
            var view = new RadPageView ();
            var page = new RadPageViewPage ();

            Assert.Equal (PageViewMode.Strip, view.ViewMode);
            Assert.Equal (0, (int) PageViewMode.Strip);
            Assert.Equal (3, (int) PageViewMode.ExplorerBar);
            Assert.Equal (6, (int) PageViewMode.OfficeNavigationBar);
            Assert.True (view.PageBackColor.IsEmpty);
            Assert.False (page.IsContentVisible);
            Assert.Null (page.Image);
            Assert.Equal (ContentAlignment.MiddleLeft, new RadButtonElement ().ImageAlignment);
        }

        [Fact]
        public void The_items_stack_last_page_first_with_the_open_pages_under_them ()
        {
            var view = EmailTemplatesView (expanded: new[] { 1, 2 });

            WithForm (view, _ => {
                // Financial, Project, Work Order, Task, Service Request (open), Schedule Maintenance (open), Inventory.
                Assert.Equal (new Rectangle (0, 25, 253, 65), view.ExplorerItemBounds (Page (view, 6)));
                Assert.Equal (89, view.ExplorerItemBounds (Page (view, 5)).Top);
                Assert.Equal (153, view.ExplorerItemBounds (Page (view, 4)).Top);
                Assert.Equal (217, view.ExplorerItemBounds (Page (view, 3)).Top);
                Assert.Equal (281, view.ExplorerItemBounds (Page (view, 2)).Top);
                Assert.Equal (new Rectangle (5, 349, 244, 292), Page (view, 2).Bounds);

                // Under the open page: from its last row, down to its own line 65 lower.
                Assert.Equal (new Rectangle (0, 644, 253, 66), view.ExplorerItemBounds (Page (view, 1)));
                Assert.Equal (new Rectangle (5, 713, 244, 292), Page (view, 1).Bounds);
                Assert.Equal (1008, view.ExplorerItemBounds (Page (view, 0)).Top);

                Assert.True (Page (view, 1).Visible);
                Assert.True (Page (view, 2).Visible);
                Assert.False (Page (view, 0).Visible);
                Assert.False (Page (view, 6).Visible);

                var bar = view.ExplorerScrollBar;
                Assert.NotNull (bar);
                Assert.Equal (new Rectangle (253, 26, 17, 816), bar!.Bounds);
                Assert.Equal (1047, bar.Maximum);
                Assert.Equal (817, bar.LargeChange);
                Assert.Equal (0, bar.Value);
                return 0;
            });
        }

        [Fact]
        public void Without_a_scroll_bar_the_items_and_pages_run_the_full_width ()
        {
            var view = EmailTemplatesView (expanded: 4);

            WithForm (view, _ => {
                Assert.Null (view.ExplorerScrollBar);
                Assert.Equal (new Rectangle (0, 153, 271, 65), view.ExplorerItemBounds (Page (view, 4)));
                Assert.Equal (new Rectangle (5, 221, 261, 292), Page (view, 4).Bounds);
                Assert.Equal (new Rectangle (0, 516, 271, 66), view.ExplorerItemBounds (Page (view, 3)));
                Assert.Equal (581, view.ExplorerItemBounds (Page (view, 2)).Top);
                return 0;
            });
        }

        // Without images the items are as high as the caption's line plus 17: 32 in Segoe UI 8.25pt bold, which only
        // Windows has.
        [Fact]
        public void Without_images_the_items_follow_the_caption_font ()
        {
            var view = new RadPageView { Size = new Size (271, 500), ViewMode = PageViewMode.ExplorerBar };

            foreach (var caption in new[] { "one", "two", "three" })
                view.Pages.Add (new RadPageViewPage { Text = caption, ItemSize = new SizeF (254f, 66f) });

            WithForm (view, _ => {
                var first = view.ExplorerItemBounds (Page (view, 2));
                var second = view.ExplorerItemBounds (Page (view, 1));

                Assert.Equal (25, first.Top);
                Assert.Equal (first.Height - 1, second.Top - first.Top);
                Assert.True (first.Height < 66, "The designer's ItemSize is not read back.");

                if (OperatingSystem.IsWindows ())
                    Assert.Equal (31, first.Height);
                return 0;
            });
        }

        [Fact]
        public void It_paints_Telerik_s_title_band_items_and_content ()
        {
            var view = EmailTemplatesView (expanded: 4);

            WithForm (view, form => {
                var scale = view.LogicalToDeviceUnits (100) / 100f;
                var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 271, 843));
                SKColor At (int x, int y) => bitmap.GetPixel ((int) Math.Round (x * scale), (int) Math.Round (y * scale));

                // The frame, and the title band's white line and fill.
                Assert.Equal (C (0x6593CF), At (0, 300));
                Assert.Equal (C (0x6593CF), At (270, 300));
                Assert.Equal (C (0x6593CF), At (200, 0));
                Assert.Equal (C (0x6593CF), At (200, 842));
                Assert.Equal (SKColors.White, At (200, 1));
                Assert.Equal (SKColors.White, At (200, 24));
                Near (C (0xDFEDFF), At (200, 2), 5);
                Near (C (0xBAD8FF), At (200, 23), 5);

                // An item: its lines, then the two halves of its fill.
                Assert.Equal (C (0x6593CF), At (200, 25));
                Assert.Equal (C (0x6593CF), At (200, 89));
                Near (C (0xE1EEFF), At (200, 26));
                Near (C (0xC6DEFF), At (200, 49));
                Near (C (0xADD1FF), At (200, 51));
                Near (C (0xBFDAFF), At (200, 88));

                // The selected item (Service Request, stacked fifth: 4 x 64 and the open page's 300 under the title band).
                Near (C (0xFFD7A5), At (200, 25 + 64 * 4 + 300 + 1), 4);
                Near (C (0xFFAB3F), At (200, 25 + 64 * 4 + 300 + 26), 4);

                // Its image at (5, 7) in the item: the Work Order page's purple.
                Assert.Equal (SKColors.Purple, At (5, 153 + 7));
                Assert.Equal (SKColors.Purple, At (54, 153 + 56));

                // The open page: white round it, its own colour inside.
                Assert.Equal (SKColors.White, At (240, 218));
                Assert.Equal (SKColors.White, At (240, 515));
                Assert.Equal (SKColors.White, At (2, 400));
                Assert.Equal (SKColors.White, At (268, 400));
                Assert.Equal (C (0xE9F0F9), At (240, 221));
                Assert.Equal (C (0xE9F0F9), At (240, 512));

                // Below the stack: the page view's own (ambient) colour.
                Assert.Equal (C (0xE9F0F9), At (200, 800));

                // The caption, dark on the item; the title band's, the selected page's.
                if (OperatingSystem.IsWindows ()) {
                    Assert.True (Ink (bitmap, scale, 56, 268, 218, 280), "No caption on the Task item.");
                    Assert.True (Ink (bitmap, scale, 4, 268, 2, 24), "No caption in the title band.");
                }

                return 0;
            });
        }

        private static bool Ink (SKBitmap bitmap, float scale, int x0, int x1, int y0, int y1)
        {
            for (var x = (int) (x0 * scale); x < (int) (x1 * scale); x++)
                for (var y = (int) (y0 * scale); y < (int) (y1 * scale); y++) {
                    var p = bitmap.GetPixel (x, y);
                    if (p.Red < 100 && p.Blue > 100)
                        return true;
                }

            return false;
        }

        [Fact]
        public void Clicking_an_item_selects_its_page_then_expands_it_and_clicking_again_collapses_it ()
        {
            var view = EmailTemplatesView ();
            var events = new List<string> ();

            WithForm (view, form => {
                view.SelectedPageChanged += (_, _) => events.Add ("SelectedPageChanged " + view.SelectedPage!.Text);
                view.PageExpanding += (_, e) => events.Add ("PageExpanding " + e.Page!.Text);
                view.PageExpanded += (_, e) => events.Add ("PageExpanded " + e.Page!.Text);
                view.PageCollapsing += (_, e) => events.Add ("PageCollapsing " + e.Page!.Text);
                view.PageCollapsed += (_, e) => events.Add ("PageCollapsed " + e.Page!.Text);

                // Task, the fourth item down.
                HeadlessInput.Click (form, WindowPoint.In (view, 150, 217 + 32));

                Assert.Equal (new[] { "SelectedPageChanged Task Email Templates", "PageExpanding Task Email Templates", "PageExpanded Task Email Templates" }, events);
                Assert.True (Page (view, 3).IsContentVisible);
                Assert.True (Page (view, 3).Visible);
                Assert.Equal (new Rectangle (5, 285, 261, 292), Page (view, 3).Bounds);

                // A click on the title band in between, so the next is a click of its own and not a double click.
                HeadlessInput.Click (form, WindowPoint.In (view, 150, 10));
                events.Clear ();
                HeadlessInput.Click (form, WindowPoint.In (view, 150, 217 + 32));

                Assert.Equal (new[] { "PageCollapsing Task Email Templates", "PageCollapsed Task Email Templates" }, events);
                Assert.False (Page (view, 3).IsContentVisible);
                Assert.False (Page (view, 3).Visible);
                return 0;
            });
        }

        // Measured: a double click on a collapsed item expands it once; its second click does not collapse it again.
        [Fact]
        public void A_double_click_expands_the_page_once ()
        {
            var view = EmailTemplatesView ();

            WithForm (view, form => {
                var at = WindowPoint.In (view, 150, 217 + 32);
                HeadlessInput.Click (form, at);
                HeadlessInput.Click (form, at);

                Assert.True (Page (view, 3).IsContentVisible);
                return 0;
            });
        }

        [Fact]
        public void A_cancelled_expand_leaves_the_page_collapsed ()
        {
            var view = EmailTemplatesView ();

            WithForm (view, _ => {
                view.PageExpanding += (_, e) => e.Cancel = true;
                Page (view, 3).IsContentVisible = true;

                Assert.False (Page (view, 3).IsContentVisible);
                Assert.False (Page (view, 3).Visible);
                return 0;
            });
        }

        // Measured: in a 271 x 500 view, expanding a page scrolls its item to the top, as far as the scroll bar goes.
        [Fact]
        public void Expanding_scrolls_the_item_to_the_top_and_the_wheel_scrolls_15_a_notch ()
        {
            var view = EmailTemplatesView (height: 500, expanded: 0);

            WithForm (view, form => {
                Assert.Equal (0, view.ExplorerScrollOffset);

                // Task's item starts 192 down the stack; the stack is 7 x 64 + 2 x 300, the room 474.
                Page (view, 3).IsContentVisible = true;
                Assert.Equal (192, view.ExplorerScrollOffset);
                Assert.Equal (25, view.ExplorerItemBounds (Page (view, 3)).Top);
                Assert.Equal (192, view.ExplorerScrollBar!.Value);

                // Financial's, at the top of the stack: back to the start.
                Page (view, 6).IsContentVisible = true;
                Assert.Equal (0, view.ExplorerScrollOffset);

                HeadlessRenderer.MouseWheel (form, WindowPoint.In (view, 150, 30).X, WindowPoint.In (view, 150, 30).Y, -120);
                Assert.Equal (15, view.ExplorerScrollOffset);
                Assert.Equal (10, view.ExplorerItemBounds (Page (view, 6)).Top);
                return 0;
            });
        }

        [Fact]
        public void PageBackColor_colours_the_pages_with_none_of_their_own_and_pages_added_later ()
        {
            var view = new RadPageView ();
            var plain = new RadPageViewPage { Text = "plain" };
            var own = new RadPageViewPage { Text = "own", BackColor = Color.Red };

            view.Pages.Add (plain);
            view.Pages.Add (own);
            view.PageBackColor = Color.Yellow;

            Assert.Equal (Color.Yellow.ToArgb (), plain.BackColor.ToArgb ());
            Assert.Equal (Color.Red.ToArgb (), own.BackColor.ToArgb ());

            var later = new RadPageViewPage { Text = "later" };
            view.Pages.Add (later);
            Assert.Equal (Color.Yellow.ToArgb (), later.BackColor.ToArgb ());

            // A colour of the page's own, set after, wins over a later PageBackColor.
            plain.BackColor = Color.Green;
            view.PageBackColor = Color.Blue;
            Assert.Equal (Color.Green.ToArgb (), plain.BackColor.ToArgb ());
            Assert.Equal (Color.Blue.ToArgb (), later.BackColor.ToArgb ());
        }

        [Fact]
        public void In_the_strip_IsContentVisible_is_only_stored ()
        {
            var view = new RadPageView ();
            var page = new RadPageViewPage { Text = "one" };
            var raised = false;

            view.Pages.Add (page);
            view.PageExpanded += (_, _) => raised = true;
            page.IsContentVisible = true;

            Assert.True (page.IsContentVisible);
            Assert.False (raised);
        }

        [Fact]
        public void Back_in_the_strip_only_the_selected_page_shows ()
        {
            var view = EmailTemplatesView (expanded: new[] { 1, 2 });

            WithForm (view, _ => {
                view.ViewMode = PageViewMode.Strip;
                view.PerformLayout ();

                Assert.True (Page (view, 2).Visible);
                Assert.False (Page (view, 1).Visible);
                Assert.Null (view.ExplorerScrollBar);
                return 0;
            });
        }
    }
}
