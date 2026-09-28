using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Printing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, eighteenth chunk: the grid's editing-control protocol, the binding managers' list
// mutation, data-bound error display, a format-keyed clipboard, F1 help, style refresh and tool strip
// layout persistence -- twenty-seven empty-bodied methods.
[Collection ("Headless")]
public class W6EditingBindingAndSettingsTests
{
    // ── the grid's editing-control protocol ─────────────────────────────────────────────────────────

    private sealed class RecordingCell : DataGridViewTextBoxCell
    {
        internal int Seeded;
        internal int Detached;

        public override void InitializeEditingControl (int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            Seeded++;
            base.InitializeEditingControl (rowIndex, initialFormattedValue, dataGridViewCellStyle);
        }

        public override void DetachEditingControl ()
        {
            Detached++;
            base.DetachEditingControl ();
        }

        public override object Clone () => new RecordingCell { Value = Value };
    }

    private static DataGridView TextGrid (out Form form, out RecordingCell cell)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 460, Height = 300 };
        var grid = new DataGridView { Width = 380, Height = 200, AllowUserToAddRows = false };
        grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "Name", Width = 120 });

        var row = new DataGridViewRow ();
        cell = new RecordingCell { Value = "original" };
        row.Cells.Add (cell);
        grid.Rows.Add (row);

        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.RenderOnForm (grid, 1f).Dispose ();
        grid.SelectedRowIndex = 0;
        grid.SelectedColumnIndex = 0;
        return grid;
    }

    [Fact]
    public void The_cell_seeds_its_editor_and_releases_it_again ()
    {
        var grid = TextGrid (out var form, out var cell);

        using (form) {
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            Assert.True (grid.BeginEdit (true));
            var editor = Assert.IsType<DataGridViewTextBoxEditingControl> (grid.EditingControl);

            // The override ran, and the base did its seeding: the cell's style reached the editor,
            // which nothing else in the edit path applies.
            Assert.Equal (1, cell.Seeded);
            Assert.Equal (HorizontalAlignment.Right, editor.TextAlign);
            Assert.Same (grid, editor.EditingControlDataGridView);
            Assert.Equal (0, editor.EditingControlRowIndex);

            Assert.True (grid.EndEdit ());

            Assert.Equal (1, cell.Detached);
            Assert.Null (editor.EditingControlDataGridView);
            Assert.Equal (-1, editor.EditingControlRowIndex);
        }
    }

    [Fact]
    public void RefreshEdit_puts_the_cells_value_back_and_leaves_the_cell_clean ()
    {
        var grid = TextGrid (out var form, out _);

        using (form) {
            Assert.True (grid.BeginEdit (true));
            var editor = Assert.IsType<DataGridViewTextBoxEditingControl> (grid.EditingControl);

            editor.Text = "typed over";
            Assert.True (grid.IsCurrentCellDirty);

            grid.RefreshEdit ();

            Assert.Equal ("original", editor.Text);
            Assert.False (grid.IsCurrentCellDirty);
            Assert.True (grid.IsCurrentCellInEditMode);
            Assert.Equal ("original".Length, editor.SelectionLength);
        }
    }

    [Fact]
    public void The_combo_editor_selects_its_text_when_asked_to ()
    {
        using var editor = new DataGridViewComboBoxEditingControl { DropDownStyle = ComboBoxStyle.DropDown };
        editor.Items.Add ("alpha");
        editor.Text = "alpha";
        editor.Select (0, 0);

        editor.PrepareEditingControlForEdit (selectAll: false);
        Assert.Equal (0, editor.SelectionLength);

        editor.PrepareEditingControlForEdit (selectAll: true);
        Assert.Equal ("alpha".Length, editor.SelectionLength);
    }

    // ── binding managers mutate their list ──────────────────────────────────────────────────────────

    private sealed class Person : IDataErrorInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Error => string.Empty;

        public string this[string columnName]
            => columnName == nameof (Name) && Name.Length == 0 ? "Name is required" : string.Empty;
    }

    private sealed class NoDefaultConstructor
    {
        public NoDefaultConstructor (int _) { }
    }

    [Fact]
    public void A_currency_manager_adds_and_removes_through_its_list ()
    {
        var people = new List<Person> { new () { Name = "a" } };
        var context = new BindingContext ();
        var manager = Assert.IsType<CurrencyManager> (context[people]);

        var positions = 0;
        manager.PositionChanged += (_, _) => positions++;

        manager.AddNew ();

        // The list grew, the new item is current, and the move was announced even though a List<T>
        // announces nothing itself.
        Assert.Equal (2, people.Count);
        Assert.Equal (1, manager.Position);
        Assert.Same (people[1], manager.Current);
        Assert.Equal (1, positions);

        manager.RemoveAt (1);

        Assert.Single (people);
        Assert.Equal (0, manager.Position);

        // A manager over one object has no list to add to, which upstream says the same way.
        var single = context[new Person ()];
        Assert.Throws<NotSupportedException> (() => single.AddNew ());
    }

    [Fact]
    public void AllowNew_is_what_the_list_implies_until_set_and_ResetAllowNew_takes_the_override_away ()
    {
        using var fixed_size = new BindingSource { DataSource = new[] { 1, 2, 3 } };
        Assert.False (fixed_size.AllowNew);

        using var no_constructor = new BindingSource { DataSource = new List<NoDefaultConstructor> () };
        Assert.False (no_constructor.AllowNew);

        using var constructible = new BindingSource { DataSource = new List<Person> () };
        Assert.True (constructible.AllowNew);

        // The override wins, and the reset gives the list back its say.
        constructible.AllowNew = false;
        Assert.False (constructible.AllowNew);

        constructible.ResetAllowNew ();
        Assert.True (constructible.AllowNew);

        // A type that cannot construct itself can still be supplied by a handler: AddNew checks the
        // list, not the constructor, as upstream does.
        no_constructor.AddingNew += (_, e) => e.NewObject = new NoDefaultConstructor (1);
        Assert.NotNull (no_constructor.AddNew ());
    }

    // ── data-bound errors ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_error_provider_bound_to_a_source_shows_the_current_items_errors ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var box = new TextBox { Width = 120 };
        form.Controls.Add (box);
        form.Show ();

        var people = new List<Person> { new (), new () { Name = "complete" } };
        using var source = new BindingSource { DataSource = people };
        box.DataBindings.Add ("Text", source, "Name");

        using var provider = new ErrorProvider { ContainerControl = form };
        provider.DataSource = source;

        // The current item is missing its name, so its bound box carries that error.
        Assert.Equal ("Name is required", provider.GetError (box));

        // Fixing the item and asking again clears it ...
        people[0].Name = "now set";
        provider.UpdateBinding ();
        Assert.Equal (string.Empty, provider.GetError (box));

        // ... and moving to another item re-reads without being asked.
        people[0].Name = string.Empty;
        source.Position = 1;
        Assert.Equal (string.Empty, provider.GetError (box));
        source.Position = 0;
        Assert.Equal ("Name is required", provider.GetError (box));
    }

    // ── a format-keyed clipboard ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Named_formats_survive_the_clipboard_within_the_process ()
    {
        HeadlessRenderer.Use ();
        Clipboard.Clear ();

        var payload = new Person { Name = "clip" };
        Clipboard.SetData ("Majorsilence.Person", payload);

        Assert.True (Clipboard.ContainsData ("Majorsilence.Person"));
        Assert.Same (payload, Clipboard.GetData ("Majorsilence.Person"));
        Assert.False (Clipboard.ContainsData ("Something.Else"));

        // The data object is a snapshot that carries every format, and editing it is editing the
        // snapshot, not the clipboard -- until it is put back.
        var snapshot = Clipboard.GetDataObject ();
        Assert.True (snapshot.GetDataPresent ("Majorsilence.Person"));

        snapshot.SetData ("Second", 42);
        Assert.False (Clipboard.ContainsData ("Second"));

        Clipboard.SetDataObject (snapshot);
        Assert.Equal (42, Clipboard.GetData ("Second"));

        Clipboard.Clear ();
        Assert.False (Clipboard.ContainsData ("Majorsilence.Person"));
        Assert.False (Clipboard.ContainsData ("Second"));
    }

    // ── F1 help ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void F1_shows_a_controls_help_string_unless_its_help_is_turned_off ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var helped = new TextBox { Width = 120, Top = 10 };
        var silenced = new TextBox { Width = 120, Top = 50 };
        form.Controls.Add (helped);
        form.Controls.Add (silenced);
        form.Show ();

        var reached_form = 0;
        form.HelpRequested += (_, _) => reached_form++;

        using var provider = new HelpProvider ();
        provider.SetHelpString (helped, "Type the customer's name.");
        provider.SetHelpString (silenced, "Never shown.");
        provider.SetShowHelp (silenced, false);
        provider.SetHelpNavigator (helped, HelpNavigator.Topic);

        Assert.True (provider.GetShowHelp (helped));
        Assert.False (provider.GetShowHelp (silenced));
        Assert.Equal (HelpNavigator.Topic, provider.GetHelpNavigator (helped));

        silenced.RaiseKeyDown (new KeyEventArgs (Keys.F1));

        // Help is off for this one: nothing shown, and the request went on up to the form.
        Assert.NotEqual ("Never shown.", Help.PopupText);
        Assert.Equal (1, reached_form);

        helped.RaiseKeyDown (new KeyEventArgs (Keys.F1));

        Assert.Equal ("Type the customer's name.", Help.PopupText);
        Assert.Equal (1, reached_form);
    }

    // ── style refresh ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateStyles_raises_StyleChanged_on_a_control_and_on_a_window ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 200, Height = 120 };
        var panel = new Panel ();
        form.Controls.Add (panel);
        form.Show ();

        var control_changes = 0;
        var window_changes = 0;
        panel.StyleChanged += (_, _) => control_changes++;
        form.StyleChanged += (_, _) => window_changes++;

        panel.UpdateStyles ();
        Assert.Equal (1, control_changes);
        Assert.Equal (0, window_changes);

        form.UpdateStyles ();
        Assert.Equal (1, window_changes);
    }

    // ── the DataGrid initialize pair ────────────────────────────────────────────────────────────────

    private sealed class CountingDataGrid : DataGrid
    {
        internal int Layouts;

        protected override void OnLayout (LayoutEventArgs e)
        {
            Layouts++;
            base.OnLayout (e);
        }
    }

    [Fact]
    public void The_legacy_grid_batches_its_initialization ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var grid = new CountingDataGrid { Width = 300, Height = 200 };
        form.Controls.Add (grid);
        form.Show ();

        grid.Layouts = 0;

        grid.BeginInit ();
        grid.Width = 280;
        grid.Height = 180;
        Assert.Equal (0, grid.Layouts);

        grid.EndInit ();
        Assert.True (grid.Layouts > 0, "EndInit should lay the grid out once");
    }

    // ── the group box renderer's parent background ─────────────────────────────────────────────────

    [Fact]
    public void The_group_box_renderer_fills_with_the_nearest_opaque_ancestors_colour ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 200, Height = 120 };
        var parent = new Panel { BackColor = Color.Red, Width = 100, Height = 60 };
        var child = new GroupBox { BackColor = Color.Transparent, Width = 40, Height = 30 };
        parent.Controls.Add (child);
        form.Controls.Add (parent);

        using var surface = new SKBitmap (20, 20, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (surface);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);

        GroupBoxRenderer.DrawParentBackground (graphics, new Rectangle (0, 0, 20, 20), child);

        Assert.Equal (SKColors.Red, surface.GetPixel (10, 10));
    }

    // ── the preview controller closes its page ──────────────────────────────────────────────────────

    [Fact]
    public void The_preview_controller_releases_the_page_surface_it_handed_out ()
    {
        using var document = new PrintDocument ();
        var controller = new PreviewPrintController ();
        using var scratch = new SKBitmap (4, 4);
        using var scratch_canvas = new SKCanvas (scratch);
        var page = new PrintPageEventArgs (new Majorsilence.Forms.Drawing.SkiaGraphics (scratch_canvas), new RectangleF (0, 0, 100, 100), new RectangleF (0, 0, 120, 120), document.DefaultPageSettings);

        controller.OnStartPrint (document, new PrintEventArgs ());
        var graphics = controller.OnStartPage (document, page);
        Assert.NotNull (graphics);
        Assert.False (graphics!.IsDisposed);

        controller.OnEndPage (document, page);

        Assert.True (graphics.IsDisposed);
        Assert.Single (controller.GetPreviewPageInfo ());
    }

    // ── tool strip layout persistence ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_saved_strip_layout_comes_back_on_load ()
    {
        HeadlessRenderer.Use ();
        var file = Path.Combine (Path.GetTempPath (), "majorsilence-strips-" + Guid.NewGuid ().ToString ("N") + ".txt");
        var previous = ToolStripManager.SettingsPath;
        ToolStripManager.SettingsPath = file;

        try {
            using var form = new Form { Width = 400, Height = 300 };
            var panel = new ToolStripPanel { Name = "topPanel", Dock = DockStyle.Top };
            var first = new ToolStrip { Name = "firstStrip", Width = 120, Height = 25 };
            var second = new ToolStrip { Name = "secondStrip", Width = 120, Height = 25 };
            var floating = new ToolStrip { Name = "floatingStrip", Width = 90, Height = 25, Dock = DockStyle.None, Location = new Point (10, 100) };
            var nameless = new ToolStrip { Width = 60, Height = 25, Dock = DockStyle.None, Location = new Point (200, 150) };
            panel.Join (first);
            panel.Join (second);
            form.Controls.Add (panel);
            form.Controls.Add (floating);
            form.Controls.Add (nameless);
            form.Show ();
            form.PerformLayout ();

            // The panel gives each strip its own row, in the panel's order: first above second.
            Assert.True (first.Top < second.Top);
            second.Visible = false;

            ToolStripManager.SaveSettings (form, "layout-test");

            // The user rearranges everything; the saved layout is not what is on screen any more.
            panel.Controls.SetChildIndex (second, 0);
            second.Visible = true;
            form.Controls.Add (first);   // out of the panel altogether
            floating.Location = new Point (0, 0);
            nameless.Location = new Point (0, 0);
            form.PerformLayout ();
            Assert.NotSame (panel, first.Parent);

            ToolStripManager.LoadSettings (form, "layout-test");
            form.PerformLayout ();

            Assert.Same (panel, first.Parent);
            Assert.True (first.Top < second.Top, "the saved order should put first back above second");
            Assert.False (second.Visible);
            Assert.Equal (new Point (10, 100), floating.Location);

            // A strip with no name cannot be matched on the way back, so it is left alone.
            Assert.Equal (new Point (0, 0), nameless.Location);

            // A different key is a different layout.
            floating.Location = new Point (5, 5);
            ToolStripManager.LoadSettings (form, "some-other-key");
            Assert.Equal (new Point (5, 5), floating.Location);
        } finally {
            ToolStripManager.SettingsPath = previous;

            try {
                File.Delete (file);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
    }
}
