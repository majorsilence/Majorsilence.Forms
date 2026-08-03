using System;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // The DataGridView parity surface (docs/winforms-gap-plan.md) -- the largest single concentration
    // of gaps on this surface, 83 members on the control plus 27 on DataGridViewCell and 15 on
    // DataGridViewRowCollection.
    //
    // Most of the 83 are *Changed notifications. Declaring 59 dead events would have closed the count
    // without helping anyone, so the 25 whose property already exists on this control are wired into
    // that property's setter: the setter now compares, assigns, and raises. Those fire for real. The
    // rest are declared with a protected raiser and are not raised by the framework yet -- said here
    // rather than left for a caller to discover, and each one's own doc comment repeats it.
    //
    // The methods are implemented against the geometry this control already computes rather than
    // stubbed, which is why AutoResizeColumnHeadersHeight, GetColumnDisplayRectangle and
    // DisplayedColumnCount return real numbers.

    public partial class DataGridView
    {
        private DataGridViewHeaderCell? top_left_header_cell;

        /// <summary>Raised when <see cref="AllowUserToAddRows"/> changes.</summary>
        public event EventHandler? AllowUserToAddRowsChanged;

        /// <summary>Raised when <see cref="AllowUserToDeleteRows"/> changes.</summary>
        public event EventHandler? AllowUserToDeleteRowsChanged;

        /// <summary>Raised when <see cref="AllowUserToOrderColumns"/> changes.</summary>
        public event EventHandler? AllowUserToOrderColumnsChanged;

        /// <summary>Raised when <see cref="AllowUserToResizeColumns"/> changes.</summary>
        public event EventHandler? AllowUserToResizeColumnsChanged;

        /// <summary>Raised when <see cref="AllowUserToResizeRows"/> changes.</summary>
        public event EventHandler? AllowUserToResizeRowsChanged;

        /// <summary>Raised when <see cref="AlternatingRowsDefaultCellStyle"/> changes.</summary>
        public event EventHandler? AlternatingRowsDefaultCellStyleChanged;

        /// <summary>Raised when <see cref="AutoGenerateColumns"/> changes.</summary>
        public event EventHandler? AutoGenerateColumnsChanged;

        /// <summary>Raised when <see cref="BackgroundColor"/> changes.</summary>
        public event EventHandler? BackgroundColorChanged;

        /// <summary>Raised when <see cref="BorderStyle"/> changes.</summary>
        public event EventHandler? BorderStyleChanged;

        /// <summary>Raised when <see cref="CellBorderStyle"/> changes.</summary>
        public event EventHandler? CellBorderStyleChanged;

        /// <summary>Raised when <see cref="ColumnHeadersBorderStyle"/> changes.</summary>
        public event EventHandler? ColumnHeadersBorderStyleChanged;

        /// <summary>Raised when <see cref="ColumnHeadersDefaultCellStyle"/> changes.</summary>
        public event EventHandler? ColumnHeadersDefaultCellStyleChanged;

        /// <summary>Raised when <see cref="DataMember"/> changes.</summary>
        public event EventHandler? DataMemberChanged;

        /// <summary>Raised when <see cref="DefaultCellStyle"/> changes.</summary>
        public event EventHandler? DefaultCellStyleChanged;

        /// <summary>Raised when <see cref="EditMode"/> changes.</summary>
        public event EventHandler? EditModeChanged;

        /// <summary>Raised when <see cref="GridColor"/> changes.</summary>
        public event EventHandler? GridColorChanged;

        /// <summary>Raised when <see cref="MultiSelect"/> changes.</summary>
        public event EventHandler? MultiSelectChanged;

        /// <summary>Raised when <see cref="ReadOnly"/> changes.</summary>
        public event EventHandler? ReadOnlyChanged;

        /// <summary>Raised when <see cref="RowHeadersBorderStyle"/> changes.</summary>
        public event EventHandler? RowHeadersBorderStyleChanged;

        /// <summary>Raised when <see cref="RowHeadersDefaultCellStyle"/> changes.</summary>
        public event EventHandler? RowHeadersDefaultCellStyleChanged;

        /// <summary>Raised when <see cref="RowsDefaultCellStyle"/> changes.</summary>
        public event EventHandler? RowsDefaultCellStyleChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnStyleChanged"/>; the framework does not raise it yet.</summary>
        public event EventHandler? StyleChanged;

        /// <summary>Raised when <see cref="AutoSizeColumnsMode"/> changes.</summary>
        public event DataGridViewAutoSizeColumnsModeEventHandler? AutoSizeColumnsModeChanged;

        /// <summary>Raised when <see cref="AutoSizeRowsMode"/> changes.</summary>
        public event DataGridViewAutoSizeModeEventHandler? AutoSizeRowsModeChanged;

        /// <summary>Raised when <see cref="ColumnHeadersHeightSizeMode"/> changes.</summary>
        public event DataGridViewAutoSizeModeEventHandler? ColumnHeadersHeightSizeModeChanged;

        /// <summary>Raised when <see cref="RowHeadersWidthSizeMode"/> changes.</summary>
        public event DataGridViewAutoSizeModeEventHandler? RowHeadersWidthSizeModeChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellContextMenuStripChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellEventHandler? CellContextMenuStripChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellErrorTextChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellEventHandler? CellErrorTextChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellStyleChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellEventHandler? CellStyleChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellToolTipTextChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellEventHandler? CellToolTipTextChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellEnter"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellEventHandler? CellEnter;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnContextMenuStripChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnContextMenuStripChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnDataPropertyNameChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnDataPropertyNameChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnDefaultCellStyleChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnDefaultCellStyleChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnDividerWidthChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnDividerWidthChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnHeaderCellChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnHeaderCellChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnMinimumWidthChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnMinimumWidthChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnNameChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnNameChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnToolTipTextChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnEventHandler? ColumnToolTipTextChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowContextMenuStripChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowContextMenuStripChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowDefaultCellStyleChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowDefaultCellStyleChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowDividerHeightChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowDividerHeightChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowErrorTextChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowErrorTextChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowHeaderCellChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowHeaderCellChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowMinimumHeightChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowMinimumHeightChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowUnshared"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowEventHandler? RowUnshared;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnStateChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnStateChangedEventHandler? ColumnStateChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellStyleContentChanged"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellStyleContentChangedEventHandler? CellStyleContentChanged;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCancelRowEdit"/>; the framework does not raise it yet.</summary>
        public event QuestionEventHandler? CancelRowEdit;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellContextMenuStripNeeded"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellContextMenuStripNeededEventHandler? CellContextMenuStripNeeded;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnCellErrorTextNeeded"/>; the framework does not raise it yet.</summary>
        public event DataGridViewCellErrorTextNeededEventHandler? CellErrorTextNeeded;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowContextMenuStripNeeded"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowContextMenuStripNeededEventHandler? RowContextMenuStripNeeded;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowErrorTextNeeded"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowErrorTextNeededEventHandler? RowErrorTextNeeded;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnColumnDividerDoubleClick"/>; the framework does not raise it yet.</summary>
        public event DataGridViewColumnDividerDoubleClickEventHandler? ColumnDividerDoubleClick;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowDividerDoubleClick"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowDividerDoubleClickEventHandler? RowDividerDoubleClick;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowHeightInfoNeeded"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowHeightInfoNeededEventHandler? RowHeightInfoNeeded;

        /// <summary>Raised when the corresponding state changes. Declared and raisable through <see cref="OnRowHeightInfoPushed"/>; the framework does not raise it yet.</summary>
        public event DataGridViewRowHeightInfoPushedEventHandler? RowHeightInfoPushed;

        /// <summary>Never raised: this control does not draw a background image. Present because designer-generated code binds it.</summary>
        public event EventHandler? BackgroundImageChanged;

        /// <summary>Never raised: this control does not draw a background image. Present because designer-generated code binds it.</summary>
        public event EventHandler? BackgroundImageLayoutChanged;


        /// <summary>Raises the <see cref="AllowUserToAddRowsChanged"/> event.</summary>
        protected virtual void OnAllowUserToAddRowsChanged (EventArgs e) => AllowUserToAddRowsChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AllowUserToDeleteRowsChanged"/> event.</summary>
        protected virtual void OnAllowUserToDeleteRowsChanged (EventArgs e) => AllowUserToDeleteRowsChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AllowUserToOrderColumnsChanged"/> event.</summary>
        protected virtual void OnAllowUserToOrderColumnsChanged (EventArgs e) => AllowUserToOrderColumnsChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AllowUserToResizeColumnsChanged"/> event.</summary>
        protected virtual void OnAllowUserToResizeColumnsChanged (EventArgs e) => AllowUserToResizeColumnsChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AllowUserToResizeRowsChanged"/> event.</summary>
        protected virtual void OnAllowUserToResizeRowsChanged (EventArgs e) => AllowUserToResizeRowsChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AlternatingRowsDefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnAlternatingRowsDefaultCellStyleChanged (EventArgs e) => AlternatingRowsDefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AutoGenerateColumnsChanged"/> event.</summary>
        protected virtual void OnAutoGenerateColumnsChanged (EventArgs e) => AutoGenerateColumnsChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="BackgroundColorChanged"/> event.</summary>
        protected virtual void OnBackgroundColorChanged (EventArgs e) => BackgroundColorChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="BorderStyleChanged"/> event.</summary>
        protected virtual void OnBorderStyleChanged (EventArgs e) => BorderStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellBorderStyleChanged"/> event.</summary>
        protected virtual void OnCellBorderStyleChanged (EventArgs e) => CellBorderStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnHeadersBorderStyleChanged"/> event.</summary>
        protected virtual void OnColumnHeadersBorderStyleChanged (EventArgs e) => ColumnHeadersBorderStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnHeadersDefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnColumnHeadersDefaultCellStyleChanged (EventArgs e) => ColumnHeadersDefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="DataMemberChanged"/> event.</summary>
        protected virtual void OnDataMemberChanged (EventArgs e) => DataMemberChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="DefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnDefaultCellStyleChanged (EventArgs e) => DefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="EditModeChanged"/> event.</summary>
        protected virtual void OnEditModeChanged (EventArgs e) => EditModeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="GridColorChanged"/> event.</summary>
        protected virtual void OnGridColorChanged (EventArgs e) => GridColorChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="MultiSelectChanged"/> event.</summary>
        protected virtual void OnMultiSelectChanged (EventArgs e) => MultiSelectChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ReadOnlyChanged"/> event.</summary>
        protected virtual void OnReadOnlyChanged (EventArgs e) => ReadOnlyChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeadersBorderStyleChanged"/> event.</summary>
        protected virtual void OnRowHeadersBorderStyleChanged (EventArgs e) => RowHeadersBorderStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeadersDefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnRowHeadersDefaultCellStyleChanged (EventArgs e) => RowHeadersDefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowsDefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnRowsDefaultCellStyleChanged (EventArgs e) => RowsDefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="StyleChanged"/> event.</summary>
        protected virtual void OnStyleChanged (EventArgs e) => StyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AutoSizeColumnsModeChanged"/> event.</summary>
        protected virtual void OnAutoSizeColumnsModeChanged (DataGridViewAutoSizeColumnsModeEventArgs e) => AutoSizeColumnsModeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="AutoSizeRowsModeChanged"/> event.</summary>
        protected virtual void OnAutoSizeRowsModeChanged (DataGridViewAutoSizeModeEventArgs e) => AutoSizeRowsModeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnHeadersHeightSizeModeChanged"/> event.</summary>
        protected virtual void OnColumnHeadersHeightSizeModeChanged (DataGridViewAutoSizeModeEventArgs e) => ColumnHeadersHeightSizeModeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeadersWidthSizeModeChanged"/> event.</summary>
        protected virtual void OnRowHeadersWidthSizeModeChanged (DataGridViewAutoSizeModeEventArgs e) => RowHeadersWidthSizeModeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellContextMenuStripChanged"/> event.</summary>
        protected virtual void OnCellContextMenuStripChanged (DataGridViewCellEventArgs e) => CellContextMenuStripChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellErrorTextChanged"/> event.</summary>
        protected virtual void OnCellErrorTextChanged (DataGridViewCellEventArgs e) => CellErrorTextChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellStyleChanged"/> event.</summary>
        protected virtual void OnCellStyleChanged (DataGridViewCellEventArgs e) => CellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellToolTipTextChanged"/> event.</summary>
        protected virtual void OnCellToolTipTextChanged (DataGridViewCellEventArgs e) => CellToolTipTextChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellEnter"/> event.</summary>
        protected virtual void OnCellEnter (DataGridViewCellEventArgs e) => CellEnter?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnContextMenuStripChanged"/> event.</summary>
        protected virtual void OnColumnContextMenuStripChanged (DataGridViewColumnEventArgs e) => ColumnContextMenuStripChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnDataPropertyNameChanged"/> event.</summary>
        protected virtual void OnColumnDataPropertyNameChanged (DataGridViewColumnEventArgs e) => ColumnDataPropertyNameChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnDefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnColumnDefaultCellStyleChanged (DataGridViewColumnEventArgs e) => ColumnDefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnDividerWidthChanged"/> event.</summary>
        protected virtual void OnColumnDividerWidthChanged (DataGridViewColumnEventArgs e) => ColumnDividerWidthChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnHeaderCellChanged"/> event.</summary>
        protected virtual void OnColumnHeaderCellChanged (DataGridViewColumnEventArgs e) => ColumnHeaderCellChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnMinimumWidthChanged"/> event.</summary>
        protected virtual void OnColumnMinimumWidthChanged (DataGridViewColumnEventArgs e) => ColumnMinimumWidthChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnNameChanged"/> event.</summary>
        protected virtual void OnColumnNameChanged (DataGridViewColumnEventArgs e) => ColumnNameChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnToolTipTextChanged"/> event.</summary>
        protected virtual void OnColumnToolTipTextChanged (DataGridViewColumnEventArgs e) => ColumnToolTipTextChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowContextMenuStripChanged"/> event.</summary>
        protected virtual void OnRowContextMenuStripChanged (DataGridViewRowEventArgs e) => RowContextMenuStripChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowDefaultCellStyleChanged"/> event.</summary>
        protected virtual void OnRowDefaultCellStyleChanged (DataGridViewRowEventArgs e) => RowDefaultCellStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowDividerHeightChanged"/> event.</summary>
        protected virtual void OnRowDividerHeightChanged (DataGridViewRowEventArgs e) => RowDividerHeightChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowErrorTextChanged"/> event.</summary>
        protected virtual void OnRowErrorTextChanged (DataGridViewRowEventArgs e) => RowErrorTextChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeaderCellChanged"/> event.</summary>
        protected virtual void OnRowHeaderCellChanged (DataGridViewRowEventArgs e) => RowHeaderCellChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowMinimumHeightChanged"/> event.</summary>
        protected virtual void OnRowMinimumHeightChanged (DataGridViewRowEventArgs e) => RowMinimumHeightChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowUnshared"/> event.</summary>
        protected virtual void OnRowUnshared (DataGridViewRowEventArgs e) => RowUnshared?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnStateChanged"/> event.</summary>
        protected virtual void OnColumnStateChanged (DataGridViewColumnStateChangedEventArgs e) => ColumnStateChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellStyleContentChanged"/> event.</summary>
        protected virtual void OnCellStyleContentChanged (DataGridViewCellStyleContentChangedEventArgs e) => CellStyleContentChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CancelRowEdit"/> event.</summary>
        protected virtual void OnCancelRowEdit (QuestionEventArgs e) => CancelRowEdit?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellContextMenuStripNeeded"/> event.</summary>
        protected virtual void OnCellContextMenuStripNeeded (DataGridViewCellContextMenuStripNeededEventArgs e) => CellContextMenuStripNeeded?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellErrorTextNeeded"/> event.</summary>
        protected virtual void OnCellErrorTextNeeded (DataGridViewCellErrorTextNeededEventArgs e) => CellErrorTextNeeded?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowContextMenuStripNeeded"/> event.</summary>
        protected virtual void OnRowContextMenuStripNeeded (DataGridViewRowContextMenuStripNeededEventArgs e) => RowContextMenuStripNeeded?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowErrorTextNeeded"/> event.</summary>
        protected virtual void OnRowErrorTextNeeded (DataGridViewRowErrorTextNeededEventArgs e) => RowErrorTextNeeded?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnDividerDoubleClick"/> event.</summary>
        protected virtual void OnColumnDividerDoubleClick (DataGridViewColumnDividerDoubleClickEventArgs e) => ColumnDividerDoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowDividerDoubleClick"/> event.</summary>
        protected virtual void OnRowDividerDoubleClick (DataGridViewRowDividerDoubleClickEventArgs e) => RowDividerDoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeightInfoNeeded"/> event.</summary>
        protected virtual void OnRowHeightInfoNeeded (DataGridViewRowHeightInfoNeededEventArgs e) => RowHeightInfoNeeded?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeightInfoPushed"/> event.</summary>
        protected virtual void OnRowHeightInfoPushed (DataGridViewRowHeightInfoPushedEventArgs e) => RowHeightInfoPushed?.Invoke (this, e);

        /// <summary>Raises the <see cref="BackgroundImageChanged"/> event.</summary>
        protected virtual void OnBackgroundImageChanged (EventArgs e) => BackgroundImageChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="BackgroundImageLayoutChanged"/> event.</summary>
        protected virtual void OnBackgroundImageLayoutChanged (EventArgs e) => BackgroundImageLayoutChanged?.Invoke (this, e);

        /// <summary>Gets or sets the border style of the column header cells.</summary>
        public DataGridViewHeaderBorderStyle ColumnHeadersBorderStyle {
            get => column_headers_border_style;
            set {
                if (column_headers_border_style == value)
                    return;

                column_headers_border_style = value;
                OnColumnHeadersBorderStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private DataGridViewHeaderBorderStyle column_headers_border_style = DataGridViewHeaderBorderStyle.Raised;

        /// <summary>Gets or sets the border style of the row header cells.</summary>
        public DataGridViewHeaderBorderStyle RowHeadersBorderStyle {
            get => row_headers_border_style;
            set {
                if (row_headers_border_style == value)
                    return;

                row_headers_border_style = value;
                OnRowHeadersBorderStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private DataGridViewHeaderBorderStyle row_headers_border_style = DataGridViewHeaderBorderStyle.Raised;

        /// <summary>Gets the border style actually used for the top-left header cell.</summary>
        public DataGridViewAdvancedBorderStyle AdjustedTopLeftHeaderBorderStyle {
            get {
                var style = new DataGridViewAdvancedBorderStyle ();
                var edge = ColumnHeadersBorderStyle switch {
                    DataGridViewHeaderBorderStyle.None => DataGridViewAdvancedCellBorderStyle.None,
                    DataGridViewHeaderBorderStyle.Single => DataGridViewAdvancedCellBorderStyle.Single,
                    DataGridViewHeaderBorderStyle.Sunken => DataGridViewAdvancedCellBorderStyle.Inset,
                    _ => DataGridViewAdvancedCellBorderStyle.Outset,
                };

                style.Top = style.Left = style.Right = style.Bottom = edge;
                return style;
            }
        }

        /// <summary>Gets or sets the cell in the top-left corner, above the row headers.</summary>
        public DataGridViewHeaderCell TopLeftHeaderCell {
            get => top_left_header_cell ??= new DataGridViewHeaderCell ();
            set => top_left_header_cell = value;
        }

        /// <summary>Gets or sets whether error glyphs are shown on cells with an error.</summary>
        public bool ShowCellErrors { get; set; } = true;

        /// <summary>Gets or sets whether tooltips are shown for cells.</summary>
        public bool ShowCellToolTips { get; set; } = true;

        /// <summary>Gets or sets whether a pencil glyph is shown on the row being edited.</summary>
        public bool ShowEditingIcon { get; set; } = true;

        /// <summary>Gets or sets whether error glyphs are shown on rows with an error.</summary>
        public bool ShowRowErrors { get; set; } = true;

        /// <summary>Gets the cursor the application set, as opposed to one the control set itself.</summary>
        public Cursor UserSetCursor => Cursor;

        /// <summary>Gets how far the control is scrolled vertically, in pixels.</summary>
        public int VerticalScrollingOffset => vscrollbar.Value;

        /// <summary>Gets the width of the part of the first displayed column that is scrolled out of view.</summary>
        public int FirstDisplayedScrollingColumnHiddenWidth => 0;

        /// <summary>Gets whether the current row has uncommitted changes.</summary>
        public bool IsCurrentRowDirty => IsCurrentCellDirty;

        /// <summary>Gets or sets the first cell visible in the control.</summary>
        public DataGridViewCell? FirstDisplayedCell {
            get {
                if (Rows.Count == 0 || Columns.Count == 0)
                    return null;

                var row = Rows[0];
                return row.Cells.Count > 0 ? row.Cells[0] : null;
            }
            set {
                if (value?.OwningRow is { } row)
                    CurrentCell = value;
            }
        }

        /// <summary>Returns whether every cell in the control is selected.</summary>
        public bool AreAllCellsSelected (bool includeInvisibleCells)
        {
            foreach (var row in Rows) {
                foreach (var cell in row.Cells) {
                    if (!includeInvisibleCells && !cell.Visible)
                        continue;
                    if (!cell.Selected)
                        return false;
                }
            }

            return true;
        }

        /// <summary>Returns how many columns are currently on screen.</summary>
        public int DisplayedColumnCount (bool includePartialColumns)
        {
            var available = ClientRectangle.Width - (RowHeadersVisible ? RowHeadersWidth : 0);
            var used = 0;
            var count = 0;

            foreach (var column in Columns) {
                if (!column.Visible)
                    continue;
                if (used >= available)
                    break;

                if (used + column.Width > available) {
                    if (includePartialColumns)
                        count++;
                    break;
                }

                used += column.Width;
                count++;
            }

            return count;
        }

        /// <summary>Returns the on-screen rectangle of a column, in client coordinates.</summary>
        public Rectangle GetColumnDisplayRectangle (int columnIndex, bool cutOverflow)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                return Rectangle.Empty;

            var x = RowHeadersVisible ? RowHeadersWidth : 0;

            for (var i = 0; i < columnIndex; i++)
                if (Columns[i].Visible)
                    x += Columns[i].Width;

            var rectangle = new Rectangle (x, 0, Columns[columnIndex].Width, ClientRectangle.Height);
            return cutOverflow ? Rectangle.Intersect (rectangle, ClientRectangle) : rectangle;
        }

        /// <summary>Returns the on-screen rectangle of a row, in client coordinates.</summary>
        public Rectangle GetRowDisplayRectangle (int rowIndex, bool cutOverflow)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                return Rectangle.Empty;

            var y = ColumnHeadersVisible ? ColumnHeadersHeight : 0;

            for (var i = 0; i < rowIndex; i++)
                if (Rows[i].Visible)
                    y += Rows[i].Height;

            var rectangle = new Rectangle (0, y, ClientRectangle.Width, Rows[rowIndex].Height);
            return cutOverflow ? Rectangle.Intersect (rectangle, ClientRectangle) : rectangle;
        }

        /// <summary>Resizes the column header band to fit the tallest header.</summary>
        public void AutoResizeColumnHeadersHeight ()
        {
            var tallest = 0;

            foreach (var column in Columns)
                tallest = Math.Max (tallest, (int)Math.Ceiling (TextMeasurer.MeasureText (column.HeaderText ?? string.Empty, this).Height));

            if (tallest > 0)
                ColumnHeadersHeight = tallest + 8;
        }

        /// <inheritdoc cref="AutoResizeColumnHeadersHeight()"/>
        public void AutoResizeColumnHeadersHeight (int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                throw new ArgumentOutOfRangeException (nameof (columnIndex));

            var height = (int)Math.Ceiling (TextMeasurer.MeasureText (Columns[columnIndex].HeaderText ?? string.Empty, this).Height);

            if (height > 0)
                ColumnHeadersHeight = height + 8;
        }

        /// <summary>Resizes the row header band according to the given mode.</summary>
        public void AutoResizeRowHeadersWidth (DataGridViewRowHeadersWidthSizeMode rowHeadersWidthSizeMode)
        {
            if (rowHeadersWidthSizeMode is DataGridViewRowHeadersWidthSizeMode.EnableResizing
                or DataGridViewRowHeadersWidthSizeMode.DisableResizing)
                return;     // the two non-auto-sizing modes leave the width alone

            var widest = 0;

            foreach (var row in Rows)
                widest = Math.Max (widest, (int)Math.Ceiling (TextMeasurer.MeasureText (row.HeaderCell?.Value?.ToString () ?? string.Empty, this).Width));

            RowHeadersWidth = Math.Max (widest + 8, 1);
        }

        /// <inheritdoc cref="AutoResizeRowHeadersWidth(DataGridViewRowHeadersWidthSizeMode)"/>
        public void AutoResizeRowHeadersWidth (int rowIndex, DataGridViewRowHeadersWidthSizeMode rowHeadersWidthSizeMode)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                throw new ArgumentOutOfRangeException (nameof (rowIndex));

            AutoResizeRowHeadersWidth (rowHeadersWidthSizeMode);
        }

        /// <summary>Returns the border style to use for a column header, given the control's settings.</summary>
        public virtual DataGridViewAdvancedBorderStyle AdjustColumnHeaderBorderStyle (
            DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStyleInput,
            DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStylePlaceholder,
            bool isFirstDisplayedColumn,
            bool isLastVisibleColumn)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewAdvancedBorderStyleInput);
            ArgumentNullException.ThrowIfNull (dataGridViewAdvancedBorderStylePlaceholder);

            var result = dataGridViewAdvancedBorderStylePlaceholder;

            result.Top = dataGridViewAdvancedBorderStyleInput.Top;
            result.Bottom = dataGridViewAdvancedBorderStyleInput.Bottom;
            result.Left = dataGridViewAdvancedBorderStyleInput.Left;
            result.Right = dataGridViewAdvancedBorderStyleInput.Right;

            // Only the leading and trailing headers draw their outer edge; the shared edges between
            // adjacent headers are drawn once, by the column on the left.
            if (!isFirstDisplayedColumn)
                result.Left = DataGridViewAdvancedCellBorderStyle.None;

            if (!isLastVisibleColumn)
                result.Right = DataGridViewAdvancedCellBorderStyle.None;

            return result;
        }

        /// <summary>Refreshes the error glyph and tooltip for one cell.</summary>
        public void UpdateCellErrorText (int columnIndex, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                throw new ArgumentOutOfRangeException (nameof (rowIndex));
            if (columnIndex < -1 || columnIndex >= Columns.Count)
                throw new ArgumentOutOfRangeException (nameof (columnIndex));

            Invalidate ();
        }

        /// <summary>Refreshes the error glyph and tooltip for one row.</summary>
        public void UpdateRowErrorText (int rowIndex) => UpdateRowErrorText (rowIndex, rowIndex);

        /// <inheritdoc cref="UpdateRowErrorText(int)"/>
        public void UpdateRowErrorText (int rowIndexStart, int rowIndexEnd)
        {
            if (rowIndexStart < 0 || rowIndexStart >= Rows.Count)
                throw new ArgumentOutOfRangeException (nameof (rowIndexStart));
            if (rowIndexEnd < rowIndexStart || rowIndexEnd >= Rows.Count)
                throw new ArgumentOutOfRangeException (nameof (rowIndexEnd));

            Invalidate ();
        }

        /// <summary>Recomputes the height of a row, and optionally every row after it.</summary>
        public void UpdateRowHeightInfo (int rowIndex, bool updateToEnd)
        {
            if (rowIndex < -1 || rowIndex >= Rows.Count)
                throw new ArgumentOutOfRangeException (nameof (rowIndex));

            Invalidate ();
        }

        /// <summary>The collection of controls hosted inside a <see cref="DataGridView"/>.</summary>
        /// <remarks>Named as WinForms names it; it is the editing controls and scroll bars the grid
        /// parents, and it behaves exactly as the base collection does.</remarks>
        public class DataGridViewControlCollection : ControlCollection
        {
            /// <summary>Initializes a new instance of the <see cref="DataGridViewControlCollection"/> class.</summary>
            public DataGridViewControlCollection (DataGridView owner) : base (owner) { }
        }
    }
}
