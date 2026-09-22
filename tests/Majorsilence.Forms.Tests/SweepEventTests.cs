using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Printing;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6.1 sweep: the remaining unraised events that had a hook site in reach. One test per event,
/// each subscribing at the public surface and driving the public path that upstream raises from.
/// </summary>
public class SweepEventTests
{
    // ── DataGridView: the three ContextMenuStrip change events ───────────────────────────────────────

    private static DataGridView GridWithOneCell ()
    {
        var grid = new DataGridView ();
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "A" });
        grid.Rows.Add ();
        return grid;
    }

    [Fact]
    public void ColumnContextMenuStripChanged_fires_when_a_columns_menu_changes ()
    {
        using var grid = GridWithOneCell ();
        var fired = 0;
        grid.ColumnContextMenuStripChanged += (_, e) => { fired++; Assert.Same (grid.Columns[0], e.Column); };

        var menu = new ContextMenuStrip ();
        grid.Columns[0].ContextMenuStrip = menu;
        grid.Columns[0].ContextMenuStrip = menu;

        Assert.Equal (1, fired);
    }

    [Fact]
    public void RowContextMenuStripChanged_fires_when_a_rows_menu_changes ()
    {
        using var grid = GridWithOneCell ();
        var fired = 0;
        grid.RowContextMenuStripChanged += (_, e) => { fired++; Assert.Same (grid.Rows[0], e.Row); };

        grid.Rows[0].ContextMenuStrip = new ContextMenuStrip ();

        Assert.Equal (1, fired);
    }

    [Fact]
    public void CellContextMenuStripChanged_fires_with_the_cells_coordinates ()
    {
        using var grid = GridWithOneCell ();
        var fired = 0;
        grid.CellContextMenuStripChanged += (_, e) => { fired++; Assert.Equal (0, e.ColumnIndex); Assert.Equal (0, e.RowIndex); };

        grid.Rows[0].Cells[0].ContextMenuStrip = new ContextMenuStrip ();

        Assert.Equal (1, fired);
    }

    // ── Command relays ──────────────────────────────────────────────────────────────────────────────

    private sealed class FakeCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute (object? parameter) => true;
        public void Execute (object? parameter) { }
        public void Raise () => CanExecuteChanged?.Invoke (this, EventArgs.Empty);
    }

    private sealed class FakeExecutor : ICommandExecutor
    {
        public event EventHandler? CommandCanExecuteChanged;
        public void Execute () { }
        public void Raise () => CommandCanExecuteChanged?.Invoke (this, EventArgs.Empty);
    }

    [Fact]
    public void ToolStripItem_relays_its_commands_CanExecuteChanged ()
    {
        var item = new ToolStripButton ();
        var command = new FakeCommand ();
        var fired = 0;
        item.CommandCanExecuteChanged += (s, _) => { fired++; Assert.Same (item, s); };

        item.Command = command;
        command.Raise ();
        Assert.Equal (1, fired);

        item.Command = null;
        command.Raise ();
        Assert.Equal (1, fired); // detached with the command
    }

    [Fact]
    public void ButtonBase_relays_its_commands_CanExecuteChanged ()
    {
        using var button = new Button ();
        var command = new FakeExecutor ();
        var fired = 0;
        button.CommandCanExecuteChanged += (_, _) => fired++;

        button.Command = command;
        command.Raise ();
        button.Command = null;
        command.Raise ();

        Assert.Equal (1, fired);
    }

    // ── Application modal bracket ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RunModal_brackets_the_loop_with_EnterThreadModal_and_LeaveThreadModal ()
    {
        HeadlessRenderer.Use ();
        var entered = 0; var left = 0;
        EventHandler onEnter = (_, _) => entered++;
        EventHandler onLeave = (_, _) => left++;
        Application.EnterThreadModal += onEnter;
        Application.LeaveThreadModal += onLeave;
        try {
            Assert.Equal (7, Form.RunModal (Task.FromResult (7)));
        } finally {
            Application.EnterThreadModal -= onEnter;
            Application.LeaveThreadModal -= onLeave;
        }

        Assert.Equal (1, entered);
        Assert.Equal (1, left);
    }

    // ── Strips ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DropDownItemClicked_fires_when_a_drop_down_child_is_clicked ()
    {
        var parent = new ToolStripMenuItem ("File");
        var child = new ToolStripMenuItem ("Open");
        parent.DropDownItems.Add (child);

        var fired = 0;
        parent.DropDownItemClicked += (_, e) => { fired++; Assert.Same (child, e.ClickedItem); };

        child.PerformClick ();

        Assert.Equal (1, fired);
    }

    [Fact]
    public void DropDownItemClicked_stops_for_a_removed_child ()
    {
        var parent = new ToolStripMenuItem ("File");
        var child = new ToolStripMenuItem ("Open");
        parent.DropDownItems.Add (child);
        var fired = 0;
        parent.DropDownItemClicked += (_, _) => fired++;

        parent.DropDownItems.Remove (child);
        child.PerformClick ();

        Assert.Equal (0, fired);
    }

    [Fact]
    public void LayoutCompleted_fires_after_the_strip_lays_out ()
    {
        using var strip = new ToolStrip { Size = new Size (300, 30) };
        strip.Items.Add ("One");
        var fired = 0;
        strip.LayoutCompleted += (_, _) => fired++;

        // A strip lays its items out as it paints (MenuBase.OnPaint), so paint it.
        using var bitmap = PaintSurface.Render (strip);

        Assert.Equal (1, fired);
    }

    [Fact]
    public void StatusStrip_LayoutCompleted_fires_after_its_own_layout ()
    {
        using var strip = new StatusStrip { Size = new Size (300, 24) };
        strip.Items.Add ("Ready");
        var fired = 0;
        strip.LayoutCompleted += (_, _) => fired++;

        // A strip lays its items out as it paints (MenuBase.OnPaint), so paint it.
        using var bitmap = PaintSurface.Render (strip);

        Assert.Equal (1, fired);
    }

    [Fact]
    public void ToolStripProgressBar_forwards_the_hosted_bars_key_and_validation_events ()
    {
        var item = new ToolStripProgressBar ();
        var down = 0; var press = 0; var up = 0; var validating = 0; var validated = 0;
        item.KeyDown += (_, _) => down++;
        item.KeyPress += (_, _) => press++;
        item.KeyUp += (_, _) => up++;
        item.Validating += (_, _) => validating++;
        item.Validated += (_, _) => validated++;

        item.ProgressBar.RaiseKeyDown (new KeyEventArgs (Keys.A));
        item.ProgressBar.RaiseKeyPress (new KeyPressEventArgs ('a'));
        item.ProgressBar.RaiseKeyUp (new KeyEventArgs (Keys.A));
        item.ProgressBar.RaiseValidation ();

        Assert.Equal ((1, 1, 1, 1, 1), (down, press, up, validating, validated));
    }

    [Fact]
    public void ToolStripContentPanel_Load_fires_once_on_creation ()
    {
        using var panel = new ToolStripContentPanel ();
        var fired = 0;
        panel.Load += (_, _) => fired++;

        panel.CreateControl ();
        panel.CreateControl ();

        Assert.Equal (1, fired);
    }

    [Fact]
    public void Form_MenuStart_and_MenuComplete_bracket_a_menu_strips_activation ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var menu = new MenuStrip ();
        var file = new ToolStripMenuItem ("File");
        menu.Items.Add (file);
        form.Controls.Add (menu);

        var started = 0; var completed = 0;
        form.MenuStart += (_, _) => started++;
        form.MenuComplete += (_, _) => completed++;

        menu.SelectedItem = file;
        Assert.Equal (1, started);
        Assert.Equal (0, completed);

        menu.Deactivate ();
        Assert.Equal (1, completed);
    }

    // ── Printing ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void QueryPageSettings_is_asked_before_each_page ()
    {
        var document = new PrintDocument ();
        var pages = 0; var queries = 0;
        document.QueryPageSettings += (_, e) => { queries++; Assert.NotNull (e.PageSettings); };
        document.PrintPage += (_, e) => { pages++; e.HasMorePages = pages < 2; };

        document.Print ();

        Assert.Equal (2, pages);
        Assert.Equal (2, queries);
    }

    [Fact]
    public void A_cancelled_QueryPageSettings_ends_the_job_before_the_page ()
    {
        var document = new PrintDocument ();
        var pages = 0; var ended = 0;
        document.QueryPageSettings += (_, e) => e.Cancel = true;
        document.PrintPage += (_, _) => pages++;
        document.EndPrint += (_, _) => ended++;

        document.Print ();

        Assert.Equal (0, pages);
        Assert.Equal (1, ended);
    }

    // ── Text and lists ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsOverwriteModeChanged_fires_only_when_the_effective_mode_flips ()
    {
        using var box = new MaskedTextBox ();
        var fired = 0;
        box.IsOverwriteModeChanged += (_, _) => fired++;

        box.InsertKeyMode = InsertKeyMode.Insert;     // Default already resolves to insert: no flip
        Assert.Equal (0, fired);
        box.InsertKeyMode = InsertKeyMode.Overwrite;
        Assert.Equal (1, fired);
        Assert.True (box.IsOverwriteMode);
        box.InsertKeyMode = InsertKeyMode.Default;
        Assert.Equal (2, fired);
    }

    [Fact]
    public void Format_is_offered_each_display_text_when_FormattingEnabled ()
    {
        using var list = new ListBox { FormattingEnabled = true };
        list.Items.Add ("alpha");
        var fired = 0;
        list.Format += (_, e) => { fired++; e.Value = ((string)e.Value!).ToUpperInvariant (); };

        Assert.Equal ("ALPHA", list.GetItemText ("alpha"));
        Assert.Equal (1, fired);

        list.FormattingEnabled = false;
        Assert.Equal ("alpha", list.GetItemText ("alpha"));
        Assert.Equal (1, fired);
    }

    [Fact]
    public void ComboBox_Format_is_offered_too ()
    {
        using var combo = new ComboBox { FormattingEnabled = true };
        combo.Format += (_, e) => e.Value = "[" + e.Value + "]";

        Assert.Equal ("[x]", combo.GetItemText ("x"));
    }

    // ── Collections ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DataGridViewColumnCollection_CollectionChanged_reports_add_remove_and_clear ()
    {
        using var grid = new DataGridView ();
        var actions = new List<CollectionChangeAction> ();
        grid.Columns.CollectionChanged += (_, e) => actions.Add (e.Action);

        var column = new DataGridViewTextBoxColumn { Name = "A" };
        grid.Columns.Add (column);
        grid.Columns.Remove (column);
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "B" });
        grid.Columns.Clear ();

        Assert.Equal (new[] { CollectionChangeAction.Add, CollectionChangeAction.Remove, CollectionChangeAction.Add, CollectionChangeAction.Refresh }, actions);
    }

    [Fact]
    public void BindingsCollection_CollectionChanging_fires_before_each_change_and_CollectionChanged_after ()
    {
        // ControlBindingsCollection is a Collection<Binding>, not a BindingsCollection; this is the
        // WinForms base type itself, whose Add/Remove/Clear are protected internal.
        var bindings = new BindingsCollection ();
        var binding = new Binding ("Text", new { Name = "n" }, "Name");
        var before = new List<int> (); var after = new List<int> (); var actions = new List<CollectionChangeAction> ();
        bindings.CollectionChanging += (_, e) => { before.Add (bindings.Count); actions.Add (e.Action); };
        bindings.CollectionChanged += (_, _) => after.Add (bindings.Count);

        bindings.Add (binding);
        bindings.Remove (binding);
        bindings.Add (binding);
        bindings.Clear ();

        Assert.Equal (new[] { 0, 1, 0, 1 }, before);
        Assert.Equal (new[] { 1, 0, 1, 0 }, after);
        Assert.Equal (new[] { CollectionChangeAction.Add, CollectionChangeAction.Remove, CollectionChangeAction.Add, CollectionChangeAction.Refresh }, actions);
    }

    [Fact]
    public void BindingContext_CollectionChanged_fires_when_a_manager_is_first_created ()
    {
        var context = new BindingContext ();
        var fired = 0;
        context.CollectionChanged += (_, e) => { fired++; Assert.Equal (CollectionChangeAction.Add, e.Action); };
        var list = new List<string> { "a" };

        var first = context[list];
        var again = context[list];

        Assert.Same (first, again);
        Assert.Equal (1, fired);
    }

    // ── Dialogs, help, tool tips ────────────────────────────────────────────────────────────────────

    private sealed class FakeFileDialog : FileDialog
    {
        public DialogResult Answer = DialogResult.OK;
        public override Task<DialogResult> ShowDialogAsync (Form owner) => Task.FromResult (Answer);
    }

    [Fact]
    public void FileOk_fires_on_an_accepted_dialog_and_can_turn_it_into_Cancel ()
    {
        HeadlessRenderer.Use ();
        using var owner = new Form ();
        using var dialog = new FakeFileDialog ();
        var fired = 0; var cancel = false;
        dialog.FileOk += (_, e) => { fired++; e.Cancel = cancel; };

        Assert.Equal (DialogResult.OK, dialog.ShowDialogSync (owner));
        cancel = true;
        Assert.Equal (DialogResult.Cancel, dialog.ShowDialogSync (owner));
        dialog.Answer = DialogResult.Cancel;
        Assert.Equal (DialogResult.Cancel, dialog.ShowDialogSync (owner));

        Assert.Equal (2, fired); // not asked when the dialog itself was cancelled
    }

    [Fact]
    public void F1_raises_HelpRequested_up_the_parents_and_then_on_the_form ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var panel = new Panel ();
        var button = new Button ();
        panel.Controls.Add (button);
        form.Controls.Add (panel);

        var order = new List<string> ();
        button.HelpRequested += (_, _) => order.Add ("button");
        panel.HelpRequested += (_, _) => order.Add ("panel");
        form.HelpRequested += (_, _) => order.Add ("form");

        button.RaiseKeyDown (new KeyEventArgs (Keys.F1));
        Assert.Equal (new[] { "button", "panel", "form" }, order);

        order.Clear ();
        button.RaiseKeyDown (new KeyEventArgs (Keys.A));
        Assert.Empty (order);
    }

    [Fact]
    public void A_handled_HelpRequested_stops_at_the_handler ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var button = new Button ();
        form.Controls.Add (button);

        var form_heard = 0;
        button.HelpRequested += (_, e) => e.Handled = true;
        form.HelpRequested += (_, _) => form_heard++;

        button.RaiseKeyDown (new KeyEventArgs (Keys.F1));

        Assert.Equal (0, form_heard);
    }

    [Fact]
    public void ToolTip_Popup_is_asked_before_the_tip_shows_and_can_cancel_it ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var button = new Button { Bounds = new Rectangle (10, 10, 80, 24) };
        form.Controls.Add (button);
        form.Show ();

        using var tip = new ToolTip ();
        var fired = 0; var cancel = false;
        tip.Popup += (_, e) => { fired++; Assert.Same (button, e.AssociatedControl); Assert.True (e.ToolTipSize.Width > 0); e.Cancel = cancel; };

        tip.ShowItemTip (button, "hello", new Point (5, 5));
        Assert.Equal (1, fired);

        cancel = true;
        tip.ShowItemTip (button, "again", new Point (5, 5));
        Assert.Equal (2, fired);
    }

    [Fact]
    public void TaskDialog_Help_button_raises_the_pages_HelpRequest_and_keeps_the_dialog_open ()
    {
        HeadlessRenderer.Use ();
        var page = new TaskDialogPage { Heading = "Q", Buttons = { TaskDialogButton.Help, TaskDialogButton.OK } };
        var help = 0;
        page.HelpRequest += (_, _) => help++;

        TaskDialogButton? chosen = null;
        Exception? failure = null;
        var thread = new Thread (() => {
            try { chosen = TaskDialog.ShowDialog (page); } catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };

        Backends.Platform.Backend.Post (() => {
            var form = Application.OpenForms.Cast<Form> ().First (f => f.Controls.GetAllControls ().OfType<Button> ().Any (b => b.Text == "Help"));
            var buttons = form.Controls.GetAllControls ().OfType<Button> ().ToList ();
            buttons.First (b => b.Text == "Help").PerformClick ();
            Assert.Equal (1, help);
            Assert.True (form.Visible, "the Help button must not close the dialog");
            buttons.First (b => b.Text == "OK").PerformClick ();
        });
        thread.Start ();

        Assert.True (thread.Join (TimeSpan.FromSeconds (10)), "the task dialog never returned");
        if (failure is not null)
            throw failure;

        Assert.Equal (1, help);
        Assert.Equal ("OK", chosen?.Text);
    }
}
