using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;
using DStyle = Majorsilence.Forms.Drawing.FontStyle;

namespace Majorsilence.Forms.Tests
{
    // Second round of regressions found running a migrated designer-generated maintenance form beside the
    // same form on real WinForms: fonts that did not reach the paint, and wrapping that did not match GDI.
    [Collection ("Headless")]
    public class DesignerParityFontAndWrapTests
    {
        // Button chains its state styles to the TYPE-level state defaults, so walking CurrentStyle alone
        // skipped the control's own Font whenever it was disabled: a disabled 12pt button drew its caption
        // at the theme size, visibly smaller than the same button enabled.
        [Fact]
        public void A_disabled_button_keeps_its_own_font ()
        {
            using var form = new Form ();
            var font = new DFont ("Arial", 12F);
            var button = new Button { Font = font, Text = "&Print" };
            form.Controls.Add (button);

            var enabled_size = button.GetEffectiveFontSize ();
            var enabled_face = button.GetEffectiveFont ();

            button.Enabled = false;

            Assert.Equal (enabled_size, button.GetEffectiveFontSize ());
            Assert.Same (enabled_face, button.GetEffectiveFont ());
        }

        // A font set on the state style itself still wins over the instance's base font.
        [Fact]
        public void A_state_styles_own_font_size_still_wins ()
        {
            using var form = new Form ();
            var button = new Button { Font = new DFont ("Arial", 12F) };
            form.Controls.Add (button);

            button.StyleDisabled.FontSize = 30;
            button.Enabled = false;

            Assert.Equal (30, button.GetEffectiveFontSize ());
        }

        // ToolBarRenderer measured and drew every caption with the theme font, so a designer's
        // `Font = Microsoft Sans Serif 12pt` on each toolbar button sized and drew it at the theme size.
        [Fact]
        public void A_tool_strip_item_is_measured_with_its_own_font ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var strip = new ToolStrip ();
            var plain = new ToolStripButton ("Customer Records");
            var large = new ToolStripButton ("Customer Records") { Font = new DFont ("Arial", 20F) };
            strip.Items.Add (plain);
            strip.Items.Add (large);
            form.Controls.Add (strip);

            var renderer = RenderManager.GetRenderer<ToolBarRenderer> (strip)!;

            Assert.True (renderer.GetPreferredItemSize (strip, large, Size.Empty).Width
                       > renderer.GetPreferredItemSize (strip, plain, Size.Empty).Width);
        }

        // A drop-down's items inherit the font of the menu bar that opened them, as upstream's ToolStripItem.Font
        // does. MenuDropDownRenderer used the theme font outright, so `MenuStrip.Font = 12pt` sized the bar and
        // left every drop-down in the smaller theme font.
        [Fact]
        public void Drop_down_items_inherit_the_menu_bars_font ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var menu = new Menu { Font = new DFont ("Arial", 20F) };
            var top = new ToolStripMenuItem ("Customers");
            var records = new ToolStripMenuItem ("Records");
            var big = new ToolStripMenuItem ("Scan Notes") { Font = new DFont ("Arial", 30F) };
            top.DropDownItems.Add (records);
            top.DropDownItems.Add (big);
            menu.Items.Add (top);
            form.Controls.Add (menu);

            var drop_down = new MenuDropDown (top);
            var expected = (int) System.Math.Round (new DFont ("Arial", 20F).PixelSize);

            Assert.Equal (expected, StripItemFont.Resolve (records, drop_down).Size);
            Assert.Equal ((int) System.Math.Round (new DFont ("Arial", 30F).PixelSize), StripItemFont.Resolve (big, drop_down).Size);
        }

        // Upstream's ControlPaint.CreateTextFormatFlags gives Label and the whole button family
        // WordBreak | TextBoxControl: a last line that would only be partly visible is not drawn. A check box
        // one line high whose caption wraps showed its second line sliced in half.
        [Fact]
        public void A_wrapping_caption_one_line_high_keeps_only_its_whole_first_line ()
        {
            using var form = new Form ();
            var check = new CheckBox { Size = new Size (179, 25), Font = new DFont ("Arial", 12F, DStyle.Bold), Text = "Adjust To Minimum Tax" };
            form.Controls.Add (check);

            // Narrower than the caption's one line, whatever font the machine resolves Arial to, so it wraps.
            var font = check.GetEffectiveFont ();
            var size = check.LogicalToDeviceUnits (check.GetEffectiveFontSize ());
            var one_line = TextMeasurer.MeasureText (check.Text, font, size).Width;
            var bounds = new Rectangle (20, 0, (int) (one_line * 0.7), check.LogicalToDeviceUnits (25));
            var (box, align) = SkiaTextExtensions.WholeLineBox (check, check.Text, bounds, ContentAlignment.MiddleLeft);
            var line = TextMeasurer.MeasureText ("X", font, size).Height;

            Assert.Equal (ContentAlignment.TopLeft, align);
            Assert.Equal ((int) line, box.Height);
            Assert.Equal (bounds.Top + ((bounds.Height - box.Height) / 2), box.Top);
        }

        [Fact]
        public void Text_that_fits_keeps_its_box_and_alignment ()
        {
            using var form = new Form ();
            var label = new Label { Size = new Size (300, 25), Font = new DFont ("Arial", 12F), Text = "Short" };
            form.Controls.Add (label);

            var bounds = new Rectangle (0, 0, 300, 25);

            Assert.Equal ((bounds, ContentAlignment.MiddleLeft), SkiaTextExtensions.WholeLineBox (label, label.Text, bounds, ContentAlignment.MiddleLeft));
        }

        // Upstream derives DataGridViewColumn.IsDataBound from the binding. It was a bare property here, so a
        // legacy table-style shim that hides every column with IsDataBound == false once a source is assigned
        // showed the bound rows with no columns at all.
        [Fact]
        public void Predefined_columns_report_IsDataBound_once_a_matching_source_is_bound ()
        {
            using var form = new Form ();
            var grid = new DataGridView { AutoGenerateColumns = false };
            var code = new DataGridViewTextBoxColumn { Name = "code", DataPropertyName = "custcode" };
            var stray = new DataGridViewTextBoxColumn { Name = "stray", DataPropertyName = "nothing_here" };
            grid.Columns.Add (code);
            grid.Columns.Add (stray);
            form.Controls.Add (grid);

            var table = new System.Data.DataTable ("TABLE");
            table.Columns.Add ("CustCode", typeof (string));
            table.Rows.Add ("ACADI010");

            Assert.False (code.IsDataBound);

            var seen = (bool?) null;
            grid.DataSourceChanged += (_, _) => seen = code.IsDataBound;
            grid.DataSource = table;

            Assert.True (seen);   // already true when DataSourceChanged runs
            Assert.True (code.IsDataBound);
            Assert.False (stray.IsDataBound);

            // ... and the value is read through the same case-insensitive match ("custcode" vs "CustCode").
            Assert.Equal ("ACADI010", grid.Rows[0].Cells[code.Index].Value);

            var late = new DataGridViewTextBoxColumn { Name = "late", DataPropertyName = "CUSTCODE" };
            grid.Columns.Add (late);
            Assert.True (late.IsDataBound);

            grid.DataSource = null;
            Assert.False (code.IsDataBound);
        }

        // An MDI child's caption follows the Win32 rules a top-level form does: min/max as a pair (the refused one
        // shown but inert), the help button only when both are off, nothing at all without a ControlBox. A child
        // frame never drew the help button, so a designer HelpButton dialog shown as an MDI child lost its "?".
        [Fact]
        public void An_mdi_child_caption_follows_the_win32_button_rules ()
        {
            var original = MdiChildWindow.CaptionButtonsOnLeft;
            MdiChildWindow.CaptionButtonsOnLeft = false;

            try {
                MdiChildWindow Frame (bool min, bool max, bool help, bool box = true)
                {
                    var parent = new Form { IsMdiContainer = true };
                    var child = new Form { MdiParent = parent, MinimizeBox = min, MaximizeBox = max, HelpButton = help, ControlBox = box };
                    return new MdiChildWindow (parent.MdiClientControl!, child);
                }

                Assert.Equal (new[] { MdiChildWindow.CaptionHit.Help, MdiChildWindow.CaptionHit.Close },
                    Frame (min: false, max: false, help: true).CaptionButtonOrder ());

                Assert.Equal (new[] { MdiChildWindow.CaptionHit.Minimize, MdiChildWindow.CaptionHit.Maximize, MdiChildWindow.CaptionHit.Close },
                    Frame (min: true, max: false, help: true).CaptionButtonOrder ());

                Assert.Empty (Frame (min: true, max: true, help: false, box: false).CaptionButtonOrder ());
            } finally {
                MdiChildWindow.CaptionButtonsOnLeft = original;
            }
        }

        // Popups activate (to receive clicks), and the close-on-deactivate check only asked whether the NEWEST popup
        // -- an open submenu -- was active. Clicking back on its parent drop-down moves activation there, which read
        // as the application losing focus: the menu closed under the click and left the submenu floating.
        [Fact]
        public void Activating_the_parent_drop_down_of_an_open_submenu_keeps_the_menu_open ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            var strip = new MenuStrip ();
            var file = new ToolStripMenuItem ("File");
            var recent = new ToolStripMenuItem ("Recent");
            recent.DropDownItems.Add (new ToolStripMenuItem ("a.txt"));
            file.DropDownItems.Add (recent);
            strip.Items.Add (file);
            form.Controls.Add (strip);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 500, 200);

            var loc = WindowPoint.In (strip, file.Bounds.X + 5, file.Bounds.Y + 5);
            HeadlessRenderer.MouseDown (form, loc.X, loc.Y);
            HeadlessRenderer.MouseUp (form, loc.X, loc.Y);
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            recent.ShowDropDown ();
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            var file_popup = file.OpenDropDown!.FindWindow ()!;
            var recent_popup = recent.OpenDropDown!.FindWindow ()!;

            // The user clicks the File drop-down again: it activates, the submenu's popup deactivates.
            file_popup.OnBackendActivated ();
            recent_popup.OnBackendDeactivated ();
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            var file_open = file.IsDropDownOpened;
            var recent_open = recent.IsDropDownOpened;
            form.Close ();

            Assert.True (file_open, "the parent drop-down closed");
            Assert.True (recent_open, "the submenu closed");
        }

        // Upstream's DefaultCellStyle takes the grid's (ambient) ForeColor and Font, so a legacy grid inside a
        // container whose designer set Font = Tahoma 8pt / ForeColor = MidnightBlue draws its cells that way. The
        // cells here drew the theme colour and font regardless.
        [Fact]
        public void Grid_cells_draw_in_the_grids_ambient_colour ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (420, 160) };
            var host = new Panel { Bounds = new Rectangle (0, 0, 420, 160), Font = new DFont ("Arial", 12F), ForeColor = Color.MidnightBlue };
            var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, RowHeadersVisible = false };
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "code", DataPropertyName = "custcode", Width = 300 });
            host.Controls.Add (grid);
            form.Controls.Add (host);

            var table = new System.Data.DataTable ();
            table.Columns.Add ("custcode", typeof (string));
            table.Rows.Add ("WWWWWWWW"); table.Rows.Add ("WWWWWWWW");
            grid.DataSource = table;
            grid.ClearSelection ();
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var painted = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;

            var midnight = 0;
            for (var y = 0; y < painted.Height; y++)
                for (var x = 0; x < painted.Width; x++) {
                    var c = painted.GetPixel (x, y);
                    if (c.Blue > 90 && c.Blue - c.Red > 60 && c.Red < 60 && c.Green < 60)
                        midnight++;
                }

            Assert.True (midnight > 20, $"expected MidnightBlue cell text, found {midnight} such pixels");
            Assert.Equal ((int) System.Math.Round (new DFont ("Arial", 12F).PixelSize), grid.CellFallbackFont ().Size);
        }

        // Upstream: clicking a drop-down item that has children opens (or keeps open) its submenu. Here the press
        // closed the submenu the hover had just opened, so clicking "Transfer Balances" left its parent open and
        // nothing else.
        [Fact]
        public void Clicking_a_parent_item_in_a_drop_down_leaves_its_submenu_open ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            var strip = new MenuStrip ();
            var file = new ToolStripMenuItem ("File");
            var recent = new ToolStripMenuItem ("Recent");
            recent.DropDownItems.Add (new ToolStripMenuItem ("a.txt"));
            file.DropDownItems.Add (new ToolStripMenuItem ("Open"));
            file.DropDownItems.Add (recent);
            strip.Items.Add (file);
            form.Controls.Add (strip);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 500, 200);

            var loc = WindowPoint.In (strip, file.Bounds.X + 5, file.Bounds.Y + 5);
            HeadlessRenderer.MouseDown (form, loc.X, loc.Y);
            HeadlessRenderer.MouseUp (form, loc.X, loc.Y);
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            var drop_down = file.OpenDropDown!;
            var popup = drop_down.FindWindow ()!;
            var at = WindowPoint.In (drop_down, recent.Bounds.X + 10, recent.Bounds.Y + (recent.Bounds.Height / 2));

            HeadlessRenderer.MouseMove (popup, at.X, at.Y);
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            // As on a real desktop, the submenu the hover opened is the active popup when the press arrives.
            var recent_popup = (PopupWindow) recent.OpenDropDown!.FindWindow ()!;
            Application.ActivePopupWindow = recent_popup;

            HeadlessRenderer.MouseDown (popup, at.X, at.Y);
            HeadlessRenderer.MouseUp (popup, at.X, at.Y);
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            var file_open = file.IsDropDownOpened && popup.Visible;
            var recent_open = recent.IsDropDownOpened && recent_popup.Visible;
            form.Close ();

            Assert.True (file_open, "the parent drop-down closed");
            Assert.True (recent_open, "the submenu did not stay open");
        }

        // Upstream formats an unformatted DateTime cell through DateTimeConverter: the short date alone at
        // midnight, date and short time otherwise. ToString () appended the time to every bound date.
        [Fact]
        public void An_unformatted_date_cell_shows_the_short_date_at_midnight ()
        {
            var original = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            try {
                using var form = new Form ();
                var grid = new DataGridView ();
                form.Controls.Add (grid);

                var table = new System.Data.DataTable ();
                table.Columns.Add ("when", typeof (DateTime));
                table.Rows.Add (new DateTime (2017, 11, 16));
                table.Rows.Add (new DateTime (2017, 11, 16, 14, 30, 0));
                grid.DataSource = table;

                Assert.Equal ("11/16/2017", grid.Rows[0].Cells[0].FormattedValue);
                Assert.Equal ("11/16/2017 14:30", grid.Rows[1].Cells[0].FormattedValue);
            } finally {
                System.Globalization.CultureInfo.CurrentCulture = original;
            }
        }

        // Upstream's DateTimePicker passes CustomFormat to the native control, where '/' and ':' are literal. .NET
        // reads them as the culture's separators, so "MM/dd/yyyy" printed "10-14-2026" under en-CA. And a picker
        // is always framed upstream; it lost its frame with its TextBox base class.
        [Fact]
        public void A_custom_date_format_keeps_its_separators_and_the_picker_is_framed ()
        {
            var original = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo ("en-CA") {
                DateTimeFormat = { DateSeparator = "-" },
            };

            try {
                using var picker = new DateTimePicker {
                    Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy HH:mm 'at' ':'", Value = new DateTime (2026, 10, 14, 9, 5, 0),
                };

                Assert.Equal ("10/14/2026 09:05 at :", picker.Text);
                Assert.Equal (1, picker.CurrentStyle.Border.Top.GetWidth ());
            } finally {
                System.Globalization.CultureInfo.CurrentCulture = original;
            }
        }

        // Telerik's RadGridView shows its group panel and groups by default, and filtering means the filtering
        // row; funnels in the header cells come only with ShowHeaderCellButtons. Here the panel and grouping were
        // off and EnableFiltering put funnels in the headers instead of the row.
        [Fact]
        public void RadGridView_grouping_and_filtering_defaults_follow_Telerik ()
        {
            using var grid = new Majorsilence.Forms.Telerik.RadGridView ();
            grid.Columns.Add (new Majorsilence.Forms.Telerik.GridViewTextBoxColumn { Name = "a", HeaderText = "A" });   // bands show with columns

            Assert.True (grid.ShowGroupPanel);
            Assert.True (grid.EnableGrouping);
            Assert.True (grid.ShowFilteringRow);
            Assert.False (grid.ShowFilterRow);
            Assert.False (grid.ShowsHeaderFilterButtons);

            grid.EnableFiltering = true;
            Assert.True (grid.ShowFilterRow);
            Assert.True (grid.FilterRowBandHeight > 0);
            Assert.False (grid.ShowsHeaderFilterButtons);

            grid.MasterTemplate.ShowFilteringRow = false;
            Assert.False (grid.ShowFilterRow);

            grid.MasterTemplate.ShowHeaderCellButtons = true;
            Assert.True (grid.ShowsHeaderFilterButtons);

            // Whole rows, as Telerik selects by default -- and as the Telerik-typed SelectionMode reports.
            Assert.Equal (DataGridViewSelectionMode.FullRowSelect, ((DataGridView) grid).SelectionMode);
        }

        // Telerik shows the group panel and filtering row only once a grid has columns, and an empty grid says
        // "No data to display". Here an unbound grid showed a group panel over a blank area.
        [Fact]
        public void An_empty_RadGridView_shows_only_its_no_data_text ()
        {
            using var deterministic = HeadlessRenderer.UseDeterministic ();

            using var form = new Form { Size = new System.Drawing.Size (400, 300) };
            var grid = new Majorsilence.Forms.Telerik.RadGridView { Left = 0, Top = 0, Width = 300, Height = 200, EnableFiltering = true };
            form.Controls.Add (grid);
            form.Show ();

            Assert.Equal (0, grid.GroupPanelBandHeight);
            Assert.Equal (0, grid.FilterRowBandHeight);

            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;

            // Ink across the middle of the grid: the centred text.
            var ink = 0;
            var mid = bitmap.Height / 2;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = mid - grid.LogicalToDeviceUnits (8); y < mid + grid.LogicalToDeviceUnits (8); y++)
                    if (bitmap.GetPixel (x, y) is var c && c.Red < 100 && c.Green < 100 && c.Blue < 100)
                        ink++;
            Assert.True (ink > 20, $"no text in the middle of an empty grid ({ink} dark pixels)");

            grid.Columns.Add (new Majorsilence.Forms.Telerik.GridViewTextBoxColumn { Name = "a", HeaderText = "A" });
            Assert.True (grid.GroupPanelBandHeight > 0);
            Assert.True (grid.FilterRowBandHeight > 0);
        }

        // Selecting a container control in tab order passes the focus on to its own first control, as
        // upstream's ContainerControl.Select (directed) does. A UserControl wrapping a TextBox (a date
        // field, say) took the focus itself, so the TextBox inside never got it, nor its Enter event.
        [Fact]
        public void Tab_order_selection_enters_a_user_controls_first_control ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var inner = new TextBox { Width = 100 };
            var entered = false;
            inner.Enter += (_, _) => entered = true;
            var wrapper = new UserControl { Left = 10, Top = 10, Width = 120, Height = 30, TabIndex = 0 };
            wrapper.Controls.Add (inner);
            form.Controls.Add (wrapper);
            form.Controls.Add (new Button { Left = 10, Top = 60, TabIndex = 1 });

            Assert.True (form.SelectNextControl (null, true, true, true, false));
            Assert.True (inner.Focused);
            Assert.True (entered);
        }

        // Hiding a control repaints what it uncovered, as upstream's parent repaints the area a hidden child
        // window leaves. A "loading" progress bar hidden once its data arrived stayed on screen: nothing
        // invalidated the parent, and the form's client area never cleared its retained surface.
        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void A_hidden_control_is_painted_over (bool inPanel)
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new System.Drawing.Size (300, 100) };
            var bar = new ProgressBar { Location = new System.Drawing.Point (100, 40), Size = new System.Drawing.Size (100, 23), Value = 50, Visible = false };
            if (inPanel) {
                var panel = new Panel { Dock = DockStyle.Fill };
                panel.Controls.Add (bar);
                form.Controls.Add (panel);
            } else {
                form.Controls.Add (bar);
            }

            form.Load += (_, _) => bar.Visible = true;   // shown while loading, as such forms do
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            bar.Visible = false;
            using var bitmap = SkiaSharp.SKBitmap.Decode (HeadlessRenderer.CapturePng (form));

            // The bar's fill colour where the bar was (device pixels; the capture includes the caption above
            // the client area).
            var top = bitmap.Height - form.LogicalToDeviceUnits (form.ClientSize.Height);
            var left = form.LogicalToDeviceUnits (100);
            var bar_top = top + form.LogicalToDeviceUnits (40);
            var fill = 0;
            for (var x = left + 2; x < left + form.LogicalToDeviceUnits (100) - 2; x++)
                for (var y = bar_top + 2; y < bar_top + form.LogicalToDeviceUnits (23) - 2; y++)
                    if (bitmap.GetPixel (x, y) is var c && c.Blue > 150 && c.Red < 80)
                        fill++;

            Assert.Equal (0, fill);
        }

        // A focused button keeps its explicit colours, as upstream's does. Its :focus style chains to the type
        // default rather than the instance, so focus swapped the designer's BackColor and ForeColor for the
        // defaults -- on every Show () once a form focuses its first control.
        [Fact]
        public void A_focused_button_keeps_its_explicit_colours ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var button = new Button { BackColor = System.Drawing.Color.Gold, ForeColor = System.Drawing.Color.DarkRed };
            form.Controls.Add (button);
            form.Show ();
            button.Focus ();

            Assert.True (button.Focused);
            Assert.Equal (System.Drawing.Color.Gold.ToArgb (), button.BackColor.ToArgb ());
            Assert.Equal (System.Drawing.Color.DarkRed.ToArgb (), button.ForeColor.ToArgb ());
        }

        // Telerik centres column header text by default; the compat columns inherited WinForms' left alignment.
        [Fact]
        public void Telerik_column_headers_are_centred_by_default ()
        {
            var text = new Majorsilence.Forms.Telerik.GridViewTextBoxColumn ();
            Assert.Equal (ContentAlignment.MiddleCenter, text.HeaderTextAlignment);

            text.HeaderTextAlignment = ContentAlignment.MiddleLeft;
            Assert.Equal (ContentAlignment.MiddleLeft, text.HeaderAlignment);
        }

        // Upstream's text cells end what does not fit with "..." unless they wrap; here the text was clipped
        // mid-letter, so a cut-off value looked complete.
        [Fact]
        public void An_overflowing_cell_ends_in_an_ellipsis ()
        {
            // The bundled font, so the strokes measured below are the same on every machine.
            using var deterministic = HeadlessRenderer.UseDeterministic ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var grid = new DataGridView { Left = 0, Top = 0, Width = 200, Height = 100, RowHeadersVisible = false, ColumnHeadersVisible = false };
            grid.Columns.Add ("a", "A");
            grid.Columns[0].Width = 40;
            grid.AllowUserToAddRows = false;
            grid.Rows.Add (new string ('l', 60));
            grid.ClearSelection ();
            form.Controls.Add (grid);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;

            // The cell's last ink is the ellipsis's final dot -- a couple of pixels tall -- where clipped
            // text ends on a full-height letter stroke. Device pixels throughout (the back buffer's).
            var cell = grid.GetCellBounds (0, 0);
            bool Dark (int x, int y) => bitmap.GetPixel (x, y) is var c && c.Red < 128 && c.Green < 128;
            var lastInk = -1;
            for (var x = cell.Left; x < cell.Right; x++)
                for (var y = cell.Top + 1; y < cell.Bottom - 1; y++)
                    if (Dark (x, y)) { lastInk = x; break; }
            var height = 0;
            for (var y = cell.Top + 1; y < cell.Bottom - 1; y++)
                if (lastInk >= 0 && Dark (lastInk, y))
                    height++;

            Assert.True (lastInk >= 0 && height <= grid.LogicalToDeviceUnits (3), $"the text ends at x={lastInk} on a {height}px stroke, not a dot");
        }

        // Telerik's header cells end a caption that does not fit with "..."; the compat grid clipped it
        // mid-letter, which with a filter funnel beside it read as a misspelt column name.
        [Fact]
        public void A_Telerik_header_that_does_not_fit_ends_in_an_ellipsis ()
        {
            // The bundled font, so the strokes measured below are the same on every machine.
            using var deterministic = HeadlessRenderer.UseDeterministic ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var grid = new Majorsilence.Forms.Telerik.RadGridView { Left = 0, Top = 0, Width = 200, Height = 100, ShowGroupPanel = false };
            grid.RowHeadersVisible = false;
            grid.Columns.Add (new Majorsilence.Forms.Telerik.GridViewTextBoxColumn { Name = "a", HeaderText = new string ('l', 60), Width = 50 });
            form.Controls.Add (grid);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;

            // The header's last ink is the ellipsis's final dot, not a full-height letter stroke.
            var top = 0;
            var bottom = grid.ScaledHeaderHeight;
            bool Dark (int x, int y) => bitmap.GetPixel (x, y) is var c && c.Red < 110 && c.Green < 110 && c.Blue < 110;
            var lastInk = -1;
            for (var x = 2; x < grid.LogicalToDeviceUnits (48); x++)
                for (var y = top + 3; y < bottom - 3; y++)
                    if (Dark (x, y)) { lastInk = x; break; }
            var height = 0;
            for (var y = top + 3; y < bottom - 3; y++)
                if (lastInk >= 0 && Dark (lastInk, y))
                    height++;

            Assert.True (lastInk >= 0 && height <= grid.LogicalToDeviceUnits (3), $"the header ends at x={lastInk} on a {height}px stroke, not a dot");
        }

        // Upstream's picker frames the text and the drop-down button together. Here the button face was
        // filled edge to edge over the border, so the frame stopped where the button began.
        [Fact]
        public void The_picker_frame_runs_round_the_drop_down_button ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var picker = new DateTimePicker { Left = 10, Top = 10, Width = 120, Height = 22 };
            form.Controls.Add (picker);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (picker, null)!;

            // The frame's colour along the text part's top edge is the colour along the button's edges too.
            var frame = bitmap.GetPixel (bitmap.Width / 4, 0);
            Assert.Equal (frame, bitmap.GetPixel (bitmap.Width - 1, bitmap.Height / 2));
            Assert.Equal (frame, bitmap.GetPixel (bitmap.Width - 5, 0));
            Assert.Equal (frame, bitmap.GetPixel (bitmap.Width - 5, bitmap.Height - 1));
        }

        // Upstream's spin box frames its up/down buttons with the text; their faces were filled over the border.
        [Fact]
        public void The_spin_box_frame_runs_round_its_buttons ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var spin = new NumericUpDown { Left = 10, Top = 10, Width = 120, Height = 23 };
            form.Controls.Add (spin);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (spin, null)!;

            var frame = bitmap.GetPixel (bitmap.Width / 4, 0);
            Assert.Equal (frame, bitmap.GetPixel (bitmap.Width - 1, bitmap.Height / 4));
            Assert.Equal (frame, bitmap.GetPixel (bitmap.Width - 5, 0));
            Assert.Equal (frame, bitmap.GetPixel (bitmap.Width - 5, bitmap.Height - 1));
        }

        // Upstream's column headers inherit the grid's Font; here they always drew in the theme's bold font,
        // so a grid set to a regular 8pt face had bold headers over regular cells.
        [Fact]
        public void Column_headers_inherit_the_grid_font ()
        {
            using var form = new Form ();
            var grid = new DataGridView { Font = new Majorsilence.Forms.Drawing.Font ("Tahoma", 8f) };
            form.Controls.Add (grid);

            var header = grid.HeaderFallbackFont ();
            var cell = grid.CellFallbackFont ();
            Assert.Equal (cell.Typeface.FamilyName, header.Typeface.FamilyName);
            Assert.Equal (cell.Size, header.Size);
            Assert.False (header.Typeface.IsBold);

            // A grid in an app that never chose a font keeps the theme's header style.
            using var plain = new DataGridView ();
            Assert.Equal (DataGridView.DefaultColumnHeaderStyle.GetFont (), plain.HeaderFallbackFont ().Typeface);
        }

        // In RowHeaderSelect -- the default -- upstream selects the clicked cell, and a whole row only from its
        // row header. Here every cell click selected and painted the whole row, as FullRowSelect does.
        [Fact]
        public void RowHeaderSelect_selects_the_cell_and_the_row_only_from_its_header ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", "A");
            grid.Columns.Add ("b", "B");
            for (var r = 0; r < 3; r++)
                grid.Rows.Add ($"a{r}", $"b{r}");
            grid.ClearSelection ();

            Assert.Equal (DataGridViewSelectionMode.RowHeaderSelect, grid.SelectionMode);

            grid.SelectFromPointer (1, 1, Keys.None);
            Assert.Empty (grid.SelectedRows);
            Assert.Same (grid[1, 1], Assert.Single (grid.SelectedCells));
            Assert.True (grid.IsCellPaintedSelected (1, 1));
            Assert.False (grid.IsRowPaintedSelected (1));

            grid.SelectFromPointer (2, -1, Keys.None);
            Assert.Same (grid.Rows[2], Assert.Single (grid.SelectedRows));
            Assert.True (grid.IsRowPaintedSelected (2));
        }

        // Upstream fills a selected cell with the selection colour, as it does a selected row; here a selected
        // cell was only outlined, so the current cell of a grid in the default RowHeaderSelect looked unselected.
        [Fact]
        public void A_selected_cell_is_filled_with_the_selection_colour ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var grid = new DataGridView { Left = 0, Top = 0, Width = 240, Height = 120, RowHeadersVisible = false, AllowUserToAddRows = false };
            grid.Columns.Add ("a", "A");
            grid.Columns.Add ("b", "B");
            grid.Rows.Add ("", "");
            form.Controls.Add (grid);
            form.Show ();
            grid.CurrentCell = grid[0, 0];
            Assert.True (grid.IsCellPaintedSelected (0, 0));

            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;

            // GetCellBounds is in device pixels, as the back buffer is.
            var selected = grid.GetCellBounds (0, 0);
            var other = grid.GetCellBounds (0, 1);
            var expected = DataGridView.DefaultSelectionStyle.GetBackgroundColor ();
            Assert.Equal (expected, bitmap.GetPixel (selected.Left + selected.Width / 2, selected.Top + selected.Height / 2));
            Assert.NotEqual (expected, bitmap.GetPixel (other.Left + other.Width / 2, other.Top + other.Height / 2));
        }

        // Upstream wraps column header captions (the header style's WrapMode defaults to True) and, with
        // ColumnHeadersHeightSizeMode.AutoSize, sizes the header band to the tallest wrapped caption. AutoSize
        // was stored and ignored and captions drew on one line, so "Payment Made" in a narrow column was cut.
        [Fact]
        public void AutoSize_headers_grow_to_fit_a_wrapped_caption ()
        {
            using var deterministic = HeadlessRenderer.UseDeterministic ();
            using var grid = new DataGridView { ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize };
            grid.Columns.Add ("a", "Short");
            grid.Columns[0].Width = 200;

            var one_line = grid.ColumnHeadersHeight;

            grid.Columns.Add ("b", "Payment Made");
            grid.Columns[1].Width = 40;   // too narrow for the caption on one line

            Assert.True (grid.HeaderWraps);
            Assert.True (grid.ColumnHeadersHeight > one_line * 3 / 2, $"{grid.ColumnHeadersHeight} is not two lines (one is {one_line})");

            // An explicit no-wrap header style keeps one line.
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            Assert.Equal (one_line, grid.ColumnHeadersHeight);
        }

        // Upstream holds no rows without columns, so a fresh grid with no columns shows only its background. Here it
        // drew an empty new-row line (and its row header) in every column-less grid waiting for data.
        [Fact]
        public void A_grid_without_columns_has_no_new_row ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var grid = new DataGridView { Left = 0, Top = 0, Width = 200, Height = 120 };
            form.Controls.Add (grid);

            Assert.Equal (-1, grid.NewRowIndex);
            Assert.Equal (0, grid.RowCountWithNewRow);

            // Nothing but the background: no header band, no row header.
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;
            var background = bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2);
            Assert.Equal (background, bitmap.GetPixel (bitmap.Width / 2, 8));
            Assert.Equal (background, bitmap.GetPixel (12, 8));
            Assert.Equal (background, bitmap.GetPixel (12, 30));

            grid.Columns.Add ("a", "A");
            Assert.Equal (0, grid.NewRowIndex);
        }

        // FlowLayout's container RTL check was stubbed to false, so a FlowLayoutPanel { RightToLeft = Yes } -- the
        // usual designer way to right-align a row of buttons -- laid them out in reverse: upstream puts the FIRST
        // child against the right edge and flows leftward.
        [Fact]
        public void A_right_to_left_flow_panel_places_its_first_child_at_the_right_edge ()
        {
            using var form = new Form ();
            var panel = new FlowLayoutPanel { Bounds = new Rectangle (0, 0, 400, 40), RightToLeft = RightToLeft.Yes };
            var first = new Button { Size = new Size (100, 28), Text = "Address Search" };
            var second = new Button { Size = new Size (100, 28), Text = "Attribute Search" };
            panel.Controls.Add (first);
            panel.Controls.Add (second);
            form.Controls.Add (panel);
            panel.PerformLayout ();

            Assert.True (first.Left > second.Left, $"first at {first.Left}, second at {second.Left}");
            Assert.Equal (panel.ClientSize.Width - first.Margin.Right, first.Right);
        }

        // Upstream mirrors CheckAlign/TextAlign (RtlTranslateContent) for a RightToLeft = Yes check box, so the box
        // sits at the right with the caption before it -- the usual "Interest [ ]" field. The alignments were taken as
        // written, so it drew box-then-caption.
        [Fact]
        public void A_right_to_left_check_box_puts_its_box_at_the_right ()
        {
            using var form = new Form ();
            var check = new CheckBox { Size = new Size (110, 27), Text = "Active", RightToLeft = RightToLeft.Yes, TextAlign = ContentAlignment.MiddleRight };
            var plain = new CheckBox { Size = new Size (110, 27), Text = "Active" };
            form.Controls.Add (check);
            form.Controls.Add (plain);

            var rtl = Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (check);
            var ltr = Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (plain);

            Assert.True (rtl.GlyphBounds.Left > rtl.TextBounds.Left, "the box should follow the caption");
            Assert.True (ltr.GlyphBounds.Left < ltr.TextBounds.Left, "a plain check box keeps box-then-caption");
            Assert.Equal (ContentAlignment.MiddleLeft, rtl.TextAlign);
        }

        // GDI widens every glyph of a synthesized bold (a bold asked of a family with no bold face) by about a
        // pixel; DirectWrite/FreeType by much less. Designer layouts were measured in GDI, so bold captions came
        // out narrower than laid out for -- a label that wraps upstream stayed on one line and ran under the next
        // control. Microsoft Sans Serif is the classic case; it exists only on Windows.
        [Fact]
        public void A_synthesized_bold_is_widened_to_GDIs_advance_and_a_real_bold_face_is_not ()
        {
            // Windows only: a synthesized bold is recognised by sharing the regular face's font file, which
            // the macOS port (where this family also ships) does not expose the same way -- there the bold
            // keeps Skia's own advance, as it did before.
            if (!OperatingSystem.IsWindows ())
                return;

            using var regular = SKFontManager.Default.MatchFamily ("Microsoft Sans Serif", SKFontStyle.Normal);

            if (regular is null || regular.FamilyName != "Microsoft Sans Serif")
                return;   // not installed here

            var synthetic = new DFont ("Microsoft Sans Serif", 12F, DStyle.Bold).GetSKTypeface ();
            var spacing = TextMeasurer.GdiBoldSpacing (synthetic, 16);

            Assert.InRange (spacing, 0.1f, 1f);
            Assert.Equal (0f, TextMeasurer.GdiBoldSpacing (regular, 16));

            using var arial_bold = SKFontManager.Default.MatchFamily ("Arial", SKFontStyle.Bold);
            if (arial_bold is { FamilyName: "Arial" })
                Assert.Equal (0f, TextMeasurer.GdiBoldSpacing (arial_bold, 16));
        }
    }
}
