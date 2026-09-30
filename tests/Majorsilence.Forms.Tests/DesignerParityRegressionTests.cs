using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Layout;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regressions found by running a migrated designer-generated login form side by side with the same
    // form on real WinForms: each of these rendered visibly differently there.
    public class DesignerParityRegressionTests
    {
        // ButtonBase.ApplyLatchedBackground cleared Style.ForegroundColor/BackgroundColor before every
        // paint of a button that is not latched -- i.e. every ordinary Button -- which is also where
        // ForeColor/BackColor keep the application's explicit colours. Under a form whose ForeColor is
        // White, `ForeColor = ControlText` then fell back to the ambient white: an invisible caption.
        [Fact]
        public void An_explicit_button_ForeColor_survives_painting_under_a_white_ambient_ForeColor ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 100), BackColor = Color.White, ForeColor = SystemColors.HighlightText };
            var button = new Button { Bounds = new Rectangle (10, 10, 96, 26), Text = "Login", ForeColor = SystemColors.ControlText };
            form.Controls.Add (button);
            form.Show ();

            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            Assert.Equal (SystemColors.ControlText.ToArgb (), button.ForeColor.ToArgb ());
        }

        [Fact]
        public void An_explicit_button_BackColor_survives_painting ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 100) };
            var button = new Button { Bounds = new Rectangle (10, 10, 96, 26), Text = "Go", BackColor = Color.Gold };
            form.Controls.Add (button);
            form.Show ();

            HeadlessRenderer.CapturePng (form);

            Assert.Equal (Color.Gold.ToArgb (), button.BackColor.ToArgb ());
        }

        // A latched colour is still undone when the control goes off, and gives back the explicit one.
        [Fact]
        public void A_toggle_button_restores_its_own_colours_when_unchecked ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 100) };
            var toggle = new CheckBox { Appearance = Appearance.Button, Bounds = new Rectangle (10, 10, 96, 26), Text = "Bold", ForeColor = Color.DarkRed };
            form.Controls.Add (toggle);
            form.Show ();

            toggle.Checked = true;
            HeadlessRenderer.CapturePng (form);
            Assert.NotEqual (Color.DarkRed.ToArgb (), toggle.ForeColor.ToArgb ());

            toggle.Checked = false;
            HeadlessRenderer.CapturePng (form);
            Assert.Equal (Color.DarkRed.ToArgb (), toggle.ForeColor.ToArgb ());
        }

        // The text document of a TextBox was laid out in a fixed Theme.UIFont that nothing reassigned,
        // so a designer `Font = Microsoft Sans Serif 12pt Bold` only changed the size -- the text drew
        // in the theme's regular face next to a Label that honoured the same font.
        [Fact]
        public void A_TextBox_lays_its_text_out_in_its_own_font_and_follows_changes ()
        {
            using var form = new Form ();
            var bold = new Majorsilence.Forms.Drawing.Font ("Arial", 12F, Majorsilence.Forms.Drawing.FontStyle.Bold);
            var box = new TextBox { Font = bold, Text = "someone@example.com" };
            form.Controls.Add (box);

            Assert.Same (box.GetEffectiveFont (), box.document.Font);
            Assert.Equal (700, box.document.Font.FontWeight);
            var before = box.document.GetTextBlock ();

            box.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 12F);

            Assert.Equal (400, box.document.Font.FontWeight);
            Assert.NotSame (before, box.document.GetTextBlock ());
        }

        // ClientRectangle already excludes the border; the text/image layout subtracted it again, so a
        // 20px Fixed3D label kept a 12px text field instead of 16px and its text was cut off.
        [Fact]
        public void A_bordered_labels_text_field_is_its_client_area ()
        {
            using var label = new Label { Size = new Size (316, 20), BorderStyle = BorderStyle.Fixed3D, Text = "Production Server" };

            var layout = TextImageLayoutEngine.Layout (label);

            Assert.Equal (label.ClientRectangle, layout.Field);
            Assert.Equal (16, layout.Field.Height);
        }

        // ApplyFlatAppearance re-derives the border before every paint, which put the themed 1px frame
        // back on the caption buttons the constructor had made borderless.
        [Fact]
        public void Caption_buttons_are_drawn_without_a_frame ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (300, 200) };
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            Assert.Equal (0, form.TitleBar.MaximizeButtonControl.CurrentStyle.Border.Top.GetWidth ());
        }

        [Fact]
        public void MinimizeBox_and_MaximizeBox_off_together_hide_both_and_allow_the_help_button ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (300, 200), HelpButton = true };
            form.Show ();

            // Skipped when the title bar is a native overlay (macOS's default): the OS draws the traffic
            // lights there and every managed caption button is hidden regardless, so nothing this rule
            // decides is observable -- the same guard W62RemainingTypesTests uses. The caption getters
            // report the buttons' visibility, which is also why the form has to be shown first.
            if (!form.TitleBar.Visible || form.TitleBar.NativeOverlay)
                return;

            form.MaximizeBox = false;

            Assert.True (form.TitleBar.AllowMinimize);
            Assert.True (form.TitleBar.AllowMaximize);
            Assert.False (form.TitleBar.MaximizeButtonControl.Enabled);
            Assert.False (form.TitleBar.AllowHelp);

            form.MinimizeBox = false;

            Assert.False (form.TitleBar.AllowMinimize);
            Assert.False (form.TitleBar.AllowMaximize);
            Assert.True (form.TitleBar.AllowHelp);
        }

        // Zoom/CenterImage centred on (0,0)-relative halves of the padded rectangle, so padding moved
        // the image up and left instead of insetting it.
        [Fact]
        public void A_padded_zoomed_picture_is_centred_in_the_padded_area ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (100, 100) };
            using var bitmap = new SkiaSharp.SKBitmap (48, 48);
            bitmap.Erase (SkiaSharp.SKColors.Red);
            var box = new PictureBox { Bounds = new Rectangle (0, 0, 34, 34), SizeMode = PictureBoxSizeMode.Zoom, Padding = new Padding (9) };
            box.SetSKImage (bitmap.Copy ());
            box.Style.BackgroundColor = SkiaSharp.SKColors.White;
            form.Controls.Add (box);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var painted = (SkiaSharp.SKBitmap)buffer.Invoke (box, null)!;

            // The back buffer is in device pixels, the points below logical (RC-8).
            SkiaSharp.SKColor At (int logical) => painted.GetPixel (box.LogicalToDeviceUnits (logical), box.LogicalToDeviceUnits (logical));

            Assert.Equal (SkiaSharp.SKColors.White, At (4));   // inside the padding
            Assert.Equal (SkiaSharp.SKColors.Red, At (17));    // the centre
            Assert.Equal (SkiaSharp.SKColors.White, At (29));  // the far padding
        }

        private static RadPageView DesignerPageView (Form form)
        {
            var view = new RadPageView { Bounds = new Rectangle (10, 10, 562, 280) };
            view.Controls.Add (new RadPageViewPage { Text = "Login", ItemSize = new SizeF (280F, 32F) });
            view.Controls.Add (new RadPageViewPage { Text = "Server/Database", ItemSize = new SizeF (280F, 32F) });
            view.ItemSizeMode = PageViewItemSizeMode.EqualWidth;
            view.GetChildAt (0).ItemFitMode = StripViewItemFitMode.Fill;
            view.GetChildAt (0).ItemSizeMode = PageViewItemSizeMode.EqualWidth;
            form.Controls.Add (view);
            return view;
        }

        [Fact]
        public void RadPageView_EqualWidth_Fill_gives_every_tab_an_equal_share_at_the_designer_height ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (600, 320) };
            var view = DesignerPageView (form);
            form.Show ();
            view.PerformLayout ();

            var tabs = view.TabStrip.Tabs;
            var share = view.DeviceToLogicalUnits (view.ClientRectangle.Width) / 2;

            Assert.Equal (share, tabs[0].Bounds.Width);
            Assert.Equal (share, tabs[1].Bounds.Width);
            Assert.Equal (32, tabs[0].Bounds.Height);
            Assert.Equal (ContentAlignment.MiddleLeft, view.TabStrip.ItemTextAlign);
        }

        // A themed Telerik control takes its item text colour from the theme, not from the form's ambient
        // ForeColor -- a white form ForeColor used to make the tab captions white on the light strip.
        [Fact]
        public void RadPageView_tab_text_ignores_a_white_ambient_ForeColor_unless_set_on_the_view ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (600, 320), ForeColor = Color.White };
            var view = DesignerPageView (form);

            Assert.Equal (Theme.ForegroundColor, view.TabStrip.GetEffectiveForegroundColor ());

            // ... and so do the controls on its pages, which inherit the page view's colour.
            var label = new Label { Text = "Select Server:" };
            view.TabPages[0].Controls.Add (label);
            Assert.Equal (Theme.ForegroundColor, label.GetEffectiveForegroundColor ());

            view.ForeColor = Color.DarkBlue;
            Assert.Equal (Color.DarkBlue.ToArgb (), view.TabStrip.GetEffectiveForegroundColor ().ToDrawingColor ().ToArgb ());
            Assert.Equal (Color.DarkBlue.ToArgb (), label.ForeColor.ToArgb ());
        }

        // Upstream's TextFormatFlags.TextBoxControl: a caption that wraps on a button one line high shows
        // only its whole first line, centred, instead of two top-aligned lines with the second sliced.
        [Fact]
        public void A_wrapping_button_caption_shows_whole_lines_centred ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 100), BackColor = Color.White };
            var button = new Button {
                Bounds = new Rectangle (10, 10, 90, 26), Text = "Save Default",
                Font = new Majorsilence.Forms.Drawing.Font ("Arial", 12F), ForeColor = Color.Black, BackColor = Color.White,
            };
            form.Controls.Add (button);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var painted = (SkiaSharp.SKBitmap)buffer.Invoke (button, null)!;

            int first = -1, last = -1;
            for (var y = 2; y < painted.Height - 2; y++)
                for (var x = 2; x < painted.Width - 2; x++)
                    if (painted.GetPixel (x, y).Red < 100) {
                        if (first < 0) first = y;
                        last = y;
                        break;
                    }

            Assert.True (first >= 0, "the caption was not drawn");

            // The back buffer is in device pixels, so the limits are logical ones scaled (RC-8).
            var one_line = button.LogicalToDeviceUnits (16);
            var slack = button.LogicalToDeviceUnits (5);

            // One whole line only: a sliced second line under the first would make the ink taller.
            Assert.InRange (last - first, 1, one_line);
            // Centred: the gap above the ink and the gap below it agree to within a few pixels.
            Assert.InRange (first - (painted.Height - 1 - last), -slack, slack);
        }
    }
}
