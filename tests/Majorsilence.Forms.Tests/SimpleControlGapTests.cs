using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;
using HitArea = Majorsilence.Forms.MonthCalendar.HitArea;

namespace Majorsilence.Forms.Tests
{
    // Behaviour-gap findings in the simple and value controls (docs/behaviour-gap/simple.md, #349).
    // Each test names the finding it pins. Every one was run red against the code with its fix
    // neutralized, except those labelled GUARD.
    [Collection ("Headless")]
    public class SimpleControlGapTests
    {
        // ---------------- SMP-08: PerformClick honours CanSelect

        [Fact]
        public void A_disabled_button_does_not_click_from_PerformClick ()
        {
            using var button = new Button ();
            var clicks = 0;
            button.Click += (_, _) => clicks++;

            button.PerformClick ();
            Assert.Equal (1, clicks);   // the enabled, detached button clicks, as upstream's does

            button.Enabled = false;
            button.PerformClick ();
            Assert.Equal (1, clicks);
        }

        [Fact]
        public void A_button_inside_a_disabled_or_hidden_parent_does_not_click ()
        {
            using var panel = new Panel ();
            var button = new Button ();
            panel.Controls.Add (button);
            var clicks = 0;
            button.Click += (_, _) => clicks++;

            panel.Enabled = false;
            button.PerformClick ();
            Assert.Equal (0, clicks);

            panel.Enabled = true;
            panel.Visible = false;
            button.PerformClick ();
            Assert.Equal (0, clicks);

            panel.Visible = true;
            button.PerformClick ();
            Assert.Equal (1, clicks);
        }

        [Fact]
        public void A_disabled_radio_button_is_not_checked_by_PerformClick ()
        {
            using var radio = new RadioButton { Enabled = false };

            radio.PerformClick ();

            Assert.False (radio.Checked);
        }

        // ---------------- SMP-12: the buttons use the arrow cursor

        [Fact]
        public void Buttons_check_boxes_and_radio_buttons_show_the_default_cursor ()
        {
            using var button = new Button ();
            using var check = new CheckBox ();
            using var radio = new RadioButton ();

            Assert.Same (Cursors.Default, button.Cursor);
            Assert.Same (Cursors.Default, check.Cursor);
            Assert.Same (Cursors.Default, radio.Cursor);
        }

        // ---------------- SMP-17: Label.PreferredWidth/PreferredHeight

        [Fact]
        public void A_mnemonic_ampersand_is_not_measured_into_PreferredWidth ()
        {
            HeadlessRenderer.Use ();
            using var mnemonic = new Label { Text = "&Name" };
            using var plain = new Label { Text = "Name" };
            using var literal = new Label { Text = "&Name", UseMnemonic = false };

            Assert.Equal (plain.PreferredWidth, mnemonic.PreferredWidth);
            Assert.True (literal.PreferredWidth > plain.PreferredWidth, "with UseMnemonic off the '&' is drawn, so it is measured");
        }

        [Theory]
        [InlineData (BorderStyle.FixedSingle, 1)]
        [InlineData (BorderStyle.Fixed3D, 2)]
        public void PreferredHeight_and_PreferredWidth_include_the_border (BorderStyle style, int edge)
        {
            HeadlessRenderer.Use ();
            using var plain = new Label { Text = "Caption" };
            using var framed = new Label { Text = "Caption", BorderStyle = style };

            Assert.Equal (plain.PreferredHeight + 2 * edge, framed.PreferredHeight);
            Assert.Equal (plain.PreferredWidth + 2 * edge, framed.PreferredWidth);
        }

        [Fact]
        public void An_empty_label_still_prefers_a_line_of_its_font_plus_the_border ()
        {
            // Upstream returns the extent of "0" with no width, plus borders; the old measure of "" gave
            // a line height but no border.
            HeadlessRenderer.Use ();
            using var plain = new Label ();
            using var framed = new Label { BorderStyle = BorderStyle.FixedSingle };

            Assert.True (plain.PreferredHeight > 0);
            Assert.Equal (plain.PreferredHeight + 2, framed.PreferredHeight);
            Assert.Equal (2, framed.PreferredWidth);
        }

        // ---------------- SMP-18 / SMP-19: LinkLabel cursor and Visited

        private sealed class HoverLinkLabel : LinkLabel
        {
            internal void MoveTo (Point p) => OnMouseMove (new MouseEventArgs (MouseButtons.None, 0, p.X, p.Y, 0));

            internal void MouseOff () => OnMouseLeave (EventArgs.Empty);
        }

        private static Point PointOnLink (LinkLabel label)
        {
            var renderer = RenderManager.GetRenderer<LinkLabelRenderer> ()!;

            for (var y = 0; y < label.Height; y++)
                for (var x = 0; x < label.Width; x++)
                    if (renderer.HitTest (label, new Point (x, y)) is not null)
                        return new Point (x, y);

            throw new InvalidOperationException ("no point of the label is on its link");
        }

        [Fact]
        public void The_pointer_turns_to_a_hand_over_a_link_and_back_off_it ()
        {
            HeadlessRenderer.Use ();
            using var label = new HoverLinkLabel { Text = "Open the report", AutoSize = false, Size = new Size (240, 30) };
            label.LinkArea = new LinkArea (9, 6);   // "report" only, so most of the label is not a link
            var configured = label.Cursor;

            label.MoveTo (PointOnLink (label));
            Assert.Same (Cursors.Hand, label.Cursor);

            label.MoveTo (new Point (label.Width - 1, label.Height - 1));
            Assert.Same (configured, label.Cursor);

            label.MoveTo (PointOnLink (label));
            label.MouseOff ();
            Assert.Same (configured, label.Cursor);
        }

        [Fact]
        public void A_disabled_link_does_not_show_the_hand ()
        {
            HeadlessRenderer.Use ();
            using var label = new HoverLinkLabel { Text = "Open", AutoSize = false, Size = new Size (120, 30) };
            label.Links[0].Enabled = false;

            label.MoveTo (PointOnLink (label));

            Assert.NotSame (Cursors.Hand, label.Cursor);
        }

        [Fact]
        public void Clicking_a_link_leaves_Visited_to_the_application ()
        {
            using var label = new LinkLabel { Text = "Open" };
            var clicked = 0;
            label.LinkClicked += (_, _) => clicked++;

            label.DriveLinkClick (label.Links[0]);

            Assert.Equal (1, clicked);
            Assert.False (label.Links[0].Visited);
            Assert.False (label.LinkVisited);
        }

        // ---------------- SMP-24 / SMP-25: PictureBox border and padding

        private static Majorsilence.Forms.Drawing.Bitmap RedSquare (int size)
        {
            var bitmap = new Majorsilence.Forms.Drawing.Bitmap (size, size);

            for (var x = 0; x < size; x++)
                for (var y = 0; y < size; y++)
                    bitmap.SetPixel (x, y, Color.Red);

            return bitmap;
        }

        private static bool IsRed (SKColor c) => c.Red > 200 && c.Green < 60 && c.Blue < 60;

        [Theory]
        [InlineData (BorderStyle.FixedSingle, 1)]
        [InlineData (BorderStyle.Fixed3D, 2)]
        public void PictureBox_BorderStyle_insets_the_client_area (BorderStyle style, int edge)
        {
            using var box = new PictureBox { Size = new Size (60, 40) };

            box.BorderStyle = style;

            Assert.Equal (new Size (60 - 2 * edge, 40 - 2 * edge), box.ClientSize);

            box.BorderStyle = BorderStyle.None;
            Assert.Equal (box.Size, box.ClientSize);
        }

        [Fact]
        public void PictureBox_BorderStyle_draws_a_frame ()
        {
            HeadlessRenderer.Use ();
            using var box = new PictureBox { Size = new Size (60, 40), BackColor = Color.White };

            using (var plain = PaintSurface.Render (box, 1f))
                Assert.Equal (plain.GetPixel (30, 20), plain.GetPixel (0, 20));

            box.BorderStyle = BorderStyle.FixedSingle;

            using var framed = PaintSurface.Render (box, 1f);
            Assert.NotEqual (framed.GetPixel (30, 20), framed.GetPixel (0, 20));
        }

        [Fact]
        public void PictureBox_Normal_draws_the_image_inside_the_padding ()
        {
            HeadlessRenderer.Use ();
            using var image = RedSquare (4);
            using var box = new PictureBox { Size = new Size (60, 40), BackColor = Color.White, Padding = new Padding (10), Image = image };

            using var bitmap = PaintSurface.Render (box, 1f);

            Assert.True (IsRed (bitmap.GetPixel (10, 10)), $"the image should start at the padding, found {bitmap.GetPixel (10, 10)}");
            Assert.False (IsRed (bitmap.GetPixel (9, 9)));
            Assert.False (IsRed (bitmap.GetPixel (0, 0)));
        }

        [Fact]
        public void PictureBox_AutoSize_grows_by_the_padding_and_the_border ()
        {
            using var image = RedSquare (4);
            using var box = new PictureBox { Image = image, Padding = new Padding (3), BorderStyle = BorderStyle.FixedSingle };

            box.SizeMode = PictureBoxSizeMode.AutoSize;

            Assert.Equal (new Size (4 + 6 + 2, 4 + 6 + 2), box.Size);

            box.Padding = new Padding (0);
            Assert.Equal (new Size (4 + 2, 4 + 2), box.Size);
        }

        // ---------------- SMP-27 / SMP-28: ProgressBar colours and trough border

        [Fact]
        public void ProgressBar_ForeColor_is_the_bar_colour ()
        {
            HeadlessRenderer.Use ();
            using var bar = new ProgressBar { Size = new Size (100, 23), Style = ProgressBarStyle.Continuous, Value = 50 };

            using (var themed = PaintSurface.Render (bar, 1f))
                Assert.False (IsRed (themed.GetPixel (25, 11)));

            bar.ForeColor = Color.Red;

            using var coloured = PaintSurface.Render (bar, 1f);
            Assert.True (IsRed (coloured.GetPixel (25, 11)), $"the filled part should be the ForeColor, found {coloured.GetPixel (25, 11)}");
            Assert.False (IsRed (coloured.GetPixel (75, 11)), "the empty trough is not the bar colour");
        }

        [Fact]
        public void ProgressBar_does_not_inherit_its_parents_text_colour_as_the_bar_colour ()
        {
            // GUARD: ForeColor is ambient here, so the renderer must ask the bar's own style chain, not
            // ForeColor -- a bar on a black-text panel would otherwise turn black. No old version did.
            HeadlessRenderer.Use ();
            using var panel = new Panel { ForeColor = Color.Black };
            var bar = new ProgressBar { Size = new Size (100, 23), Style = ProgressBarStyle.Continuous, Value = 50 };
            panel.Controls.Add (bar);

            using var bitmap = PaintSurface.Render (bar, 1f);

            Assert.Equal (Theme.AccentColor2, bitmap.GetPixel (25, 11));
        }

        [Fact]
        public void ProgressBar_keeps_its_one_pixel_trough_border ()
        {
            // GUARD, not proof: SMP-28 was already fixed by the TypeDefaultStyle hook (#100), which
            // makes the instance ControlStyle seed from ProgressBar.DefaultStyle.
            using var bar = new ProgressBar ();

            Assert.Equal (1, ((Control) bar).Style.Border.GetWidth ());
            Assert.Equal (new Size (bar.Width - 2, bar.Height - 2), bar.ClientSize);
        }

        // ---------------- SMP-30: TrackBar.Value

        [Fact]
        public void TrackBar_Value_set_from_code_is_not_snapped_to_a_tick ()
        {
            using var bar = new TrackBar { Maximum = 20, TickFrequency = 5, SnapToTicks = true };

            bar.Value = 3;

            Assert.Equal (3, bar.Value);
        }

        [Fact]
        public void TrackBar_Value_is_not_range_checked_between_BeginInit_and_EndInit ()
        {
            using var bar = new TrackBar ();
            var init = (ISupportInitialize) bar;

            // Designer order: Value before the Maximum that admits it.
            init.BeginInit ();
            bar.Value = 50;
            bar.Maximum = 100;
            init.EndInit ();

            Assert.Equal (50, bar.Value);
            Assert.Throws<ArgumentOutOfRangeException> (() => bar.Value = 500);
        }

        [Fact]
        public void TrackBar_Maximum_set_while_initializing_does_not_constrain_Value ()
        {
            // Upstream ConstrainValue returns while initializing: an intermediate Maximum must not clip
            // a Value the final one admits.
            using var bar = new TrackBar ();

            bar.BeginInit ();
            bar.Value = 50;
            bar.Maximum = 20;
            bar.Maximum = 100;
            bar.EndInit ();

            Assert.Equal (50, bar.Value);
        }

        [Fact]
        public void TrackBar_EndInit_constrains_a_value_the_range_never_admitted ()
        {
            using var bar = new TrackBar ();
            var changes = 0;

            bar.BeginInit ();
            bar.Value = 50;
            bar.ValueChanged += (_, _) => changes++;
            bar.EndInit ();

            Assert.Equal (10, bar.Value);
            Assert.Equal (1, changes);
        }

        // ---------------- SMP-34 / SMP-35: NumericUpDown range and initialization

        private sealed class TypingNumericUpDown : NumericUpDown
        {
            internal void Type (string text)
            {
                foreach (var c in text)
                    OnKeyPress (new KeyPressEventArgs (c));

                OnKeyDown (new KeyEventArgs (Keys.Enter));
            }
        }

        [Fact]
        public void NumericUpDown_Value_throws_outside_the_range ()
        {
            using var box = new NumericUpDown { Maximum = 10 };

            Assert.Throws<ArgumentOutOfRangeException> (() => box.Value = 20);
            Assert.Throws<ArgumentOutOfRangeException> (() => box.Value = -1);
            Assert.Equal (0m, box.Value);
        }

        [Fact]
        public void NumericUpDown_a_typed_value_outside_the_range_is_constrained_not_thrown ()
        {
            using var box = new TypingNumericUpDown { Maximum = 100 };

            box.Type ("500");

            Assert.Equal (100m, box.Value);
        }

        [Fact]
        public void NumericUpDown_narrowing_the_range_moves_Value_and_says_so ()
        {
            using var box = new NumericUpDown { Maximum = 100, Value = 80 };
            var changes = 0;
            box.ValueChanged += (_, _) => changes++;

            box.Maximum = 50;

            Assert.Equal (50m, box.Value);
            Assert.Equal (1, changes);
            Assert.Equal (box.FormatValue (50m), box.Text);
        }

        [Fact]
        public void NumericUpDown_designer_order_survives_BeginInit_and_EndInit ()
        {
            using var box = new NumericUpDown ();
            var init = (ISupportInitialize) box;

            init.BeginInit ();
            box.Value = 500;
            box.Maximum = 1000;
            init.EndInit ();

            Assert.Equal (500m, box.Value);
            Assert.Equal (box.FormatValue (500m), box.Text);
        }

        [Fact]
        public void NumericUpDown_EndInit_constrains_a_value_the_range_never_admitted ()
        {
            using var box = new NumericUpDown ();

            box.BeginInit ();
            box.Value = 500;
            box.EndInit ();

            Assert.Equal (100m, box.Value);
        }

        // ---------------- SMP-38: DomainUpDown selection and sorting

        [Fact]
        public void DomainUpDown_SelectedIndex_is_range_checked ()
        {
            using var box = new DomainUpDown ();
            box.Items.Add ("a");
            box.Items.Add ("b");
            box.Items.Add ("c");

            Assert.Throws<ArgumentOutOfRangeException> (() => box.SelectedIndex = 99);
            Assert.Throws<ArgumentOutOfRangeException> (() => box.SelectedIndex = -2);

            box.SelectedIndex = -1;
            Assert.Null (box.SelectedItem);
        }

        [Fact]
        public void DomainUpDown_raises_SelectedItemChanged_once_per_change ()
        {
            using var box = new DomainUpDown ();
            box.Items.Add ("a");
            box.Items.Add ("b");
            var changes = 0;
            box.SelectedItemChanged += (_, _) => changes++;

            box.SelectedIndex = 1;
            Assert.Equal (1, changes);

            box.SelectedIndex = 1;       // no change, no event
            Assert.Equal (1, changes);

            box.UpButton ();             // the buttons went through the same path and raised it twice
            Assert.Equal (2, changes);
            Assert.Equal ("a", box.SelectedItem);

            box.SelectedItem = null;
            Assert.Equal (-1, box.SelectedIndex);
            Assert.Equal (3, changes);
        }

        [Fact]
        public void DomainUpDown_Sorted_keeps_items_added_later_in_order_and_the_selection_on_its_item ()
        {
            using var box = new DomainUpDown { Sorted = true };
            box.Items.Add ("pear");
            box.Items.Add ("apple");
            box.SelectedItem = "pear";

            box.Items.Add ("banana");

            Assert.Equal (new object[] { "apple", "banana", "pear" }, box.Items.Cast<object> ().ToArray ());
            Assert.Equal ("pear", box.SelectedItem);
            Assert.Equal (2, box.SelectedIndex);
        }

        [Fact]
        public void DomainUpDown_removing_an_earlier_item_keeps_the_selection_on_its_item ()
        {
            using var box = new DomainUpDown ();
            box.Items.Add ("a");
            box.Items.Add ("b");
            box.Items.Add ("c");
            box.SelectedIndex = 2;

            box.Items.RemoveAt (0);

            Assert.Equal ("c", box.SelectedItem);

            box.Items.Remove ("c");
            Assert.Equal (-1, box.SelectedIndex);
        }

        // ---------------- SMP-44 / SMP-45: MonthCalendar bolded dates and SelectionEnd

        [Fact]
        public void An_assigned_BoldedDates_array_is_bolded_and_survives_UpdateBoldedDates ()
        {
            using var calendar = new MonthCalendar ();
            var day = new DateTime (2026, 3, 14);

            calendar.BoldedDates = [day];

            Assert.True (calendar.IsBoldedDate (day));

            calendar.UpdateBoldedDates ();
            Assert.Contains (day, calendar.BoldedDates);

            calendar.AddBoldedDate (day.AddDays (1));
            Assert.Equal (2, calendar.BoldedDates.Length);

            calendar.AnnuallyBoldedDates = [new DateTime (2000, 7, 1)];
            calendar.MonthlyBoldedDates = [new DateTime (2000, 1, 20)];
            Assert.True (calendar.IsBoldedDate (new DateTime (2031, 7, 1)));
            Assert.True (calendar.IsBoldedDate (new DateTime (2031, 2, 20)));

            calendar.BoldedDates = null!;
            Assert.Empty (calendar.BoldedDates);
            Assert.False (calendar.IsBoldedDate (day));
        }

        [Fact]
        public void MonthCalendar_SelectionEnd_validates_against_the_effective_range ()
        {
            using var calendar = new MonthCalendar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SelectionEnd = new DateTime (1200, 1, 1));
        }

        [Fact]
        public void MonthCalendar_SelectionStart_validates_against_the_raw_range_as_upstream_does ()
        {
            // GUARD: upstream's SelectionStart (unlike its SelectionEnd) checks the raw fields, which
            // default to DateTime.MinValue -- the finding's "use the effective range everywhere" was not
            // what upstream does. Pinned so nobody "fixes" it into a divergence.
            using var calendar = new MonthCalendar ();
            var early = new DateTime (1200, 1, 1);

            calendar.SelectionStart = early;

            Assert.Equal (early, calendar.SelectionStart);
        }

        // ---------------- SMP-43: MonthCalendar title hit areas

        [Fact]
        public void HitTest_splits_the_title_into_month_year_and_background_where_the_caption_is_drawn ()
        {
            HeadlessRenderer.Use ();
            using var calendar = new MonthCalendar {
                Size = new Size (220, 162),
                TitleBackColor = Color.Black,
                TitleForeColor = Color.White,
            };
            calendar.SetDate (new DateTime (2026, 3, 14));

            var geometry = calendar.Geometry;
            var text = MonthCalendar.TitleTextArea (geometry);

            using var bitmap = PaintSurface.Render (calendar);

            // The caption's ink, in device columns, across the band between the arrows.
            var ink = new List<int> ();

            for (var x = text.Left; x < text.Right; x++)
                for (var y = text.Top; y < text.Bottom; y++)
                    if (bitmap.GetPixel (x, y).Red > 128) {
                        ink.Add (x);
                        break;
                    }

            Assert.NotEmpty (ink);

            HitArea At (int device) => calendar.HitTest (calendar.DeviceToLogicalUnits (device), calendar.DeviceToLogicalUnits (text.Top + text.Height / 2)).HitArea;

            // The first and last inked columns are the month's first glyph and the year's last.
            Assert.Equal (HitArea.TitleMonth, At (ink.First () + 2));
            Assert.Equal (HitArea.TitleYear, At (ink.Last () - 2));
            Assert.Equal (HitArea.TitleBackground, At (text.Left + 1));
            Assert.Equal (HitArea.TitleBackground, At (text.Right - 2));

            // Left to right: background, the month, the space between, the year, background.
            var runs = new List<HitArea> ();

            for (var x = text.Left; x < text.Right; x++) {
                var area = At (x);

                if (runs.Count == 0 || runs[runs.Count - 1] != area)
                    runs.Add (area);
            }

            Assert.Equal (new[] { HitArea.TitleBackground, HitArea.TitleMonth, HitArea.TitleBackground, HitArea.TitleYear, HitArea.TitleBackground }, runs);
        }

        // ---------------- SMP-50: ScrollBar.Scroll's delegate type

        [Fact]
        public void ScrollBar_Scroll_takes_a_ScrollEventHandler ()
        {
            Assert.Equal (typeof (ScrollEventHandler), typeof (ScrollBar).GetEvent (nameof (ScrollBar.Scroll))!.EventHandlerType);

            // The designer's exact form, which did not compile against EventHandler<ScrollEventArgs>.
            using var bar = new VScrollBar ();
            var raised = 0;
            bar.Scroll += new ScrollEventHandler ((_, _) => raised++);
            Assert.Equal (0, raised);
        }

        // ---------------- SMP-54 / SMP-56: ImageList size and Draw

        [Theory]
        [InlineData (0, 16)]
        [InlineData (16, 0)]
        [InlineData (257, 16)]
        [InlineData (16, 257)]
        public void ImageList_ImageSize_rejects_sizes_outside_1_to_256 (int width, int height)
        {
            using var list = new ImageList ();

            Assert.ThrowsAny<ArgumentException> (() => list.ImageSize = new Size (width, height));
            Assert.Equal (new Size (16, 16), list.ImageSize);
        }

        [Fact]
        public void ImageList_ImageSize_after_ImageStream_resizes_rather_than_throwing ()
        {
            // The designer order the finding names: the streamer populates Images first.
            using var list = new ImageList ();
            using var frame = new SKBitmap (16, 16);
            list.Images.Add (frame);
            list.Images.Add (frame);

            list.ImageSize = new Size (32, 32);

            Assert.All (Enumerable.Range (0, 2), i => Assert.Equal (32, list.Images[i].Width));
            Assert.Equal (2, list.Images.Count);
        }

        [Fact]
        public void ImageList_Draw_throws_for_an_index_it_does_not_hold ()
        {
            using var list = new ImageList ();
            using var frame = new SKBitmap (16, 16);
            list.Images.Add (frame);
            using var target = new SKBitmap (40, 40);
            using var canvas = new SKCanvas (target);
            var g = new Graphics (canvas);

            list.Draw (g, 0, 0, 0);   // a held index draws
            Assert.Throws<ArgumentOutOfRangeException> (() => list.Draw (g, 0, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException> (() => list.Draw (g, 0, 0, 16, 16, -1));
        }

        // ---------------- SMP-57: WebBrowser history, events and title

        private sealed class FakeEngine : IWebViewHandle
        {
            public object NativeControl { get; } = new object ();

            public List<Uri> Navigations { get; } = [];

            public string Title { get; set; } = "\"Untitled\"";

            public void Navigate (Uri url)
            {
                Navigations.Add (url);
                NavigationCompleted?.Invoke (this, new WebViewNavigationCompletedEventArgs (url, true));
            }

            public void NavigateToString (string html) { }

            public Task<string?> ExecuteScriptAsync (string script)
                => Task.FromResult<string?> (script == "document.title" ? Title : null);

            public event EventHandler<WebViewNavigationCompletedEventArgs>? NavigationCompleted;

            public event EventHandler<WebViewMessageEventArgs>? WebMessageReceived { add { } remove { } }

            // A navigation the page started itself (a clicked link): only the completion is reported.
            public void Follow (Uri url) => NavigationCompleted?.Invoke (this, new WebViewNavigationCompletedEventArgs (url, true));

            public void Dispose () { }
        }

        private static readonly Uri PageA = new Uri ("https://example.test/a");
        private static readonly Uri PageB = new Uri ("https://example.test/b");
        private static readonly Uri PageC = new Uri ("https://example.test/c");

        [Fact]
        public void WebBrowser_records_history_and_raises_its_navigation_events ()
        {
            var engine = new FakeEngine ();
            using var browser = new WebBrowser (engine);
            var navigating = new List<Uri?> ();
            var order = new List<string> ();
            var back_changed = 0;
            browser.Navigating += (_, e) => navigating.Add (e.Url);
            browser.Navigated += (_, _) => order.Add ("Navigated");
            browser.DocumentCompleted += (_, _) => order.Add ("DocumentCompleted");
            browser.CanGoBackChanged += (_, _) => back_changed++;

            browser.Navigate (PageA);
            Assert.False (browser.CanGoBack);

            browser.Navigate (PageB);

            Assert.True (browser.CanGoBack);
            Assert.False (browser.CanGoForward);
            Assert.Equal (new Uri?[] { PageA, PageB }, navigating);
            Assert.Equal (new[] { "Navigated", "DocumentCompleted", "Navigated", "DocumentCompleted" }, order);
            Assert.Equal (1, back_changed);

            browser.GoBack ();

            Assert.Equal (PageA, engine.Navigations.Last ());
            Assert.Equal (PageA, browser.Url);
            Assert.False (browser.CanGoBack);
            Assert.True (browser.CanGoForward);

            browser.GoForward ();
            Assert.Equal (PageB, browser.Url);
            Assert.True (browser.CanGoBack);
            Assert.False (browser.CanGoForward);
        }

        [Fact]
        public void WebBrowser_a_new_page_after_GoBack_drops_the_forward_history ()
        {
            var engine = new FakeEngine ();
            using var browser = new WebBrowser (engine);

            browser.Navigate (PageA);
            browser.Navigate (PageB);
            browser.GoBack ();

            engine.Follow (PageC);   // a link clicked inside the page

            Assert.Equal (PageC, browser.Url);
            Assert.True (browser.CanGoBack);
            Assert.False (browser.CanGoForward);

            browser.GoBack ();
            Assert.Equal (PageA, browser.Url);
        }

        [Fact]
        public void WebBrowser_cancelling_Navigating_stops_the_navigation ()
        {
            var engine = new FakeEngine ();
            using var browser = new WebBrowser (engine);
            browser.Navigating += (_, e) => e.Cancel = e.Url == PageB;

            browser.Navigate (PageA);
            browser.Navigate (PageB);

            Assert.Equal (new[] { PageA }, engine.Navigations);
            Assert.Equal (PageA, browser.Url);
            Assert.False (browser.CanGoBack);
        }

        [Fact]
        public void WebBrowser_DocumentTitle_comes_from_the_page_after_each_navigation ()
        {
            var engine = new FakeEngine { Title = "\"Quarterly \\\"report\\\"\"" };
            using var browser = new WebBrowser (engine);
            var changed = 0;
            browser.DocumentTitleChanged += (_, _) => changed++;

            browser.Navigate (PageA);

            Assert.Equal ("Quarterly \"report\"", browser.DocumentTitle);
            Assert.Equal (1, changed);

            browser.Navigate (PageB);   // same title: no change event
            Assert.Equal (1, changed);
        }

        [Theory]
        [InlineData ("\"Plain\"", "Plain")]
        [InlineData ("Bare title", "Bare title")]
        [InlineData ("null", "")]
        [InlineData (null, "")]
        public void WebBrowser_decodes_both_engine_result_shapes (string? result, string expected)
            => Assert.Equal (expected, WebBrowser.DecodeScriptString (result));

        // ---------------- SMP-59: PropertyGrid.SelectedObjects

        private sealed class Shape
        {
            public string Name { get; set; } = "shape";

            public int Width { get; set; } = 10;

            [MergableProperty (false)]
            public string Tag { get; set; } = "t";
        }

        private sealed class Label2
        {
            public string Name { get; set; } = "label";

            public string Tag { get; set; } = "t";

            public bool Bold { get; set; }
        }

        [Fact]
        public void PropertyGrid_keeps_every_selected_object ()
        {
            using var grid = new PropertyGrid ();
            var a = new Shape ();
            var b = new Shape ();

            grid.SelectedObjects = [a, b];

            Assert.Equal (new object[] { a, b }, grid.SelectedObjects);
            Assert.Same (a, grid.SelectedObject);

            grid.SelectedObject = b;
            Assert.Equal (new object[] { b }, grid.SelectedObjects);

            grid.SelectedObject = null;
            Assert.Empty (grid.SelectedObjects!);
            Assert.Throws<ArgumentException> (() => grid.SelectedObjects = [a, null!]);
        }

        [Fact]
        public void PropertyGrid_lists_only_the_mergeable_properties_every_object_has ()
        {
            using var grid = new PropertyGrid ();

            grid.SelectedObjects = [new Shape (), new Label2 ()];

            var labels = grid.VisibleRows.Where (r => r.GridItemType == GridItemType.Property).Select (r => r.Label).ToList ();

            Assert.Equal (new[] { "Name" }, labels);   // Width and Bold are not shared; Tag will not merge
        }

        [Fact]
        public void PropertyGrid_shows_a_value_only_where_the_objects_agree_and_edits_them_all ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Size = new Size (420, 360) };
            var a = new Shape { Width = 10 };
            var b = new Shape { Width = 20 };
            var grid = new PropertyGrid { Bounds = new Rectangle (0, 0, 360, 300) };
            form.Controls.Add (grid);
            form.Show ();

            grid.SelectedObjects = [a, b];

            var width = grid.VisibleRows.First (r => r.Label == "Width");
            var name = grid.VisibleRows.First (r => r.Label == "Name");

            Assert.Null (width.Value);   // upstream's multi-select entry holds no value where they differ
            Assert.Equal (string.Empty, PropertyGrid.ValueTextOf (width));
            Assert.Equal ("shape", PropertyGrid.ValueTextOf (name));

            var index = grid.VisibleRows.ToList ().IndexOf (width);
            grid.BeginEdit (index);
            grid.EditingControl!.Text = "35";
            grid.EndEdit (commit: true);

            Assert.Equal (35, a.Width);
            Assert.Equal (35, b.Width);
            Assert.Equal ("35", PropertyGrid.ValueTextOf (grid.VisibleRows.First (r => r.Label == "Width")));
        }
    }
}
