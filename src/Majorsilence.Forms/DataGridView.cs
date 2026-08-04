using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a DataGridView control for displaying tabular data.
    /// </summary>
    public partial class DataGridView : Control, System.ComponentModel.ISupportInitialize
    {
        private int header_height = 30;
        private int row_height = 25;
        private int row_headers_width = 40;
        private bool row_headers_visible;
        private int top_index;
        private int horizontal_scroll_offset;
        private int selected_row_index = -1;
        private int selected_column_index = -1;
        private int hovered_row_index = -1;
        private int resize_column_index = -1;
        private int resize_row_index = -1;
        private int resize_start_x;
        private int resize_start_y;
        private int resize_start_width;
        private int resize_start_height;
        private bool is_resizing_column;
        private bool is_resizing_row;
        private bool column_headers_visible = true;
        private DataGridViewSelectionMode selection_mode = DataGridViewSelectionMode.FullRowSelect;
        private bool read_only;
        private IList? data_source;
        private TextBox? edit_textbox;
        private int editing_row_index = -1;
        private int editing_column_index = -1;

        private readonly VerticalScrollBar vscrollbar;
        private readonly HorizontalScrollBar hscrollbar;

        /// <summary>
        /// Initializes a new instance of the DataGridView class.
        /// </summary>
        public DataGridView ()
        {
            Columns = new DataGridViewColumnCollection (this);
            Rows = new DataGridViewRowCollection (this);

            vscrollbar = new VerticalScrollBar {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = false,
                Dock = DockStyle.Right
            };

            vscrollbar.ValueChanged += (o, e) => {
                top_index = Math.Max (vscrollbar.Value, 0);
                UpdateEditTextBoxPosition ();
                Invalidate ();
            };

            hscrollbar = new HorizontalScrollBar {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 10,
                LargeChange = 50,
                Visible = false,
                Dock = DockStyle.Bottom
            };

            hscrollbar.ValueChanged += (o, e) => {
                horizontal_scroll_offset = Math.Max (hscrollbar.Value, 0);
                UpdateEditTextBoxPosition ();
                Invalidate ();
            };

            Controls.AddImplicitControl (vscrollbar);
            Controls.AddImplicitControl (hscrollbar);

            // Seed the per-edge border styles from the coarse CellBorderStyle default (Single), then
            // repaint whenever an edge is hand-edited (which, as in WinForms, makes the coarse property
            // report Custom).
            ApplyCellBorderStyle (cell_border_style);
            advanced_column_headers_border_style.All (DataGridViewAdvancedCellBorderStyle.Single);
            advanced_row_headers_border_style.All (DataGridViewAdvancedCellBorderStyle.Single);

            advanced_cell_border_style.Changed += (o, e) => {
                if (!applying_cell_border_style)
                    cell_border_style = DataGridViewCellBorderStyle.Custom;

                Invalidate ();
            };

            advanced_column_headers_border_style.Changed += (o, e) => Invalidate ();
            advanced_row_headers_border_style.Changed += (o, e) => Invalidate ();
        }

        /// <summary>
        /// Begins editing the currently selected cell (WinForms compat overload). Stub in Majorsilence.Forms.
        /// </summary>
        public bool BeginEdit (bool selectAll) { return true; }

        /// <summary>
        /// Begins editing the specified cell.
        /// </summary>
        public void BeginEdit (int rowIndex, int columnIndex)
        {
            if (read_only || rowIndex < 0 || rowIndex >= Rows.Count || columnIndex < 0 || columnIndex >= Columns.Count)
                return;

            // End any current edit
            EndEdit ();

            editing_row_index = rowIndex;
            editing_column_index = columnIndex;

            var cell_bounds = GetCellBounds (rowIndex, columnIndex);

            if (cell_bounds.IsEmpty)
                return;

            var cell_value = columnIndex < Rows[rowIndex].Cells.Count
                ? Rows[rowIndex].Cells[columnIndex].Value?.ToString () ?? string.Empty
                : string.Empty;

            // Raise CellBeginEdit event
            var begin_args = new DataGridViewCellCancelEventArgs (columnIndex, rowIndex);
            OnCellBeginEdit (begin_args);

            if (begin_args.Cancel)
                return;

            // GetCellBounds returns device pixel coordinates; child control bounds are
            // in logical units, so convert before positioning the TextBox.
            edit_textbox = new TextBox {
                Left = DeviceToLogicalUnits (cell_bounds.Left) + 1,
                Top = DeviceToLogicalUnits (cell_bounds.Top) + 1,
                Width = DeviceToLogicalUnits (cell_bounds.Width) - 2,
                Height = DeviceToLogicalUnits (cell_bounds.Height) - 2,
                Text = cell_value
            };

            edit_textbox.Style.Border.Width = 0;

            edit_textbox.KeyDown += EditTextBox_KeyDown;
            edit_textbox.LostFocus += EditTextBox_LostFocus;

            Controls.Add (edit_textbox);

            // Let handlers customise/inspect the editing control before it is shown (WinForms).
            OnEditingControlShowing (new DataGridViewEditingControlShowingEventArgs (edit_textbox, new DataGridViewCellStyle ()));

            edit_textbox.Select ();
            edit_textbox.SelectAll ();
        }

        /// <summary>
        /// Raised when a cell begins editing.
        /// </summary>
        /// <summary>Raised when a cell is clicked.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellClick;

        /// <summary>Raised when a cell loses input focus. Mirrors WinForms DataGridView.CellLeave.</summary>
#pragma warning disable CS0067 // raised once cell-focus tracking lands; declared for WinForms source compat
        public event EventHandler<DataGridViewCellEventArgs>? CellLeave;
#pragma warning restore CS0067

        /// <summary>Raised when a cell's tooltip text is needed.</summary>
        public event EventHandler<DataGridViewCellToolTipTextNeededEventArgs>? CellToolTipTextNeeded;

        /// <summary>Raised when a cell begins editing. Args typed DataGridViewCellCancelEventArgs to match System.Windows.Forms.</summary>
        public event EventHandler<DataGridViewCellCancelEventArgs>? CellBeginEdit;

        /// <summary>Raised when a cell ends editing.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellEndEdit;

        /// <summary>Raised when a cell value has changed.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellValueChanged;

        /// <summary>Raised when the <see cref="DataSource"/> changes. WinForms compatibility.</summary>
        public event EventHandler? DataSourceChanged;

        private EventHandler<DataGridViewCellFormattingEventArgs>? _cellFormatting;
        /// <summary>Raised when a cell is being formatted for display.</summary>
        public event EventHandler<DataGridViewCellFormattingEventArgs>? CellFormatting { add => _cellFormatting += value; remove => _cellFormatting -= value; }

        private EventHandler<DataGridViewRowsAddedEventArgs>? _rowsAdded;
        /// <summary>Raised when a row is added.</summary>
        public event EventHandler<DataGridViewRowsAddedEventArgs>? RowsAdded { add => _rowsAdded += value; remove => _rowsAdded -= value; }

        private EventHandler<DataGridViewRowsRemovedEventArgs>? _rowsRemoved;
        /// <summary>Raised when rows are removed.</summary>
        public event EventHandler<DataGridViewRowsRemovedEventArgs>? RowsRemoved { add => _rowsRemoved += value; remove => _rowsRemoved -= value; }

        private EventHandler<DataGridViewRowCancelEventArgs>? _userDeletingRow;
        /// <summary>Raised when the user is about to delete a row.</summary>
        public event EventHandler<DataGridViewRowCancelEventArgs>? UserDeletingRow { add => _userDeletingRow += value; remove => _userDeletingRow -= value; }

        private EventHandler<DataGridViewRowEventArgs>? _userDeletedRow;
        /// <summary>Raised after the user has deleted a row.</summary>
        public event EventHandler<DataGridViewRowEventArgs>? UserDeletedRow { add => _userDeletedRow += value; remove => _userDeletedRow -= value; }

        private EventHandler<DataGridViewDataErrorEventArgs>? _dataError;
        /// <summary>Raised when a data error occurs (e.g., binding failure).</summary>
        public event EventHandler<DataGridViewDataErrorEventArgs>? DataError { add => _dataError += value; remove => _dataError -= value; }

        private EventHandler<EventArgs>? _dataBindingComplete;
        /// <summary>Raised when data binding is complete.</summary>
        public event EventHandler<EventArgs>? DataBindingComplete { add => _dataBindingComplete += value; remove => _dataBindingComplete -= value; }

        private EventHandler? _currentCellChanged;
        /// <summary>Raised when the current cell changes.</summary>
        public event EventHandler? CurrentCellChanged { add => _currentCellChanged += value; remove => _currentCellChanged -= value; }

        private EventHandler? _rowDirtyStateNeeded;
        /// <summary>Raised when a row enters the dirty state.</summary>
        public event EventHandler? RowDirtyStateNeeded { add => _rowDirtyStateNeeded += value; remove => _rowDirtyStateNeeded -= value; }

        /// <summary>Raised when a cell is double-clicked.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellDoubleClick;

        /// <summary>Raised on a mouse click in a cell.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? CellMouseClick;

        /// <summary>Raised on a mouse double-click in a cell.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? CellMouseDoubleClick { add { } remove { } }

        /// <summary>Raised on a mouse down in a cell.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? CellMouseDown { add { } remove { } }

        /// <summary>Raised on a mouse up in a cell.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? CellMouseUp { add { } remove { } }

        /// <summary>Raised when the mouse moves over a cell.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? CellMouseMove { add { } remove { } }

        /// <summary>Raised when the mouse enters a cell.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellMouseEnter;

        /// <summary>Raised when the mouse leaves a cell.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellMouseLeave;

        /// <summary>Raised when a cell is validating its content.</summary>
        public event EventHandler<DataGridViewCellValidatingEventArgs>? CellValidating;

        /// <summary>Raised after a cell has been validated.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellValidated;

        private EventHandler<DataGridViewRowPrePaintEventArgs>? _rowPrePaint;
        /// <summary>
        /// Raised by the renderer before a row's background, header and cells are drawn. A handler can
        /// draw its own row content and set <see cref="DataGridViewRowPrePaintEventArgs.Handled"/> to
        /// suppress the grid's default painting for that row.
        /// </summary>
        public event EventHandler<DataGridViewRowPrePaintEventArgs>? RowPrePaint { add => _rowPrePaint += value; remove => _rowPrePaint -= value; }

        /// <summary>Raised by the renderer after a row has been painted.</summary>
        public event EventHandler<DataGridViewRowPostPaintEventArgs>? RowPostPaint;

        private EventHandler<DataGridViewCellCancelEventArgs>? _rowValidating;
        /// <summary>
        /// Raised when the current row is about to be left (row commit). A handler can set
        /// <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> to keep the current row current.
        /// </summary>
        public event EventHandler<DataGridViewCellCancelEventArgs>? RowValidating { add => _rowValidating += value; remove => _rowValidating -= value; }

        private EventHandler<DataGridViewCellEventArgs>? _rowValidated;
        /// <summary>Raised after a row has been validated (i.e. <see cref="RowValidating"/> was not cancelled).</summary>
        public event EventHandler<DataGridViewCellEventArgs>? RowValidated { add => _rowValidated += value; remove => _rowValidated -= value; }

        /// <summary>Raised to supply default values for new rows.</summary>
        public event EventHandler<DataGridViewRowEventArgs>? DefaultValuesNeeded { add { } remove { } }

        /// <summary>Raised when a column is added to the grid.</summary>
        public event EventHandler<DataGridViewColumnEventArgs>? ColumnAdded { add { } remove { } }

        /// <summary>Raised when a column is removed from the grid.</summary>
        public event EventHandler<DataGridViewColumnEventArgs>? ColumnRemoved { add { } remove { } }

        /// <summary>Raised when the user clicks a row header.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? RowHeaderMouseClick { add { } remove { } }

        private EventHandler<DataGridViewCellEventArgs>? _rowEnter;
        /// <summary>Raised when a row becomes the current row.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? RowEnter { add => _rowEnter += value; remove => _rowEnter -= value; }

        private EventHandler<DataGridViewCellEventArgs>? _rowLeave;
        /// <summary>Raised when a row stops being the current row.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? RowLeave { add => _rowLeave += value; remove => _rowLeave -= value; }

        /// <summary>Raised when a cell's content is clicked.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellContentClick;

        /// <summary>Raised when a cell's content is double-clicked.</summary>
        public event EventHandler<DataGridViewCellEventArgs>? CellContentDoubleClick { add { } remove { } }

        private EventHandler<DataGridViewCellPaintingEventArgs>? _cellPainting;
        /// <summary>
        /// Raised by the renderer for each data cell before it is drawn. A handler can paint the cell
        /// itself (optionally calling <see cref="DataGridViewCellPaintingEventArgs.PaintBackground"/> /
        /// <see cref="DataGridViewCellPaintingEventArgs.PaintContent"/> for the parts it does not draw)
        /// and set <see cref="DataGridViewCellPaintingEventArgs.Handled"/> to suppress default painting.
        /// </summary>
        public event EventHandler<DataGridViewCellPaintingEventArgs>? CellPainting { add => _cellPainting += value; remove => _cellPainting -= value; }

        private EventHandler<DataGridViewCellParsingEventArgs>? _cellParsing;
        /// <summary>
        /// Raised when an edit is committed, before the edited text is stored. A handler can convert the
        /// text to a typed value and set <see cref="DataGridViewCellParsingEventArgs.ParsingApplied"/>
        /// to have the grid store that value verbatim.
        /// </summary>
        public event EventHandler<DataGridViewCellParsingEventArgs>? CellParsing { add => _cellParsing += value; remove => _cellParsing -= value; }

        /// <summary>Raised when the state of a row changes.</summary>
        public event EventHandler<DataGridViewRowStateChangedEventArgs>? RowStateChanged { add { } remove { } }

        /// <summary>Raised when the state of a cell changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<DataGridViewCellStateChangedEventArgs>? CellStateChanged { add { } remove { } }

        /// <summary>Raised when a cell enters editing mode and the editing control is about to be shown. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<DataGridViewEditingControlShowingEventArgs>? EditingControlShowing;

        /// <summary>Raised when a column header cell is clicked.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? ColumnHeaderMouseClick;

        /// <summary>Raised when a column header cell is double-clicked.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? ColumnHeaderMouseDoubleClick { add { } remove { } }

        /// <summary>Raised when the width of a column changes.</summary>
        public event EventHandler<DataGridViewColumnEventArgs>? ColumnWidthChanged { add { } remove { } }

        /// <summary>Raised when a new row is needed (virtual mode). Stub in Majorsilence.Forms.</summary>
        public event EventHandler<DataGridViewRowEventArgs>? NewRowNeeded { add { } remove { } }

        /// <summary>Raised when the height of a row changes.</summary>
        public event EventHandler<DataGridViewRowEventArgs>? RowHeightChanged { add { } remove { } }

        /// <summary>Raised when a row header cell is double-clicked.</summary>
        public event EventHandler<DataGridViewCellMouseEventArgs>? RowHeaderMouseDoubleClick { add { } remove { } }

        /// <summary>Raised when the user is deleting a row. Fires before the row is deleted.</summary>
        public event EventHandler<DataGridViewRowEventArgs>? UserAddedRow { add { } remove { } }

        /// <summary>Raised when the sort glyph direction changes.</summary>
        public event EventHandler<DataGridViewColumnEventArgs>? ColumnSortModeChanged { add { } remove { } }

        /// <summary>Raised when a column's display index changes.</summary>
        public event EventHandler<DataGridViewColumnEventArgs>? ColumnDisplayIndexChanged { add { } remove { } }

        /// <summary>Raised when the column header height changes.</summary>
        public event EventHandler? ColumnHeadersHeightChanged { add { } remove { } }

        /// <summary>Raised when the row header width changes.</summary>
        public event EventHandler? RowHeadersWidthChanged { add { } remove { } }

        /// <summary>Raised when auto-sizing in a column finishes.</summary>
        public event EventHandler<DataGridViewAutoSizeColumnModeEventArgs>? AutoSizeColumnModeChanged { add { } remove { } }

        /// <summary>Raised in virtual mode to retrieve the value for a cell.</summary>
        public event EventHandler<DataGridViewCellValueEventArgs>? CellValueNeeded { add { } remove { } }

        /// <summary>Raised in virtual mode to push a new cell value back to the data source.</summary>
        public event EventHandler<DataGridViewCellValueEventArgs>? CellValuePushed { add { } remove { } }

        /// <summary>Raised to allow custom sorting comparison. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<DataGridViewSortCompareEventArgs>? SortCompare { add { } remove { } }

        /// <summary>
        /// Gets or sets whether column headers are visible.
        /// </summary>
        public bool ColumnHeadersVisible {
            get => column_headers_visible;
            set {
                if (column_headers_visible != value) {
                    column_headers_visible = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the collection of columns in the DataGridView.
        /// </summary>
        public DataGridViewColumnCollection Columns { get; }

        /// <summary>
        /// Gets or sets how column widths are automatically adjusted.
        /// </summary>
        public DataGridViewAutoSizeColumnsMode AutoSizeColumnsMode {
            get => auto_size_columns_mode;
            set {
                if (EqualityComparer<DataGridViewAutoSizeColumnsMode>.Default.Equals (auto_size_columns_mode, value))
                    return;

                auto_size_columns_mode = value;
                OnAutoSizeColumnsModeChanged (new DataGridViewAutoSizeColumnsModeEventArgs ([]));
                Invalidate ();
            }
        }

        private DataGridViewAutoSizeColumnsMode auto_size_columns_mode = DataGridViewAutoSizeColumnsMode.None;

        /// <summary>Gets or sets how row heights are automatically adjusted. Stub in Majorsilence.Forms.</summary>
        public DataGridViewAutoSizeRowsMode AutoSizeRowsMode {
            get => auto_size_rows_mode;
            set {
                if (EqualityComparer<DataGridViewAutoSizeRowsMode>.Default.Equals (auto_size_rows_mode, value))
                    return;

                auto_size_rows_mode = value;
                OnAutoSizeRowsModeChanged (new DataGridViewAutoSizeModeEventArgs (false));
                Invalidate ();
            }
        }

        private DataGridViewAutoSizeRowsMode auto_size_rows_mode = DataGridViewAutoSizeRowsMode.None;

        /// <summary>Gets or sets what is copied to the clipboard. Stub in Majorsilence.Forms.</summary>
        public DataGridViewClipboardCopyMode ClipboardCopyMode { get; set; } = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;

        /// <summary>
        /// Gets or sets how the height of the column header row is adjusted.
        /// </summary>
        public DataGridViewColumnHeadersHeightSizeMode ColumnHeadersHeightSizeMode {
            get => column_headers_height_size_mode;
            set {
                if (EqualityComparer<DataGridViewColumnHeadersHeightSizeMode>.Default.Equals (column_headers_height_size_mode, value))
                    return;

                column_headers_height_size_mode = value;
                OnColumnHeadersHeightSizeModeChanged (new DataGridViewAutoSizeModeEventArgs (false));
                Invalidate ();
            }
        }

        private DataGridViewColumnHeadersHeightSizeMode column_headers_height_size_mode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;

        /// <summary>
        /// Gets or sets whether columns are created automatically when DataSource is set.
        /// Majorsilence.Forms stub — column creation from DataSource is not automatically generated.
        /// </summary>
        public bool AutoGenerateColumns {
            get => auto_generate_columns;
            set {
                if (EqualityComparer<bool>.Default.Equals (auto_generate_columns, value))
                    return;

                auto_generate_columns = value;
                OnAutoGenerateColumnsChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool auto_generate_columns = true;

        void System.ComponentModel.ISupportInitialize.BeginInit () { }
        void System.ComponentModel.ISupportInitialize.EndInit () { }

        /// <summary>
        /// Gets or sets the data source for the DataGridView.
        /// Setting this property auto-generates columns from the item type's public properties
        /// and populates the rows from the collection.
        /// </summary>
        public object? DataSource {
            get => data_source;
            set {
                // WinForms accepts any list-like source. Resolve the common ADO.NET cases:
                // a DataTable binds via its DefaultView; an IListSource (e.g. DataSet) via GetList ().
                data_source = value switch {
                    null => null,
                    IList list => list,
                    System.Data.DataTable table => table.DefaultView,
                    System.ComponentModel.IListSource source => source.GetList (),
                    _ => data_source
                };
                OnDataSourceChanged ();
                DataSourceChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (450, 300);

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.Border.Width = 1;
            });

        /// <summary>Gets or sets the data member within the data source. Stub in Majorsilence.Forms.</summary>
        public string DataMember {
            get => data_member;
            set {
                if (EqualityComparer<string>.Default.Equals (data_member, value))
                    return;

                data_member = value;
                OnDataMemberChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private string data_member = string.Empty;

        /// <summary>
        /// Gets or sets the border style applied to the grid's data cells. Setting it rewrites
        /// <see cref="AdvancedCellBorderStyle"/> (as WinForms does) and the renderer draws whatever edges
        /// that describes, so <see cref="DataGridViewCellBorderStyle.None"/> really does remove the grid
        /// lines and the *Horizontal/*Vertical variants really do drop one axis.
        /// </summary>
        public DataGridViewCellBorderStyle CellBorderStyle {
            get => cell_border_style;
            set {
                if (cell_border_style == value)
                    return;

                cell_border_style = value;

                if (value != DataGridViewCellBorderStyle.Custom)
                    ApplyCellBorderStyle (value);

                OnCellBorderStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private DataGridViewCellBorderStyle cell_border_style = DataGridViewCellBorderStyle.Single;
        private readonly DataGridViewAdvancedBorderStyle advanced_cell_border_style = new DataGridViewAdvancedBorderStyle ();
        private readonly DataGridViewAdvancedBorderStyle advanced_column_headers_border_style = new DataGridViewAdvancedBorderStyle ();
        private readonly DataGridViewAdvancedBorderStyle advanced_row_headers_border_style = new DataGridViewAdvancedBorderStyle ();

        /// <summary>
        /// Gets the per-edge border style used for data cells. Assign to its <c>Left</c>/<c>Right</c>/
        /// <c>Top</c>/<c>Bottom</c> members (or call <see cref="DataGridViewAdvancedBorderStyle.All"/>) to
        /// control exactly which cell edges the renderer draws. Changing an edge switches
        /// <see cref="CellBorderStyle"/> to <see cref="DataGridViewCellBorderStyle.Custom"/>, matching
        /// WinForms.
        /// </summary>
        public DataGridViewAdvancedBorderStyle AdvancedCellBorderStyle => advanced_cell_border_style;

        /// <summary>Gets the per-edge border style used for column header cells.</summary>
        public DataGridViewAdvancedBorderStyle AdvancedColumnHeadersBorderStyle => advanced_column_headers_border_style;

        /// <summary>Gets the per-edge border style used for row header cells.</summary>
        public DataGridViewAdvancedBorderStyle AdvancedRowHeadersBorderStyle => advanced_row_headers_border_style;

        // Maps the coarse WinForms CellBorderStyle onto the four edges of AdvancedCellBorderStyle.
        private void ApplyCellBorderStyle (DataGridViewCellBorderStyle style)
        {
            var (horizontal, vertical) = style switch {
                DataGridViewCellBorderStyle.None => (DataGridViewAdvancedCellBorderStyle.None, DataGridViewAdvancedCellBorderStyle.None),
                DataGridViewCellBorderStyle.Single => (DataGridViewAdvancedCellBorderStyle.Single, DataGridViewAdvancedCellBorderStyle.Single),
                DataGridViewCellBorderStyle.Sunken => (DataGridViewAdvancedCellBorderStyle.Inset, DataGridViewAdvancedCellBorderStyle.Inset),
                DataGridViewCellBorderStyle.Raised => (DataGridViewAdvancedCellBorderStyle.Outset, DataGridViewAdvancedCellBorderStyle.Outset),
                DataGridViewCellBorderStyle.SingleHorizontal => (DataGridViewAdvancedCellBorderStyle.Single, DataGridViewAdvancedCellBorderStyle.None),
                DataGridViewCellBorderStyle.SunkenHorizontal => (DataGridViewAdvancedCellBorderStyle.Inset, DataGridViewAdvancedCellBorderStyle.None),
                DataGridViewCellBorderStyle.RaisedHorizontal => (DataGridViewAdvancedCellBorderStyle.Outset, DataGridViewAdvancedCellBorderStyle.None),
                DataGridViewCellBorderStyle.SingleVertical => (DataGridViewAdvancedCellBorderStyle.None, DataGridViewAdvancedCellBorderStyle.Single),
                DataGridViewCellBorderStyle.SunkenVertical => (DataGridViewAdvancedCellBorderStyle.None, DataGridViewAdvancedCellBorderStyle.Inset),
                DataGridViewCellBorderStyle.RaisedVertical => (DataGridViewAdvancedCellBorderStyle.None, DataGridViewAdvancedCellBorderStyle.Outset),
                _ => (DataGridViewAdvancedCellBorderStyle.Single, DataGridViewAdvancedCellBorderStyle.Single)
            };

            // Set the fields through the properties but suppress the Custom switch-over: this is the
            // grid applying CellBorderStyle, not the user hand-editing an edge.
            applying_cell_border_style = true;
            advanced_cell_border_style.Top = horizontal;
            advanced_cell_border_style.Bottom = horizontal;
            advanced_cell_border_style.Left = vertical;
            advanced_cell_border_style.Right = vertical;
            applying_cell_border_style = false;
        }

        private bool applying_cell_border_style;

        /// <summary>Gets or sets whether users can add new rows. Stub in Majorsilence.Forms.</summary>
        public bool AllowUserToAddRows {
            get => allow_user_to_add_rows;
            set {
                if (EqualityComparer<bool>.Default.Equals (allow_user_to_add_rows, value))
                    return;

                allow_user_to_add_rows = value;
                OnAllowUserToAddRowsChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool allow_user_to_add_rows = true;

        /// <summary>Gets or sets whether users can delete rows. Stub in Majorsilence.Forms.</summary>
        public bool AllowUserToDeleteRows {
            get => allow_user_to_delete_rows;
            set {
                if (EqualityComparer<bool>.Default.Equals (allow_user_to_delete_rows, value))
                    return;

                allow_user_to_delete_rows = value;
                OnAllowUserToDeleteRowsChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool allow_user_to_delete_rows = true;

        /// <summary>Gets or sets whether users can order columns. Stub in Majorsilence.Forms.</summary>
        public bool AllowUserToOrderColumns {
            get => allow_user_to_order_columns;
            set {
                if (allow_user_to_order_columns == value)
                    return;

                allow_user_to_order_columns = value;
                OnAllowUserToOrderColumnsChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool allow_user_to_order_columns;

        /// <summary>
        /// Gets or sets whether the user can resize columns by dragging column header borders.
        /// </summary>
        public bool AllowUserToResizeColumns {
            get => allow_user_to_resize_columns;
            set {
                if (EqualityComparer<bool>.Default.Equals (allow_user_to_resize_columns, value))
                    return;

                allow_user_to_resize_columns = value;
                OnAllowUserToResizeColumnsChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool allow_user_to_resize_columns = true;

        /// <summary>
        /// Gets or sets whether the user can resize rows by dragging row header borders.
        /// </summary>
        public bool AllowUserToResizeRows {
            get => allow_user_to_resize_rows;
            set {
                if (EqualityComparer<bool>.Default.Equals (allow_user_to_resize_rows, value))
                    return;

                allow_user_to_resize_rows = value;
                OnAllowUserToResizeRowsChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool allow_user_to_resize_rows = true;

        /// <summary>Gets or sets whether multiple rows can be selected.</summary>
        public bool MultiSelect {
            get => multi_select;
            set {
                if (EqualityComparer<bool>.Default.Equals (multi_select, value))
                    return;

                multi_select = value;
                OnMultiSelectChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool multi_select = true;

        /// <summary>Gets or sets the background color of the DataGridView. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color BackgroundColor {
            get => background_color;
            set {
                if (EqualityComparer<System.Drawing.Color>.Default.Equals (background_color, value))
                    return;

                background_color = value;
                OnBackgroundColorChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private System.Drawing.Color background_color = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the color of the grid lines. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color GridColor {
            get => grid_color;
            set {
                if (EqualityComparer<System.Drawing.Color>.Default.Equals (grid_color, value))
                    return;

                grid_color = value;
                OnGridColorChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private System.Drawing.Color grid_color = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the edit mode of the DataGridView. Stub in Majorsilence.Forms.</summary>
        public DataGridViewEditMode EditMode {
            get => edit_mode;
            set {
                if (EqualityComparer<DataGridViewEditMode>.Default.Equals (edit_mode, value))
                    return;

                edit_mode = value;
                OnEditModeChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private DataGridViewEditMode edit_mode = DataGridViewEditMode.EditOnKeystrokeOrF2;

        /// <summary>Gets or sets the size mode for the row header width. Stub in Majorsilence.Forms.</summary>
        public DataGridViewRowHeadersWidthSizeMode RowHeadersWidthSizeMode {
            get => row_headers_width_size_mode;
            set {
                if (EqualityComparer<DataGridViewRowHeadersWidthSizeMode>.Default.Equals (row_headers_width_size_mode, value))
                    return;

                row_headers_width_size_mode = value;
                OnRowHeadersWidthSizeModeChanged (new DataGridViewAutoSizeModeEventArgs (false));
                Invalidate ();
            }
        }

        private DataGridViewRowHeadersWidthSizeMode row_headers_width_size_mode = DataGridViewRowHeadersWidthSizeMode.EnableResizing;

        /// <summary>Gets or sets the tab key behavior in the DataGridView. Stub in Majorsilence.Forms.</summary>
        public bool StandardTab { get; set; }

        /// <summary>
        /// Gets or sets the default cell style applied to alternating rows. Settable for WinForms
        /// designer assignments (see <see cref="DefaultCellStyle"/>).
        /// </summary>
        public ControlStyle AlternatingRowsDefaultCellStyle {
            get => alternating_rows_default_cell_style;
            set {
                if (EqualityComparer<ControlStyle>.Default.Equals (alternating_rows_default_cell_style, value))
                    return;

                alternating_rows_default_cell_style = value;
                OnAlternatingRowsDefaultCellStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private ControlStyle alternating_rows_default_cell_style = new ControlStyle (DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets or sets the default cell style applied to cells in the DataGridView. Settable so
        /// WinForms designer code assigning a DataGridViewCellStyle compiles (via ControlStyle's
        /// implicit conversion from DataGridViewCellStyle).
        /// </summary>
        public ControlStyle DefaultCellStyle {
            get => default_cell_style;
            set {
                if (EqualityComparer<ControlStyle>.Default.Equals (default_cell_style, value))
                    return;

                default_cell_style = value;
                OnDefaultCellStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private ControlStyle default_cell_style = new ControlStyle (DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets or sets the default cell style applied to column header cells. Stays
        /// ControlStyle-typed (DataGridViewRenderer actually reads BackgroundColor/
        /// ForegroundColor/Font/FontSize off this for real header painting) -- WinForms-style
        /// code assigning a `new DataGridViewCellStyle { BackColor = ..., Font = ..., ... }` here
        /// goes through ControlStyle's implicit conversion from DataGridViewCellStyle instead.
        /// </summary>
        public ControlStyle ColumnHeadersDefaultCellStyle {
            get => column_headers_default_cell_style;
            set {
                if (EqualityComparer<ControlStyle>.Default.Equals (column_headers_default_cell_style, value))
                    return;

                column_headers_default_cell_style = value;
                OnColumnHeadersDefaultCellStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private ControlStyle column_headers_default_cell_style = new ControlStyle (DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets or sets the default cell style applied to row header cells. Settable for
        /// WinForms designer assignments (see <see cref="DefaultCellStyle"/>).
        /// </summary>
        public ControlStyle RowHeadersDefaultCellStyle {
            get => row_headers_default_cell_style;
            set {
                if (EqualityComparer<ControlStyle>.Default.Equals (row_headers_default_cell_style, value))
                    return;

                row_headers_default_cell_style = value;
                OnRowHeadersDefaultCellStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private ControlStyle row_headers_default_cell_style = new ControlStyle (DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>
        /// Gets or sets the default cell style applied to all rows. Settable for WinForms
        /// designer assignments (see <see cref="DefaultCellStyle"/>).
        /// </summary>
        public ControlStyle RowsDefaultCellStyle {
            get => rows_default_cell_style;
            set {
                if (EqualityComparer<ControlStyle>.Default.Equals (rows_default_cell_style, value))
                    return;

                rows_default_cell_style = value;
                OnRowsDefaultCellStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private ControlStyle rows_default_cell_style = new ControlStyle (DataGridViewCell.DefaultCellStyleInternal);

        /// <summary>Commits any pending edit for the specified context. Delegates to EndEdit in Majorsilence.Forms.</summary>
        public bool CommitEdit (DataGridViewDataErrorContexts context) => EndEdit ();

        /// <summary>
        /// Creates the column instance used when columns are created by name (string-based
        /// <see cref="DataGridViewColumnCollection.Add(string)"/> overloads and data-bound
        /// auto-generation). Derived grids override this so every column they own is of their
        /// column type (e.g. the Telerik-compat grid returns its data-column type).
        /// </summary>
        protected internal virtual DataGridViewColumn CreateColumnInstance (string headerText) => new DataGridViewColumn (headerText);

        // Whether the specified cell is the one currently being edited (used by DataGridViewCell compat members).
        internal bool IsCellInEditMode (int rowIndex, int columnIndex)
            => edit_textbox is not null && editing_row_index == rowIndex && editing_column_index == columnIndex;

        // The uncommitted text of the active editor, if any (used by DataGridViewCell.EditedFormattedValue).
        internal string? CurrentEditValue => edit_textbox?.Text;

        /// <summary>Gets whether the current cell has uncommitted changes. Mirrors WinForms.</summary>
        public bool IsCurrentCellDirty => edit_textbox is not null;

        /// <summary>Raised when the current cell's dirty state changes. Declared for WinForms compat; the compat grid commits on end-edit and does not raise it.</summary>
#pragma warning disable CS0067
        public event EventHandler? CurrentCellDirtyStateChanged;

        /// <summary>Raised after the grid finishes sorting. Declared for WinForms compat; the compat grid has no sort pipeline yet.</summary>
        public event EventHandler? Sorted;
#pragma warning restore CS0067

        /// <summary>
        /// Commits the current edit and hides the edit TextBox.
        /// </summary>
        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        public bool EndEdit ()
        {
            if (edit_textbox is null || editing_row_index < 0 || editing_column_index < 0)
                return false;

            var new_value = edit_textbox.Text;

            // WinForms validation cycle: a handler can cancel the commit, keeping the cell in edit mode.
            var validatingArgs = new DataGridViewCellValidatingEventArgs (editing_column_index, editing_row_index, new_value);
            OnCellValidating (validatingArgs);
            if (validatingArgs.Cancel)
                return false;

            var row = Rows[editing_row_index];

            // Ensure enough cells exist
            while (row.Cells.Count <= editing_column_index)
                row.Cells.Add (string.Empty);

            var editing_cell = row.Cells[editing_column_index];
            var old_value = editing_cell.Value?.ToString () ?? string.Empty;

            // WinForms CellParsing: a handler converts the edited text into a typed value before the
            // grid stores it. Without a handler the edited text is stored as-is, as before.
            object? parsed_value = new_value;

            if (_cellParsing is not null) {
                var desired_type = (editing_column_index < Columns.Count ? Columns[editing_column_index].ValueType : null)
                    ?? editing_cell.ValueType
                    ?? typeof (string);

                var parsing_args = new DataGridViewCellParsingEventArgs (editing_column_index, editing_row_index,
                    new_value, desired_type, editing_cell.InheritedStyle);
                OnCellParsing (parsing_args);

                if (parsing_args.ParsingApplied)
                    parsed_value = parsing_args.Value;
            }

            if (old_value != (parsed_value?.ToString () ?? string.Empty) || !Equals (editing_cell.Value, parsed_value)) {
                editing_cell.Value = parsed_value;
                var committed = true;

                // Update the data source if bound
                if (data_source is not null && editing_row_index < data_source.Count) {
                    var item = data_source[editing_row_index];

                    if (item is not null && editing_column_index < Columns.Count) {
                        var prop = item.GetType ().GetProperty (Columns[editing_column_index].HeaderText, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                        if (prop?.CanWrite == true) {
                            try {
                                // A CellParsing handler may already have produced a value of the bound
                                // property's type; only fall back to Convert for anything else.
                                var converted = parsed_value is not null && prop.PropertyType.IsInstanceOfType (parsed_value)
                                    ? parsed_value
                                    : Convert.ChangeType (parsed_value, prop.PropertyType);
                                prop.SetValue (item, converted);
                            } catch {
                                // Conversion failed - revert cell value
                                editing_cell.Value = (object)old_value;
                                committed = false;
                            }
                        }
                    }
                }

                if (committed) {
                    var changed_args = new DataGridViewCellEventArgs (editing_column_index, editing_row_index);
                    OnCellValueChanged (changed_args);
                }
            }

            OnCellValidated (new DataGridViewCellEventArgs (editing_column_index, editing_row_index));

            var end_args = new DataGridViewCellEventArgs (editing_column_index, editing_row_index);
            OnCellEndEdit (end_args);

            // Clean up the TextBox
            edit_textbox.KeyDown -= EditTextBox_KeyDown;
            edit_textbox.LostFocus -= EditTextBox_LostFocus;
            Controls.Remove (edit_textbox);
            edit_textbox.Dispose ();
            edit_textbox = null;
            editing_row_index = -1;
            editing_column_index = -1;

            Invalidate ();
            return true;
        }

        // Handle key events during editing.
        private void EditTextBox_KeyDown (object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) {
                EndEdit ();
                e.Handled = true;
            } else if (e.KeyCode == Keys.Escape) {
                CancelEdit ();
                e.Handled = true;
            } else if (e.KeyCode == Keys.Tab) {
                EndEdit ();

                if (e.Shift)
                    NavigateToPreviousCell ();
                else
                    NavigateToNextCell ();

                // Begin editing the newly selected cell
                if (selected_row_index >= 0 && selected_column_index >= 0)
                    BeginEdit (selected_row_index, selected_column_index);

                e.Handled = true;
            }
        }

        // Handle lost focus during editing.
        private void EditTextBox_LostFocus (object? sender, EventArgs e)
        {
            EndEdit ();
        }

        /// <summary>
        /// Cancels the current edit without committing changes.
        /// </summary>
        public void CancelEdit ()
        {
            if (edit_textbox is null)
                return;

            edit_textbox.KeyDown -= EditTextBox_KeyDown;
            edit_textbox.LostFocus -= EditTextBox_LostFocus;
            Controls.Remove (edit_textbox);
            edit_textbox.Dispose ();
            edit_textbox = null;
            editing_row_index = -1;
            editing_column_index = -1;

            Invalidate ();
        }

        // Repositions the editing TextBox after a scroll; cancels the edit if the cell has scrolled out of view.
        private void UpdateEditTextBoxPosition ()
        {
            if (edit_textbox is null || editing_row_index < 0 || editing_column_index < 0)
                return;

            var cell_bounds = GetCellBounds (editing_row_index, editing_column_index);

            if (cell_bounds.IsEmpty) {
                CancelEdit ();
                return;
            }

            edit_textbox.Left = DeviceToLogicalUnits (cell_bounds.Left) + 1;
            edit_textbox.Top = DeviceToLogicalUnits (cell_bounds.Top) + 1;
            edit_textbox.Width = DeviceToLogicalUnits (cell_bounds.Width) - 2;
            edit_textbox.Height = DeviceToLogicalUnits (cell_bounds.Height) - 2;
        }

        /// <summary>
        /// Gets or sets the index of the first row displayed on the DataGridView.
        /// </summary>
        public int FirstDisplayedScrollingRowIndex {
            get => top_index;
            set {
                if (top_index == value)
                    return;

                if (value < 0 || value >= Rows.Count)
                    return;

                vscrollbar.Value = Math.Min (value, vscrollbar.Maximum);
            }
        }

        /// <summary>
        /// Gets the bounding rectangle for a cell.
        /// </summary>
        public Rectangle GetCellBounds (int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count || columnIndex < 0 || columnIndex >= Columns.Count)
                return Rectangle.Empty;

            if (rowIndex < top_index)
                return Rectangle.Empty;

            var client = GetContentArea ();
            var row_top = client.Top + RowsTopOffset;

            // Accumulate y by summing individual row heights from the first visible row
            var y = row_top;

            for (var i = top_index; i < rowIndex; i++)
                y += LogicalToDeviceUnits (Rows[i].Height);

            var scaled_row_height = LogicalToDeviceUnits (Rows[rowIndex].Height);

            // Row is below the visible area
            if (y >= client.Bottom)
                return Rectangle.Empty;

            var x = GetColumnDeviceLeft (columnIndex);
            var col_width = LogicalToDeviceUnits (Columns[columnIndex].Width);
            return new Rectangle (x, y, col_width, scaled_row_height);
        }

        /// <summary>
        /// Total device-pixel width of the visible frozen columns. Frozen columns are pinned to the left
        /// and do not scroll horizontally.
        /// </summary>
        internal int FrozenColumnsWidth {
            get {
                var total = 0;
                foreach (var col in Columns)
                    if (col.Visible && col.Frozen)
                        total += LogicalToDeviceUnits (col.Width);
                return total;
            }
        }

        /// <summary>Total device-pixel width of the visible right-pinned columns.</summary>
        internal int RightPinnedColumnsWidth {
            get {
                var total = 0;
                foreach (var col in Columns)
                    if (col.Visible && col.PinnedRight)
                        total += LogicalToDeviceUnits (col.Width);
                return total;
            }
        }

        /// <summary>
        /// Device-pixel left X of a column's content. Left-frozen columns pin to the left and right-pinned
        /// columns pin to the right edge (both ignore the horizontal scroll offset); the remaining columns
        /// occupy the scrolling middle band.
        /// </summary>
        internal int GetColumnDeviceLeft (int columnIndex)
        {
            var client = GetContentArea ();
            var left0 = client.Left + (row_headers_visible ? ScaledRowHeadersWidth : 0);

            if (columnIndex < 0 || columnIndex >= Columns.Count)
                return left0;

            var column = Columns[columnIndex];

            if (column.Frozen) {
                var fx = left0;
                for (var i = 0; i < columnIndex; i++)
                    if (Columns[i].Visible && Columns[i].Frozen)
                        fx += LogicalToDeviceUnits (Columns[i].Width);
                return fx;
            }

            if (column.PinnedRight) {
                var rx = client.Right - RightPinnedColumnsWidth;
                for (var i = 0; i < columnIndex; i++)
                    if (Columns[i].Visible && Columns[i].PinnedRight)
                        rx += LogicalToDeviceUnits (Columns[i].Width);
                return rx;
            }

            var x = left0 + FrozenColumnsWidth - horizontal_scroll_offset;
            for (var i = 0; i < columnIndex; i++)
                if (Columns[i].Visible && !Columns[i].Frozen && !Columns[i].PinnedRight)
                    x += LogicalToDeviceUnits (Columns[i].Width);
            return x;
        }

        /// <summary>
        /// Returns the display rectangle for a cell, in client (logical) coordinates.
        /// Pass <paramref name="cutOverflow"/> = true to clip the rectangle to the control bounds.
        /// </summary>
        public Rectangle GetCellDisplayRectangle (int columnIndex, int rowIndex, bool cutOverflow)
        {
            var bounds = DeviceToLogicalUnits (GetCellBounds (rowIndex, columnIndex));

            if (cutOverflow)
                bounds = Rectangle.Intersect (bounds, ClientRectangle);

            return bounds;
        }

        /// <summary>
        /// Returns information about the grid element at the given client coordinates (WinForms compatibility).
        /// Best-effort: scans visible cells via <see cref="GetCellDisplayRectangle"/> and reports the
        /// containing cell, or <see cref="HitTestInfo.Nowhere"/> when the point hits no cell.
        /// </summary>
        public HitTestInfo HitTest (int x, int y)
        {
            for (var col = 0; col < Columns.Count; col++) {
                for (var row = 0; row < Rows.Count; row++) {
                    var rect = GetCellDisplayRectangle (col, row, false);
                    if (rect.Width > 0 && rect.Height > 0 && rect.Contains (x, y))
                        return new HitTestInfo (col, row, rect.X, rect.Y, DataGridViewHitTestType.Cell);
                }
            }

            return HitTestInfo.Nowhere;
        }

        /// <summary>Specifies the part of the <see cref="DataGridView"/> identified by a hit test.</summary>
        public enum DataGridViewHitTestType
        {
            /// <summary>The point is not part of the grid.</summary>
            None,
            /// <summary>The point is over a cell.</summary>
            Cell,
            /// <summary>The point is over a column header.</summary>
            ColumnHeader,
            /// <summary>The point is over a row header.</summary>
            RowHeader,
            /// <summary>The point is over the top-left header.</summary>
            TopLeftHeader,
            /// <summary>The point is over the horizontal scroll bar.</summary>
            HorizontalScrollBar,
            /// <summary>The point is over the vertical scroll bar.</summary>
            VerticalScrollBar
        }

        /// <summary>Contains information about a part of the <see cref="DataGridView"/> at a given location.</summary>
        public sealed class HitTestInfo
        {
            /// <summary>Represents a hit test result that is not over any part of the grid.</summary>
            public static readonly HitTestInfo Nowhere = new HitTestInfo (-1, -1, -1, -1, DataGridViewHitTestType.None);

            internal HitTestInfo (int columnIndex, int rowIndex, int columnX, int rowY, DataGridViewHitTestType type)
            {
                ColumnIndex = columnIndex;
                RowIndex = rowIndex;
                ColumnX = columnX;
                RowY = rowY;
                Type = type;
            }

            /// <summary>The zero-based column index of the cell under the point, or -1.</summary>
            public int ColumnIndex { get; }

            /// <summary>The zero-based row index of the cell under the point, or -1.</summary>
            public int RowIndex { get; }

            /// <summary>The x-coordinate of the left edge of the hit column.</summary>
            public int ColumnX { get; }

            /// <summary>The y-coordinate of the top edge of the hit row.</summary>
            public int RowY { get; }

            /// <summary>The part of the grid that was hit.</summary>
            public DataGridViewHitTestType Type { get; }
        }

        private Rectangle DeviceToLogicalUnits (Rectangle r) =>
            new Rectangle (DeviceToLogicalUnits (r.X), DeviceToLogicalUnits (r.Y), DeviceToLogicalUnits (r.Width), DeviceToLogicalUnits (r.Height));

        /// <summary>
        /// Gets the content area, accounting for scrollbars.
        /// Use Math.Ceiling to avoid fractional DPI rounding artifacts.
        /// </summary>
        internal Rectangle GetContentArea ()
        {
            var client = ClientRectangle;
            var top_offset = Math.Max (0, ContentTopOffset);
            var w = client.Width - (vscrollbar.Visible ? (int)Math.Ceiling (vscrollbar.Width * ScaleFactor.Width) : 0);
            var h = client.Height - top_offset - (hscrollbar.Visible ? (int)Math.Ceiling (hscrollbar.Height * ScaleFactor.Height) : 0);
            return new Rectangle (client.Left, client.Top + top_offset, w, h);
        }

        /// <summary>
        /// Gets the height, in device pixels, of a band reserved at the top of the control (above the
        /// column headers) that the grid does not draw into. Subclasses (e.g. the Telerik-compat
        /// <c>RadGridView</c>) override this to reserve room for a group panel; because every geometry
        /// helper and the renderer derive from <see cref="GetContentArea"/>, the whole grid shifts down
        /// consistently and the reserved band is the subclass's to paint and hit-test. Default 0.
        /// </summary>
        protected virtual int ContentTopOffset => 0;

        /// <summary>
        /// Device-pixel height of an extra fixed band drawn below the column headers and above the data
        /// rows (e.g. the Telerik-compat <c>RadGridView</c> filter row). Because every geometry helper and
        /// the renderer derive the row-start from <see cref="RowsTopOffset"/>, the data rows shift down to
        /// make room and the band is the subclass's to paint and hit-test. Default 0.
        /// </summary>
        protected internal virtual int HeaderExtraHeight => 0;

        /// <summary>
        /// Device-pixel offset from the content top to the first data row: the column-header band (when
        /// visible) plus <see cref="HeaderExtraHeight"/>.
        /// </summary>
        internal int RowsTopOffset => (ColumnHeadersVisible ? ScaledHeaderHeight : 0) + HeaderExtraHeight;

        /// <summary>
        /// Whether the renderer paints alternating-row striping. Default true (the plain grid always
        /// stripes). Subclasses (e.g. the Telerik-compat <c>RadGridView</c>, whose
        /// <c>EnableAlternatingRowColor</c> defaults to false) override to gate it.
        /// </summary>
        protected internal virtual bool AlternatingRowColorsEnabled => true;

        /// <summary>
        /// Gets the column index at the specified location.
        /// </summary>
        internal int GetColumnAtLocation (Point location)
        {
            var client = GetContentArea ();
            var left_end = client.Left + (row_headers_visible ? ScaledRowHeadersWidth : 0) + FrozenColumnsWidth;
            var right_start = client.Right - RightPinnedColumnsWidth;

            // Pinned columns (left and right) sit on top of the scrolling band; test them first.
            for (var i = 0; i < Columns.Count; i++) {
                if (!Columns[i].Visible || (!Columns[i].Frozen && !Columns[i].PinnedRight))
                    continue;
                var px = GetColumnDeviceLeft (i);
                var pw = LogicalToDeviceUnits (Columns[i].Width);
                if (location.X >= px && location.X < px + pw)
                    return i;
            }

            // Scrollable columns occupy only the middle band, between the two pinned bands.
            if (location.X < left_end || location.X >= right_start)
                return -1;

            for (var i = 0; i < Columns.Count; i++) {
                if (!Columns[i].Visible || Columns[i].Frozen || Columns[i].PinnedRight)
                    continue;
                var x = GetColumnDeviceLeft (i);
                var w = LogicalToDeviceUnits (Columns[i].Width);
                if (location.X >= x && location.X < x + w)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Gets the resize column index if the mouse is near a column border.
        /// </summary>
        private int GetResizeColumnAtLocation (Point location)
        {
            var client = GetContentArea ();
            var header_rect = new Rectangle (client.Left, client.Top, client.Width, ScaledHeaderHeight);

            if (!header_rect.Contains (location))
                return -1;

            var left_end = client.Left + (row_headers_visible ? ScaledRowHeadersWidth : 0) + FrozenColumnsWidth;
            var right_start = client.Right - RightPinnedColumnsWidth;
            var resize_zone = LogicalToDeviceUnits (4);

            for (var i = 0; i < Columns.Count; i++) {
                if (!Columns[i].Visible)
                    continue;

                var right = GetColumnDeviceLeft (i) + LogicalToDeviceUnits (Columns[i].Width);

                // Skip scrollable column edges hidden behind either pinned band.
                var scrollable = !Columns[i].Frozen && !Columns[i].PinnedRight;
                if (scrollable && (right < left_end || right > right_start))
                    continue;

                if (Math.Abs (location.X - right) <= resize_zone)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Gets the row index if the mouse is near a row border in the row header area.
        /// </summary>
        private int GetResizeRowAtLocation (Point location)
        {
            if (!row_headers_visible)
                return -1;

            var client = GetContentArea ();
            var header_offset = RowsTopOffset;
            var row_header_rect = new Rectangle (client.Left, client.Top + header_offset, ScaledRowHeadersWidth, Math.Max (0, client.Height - header_offset));

            if (!row_header_rect.Contains (location))
                return -1;

            var row_top = client.Top + header_offset;
            var resize_zone = LogicalToDeviceUnits (4);

            for (var i = top_index; i < Rows.Count; i++) {
                var scaled_row_height = LogicalToDeviceUnits (Rows[i].Height);
                row_top += scaled_row_height;

                if (row_top > client.Bottom)
                    break;

                if (Math.Abs (location.Y - row_top) <= resize_zone)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Gets the row index at the specified location.
        /// </summary>
        internal int GetRowAtLocation (Point location)
        {
            var client = GetContentArea ();
            var row_top = client.Top + RowsTopOffset;

            if (location.Y < row_top)
                return -1;

            var y = row_top;

            for (var i = top_index; i < Rows.Count; i++) {
                var h = LogicalToDeviceUnits (Rows[i].Height);

                if (location.Y >= y && location.Y < y + h)
                    return i;

                y += h;

                if (y >= client.Bottom)
                    break;
            }

            return -1;
        }

        /// <summary>
        /// Gets or sets the height, in pixels, of the column headers row.
        /// </summary>
        public int ColumnHeadersHeight {
            get => header_height;
            set {
                if (header_height != value) {
                    header_height = Math.Max (value, 10);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the currently selected cell, or null if no cell is selected.
        /// </summary>
        public DataGridViewCell? CurrentCell {
            get {
                if (selected_row_index < 0 || selected_row_index >= Rows.Count)
                    return null;

                if (selected_column_index < 0 || selected_column_index >= Rows[selected_row_index].Cells.Count)
                    return null;

                return Rows[selected_row_index].Cells[selected_column_index];
            }
            set {
                // WinForms compatibility: setting CurrentCell moves the selection to that cell.
                if (value is null)
                    return;

                SelectedColumnIndex = value.ColumnIndex;
                SelectedRowIndex = value.RowIndex;
                _currentCellChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets whether the header cells use the system's visual styles. Stub in
        /// Majorsilence.Forms (headers are theme-rendered) — kept for WinForms compatibility.
        /// </summary>
        public bool EnableHeadersVisualStyles { get; set; } = true;

        /// <summary>
        /// Gets the row and column indices of the currently selected cell.
        /// </summary>
        public Point CurrentCellAddress => new Point (selected_column_index, selected_row_index);

        /// <summary>Gets the cell at the specified column and row indices. In WinForms, [columnIndex, rowIndex].</summary>
        public DataGridViewCell? this[int columnIndex, int rowIndex] {
            get {
                if (rowIndex < 0 || rowIndex >= Rows.Count) return null;
                var row = Rows[rowIndex];
                if (columnIndex < 0 || columnIndex >= row.Cells.Count) return null;
                return row.Cells[columnIndex];
            }
        }

        /// <summary>Gets the cell at the specified column name and row index.</summary>
        public DataGridViewCell? this[string columnName, int rowIndex] {
            get {
                if (rowIndex < 0 || rowIndex >= Rows.Count) return null;
                return Rows[rowIndex].Cells[columnName];
            }
        }

        /// <summary>
        /// Gets the row containing the currently selected cell, or null if no row is selected.
        /// </summary>
        public DataGridViewRow? CurrentRow {
            get {
                if (selected_row_index >= 0 && selected_row_index < Rows.Count)
                    return Rows[selected_row_index];

                return null;
            }
        }

        /// <summary>
        /// Gets the horizontal scroll offset.
        /// </summary>
        internal int HorizontalScrollOffset => horizontal_scroll_offset;

        /// <summary>
        /// Gets or sets the index of the currently hovered row.
        /// </summary>
        internal int HoveredRowIndex {
            get => hovered_row_index;
            set {
                if (hovered_row_index != value) {
                    hovered_row_index = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets whether a cell is currently being edited.
        /// </summary>
        public bool IsCurrentCellInEditMode => edit_textbox is not null;

        /// <summary>
        /// Raises the CellBeginEdit event.
        /// </summary>
        protected virtual void OnCellBeginEdit (DataGridViewCellCancelEventArgs e) => CellBeginEdit?.Invoke (this, e);

        /// <summary>
        /// Raises the CellEndEdit event.
        /// </summary>
        protected virtual void OnCellEndEdit (DataGridViewCellEventArgs e) => CellEndEdit?.Invoke (this, e);

        /// <summary>
        /// Raises the CellValueChanged event.
        /// </summary>
        protected virtual void OnCellValueChanged (DataGridViewCellEventArgs e) => CellValueChanged?.Invoke (this, e);

        /// <summary>
        /// Handles a column header click for sorting.
        /// </summary>
        private void OnColumnHeaderClick (int columnIndex)
        {
            var column = Columns[columnIndex];

            // Toggle sort order
            var new_order = column.SortOrder == SortOrder.Ascending
                ? SortOrder.Descending
                : SortOrder.Ascending;

            // Reset all other columns
            foreach (var col in Columns)
                col.SortOrder = SortOrder.None;

            column.SortOrder = new_order;

            // Sort the data
            SortByColumn (columnIndex, new_order);

            // Raise the event
            ColumnHeaderClick?.Invoke (this, new EventArgs<DataGridViewColumn> (column));

            Invalidate ();
        }

        /// <summary>
        /// Raised when a column header is clicked.
        /// </summary>
        public event EventHandler<EventArgs<DataGridViewColumn>>? ColumnHeaderClick;

        // Populates rows and columns from the DataSource.
        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        private void OnDataSourceChanged ()
        {
            Rows.Clear ();

            if (data_source is null)
                return;

            // Preferred bound-list path. DataView, DataTable.DefaultView and BindingSource all implement
            // ITypedList, which exposes the bound *schema* as property descriptors -- the DataColumns of
            // a view, or the columns of a BindingSource's resolved list (including a DataSet + DataMember).
            // Generate columns from those descriptors even when the source has zero rows, matching
            // WinForms, whose column collection reflects the bound schema regardless of row count. This
            // is what makes grid.Columns["Foo"] resolvable before any row loads.
            //
            // Name/DataPropertyName are set to the member name (not just HeaderText): the Columns[name]
            // indexer matches Name OR HeaderText, and app code routinely reassigns HeaderText
            // (grid.Columns["Start"].HeaderText = "Reminder Date") then looks the column up again by its
            // original field name -- without Name set, that second lookup returns null and the caller NREs.
            if (data_source is System.ComponentModel.ITypedList typed_list) {
                var descriptors = typed_list.GetItemProperties (null);

                if (AutoGenerateColumns) {
                    Columns.Clear ();

                    foreach (System.ComponentModel.PropertyDescriptor descriptor in descriptors) {
                        var generated = Columns.Add (descriptor.Name, EstimateColumnWidth (descriptor.Name));
                        generated.Name = descriptor.Name;
                        generated.DataPropertyName = descriptor.Name;
                    }
                }

                var typedRows = new System.Collections.Generic.List<DataGridViewRow> ();
                foreach (var item in data_source) {
                    if (item is null)
                        continue;

                    var row = new DataGridViewRow ();
                    for (var i = 0; i < descriptors.Count; i++)
                        row.Cells.Add (descriptors[i].GetValue (item)?.ToString () ?? string.Empty);
                    row.DataBoundItem = item;
                    typedRows.Add (row);
                }

                // Populate in one shot: each individual Rows.Add fires OnRowsChanged, and derived grids
                // (RadGridView) rebuild their whole view on every change, so adding N rows one at a time
                // is O(N^2) -- slow to display for large result sets. ReplaceAll notifies once.
                Rows.ReplaceAll (typedRows);

                SetInitialCurrentCell ();
                _dataBindingComplete?.Invoke (this, EventArgs.Empty);
                return;
            }

            if (AutoGenerateColumns) {
                // Auto-generate columns from public readable properties
                Columns.Clear ();
                var element_type = GetElementType (data_source);

                if (element_type is null)
                    return;

                // Indexers are excluded, and not as a tidy-up: GetValue with no index arguments
                // throws TargetParameterCountException, so binding to a List<string> -- whose element
                // type carries string's Chars indexer -- crashed on the first row.
                var properties = element_type.GetProperties (BindingFlags.Public | BindingFlags.Instance)
                    .Where (p => p.CanRead && p.GetIndexParameters ().Length == 0)
                    .ToArray ();

                foreach (var prop in properties) {
                    var generated = Columns.Add (prop.Name, EstimateColumnWidth (prop.Name));
                    // See the DataView branch above: set Name/DataPropertyName to the member name so
                    // Columns[name] lookups keep working after app code reassigns HeaderText.
                    generated.Name = prop.Name;
                    generated.DataPropertyName = prop.Name;
                }

                var propertyRows = new System.Collections.Generic.List<DataGridViewRow> ();
                foreach (var item in data_source) {
                    if (item is null)
                        continue;

                    var row = new DataGridViewRow ();
                    for (var i = 0; i < properties.Length; i++)
                        row.Cells.Add (properties[i].GetValue (item)?.ToString () ?? string.Empty);
                    row.DataBoundItem = item;
                    propertyRows.Add (row);
                }

                // One notification instead of one per row (see ReplaceAll note in the ITypedList branch).
                Rows.ReplaceAll (propertyRows);
            } else {
                // Columns were manually defined — populate rows via DataPropertyName (or HeaderText fallback)
                var manualRows = new System.Collections.Generic.List<DataGridViewRow> ();
                foreach (var item in data_source) {
                    if (item is null)
                        continue;

                    var row = new DataGridViewRow ();
                    for (var i = 0; i < Columns.Count; i++) {
                        var col = Columns[i];
                        var prop_name = string.IsNullOrEmpty (col.DataPropertyName) ? col.HeaderText : col.DataPropertyName;
                        var prop = string.IsNullOrEmpty (prop_name)
                            ? null
                            : item.GetType ().GetProperty (prop_name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                        // See the generated-columns branch: an indexer cannot be read without index
                        // arguments, so a column mapped to one contributes nothing rather than throwing.
                        if (prop?.GetIndexParameters ().Length > 0)
                            prop = null;

                        row.Cells.Add (prop?.GetValue (item)?.ToString () ?? string.Empty);
                    }

                    row.DataBoundItem = item;
                    manualRows.Add (row);
                }

                Rows.ReplaceAll (manualRows);
            }

            SetInitialCurrentCell ();
            _dataBindingComplete?.Invoke (this, EventArgs.Empty);
        }

        // WinForms/Telerik make the first cell current as soon as a data source with rows is bound, so
        // grid.CurrentRow / grid.CurrentCell are non-null immediately -- app code very commonly reads
        // CurrentRow.Cells["..."] right after binding. Only seed a selection when none exists; never
        // override a selection the user or app already set. Fields are set directly (rather than through
        // the SelectedRowIndex property) to avoid raising selection/layout events mid-bind.
        private void SetInitialCurrentCell ()
        {
            if (selected_row_index < 0 && Rows.Count > 0)
                selected_row_index = 0;

            if (selected_column_index < 0 && Columns.Count > 0)
                selected_column_index = 0;
        }

        // Gets the element type from an IList.
        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        private static Type? GetElementType (IList list)
        {
            var list_type = list.GetType ();

            // Check for generic IList<T>
            foreach (var iface in list_type.GetInterfaces ()) {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition () == typeof (IList<>))
                    return iface.GetGenericArguments ()[0];
            }

            // Fallback: use type of first item
            if (list.Count > 0 && list[0] is not null)
                return list[0]!.GetType ();

            return null;
        }

        // Estimates a column width based on header text length.
        private static int EstimateColumnWidth (string headerText)
        {
            return Math.Max (80, headerText.Length * 10 + 20);
        }

        /// <inheritdoc/>
        // Tracks the cell the pointer is currently over, so CellMouseEnter/CellMouseLeave fire once per
        // cell transition. Kept separate from HoveredRowIndex (row highlight) to detect transitions.
        private int hovered_column_index = -1;
        private int hovered_cell_row = -1;

        // Fires CellMouseLeave for the previously hovered cell and CellMouseEnter for the new one, once
        // per cell transition. Pass (-1, -1) to signal the pointer left the grid.
        private void UpdateHoveredCell (int row, int col)
        {
            if (row == hovered_cell_row && col == hovered_column_index)
                return;

            if (hovered_cell_row >= 0 && hovered_column_index >= 0)
                OnCellMouseLeave (new DataGridViewCellEventArgs (hovered_column_index, hovered_cell_row));

            hovered_cell_row = row;
            hovered_column_index = col;

            if (row >= 0 && col >= 0)
                OnCellMouseEnter (new DataGridViewCellEventArgs (col, row));
        }

        /// <summary>Raises the CellDoubleClick event.</summary>
        protected virtual void OnCellDoubleClick (DataGridViewCellEventArgs e) => CellDoubleClick?.Invoke (this, e);
        /// <summary>Raises the CellMouseClick event.</summary>
        protected virtual void OnCellMouseClick (DataGridViewCellMouseEventArgs e) => CellMouseClick?.Invoke (this, e);
        /// <summary>Raises the CellContentClick event.</summary>
        protected virtual void OnCellContentClick (DataGridViewCellEventArgs e) => CellContentClick?.Invoke (this, e);
        /// <summary>Raises the ColumnHeaderMouseClick event.</summary>
        protected virtual void OnColumnHeaderMouseClick (DataGridViewCellMouseEventArgs e) => ColumnHeaderMouseClick?.Invoke (this, e);
        /// <summary>Raises the CellMouseEnter event.</summary>
        protected virtual void OnCellMouseEnter (DataGridViewCellEventArgs e) => CellMouseEnter?.Invoke (this, e);
        /// <summary>Raises the CellMouseLeave event.</summary>
        protected virtual void OnCellMouseLeave (DataGridViewCellEventArgs e) => CellMouseLeave?.Invoke (this, e);
        /// <summary>Raises the CellValidating event.</summary>
        protected virtual void OnCellValidating (DataGridViewCellValidatingEventArgs e) => CellValidating?.Invoke (this, e);
        /// <summary>Raises the CellValidated event.</summary>
        protected virtual void OnCellValidated (DataGridViewCellEventArgs e) => CellValidated?.Invoke (this, e);
        /// <summary>Raises the EditingControlShowing event.</summary>
        protected virtual void OnEditingControlShowing (DataGridViewEditingControlShowingEventArgs e) => EditingControlShowing?.Invoke (this, e);
        /// <summary>Raises the RowPostPaint event. Called by the renderer after a data row is drawn.</summary>
        protected internal virtual void RaiseRowPostPaint (int rowIndex) => RaiseRowPostPaint (new DataGridViewRowPostPaintEventArgs (rowIndex));

        /// <summary>Raises the RowPostPaint event with fully populated paint args (renderer path).</summary>
        protected internal virtual void RaiseRowPostPaint (DataGridViewRowPostPaintEventArgs e) => RowPostPaint?.Invoke (this, e);

        /// <summary>Raises the RowPrePaint event. Called by the renderer before a data row is drawn.</summary>
        protected virtual void OnRowPrePaint (DataGridViewRowPrePaintEventArgs e) => _rowPrePaint?.Invoke (this, e);

        /// <summary>
        /// Raises RowPrePaint from the renderer. Returns true when a handler set
        /// <see cref="DataGridViewRowPrePaintEventArgs.Handled"/>, meaning the renderer must skip its own
        /// painting of the row. No allocation happens when nothing is subscribed.
        /// </summary>
        protected internal virtual bool RaiseRowPrePaint (DataGridViewRowPrePaintEventArgs e)
        {
            if (_rowPrePaint is null)
                return false;

            OnRowPrePaint (e);
            return e.Handled;
        }

        /// <summary>Gets whether anything is subscribed to <see cref="RowPrePaint"/> (lets the renderer skip building args).</summary>
        internal bool HasRowPrePaintHandlers => _rowPrePaint is not null;

        /// <summary>Gets whether anything is subscribed to <see cref="RowPostPaint"/>.</summary>
        internal bool HasRowPostPaintHandlers => RowPostPaint is not null;

        /// <summary>Gets whether anything is subscribed to <see cref="CellPainting"/>.</summary>
        internal bool HasCellPaintingHandlers => _cellPainting is not null;

        /// <summary>Raises the CellPainting event.</summary>
        protected virtual void OnCellPainting (DataGridViewCellPaintingEventArgs e) => _cellPainting?.Invoke (this, e);

        /// <summary>
        /// Raises CellPainting from the renderer. Returns true when a handler set
        /// <see cref="DataGridViewCellPaintingEventArgs.Handled"/>, meaning the renderer must skip its
        /// own painting of that cell.
        /// </summary>
        protected internal virtual bool RaiseCellPainting (DataGridViewCellPaintingEventArgs e)
        {
            if (_cellPainting is null)
                return false;

            OnCellPainting (e);
            return e.Handled;
        }

        /// <summary>Raises the CellFormatting event.</summary>
        protected virtual void OnCellFormatting (DataGridViewCellFormattingEventArgs e) => _cellFormatting?.Invoke (this, e);

        /// <summary>Raises the CellParsing event.</summary>
        protected virtual void OnCellParsing (DataGridViewCellParsingEventArgs e) => _cellParsing?.Invoke (this, e);

        /// <summary>Raises the RowValidating event.</summary>
        protected virtual void OnRowValidating (DataGridViewCellCancelEventArgs e) => _rowValidating?.Invoke (this, e);

        /// <summary>Raises the RowValidated event.</summary>
        protected virtual void OnRowValidated (DataGridViewCellEventArgs e) => _rowValidated?.Invoke (this, e);

        /// <summary>Raises the RowEnter event.</summary>
        protected virtual void OnRowEnter (DataGridViewCellEventArgs e) => _rowEnter?.Invoke (this, e);

        /// <summary>Raises the RowLeave event.</summary>
        protected virtual void OnRowLeave (DataGridViewCellEventArgs e) => _rowLeave?.Invoke (this, e);

        /// <summary>
        /// Runs the WinForms row-commit cycle for the row that is about to stop being current: commits a
        /// pending edit, then raises RowValidating and (unless cancelled) RowValidated followed by
        /// RowLeave. Returns false when a handler cancelled, in which case the caller must keep the row
        /// current. Mirrors <see cref="Control"/>'s Validating/Validated cycle, at row granularity.
        /// </summary>
        protected virtual bool ValidateRow (int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                return true;

            // WinForms commits the cell edit before validating the row it belongs to.
            if (edit_textbox is not null && editing_row_index == rowIndex && !EndEdit ())
                return false;

            var args = new DataGridViewCellCancelEventArgs (selected_column_index, rowIndex);
            OnRowValidating (args);

            if (args.Cancel)
                return false;

            OnRowValidated (new DataGridViewCellEventArgs (selected_column_index, rowIndex));
            OnRowLeave (new DataGridViewCellEventArgs (selected_column_index, rowIndex));
            return true;
        }

        /// <summary>
        /// Runs the row-commit cycle for the current row (WinForms <c>EndEdit</c> + row validation), as
        /// happens when the grid loses focus. Returns false when a handler cancelled.
        /// </summary>
        public bool ValidateCurrentRow () => ValidateRow (selected_row_index);

        /// <inheritdoc/>
        protected override void OnLostFocus (EventArgs e)
        {
            // WinForms validates the current row as the grid loses focus, before the control-level
            // Validating/Validated cycle the base class runs.
            ValidateRow (selected_row_index);
            base.OnLostFocus (e);
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            if (read_only || !Enabled)
                return;

            var row = GetRowAtLocation (e.Location);
            var col = GetColumnAtLocation (e.Location);

            if (row >= 0 && col >= 0) {
                OnCellDoubleClick (new DataGridViewCellEventArgs (col, row));
                BeginEdit (row, col);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!Enabled || !e.Button.HasFlag (MouseButtons.Left))
                return;

            // If editing, end edit when clicking outside the editor
            if (edit_textbox is not null) {
                var edit_bounds = edit_textbox.ScaledBounds;

                if (!edit_bounds.Contains (e.Location))
                    EndEdit ();
            }

            // Check for column resize
            if (ColumnHeadersVisible && AllowUserToResizeColumns) {
                var resize_col = GetResizeColumnAtLocation (e.Location);

                if (resize_col >= 0) {
                    is_resizing_column = true;
                    resize_column_index = resize_col;
                    resize_start_x = e.Location.X;
                    resize_start_width = LogicalToDeviceUnits (Columns[resize_col].Width);
                    return;
                }
            }

            // Check for row resize
            if (row_headers_visible && AllowUserToResizeRows) {
                var resize_row = GetResizeRowAtLocation (e.Location);

                if (resize_row >= 0) {
                    is_resizing_row = true;
                    resize_row_index = resize_row;
                    resize_start_y = e.Location.Y;
                    resize_start_height = LogicalToDeviceUnits (Rows[resize_row].Height);
                    return;
                }
            }

            // Check for header click (sorting)
            if (ColumnHeadersVisible) {
                var client = GetContentArea ();
                var header_rect = new Rectangle (client.Left, client.Top, client.Width, ScaledHeaderHeight);

                if (header_rect.Contains (e.Location)) {
                    var col = GetColumnAtLocation (e.Location);

                    if (col >= 0) {
                        // Fires for every header click (WinForms uses rowIndex -1 for header cells),
                        // not just sortable columns.
                        OnColumnHeaderMouseClick (new DataGridViewCellMouseEventArgs (col, -1, e.Location.X - GetColumnDeviceLeft (col), e.Location.Y - client.Top, e));

                        if (Columns[col].Sortable)
                            OnColumnHeaderClick (col);
                    }

                    return;
                }
            }

            // Select row/cell
            var row = GetRowAtLocation (e.Location);

            if (row >= 0) {
                int col;

                if (selection_mode == DataGridViewSelectionMode.FullRowSelect) {
                    SelectedRowIndex = row;
                    col = GetColumnAtLocation (e.Location);
                } else {
                    col = GetColumnAtLocation (e.Location);
                    SelectedRowIndex = row;
                    SelectedColumnIndex = col;
                }

                // Toggle check-box cells on click (covers DataGridViewCheckBoxColumn and any column
                // that renders as a check box, e.g. the Telerik-compat GridViewCheckBoxColumn).
                if (col >= 0 && col < Columns.Count
                    && (Columns[col] is DataGridViewCheckBoxColumn || Columns[col].DisplaysAsCheckBox)
                    && row < Rows.Count && col < Rows[row].Cells.Count) {
                    var cell = Rows[row].Cells[col];

                    if (!cell.ReadOnly) {
                        var current = cell.Value is bool b ? b
                            : string.Equals (cell.Value?.ToString (), "True", StringComparison.OrdinalIgnoreCase) || cell.Value?.ToString () == "1";
                        cell.Value = !current;
                        OnCellValueChanged (new DataGridViewCellEventArgs (col, row));
                    }
                }

                var cellArgs = new DataGridViewCellEventArgs (col, row);
                CellClick?.Invoke (this, cellArgs);

                if (col >= 0) {
                    OnCellMouseClick (new DataGridViewCellMouseEventArgs (col, row, e.Location.X - GetColumnDeviceLeft (col), e.Location.Y, e));
                    // Raised after the check-box toggle above so the committed value is current.
                    OnCellContentClick (cellArgs);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);
            HoveredRowIndex = -1;
            UpdateHoveredCell (-1, -1);

            if (!is_resizing_column && !is_resizing_row)
                SetCursorDirect (Cursors.Arrow);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (is_resizing_column) {
                var delta = e.Location.X - resize_start_x;
                var new_width = DeviceToLogicalUnits (resize_start_width + delta);
                Columns[resize_column_index].Width = new_width;
                UpdateScrollBars ();
                return;
            }

            if (is_resizing_row) {
                var delta = e.Location.Y - resize_start_y;
                var new_height = DeviceToLogicalUnits (resize_start_height + delta);
                Rows[resize_row_index].Height = Math.Max (new_height, 10);
                UpdateScrollBars ();
                return;
            }

            // Update cursor for column resize zones
            if (ColumnHeadersVisible && AllowUserToResizeColumns) {
                var resize_col = GetResizeColumnAtLocation (e.Location);

                if (resize_col >= 0) {
                    if (Cursor != Cursors.SizeWestEast)
                        SetCursorDirect (Cursors.SizeWestEast);

                    // Update hovered row
                    HoveredRowIndex = GetRowAtLocation (e.Location);
                    return;
                }
            }

            // Update cursor for row resize zones
            if (row_headers_visible && AllowUserToResizeRows) {
                var resize_row = GetResizeRowAtLocation (e.Location);

                if (resize_row >= 0) {
                    if (Cursor != Cursors.SizeNorthSouth)
                        SetCursorDirect (Cursors.SizeNorthSouth);

                    HoveredRowIndex = GetRowAtLocation (e.Location);
                    return;
                }
            }

            if (Cursor != Cursors.Arrow)
                SetCursorDirect (Cursors.Arrow);

            // Update hovered row
            var row = GetRowAtLocation (e.Location);
            HoveredRowIndex = row;

            UpdateHoveredCell (row, row >= 0 ? GetColumnAtLocation (e.Location) : -1);

            // Fire CellToolTipTextNeeded if handlers are attached
            if (CellToolTipTextNeeded != null && row >= 0) {
                var col = GetColumnAtLocation (e.Location);
                if (col >= 0) {
                    var args = new DataGridViewCellToolTipTextNeededEventArgs (col, row);
                    CellToolTipTextNeeded?.Invoke (this, args);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (is_resizing_column) {
                is_resizing_column = false;
                resize_column_index = -1;
                SetCursorDirect (Cursors.Arrow);
            }

            if (is_resizing_row) {
                is_resizing_row = false;
                resize_row_index = -1;
                SetCursorDirect (Cursors.Arrow);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (vscrollbar.Visible)
                vscrollbar.RaiseMouseWheel (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            RenderManager.Render (this, e);

            base.OnPaint (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            // F2 begins editing
            if (e.KeyCode == Keys.F2 && !read_only && selected_row_index >= 0 && selected_column_index >= 0) {
                BeginEdit (selected_row_index, selected_column_index);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Down) {
                if (selected_row_index < Rows.Count - 1) {
                    SelectedRowIndex = selected_row_index + 1;
                    EnsureRowVisible (selected_row_index);
                    e.Handled = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.Up) {
                if (selected_row_index > 0) {
                    SelectedRowIndex = selected_row_index - 1;
                    EnsureRowVisible (selected_row_index);
                    e.Handled = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.PageDown) {
                var new_index = Math.Min (selected_row_index + DisplayedRowCount, Rows.Count - 1);
                SelectedRowIndex = new_index;
                EnsureRowVisible (new_index);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.PageUp) {
                var new_index = Math.Max (selected_row_index - DisplayedRowCount, 0);
                SelectedRowIndex = new_index;
                EnsureRowVisible (new_index);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Home) {
                SelectedRowIndex = 0;
                EnsureRowVisible (0);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.End) {
                SelectedRowIndex = Rows.Count - 1;
                EnsureRowVisible (Rows.Count - 1);
                e.Handled = true;
                return;
            }

            if (selection_mode != DataGridViewSelectionMode.FullRowSelect) {
                if (e.KeyCode == Keys.Left && selected_column_index > 0) {
                    SelectedColumnIndex = selected_column_index - 1;
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Right && selected_column_index < Columns.Count - 1) {
                    SelectedColumnIndex = selected_column_index + 1;
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode == Keys.Tab) {
                    if (e.Shift)
                        NavigateToPreviousCell ();
                    else
                        NavigateToNextCell ();

                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyUp (e);
        }

        /// <summary>
        /// Called when the row collection changes.
        /// </summary>
        internal virtual void OnRowsChanged ()
        {
            UpdateScrollBars ();
            Invalidate ();
        }

        /// <summary>
        /// Called when the column collection changes.
        /// </summary>
        internal virtual void OnColumnsChanged ()
        {
            UpdateScrollBars ();
            Invalidate ();
        }

        /// <summary>
        /// Gets or sets whether the DataGridView is read-only.
        /// </summary>
        public bool ReadOnly {
            get => read_only;
            set {
                if (read_only != value) {
                    read_only = value;

                    if (read_only)
                        CancelEdit ();

                    OnReadOnlyChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the collection of rows in the DataGridView.
        /// </summary>
        public DataGridViewRowCollection Rows { get; }

        /// <summary>
        /// Gets or sets the default height, in pixels, of each row.
        /// </summary>
        public int RowHeight {
            get => row_height;
            set {
                if (row_height != value) {
                    row_height = Math.Max (value, 10);
                    UpdateScrollBars ();
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the row header column is displayed.
        /// </summary>
        public bool RowHeadersVisible {
            get => row_headers_visible;
            set {
                if (row_headers_visible != value) {
                    row_headers_visible = value;
                    UpdateScrollBars ();
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the width, in pixels, of the row header column.
        /// </summary>
        public int RowHeadersWidth {
            get => row_headers_width;
            set {
                if (row_headers_width != value) {
                    row_headers_width = Math.Max (value, 10);
                    UpdateScrollBars ();
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets how cells in the DataGridView can be selected.
        /// </summary>
        public DataGridViewSelectionMode SelectionMode {
            get => selection_mode;
            set {
                if (selection_mode != value) {
                    selection_mode = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the scaled height of the header row.
        /// </summary>
        internal int ScaledHeaderHeight => LogicalToDeviceUnits (header_height);

        /// <summary>
        /// Gets the scaled height of each data row.
        /// </summary>
        internal int ScaledRowHeight => LogicalToDeviceUnits (row_height);

        /// <summary>
        /// Gets the scaled width of the row header column.
        /// </summary>
        internal int ScaledRowHeadersWidth => LogicalToDeviceUnits (row_headers_width);

        /// <summary>
        /// Gets or sets the index of the currently selected column.
        /// </summary>
        public int SelectedColumnIndex {
            get => selected_column_index;
            set {
                if (selected_column_index != value) {
                    selected_column_index = value;
                    OnSelectionChanged (EventArgs.Empty);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the collection of selected rows (read-only).
        /// </summary>
        public IReadOnlyList<DataGridViewRow> SelectedRows =>
            Rows.Where (r => r.Selected).ToList ().AsReadOnly ();

        /// <summary>
        /// Gets the collection of selected cells (read-only, returns cells in the selected rows).
        /// </summary>
        public IReadOnlyList<DataGridViewCell> SelectedCells {
            get {
                var cells = new List<DataGridViewCell> ();

                foreach (var row in SelectedRows)
                    cells.AddRange (row.Cells);

                return cells.AsReadOnly ();
            }
        }

        /// <summary>
        /// Gets or sets the index of the currently selected row.
        /// </summary>
        public int SelectedRowIndex {
            get => selected_row_index;
            set {
                if (selected_row_index != value) {
                    // WinForms row-commit cycle: leaving a row commits its edit and runs
                    // RowValidating/RowValidated/RowLeave. A cancelling handler keeps the row current.
                    if (!ValidateRow (selected_row_index))
                        return;

                    // Deselect old row
                    if (selected_row_index >= 0 && selected_row_index < Rows.Count)
                        Rows[selected_row_index].Selected = false;

                    selected_row_index = value;

                    // Select new row
                    if (selected_row_index >= 0 && selected_row_index < Rows.Count)
                        Rows[selected_row_index].Selected = true;

                    if (selected_row_index >= 0 && selected_row_index < Rows.Count)
                        OnRowEnter (new DataGridViewCellEventArgs (selected_column_index, selected_row_index));

                    OnSelectionChanged (EventArgs.Empty);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Raises the SelectionChanged event.
        /// </summary>
        protected virtual void OnSelectionChanged (EventArgs e) => SelectionChanged?.Invoke (this, e);

        /// <summary>
        /// Called by the renderer once per row, before its cells are drawn, so subclasses (e.g. the
        /// Telerik-compat RadGridView) can apply row-level formatting. Default no-op.
        /// </summary>
        protected internal virtual void RaiseRowFormatting (DataGridViewRow row, int rowIndex) { }

        /// <summary>
        /// Called by the renderer for each cell, before it is drawn, so subclasses can apply
        /// cell-level formatting (e.g. data-driven colors). Default no-op.
        /// </summary>
        protected internal virtual void RaiseCellFormatting (DataGridViewRow row, int rowIndex, int columnIndex) { }

        /// <summary>
        /// The renderer's single formatting entry point for a cell: runs the subclass formatting hook
        /// (<see cref="RaiseCellFormatting"/>), then raises the WinForms <see cref="CellFormatting"/>
        /// event, then applies the resolved cell style's format string. Returns the display text the
        /// renderer should draw, or null to fall back to the cell's own value. Any style the handler
        /// changed is returned through <paramref name="handlerStyle"/> and applies to this paint only --
        /// nothing is written back onto the cell, so a conditional format never goes stale.
        /// </summary>
        internal string? ApplyCellFormatting (DataGridViewRow row, int rowIndex, int columnIndex, out DataGridViewCellStyle? handlerStyle)
        {
            handlerStyle = null;

            RaiseCellFormatting (row, rowIndex, columnIndex);

            if (columnIndex < 0 || columnIndex >= row.Cells.Count)
                return null;

            var cell = row.Cells[columnIndex];
            var column = columnIndex < Columns.Count ? Columns[columnIndex] : null;

            // A subclass formatting pass (e.g. RadGridView's CellFormatting) may already have produced
            // the display text; the renderer picks that up from the cell when we return null.
            var subclassText = cell.FormattedTextOverride;

            if (_cellFormatting is null)
                return subclassText is null ? FormatCellValue (cell.Value, cell.InheritedStyle, column) : null;

            var args = new DataGridViewCellFormattingEventArgs (columnIndex, rowIndex, cell.Value, typeof (string), cell.InheritedStyle);
            OnCellFormatting (args);
            handlerStyle = args.CellStyle;

            // WinForms: FormattingApplied means the handler already produced the display value, so the
            // grid must not format it again.
            if (args.FormattingApplied)
                return args.Value?.ToString () ?? string.Empty;

            var formatted = FormatCellValue (args.Value, args.CellStyle, column);

            if (formatted is not null)
                return formatted;

            // The handler replaced the value but left the default formatting to the grid.
            if (!Equals (args.Value, cell.Value))
                return args.Value?.ToString () ?? string.Empty;

            return null;
        }

        /// <summary>
        /// Applies a resolved cell style's format string (falling back to the column's
        /// <see cref="DataGridViewColumn.FormatString"/>) to a raw value. Returns null when there is
        /// nothing to format, so callers can fall back to <c>value.ToString ()</c>.
        /// </summary>
        private static string? FormatCellValue (object? value, DataGridViewCellStyle? style, DataGridViewColumn? column)
        {
            if (value is null || value is DBNull)
                return style?.NullValue?.ToString ();

            var format = style is not null && style.Format.Length > 0 ? style.Format : column?.FormatString;

            if (string.IsNullOrEmpty (format))
                return null;

            var provider = style?.FormatProvider ?? System.Globalization.CultureInfo.CurrentCulture;

            // Telerik-style "{0:C2}" composite formats and plain ".NET" format specifiers both occur.
            if (format.Contains ('{'))
                return string.Format (provider, format, value);

            return value is IFormattable formattable ? formattable.ToString (format, provider) : null;
        }

        /// <summary>
        /// Raised when the selection changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        // Sets the cursor and immediately updates the OS cursor.
        // Setting Cursor alone only takes effect on next OnMouseEnter, so we
        // must call SetCursor directly to update the cursor during mouse move.
        private void SetCursorDirect (Cursor cursor)
        {
            Cursor = cursor;
            FindForm ()?.SetCursor (cursor);
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore (x, y, width, height, specified);

            UpdateScrollBars ();
        }

        /// <summary>
        /// Sorts the rows by the specified column.
        /// </summary>
        public void SortByColumn (int columnIndex, SortOrder order)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count || order == SortOrder.None || Rows.Count == 0)
                return;

            // Sort the rows in-place (note: List.Sort is not guaranteed to be stable)
            var sorted = Rows.ToList ();

            sorted.Sort ((a, b) => {
                var val_a = columnIndex < a.Cells.Count ? a.Cells[columnIndex].Value?.ToString () ?? string.Empty : string.Empty;
                var val_b = columnIndex < b.Cells.Count ? b.Cells[columnIndex].Value?.ToString () ?? string.Empty : string.Empty;

                // Try numeric comparison first
                if (double.TryParse (val_a, out var num_a) && double.TryParse (val_b, out var num_b)) {
                    var cmp = num_a.CompareTo (num_b);
                    return order == SortOrder.Descending ? -cmp : cmp;
                }

                // Fall back to string comparison
                var result = string.Compare (val_a, val_b, StringComparison.CurrentCultureIgnoreCase);
                return order == SortOrder.Descending ? -result : result;
            });

            // Replace rows without triggering per-item change notifications
            Rows.ReplaceAll (sorted);
        }

        /// <summary>Clears the current selection.</summary>
        public void ClearSelection ()
        {
            foreach (var row in Rows)
                row.Selected = false;

            selected_row_index = -1;
            selected_column_index = -1;
            Invalidate ();
        }

        /// <summary>Sorts the data by the specified column in the specified direction.</summary>
        public void Sort (DataGridViewColumn column, System.ComponentModel.ListSortDirection direction)
        {
            var idx = Columns.IndexOf (column);

            if (idx >= 0)
                SortByColumn (idx, direction == System.ComponentModel.ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending);
        }

        /// <summary>Invalidates a specific cell, forcing it to repaint.</summary>
        public void InvalidateCell (int columnIndex, int rowIndex) => Invalidate ();

        /// <summary>
        /// Builds a data object holding the currently selected cells, honoring
        /// <see cref="ClipboardCopyMode"/> (returns null when copying is disabled or nothing is selected).
        /// The object carries tab-delimited text (Text/UnicodeText), CSV and an HTML table, so
        /// <c>Clipboard.SetDataObject (grid.GetClipboardContent ())</c> pastes into a text editor or a
        /// spreadsheet the same way WinForms does.
        /// </summary>
        public virtual DataObject? GetClipboardContent ()
        {
            if (ClipboardCopyMode == DataGridViewClipboardCopyMode.Disable)
                return null;

            // Selected cells, grouped into the rows/columns they occupy so the copy keeps its shape.
            var rows = new List<int> ();
            var columns = new SortedSet<int> ();

            for (var r = 0; r < Rows.Count; r++) {
                var row = Rows[r];
                var any = false;

                for (var c = 0; c < row.Cells.Count; c++) {
                    if (!row.Cells[c].Selected && !(row.Selected && SelectionMode == DataGridViewSelectionMode.FullRowSelect))
                        continue;

                    if (c < Columns.Count && !Columns[c].Visible)
                        continue;

                    columns.Add (c);
                    any = true;
                }

                if (any)
                    rows.Add (r);
            }

            if (rows.Count == 0 || columns.Count == 0)
                return null;

            var includeHeaders = ClipboardCopyMode is DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
                || (ClipboardCopyMode == DataGridViewClipboardCopyMode.EnableWithAutoHeaderText && ColumnHeadersVisible);

            var text = new System.Text.StringBuilder ();
            var csv = new System.Text.StringBuilder ();
            var html = new System.Text.StringBuilder ("<table>");

            if (includeHeaders) {
                var headers = columns.Select (c => c < Columns.Count ? Columns[c].HeaderText : string.Empty).ToList ();
                AppendClipboardRow (text, csv, html, headers, isHeader: true);
            }

            foreach (var r in rows) {
                var row = Rows[r];
                var values = columns
                    .Select (c => c < row.Cells.Count ? (row.Cells[c].FormattedValue?.ToString () ?? string.Empty) : string.Empty)
                    .ToList ();
                AppendClipboardRow (text, csv, html, values, isHeader: false);
            }

            html.Append ("</table>");

            var data = new DataObject ();
            var plain = text.ToString ();
            data.SetData (DataFormats.Text.Name, plain);
            data.SetData (DataFormats.UnicodeText.Name, plain);
            data.SetData (DataFormats.CommaSeparatedValue.Name, csv.ToString ());
            data.SetData (DataFormats.Html.Name, html.ToString ());
            return data;
        }

        // Appends one clipboard row in each of the three formats the grid publishes.
        private static void AppendClipboardRow (System.Text.StringBuilder text, System.Text.StringBuilder csv,
            System.Text.StringBuilder html, IReadOnlyList<string> values, bool isHeader)
        {
            text.AppendLine (string.Join ("\t", values));
            csv.AppendLine (string.Join (",", values.Select (QuoteCsv)));

            var tag = isHeader ? "TH" : "TD";
            html.Append ("<TR>");

            foreach (var value in values)
                html.Append ('<').Append (tag).Append ('>')
                    .Append (System.Net.WebUtility.HtmlEncode (value))
                    .Append ("</").Append (tag).Append ('>');

            html.Append ("</TR>");
        }

        // CSV quoting: only when needed, doubling embedded quotes (RFC 4180).
        private static string QuoteCsv (string value)
            => value.IndexOfAny ([',', '"', '\n', '\r']) >= 0
                ? '"' + value.Replace ("\"", "\"\"") + '"'
                : value;

        /// <summary>Gets a row that serves as a template for new rows. Stub in Majorsilence.Forms.</summary>
        public DataGridViewRow RowTemplate { get; } = new DataGridViewRow ();

        /// <summary>Gets the index of the row for new records, or -1 if AllowUserToAddRows is false.</summary>
        public int NewRowIndex => AllowUserToAddRows ? Rows.Count : -1;

        /// <summary>Gets or sets the column used for row headers. Stub in Majorsilence.Forms.</summary>
        public DataGridViewColumn? SortedColumn { get; private set; }

        /// <summary>Gets the sort order of the current sort. None if not sorted.</summary>
        public SortOrder SortOrder { get; private set; } = SortOrder.None;

        /// <summary>Adjusts the width of all columns to fit their contents. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeColumns () => Invalidate ();

        /// <summary>Adjusts the width of all columns using the specified sizing mode. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeColumns (DataGridViewAutoSizeColumnsMode autoSizeColumnsMode) => Invalidate ();

        /// <summary>Adjusts the width of the specified column to fit its contents. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeColumn (int columnIndex) => Invalidate ();

        /// <summary>Adjusts the width of the specified column using the specified sizing mode. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeColumn (int columnIndex, DataGridViewAutoSizeColumnMode autoSizeColumnMode) => Invalidate ();

        /// <summary>Adjusts the height of all rows to fit their contents. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeRows () => Invalidate ();

        /// <summary>Adjusts the height of all rows using the specified sizing mode. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeRow (int rowIndex) => Invalidate ();

        /// <summary>Adjusts the height of the specified row using the given sizing mode. Stub in Majorsilence.Forms.</summary>
        public void AutoResizeRow (int rowIndex, DataGridViewAutoSizeRowMode autoSizeRowMode) => Invalidate ();

        /// <summary>Gets or sets the visual style of the grid's border.</summary>
        public BorderStyle BorderStyle {
            get => border_style;
            set {
                if (EqualityComparer<BorderStyle>.Default.Equals (border_style, value))
                    return;

                border_style = value;
                OnBorderStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private BorderStyle border_style = BorderStyle.Fixed3D;

        /// <summary>Gets the columns currently selected (via column-header click). Stub: always empty -- the compat grid does not track column selection.</summary>
        public DataGridViewColumnCollection SelectedColumns => new DataGridViewColumnCollection (this);

        /// <summary>Selects all cells, rows, or columns, depending on selection mode.</summary>
        public void SelectAll ()
        {
            if (SelectionMode == DataGridViewSelectionMode.FullRowSelect) {
                foreach (var row in Rows)
                    row.Selected = true;
            } else {
                foreach (var row in Rows)
                    foreach (var cell in row.Cells)
                        cell.Selected = true;
            }

            Invalidate ();
        }

        /// <summary>Scrolls the DataGridView so that the specified cell is visible.</summary>
        public void ScrollIntoView (int columnIndex, int rowIndex) => Invalidate ();

        /// <summary>Scrolls the DataGridView to ensure the specified cell is visible.</summary>
        public void EnsureVisible (int rowIndex, int columnIndex) => ScrollIntoView (columnIndex, rowIndex);

        /// <summary>Returns the number of cells that have the specified state.</summary>
        public int GetCellCount (DataGridViewElementStates includeFilter)
        {
            if (includeFilter == DataGridViewElementStates.Selected)
                return SelectedCells.Count;
            return Rows.Count * Columns.Count;
        }

        /// <summary>Invalidates a specific row, forcing it to repaint.</summary>
        public void InvalidateRow (int rowIndex) => Invalidate ();

        /// <summary>Invalidates a specific column, forcing it to repaint.</summary>
        public void InvalidateColumn (int columnIndex) => Invalidate ();

        /// <summary>Notifies the DataGridView that the current cell value has changed. Stub in Majorsilence.Forms.</summary>
        public void NotifyCurrentCellDirty (bool dirty) { }

        /// <summary>Updates the value displayed in the specified cell. Invalidates the cell in Majorsilence.Forms.</summary>
        public void UpdateCellValue (int columnIndex, int rowIndex) => Invalidate ();

        /// <summary>Resets the editing control for the current cell. Stub in Majorsilence.Forms.</summary>
        public void RefreshEdit () { }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets the total width needed to display all columns.
        /// </summary>
        internal int TotalColumnsWidth {
            get {
                var total = 0;

                foreach (var col in Columns)
                    if (col.Visible)
                        total += LogicalToDeviceUnits (col.Width);

                return total;
            }
        }

        /// <summary>
        /// Updates the scrollbars based on the current content.
        /// </summary>
        private void UpdateScrollBars ()
        {
            var client = GetContentArea ();
            var header_offset = RowsTopOffset;
            var content_height = client.Height - header_offset;

            // Count how many rows fit in the content area using their actual heights
            var visible_rows = 0;
            var rows_height = 0;

            for (var i = 0; i < Rows.Count; i++) {
                var rh = LogicalToDeviceUnits (Rows[i].Height);

                if (rows_height + rh <= content_height) {
                    visible_rows++;
                    rows_height += rh;
                } else {
                    break;
                }
            }

            // Vertical scrollbar
            if (Rows.Count > visible_rows && visible_rows > 0) {
                vscrollbar.Visible = true;
                vscrollbar.Maximum = Rows.Count - visible_rows;
                vscrollbar.LargeChange = Math.Max (0, visible_rows);
            } else {
                vscrollbar.Visible = false;
                vscrollbar.Value = 0;
                top_index = 0;
            }

            // Horizontal scrollbar
            var available_width = client.Width - (vscrollbar.Visible ? (int)Math.Ceiling (vscrollbar.Width * ScaleFactor.Width) : 0);

            // Pinned columns (left + right) are always visible, so only the scrollable columns drive the H-scrollbar.
            var pinned_width = FrozenColumnsWidth + RightPinnedColumnsWidth;
            var scrollable_total = TotalColumnsWidth - pinned_width;
            var scrollable_available = available_width - pinned_width;

            if (scrollable_total > scrollable_available && scrollable_available > 0) {
                hscrollbar.Visible = true;
                hscrollbar.Maximum = scrollable_total - scrollable_available;
                hscrollbar.LargeChange = Math.Max (0, scrollable_available);
            } else {
                hscrollbar.Visible = false;
                hscrollbar.Value = 0;
                horizontal_scroll_offset = 0;
            }
        }

        /// <summary>
        /// Gets or sets the number of columns. Setting it before binding declares an unbound grid's
        /// columns (WinForms pattern): grows by appending plain columns, or shrinks by removing from
        /// the end.
        /// </summary>
        public int ColumnCount {
            get => Columns.Count;
            set {
                while (Columns.Count < value)
                    Columns.Add (string.Empty);
                while (Columns.Count > value)
                    Columns.RemoveAt (Columns.Count - 1);
            }
        }

        /// <summary>Gets or sets which scroll bars are displayed. Stub in Majorsilence.Forms.</summary>
        public ScrollBars ScrollBars { get; set; } = ScrollBars.Both;

        /// <summary>Gets or sets the horizontal scrolling offset in pixels. Stub in Majorsilence.Forms.</summary>
        public int HorizontalScrollingOffset { get; set; }

        /// <summary>Gets or sets the first column index that is displayed.</summary>
        public int FirstDisplayedScrollingColumnIndex { get; set; }

        /// <summary>Gets or sets whether the grid is in virtual mode. Stub in Majorsilence.Forms.</summary>
        public bool VirtualMode { get; set; }

        /// <summary>Gets or sets whether the selection highlight is hidden when the control loses focus. Stub in Majorsilence.Forms.</summary>
        public bool HideSelection { get; set; }

        /// <summary>Gets or sets the number of rows in the grid. Setting adds/removes rows to reach the count.</summary>
        public int RowCount {
            get => Rows.Count;
            set {
                while (Rows.Count < value) Rows.Add ();
                while (Rows.Count > value && Rows.Count > 0) Rows.RemoveAt (Rows.Count - 1);
            }
        }

        /// <summary>Gets the control used to edit the current cell, or null if not in edit mode. Stub in Majorsilence.Forms.</summary>
        public Control? EditingControl => null;

        /// <summary>Gets the panel that contains editing controls. Stub in Majorsilence.Forms.</summary>
        public Panel? EditingPanel => null;

        /// <summary>
        /// Gets the number of full rows that can be displayed at a time.
        /// </summary>
        public int DisplayedRowCount {
            get {
                var content = GetContentArea ();
                var available = content.Height - RowsTopOffset;
                var count = 0;
                var h = 0;

                for (var i = 0; i < Rows.Count; i++) {
                    var rh = LogicalToDeviceUnits (Rows[i].Height);

                    if (h + rh <= available) {
                        count++;
                        h += rh;
                    } else {
                        break;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Ensures the specified row is visible by scrolling if necessary.
        /// </summary>
        private void EnsureRowVisible (int index)
        {
            if (DisplayedRowCount >= Rows.Count)
                return;

            if (index < top_index)
                FirstDisplayedScrollingRowIndex = index;
            else if (index >= top_index + DisplayedRowCount)
                FirstDisplayedScrollingRowIndex = index - DisplayedRowCount + 1;
        }

        // Moves the selection to the next cell, wrapping to the next row.
        private void NavigateToNextCell ()
        {
            if (Columns.Count == 0 || Rows.Count == 0)
                return;

            if (selected_column_index < Columns.Count - 1) {
                SelectedColumnIndex = selected_column_index + 1;
            } else if (selected_row_index < Rows.Count - 1) {
                SelectedColumnIndex = 0;
                SelectedRowIndex = selected_row_index + 1;
                EnsureRowVisible (selected_row_index);
            }
        }

        // Moves the selection to the previous cell, wrapping to the previous row.
        private void NavigateToPreviousCell ()
        {
            if (Columns.Count == 0 || Rows.Count == 0)
                return;

            if (selected_column_index > 0) {
                SelectedColumnIndex = selected_column_index - 1;
            } else if (selected_row_index > 0) {
                SelectedColumnIndex = Columns.Count - 1;
                SelectedRowIndex = selected_row_index - 1;
                EnsureRowVisible (selected_row_index);
            }
        }

        /// <summary>
        /// Converts device units to logical units.
        /// </summary>
        internal int DeviceToLogicalUnits (int value)
        {
            var factor = Scaling;
            return factor > 0 ? (int)(value / factor) : value;
        }
    }

    /// <summary>
    /// Provides data for a cancelable cell event. Mirrors System.Windows.Forms
    /// DataGridViewCellCancelEventArgs (the args of CellBeginEdit/RowValidating).
    /// </summary>
    public class DataGridViewCellCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance for the specified cell.</summary>
        public DataGridViewCellCancelEventArgs (int columnIndex, int rowIndex)
        {
            ColumnIndex = columnIndex;
            RowIndex = rowIndex;
        }

        /// <summary>Gets the column index of the cell.</summary>
        public int ColumnIndex { get; }

        /// <summary>Gets the row index of the cell.</summary>
        public int RowIndex { get; }
    }

    /// <summary>
    /// Provides data for cell editing events. Superseded by
    /// <see cref="DataGridViewCellCancelEventArgs"/> for CellBeginEdit (WinForms parity); kept for
    /// source compatibility.
    /// </summary>
    public class DataGridViewCellEditEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the DataGridViewCellEditEventArgs class.
        /// </summary>
        public DataGridViewCellEditEventArgs (int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        /// <summary>
        /// Gets or sets whether the editing operation should be canceled.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Gets the column index of the cell.
        /// </summary>
        public int ColumnIndex { get; }

        /// <summary>
        /// Gets the row index of the cell.
        /// </summary>
        public int RowIndex { get; }
    }
}
