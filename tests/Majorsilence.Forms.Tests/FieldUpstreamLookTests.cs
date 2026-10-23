using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets WinForms'
    // Windows 11 text and list boxes: a white field (SystemColors.Window) in a light outline, and a
    // single-line text box as tall as its font makes it (Font.Height + 7, 23px for Segoe UI 9pt).
    [Collection ("Headless")]
    public class FieldUpstreamLookTests
    {
        private static T With<T> (bool chooseFont, System.Func<Form, T> act)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
                form.Show ();
                return act (form);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static SKColor CentrePixel (Form form, Control control)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));

            // The capture is in device pixels, which CI also runs at scale 2 (MF_HEADLESS_SCALE).
            var scale = bitmap.Width / 300f;
            return bitmap.GetPixel ((int) ((control.Left + control.Width / 2) * scale), (int) ((control.Top + 3) * scale));
        }

        [Fact]
        public void A_text_box_is_a_white_field_as_tall_as_its_font ()
            => With (true, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20) };
                form.Controls.Add (box);

                Assert.Equal (box.Font.Height + 7, box.Height);
                Assert.Equal (SKColors.White, CentrePixel (form, box));
                Assert.Equal (System.Drawing.Color.White.ToArgb (), box.BackColor.ToArgb ());
                return 0;
            });

        // Upstream's date picker is the native control: one darker line all round (#8D8D8D under Windows 11) and, with
        // ShowUpDown, spin buttons with a near-white face in a pale outline. It was drawn as a text box, its pale
        // outline all but invisible on the control colour, and its spin buttons as the theme's flat face.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_date_picker_has_the_native_frame_and_spin_buttons (bool chooseFont)
            => With (chooseFont, form => {
                var picker = new DateTimePicker { Location = new Point (10, 10), Size = new Size (120, 26), ShowUpDown = true };
                form.Controls.Add (picker);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var frame = new SKColor (0x8D, 0x8D, 0x8D);
                var mid = (int) ((picker.Top + picker.Height / 2) * scale);
                var left = bitmap.GetPixel ((int) (picker.Left * scale), mid);
                var top = bitmap.GetPixel ((int) ((picker.Left + 30) * scale), (int) (picker.Top * scale));
                var spin_faces = 0;

                for (var x = (int) ((picker.Right - 20) * scale); x < (int) (picker.Right * scale); x++)
                    for (var y = (int) (picker.Top * scale); y < (int) (picker.Bottom * scale); y++)
                        if (bitmap.GetPixel (x, y) == new SKColor (0xFA, 0xFA, 0xFA))
                            spin_faces++;

                if (chooseFont) {
                    Assert.Equal (frame, left);
                    Assert.Equal (frame, top);
                    Assert.True (spin_faces > 20, $"{spin_faces} spin button face pixels");
                } else {
                    // The theme's own picker: not the native frame. (Its button colours are the theme's and may
                    // include the spin face's near-white, so that is not asserted against.)
                    Assert.NotEqual (frame, left);
                }

                return 0;
            });

        // Upstream's Windows 11 text box underlines itself in the accent while it has the keyboard focus.
        // Upstream's native picker, measured: enabled, a #4D4D4D chevron (darker than a combo box's); disabled, a #FEFEFE
        // field in a #C8C8C8 frame with a #C9C9C9 chevron and its date in GrayText. Disabled, it kept the enabled frame on
        // the Control face, and both states drew the combo box's chevron.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_date_picker_has_the_native_look_enabled_and_disabled (bool enabled)
            => With (true, form => {
                var picker = new DateTimePicker {
                    Location = new Point (10, 10), Size = new Size (180, 40), Enabled = enabled,
                    Font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 16f, Majorsilence.Forms.Drawing.FontStyle.Bold),
                };
                form.Controls.Add (picker);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                int X (int x) => (int) ((picker.Left + x) * scale);
                int Y (int y) => (int) ((picker.Top + y) * scale);

                int Count (SKColor colour, int x0, int x1)
                {
                    var n = 0;
                    for (var x = X (x0); x < X (x1); x++)
                        for (var y = Y (0); y < Y (picker.Height); y++)
                            if (bitmap.GetPixel (x, y) == colour)
                                n++;
                    return n;
                }

                var chevron = Count (enabled ? new SKColor (0x4D, 0x4D, 0x4D) : new SKColor (0xC9, 0xC9, 0xC9), picker.Width - 20, picker.Width - 2);
                Assert.True (chevron >= 8, $"{chevron} chevron pixels");

                if (!enabled) {
                    Assert.Equal (new SKColor (0xC8, 0xC8, 0xC8), bitmap.GetPixel (X (0), Y (20)));
                    Assert.Equal (new SKColor (0xC8, 0xC8, 0xC8), bitmap.GetPixel (X (90), Y (0)));
                    Assert.Equal (new SKColor (0xFE, 0xFE, 0xFE), bitmap.GetPixel (X (90), Y (2)));
                    Assert.True (Count (SystemColors.GrayText.ToSKColor (), 2, picker.Width - 24) > 10, "the date is in GrayText");
                }

                return 0;
            });

        [Fact]
        public void A_focused_text_box_is_underlined_in_the_accent ()
            => With (true, form => {
                var focused = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20), TabIndex = 0 };
                var other = new TextBox { Location = new Point (10, 60), Size = new Size (120, 20), TabIndex = 1 };
                form.Controls.Add (focused);
                form.Controls.Add (other);
                focused.Select ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var accent = new SKColor (0x00, 0x67, 0xC0);
                SKColor Bottom (TextBox box, int up) => bitmap.GetPixel ((int) ((box.Left + box.Width / 2) * scale), (int) (box.Bottom * scale) - up);

                Assert.True (focused.Focused);
                Assert.Equal (accent, Bottom (focused, 1));
                Assert.Equal (accent, Bottom (focused, (int) (2 * scale)));
                Assert.NotEqual (accent, Bottom (other, 1));
                return 0;
            });

        // The native edit control keeps the same margin on both sides: right-aligned text ends as far in from
        // the right edge as left-aligned text starts from the left. Here right-aligned text ran to the edge.
        [Fact]
        public void Right_aligned_text_keeps_the_margin_left_aligned_text_has ()
            => With (true, form => {
                var left = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20), Text = "8888" };
                var right = new TextBox { Location = new Point (10, 60), Size = new Size (120, 20), Text = "8888", TextAlign = HorizontalAlignment.Right };
                form.Controls.Add (left);
                form.Controls.Add (right);
                form.ActiveControl = null;

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;

                // The first and last columns holding dark (text) pixels across a box.
                (int first, int last) Ink (TextBox box)
                {
                    int first = -1, last = -1;

                    for (var x = (int) (box.Left * scale); x < (int) (box.Right * scale); x++)
                        for (var y = (int) ((box.Top + 3) * scale); y < (int) ((box.Bottom - 3) * scale); y++)
                            if (bitmap.GetPixel (x, y).Red < 0x80) {
                                if (first < 0) first = x;
                                last = x;
                                break;
                            }

                    return (first, last);
                }

                var left_inset = Ink (left).first - (int) (left.Left * scale);
                var right_inset = (int) (right.Right * scale) - 1 - Ink (right).last;

                // The glyphs' own side bearings differ by platform font, so the two insets agree only roughly: the
                // right one keeps at least the margin, and sits no further in than the left one does, give or take.
                Assert.True (right_inset >= (int) (2 * scale) && right_inset <= left_inset + (int) (2 * scale), $"left {left_inset} right {right_inset} ink {Ink (right)} box {right.Bounds} scale {scale}");
                return 0;
            });

        // Upstream's themed group box: one pale line round the frame. It was the classic etched pair, a dark
        // grey line with a lighter one inside it.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_group_box_frame_is_one_pale_line (bool chooseFont)
            => With (chooseFont, form => {
                var group = new GroupBox { Text = "Earning", Location = new Point (10, 10), Size = new Size (150, 100) };
                form.Controls.Add (group);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var y = (int) ((group.Top + 60) * scale);
                var pale = new SKColor (0xDC, 0xDC, 0xDC);
                var darkest = (byte) 0xFF;
                var has_pale = false;

                // The left side of the frame, a few device pixels in from the box's edge.
                for (var x = (int) (group.Left * scale); x < (int) ((group.Left + 4) * scale); x++) {
                    var c = bitmap.GetPixel (x, y);
                    has_pale |= c == pale;
                    darkest = System.Math.Min (darkest, c.Red);
                }

                if (chooseFont) {
                    Assert.True (has_pale, "no pale frame line");
                    Assert.Equal (0xDC, darkest);
                } else {
                    Assert.True (darkest < 0xDC, "the theme's frame was replaced without a chosen font");
                }

                return 0;
            });

        // Upstream's edit control selects in Highlight behind HighlightText. It was the theme's pale blue behind the
        // text's own colour.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Selected_text_is_highlight_text_on_highlight (bool chooseFont)
            => With (chooseFont, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (150, 20), Text = "8888 8888" };
                form.Controls.Add (box);
                box.Select ();
                box.SelectAll ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var highlight = SystemColors.Highlight.ToSKColor ();
                int highlighted = 0, light_text = 0;
                var dark_columns = new System.Collections.Generic.HashSet<int> ();

                for (var x = (int) ((box.Left + 2) * scale); x < (int) ((box.Right - 2) * scale); x++)
                    for (var y = (int) ((box.Top + 3) * scale); y < (int) ((box.Bottom - 3) * scale); y++) {
                        var c = bitmap.GetPixel (x, y);
                        if (c == highlight) highlighted++;
                        else if (c.Red > 0xE0 && c.Green > 0xE0 && c.Blue > 0xE0 && highlighted > 0) light_text++;
                        if (c.Red < 0x40 && c.Green < 0x40 && c.Blue < 0x40) dark_columns.Add (x);
                    }

                if (chooseFont) {
                    Assert.True (highlighted > 50 * scale, $"{highlighted} Highlight pixels");
                    Assert.True (light_text > 0, "no HighlightText glyphs");
                    // Nothing dark but the caret, a single upright line, as upstream's focused box shows it.
                    Assert.True (dark_columns.Count <= (int) System.Math.Ceiling (scale), $"dark text in {dark_columns.Count} columns");
                } else {
                    Assert.Equal (0, highlighted);
                }

                return 0;
            });

        [Fact]
        public void A_list_box_is_a_white_field ()
            => With (true, form => {
                var list = new ListBox { Location = new Point (10, 50), Size = new Size (120, 100) };
                form.Controls.Add (list);

                Assert.Equal (SKColors.White, CentrePixel (form, list));
                return 0;
            });

        // Pixels of the upstream chevron's grey in the combo's right-hand 16px.
        private static int ChevronPixels (Form form, Control combo)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
            var scale = bitmap.Width / 300f;
            var count = 0;

            for (var x = (int) ((combo.Right - 16) * scale); x < (int) (combo.Right * scale); x++)
                for (var y = (int) (combo.Top * scale); y < (int) (combo.Bottom * scale); y++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Red is > 0x50 and < 0x90 && c.Red == c.Green && c.Green == c.Blue)
                        count++;
                }

            return count;
        }

        [Fact]
        public void A_drop_down_list_has_a_near_white_face_and_a_thin_chevron ()
            => With (true, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point (10, 10), Size = new Size (150, 23) };
                form.Controls.Add (combo);

                Assert.Equal (new SKColor (0xFD, 0xFD, 0xFD), CentrePixel (form, combo));
                Assert.True (ChevronPixels (form, combo) > 3, "the chevron is drawn in upstream's grey");
                return 0;
            });

        // Measured on WinForms (.NET 10), a 150px combo box column: the cell is the drop-down list's field inside the grid
        // lines -- #FDFDFD in a #D2D2D2 frame with a #BCBCBC bottom -- with a #D2D2D2 separator 17px from the field's right
        // and the chevron, #6F6F6F, from 13px to 6px from it. The cell drew its text, a rule and a "▾" glyph instead.
        [Fact]
        public void A_grid_combo_box_cell_is_drawn_as_a_drop_down_list ()
            => With (true, form => {
                var grid = new DataGridView { Location = new Point (0, 0), Size = new Size (300, 120), RowHeadersVisible = false, AllowUserToAddRows = false };
                var column = new DataGridViewComboBoxColumn { HeaderText = "Email", Width = 150 };
                column.Items.Add ("Short");
                grid.Columns.Add (column);
                grid.Rows.Add ("Short");
                grid.Rows.Add ("Short");
                form.Controls.Add (grid);
                form.Controls.Add (new TextBox { Location = new Point (250, 150), Size = new Size (30, 20) });
                form.ActiveControl = form.Controls[form.Controls.Count - 1];
                grid.ClearSelection ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var px = (int) System.Math.Round (scale);
                var cell = grid.GetCellDisplayRectangle (0, 1, false);

                // The field sits inside the grid lines, wherever device rounding put them: found by walking to them.
                var x0 = (int) ((cell.Left + 10) * scale);
                var y = (int) (cell.Top * scale) - 2 * px;
                while (bitmap.GetPixel (x0, y) != new SKColor (0x64, 0x64, 0x64)) y++;
                var top = y + 1;
                y = top;
                while (bitmap.GetPixel (x0, y) != new SKColor (0x64, 0x64, 0x64)) y++;
                var bottom = y;                                      // exclusive
                var mid = (top + bottom) / 2;
                var x = (int) ((cell.Right - 10) * scale);
                while (bitmap.GetPixel (x, mid) != new SKColor (0x64, 0x64, 0x64)) x++;
                var right = x;                                       // exclusive

                Assert.Equal (new SKColor (0xD2, 0xD2, 0xD2), bitmap.GetPixel (x0, top));
                Assert.Equal (new SKColor (0xBC, 0xBC, 0xBC), bitmap.GetPixel (x0, bottom - 1));
                Assert.Equal (new SKColor (0xD2, 0xD2, 0xD2), bitmap.GetPixel (right - 1, mid));
                Assert.Equal (new SKColor (0xD2, 0xD2, 0xD2), bitmap.GetPixel (right - 17 * px, mid));
                Assert.Equal (new SKColor (0xFD, 0xFD, 0xFD), bitmap.GetPixel (right - 20 * px, top + 3 * px));

                var chevron = 0;
                for (var cx = right - 13 * px; cx < right - 5 * px; cx++)
                    for (var cy = top; cy < bottom; cy++)
                        if (bitmap.GetPixel (cx, cy) == new SKColor (0x6F, 0x6F, 0x6F))
                            chevron++;

                Assert.Equal (8 * px * px, chevron);
                return 0;
            });

        // Measured on WinForms (.NET 10): the themed drop-down list draws its text in WindowText whatever its ForeColor.
        // GrayText and Red both came out black there; an editable DropDown shows its ForeColor. A designer that set a
        // list's ForeColor to GrayText had its selection drawn as if disabled.
        [Theory]
        [InlineData (255, 0, 0)]
        [InlineData (0x6D, 0x6D, 0x6D)]
        public void A_drop_down_list_draws_its_text_in_window_text_whatever_its_fore_color (byte r, byte g, byte b)
            => With (true, form => {
                // Two identical lists, one with the ForeColor and one with the default, must draw the same pixels.
                // Compared with each other rather than counted by colour: anti-aliased edges of black text are grey too,
                // and how many depends on the font the platform substitutes.
                ComboBox List (int top, Color? fore)
                {
                    var combo = new ComboBox {
                        DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point (10, top), Size = new Size (200, 23),
                        Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 9f, Majorsilence.Forms.Drawing.FontStyle.Bold)
                    };
                    if (fore is { } colour)
                        combo.ForeColor = colour;
                    combo.Items.Add ("Add New Requisition");
                    combo.SelectedIndex = 0;
                    form.Controls.Add (combo);
                    return combo;
                }

                List (10, Color.FromArgb (r, g, b));
                List (60, null);
                form.Controls.Add (new TextBox { Location = new Point (250, 150), Size = new Size (30, 20) });
                form.ActiveControl = form.Controls[form.Controls.Count - 1];

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                int differing = 0, ink = 0;

                for (var x = (int) (14 * scale); x < (int) (180 * scale); x++)
                    for (var y = (int) (13 * scale); y < (int) (30 * scale); y++) {
                        var coloured = bitmap.GetPixel (x, y);
                        var plain = bitmap.GetPixel (x, y + (int) (50 * scale));
                        if (coloured != plain) differing++;
                        if (plain.Red < 128) ink++;
                    }

                Assert.True (ink > 20, $"{ink} text pixels in the default list");
                Assert.Equal (0, differing);
                return 0;
            });

        [Fact]
        public void An_editable_combo_has_a_white_field ()
            => With (true, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Location = new Point (10, 10), Size = new Size (150, 23) };
                form.Controls.Add (combo);

                Assert.Equal (System.Drawing.Color.White.ToArgb (), combo.BackColor.ToArgb ());
                return 0;
            });

        // The combo paints the field; its edit region is a borderless child inside it. The region painted a text box's
        // field of its own, forced to a text box's height, so its outline showed inside the combo's and its face broke
        // the combo's dark bottom line under the text.
        [Fact]
        public void An_editable_combo_s_edit_region_paints_no_field_of_its_own ()
            => With (true, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Location = new Point (10, 10), Size = new Size (150, 23), Text = "Text" };
                form.Controls.Add (combo);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var x = (int) ((combo.Left + 60) * scale);

                Assert.Equal (new SKColor (0x83, 0x83, 0x83), bitmap.GetPixel (x, (int) ((combo.Bottom - 1) * scale)));
                Assert.Equal (SKColors.White, bitmap.GetPixel (x, (int) ((combo.Top + 1) * scale)));
                return 0;
            });

        [Fact]
        public void Without_a_chosen_font_the_theme_combo_is_kept ()
            => With (false, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point (10, 10), Size = new Size (150, 23) };
                form.Controls.Add (combo);

                Assert.NotEqual (new SKColor (0xFD, 0xFD, 0xFD), CentrePixel (form, combo));
                return 0;
            });

        [Fact]
        public void A_read_only_or_coloured_box_keeps_its_colour ()
            => With (true, form => {
                var read_only = new TextBox { ReadOnly = true, Location = new Point (10, 10), Size = new Size (120, 20) };
                var coloured = new TextBox { BackColor = System.Drawing.Color.LightYellow, Location = new Point (10, 50), Size = new Size (120, 20) };
                form.Controls.Add (read_only);
                form.Controls.Add (coloured);

                Assert.NotEqual (SKColors.White, CentrePixel (form, read_only));
                Assert.Equal (System.Drawing.Color.LightYellow.ToArgb (), coloured.BackColor.ToArgb ());
                return 0;
            });

        // Upstream's read-only box keeps the frame with a near-white (#FEFEFE) line just inside it, round the Control face.
        [Fact]
        public void A_read_only_box_has_a_near_white_line_inside_its_frame ()
            => With (true, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20), ReadOnly = true };
                form.Controls.Add (box);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var x = (int) ((box.Left + box.Width / 2) * scale);

                Assert.Equal (new SKColor (0xEC, 0xEC, 0xEC), bitmap.GetPixel (x, (int) (box.Top * scale)));
                Assert.Equal (new SKColor (0xFE, 0xFE, 0xFE), bitmap.GetPixel (x, (int) ((box.Top + 1) * scale)));
                Assert.Equal (new SKColor (0xF0, 0xF0, 0xF0), bitmap.GetPixel (x, (int) ((box.Top + box.Height / 2) * scale)));
                Assert.Equal (new SKColor (0xFE, 0xFE, 0xFE), bitmap.GetPixel (x, (int) ((box.Bottom - 2) * scale)));
                Assert.Equal (new SKColor (0x83, 0x83, 0x83), bitmap.GetPixel (x, (int) ((box.Bottom - 1) * scale)));
                return 0;
            });

        // A Fixed3D label is upstream's sunken static control: ControlDark along the top and left, white along the bottom
        // and right, and its own background in the border's second pixel. It was a 2px ring of the theme's grey.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_fixed_3d_label_has_upstream_s_sunken_edge (bool chooseFont)
            => With (chooseFont, form => {
                form.BackColor = System.Drawing.Color.FromArgb (0xF0, 0xF0, 0xF0);
                var label = new Label { Location = new Point (10, 10), Size = new Size (90, 23), BorderStyle = BorderStyle.Fixed3D, Text = "" };
                form.Controls.Add (label);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                int X (int x) => (int) ((label.Left + x) * scale);
                int Y (int y) => (int) ((label.Top + y) * scale);
                var dark = new SKColor (0xA0, 0xA0, 0xA0);

                if (chooseFont) {
                    Assert.Equal (dark, bitmap.GetPixel (X (0), Y (11)));
                    Assert.Equal (dark, bitmap.GetPixel (X (45), Y (0)));
                    Assert.Equal (SKColors.White, bitmap.GetPixel (X (label.Width - 1), Y (11)));
                    Assert.Equal (SKColors.White, bitmap.GetPixel (X (45), Y (label.Height - 1)));
                    Assert.Equal (new SKColor (0xF0, 0xF0, 0xF0), bitmap.GetPixel (X (1), Y (11)));
                    Assert.Equal (new SKColor (0xF0, 0xF0, 0xF0), bitmap.GetPixel (X (45), Y (label.Height - 2)));
                } else {
                    // The theme's ring: one colour all round.
                    Assert.Equal (bitmap.GetPixel (X (0), Y (11)), bitmap.GetPixel (X (label.Width - 1), Y (11)));
                }

                return 0;
            });

        [Fact]
        public void A_multiline_box_keeps_its_height ()
            => With (true, form => {
                var box = new TextBox { Multiline = true, Location = new Point (10, 10), Size = new Size (120, 60) };
                form.Controls.Add (box);
                Assert.Equal (60, box.Height);
                return 0;
            });

        [Fact]
        public void Without_a_chosen_font_the_theme_field_is_kept ()
            => With (false, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20) };
                form.Controls.Add (box);

                Assert.Equal (20, box.Height);
                Assert.NotEqual (SKColors.White, CentrePixel (form, box));
                return 0;
            });
    }
}
