using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms;
using static Majorsilence.Forms.DataGridView;

namespace Majorsilence.Forms
{
    public partial class DataGrid : Panel, System.ComponentModel.ISupportInitialize, IDisposable
    {
        private DataGridView _grid;

        public DataGridView Grid { get => _grid; }

        public new event EventHandler<EventArgs> Click;
        public new event EventHandler<EventArgs> DoubleClick;
        public event EventHandler<EventArgs> CurrentCellChanged;

        public DataGrid()
        {
            _grid = new DataGridView();

            //this.back
            TableStyles = new GridTableStylesCollection();
            captionLabel = new Label()
            {
                BackColor = SystemColors.Control,
                AutoSize = false,
                Font = new Font("Arial", 10, FontStyle.Bold),
            };
            captionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            captionLabel.Width = this.Width;
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grid.Width = this.Width;
            _grid.Height = this.Height - 20;
            _grid.Location = new Point(0, captionLabel.Height);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.ScrollBars = ScrollBars.Both;
            this.Controls.Add(captionLabel);
            this.Controls.Add(_grid);

            _grid.CurrentCellChanged += grid_CellChanged;
            _grid.DataSourceChanged += grid_DataSourceChanged;
            _grid.CellValidating += grid_CellValidating;
            _grid.CellFormatting += grid_CellFormatting;
            _grid.CellEndEdit += grid_CellEndEdit;
            _grid.Click += _grid_Click;
            _grid.DoubleClick += _grid_DoubleClick;

            TableStyles.CollectionChanged += grid_tableStyles_CollectionChanged;
        }

        /// <summary>
        /// Unselects all the cells in the DataGrid.
        /// </summary>
        public void UnselectAllCells()
        {
            _grid.ClearSelection();
        }

        private void _grid_Click(object sender, EventArgs e)
        {
            if (Click != null)
            {
                Click(sender, e);
            }
        }

        private void _grid_DoubleClick(object sender, EventArgs e)
        {
            if (DoubleClick != null)
            {
                DoubleClick(sender, e);
            }
        }

        private void grid_tableStyles_CollectionChanged(object sender, DataGridTableStyle e)
        {
            e.GridColumnStyles.CollectionChanged -= grid_columnsStyles_CollectionChanged;
            e.GridColumnStyles.CollectionChanged += grid_columnsStyles_CollectionChanged;
        }

        private void grid_columnsStyles_CollectionChanged(object sender, IDataGridColumnStyle e)
        {
            if (e.GetType() == typeof(Majorsilence.Forms.DataGridColumnStyle))
            {
                var col = e as Majorsilence.Forms.DataGridColumnStyle;
                if (!_grid.Columns.Contains(col))
                {
                    _grid.Columns.Add(col);
                }
            }
            else if (e.GetType() == typeof(DataGridViewComboBoxColumn) || e.GetType().IsSubclassOf(typeof(DataGridViewComboBoxColumn)))
            {
                var col = e as DataGridViewComboBoxColumn;
                if (!_grid.Columns.Contains(col))
                {
                    _grid.Columns.Add(col);
                }
            }
            else if (e.GetType() == typeof(DataGridViewColumn) || e.GetType().IsSubclassOf(typeof(DataGridViewColumn)))
            {
                var col = e as DataGridViewColumn;
                if (!_grid.Columns.Contains(col))
                {
                    _grid.Columns.Add(col);
                }
            }

            if (_grid.ColumnCount > 0)
            {
                _grid.AutoGenerateColumns = false;
            }
        }

        private void grid_CellValidating(object sender,
        DataGridViewCellValidatingEventArgs e)
        {
            int output;

            // Confirm that the cell is an integer.
            var dg = sender as DataGridView;
            var column = dg.Columns[e.ColumnIndex];
            if (column.ReadOnly) return;

            if (column.GetType() == typeof(Majorsilence.Forms.DataGridDecimalsColumn))
            {
                if (!decimal.TryParse(e.FormattedValue.ToString(), out _))
                {
                    _grid.Rows[e.RowIndex].ErrorText = $"{column.Name} must be numeric";
                    e.Cancel = true;
                    // Beep()
                }
            }
            else if (column.GetType() == typeof(Majorsilence.Forms.DataGridIntegersColumn))
            {
                if (!int.TryParse(e.FormattedValue.ToString(), out _))
                {
                    _grid.Rows[e.RowIndex].ErrorText = $"{column.Name} must be numeric";
                    e.Cancel = true;
                    // Beep()
                }
            }
            else if (column.ValueType == typeof(decimal) || column.ValueType == typeof(double) || column.ValueType == typeof(float))
            {
                if (!decimal.TryParse(e.FormattedValue.ToString(), out _))
                {
                    _grid.Rows[e.RowIndex].ErrorText = $"{column.Name} must be numeric";
                    e.Cancel = true;
                    // Beep()
                }
            }
            else if (
                (column.GetType() != typeof(DataGridViewCheckBoxColumn) && !column.GetType().IsSubclassOf(typeof(DataGridViewCheckBoxColumn)))
                && (column.ValueType == typeof(int) || column.ValueType == typeof(long) || column.ValueType == typeof(short))
                )
            {
                if (!int.TryParse(e.FormattedValue.ToString(), out output))
                {
                    _grid.Rows[e.RowIndex].ErrorText = $"{column.Name} must be numeric";
                    e.Cancel = true;
                    // Beep()
                }
            }
        }

        private void grid_CellEndEdit(object sender, DataGridViewCellEditEventArgs e)
        {
            // Clear the row error in case the user presses ESC.
            _grid.Rows[e.RowIndex].ErrorText = String.Empty;
        }

        private void grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;

            var dg = sender as DataGridView;
            var column = dg.Columns[e.ColumnIndex];
            if (column.ValueType == typeof(decimal) || column.ValueType == typeof(double) || column.ValueType == typeof(float))
            {
                column.DefaultCellStyle.Format = "N2";
            }
            else if (
                (column.GetType() != typeof(DataGridViewCheckBoxColumn) && !column.GetType().IsSubclassOf(typeof(DataGridViewCheckBoxColumn)))
                && (column.ValueType == typeof(int) || column.ValueType == typeof(long) || column.ValueType == typeof(short))
                )
            {
                column.DefaultCellStyle.Format = "N0";
            }
            else if (column.GetType() == typeof(Majorsilence.Forms.DataGridDecimalsColumn))
            {
                e.CellStyle.Format = "N2";
            }
            else if (column.GetType() == typeof(Majorsilence.Forms.DataGridIntegersColumn))
            {
                e.CellStyle.Format = "N0";
            }

        }

        private void grid_CellChanged(object sender, EventArgs e)
        {
            if (CurrentCellChanged != null)
            {
                CurrentCellChanged?.Invoke(sender, e);
            }
        }

        private void grid_DataSourceChanged(object sender, EventArgs e)
        {
            if (_grid.DataSource != null)
            {
                var tableStyle = TableStyles[0];

                foreach (DataGridViewColumn column in _grid.Columns)
                {
                    if (tableStyle != null && tableStyle.GridColumnStyles.Count > 0)
                    {
                        if (tableStyle.GridColumnStyles.Contains(column.DataPropertyName))
                        {
                            if (_grid.DataSource != null && column.IsDataBound == false)
                            {
                                column.Visible = false;
                            }
                        }
                        else
                        {
                            column.Visible = false;
                        }
                    }

                    if (column.ValueType == typeof(decimal) || column.ValueType == typeof(double) || column.ValueType == typeof(float))
                    {
                        column.DefaultCellStyle.Format = "N2";
                    }
                    else if (column.ValueType == typeof(int) || column.ValueType == typeof(long) || column.ValueType == typeof(short))
                    {
                        column.DefaultCellStyle.Format = "N0";
                    }
                    else if (column.GetType() == typeof(DataGridDecimalsColumn))
                    {
                        column.DefaultCellStyle.Format = "N2";
                    }
                    else if (column.GetType() == typeof(DataGridIntegersColumn))
                    {
                        column.DefaultCellStyle.Format = "N0";
                    }
                }

                var displayIndex = 0;
                if (tableStyle != null)
                {
                    foreach (IDataGridColumnStyle style in tableStyle.GridColumnStyles)
                    {
                        var column = _grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.DataPropertyName == style.MappingName);
                        if (column != null)
                        {
                            column.DisplayIndex = displayIndex;
                            displayIndex++;
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (TableStyles != null)
                {
                    TableStyles.CollectionChanged -= grid_tableStyles_CollectionChanged;
                }
                _grid.CurrentCellChanged -= grid_CellChanged;
                _grid.DataSourceChanged -= grid_DataSourceChanged;
                _grid.CellFormatting -= grid_CellFormatting;
                _grid.CellValidating -= grid_CellValidating;
                _grid.CellEndEdit -= grid_CellEndEdit;
                _grid.Click -= _grid_Click;
                _grid?.Dispose();
                captionLabel?.Dispose();
            }

            base.Dispose(disposing);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGridViewCell CurrentCell
        {
            get => _grid.CurrentCell;
            set
            {
                if (value != null && value.RowIndex >= 0 && value.RowIndex < _grid.Rows.Count && value.ColumnIndex >= 0 && value.ColumnIndex < _grid.Columns.Count)
                {
                    _grid.CurrentCell = value;
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ReadOnly { get => _grid.ReadOnly; set => _grid.ReadOnly = value; }

        private Label captionLabel;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool CaptionVisible
        {
            get => captionLabel.Visible;
            set
            {
                captionLabel.Visible = value;
                UpdateGridPosition();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color CaptionBackColor { get => captionLabel.BackColor; set => captionLabel.BackColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color CaptionForeColor { get => captionLabel.ForeColor; set => captionLabel.ForeColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string CaptionText
        {
            get { return captionLabel.Text; }
            set
            {
                captionLabel.Text = value;
                UpdateGridPosition();
            }
        }

        private void UpdateGridPosition()
        {
            if (captionLabel.Visible)
            {
                _grid.Location = new Point(0, captionLabel.Height);
                _grid.Height = this.Height - captionLabel.Height;
            }
            else
            {
                _grid.Location = new Point(0, 0);
                _grid.Height = this.Height;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            captionLabel.Width = this.Width;
            UpdateGridPosition();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public object DataSource { get => _grid.DataSource; set => _grid.DataSource = value; }
        public DataGridViewRow CurrentRow { get => _grid.CurrentRow; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int CurrentRowIndex { get => _grid.CurrentRow?.Index ?? 0; set => Select(value); }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color AlternatingBackColor { get => _grid.AlternatingRowsDefaultCellStyle.BackColor; 
            set {
                _grid.EnableHeadersVisualStyles = false;
                _grid.AlternatingRowsDefaultCellStyle.BackColor = value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color GridLineColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HeaderBackColor { get => _grid.ColumnHeadersDefaultCellStyle.BackColor; set => _grid.ColumnHeadersDefaultCellStyle.BackColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGridLineStyle GridLineStyle { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Font HeaderFont { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HeaderForeColor { get => _grid.ColumnHeadersDefaultCellStyle.ForeColor; set => _grid.ColumnHeadersDefaultCellStyle.ForeColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color LinkColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color ParentRowsBackColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color ParentRowsForeColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int PreferredColumnWidth { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SelectionBackColor { get => _grid.RowsDefaultCellStyle.SelectionBackColor; set => _grid.RowsDefaultCellStyle.SelectionBackColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SelectionForeColor { get => _grid.RowsDefaultCellStyle.SelectionForeColor; set => _grid.RowsDefaultCellStyle.SelectionForeColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BackgroundColor { get => _grid.BackgroundColor; set => _grid.BackgroundColor = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string DataMember { get => _grid.DataMember; set => _grid.DataMember = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public GridTableStylesCollection TableStyles { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool FlatMode { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AllowSorting { get; set; }

        public event EventHandler<NavigateEventArgs> Navigate;
        //protected virtual void Navigate(object sender, EventArgs e)
        //{
        //    // Check if there are any subscribers
        //    if (Navigate != null)
        //    {
        //        Navigate(this, e); // Raise the event
        //    }
        //}

        public DataGridViewCell Item(int rowIndex, int columnIndex)
        {

            int countVisible = 0;

            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                if (_grid.Columns[i].Visible)
                {
                    if (countVisible == columnIndex)
                    {
                        columnIndex = i;
                        break;
                    }
                    countVisible++;
                }
            }

            var cell = _grid.Rows[rowIndex].Cells[columnIndex];

            if (cell.GetType() == typeof(DataGridCell))
            {
                return cell as DataGridCell;
            }

            return cell;
        }

        public DataGridViewCell Item(int rowIndex, string columnName)
        {
            int columnIndex = -1;

            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                if (string.Equals( _grid.Columns[i].Name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnIndex = i;
                    break;
                }
            }

            if (columnIndex == -1) return null;

            var cell = _grid.Rows[rowIndex].Cells[columnIndex];

            if (cell.GetType() == typeof(DataGridCell))
            {
                return cell as DataGridCell;
            }

            return cell;
        }

        public DataGridCell Item(DataGridCell cell)
        {
            return cell;
        }

        public void SelectCell(int rowIndex, int columnIndex)
        {
            if (rowIndex >= 0 && rowIndex < _grid.Rows.Count &&
                columnIndex >= 0 && columnIndex < _grid.Columns.Count)
            {
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells[columnIndex];
            }
        }

        public void SendToRow(int row)
        {
            if (_grid.DataSource != null)
            {
                this.GridVScrolled(this, new ScrollEventArgs(ScrollEventType.LargeIncrement, row));
            }
        }

        public bool IsSelected(int rowIndex)
        {
            return _grid.Rows[rowIndex].Selected;
        }

        public void Select(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _grid.Rows.Count)
            {
                try
                {
                    _grid.CurrentCell = _grid.Rows[rowIndex].Cells[0];
                }
                catch(InvalidOperationException) {
                    _grid.Rows[rowIndex].Selected = true;
                }
              
                _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            }
        }

        public void UnSelect(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _grid.Rows.Count)
            {
                try
                {
                    _grid.CurrentCell = _grid.Rows[rowIndex].Cells[0];
                }
                catch (InvalidOperationException) {
                    _grid.Rows[rowIndex].Selected = true;
                }

                               
                _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            }
        }

        protected virtual void GridVScrolled(object sender, ScrollEventArgs se)
        {
            ScrollToRow(_grid, se.NewValue);
        }

        private void ScrollToRow(DataGridView dataGridView, int rowIndex)
        {
            // Ensure the row index is within the valid range
            if (rowIndex >= 0 && rowIndex < dataGridView.Rows.Count)
            {
                // Set the first displayed scrolling row index to the specified row index
                dataGridView.FirstDisplayedScrollingRowIndex = rowIndex;
            }
            else
            {
                // Optionally, handle the case where the row index is out of range
                MessageBox.Show("Row index is out of range.");
            }
        }

        public Rectangle GetCellBounds(int rowIndex, int columnIndex)
        {
            return _grid.GetCellDisplayRectangle(columnIndex, rowIndex, false);
        }

        public HitTestInfo HitTest(Point point)
        {
            return _grid.HitTest(point.X, point.Y);
        }

        public HitTestInfo HitTest(int x, int y)
        {
            return _grid.HitTest(x, y);
        }

        public void BeginInit()
        {

        }

        public void EndInit()
        {

        }
    }
}
