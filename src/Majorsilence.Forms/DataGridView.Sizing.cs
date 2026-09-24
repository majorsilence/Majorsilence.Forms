using System;
using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    // W5.4, the sizing half (findings DGV-18, DGV-19), plus #94.
    //
    // AutoSizeColumnsMode was stored and Invalidate ()d. Fill -- what most designer-built grids use --
    // left 100-px columns and a blank band down the right. AutoResizeColumn(s)/Row(s) were
    // `=> Invalidate ()` while GetPreferredWidth/GetPreferredHeight sat unused beside them. And
    // RowTemplate, the WinForms way to set row height (the designer emits `RowTemplate.Height = 32`),
    // was a settable field with no readers: every path did `new DataGridViewRow ()`.
    public partial class DataGridView
    {
        // ---------------- DGV-18: Fill

        // Distributes the content width across the fill columns by FillWeight, once per layout. Called
        // from UpdateScrollBars, which every relayout path already reaches (SetBoundsCore,
        // OnColumnsChanged, row changes), so Fill is re-applied whenever the width it depends on moves.
        private void ApplyFillColumnWidths ()
        {
            if (Columns.Count == 0)
                return;

            var fill = Columns.Where (c => c.Visible && c.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.Fill).ToList ();

            if (fill.Count == 0)
                return;

            var client = GetContentArea ();
            var row_header = RowHeadersVisible ? ScaledRowHeadersWidth : 0;
            var scrollbar = vscrollbar.Visible ? (int)Math.Ceiling (vscrollbar.Width * ScaleFactor.Width) : 0;
            var available = DeviceToLogicalUnits (client.Width - row_header - scrollbar);

            var fixed_width = Columns.Where (c => c.Visible && !fill.Contains (c)).Sum (c => c.Width);
            var remaining = Math.Max (0, available - fixed_width);
            var total_weight = fill.Sum (c => Math.Max (0f, c.FillWeight));

            if (total_weight <= 0)
                return;

            // Widths are handed out in weight order and the last column takes the rounding remainder,
            // so the fill columns add up to exactly the available width rather than leaving a 1-px band.
            var assigned = 0;

            for (var i = 0; i < fill.Count; i++) {
                var column = fill[i];
                var share = i == fill.Count - 1
                    ? remaining - assigned
                    : (int)Math.Floor (remaining * (Math.Max (0f, column.FillWeight) / total_weight));

                // Not through Width's setter: that raises OnColumnsChanged, which calls UpdateScrollBars,
                // which is where this is running. MinimumWidth is enforced there, once -- a second clamp
                // here was the kind of redundancy no test can tell from a working one.
                column.SetWidthFromLayout (share);
                assigned += column.Width;
            }
        }

        // ---------------- DGV-18: measure-based resizing

        /// <summary>Resizes every column to fit its content, using <see cref="AutoSizeColumnsMode"/>.</summary>
        public void AutoResizeColumns () => AutoResizeColumns (AutoSizeColumnsMode);

        /// <summary>Resizes every column to fit its content, using the given mode.</summary>
        public void AutoResizeColumns (DataGridViewAutoSizeColumnsMode autoSizeColumnsMode)
        {
            var mode = ToColumnMode (autoSizeColumnsMode);

            if (mode is null)
                return;

            for (var i = 0; i < Columns.Count; i++)
                AutoResizeColumn (i, mode.Value);
        }

        /// <summary>Resizes one column to fit its content, using its <see cref="DataGridViewColumn.InheritedAutoSizeMode"/>.</summary>
        public void AutoResizeColumn (int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                return;

            var inherited = Columns[columnIndex].InheritedAutoSizeMode;
            AutoResizeColumn (columnIndex, inherited is DataGridViewAutoSizeColumnMode.None or DataGridViewAutoSizeColumnMode.NotSet
                ? DataGridViewAutoSizeColumnMode.AllCells
                : inherited);
        }

        /// <summary>Resizes one column to fit its content, using the given mode.</summary>
        public void AutoResizeColumn (int columnIndex, DataGridViewAutoSizeColumnMode autoSizeColumnMode)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                return;

            // Fill is a layout mode, not a measurement: it is applied by the layout pass, not here.
            if (autoSizeColumnMode is DataGridViewAutoSizeColumnMode.Fill or DataGridViewAutoSizeColumnMode.None or DataGridViewAutoSizeColumnMode.NotSet)
                return;

            var column = Columns[columnIndex];
            column.Width = column.GetPreferredWidth (autoSizeColumnMode, fixedHeight: true);
        }

        /// <summary>Resizes every row to fit its content.</summary>
        public void AutoResizeRows () => AutoResizeRows (DataGridViewAutoSizeRowsMode.AllCells);

        /// <summary>Resizes every row to fit its content, using the given mode.</summary>
        public void AutoResizeRows (DataGridViewAutoSizeRowsMode autoSizeRowsMode)
        {
            if (autoSizeRowsMode == DataGridViewAutoSizeRowsMode.None)
                return;

            for (var i = 0; i < Rows.Count; i++)
                AutoResizeRow (i, DataGridViewAutoSizeRowMode.AllCells);
        }

        /// <summary>Resizes one row to fit its content.</summary>
        public void AutoResizeRow (int rowIndex) => AutoResizeRow (rowIndex, DataGridViewAutoSizeRowMode.AllCells);

        /// <summary>Resizes one row to fit its content, using the given mode.</summary>
        public void AutoResizeRow (int rowIndex, DataGridViewAutoSizeRowMode autoSizeRowMode)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                return;

            var row = Rows[rowIndex];
            row.Height = row.GetPreferredHeight (rowIndex, autoSizeRowMode, fixedWidth: true);
        }

        // The grid-wide mode names the per-column mode it stands for; None means "do not measure".
        private static DataGridViewAutoSizeColumnMode? ToColumnMode (DataGridViewAutoSizeColumnsMode mode)
            => mode switch {
                DataGridViewAutoSizeColumnsMode.AllCells => DataGridViewAutoSizeColumnMode.AllCells,
                DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader => DataGridViewAutoSizeColumnMode.AllCellsExceptHeader,
                DataGridViewAutoSizeColumnsMode.ColumnHeader => DataGridViewAutoSizeColumnMode.ColumnHeader,
                DataGridViewAutoSizeColumnsMode.DisplayedCells => DataGridViewAutoSizeColumnMode.DisplayedCells,
                DataGridViewAutoSizeColumnsMode.DisplayedCellsExceptHeader => DataGridViewAutoSizeColumnMode.DisplayedCellsExceptHeader,
                _ => null,
            };

        // ---------------- DGV-19: rows come from the template

        /// <summary>
        /// Creates a row from <see cref="RowTemplate"/> -- its height, minimum height, style and
        /// resizability -- with one empty cell per column. Every path that makes a row goes through here.
        /// </summary>
        internal DataGridViewRow CreateRowFromTemplate ()
        {
            var row = (DataGridViewRow)row_template.Clone ();
            row.Cells.Clear ();

            for (var c = 0; c < Columns.Count; c++)
                row.Cells.Add (new DataGridViewCell ());

            return row;
        }

        /// <summary>
        /// Gets or sets the default height of new rows. This is <see cref="RowTemplate"/>'s
        /// <see cref="DataGridViewRow.Height"/> under the name this library has always used.
        /// </summary>
        public int RowHeight {
            get => row_template.Height;
            set {
                if (row_template.Height == value)
                    return;

                row_template.Height = value;
                UpdateScrollBars ();
                Invalidate ();
            }
        }

        // ---------------- #94: the scroll range counts visible rows

        /// <summary>The vertical scrollbar's range, as the layout last set it. What a hidden row must
        /// not be counted in.</summary>
        internal int VerticalScrollMaximum => vscrollbar.Maximum;

        // Rows that take space. A hidden row (Row.Visible = false, W5.2a) has no height and cannot be
        // scrolled to, so it must not be in the scroll range either -- counting it left the thumb
        // draggable into blank space past the last visible row.
        private int VisibleRowCount ()
        {
            var count = 0;

            for (var i = 0; i < RowCountWithNewRow; i++)
                if (RowDeviceHeight (i) > 0)
                    count++;

            return count;
        }
    }
}
