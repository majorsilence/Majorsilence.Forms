using System;
using System.Drawing;
using System.Globalization;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides data for the DataGridView.CellClick event.
    /// </summary>
    public class DataGridViewCellEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellEventArgs (int columnIndex, int rowIndex)
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
    /// Provides data for the DataGridView.CellToolTipTextNeeded event.
    /// </summary>
    public class DataGridViewCellToolTipTextNeededEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellToolTipTextNeededEventArgs (int columnIndex, int rowIndex) : base (columnIndex, rowIndex) { }

        /// <summary>Gets or sets the tooltip text.</summary>
        public string ToolTipText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Stores style information for a DataGridView cell.
    /// </summary>
    public class DataGridViewCellStyle : ICloneable
    {
        private object? null_value = string.Empty;
        private object? data_source_null_value = DBNull.Value;
        private System.IFormatProvider? format_provider;
        private Padding padding = Padding.Empty;

        /// <summary>Initializes a new instance of the <see cref="DataGridViewCellStyle"/> class.</summary>
        public DataGridViewCellStyle ()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DataGridViewCellStyle"/> class, copying values from the supplied style.</summary>
        public DataGridViewCellStyle (DataGridViewCellStyle dataGridViewCellStyle)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewCellStyle);
            ApplyStyle (dataGridViewCellStyle);
        }

        /// <summary>Gets or sets the background color.</summary>
        public Color BackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the foreground color.</summary>
        public Color ForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the format string applied to cell content.</summary>
        public string Format {
            get => format ?? string.Empty;
            set => format = string.IsNullOrEmpty (value) ? string.Empty : value;
        }
        private string format = string.Empty;

        /// <summary>Gets or sets the object used to provide culture-specific formatting of cell values.</summary>
        public System.IFormatProvider FormatProvider {
            get => format_provider ?? CultureInfo.CurrentCulture;
            set => format_provider = value;
        }

        /// <summary>Gets a value indicating whether the <see cref="FormatProvider"/> property has been set.</summary>
        public bool IsFormatProviderDefault => format_provider is null;

        /// <summary>Gets or sets the selection background color.</summary>
        public Color SelectionBackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the selection foreground color.</summary>
        public Color SelectionForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the font used to display text. Stub in Majorsilence.Forms.</summary>
#pragma warning disable CA1416
        public Majorsilence.Drawing.Font? Font { get; set; }
#pragma warning restore CA1416

        /// <summary>Gets or sets how cell content is aligned within the cell.</summary>
        public DataGridViewContentAlignment Alignment { get; set; } = DataGridViewContentAlignment.NotSet;

        /// <summary>Gets or sets how text is wrapped within a cell.</summary>
        public DataGridViewTriState WrapMode { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>Gets or sets the value displayed when a cell's value is null.</summary>
        public object? NullValue {
            get => null_value;
            set => null_value = value;
        }

        /// <summary>Gets a value indicating whether the <see cref="NullValue"/> property is set to its default value (the empty string).</summary>
        public bool IsNullValueDefault => null_value is string s && s.Length == 0;

        /// <summary>Gets or sets the value stored in the data source when the user enters a null value.</summary>
        public object? DataSourceNullValue {
            get => data_source_null_value;
            set => data_source_null_value = value;
        }

        /// <summary>Gets a value indicating whether the <see cref="DataSourceNullValue"/> property is set to its default value (<see cref="DBNull.Value"/>).</summary>
        public bool IsDataSourceNullValueDefault => ReferenceEquals (data_source_null_value, DBNull.Value);

        /// <summary>Gets or sets an object that contains additional data associated with the style.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the scope of the style. Always <see cref="DataGridViewCellStyleScopes.None"/> in Majorsilence.Forms.</summary>
        public DataGridViewCellStyleScopes Scope => DataGridViewCellStyleScopes.None;

        /// <summary>Gets or sets the padding within the cell. Negative values are clamped to zero.</summary>
        public Padding Padding {
            get => padding;
            set {
                if (value.Left < 0 || value.Top < 0 || value.Right < 0 || value.Bottom < 0)
                    value = new Padding (
                        Math.Max (0, value.Left),
                        Math.Max (0, value.Top),
                        Math.Max (0, value.Right),
                        Math.Max (0, value.Bottom));

                padding = value;
            }
        }

        /// <summary>Copies the values from the supplied style into this style.</summary>
        public void ApplyStyle (DataGridViewCellStyle dataGridViewCellStyle)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewCellStyle);

            if (!dataGridViewCellStyle.BackColor.IsEmpty)
                BackColor = dataGridViewCellStyle.BackColor;
            if (!dataGridViewCellStyle.ForeColor.IsEmpty)
                ForeColor = dataGridViewCellStyle.ForeColor;
            if (!dataGridViewCellStyle.SelectionBackColor.IsEmpty)
                SelectionBackColor = dataGridViewCellStyle.SelectionBackColor;
            if (!dataGridViewCellStyle.SelectionForeColor.IsEmpty)
                SelectionForeColor = dataGridViewCellStyle.SelectionForeColor;
            if (dataGridViewCellStyle.Font is not null)
                Font = dataGridViewCellStyle.Font;
            if (!dataGridViewCellStyle.IsNullValueDefault)
                NullValue = dataGridViewCellStyle.NullValue;
            if (!dataGridViewCellStyle.IsDataSourceNullValueDefault)
                DataSourceNullValue = dataGridViewCellStyle.DataSourceNullValue;
            if (dataGridViewCellStyle.Format.Length != 0)
                Format = dataGridViewCellStyle.Format;
            if (!dataGridViewCellStyle.IsFormatProviderDefault)
                FormatProvider = dataGridViewCellStyle.FormatProvider;
            if (dataGridViewCellStyle.Alignment != DataGridViewContentAlignment.NotSet)
                Alignment = dataGridViewCellStyle.Alignment;
            if (dataGridViewCellStyle.WrapMode != DataGridViewTriState.NotSet)
                WrapMode = dataGridViewCellStyle.WrapMode;
            if (dataGridViewCellStyle.Padding != Padding.Empty)
                Padding = dataGridViewCellStyle.Padding;
            if (dataGridViewCellStyle.Tag is not null)
                Tag = dataGridViewCellStyle.Tag;
        }

        /// <summary>Returns a clone of this DataGridViewCellStyle.</summary>
        public DataGridViewCellStyle Clone () => new DataGridViewCellStyle (this) {
            // The copy constructor uses ApplyStyle, which only copies non-default values.
            // Copy the remaining (defaultable) members directly so the clone is exact.
            NullValue = NullValue,
            DataSourceNullValue = DataSourceNullValue,
            Tag = Tag
        };

        object ICloneable.Clone () => Clone ();

        /// <inheritdoc/>
        public override bool Equals (object? obj)
        {
            if (obj is not DataGridViewCellStyle other)
                return false;

            return BackColor == other.BackColor
                && ForeColor == other.ForeColor
                && SelectionBackColor == other.SelectionBackColor
                && SelectionForeColor == other.SelectionForeColor
                && Equals (Font, other.Font)
                && Alignment == other.Alignment
                && WrapMode == other.WrapMode
                && Padding == other.Padding
                && Format == other.Format
                && Equals (format_provider, other.format_provider)
                && Equals (NullValue, other.NullValue)
                && Equals (DataSourceNullValue, other.DataSourceNullValue)
                && Equals (Tag, other.Tag);
        }

        /// <inheritdoc/>
        public override int GetHashCode ()
        {
            var hash = new HashCode ();
            hash.Add (BackColor);
            hash.Add (ForeColor);
            hash.Add (SelectionBackColor);
            hash.Add (SelectionForeColor);
            hash.Add (Font);
            hash.Add (Alignment);
            hash.Add (WrapMode);
            hash.Add (Padding);
            hash.Add (Format);
            return hash.ToHashCode ();
        }
    }

    /// <summary>Defines the scope to which a DataGridViewCellStyle applies. Majorsilence.Forms only exposes <see cref="None"/>.</summary>
    [Flags]
    public enum DataGridViewCellStyleScopes
    {
        /// <summary>No scope.</summary>
        None = 0,
        /// <summary>The cell scope.</summary>
        Cell = 1,
        /// <summary>The column scope.</summary>
        Column = 2,
        /// <summary>The row scope.</summary>
        Row = 4
    }

    /// <summary>
    /// Represents a text box column in a DataGridView.
    /// </summary>
    public class DataGridViewTextBoxColumn : DataGridViewColumn { }

    /// <summary>
    /// Represents an image column in a DataGridView. Stub in Majorsilence.Forms (images are not rendered).
    /// </summary>
    public class DataGridViewImageColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the image displayed in the column. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Drawing.Image? Image { get; set; }

        /// <summary>Gets or sets the description of the image. Stub in Majorsilence.Forms.</summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a link column in a DataGridView. Stub in Majorsilence.Forms.
    /// </summary>
    public class DataGridViewLinkColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the link text. Stub in Majorsilence.Forms.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the active link color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color ActiveLinkColor { get; set; } = System.Drawing.Color.Red;

        /// <summary>Gets or sets the link color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color LinkColor { get; set; } = System.Drawing.Color.Blue;

        /// <summary>Gets or sets the visited link color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color VisitedLinkColor { get; set; } = System.Drawing.Color.Purple;

        /// <summary>Gets or sets whether the link tracking is enabled. Stub in Majorsilence.Forms.</summary>
        public bool TrackVisitedState { get; set; } = true;

        /// <summary>Gets or sets whether the column header text is used as link text. Stub in Majorsilence.Forms.</summary>
        public bool UseColumnTextForLinkValue { get; set; }
    }

    /// <summary>
    /// Represents a check box column in a DataGridView.
    /// </summary>
    public class DataGridViewCheckBoxColumn : DataGridViewColumn { }

    /// <summary>
    /// Represents a button column in a DataGridView.
    /// </summary>
    public class DataGridViewButtonColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the button text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the column HeaderText is used as button text.</summary>
        public bool UseColumnTextForButtonValue { get; set; }
    }

    /// <summary>
    /// Represents a combo box column in a DataGridView.
    /// </summary>
    public class DataGridViewComboBoxColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the data source for the combo box items.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the property used for display text.</summary>
        public string DisplayMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the property used as the underlying value.</summary>
        public string ValueMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the flat style (stub).</summary>
        public FlatStyle FlatStyle { get; set; }
    }

    /// <summary>
    /// Specifies how the widths of columns are adjusted when the DataGridView is resized.
    /// </summary>
    /// <summary>Specifies how row heights are automatically adjusted in a DataGridView.</summary>
    public enum DataGridViewAutoSizeRowsMode
    {
        /// <summary>Row heights are not automatically adjusted.</summary>
        None,
        /// <summary>Row heights adjust to fit the content of all cells, including headers.</summary>
        AllCells,
        /// <summary>Row heights adjust to fit the content of all cells, excluding headers.</summary>
        AllCellsExceptHeaders,
        /// <summary>Row heights adjust to fit the content of displayed cells, including headers.</summary>
        DisplayedCells,
        /// <summary>Row heights adjust to fit the content of displayed cells, excluding headers.</summary>
        DisplayedCellsExceptHeaders
    }

    /// <summary>Specifies how content is copied to the clipboard from a DataGridView.</summary>
    public enum DataGridViewClipboardCopyMode
    {
        /// <summary>Clipboard copy is disabled.</summary>
        Disable,
        /// <summary>Text values of selected cells are copied.</summary>
        EnableAlwaysIncludeHeaderText,
        /// <summary>Text values of selected cells are copied without headers.</summary>
        EnableWithoutHeaderText,
        /// <summary>Text values of selected cells are copied; headers are included if row/column headers are selected.</summary>
        EnableWithAutoHeaderText
    }

    /// <summary>Specifies how column widths are automatically sized.</summary>
    public enum DataGridViewAutoSizeColumnsMode
    {
        /// <summary>Column widths are not automatically adjusted.</summary>
        None,
        /// <summary>Column widths adjust to fit the content of all cells, including headers.</summary>
        AllCells,
        /// <summary>Column widths adjust to fit the content of all cells, excluding headers.</summary>
        AllCellsExceptHeader,
        /// <summary>Column widths adjust to fit the header content.</summary>
        ColumnHeader,
        /// <summary>Column widths adjust to fit the content of displayed cells, including headers.</summary>
        DisplayedCells,
        /// <summary>Column widths adjust to fit the content of displayed cells, excluding headers.</summary>
        DisplayedCellsExceptHeader,
        /// <summary>Columns share the available width equally.</summary>
        Fill
    }

    /// <summary>
    /// Specifies how the height of the column header row is adjusted.
    /// </summary>
    public enum DataGridViewColumnHeadersHeightSizeMode
    {
        /// <summary>Users can resize the column header row.</summary>
        EnableResizing,
        /// <summary>Users cannot resize the column header row.</summary>
        DisableResizing,
        /// <summary>The column header row height adjusts automatically to fit its contents.</summary>
        AutoSize
    }

    /// <summary>
    /// Specifies how a DataGridView column sorts data.
    /// </summary>
    public enum DataGridViewColumnSortMode
    {
        /// <summary>The column can be sorted automatically.</summary>
        Automatic,
        /// <summary>The column cannot be sorted.</summary>
        NotSortable,
        /// <summary>The column is sorted programmatically.</summary>
        Programmatic
    }

    /// <summary>Specifies how content is aligned within a DataGridView cell.</summary>
    public enum DataGridViewContentAlignment
    {
        /// <summary>Not set.</summary>
        NotSet = 0,
        /// <summary>Top left.</summary>
        TopLeft = 1,
        /// <summary>Top center.</summary>
        TopCenter = 2,
        /// <summary>Top right.</summary>
        TopRight = 4,
        /// <summary>Middle left.</summary>
        MiddleLeft = 16,
        /// <summary>Middle center.</summary>
        MiddleCenter = 32,
        /// <summary>Middle right.</summary>
        MiddleRight = 64,
        /// <summary>Bottom left.</summary>
        BottomLeft = 256,
        /// <summary>Bottom center.</summary>
        BottomCenter = 512,
        /// <summary>Bottom right.</summary>
        BottomRight = 1024
    }

    /// <summary>
    /// Specifies a true, false, or indeterminate value for DataGridView properties.
    /// </summary>
    public enum DataGridViewTriState
    {
        /// <summary>Property is not set.</summary>
        NotSet,
        /// <summary>Property is true.</summary>
        True,
        /// <summary>Property is false.</summary>
        False
    }

    /// <summary>Provides data for the DataGridView.CellFormatting event.</summary>
    public class DataGridViewCellFormattingEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellFormattingEventArgs (int columnIndex, int rowIndex, object? value, Type? desiredType, DataGridViewCellStyle cellStyle)
            : base (columnIndex, rowIndex)
        {
            Value = value;
            DesiredType = desiredType;
            CellStyle = cellStyle;
        }

        /// <summary>Gets or sets the formatted value of the cell.</summary>
        public object? Value { get; set; }

        /// <summary>Gets the desired type for the formatted value.</summary>
        public Type? DesiredType { get; }

        /// <summary>Gets or sets the cell style.</summary>
        public DataGridViewCellStyle CellStyle { get; set; }

        /// <summary>Gets or sets whether the value has been formatted.</summary>
        public bool FormattingApplied { get; set; }
    }

    /// <summary>Provides data for the DataGridView.RowsAdded event.</summary>
    public class DataGridViewRowsAddedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowsAddedEventArgs (int rowIndex, int rowCount)
        {
            RowIndex = rowIndex;
            RowCount = rowCount;
        }

        /// <summary>Gets the index of the first added row.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the number of rows added.</summary>
        public int RowCount { get; }
    }

    /// <summary>Provides data for the DataGridView.RowsRemoved event.</summary>
    public class DataGridViewRowsRemovedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowsRemovedEventArgs (int rowIndex, int rowCount)
        {
            RowIndex = rowIndex;
            RowCount = rowCount;
        }

        /// <summary>Gets the index of the first removed row.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the number of rows removed.</summary>
        public int RowCount { get; }
    }

    /// <summary>Provides data for the DataGridView.UserDeletingRow event.</summary>
    public class DataGridViewRowCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowCancelEventArgs (DataGridViewRow row) { Row = row; }

        /// <summary>Gets the row being deleted.</summary>
        public DataGridViewRow Row { get; }
    }

    /// <summary>Provides data for the DataGridView.UserDeletedRow event.</summary>
    public class DataGridViewRowEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowEventArgs (DataGridViewRow row) { Row = row; }

        /// <summary>Gets the row that was deleted.</summary>
        public DataGridViewRow Row { get; }
    }

    /// <summary>Provides data for the DataGridView.DataError event.</summary>
    public class DataGridViewDataErrorEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewDataErrorEventArgs (Exception exception, int columnIndex, int rowIndex, DataGridViewDataErrorContexts context)
            : base (columnIndex, rowIndex)
        {
            Exception = exception;
            Context = context;
        }

        /// <summary>Gets the exception that caused the error.</summary>
        public Exception Exception { get; }

        /// <summary>Gets the error context.</summary>
        public DataGridViewDataErrorContexts Context { get; }

        /// <summary>Gets or sets whether the error was handled.</summary>
        public bool ThrowException { get; set; }
    }

    /// <summary>Specifies how column width is adjusted for a specific column.</summary>
    public enum DataGridViewAutoSizeColumnMode
    {
        /// <summary>Behavior inherits from the DataGridView.AutoSizeColumnsMode value.</summary>
        NotSet,
        /// <summary>Column width is not automatically adjusted.</summary>
        None,
        /// <summary>Column width adjusts to fit content of all cells.</summary>
        AllCells,
        /// <summary>Column width adjusts to fit content of all cells except the header.</summary>
        AllCellsExceptHeader,
        /// <summary>Column width adjusts to fit the header cell content only.</summary>
        ColumnHeader,
        /// <summary>Column width adjusts to fit content of displayed cells.</summary>
        DisplayedCells,
        /// <summary>Column width adjusts to fit content of displayed cells except the header.</summary>
        DisplayedCellsExceptHeader,
        /// <summary>Column width adjusts so all columns fill the control width.</summary>
        Fill
    }

    /// <summary>Specifies when cells in a DataGridView enter edit mode.</summary>
    public enum DataGridViewEditMode
    {
        /// <summary>Edit mode is entered by pressing F2 or double-clicking.</summary>
        EditOnEnter,
        /// <summary>Edit mode is entered when a key is pressed or F2 is pressed.</summary>
        EditOnKeystroke,
        /// <summary>Edit mode is entered when a key is pressed or F2 is pressed.</summary>
        EditOnKeystrokeOrF2,
        /// <summary>Edit mode is entered programmatically only.</summary>
        EditProgrammatically
    }

    /// <summary>Specifies how the row header width is sized.</summary>
    public enum DataGridViewRowHeadersWidthSizeMode
    {
        /// <summary>Users can resize the row header column.</summary>
        EnableResizing,
        /// <summary>Users cannot resize the row header column.</summary>
        DisableResizing,
        /// <summary>Row header column width adjusts automatically.</summary>
        AutoSizeToAllHeaders,
        /// <summary>Row header column width adjusts to fit displayed headers.</summary>
        AutoSizeToDisplayedHeaders,
        /// <summary>Row header column width adjusts to fit the first header.</summary>
        AutoSizeToFirstHeader
    }

    /// <summary>Provides data for DataGridView cell mouse events.</summary>
    public class DataGridViewCellMouseEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellMouseEventArgs (int columnIndex, int rowIndex, int localX, int localY, MouseEventArgs e)
            : base (columnIndex, rowIndex)
        {
            X = localX; Y = localY; Button = e.Button; Clicks = e.Clicks; Delta = e.Delta.Y;
        }

        /// <summary>Gets the x-coordinate of the mouse relative to the cell.</summary>
        public int X { get; }

        /// <summary>Gets the y-coordinate of the mouse relative to the cell.</summary>
        public int Y { get; }

        /// <summary>Gets which mouse button was pressed.</summary>
        public MouseButtons Button { get; }

        /// <summary>Gets the number of times the mouse button was pressed.</summary>
        public int Clicks { get; }

        /// <summary>Gets the scroll delta value (vertical).</summary>
        public int Delta { get; }
    }

    /// <summary>Provides data for the DataGridView.CellValidating event.</summary>
    public class DataGridViewCellValidatingEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellValidatingEventArgs (int columnIndex, int rowIndex, object? formattedValue)
            : base (columnIndex, rowIndex) { FormattedValue = formattedValue; }

        /// <summary>Gets the formatted cell value being validated.</summary>
        public object? FormattedValue { get; }

        /// <summary>Gets or sets whether validation should be cancelled.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>Provides data for the DataGridView.RowPrePaint event. Stub in Majorsilence.Forms.</summary>
    public class DataGridViewRowPrePaintEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowPrePaintEventArgs (int rowIndex) { RowIndex = rowIndex; }

        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }

        /// <summary>Gets or sets whether the default painting is skipped.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>Provides data for the DataGridView.RowPostPaint event. Stub in Majorsilence.Forms.</summary>
    public class DataGridViewRowPostPaintEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowPostPaintEventArgs (int rowIndex) { RowIndex = rowIndex; }

        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }
    }

    /// <summary>Provides data for the DataGridView.ColumnAdded and ColumnRemoved events.</summary>
    public class DataGridViewColumnEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewColumnEventArgs (DataGridViewColumn column) { Column = column; }

        /// <summary>Gets the column involved in the event.</summary>
        public DataGridViewColumn Column { get; }
    }

    /// <summary>Specifies the context in which a DataGridView data error occurred.</summary>
    [Flags]
    public enum DataGridViewDataErrorContexts
    {
        /// <summary>Data parsing error.</summary>
        Parsing = 1,
        /// <summary>Cell commit error.</summary>
        Commit = 2,
        /// <summary>Cell leaving error.</summary>
        LeaveControl = 4,
        /// <summary>Row dirty state needs evaluation.</summary>
        RowDirtyStateNeeded = 8,
        /// <summary>Bubble up error.</summary>
        Formatting = 16,
        /// <summary>Display error.</summary>
        Display = 32,
        /// <summary>Preferred size error.</summary>
        PreferredSize = 64,
        /// <summary>Scroll error.</summary>
        Scroll = 128,
        /// <summary>Current cell change error.</summary>
        CurrentCellChange = 256,
        /// <summary>Error while cleaning new row.</summary>
        CleanupExceptionHandling = 512,
        /// <summary>Error while initializing a new row.</summary>
        InitialValueRestoration = 1024
    }

    /// <summary>Specifies the border style of the cells in a DataGridView.</summary>
    public enum DataGridViewCellBorderStyle
    {
        /// <summary>No border.</summary>
        None = 0,
        /// <summary>A single line border.</summary>
        Single = 1,
        /// <summary>A sunken 3D border.</summary>
        Sunken = 2,
        /// <summary>A raised 3D border.</summary>
        Raised = 3,
        /// <summary>A single horizontal border only.</summary>
        SingleHorizontal = 4,
        /// <summary>A sunken horizontal 3D border only.</summary>
        SunkenHorizontal = 5,
        /// <summary>A raised horizontal 3D border only.</summary>
        RaisedHorizontal = 6,
        /// <summary>A single vertical border only.</summary>
        SingleVertical = 7,
        /// <summary>A sunken vertical 3D border only.</summary>
        SunkenVertical = 8,
        /// <summary>A raised vertical 3D border only.</summary>
        RaisedVertical = 9,
        /// <summary>Custom border.</summary>
        Custom = 10
    }

#pragma warning disable CA1711
    /// <summary>Represents the method that handles DataGridView cell events.</summary>
    public delegate void DataGridViewCellEventHandler (object sender, DataGridViewCellEventArgs e);

    /// <summary>Represents the method that handles DataGridView cell mouse events.</summary>
    public delegate void DataGridViewCellMouseEventHandler (object sender, DataGridViewCellMouseEventArgs e);

    /// <summary>Represents the method that handles DataGridView cell formatting events.</summary>
    public delegate void DataGridViewCellFormattingEventHandler (object sender, DataGridViewCellFormattingEventArgs e);

    /// <summary>Represents the method that handles DataGridView row events.</summary>
    public delegate void DataGridViewRowEventHandler (object sender, DataGridViewRowEventArgs e);

    /// <summary>Represents the method that handles DataGridView row cancel events.</summary>
    public delegate void DataGridViewRowCancelEventHandler (object sender, DataGridViewRowCancelEventArgs e);

    /// <summary>Represents the method that handles DataGridView rows-added events.</summary>
    public delegate void DataGridViewRowsAddedEventHandler (object sender, DataGridViewRowsAddedEventArgs e);

    /// <summary>Represents the method that handles DataGridView rows-removed events.</summary>
    public delegate void DataGridViewRowsRemovedEventHandler (object sender, DataGridViewRowsRemovedEventArgs e);

    /// <summary>Represents the method that handles DataGridView data-error events.</summary>
    public delegate void DataGridViewDataErrorEventHandler (object sender, DataGridViewDataErrorEventArgs e);

    /// <summary>Represents the method that handles DataGridView cell-validating events.</summary>
    public delegate void DataGridViewCellValidatingEventHandler (object sender, DataGridViewCellValidatingEventArgs e);

    /// <summary>Represents the method that handles DataGridView cell-value-changed events.</summary>
    public delegate void DataGridViewCellValueChangedEventHandler (object sender, DataGridViewCellEventArgs e);

    /// <summary>Represents the method that handles DataGridView column events.</summary>
    public delegate void DataGridViewColumnEventHandler (object sender, DataGridViewColumnEventArgs e);

    /// <summary>Represents the method that handles DataGridView cell painting events.</summary>
    public delegate void DataGridViewCellPaintingEventHandler (object sender, DataGridViewCellPaintingEventArgs e);

    /// <summary>Represents the method that handles DataGridView cell parsing events.</summary>
    public delegate void DataGridViewCellParsingEventHandler (object sender, DataGridViewCellParsingEventArgs e);

    /// <summary>Represents the method that handles DataGridView row state changed events.</summary>
    public delegate void DataGridViewRowStateChangedEventHandler (object sender, DataGridViewRowStateChangedEventArgs e);

    /// <summary>Provides data for the DataGridView.ColumnWidthChanged event.</summary>
    public delegate void DataGridViewColumnWidthChangedEventHandler (object sender, DataGridViewColumnEventArgs e);
#pragma warning restore CA1711

    /// <summary>Provides data for the DataGridView.SortCompare event.</summary>
    public class DataGridViewSortCompareEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewSortCompareEventArgs (DataGridViewColumn column, object? cellValue1, object? cellValue2, int rowIndex1, int rowIndex2)
        {
            Column = column;
            CellValue1 = cellValue1;
            CellValue2 = cellValue2;
            RowIndex1 = rowIndex1;
            RowIndex2 = rowIndex2;
        }

        /// <summary>Gets the column being sorted.</summary>
        public DataGridViewColumn Column { get; }

        /// <summary>Gets the first value being compared.</summary>
        public object? CellValue1 { get; }

        /// <summary>Gets the second value being compared.</summary>
        public object? CellValue2 { get; }

        /// <summary>Gets the row index for the first value.</summary>
        public int RowIndex1 { get; }

        /// <summary>Gets the row index for the second value.</summary>
        public int RowIndex2 { get; }

        /// <summary>Gets or sets the comparison result (-1, 0, or 1).</summary>
        public int SortResult { get; set; }
    }

    /// <summary>Provides data for the DataGridView.CellValueNeeded and CellValuePushed events.</summary>
    public class DataGridViewCellValueEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellValueEventArgs (int columnIndex, int rowIndex) { ColumnIndex = columnIndex; RowIndex = rowIndex; }

        /// <summary>Gets the column index of the cell.</summary>
        public int ColumnIndex { get; }

        /// <summary>Gets the row index of the cell.</summary>
        public int RowIndex { get; }

        /// <summary>Gets or sets the value for the cell.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for the DataGridView.CellPainting event.</summary>
    public class DataGridViewCellPaintingEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellPaintingEventArgs (int columnIndex, int rowIndex) : base (columnIndex, rowIndex) { }

        /// <summary>Gets or sets whether the painting should be handled by the caller.</summary>
        public bool Handled { get; set; }

        /// <summary>Gets the clip bounds for this cell.</summary>
        public Rectangle ClipBounds { get; set; }

        /// <summary>Gets the bounds of the cell being painted.</summary>
        public Rectangle CellBounds { get; set; }

        /// <summary>Gets the value of the cell.</summary>
        public object? Value { get; set; }

        /// <summary>Gets the formatted value of the cell.</summary>
        public object? FormattedValue { get; set; }

        /// <summary>Gets the error text for the cell.</summary>
        public string ErrorText { get; set; } = string.Empty;

        /// <summary>Gets the cell style.</summary>
        public DataGridViewCellStyle? CellStyle { get; set; }

        /// <summary>Gets the paint parts.</summary>
        public DataGridViewPaintParts PaintParts { get; set; } = DataGridViewPaintParts.All;
    }

    /// <summary>Provides data for the DataGridView.CellParsing event.</summary>
    public class DataGridViewCellParsingEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellParsingEventArgs (int columnIndex, int rowIndex, object? value, System.Type desiredType, DataGridViewCellStyle inheritedCellStyle)
            : base (columnIndex, rowIndex)
        {
            Value = value;
            DesiredType = desiredType;
            InheritedCellStyle = inheritedCellStyle;
        }

        /// <summary>Gets or sets the new value for the cell.</summary>
        public object? Value { get; set; }

        /// <summary>Gets the desired type of the value.</summary>
        public System.Type DesiredType { get; }

        /// <summary>Gets the inherited cell style.</summary>
        public DataGridViewCellStyle InheritedCellStyle { get; }

        /// <summary>Gets or sets whether the parsing was handled.</summary>
        public bool ParsingApplied { get; set; }
    }

    /// <summary>Provides data for the DataGridView.RowStateChanged event.</summary>
    public class DataGridViewRowStateChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowStateChangedEventArgs (DataGridViewRow dataGridViewRow, DataGridViewElementStates stateChanged)
        {
            Row = dataGridViewRow;
            StateChanged = stateChanged;
        }

        /// <summary>Gets the row whose state changed.</summary>
        public DataGridViewRow Row { get; }

        /// <summary>Gets the state that changed.</summary>
        public DataGridViewElementStates StateChanged { get; }
    }

    /// <summary>Provides data for the DataGridView.CellStateChanged event.</summary>
    public class DataGridViewCellStateChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellStateChangedEventArgs (DataGridViewCell cell, DataGridViewElementStates stateChanged)
        {
            Cell = cell;
            StateChanged = stateChanged;
        }

        /// <summary>Gets the cell whose state changed.</summary>
        public DataGridViewCell Cell { get; }

        /// <summary>Gets the state that changed.</summary>
        public DataGridViewElementStates StateChanged { get; }
    }

    /// <summary>Provides data for the DataGridView.EditingControlShowing event.</summary>
    public class DataGridViewEditingControlShowingEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewEditingControlShowingEventArgs (Control? control, DataGridViewCellStyle cellStyle)
        {
            Control = control;
            CellStyle = cellStyle;
        }

        /// <summary>Gets the editing control for the current cell.</summary>
        public Control? Control { get; }

        /// <summary>Gets the cell style of the current cell.</summary>
        public DataGridViewCellStyle CellStyle { get; }
    }

    /// <summary>Provides data for the DataGridView.AutoSizeColumnModeChanged event.</summary>
    public class DataGridViewAutoSizeColumnModeEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewAutoSizeColumnModeEventArgs (DataGridViewColumn column, DataGridViewAutoSizeColumnMode previousMode)
        {
            Column = column;
            PreviousMode = previousMode;
        }

        /// <summary>Gets the column.</summary>
        public DataGridViewColumn Column { get; }

        /// <summary>Gets the previous auto-size mode.</summary>
        public DataGridViewAutoSizeColumnMode PreviousMode { get; }
    }

    /// <summary>Specifies what parts of a DataGridView cell are to be painted.</summary>
    [Flags]
    public enum DataGridViewPaintParts
    {
        /// <summary>None of the cell is to be painted.</summary>
        None = 0,
        /// <summary>The background of the cell is to be painted.</summary>
        Background = 1,
        /// <summary>The border of the cell is to be painted.</summary>
        Border = 2,
        /// <summary>The focus rectangle is to be painted.</summary>
        Focus = 4,
        /// <summary>The content background is to be painted.</summary>
        ContentBackground = 8,
        /// <summary>The foreground of the content is to be painted.</summary>
        ContentForeground = 16,
        /// <summary>The error icon is to be painted.</summary>
        ErrorIcon = 32,
        /// <summary>The selection background is to be painted.</summary>
        SelectionBackground = 64,
        /// <summary>All parts of the cell are to be painted.</summary>
        All = Background | Border | Focus | ContentBackground | ContentForeground | ErrorIcon | SelectionBackground
    }

    /// <summary>Specifies the state of a DataGridView element.</summary>
    [Flags]
    public enum DataGridViewElementStates
    {
        /// <summary>No state is specified.</summary>
        None = 0,
        /// <summary>The element is displayed.</summary>
        Displayed = 1,
        /// <summary>The element cannot be scrolled through the UI.</summary>
        Frozen = 2,
        /// <summary>The element will not accept user input.</summary>
        ReadOnly = 4,
        /// <summary>The element can be resized through the UI.</summary>
        Resizable = 8,
        /// <summary>The element has been resized.</summary>
        ResizableSet = 16,
        /// <summary>The element has been selected.</summary>
        Selected = 32,
        /// <summary>The element is visible.</summary>
        Visible = 64
    }

    /// <summary>Represents a text-box cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public class DataGridViewTextBoxCell : DataGridViewCell { }

    /// <summary>Represents a check-box cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public class DataGridViewCheckBoxCell : DataGridViewCell
    {
        /// <summary>Gets or sets whether three-state toggling is supported.</summary>
        public bool ThreeState { get; set; }
    }

    /// <summary>Represents a combo-box cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public class DataGridViewComboBoxCell : DataGridViewCell
    {
        /// <summary>Gets the list of items for this cell's combo box.</summary>
        public System.Collections.ArrayList Items { get; } = new System.Collections.ArrayList ();

        /// <summary>Gets or sets the display style for the cell.</summary>
        public DataGridViewComboBoxDisplayStyle DisplayStyle { get; set; } = DataGridViewComboBoxDisplayStyle.DropDownButton;

        /// <summary>Gets or sets the data source for combo-box items.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the display member for the combo-box items.</summary>
        public string DisplayMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the value member for the combo-box items.</summary>
        public string ValueMember { get; set; } = string.Empty;
    }

    /// <summary>Represents an image cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public class DataGridViewImageCell : DataGridViewCell
    {
        /// <summary>Gets or sets the image layout for this cell.</summary>
        public DataGridViewImageCellLayout ImageLayout { get; set; } = DataGridViewImageCellLayout.Normal;
    }

    /// <summary>Specifies how an image is displayed in a DataGridViewImageCell.</summary>
    public enum DataGridViewImageCellLayout
    {
        /// <summary>Not specified.</summary>
        NotSet,
        /// <summary>The image is displayed at its normal size.</summary>
        Normal,
        /// <summary>The image is stretched to fill the cell.</summary>
        Stretch,
        /// <summary>The image is zoomed to fill the cell preserving aspect ratio.</summary>
        Zoom
    }

    /// <summary>Specifies the display style for a DataGridViewComboBoxCell.</summary>
    public enum DataGridViewComboBoxDisplayStyle
    {
        /// <summary>The combo box appears as a drop-down button.</summary>
        DropDownButton,
        /// <summary>The combo box appears as a combo box.</summary>
        ComboBox,
        /// <summary>No combo box UI is shown.</summary>
        Nothing
    }
}
