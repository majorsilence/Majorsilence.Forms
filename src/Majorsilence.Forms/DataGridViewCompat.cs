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
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }
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
        Row = 4,
        /// <summary>The grid's own default style.</summary>
        DataGridView = 8,
        /// <summary>The column headers' style.</summary>
        ColumnHeaders = 16,
        /// <summary>The row headers' style.</summary>
        RowHeaders = 32,
        /// <summary>The style shared by every row.</summary>
        Rows = 64,
        /// <summary>The style applied to alternating rows.</summary>
        AlternatingRows = 128
    }

    /// <summary>
    /// Represents a text box column in a DataGridView.
    /// </summary>
    public class DataGridViewTextBoxColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the maximum number of characters allowed in the column's cells. Stub.</summary>
        public int MaxInputLength { get; set; } = 32767;

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewColumn target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewTextBoxColumn column)
                column.MaxInputLength = MaxInputLength;
        }
    }

    /// <summary>
    /// Represents an image column in a DataGridView. Stub in Majorsilence.Forms (images are not rendered).
    /// </summary>
    public partial class DataGridViewImageColumn : DataGridViewColumn
    {
        /// <summary>Initializes an image column whose cells hold images.</summary>
        public DataGridViewImageColumn () { }

        /// <summary>Initializes an image column, choosing whether its cells hold icons or images.</summary>
        /// <remarks>WinForms' second constructor. <see cref="ValuesAreIcons"/> keeps the choice, since the
        /// two are drawn differently -- an icon carries its own size and alpha, an image is scaled to the
        /// cell.</remarks>
        public DataGridViewImageColumn (bool valuesAreIcons) => ValuesAreIcons = valuesAreIcons;

        /// <summary>Gets or sets the image displayed in the column. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Image? Image { get; set; }

        /// <summary>Gets or sets the description of the image. Stub in Majorsilence.Forms.</summary>
        public string Description { get; set; } = string.Empty;

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewColumn target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewImageColumn column) {
                column.Image = Image;
                column.Description = Description;
            }
        }
    }

    /// <summary>
    /// Represents a link column in a DataGridView. Stub in Majorsilence.Forms.
    /// </summary>
    public partial class DataGridViewLinkColumn : DataGridViewColumn
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

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewColumn target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewLinkColumn column) {
                column.Text = Text;
                column.ActiveLinkColor = ActiveLinkColor;
                column.LinkColor = LinkColor;
                column.VisitedLinkColor = VisitedLinkColor;
                column.TrackVisitedState = TrackVisitedState;
                column.UseColumnTextForLinkValue = UseColumnTextForLinkValue;
            }
        }
    }

    /// <summary>
    /// Represents a check box column in a DataGridView.
    /// </summary>
    public partial class DataGridViewCheckBoxColumn : DataGridViewColumn { }

    /// <summary>
    /// Represents a button column in a DataGridView.
    /// </summary>
    public partial class DataGridViewButtonColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the button text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the column HeaderText is used as button text.</summary>
        public bool UseColumnTextForButtonValue { get; set; }

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewColumn target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewButtonColumn column) {
                column.Text = Text;
                column.UseColumnTextForButtonValue = UseColumnTextForButtonValue;
            }
        }
    }

    /// <summary>
    /// Represents a combo box column in a DataGridView.
    /// </summary>
    public partial class DataGridViewComboBoxColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the data source for the combo box items.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the property used for display text.</summary>
        public string DisplayMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the property used as the underlying value.</summary>
        public string ValueMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the flat style (stub).</summary>
        public FlatStyle FlatStyle { get; set; }

        /// <summary>Gets the collection of items available in the combo box, for statically populated (non-DataSource-bound) columns.</summary>
        public System.Collections.Generic.List<object> Items { get; } = new System.Collections.Generic.List<object> ();

        /// <summary>Gets or sets the width of the drop-down list. Stub in Majorsilence.Forms.</summary>
        public int DropDownWidth { get; set; }

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewColumn target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewComboBoxColumn column) {
                column.DataSource = DataSource;
                column.DisplayMember = DisplayMember;
                column.ValueMember = ValueMember;
                column.FlatStyle = FlatStyle;
                column.DropDownWidth = DropDownWidth;
                column.Items.AddRange (Items);
            }
        }
    }

    /// <summary>
    /// Specifies how the widths of columns are adjusted when the DataGridView is resized.
    /// </summary>
    /// <summary>Specifies how row heights are automatically adjusted in a DataGridView.</summary>
    public enum DataGridViewAutoSizeRowsMode
    {
        /// <summary>Row heights are not automatically adjusted.</summary>
        None = 0,
        /// <summary>Row heights adjust to fit the content of all cells, including headers.</summary>
        AllCells = 7,
        /// <summary>Row heights adjust to fit the content of all cells, excluding headers.</summary>
        AllCellsExceptHeaders = 6,
        /// <summary>Row heights adjust to fit the content of displayed cells, including headers.</summary>
        DisplayedCells = 11,
        /// <summary>Row heights adjust to fit the content of displayed cells, excluding headers.</summary>
        DisplayedCellsExceptHeaders = 10,
        /// <summary>Row heights adjust to fit the header cells of all rows.</summary>
        AllHeaders = 5,
        /// <summary>Row heights adjust to fit the header cells of the displayed rows.</summary>
        DisplayedHeaders = 9,
    }

    /// <summary>Specifies how content is copied to the clipboard from a DataGridView.</summary>
    public enum DataGridViewClipboardCopyMode
    {
        /// <summary>Clipboard copy is disabled.</summary>
        Disable = 0,
        /// <summary>Text values of selected cells are copied.</summary>
        EnableAlwaysIncludeHeaderText = 3,
        /// <summary>Text values of selected cells are copied without headers.</summary>
        EnableWithoutHeaderText = 2,
        /// <summary>Text values of selected cells are copied; headers are included if row/column headers are selected.</summary>
        EnableWithAutoHeaderText = 1,
    }

    /// <summary>Specifies how column widths are automatically sized.</summary>
    public enum DataGridViewAutoSizeColumnsMode
    {
        /// <summary>Column widths are not automatically adjusted.</summary>
        None = 1,
        /// <summary>Column widths adjust to fit the content of all cells, including headers.</summary>
        AllCells = 6,
        /// <summary>Column widths adjust to fit the content of all cells, excluding headers.</summary>
        AllCellsExceptHeader = 4,
        /// <summary>Column widths adjust to fit the header content.</summary>
        ColumnHeader = 2,
        /// <summary>Column widths adjust to fit the content of displayed cells, including headers.</summary>
        DisplayedCells = 10,
        /// <summary>Column widths adjust to fit the content of displayed cells, excluding headers.</summary>
        DisplayedCellsExceptHeader = 8,
        /// <summary>Columns share the available width equally.</summary>
        Fill = 16,
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
        Automatic = 1,
        /// <summary>The column cannot be sorted.</summary>
        NotSortable = 0,
        /// <summary>The column is sorted programmatically.</summary>
        Programmatic = 2,
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

    /// <summary>Specifies how a row is auto-sized. Mirrors WinForms DataGridViewAutoSizeRowMode.</summary>
    public enum DataGridViewAutoSizeRowMode
    {
        /// <summary>Size to the header cell only.</summary>
        RowHeader = 1,
        /// <summary>Size to the displayed data cells.</summary>
        AllCellsExceptHeader = 2,
        /// <summary>Size to all cells including the header.</summary>
        AllCells = 3,
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

        /// <summary>Gets or sets whether the exception should be suppressed (no further DataError notification for this operation).</summary>
        public bool Cancel { get; set; }

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
        NotSet = 0,
        /// <summary>Column width is not automatically adjusted.</summary>
        None = 1,
        /// <summary>Column width adjusts to fit content of all cells.</summary>
        AllCells = 6,
        /// <summary>Column width adjusts to fit content of all cells except the header.</summary>
        AllCellsExceptHeader = 4,
        /// <summary>Column width adjusts to fit the header cell content only.</summary>
        ColumnHeader = 2,
        /// <summary>Column width adjusts to fit content of displayed cells.</summary>
        DisplayedCells = 10,
        /// <summary>Column width adjusts to fit content of displayed cells except the header.</summary>
        DisplayedCellsExceptHeader = 8,
        /// <summary>Column width adjusts so all columns fill the control width.</summary>
        Fill = 16,
    }

    /// <summary>Specifies when cells in a DataGridView enter edit mode.</summary>
    public enum DataGridViewEditMode
    {
        /// <summary>Edit mode is entered by pressing F2 or double-clicking.</summary>
        EditOnEnter = 0,
        /// <summary>Edit mode is entered when a key is pressed or F2 is pressed.</summary>
        EditOnKeystroke = 1,
        /// <summary>Edit mode is entered when a key is pressed or F2 is pressed.</summary>
        EditOnKeystrokeOrF2 = 2,
        /// <summary>Editing begins when F2 is pressed on the current cell.</summary>
        EditOnF2 = 3,
        /// <summary>Edit mode is entered programmatically only.</summary>
        EditProgrammatically = 4,
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
            X = localX; Y = localY; Button = e.Button; Clicks = e.Clicks; Delta = e.Delta;
        }

        /// <summary>Gets the x-coordinate of the mouse relative to the cell.</summary>
        public int X { get; }

        /// <summary>Gets the y-coordinate of the mouse relative to the cell.</summary>
        public int Y { get; }

        /// <summary>Gets the position of the mouse relative to the cell.</summary>
        public System.Drawing.Point Location => new System.Drawing.Point (X, Y);

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

    /// <summary>
    /// Base for the row paint event args. Carries the paint surface and row geometry, and exposes the
    /// grid's own row painting (cells / row header) so a handler can compose its custom drawing with the
    /// default rendering. The <c>Paint*</c> methods call back into the renderer that raised the event.
    /// </summary>
    public abstract class DataGridViewRowPaintBaseEventArgs : EventArgs
    {
        internal Action<Rectangle, DataGridViewPaintParts>? PaintCellsCallback;
        internal Action<bool>? PaintHeaderCallback;

        /// <summary>Initializes a new instance for the specified row.</summary>
        protected DataGridViewRowPaintBaseEventArgs (int rowIndex) { RowIndex = rowIndex; }

        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the surface the row is being painted on, or null when the event was raised outside a paint pass.</summary>
        public Majorsilence.Forms.Drawing.Graphics? Graphics { get; internal set; }

        /// <summary>Gets the area of the grid that needs repainting, in device pixels.</summary>
        public Rectangle ClipBounds { get; set; }

        /// <summary>Gets the bounds of the row being painted, in device pixels.</summary>
        public Rectangle RowBounds { get; internal set; }

        /// <summary>Gets the state of the row being painted.</summary>
        public DataGridViewElementStates State { get; internal set; }

        /// <summary>Gets the error text for the row.</summary>
        public string ErrorText { get; internal set; } = string.Empty;

        /// <summary>Gets the style applied to the row after the grid/row style cascade.</summary>
        public DataGridViewCellStyle? InheritedRowStyle { get; internal set; }

        /// <summary>Gets whether this row is the first one currently displayed.</summary>
        public bool IsFirstDisplayedRow { get; internal set; }

        /// <summary>Gets whether this row is the last visible row of the grid.</summary>
        public bool IsLastVisibleRow { get; internal set; }

        /// <summary>Paints the row's cells (the grid's default cell rendering) for the given parts.</summary>
        public void PaintCells (Rectangle clipBounds, DataGridViewPaintParts paintParts)
            => PaintCellsCallback?.Invoke (clipBounds, paintParts);

        /// <summary>Paints only the background of the row's cells.</summary>
        public void PaintCellsBackground (Rectangle clipBounds, bool cellsPaintSelectionBackground)
            => PaintCells (clipBounds, cellsPaintSelectionBackground
                ? DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border
                : DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

        /// <summary>Paints only the content (text/glyphs) of the row's cells.</summary>
        public void PaintCellsContent (Rectangle clipBounds)
            => PaintCells (clipBounds, DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.ContentBackground);

        /// <summary>Paints the row's header cell.</summary>
        public void PaintHeader (bool paintSelectionBackground) => PaintHeaderCallback?.Invoke (paintSelectionBackground);

        /// <summary>Paints the row's header cell for the given parts.</summary>
        public void PaintHeader (DataGridViewPaintParts paintParts)
            => PaintHeader (paintParts.HasFlag (DataGridViewPaintParts.SelectionBackground));
    }

    /// <summary>
    /// Provides data for the DataGridView.RowPrePaint event, raised by the renderer before a row's
    /// background, header and cells are drawn. Set <see cref="Handled"/> to suppress the grid's own
    /// painting of the row.
    /// </summary>
    public partial class DataGridViewRowPrePaintEventArgs : DataGridViewRowPaintBaseEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowPrePaintEventArgs (int rowIndex) : base (rowIndex) { }

        /// <summary>Gets or sets whether the default painting is skipped.</summary>
        public bool Handled { get; set; }

        /// <summary>Gets or sets the parts of the row the grid should paint. Honored by the renderer when <see cref="Handled"/> is false.</summary>
        public DataGridViewPaintParts PaintParts { get; set; } = DataGridViewPaintParts.All;
    }

    /// <summary>
    /// Provides data for the DataGridView.RowPostPaint event, raised by the renderer after a row has
    /// been drawn.
    /// </summary>
    public partial class DataGridViewRowPostPaintEventArgs : DataGridViewRowPaintBaseEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewRowPostPaintEventArgs (int rowIndex) : base (rowIndex) { }
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
        Parsing = 0x100,
        /// <summary>Cell commit error.</summary>
        Commit = 0x200,
        /// <summary>Cell leaving error.</summary>
        LeaveControl = 0x800,
        /// <summary>Row dirty state needs evaluation.</summary>
        RowDeletion = 8,
        /// <summary>Bubble up error.</summary>
        Formatting = 1,
        /// <summary>Display error.</summary>
        Display = 2,
        /// <summary>Preferred size error.</summary>
        PreferredSize = 4,
        /// <summary>Scroll error.</summary>
        Scroll = 0x2000,
        /// <summary>Current cell change error.</summary>
        CurrentCellChange = 0x1000,
        /// <summary>Error while initializing a new row.</summary>
        InitialValueRestoration = 0x400,
        /// <summary>The error happened while building content for the clipboard.</summary>
        ClipboardContent = 0x4000,
    }

    /// <summary>Specifies the border style of the cells in a DataGridView.</summary>
    public enum DataGridViewCellBorderStyle
    {
        /// <summary>No border.</summary>
        None = 4,
        /// <summary>A single line border.</summary>
        Single = 1,
        /// <summary>A sunken 3D border.</summary>
        Sunken = 3,
        /// <summary>A raised 3D border.</summary>
        Raised = 2,
        /// <summary>A single horizontal border only.</summary>
        SingleHorizontal = 8,
        /// <summary>A sunken horizontal 3D border only.</summary>
        SunkenHorizontal = 10,
        /// <summary>A raised horizontal 3D border only.</summary>
        RaisedHorizontal = 9,
        /// <summary>A single vertical border only.</summary>
        SingleVertical = 5,
        /// <summary>A sunken vertical 3D border only.</summary>
        SunkenVertical = 7,
        /// <summary>A raised vertical 3D border only.</summary>
        RaisedVertical = 6,
        /// <summary>Custom border.</summary>
        Custom = 0,
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
    public partial class DataGridViewCellPaintingEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellPaintingEventArgs (int columnIndex, int rowIndex) : base (columnIndex, rowIndex) { }

        /// <summary>Gets the graphics surface to paint on. Set by the renderer when the event is raised during a paint pass.</summary>
        public Majorsilence.Forms.Drawing.Graphics? Graphics { get; set; }

        // Installed by the renderer that raises CellPainting so the Paint* methods below run the grid's
        // real default cell painting for the requested parts.
        internal Action<Rectangle, DataGridViewPaintParts>? PaintCallback;

        /// <summary>Paints the cell's default content (value/text/glyph) within the given bounds.</summary>
        public void PaintContent (Rectangle cellBounds)
            => PaintCallback?.Invoke (cellBounds, DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.ContentBackground);

        /// <summary>Paints the cell's background (and border) within the given bounds.</summary>
        public void PaintBackground (Rectangle cellBounds, bool cellsPaintSelectionBackground)
            => PaintCallback?.Invoke (cellBounds, cellsPaintSelectionBackground
                ? DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border
                : DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

        /// <summary>Paints the requested parts of the cell using the grid's default rendering.</summary>
        public void Paint (Rectangle clipBounds, DataGridViewPaintParts paintParts)
            => PaintCallback?.Invoke (CellBounds.IsEmpty ? clipBounds : CellBounds, paintParts);

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
        Focus = 32,
        /// <summary>The content background is to be painted.</summary>
        ContentBackground = 4,
        /// <summary>The foreground of the content is to be painted.</summary>
        ContentForeground = 8,
        /// <summary>The error icon is to be painted.</summary>
        ErrorIcon = 16,
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
    public partial class DataGridViewTextBoxCell : DataGridViewCell { }

    /// <summary>Represents a check-box cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public partial class DataGridViewCheckBoxCell : DataGridViewCell
    {
        /// <summary>Initializes a two-state check-box cell.</summary>
        public DataGridViewCheckBoxCell () { }

        /// <summary>Initializes a check-box cell, choosing two- or three-state toggling.</summary>
        /// <remarks>WinForms' second constructor. A derived cell that offers the choice in its own
        /// constructor chains to this one, so its absence blocked the whole type.</remarks>
        public DataGridViewCheckBoxCell (bool threeState) => ThreeState = threeState;

        /// <summary>Gets or sets whether three-state toggling is supported.</summary>
        public bool ThreeState { get; set; }

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewCell target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewCheckBoxCell cell)
                cell.ThreeState = ThreeState;
        }
    }

    /// <summary>Represents a combo-box cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public partial class DataGridViewComboBoxCell : DataGridViewCell
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

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewCell target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewComboBoxCell cell) {
                cell.DisplayStyle = DisplayStyle;
                cell.DataSource = DataSource;
                cell.DisplayMember = DisplayMember;
                cell.ValueMember = ValueMember;
                cell.Items.AddRange (Items);
            }
        }
    }

    /// <summary>Represents an image cell in a DataGridView. Stub in Majorsilence.Forms.</summary>
    public partial class DataGridViewImageCell : DataGridViewCell
    {
        /// <summary>Gets or sets the image layout for this cell.</summary>
        public DataGridViewImageCellLayout ImageLayout { get; set; } = DataGridViewImageCellLayout.Normal;

        /// <inheritdoc/>
        protected override void CopyStateTo (DataGridViewCell target)
        {
            base.CopyStateTo (target);

            if (target is DataGridViewImageCell cell)
                cell.ImageLayout = ImageLayout;
        }
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

    /// <summary>Specifies the border style of a single edge of a DataGridView cell.</summary>
    public enum DataGridViewAdvancedCellBorderStyle
    {
        /// <summary>The border is not set; the grid decides.</summary>
        NotSet = 0,
        /// <summary>No border is drawn on this edge.</summary>
        None = 1,
        /// <summary>A single-line border.</summary>
        Single = 2,
        /// <summary>A sunken (inset) border.</summary>
        Inset = 3,
        /// <summary>A doubled sunken border.</summary>
        InsetDouble = 4,
        /// <summary>A raised (outset) border.</summary>
        Outset = 5,
        /// <summary>A doubled raised border.</summary>
        OutsetDouble = 6,
        /// <summary>A partial raised border.</summary>
        OutsetPartial = 7
    }

    /// <summary>
    /// Describes the border style of each edge of a DataGridView cell. Set on
    /// <see cref="DataGridView.AdvancedCellBorderStyle"/> (or the header equivalents) to control which
    /// cell edges the renderer draws; the renderer reads all four edges every paint.
    /// </summary>
    public sealed class DataGridViewAdvancedBorderStyle : ICloneable
    {
        private DataGridViewAdvancedCellBorderStyle left = DataGridViewAdvancedCellBorderStyle.NotSet;
        private DataGridViewAdvancedCellBorderStyle right = DataGridViewAdvancedCellBorderStyle.NotSet;
        private DataGridViewAdvancedCellBorderStyle top = DataGridViewAdvancedCellBorderStyle.NotSet;
        private DataGridViewAdvancedCellBorderStyle bottom = DataGridViewAdvancedCellBorderStyle.NotSet;

        /// <summary>Initializes a new instance with every edge <see cref="DataGridViewAdvancedCellBorderStyle.NotSet"/>.</summary>
        public DataGridViewAdvancedBorderStyle () { }

        /// <summary>Raised when any edge changes. Used by the grid to repaint.</summary>
        internal event EventHandler? Changed;

        /// <summary>Gets or sets the style of the left edge.</summary>
        public DataGridViewAdvancedCellBorderStyle Left {
            get => left;
            set { if (left != value) { left = value; Changed?.Invoke (this, EventArgs.Empty); } }
        }

        /// <summary>Gets or sets the style of the right edge.</summary>
        public DataGridViewAdvancedCellBorderStyle Right {
            get => right;
            set { if (right != value) { right = value; Changed?.Invoke (this, EventArgs.Empty); } }
        }

        /// <summary>Gets or sets the style of the top edge.</summary>
        public DataGridViewAdvancedCellBorderStyle Top {
            get => top;
            set { if (top != value) { top = value; Changed?.Invoke (this, EventArgs.Empty); } }
        }

        /// <summary>Gets or sets the style of the bottom edge.</summary>
        public DataGridViewAdvancedCellBorderStyle Bottom {
            get => bottom;
            set { if (bottom != value) { bottom = value; Changed?.Invoke (this, EventArgs.Empty); } }
        }

        /// <summary>Sets every edge to the same style.</summary>
        public void All (DataGridViewAdvancedCellBorderStyle style)
        {
            left = right = top = bottom = style;
            Changed?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>Returns a copy of this border style.</summary>
        public DataGridViewAdvancedBorderStyle Clone ()
            => new DataGridViewAdvancedBorderStyle { left = left, right = right, top = top, bottom = bottom };

        object ICloneable.Clone () => Clone ();

        /// <inheritdoc/>
        public override bool Equals (object? obj)
            => obj is DataGridViewAdvancedBorderStyle other
                && other.left == left && other.right == right && other.top == top && other.bottom == bottom;

        /// <inheritdoc/>
        public override int GetHashCode () => HashCode.Combine (left, right, top, bottom);

        /// <inheritdoc/>
        public override string ToString ()
            => $"DataGridViewAdvancedBorderStyle {{ All={(left == right && right == top && top == bottom ? left.ToString () : "NotSet")}, Left={left}, Right={right}, Top={top}, Bottom={bottom} }}";
    }

    /// <summary>Specifies the display style for a DataGridViewComboBoxCell.</summary>
    public enum DataGridViewComboBoxDisplayStyle
    {
        /// <summary>The combo box appears as a drop-down button.</summary>
        DropDownButton = 1,
        /// <summary>The combo box appears as a combo box.</summary>
        ComboBox = 0,
        /// <summary>No combo box UI is shown.</summary>
        Nothing = 2,
    }
}
