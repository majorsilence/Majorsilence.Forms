using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Majorsilence.Forms
{
    // W5.1 (findings DGV-01 P0, DGV-06, DGV-07, DGV-08, DGV-09, DGV-10, DGV-11): the editing lifecycle.
    //
    // The grid could edit a cell -- through one non-WinForms overload, BeginEdit (row, column). Almost
    // everything around that worked by accident or not at all:
    //
    //  * BeginEdit (bool), the ONLY public WinForms way to start an edit from code, was
    //    `{ return true; }` -- it reported success and edited nothing (DGV-01, P0).
    //  * EditMode was stored and read by nothing, so EditProgrammatically still opened the editor on
    //    F2 and double-click, and EditOnKeystroke never opened it on a keystroke (DGV-06).
    //  * Column.ReadOnly and Cell.ReadOnly were consulted only by InheritedState, so a read-only
    //    column edited happily and wrote back to the bound object (DGV-07).
    //  * IsCurrentCellDirty was `edit_textbox is not null` -- true the instant editing began, before
    //    any change -- and CurrentCellDirtyStateChanged was never raised, so the canonical
    //    commit-a-checkbox-immediately idiom could not work (DGV-08).
    //  * CancelEdit tore the editor down without raising CellEndEdit, leaving handlers that re-enable
    //    buttons there stuck after Escape (DGV-09).
    //  * The commit path stored the editor's raw string unless a CellParsing handler was attached, and
    //    a failed write-back was swallowed silently -- DataError was declared and never raised
    //    anywhere in the type (DGV-10).
    //  * Moving the current cell raised none of CurrentCellChanged, CellLeave, CellValidating,
    //    CellValidated or CellEnter (DGV-11).
    public partial class DataGridView
    {
        // DGV-08: dirty means "the editor's contents differ from what was there when editing began",
        // not "an editor exists". Tracked rather than derived, because the transition is what the
        // event reports and a derived value has no transition to report.
        private bool current_cell_dirty;

        // DGV-08: set when a commit changes a cell in the current row, cleared when the current row
        // changes. Upstream's IsCurrentRowDirty means "something in this row was committed since it
        // became current", which is a different question from whether the editor is dirty now.
        private bool current_row_dirty;

        /// <summary>
        /// Gets whether the cell being edited has uncommitted changes. False immediately after
        /// <see cref="BeginEdit(bool)"/> and true once the editor's contents change.
        /// </summary>
        public bool IsCurrentCellDirty => current_cell_dirty;

        /// <summary>
        /// Gets whether any cell in the current row has been committed since the row became current,
        /// or the cell being edited is dirty.
        /// </summary>
        public bool IsCurrentRowDirty => current_row_dirty || current_cell_dirty;

        /// <summary>
        /// Tells the grid that the editing control's contents have changed. Mirrors WinForms: a custom
        /// editing control calls this so the grid raises
        /// <see cref="CurrentCellDirtyStateChanged"/> and <see cref="IsCurrentCellDirty"/> becomes true.
        /// </summary>
        public void NotifyCurrentCellDirty (bool dirty) => SetCurrentCellDirty (dirty);

        // The one place the flag moves, so the event fires on the transition and only on the transition.
        private void SetCurrentCellDirty (bool dirty)
        {
            if (current_cell_dirty == dirty)
                return;

            current_cell_dirty = dirty;
            OnCurrentCellDirtyStateChanged (EventArgs.Empty);
        }

        /// <summary>Raises the <see cref="CurrentCellDirtyStateChanged"/> event.</summary>
        protected virtual void OnCurrentCellDirtyStateChanged (EventArgs e)
            => CurrentCellDirtyStateChanged?.Invoke (this, e);

        // ---------------- DGV-01 / DGV-07: starting an edit

        /// <summary>
        /// Begins editing the current cell. Returns whether the cell entered edit mode -- so a caller
        /// can tell that a read-only cell, a cancelled <see cref="CellBeginEdit"/> or no current cell
        /// meant no edit began.
        /// </summary>
        /// <param name="selectAll">
        /// True to select the cell's whole contents, false to place the caret at the end.
        /// </param>
        public bool BeginEdit (bool selectAll)
        {
            if (selected_row_index < 0 || selected_column_index < 0)
                return false;

            BeginEdit (selected_row_index, selected_column_index);

            if (!IsCurrentCellInEditMode)
                return false;

            if (!selectAll && edit_textbox is { } editor)
                editor.Select (editor.TextLength, 0);

            return true;
        }

        /// <summary>
        /// Whether the given cell may be edited: the grid, the column, the row and the cell all get a
        /// veto. <see cref="DataGridViewCell.InheritedState"/> already folds all four together, which is
        /// why it is asked rather than the four properties being re-checked here (DGV-07).
        /// </summary>
        internal bool IsCellEditable (int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                return false;

            var row = Rows[rowIndex];

            if (columnIndex < 0 || columnIndex >= row.Cells.Count)
                return false;

            return !row.Cells[columnIndex].InheritedState.HasFlag (DataGridViewElementStates.ReadOnly);
        }

        // ---------------- DGV-06: what actually starts an edit

        /// <summary>
        /// Gets the control hosting the current edit, or null when no cell is being edited. Mirrors
        /// WinForms, where <c>grid.EditingControl as TextBox</c> is how a handler reaches the editor to
        /// attach its own <c>KeyPress</c>.
        /// </summary>
        public Control? EditingControl => edit_textbox;

        // True when this EditMode opens an editor for the given trigger. Upstream spreads these checks
        // through four call sites; one predicate keeps them from drifting apart.
        private bool EditModeAllows (DataGridViewEditTrigger trigger)
            => edit_mode switch {
                DataGridViewEditMode.EditProgrammatically => false,
                DataGridViewEditMode.EditOnEnter => trigger is DataGridViewEditTrigger.CellBecameCurrent
                    or DataGridViewEditTrigger.F2 or DataGridViewEditTrigger.Keystroke or DataGridViewEditTrigger.Click,
                DataGridViewEditMode.EditOnF2 => trigger is DataGridViewEditTrigger.F2,
                DataGridViewEditMode.EditOnKeystroke => trigger is DataGridViewEditTrigger.Keystroke or DataGridViewEditTrigger.Click,
                DataGridViewEditMode.EditOnKeystrokeOrF2 => trigger is DataGridViewEditTrigger.Keystroke
                    or DataGridViewEditTrigger.F2 or DataGridViewEditTrigger.Click,
                _ => true,
            };

        // Why the trigger is named rather than passed as a bool: EditProgrammatically has to refuse all
        // four, and EditOnF2 has to refuse a keystroke while accepting F2 -- a single "can edit" flag
        // cannot express either.
        private enum DataGridViewEditTrigger
        {
            F2,
            Keystroke,
            Click,
            CellBecameCurrent,
        }

        // DGV-06: a keystroke over a cell begins the edit and the character that started it becomes the
        // editor's contents, rather than being swallowed.
        internal bool TryBeginEditFromKeystroke (KeyEventArgs e)
        {
            Guard.ThrowIfNull (e);

            if (read_only || IsCurrentCellInEditMode || !EditModeAllows (DataGridViewEditTrigger.Keystroke))
                return false;

            if (selected_row_index < 0 || selected_column_index < 0 || !IsCellEditable (selected_row_index, selected_column_index))
                return false;

            if (selected_column_index >= Rows[selected_row_index].Cells.Count
                || !Rows[selected_row_index].Cells[selected_column_index].KeyEntersEditMode (e))
                return false;

            if (!BeginEdit (selectAll: true) || edit_textbox is not { } editor)
                return false;

            // The typed character replaces the selected contents, which is what selectAll: true set up
            // -- typing over a cell in WinForms overwrites, it does not append.
            var typed = CharacterFor (e);

            if (typed is not null)
                editor.Text = typed;

            editor.Select (editor.TextLength, 0);
            SetCurrentCellDirty (true);

            return true;
        }

        // The character a key event types, or null when it types nothing. Deliberately narrow: this is
        // only used to seed an editor, so anything it cannot map simply opens an empty editor.
        private static string? CharacterFor (KeyEventArgs e)
        {
            var key = e.KeyCode;

            if (key is >= Keys.A and <= Keys.Z)
                return ((char)((e.Shift ? 'A' : 'a') + (key - Keys.A))).ToString ();

            if (key is >= Keys.D0 and <= Keys.D9 && !e.Shift)
                return ((char)('0' + (key - Keys.D0))).ToString ();

            if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
                return ((char)('0' + (key - Keys.NumPad0))).ToString ();

            return null;
        }

        // DGV-06: F2 is only an edit trigger in the two EditModes that say so.
        internal bool TryBeginEditFromF2 ()
            => !read_only
               && EditModeAllows (DataGridViewEditTrigger.F2)
               && selected_row_index >= 0 && selected_column_index >= 0
               && BeginEdit (selectAll: true);

        // DGV-06: clicking the cell that is already current begins editing it, which is how a
        // single-click-to-edit grid behaves without any handler being written.
        internal void TryBeginEditFromClick (int rowIndex, int columnIndex, bool wasAlreadyCurrent)
        {
            if (!wasAlreadyCurrent || read_only || IsCurrentCellInEditMode)
                return;

            if (!EditModeAllows (DataGridViewEditTrigger.Click) || !IsCellEditable (rowIndex, columnIndex))
                return;

            BeginEdit (rowIndex, columnIndex);
        }

        // DGV-06: EditOnEnter opens the editor as soon as a cell becomes current, from any path.
        private void TryBeginEditOnEnter ()
        {
            if (read_only || IsCurrentCellInEditMode || edit_mode != DataGridViewEditMode.EditOnEnter)
                return;

            if (selected_row_index < 0 || selected_column_index < 0)
                return;

            if (IsCellEditable (selected_row_index, selected_column_index))
                BeginEdit (selected_row_index, selected_column_index);
        }

        // ---------------- DGV-10: the commit conversion, and reporting failure

        /// <summary>
        /// The type the editor's text must be converted to before it is stored: the column's declared
        /// <see cref="DataGridViewColumn.ValueType"/>, else the cell's, else the bound member's, else
        /// string.
        /// </summary>
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "TypeDescriptor is how WinForms resolves the bound member's type; a trimmed descriptor degrades to the reflection path alongside it.")]
        [UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "Data binding requires runtime reflection over user-provided types.")]
        private Type CommitValueType (DataGridViewCell cell, int columnIndex)
        {
            var declared = (columnIndex >= 0 && columnIndex < Columns.Count ? Columns[columnIndex].ValueType : null)
                ?? cell.ValueType;

            if (declared is not null)
                return declared;

            // A bound column with no declared ValueType still has a type: the property it maps to.
            if (columnIndex >= 0 && columnIndex < Columns.Count
                && !string.IsNullOrEmpty (Columns[columnIndex].DataPropertyName)
                && data_source is { Count: > 0 } source && source[0] is { } sample) {
                var descriptor = TypeDescriptor.GetProperties (sample)[Columns[columnIndex].DataPropertyName];

                if (descriptor is not null)
                    return descriptor.PropertyType;
            }

            return typeof (string);
        }

        /// <summary>
        /// Converts the editor's text to the cell's value type for a commit. Throws when it cannot,
        /// which is what <see cref="EndEdit()"/> turns into a <see cref="DataError"/>.
        /// </summary>
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "Cell value types are supplied by the application, exactly as in WinForms; a trimmed converter degrades to the same failure this method already reports through DataError.")]
        [UnconditionalSuppressMessage ("Trimming", "IL2067",
            Justification = "See above: the target type comes from the column, the cell or the bound member and cannot be annotated here.")]
        private static object? ParseForCommit (DataGridViewCell cell, string text, Type target)
        {
            // Empty text is "no value", not the empty string, for anything but a string column --
            // otherwise clearing a numeric cell stores "" and every later cast throws.
            if (string.IsNullOrEmpty (text) && target != typeof (string))
                return cell.InheritedStyle?.DataSourceNullValue;

            if (target == typeof (string))
                return text;

            var underlying = Nullable.GetUnderlyingType (target) ?? target;

            // The cell's own hook first, so a derived cell type that overrides it is honoured rather
            // than bypassed. It converts to the CELL's ValueType, though, which is usually unset when
            // the type was declared on the column -- so it hands the string straight back and the
            // result has to be checked rather than trusted.
            var parsed = cell.ParseFormattedValue (text, cell.InheritedStyle, null, null);

            if (parsed is not null && (target.IsInstanceOfType (parsed) || underlying.IsInstanceOfType (parsed)))
                return parsed;

            // Convert to the type the commit actually resolved -- column, then cell, then bound member.
            // A converter that refuses throws, which is the DataError path and the point: "abc" in an
            // int column must be reported, not silently dropped.
            var converter = TypeDescriptor.GetConverter (underlying);

            if (converter.CanConvertFrom (typeof (string)))
                return converter.ConvertFromString (text);

            throw new FormatException ($"'{text}' could not be converted to {target}.");
        }

        /// <summary>
        /// Raises <see cref="DataError"/>. Returns true when the caller should rethrow, which is how
        /// upstream lets a handler escalate a swallowed conversion failure back into an exception.
        /// </summary>
        protected virtual bool OnDataError (bool displayErrorDialogIfNoHandler, DataGridViewDataErrorEventArgs e)
        {
            Guard.ThrowIfNull (e);

            // WinForms shows a message box when nothing is listening. A UI framework that pops a dialog
            // from a commit is a worse default here than a silent failure the caller can opt into
            // seeing, so the parameter is honoured by doing nothing -- documented, not accidental.
            _dataError?.Invoke (this, e);

            return e.ThrowException;
        }
    }
}
