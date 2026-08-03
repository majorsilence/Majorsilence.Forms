using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // The DataGridView row, cell and column family (docs/winforms-gap-plan.md).
    //
    // The column collection's Get*Column family is the counterpart of the row collection's, added in
    // item 9, and it matters for the same reason: walking a grid by state -- the next visible column,
    // the last frozen one -- is how grid code is written, and open-coding that walk at every call
    // site against a DataGridViewElementStates value is what the methods exist to avoid.
    //
    // DataGridViewElement and DataGridViewBand are the upstream bases of the cell, row and column
    // types. They are declared here because migrated code names them in signatures and pattern
    // matches on them, and because the shared Selected/State surface genuinely belongs in one place.

    /// <summary>The base of the elements a <see cref="DataGridView"/> is built from.</summary>
    public class DataGridViewElement
    {
        /// <summary>Gets the grid this element belongs to.</summary>
        public DataGridView? DataGridView { get; internal set; }

        /// <summary>Gets the element's state.</summary>
        public virtual DataGridViewElementStates State => DataGridViewElementStates.Visible;

        /// <summary>Called when the element has been added to a grid.</summary>
        protected virtual void OnDataGridViewChanged () { }

        /// <summary>Raises an event on the owning grid.</summary>
        protected void RaiseCellClick (DataGridViewCellEventArgs e) { }

        /// <inheritdoc cref="RaiseCellClick"/>
        protected void RaiseCellContentClick (DataGridViewCellEventArgs e) { }

        /// <inheritdoc cref="RaiseCellClick"/>
        protected void RaiseCellValueChanged (DataGridViewCellEventArgs e) { }

        /// <inheritdoc cref="RaiseCellClick"/>
        protected void RaiseDataError (DataGridViewDataErrorEventArgs e) { }
    }

    /// <summary>The shared base of a <see cref="DataGridView"/>'s rows and columns.</summary>
    public class DataGridViewBand : DataGridViewElement, IDisposable
    {
        /// <summary>Gets or sets the style applied to the band's cells by default.</summary>
        public DataGridViewCellStyle? DefaultCellStyle { get; set; }

        /// <summary>Gets or sets the band's position within its collection.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Gets or sets whether the band scrolls out of view.</summary>
        public virtual bool Frozen { get; set; }

        /// <summary>Gets or sets whether the band's cells can be edited.</summary>
        public virtual bool ReadOnly { get; set; }

        /// <summary>Gets or sets whether the band can be resized by the user.</summary>
        public virtual DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>Gets or sets whether the band is selected.</summary>
        public virtual bool Selected { get; set; }

        /// <summary>Gets or sets whether the band is shown.</summary>
        public virtual bool Visible { get; set; } = true;

        /// <summary>Gets or sets arbitrary data associated with the band.</summary>
        public object? Tag { get; set; }

        /// <summary>Releases the resources used by the band.</summary>
        public void Dispose ()
        {
            Dispose (disposing: true);
            GC.SuppressFinalize (this);
        }

        /// <summary>Releases the resources used by the band.</summary>
        protected virtual void Dispose (bool disposing) { }
    }

    /// <summary>A cell that hosts its own editing control.</summary>
    public interface IDataGridViewEditingCell
    {
        /// <summary>Gets or sets the value being edited, in its formatted form.</summary>
        object? EditingCellFormattedValue { get; set; }

        /// <summary>Gets or sets whether the value has been changed by the user.</summary>
        bool EditingCellValueChanged { get; set; }

        /// <summary>Returns the value being edited.</summary>
        object? GetEditingCellFormattedValue (DataGridViewDataErrorContexts context);

        /// <summary>Prepares the cell for editing.</summary>
        void PrepareEditingCellForEdit (bool selectAll);
    }

    public partial class DataGridViewRow
    {
        private AccessibleObject? accessibility_object;

        /// <summary>Gets the accessible object describing this row.</summary>
        public AccessibleObject AccessibilityObject
            => accessibility_object ??= new DataGridViewRowAccessibleObject (this);

        /// <summary>Gets or sets the context menu shown when this row's header is right-clicked.</summary>
        public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

        /// <summary>Gets or sets the height of the divider below this row.</summary>
        public int DividerHeight { get; set; }

        /// <summary>Gets whether the row is currently on screen.</summary>
        public virtual bool Displayed => Visible && DataGridView is not null;

        /// <summary>Builds this row's cells from the given grid's columns.</summary>
        public void CreateCells (DataGridView dataGridView)
        {
            ArgumentNullException.ThrowIfNull (dataGridView);

            Cells.Clear ();

            foreach (var column in dataGridView.Columns)
                Cells.Add (column.CellTemplate?.Clone () as DataGridViewCell ?? new DataGridViewTextBoxCell ());
        }

        /// <inheritdoc cref="CreateCells(DataGridView)"/>
        public void CreateCells (DataGridView dataGridView, params object?[] values)
        {
            CreateCells (dataGridView);
            SetValues (values);
        }

        /// <summary>Sets the values of this row's cells, in order.</summary>
        public bool SetValues (params object?[] values)
        {
            ArgumentNullException.ThrowIfNull (values);

            for (var i = 0; i < Math.Min (values.Length, Cells.Count); i++)
                Cells[i].Value = values[i];

            // WinForms reports false when there were more values than cells, because the extra ones
            // were dropped -- a caller building rows from a wider record needs to know.
            return values.Length <= Cells.Count;
        }

        /// <summary>Returns this row's state.</summary>
        public virtual DataGridViewElementStates GetState (int rowIndex)
            => DataGridView?.Rows.GetRowState (rowIndex) ?? DataGridViewElementStates.None;

        /// <summary>Returns the context menu this row uses, falling back to the grid's.</summary>
        public ContextMenuStrip? GetContextMenuStrip (int rowIndex) => ContextMenuStrip ?? DataGridView?.ContextMenuStrip;

        /// <summary>Returns this row's error text.</summary>
        public string GetErrorText (int rowIndex) => ErrorText;

        /// <summary>Returns the height this row would like, given its cells.</summary>
        public virtual int GetPreferredHeight (int rowIndex, DataGridViewAutoSizeRowMode autoSizeRowMode, bool fixedWidth)
        {
            var tallest = 0;

            foreach (var cell in Cells)
                tallest = Math.Max (tallest, cell.PreferredSize.Height);

            return Math.Max (tallest, Height);
        }

        /// <summary>Returns the border style to use for this row's header.</summary>
        public virtual DataGridViewAdvancedBorderStyle AdjustRowHeaderBorderStyle (
            DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStyleInput,
            DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStylePlaceholder,
            bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
            bool isFirstDisplayedRow, bool isLastVisibleRow)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewAdvancedBorderStyleInput);
            ArgumentNullException.ThrowIfNull (dataGridViewAdvancedBorderStylePlaceholder);

            var result = dataGridViewAdvancedBorderStylePlaceholder;

            result.Top = dataGridViewAdvancedBorderStyleInput.Top;
            result.Bottom = dataGridViewAdvancedBorderStyleInput.Bottom;
            result.Left = dataGridViewAdvancedBorderStyleInput.Left;
            result.Right = dataGridViewAdvancedBorderStyleInput.Right;

            // Only the first row draws its top edge; every other row's top is the row above's bottom.
            if (!isFirstDisplayedRow && !singleHorizontalBorderAdded)
                result.Top = DataGridViewAdvancedCellBorderStyle.None;

            return result;
        }
    }

    /// <summary>Exposes a <see cref="DataGridViewRow"/> to accessibility clients.</summary>
    public class DataGridViewRowAccessibleObject : AccessibleObject
    {
        private readonly DataGridViewRow row;

        /// <summary>Initializes a new instance of the <see cref="DataGridViewRowAccessibleObject"/> class.</summary>
        public DataGridViewRowAccessibleObject (DataGridViewRow owner) => row = owner;

        /// <summary>Gets the name reported to assistive technology.</summary>
        public override string? Name => $"Row {row.Index + 1}";

        /// <summary>Gets the role reported to assistive technology.</summary>
        public override AccessibleRole Role => AccessibleRole.Row;
    }

    public partial class DataGridViewColumn
    {
        /// <summary>Gets the type of cell this column creates.</summary>
        public Type CellType => CellTemplate?.GetType () ?? typeof (DataGridViewTextBoxCell);

        /// <summary>Gets or sets the context menu shown when this column's header is right-clicked.</summary>
        public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

        /// <summary>Gets or sets the site of this column.</summary>
        public ISite? Site { get; set; }

        /// <summary>Gets the auto-size mode actually in effect, resolving NotSet from the grid.</summary>
        public DataGridViewAutoSizeColumnMode InheritedAutoSizeMode
            => AutoSizeMode != DataGridViewAutoSizeColumnMode.NotSet
                ? AutoSizeMode
                : DataGridView?.AutoSizeColumnsMode switch {
                    DataGridViewAutoSizeColumnsMode.AllCells => DataGridViewAutoSizeColumnMode.AllCells,
                    DataGridViewAutoSizeColumnsMode.ColumnHeader => DataGridViewAutoSizeColumnMode.ColumnHeader,
                    DataGridViewAutoSizeColumnsMode.DisplayedCells => DataGridViewAutoSizeColumnMode.DisplayedCells,
                    DataGridViewAutoSizeColumnsMode.Fill => DataGridViewAutoSizeColumnMode.Fill,
                    _ => DataGridViewAutoSizeColumnMode.None,
                };

        /// <summary>Returns the width this column would like, given its content.</summary>
        public virtual int GetPreferredWidth (DataGridViewAutoSizeColumnMode autoSizeColumnMode, bool fixedHeight)
        {
            if (DataGridView is null)
                return Width;

            var widest = autoSizeColumnMode == DataGridViewAutoSizeColumnMode.ColumnHeader
                ? 0
                : DataGridView.Rows
                    .Where (row => Index >= 0 && Index < row.Cells.Count)
                    .Select (row => row.Cells[Index].PreferredSize.Width)
                    .DefaultIfEmpty (0)
                    .Max ();

            if (autoSizeColumnMode is DataGridViewAutoSizeColumnMode.ColumnHeader
                or DataGridViewAutoSizeColumnMode.AllCells
                or DataGridViewAutoSizeColumnMode.AllCellsExceptHeader)
                widest = Math.Max (widest, (int)Math.Ceiling (
                    TextMeasurer.MeasureText (HeaderText ?? string.Empty, DataGridView).Width) + 12);

            return Math.Max (widest, MinimumWidth);
        }

        /// <summary>Raised when the column is disposed.</summary>
#pragma warning disable CS0067
        public event EventHandler? Disposed;
#pragma warning restore CS0067
    }

    public partial class DataGridViewColumnCollection
    {
        /// <summary>Raised when columns are added to or removed from the collection.</summary>
#pragma warning disable CS0067
        public event CollectionChangeEventHandler? CollectionChanged;
#pragma warning restore CS0067

        /// <summary>Returns how many columns match the filter.</summary>
        public int GetColumnCount (DataGridViewElementStates includeFilter)
            => this.Count (column => Matches (column, includeFilter, DataGridViewElementStates.None));

        /// <summary>Returns the combined width of the columns matching the filter.</summary>
        public int GetColumnsWidth (DataGridViewElementStates includeFilter)
            => this.Where (column => Matches (column, includeFilter, DataGridViewElementStates.None)).Sum (c => c.Width);

        /// <summary>Returns the last column matching the filters, or null.</summary>
        public DataGridViewColumn? GetLastColumn (DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
            => this.LastOrDefault (column => Matches (column, includeFilter, excludeFilter));

        /// <summary>Returns the first column after the given one matching the filters, or null.</summary>
        public DataGridViewColumn? GetNextColumn (DataGridViewColumn dataGridViewColumnStart,
            DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
        {
            var start = dataGridViewColumnStart is null ? -1 : IndexOf (dataGridViewColumnStart);

            for (var i = start + 1; i < Count; i++)
                if (Matches (this[i], includeFilter, excludeFilter))
                    return this[i];

            return null;
        }

        /// <summary>Returns the last column before the given one matching the filters, or null.</summary>
        public DataGridViewColumn? GetPreviousColumn (DataGridViewColumn dataGridViewColumnStart,
            DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
        {
            var start = dataGridViewColumnStart is null ? Count : IndexOf (dataGridViewColumnStart);

            for (var i = Math.Min (start, Count) - 1; i >= 0; i--)
                if (Matches (this[i], includeFilter, excludeFilter))
                    return this[i];

            return null;
        }

        // The same asymmetry the row collection uses: an include filter of None matches every column,
        // an exclude filter of None excludes nothing.
        private static bool Matches (DataGridViewColumn column, DataGridViewElementStates includeFilter,
            DataGridViewElementStates excludeFilter)
        {
            var state = StateOf (column);

            return (includeFilter == DataGridViewElementStates.None || (state & includeFilter) == includeFilter)
                && (excludeFilter == DataGridViewElementStates.None || (state & excludeFilter) == 0);
        }

        private static DataGridViewElementStates StateOf (DataGridViewColumn column)
        {
            var state = DataGridViewElementStates.None;

            if (column.Visible)
                state |= DataGridViewElementStates.Visible;
            if (column.Frozen)
                state |= DataGridViewElementStates.Frozen;
            if (column.ReadOnly)
                state |= DataGridViewElementStates.ReadOnly;
            if (column.Resizable != DataGridViewTriState.False)
                state |= DataGridViewElementStates.Resizable;

            return state;
        }
    }

    public partial class DataGridViewCheckBoxCell : IDataGridViewEditingCell
    {
        /// <summary>Gets or sets the value that means "checked".</summary>
        public object? TrueValue { get; set; }

        /// <summary>Gets or sets the value that means "unchecked".</summary>
        public object? FalseValue { get; set; }

        /// <summary>Gets or sets the value that means "indeterminate".</summary>
        public object? IndeterminateValue { get; set; }

        /// <summary>Gets or sets the flat-style appearance of the check box.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets or sets the value being edited.</summary>
        public virtual object? EditingCellFormattedValue {
            get => Value;
            set => Value = value;
        }

        /// <summary>Gets or sets whether the user has changed the value.</summary>
        public virtual bool EditingCellValueChanged { get; set; }

        /// <summary>Returns the value being edited.</summary>
        public virtual object? GetEditingCellFormattedValue (DataGridViewDataErrorContexts context) => EditingCellFormattedValue;

        /// <summary>Prepares the cell for editing.</summary>
        /// <remarks>A check box has no text to select, so the flag has nothing to act on -- which is
        /// why this is the one editing cell where <c>selectAll</c> is genuinely inert.</remarks>
        public virtual void PrepareEditingCellForEdit (bool selectAll) { }
    }

    public partial class DataGridViewComboBoxCell
    {
        /// <summary>Gets or sets whether the cell completes typed text from its items.</summary>
        public virtual bool AutoComplete { get; set; } = true;

        /// <summary>Gets or sets whether only the current cell is drawn as a combo box.</summary>
        public bool DisplayStyleForCurrentCellOnly { get; set; }

        /// <summary>Gets or sets the width of the drop-down list.</summary>
        public virtual int DropDownWidth { get; set; } = 1;

        /// <summary>Gets or sets the flat-style appearance of the combo box.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets or sets how many items the drop-down shows before scrolling.</summary>
        public virtual int MaxDropDownItems { get; set; } = 8;

        /// <summary>Gets or sets whether the items are sorted.</summary>
        public virtual bool Sorted { get; set; }

        /// <summary>The items shown by a <see cref="DataGridViewComboBoxCell"/>.</summary>
        public class ObjectCollection : System.Collections.ObjectModel.Collection<object>
        {
            /// <summary>Adds several items at once.</summary>
            public void AddRange (params object[] items)
            {
                ArgumentNullException.ThrowIfNull (items);

                foreach (var item in items)
                    Add (item);
            }
        }
    }

    public partial class DataGridViewCheckBoxColumn
    {
        /// <summary>Gets or sets the value that means "checked".</summary>
        public object? TrueValue { get; set; }

        /// <summary>Gets or sets the value that means "unchecked".</summary>
        public object? FalseValue { get; set; }

        /// <summary>Gets or sets the value that means "indeterminate".</summary>
        public object? IndeterminateValue { get; set; }

        /// <summary>Gets or sets whether the check box has an indeterminate state.</summary>
        public bool ThreeState { get; set; }

        /// <summary>Gets or sets the flat-style appearance of the check boxes.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
    }

    public partial class DataGridViewComboBoxColumn
    {
        /// <summary>Gets or sets whether the cells complete typed text from their items.</summary>
        public bool AutoComplete { get; set; } = true;

        /// <summary>Gets or sets when the cells are drawn as combo boxes.</summary>
        public DataGridViewComboBoxDisplayStyle DisplayStyle { get; set; } = DataGridViewComboBoxDisplayStyle.DropDownButton;

        /// <summary>Gets or sets whether only the current cell is drawn as a combo box.</summary>
        public bool DisplayStyleForCurrentCellOnly { get; set; }

        /// <summary>Gets or sets how many items the drop-down shows before scrolling.</summary>
        public int MaxDropDownItems { get; set; } = 8;

        /// <summary>Gets or sets whether the items are sorted.</summary>
        public bool Sorted { get; set; }
    }
}
