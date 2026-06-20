// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DataGridViewCellTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DataGridViewCellTests, adapted to the
    // Continuum.Forms compat layer. Continuum.Forms exposes most DataGridViewCell members as simple
    // auto-properties (no element-state plumbing, accessibility, edit controls, or shared rows),
    // so these pin the actual Continuum.Forms semantics: constructor defaults, Value get/set with the
    // FormattedValue projection, ReadOnly/Tag/ToolTipText/Selected/Visible round-trips, the
    // non-null Style instance, the -1 ColumnIndex/RowIndex defaults when the cell is unowned, and
    // the OwningRow/OwningColumn/ColumnIndex/RowIndex wiring that happens once a cell is added to a
    // row's Cells collection and that row is added to a DataGridView.
    //
    // Where Continuum.Forms intentionally diverges from WinForms (e.g. ReadOnly does not throw when the
    // cell has no owning row, Style is a non-null ControlStyle rather than a lazily-allocated
    // DataGridViewCellStyle, and there is no ValueType/DefaultNewRowValue/ContextMenuStrip), the
    // tests assert the real Continuum.Forms behavior rather than the WinForms behavior.
    public class DataGridViewCellTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var cell = new DataGridViewCell ();

            Assert.Equal (-1, cell.ColumnIndex);
            Assert.Equal (-1, cell.RowIndex);
            Assert.Null (cell.DataGridView);
            Assert.Null (cell.OwningRow);
            Assert.Null (cell.OwningColumn);
            Assert.Null (cell.Value);
            Assert.Null (cell.FormattedValue);
            Assert.False (cell.ReadOnly);
            Assert.False (cell.Selected);
            Assert.True (cell.Visible);
            Assert.NotNull (cell.Style);
            Assert.Null (cell.Tag);
            Assert.Empty (cell.ToolTipText);
            Assert.Empty (cell.ErrorText);
        }

        [Fact]
        public void Ctor_Value_SetsValue ()
        {
            var value = new object ();
            var cell = new DataGridViewCell (value);

            Assert.Same (value, cell.Value);
        }

        [Fact]
        public void Ctor_NullValue_SetsNullValue ()
        {
            var cell = new DataGridViewCell (null);

            Assert.Null (cell.Value);
            Assert.Null (cell.FormattedValue);
        }

        [Fact]
        public void Style_Get_ReturnsSameInstance ()
        {
            var cell = new DataGridViewCell ();

            Assert.NotNull (cell.Style);
            // The Style instance is stable across reads.
            Assert.Same (cell.Style, cell.Style);
        }

        [Fact]
        public void Style_Set_GetReturnsExpected ()
        {
            var style = new ControlStyle (DataGridViewCell.DefaultCellStyleInternal);
            var cell = new DataGridViewCell { Style = style };

            Assert.Same (style, cell.Style);
        }

        [Fact]
        public void Value_Set_GetReturnsExpected ()
        {
            var cell = new DataGridViewCell ();

            cell.Value = "value";
            Assert.Equal ("value", cell.Value);

            // Set same.
            cell.Value = "value";
            Assert.Equal ("value", cell.Value);

            // Set null.
            cell.Value = null;
            Assert.Null (cell.Value);
        }

        [Fact]
        public void Value_SetObject_GetReturnsSame ()
        {
            var value = new object ();
            var cell = new DataGridViewCell ();

            cell.Value = value;
            Assert.Same (value, cell.Value);
        }

        [Fact]
        public void FormattedValue_Get_ReturnsValueToString ()
        {
            var cell = new DataGridViewCell ();
            Assert.Null (cell.FormattedValue);

            cell.Value = 42;
            Assert.Equal ("42", cell.FormattedValue);

            cell.Value = "text";
            Assert.Equal ("text", cell.FormattedValue);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ReadOnly_Set_GetReturnsExpected (bool value)
        {
            // Unlike WinForms (which throws when the cell has no owning row), Continuum.Forms ReadOnly
            // is a plain settable property even on an unowned cell.
            var cell = new DataGridViewCell { ReadOnly = value };

            Assert.Equal (value, cell.ReadOnly);

            // Set same.
            cell.ReadOnly = value;
            Assert.Equal (value, cell.ReadOnly);

            // Set opposite.
            cell.ReadOnly = !value;
            Assert.Equal (!value, cell.ReadOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Selected_Set_GetReturnsExpected (bool value)
        {
            var cell = new DataGridViewCell { Selected = value };

            Assert.Equal (value, cell.Selected);

            cell.Selected = value;
            Assert.Equal (value, cell.Selected);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Visible_Set_GetReturnsExpected (bool value)
        {
            var cell = new DataGridViewCell { Visible = value };

            Assert.Equal (value, cell.Visible);
        }

        [Fact]
        public void Tag_Set_GetReturnsExpected ()
        {
            var tag = new object ();
            var cell = new DataGridViewCell { Tag = tag };

            Assert.Same (tag, cell.Tag);

            cell.Tag = null;
            Assert.Null (cell.Tag);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("ToolTipText")]
        public void ToolTipText_Set_GetReturnsExpected (string value)
        {
            var cell = new DataGridViewCell { ToolTipText = value };

            Assert.Equal (value, cell.ToolTipText);

            // Set same.
            cell.ToolTipText = value;
            Assert.Equal (value, cell.ToolTipText);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("ErrorText")]
        public void ErrorText_Set_GetReturnsExpected (string value)
        {
            var cell = new DataGridViewCell { ErrorText = value };

            Assert.Equal (value, cell.ErrorText);
        }

        [Fact]
        public void ColumnIndex_GetWithoutOwningRow_ReturnsMinusOne ()
        {
            var cell = new DataGridViewCell ();
            Assert.Equal (-1, cell.ColumnIndex);
        }

        [Fact]
        public void RowIndex_GetWithoutOwningRow_ReturnsMinusOne ()
        {
            var cell = new DataGridViewCell ();
            Assert.Equal (-1, cell.RowIndex);
        }

        [Fact]
        public void OwningRow_GetWithoutOwningRow_ReturnsNull ()
        {
            var cell = new DataGridViewCell ();
            Assert.Null (cell.OwningRow);
        }

        [Fact]
        public void AddToRowCells_SetsOwningRow ()
        {
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();

            row.Cells.Add (cell);

            Assert.Same (row, cell.OwningRow);
        }

        [Fact]
        public void AddToRowCells_SetsColumnIndex ()
        {
            var row = new DataGridViewRow ();
            var cell0 = new DataGridViewCell ();
            var cell1 = new DataGridViewCell ();

            row.Cells.Add (cell0);
            row.Cells.Add (cell1);

            Assert.Equal (0, cell0.ColumnIndex);
            Assert.Equal (1, cell1.ColumnIndex);
        }

        [Fact]
        public void RemoveFromRowCells_ResetsOwningRowAndColumnIndex ()
        {
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();
            row.Cells.Add (cell);

            row.Cells.Remove (cell);

            Assert.Null (cell.OwningRow);
            Assert.Equal (-1, cell.ColumnIndex);
        }

        [Fact]
        public void ClearRowCells_ResetsOwningRow ()
        {
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();
            row.Cells.Add (cell);

            row.Cells.Clear ();

            Assert.Null (cell.OwningRow);
            Assert.Empty (row.Cells);
        }

        [Fact]
        public void Cells_AddValue_ReturnsCellWithValue ()
        {
            var row = new DataGridViewRow ();

            var cell = row.Cells.Add ("hello");

            Assert.Equal ("hello", cell.Value);
            Assert.Same (row, cell.OwningRow);
        }

        [Fact]
        public void RowIndex_WithRowInDataGridView_ReturnsRowIndex ()
        {
            using var control = new DataGridView ();
            control.Columns.Add ("col0");
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();
            row.Cells.Add (cell);
            control.Rows.Add (row);

            Assert.Equal (0, cell.RowIndex);
            Assert.Same (control, cell.DataGridView);
        }

        [Fact]
        public void OwningColumn_WithCellInDataGridView_ReturnsColumn ()
        {
            using var control = new DataGridView ();
            var column = control.Columns.Add ("col0");
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();
            row.Cells.Add (cell);
            control.Rows.Add (row);

            Assert.Same (column, cell.OwningColumn);
        }

        [Fact]
        public void OwningColumn_CellWithoutDataGridView_ReturnsNull ()
        {
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();
            row.Cells.Add (cell);

            Assert.Null (cell.OwningColumn);
        }

        [Fact]
        public void DataGridView_GetWithoutOwner_ReturnsNull ()
        {
            var cell = new DataGridViewCell ();
            Assert.Null (cell.DataGridView);
        }

        [Fact]
        public void SetRowCellItem_ReassignsOwningRow ()
        {
            var row = new DataGridViewRow ();
            var original = new DataGridViewCell ();
            var replacement = new DataGridViewCell ();
            row.Cells.Add (original);

            row.Cells[0] = replacement;

            Assert.Null (original.OwningRow);
            Assert.Same (row, replacement.OwningRow);
            Assert.Equal (0, replacement.ColumnIndex);
        }

        [Fact]
        public void RowReadOnly_CascadesToCells ()
        {
            // Continuum.Forms semantics: setting the row's ReadOnly cascades to every cell.
            var row = new DataGridViewRow ();
            var cell = new DataGridViewCell ();
            row.Cells.Add (cell);

            row.ReadOnly = true;
            Assert.True (cell.ReadOnly);

            row.ReadOnly = false;
            Assert.False (cell.ReadOnly);
        }
    }
}
