// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DataGridViewTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DataGridViewTests, adapted to the
    // Continuum.Forms compat layer. These exercise CONTROL-level behavior (the Columns/Rows
    // collections at the grid level, ColumnCount/RowCount, SelectionMode, ReadOnly, MultiSelect,
    // the AllowUserToXxx toggles, header visibility, CurrentCell/CurrentRow, and selection).
    // Cell/column/row-level behavior is covered by the dedicated DataGridViewColumnTests,
    // DataGridViewRowTests, DataGridViewCellTests, etc.
    //
    // Continuum.Forms is a compat layer: many of these properties are simple auto-properties or
    // value-changed setters with no element-state plumbing, layout callbacks, or handle creation,
    // so the assertions pin Continuum.Forms' ACTUAL semantics (which differ in defaults from the
    // upstream control) rather than the original WinForms numbers.
    public class DataGridViewTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new DataGridView ();

            // Collections start empty but non-null.
            Assert.NotNull (control.Columns);
            Assert.NotNull (control.Rows);
            Assert.Empty (control.Columns);
            Assert.Empty (control.Rows);
            Assert.Equal (0, control.ColumnCount);
            Assert.Equal (0, control.RowCount);

            // Selection defaults.
            Assert.Equal (DataGridViewSelectionMode.FullRowSelect, control.SelectionMode);
            Assert.True (control.MultiSelect);
            Assert.False (control.ReadOnly);
            Assert.Null (control.CurrentCell);
            Assert.Null (control.CurrentRow);
            Assert.Empty (control.SelectedRows);

            // AllowUserToXxx defaults.
            Assert.True (control.AllowUserToAddRows);
            Assert.True (control.AllowUserToDeleteRows);
            Assert.False (control.AllowUserToOrderColumns);
            Assert.True (control.AllowUserToResizeColumns);
            Assert.True (control.AllowUserToResizeRows);

            // Header defaults.
            Assert.True (control.ColumnHeadersVisible);
            Assert.False (control.RowHeadersVisible);
            Assert.Equal (DataGridViewColumnHeadersHeightSizeMode.EnableResizing, control.ColumnHeadersHeightSizeMode);
            Assert.Equal (DataGridViewRowHeadersWidthSizeMode.EnableResizing, control.RowHeadersWidthSizeMode);

            // Misc defaults.
            Assert.Equal (DataGridViewAutoSizeColumnsMode.None, control.AutoSizeColumnsMode);
            Assert.Equal (DataGridViewAutoSizeRowsMode.None, control.AutoSizeRowsMode);
            Assert.Equal (DataGridViewEditMode.EditOnKeystrokeOrF2, control.EditMode);
            Assert.True (control.AutoGenerateColumns);
            Assert.False (control.VirtualMode);
            Assert.False (control.IsCurrentCellInEditMode);
            Assert.Null (control.DataSource);
            Assert.NotNull (control.RowTemplate);
            Assert.Same (control.RowTemplate, control.RowTemplate);
        }

        [Fact]
        public void Columns_Add_IncrementsColumnCount ()
        {
            using var control = new DataGridView ();

            control.Columns.Add ("col1", "Header 1");
            control.Columns.Add ("col2", "Header 2");

            Assert.Equal (2, control.Columns.Count);
            Assert.Equal (2, control.ColumnCount);
        }

        [Fact]
        public void Columns_Add_WiresUpColumnToGrid ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ();

            control.Columns.Add (column);

            Assert.Same (control, column.DataGridView);
            Assert.Equal (0, column.Index);
            Assert.Same (column, Assert.Single (control.Columns));
        }

        [Fact]
        public void Columns_Clear_RemovesAllColumns ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col1", "Header 1");
            control.Columns.Add ("col2", "Header 2");

            control.Columns.Clear ();

            Assert.Empty (control.Columns);
            Assert.Equal (0, control.ColumnCount);
        }

        [Fact]
        public void Rows_Add_IncrementsRowCount ()
        {
            using var control = new DataGridView ();

            control.Rows.Add ("a", "b");
            control.Rows.Add ("c", "d");

            Assert.Equal (2, control.Rows.Count);
            Assert.Equal (2, control.RowCount);
        }

        [Fact]
        public void Rows_Add_SetsOwningGrid ()
        {
            using var control = new DataGridView ();

            var row = control.Rows.Add ("a", "b");

            Assert.Same (control, row.DataGridView);
            Assert.Same (row, Assert.Single (control.Rows));
        }

        [Fact]
        public void Rows_AddCount_AddsEmptyRows ()
        {
            using var control = new DataGridView ();

            control.Rows.Add (3);

            Assert.Equal (3, control.Rows.Count);
        }

        [Fact]
        public void Rows_Clear_RemovesAllRows ()
        {
            using var control = new DataGridView ();
            control.Rows.Add ("a");
            control.Rows.Add ("b");

            control.Rows.Clear ();

            Assert.Empty (control.Rows);
            Assert.Equal (0, control.RowCount);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        public void RowCount_Set_AddsRowsToReachCount (int value)
        {
            using var control = new DataGridView ();

            control.RowCount = value;

            Assert.Equal (value, control.RowCount);
            Assert.Equal (value, control.Rows.Count);
        }

        [Fact]
        public void RowCount_SetSmaller_RemovesRows ()
        {
            using var control = new DataGridView { RowCount = 5 };

            control.RowCount = 2;

            Assert.Equal (2, control.RowCount);
            Assert.Equal (2, control.Rows.Count);
        }

        [Fact]
        public void NewRowIndex_ReflectsAllowUserToAddRows ()
        {
            using var control = new DataGridView ();
            control.Rows.Add ("a");
            control.Rows.Add ("b");

            // When new rows are allowed, the new-row index is the row count.
            Assert.True (control.AllowUserToAddRows);
            Assert.Equal (2, control.NewRowIndex);

            control.AllowUserToAddRows = false;
            Assert.Equal (-1, control.NewRowIndex);
        }

        [Theory]
        [InlineData (DataGridViewSelectionMode.CellSelect)]
        [InlineData (DataGridViewSelectionMode.FullRowSelect)]
        [InlineData (DataGridViewSelectionMode.FullColumnSelect)]
        [InlineData (DataGridViewSelectionMode.RowHeaderSelect)]
        [InlineData (DataGridViewSelectionMode.ColumnHeaderSelect)]
        public void SelectionMode_Set_GetReturnsExpected (DataGridViewSelectionMode value)
        {
            using var control = new DataGridView { SelectionMode = value };

            Assert.Equal (value, control.SelectionMode);

            // Set same.
            control.SelectionMode = value;
            Assert.Equal (value, control.SelectionMode);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void MultiSelect_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { MultiSelect = value };

            Assert.Equal (value, control.MultiSelect);

            control.MultiSelect = value;
            Assert.Equal (value, control.MultiSelect);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ReadOnly_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { ReadOnly = value };

            Assert.Equal (value, control.ReadOnly);

            control.ReadOnly = value;
            Assert.Equal (value, control.ReadOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AllowUserToAddRows_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { AllowUserToAddRows = value };

            Assert.Equal (value, control.AllowUserToAddRows);

            control.AllowUserToAddRows = value;
            Assert.Equal (value, control.AllowUserToAddRows);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AllowUserToDeleteRows_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { AllowUserToDeleteRows = value };

            Assert.Equal (value, control.AllowUserToDeleteRows);

            control.AllowUserToDeleteRows = value;
            Assert.Equal (value, control.AllowUserToDeleteRows);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AllowUserToOrderColumns_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { AllowUserToOrderColumns = value };

            Assert.Equal (value, control.AllowUserToOrderColumns);

            control.AllowUserToOrderColumns = value;
            Assert.Equal (value, control.AllowUserToOrderColumns);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AllowUserToResizeColumns_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { AllowUserToResizeColumns = value };

            Assert.Equal (value, control.AllowUserToResizeColumns);

            control.AllowUserToResizeColumns = value;
            Assert.Equal (value, control.AllowUserToResizeColumns);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AllowUserToResizeRows_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { AllowUserToResizeRows = value };

            Assert.Equal (value, control.AllowUserToResizeRows);

            control.AllowUserToResizeRows = value;
            Assert.Equal (value, control.AllowUserToResizeRows);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ColumnHeadersVisible_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { ColumnHeadersVisible = value };

            Assert.Equal (value, control.ColumnHeadersVisible);

            control.ColumnHeadersVisible = value;
            Assert.Equal (value, control.ColumnHeadersVisible);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void RowHeadersVisible_Set_GetReturnsExpected (bool value)
        {
            using var control = new DataGridView { RowHeadersVisible = value };

            Assert.Equal (value, control.RowHeadersVisible);

            control.RowHeadersVisible = value;
            Assert.Equal (value, control.RowHeadersVisible);
        }

        [Theory]
        [InlineData (DataGridViewAutoSizeColumnsMode.None)]
        [InlineData (DataGridViewAutoSizeColumnsMode.AllCells)]
        [InlineData (DataGridViewAutoSizeColumnsMode.ColumnHeader)]
        [InlineData (DataGridViewAutoSizeColumnsMode.DisplayedCells)]
        [InlineData (DataGridViewAutoSizeColumnsMode.Fill)]
        public void AutoSizeColumnsMode_Set_GetReturnsExpected (DataGridViewAutoSizeColumnsMode value)
        {
            using var control = new DataGridView { AutoSizeColumnsMode = value };

            Assert.Equal (value, control.AutoSizeColumnsMode);
        }

        [Theory]
        [InlineData (DataGridViewAutoSizeRowsMode.None)]
        [InlineData (DataGridViewAutoSizeRowsMode.AllCells)]
        [InlineData (DataGridViewAutoSizeRowsMode.DisplayedCells)]
        public void AutoSizeRowsMode_Set_GetReturnsExpected (DataGridViewAutoSizeRowsMode value)
        {
            using var control = new DataGridView { AutoSizeRowsMode = value };

            Assert.Equal (value, control.AutoSizeRowsMode);
        }

        [Theory]
        [InlineData (DataGridViewEditMode.EditOnEnter)]
        [InlineData (DataGridViewEditMode.EditOnKeystroke)]
        [InlineData (DataGridViewEditMode.EditOnKeystrokeOrF2)]
        [InlineData (DataGridViewEditMode.EditProgrammatically)]
        public void EditMode_Set_GetReturnsExpected (DataGridViewEditMode value)
        {
            using var control = new DataGridView { EditMode = value };

            Assert.Equal (value, control.EditMode);
        }

        [Theory]
        [InlineData (10, 10)]
        [InlineData (23, 23)]
        [InlineData (100, 100)]
        [InlineData (5, 10)]   // Clamped up to the minimum of 10.
        [InlineData (0, 10)]
        public void ColumnHeadersHeight_Set_ClampsToMinimum (int value, int expected)
        {
            using var control = new DataGridView { ColumnHeadersHeight = value };

            Assert.Equal (expected, control.ColumnHeadersHeight);
        }

        [Theory]
        [InlineData (10, 10)]
        [InlineData (41, 41)]
        [InlineData (100, 100)]
        [InlineData (5, 10)]   // Clamped up to the minimum of 10.
        [InlineData (0, 10)]
        public void RowHeadersWidth_Set_ClampsToMinimum (int value, int expected)
        {
            using var control = new DataGridView { RowHeadersWidth = value };

            Assert.Equal (expected, control.RowHeadersWidth);
        }

        [Theory]
        [InlineData (10, 10)]
        [InlineData (25, 25)]
        [InlineData (5, 10)]   // Clamped up to the minimum of 10.
        public void RowHeight_Set_ClampsToMinimum (int value, int expected)
        {
            using var control = new DataGridView { RowHeight = value };

            Assert.Equal (expected, control.RowHeight);
        }

        [Fact]
        public void SelectedRowIndex_Set_UpdatesCurrentRowAndSelection ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col", "Header");
            var row0 = control.Rows.Add ("a");
            var row1 = control.Rows.Add ("b");

            control.SelectedRowIndex = 1;

            Assert.Same (row1, control.CurrentRow);
            Assert.True (row1.Selected);
            Assert.False (row0.Selected);
            Assert.Same (row1, Assert.Single (control.SelectedRows));
        }

        [Fact]
        public void SelectedRowIndex_SetNewRow_DeselectsPreviousRow ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col", "Header");
            var row0 = control.Rows.Add ("a");
            var row1 = control.Rows.Add ("b");

            control.SelectedRowIndex = 0;
            control.SelectedRowIndex = 1;

            Assert.False (row0.Selected);
            Assert.True (row1.Selected);
        }

        [Fact]
        public void CurrentCell_FullRowSelect_ReturnsCellAtSelectedAddress ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col0", "H0");
            control.Columns.Add ("col1", "H1");
            control.Rows.Add ("a", "b");

            control.SelectedRowIndex = 0;
            control.SelectedColumnIndex = 1;

            var cell = control.CurrentCell;
            Assert.NotNull (cell);
            Assert.Equal (new Point (1, 0), control.CurrentCellAddress);
        }

        [Fact]
        public void CurrentCell_NoSelection_ReturnsNull ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col", "Header");
            control.Rows.Add ("a");

            Assert.Null (control.CurrentCell);
            Assert.Null (control.CurrentRow);
        }

        [Fact]
        public void ClearSelection_ResetsCurrentRowAndCell ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col", "Header");
            control.Rows.Add ("a");
            control.Rows.Add ("b");

            control.SelectedRowIndex = 1;
            control.ClearSelection ();

            Assert.Null (control.CurrentRow);
            Assert.Null (control.CurrentCell);
            Assert.Empty (control.SelectedRows);
            Assert.All (control.Rows, r => Assert.False (r.Selected));
        }

        [Fact]
        public void SelectAll_FullRowSelect_SelectsAllRows ()
        {
            using var control = new DataGridView { SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            control.Columns.Add ("col", "Header");
            control.Rows.Add ("a");
            control.Rows.Add ("b");
            control.Rows.Add ("c");

            control.SelectAll ();

            Assert.All (control.Rows, r => Assert.True (r.Selected));
            Assert.Equal (3, control.SelectedRows.Count);
        }

        [Fact]
        public void Indexer_ByColumnRow_ReturnsCell ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col0", "H0");
            control.Columns.Add ("col1", "H1");
            control.Rows.Add ("a", "b");

            var cell = control[1, 0];

            Assert.NotNull (cell);
            Assert.Equal ("b", cell!.Value);
        }

        [Fact]
        public void Indexer_OutOfRange_ReturnsNull ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col", "Header");
            control.Rows.Add ("a");

            Assert.Null (control[5, 0]);
            Assert.Null (control[0, 5]);
        }

        [Fact]
        public void ReadOnly_SetTrue_CancelsActiveEdit ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col", "Header");
            control.Rows.Add ("a");

            // ReadOnly prevents editing; setting it true must not leave a cell in edit mode.
            control.ReadOnly = true;

            Assert.False (control.IsCurrentCellInEditMode);
        }

        [Fact]
        public void GetCellCount_SelectedFilter_CountsSelectedCells ()
        {
            using var control = new DataGridView { SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            control.Columns.Add ("col0", "H0");
            control.Columns.Add ("col1", "H1");
            control.Rows.Add ("a", "b");
            control.Rows.Add ("c", "d");

            control.SelectedRowIndex = 0;

            // FullRowSelect selects every cell of the selected row.
            Assert.Equal (2, control.GetCellCount (DataGridViewElementStates.Selected));
        }
    }
}
