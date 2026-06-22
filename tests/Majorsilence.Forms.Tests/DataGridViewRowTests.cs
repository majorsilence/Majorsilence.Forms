// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DataGridViewRowTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DataGridViewRowTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/SharedRow/virtual-mode plumbing).
    // Majorsilence.Forms' DataGridView is a compatibility layer, so these tests pin the behavior that is
    // actually implemented: ctor defaults, simple property round-trips, the Cells collection,
    // the Height/MinimumHeight clamping that MF performs, the ReadOnly cascade to cells, and the
    // Index/DataGridView wiring established when a row joins a Rows collection.
    public class DataGridViewRowTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var row = new DataGridViewRow ();

            Assert.Same (row.Cells, row.Cells);
            Assert.Empty (row.Cells);
            Assert.Null (row.DataBoundItem);
            Assert.Null (row.DataGridView);
            Assert.NotNull (row.DefaultCellStyle);
            Assert.Same (row.DefaultCellStyle, row.DefaultCellStyle);
            Assert.NotNull (row.HeaderCell);
            Assert.Same (row.HeaderCell, row.HeaderCell);
            Assert.Equal (25, row.Height);
            Assert.Equal (-1, row.Index);
            Assert.False (row.IsNewRow);
            Assert.Equal (10, row.MinimumHeight);
            Assert.False (row.ReadOnly);
            Assert.Equal (DataGridViewTriState.NotSet, row.Resizable);
            Assert.False (row.Selected);
            Assert.False (row.Frozen);
            Assert.Empty (row.ErrorText);
            Assert.Null (row.Tag);
            Assert.True (row.Visible);
        }

        [Fact]
        public void Cells_NotNull ()
        {
            var row = new DataGridViewRow ();
            Assert.NotNull (row.Cells);
        }

        [Fact]
        public void Cells_Add_Value_AddsCell ()
        {
            var row = new DataGridViewRow ();

            var cell = row.Cells.Add ("hello");

            Assert.Single (row.Cells);
            Assert.Same (cell, row.Cells[0]);
            Assert.Equal ("hello", cell.Value);
        }

        [Fact]
        public void Cells_Add_SetsOwningRow ()
        {
            var row = new DataGridViewRow ();

            var cell = row.Cells.Add ("a");

            Assert.Same (row, cell.OwningRow);
        }

        [Fact]
        public void Cells_Add_Multiple_GetCountAndOrder ()
        {
            var row = new DataGridViewRow ();

            row.Cells.Add ("a");
            row.Cells.Add ("b");
            row.Cells.Add ("c");

            Assert.Equal (3, row.Cells.Count);
            Assert.Equal ("a", row.Cells[0].Value);
            Assert.Equal ("b", row.Cells[1].Value);
            Assert.Equal ("c", row.Cells[2].Value);
        }

        [Fact]
        public void Cells_IndexOf_ReturnsExpected ()
        {
            var row = new DataGridViewRow ();
            var first = row.Cells.Add ("a");
            var second = row.Cells.Add ("b");

            Assert.Equal (0, row.Cells.IndexOf (first));
            Assert.Equal (1, row.Cells.IndexOf (second));
        }

        [Fact]
        public void Cells_Clear_RemovesAllAndClearsOwner ()
        {
            var row = new DataGridViewRow ();
            var cell = row.Cells.Add ("a");
            row.Cells.Add ("b");

            row.Cells.Clear ();

            Assert.Empty (row.Cells);
            Assert.Null (cell.OwningRow);
        }

        [Theory]
        [InlineData (-1, 10)]
        [InlineData (0, 10)]
        [InlineData (5, 10)]
        [InlineData (10, 10)]
        [InlineData (11, 11)]
        [InlineData (100, 100)]
        public void Height_Set_GetReturnsExpected (int value, int expected)
        {
            var row = new DataGridViewRow { Height = value };

            Assert.Equal (expected, row.Height);

            // Set same.
            row.Height = value;
            Assert.Equal (expected, row.Height);
        }

        [Fact]
        public void Height_SetDefault_IsTwentyFive ()
        {
            var row = new DataGridViewRow ();
            Assert.Equal (25, row.Height);
        }

        [Theory]
        [InlineData (10)]
        [InlineData (3)]
        [InlineData (100)]
        public void MinimumHeight_Set_GetReturnsExpected (int value)
        {
            var row = new DataGridViewRow { MinimumHeight = value };
            Assert.Equal (value, row.MinimumHeight);

            // Set same.
            row.MinimumHeight = value;
            Assert.Equal (value, row.MinimumHeight);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Visible_Set_GetReturnsExpected (bool value)
        {
            var row = new DataGridViewRow { Visible = value };
            Assert.Equal (value, row.Visible);

            // Set same.
            row.Visible = value;
            Assert.Equal (value, row.Visible);

            // Set different.
            row.Visible = !value;
            Assert.Equal (!value, row.Visible);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Frozen_Set_GetReturnsExpected (bool value)
        {
            var row = new DataGridViewRow { Frozen = value };
            Assert.Equal (value, row.Frozen);

            // Set same.
            row.Frozen = value;
            Assert.Equal (value, row.Frozen);

            // Set different.
            row.Frozen = !value;
            Assert.Equal (!value, row.Frozen);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ReadOnly_Set_GetReturnsExpected (bool value)
        {
            var row = new DataGridViewRow { ReadOnly = value };
            Assert.Equal (value, row.ReadOnly);

            // Set same.
            row.ReadOnly = value;
            Assert.Equal (value, row.ReadOnly);

            // Set different.
            row.ReadOnly = !value;
            Assert.Equal (!value, row.ReadOnly);
        }

        [Fact]
        public void ReadOnly_SetWithCells_CascadesToCells ()
        {
            var row = new DataGridViewRow ();
            var cell1 = row.Cells.Add ("a");
            var cell2 = row.Cells.Add ("b");
            cell2.ReadOnly = true;

            // Set false leaves the existing cell flags as the row drives them.
            row.ReadOnly = false;
            Assert.False (row.ReadOnly);
            Assert.False (cell1.ReadOnly);
            Assert.False (cell2.ReadOnly);

            // Set true cascades to all cells.
            row.ReadOnly = true;
            Assert.True (row.ReadOnly);
            Assert.True (cell1.ReadOnly);
            Assert.True (cell2.ReadOnly);

            // Set false cascades to all cells.
            row.ReadOnly = false;
            Assert.False (row.ReadOnly);
            Assert.False (cell1.ReadOnly);
            Assert.False (cell2.ReadOnly);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("value")]
        public void Tag_Set_GetReturnsExpected (string? value)
        {
            var row = new DataGridViewRow { Tag = value };
            Assert.Equal (value, row.Tag);

            // Set same.
            row.Tag = value;
            Assert.Equal (value, row.Tag);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("value")]
        public void Tag_SetWithNonNullOldValue_GetReturnsExpected (string? value)
        {
            var row = new DataGridViewRow { Tag = "OldValue" };

            row.Tag = value;
            Assert.Equal (value, row.Tag);
        }

        [Fact]
        public void DefaultCellStyle_Set_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle ();
            var row = new DataGridViewRow { DefaultCellStyle = style };

            Assert.Same (style, row.DefaultCellStyle);
        }

        [Fact]
        public void Index_NoDataGridView_ReturnsNegativeOne ()
        {
            var row = new DataGridViewRow ();
            Assert.Equal (-1, row.Index);
        }

        [Fact]
        public void DataGridView_NoOwner_ReturnsNull ()
        {
            var row = new DataGridViewRow ();
            Assert.Null (row.DataGridView);
        }

        [Fact]
        public void Rows_Add_SetsDataGridViewAndIndex ()
        {
            using var control = new DataGridView ();
            var row = new DataGridViewRow ();

            control.Rows.Add (row);

            Assert.Same (control, row.DataGridView);
            Assert.Equal (0, row.Index);
        }

        [Fact]
        public void Rows_AddMultiple_AssignsSequentialIndexes ()
        {
            using var control = new DataGridView ();
            var row0 = new DataGridViewRow ();
            var row1 = new DataGridViewRow ();
            var row2 = new DataGridViewRow ();

            control.Rows.Add (row0);
            control.Rows.Add (row1);
            control.Rows.Add (row2);

            Assert.Equal (0, row0.Index);
            Assert.Equal (1, row1.Index);
            Assert.Equal (2, row2.Index);
        }

        [Fact]
        public void Rows_AddValues_CreatesRowWithCells ()
        {
            using var control = new DataGridView ();

            var row = control.Rows.Add ("a", "b", "c");

            Assert.Same (control, row.DataGridView);
            Assert.Equal (0, row.Index);
            Assert.Equal (3, row.Cells.Count);
            Assert.Equal ("a", row.Cells[0].Value);
        }

        [Fact]
        public void Rows_Remove_ClearsDataGridViewAndIndex ()
        {
            using var control = new DataGridView ();
            var row = new DataGridViewRow ();
            control.Rows.Add (row);

            control.Rows.Remove (row);

            Assert.Null (row.DataGridView);
            Assert.Equal (-1, row.Index);
        }

        [Fact]
        public void Rows_Insert_SetsDataGridViewAndIndex ()
        {
            using var control = new DataGridView ();
            var first = new DataGridViewRow ();
            control.Rows.Add (first);

            var inserted = new DataGridViewRow ();
            control.Rows.Insert (0, inserted);

            Assert.Same (control, inserted.DataGridView);
            Assert.Equal (0, inserted.Index);
            Assert.Equal (1, first.Index);
        }

        [Fact]
        public void Rows_Clear_ResetsRowOwnership ()
        {
            using var control = new DataGridView ();
            var row = new DataGridViewRow ();
            control.Rows.Add (row);

            control.Rows.Clear ();

            Assert.Null (row.DataGridView);
            Assert.Equal (-1, row.Index);
            Assert.Empty (control.Rows);
        }

        [Fact]
        public void Cell_DataGridView_ReflectsRowOwner ()
        {
            using var control = new DataGridView ();
            var row = new DataGridViewRow ();
            var cell = row.Cells.Add ("a");
            control.Rows.Add (row);

            Assert.Same (control, cell.DataGridView);
            Assert.Equal (0, cell.RowIndex);
            Assert.Equal (0, cell.ColumnIndex);
        }
    }
}
