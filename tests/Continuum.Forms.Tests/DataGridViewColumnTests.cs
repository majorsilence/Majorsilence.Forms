// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DataGridViewColumnTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DataGridViewColumnTests, adapted to the
    // Continuum.Forms compat layer. Continuum.Forms exposes most DataGridViewColumn members as simple
    // auto-properties (no element-state plumbing, accessibility, or design-time site), so these
    // pin the actual Continuum.Forms semantics: constructor defaults, property get/set round-trips,
    // Width-clamps-to-MinimumWidth, DefaultCellStyle null-coalescing, and the Index/DataGridView
    // wiring that happens when a column is added to a DataGridView's Columns collection.
    public class DataGridViewColumnTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var column = new DataGridViewColumn ();

            Assert.Null (column.CellTemplate);
            Assert.Null (column.DataGridView);
            Assert.Empty (column.DataPropertyName);
            Assert.NotNull (column.DefaultCellStyle);
            Assert.Equal (-1, column.Index);
            Assert.Equal (-1, column.DisplayIndex);
            Assert.Equal (100, column.FillWeight);
            Assert.False (column.Frozen);
            Assert.NotNull (column.HeaderCell);
            Assert.Empty (column.HeaderText);
            Assert.Equal (0, column.DividerWidth);
            Assert.Equal (30, column.MinimumWidth);
            Assert.Empty (column.Name);
            Assert.False (column.ReadOnly);
            Assert.Equal (DataGridViewTriState.NotSet, column.Resizable);
            Assert.Equal (DataGridViewColumnSortMode.Automatic, column.SortMode);
            Assert.Null (column.Tag);
            Assert.Empty (column.ToolTipText);
            Assert.True (column.Visible);
            Assert.Equal (DataGridViewAutoSizeColumnMode.None, column.AutoSizeMode);
            Assert.Equal (100, column.Width);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("HeaderText")]
        public void Ctor_HeaderText_SetsHeaderText (string headerText)
        {
            var column = new DataGridViewColumn (headerText);

            Assert.Equal (headerText, column.HeaderText);
            Assert.Null (column.DataGridView);
            Assert.Equal (-1, column.Index);
            Assert.Equal (100, column.Width);
            Assert.Equal (30, column.MinimumWidth);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("Name")]
        public void Name_Set_GetReturnsExpected (string value)
        {
            var column = new DataGridViewColumn { Name = value };

            Assert.Equal (value, column.Name);

            // Set same.
            column.Name = value;
            Assert.Equal (value, column.Name);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("HeaderText")]
        public void HeaderText_Set_GetReturnsExpected (string value)
        {
            var column = new DataGridViewColumn { HeaderText = value };

            Assert.Equal (value, column.HeaderText);

            // Set same.
            column.HeaderText = value;
            Assert.Equal (value, column.HeaderText);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("DataPropertyName")]
        public void DataPropertyName_Set_GetReturnsExpected (string value)
        {
            var column = new DataGridViewColumn { DataPropertyName = value };

            Assert.Equal (value, column.DataPropertyName);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("ToolTipText")]
        public void ToolTipText_Set_GetReturnsExpected (string value)
        {
            var column = new DataGridViewColumn { ToolTipText = value };

            Assert.Equal (value, column.ToolTipText);
        }

        [Theory]
        [InlineData (50)]
        [InlineData (100)]
        [InlineData (1000)]
        public void Width_Set_GetReturnsExpected (int value)
        {
            var column = new DataGridViewColumn { Width = value };

            Assert.Equal (value, column.Width);

            // Set same.
            column.Width = value;
            Assert.Equal (value, column.Width);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (10)]
        [InlineData (29)]
        [InlineData (-100)]
        public void Width_SetLessThanMinimumWidth_ClampsToMinimumWidth (int value)
        {
            var column = new DataGridViewColumn { Width = value };

            // Default MinimumWidth is 30, so the width is clamped up.
            Assert.Equal (30, column.Width);
            Assert.Equal (30, column.MinimumWidth);
        }

        [Fact]
        public void Width_SetBelowCustomMinimumWidth_ClampsToMinimumWidth ()
        {
            var column = new DataGridViewColumn { MinimumWidth = 75 };

            column.Width = 50;
            Assert.Equal (75, column.Width);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (30)]
        [InlineData (75)]
        [InlineData (200)]
        public void MinimumWidth_Set_GetReturnsExpected (int value)
        {
            var column = new DataGridViewColumn { MinimumWidth = value };

            Assert.Equal (value, column.MinimumWidth);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Visible_Set_GetReturnsExpected (bool value)
        {
            var column = new DataGridViewColumn { Visible = value };

            Assert.Equal (value, column.Visible);

            // Set same.
            column.Visible = value;
            Assert.Equal (value, column.Visible);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Frozen_Set_GetReturnsExpected (bool value)
        {
            var column = new DataGridViewColumn { Frozen = value };

            Assert.Equal (value, column.Frozen);

            // Set same.
            column.Frozen = value;
            Assert.Equal (value, column.Frozen);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ReadOnly_Set_GetReturnsExpected (bool value)
        {
            var column = new DataGridViewColumn { ReadOnly = value };

            Assert.Equal (value, column.ReadOnly);

            // Set same.
            column.ReadOnly = value;
            Assert.Equal (value, column.ReadOnly);
        }

        [Theory]
        [InlineData (DataGridViewColumnSortMode.NotSortable)]
        [InlineData (DataGridViewColumnSortMode.Automatic)]
        [InlineData (DataGridViewColumnSortMode.Programmatic)]
        public void SortMode_Set_GetReturnsExpected (DataGridViewColumnSortMode value)
        {
            var column = new DataGridViewColumn { SortMode = value };

            Assert.Equal (value, column.SortMode);
        }

        [Theory]
        [InlineData (DataGridViewTriState.NotSet)]
        [InlineData (DataGridViewTriState.True)]
        [InlineData (DataGridViewTriState.False)]
        public void Resizable_Set_GetReturnsExpected (DataGridViewTriState value)
        {
            var column = new DataGridViewColumn { Resizable = value };

            Assert.Equal (value, column.Resizable);
        }

        [Theory]
        [InlineData (1f)]
        [InlineData (100f)]
        [InlineData (200.5f)]
        public void FillWeight_Set_GetReturnsExpected (float value)
        {
            var column = new DataGridViewColumn { FillWeight = value };

            Assert.Equal (value, column.FillWeight);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (4)]
        [InlineData (100)]
        public void DividerWidth_Set_GetReturnsExpected (int value)
        {
            var column = new DataGridViewColumn { DividerWidth = value };

            Assert.Equal (value, column.DividerWidth);
        }

        [Theory]
        [InlineData (DataGridViewAutoSizeColumnMode.None)]
        [InlineData (DataGridViewAutoSizeColumnMode.AllCells)]
        [InlineData (DataGridViewAutoSizeColumnMode.Fill)]
        public void AutoSizeMode_Set_GetReturnsExpected (DataGridViewAutoSizeColumnMode value)
        {
            var column = new DataGridViewColumn { AutoSizeMode = value };

            Assert.Equal (value, column.AutoSizeMode);
        }

        [Fact]
        public void Tag_Set_GetReturnsExpected ()
        {
            var tag = new object ();
            var column = new DataGridViewColumn { Tag = tag };

            Assert.Same (tag, column.Tag);

            column.Tag = null;
            Assert.Null (column.Tag);
        }

        [Fact]
        public void DefaultCellStyle_Set_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle ();
            var column = new DataGridViewColumn { DefaultCellStyle = style };

            Assert.Same (style, column.DefaultCellStyle);
        }

        [Fact]
        public void DefaultCellStyle_SetNull_CoalescesToNewStyle ()
        {
            var column = new DataGridViewColumn ();
            var original = column.DefaultCellStyle;

            column.DefaultCellStyle = null!;

            // Setting null replaces it with a fresh non-null style rather than storing null.
            Assert.NotNull (column.DefaultCellStyle);
            Assert.NotSame (original, column.DefaultCellStyle);
        }

        [Fact]
        public void CellTemplate_Set_GetReturnsExpected ()
        {
            var template = new DataGridViewCell ();
            var column = new DataGridViewColumn { CellTemplate = template };

            Assert.Same (template, column.CellTemplate);

            column.CellTemplate = null;
            Assert.Null (column.CellTemplate);
        }

        [Fact]
        public void AddToColumns_SetsIndexAndDataGridView ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ();

            control.Columns.Add (column);

            Assert.Same (control, column.DataGridView);
            Assert.Equal (0, column.Index);
            Assert.Equal (0, column.DisplayIndex);
            Assert.Same (column, Assert.Single (control.Columns));
        }

        [Fact]
        public void AddMultipleToColumns_SetsSequentialIndexes ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            var column3 = new DataGridViewColumn ();

            control.Columns.Add (column1);
            control.Columns.Add (column2);
            control.Columns.Add (column3);

            Assert.Equal (0, column1.Index);
            Assert.Equal (1, column2.Index);
            Assert.Equal (2, column3.Index);
        }

        [Fact]
        public void RemoveFromColumns_ResetsIndexAndDataGridView ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ();
            control.Columns.Add (column);

            control.Columns.Remove (column);

            Assert.Null (column.DataGridView);
            Assert.Equal (-1, column.Index);
        }

        [Fact]
        public void ClearColumns_ResetsIndexAndDataGridView ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ();
            control.Columns.Add (column);

            control.Columns.Clear ();

            Assert.Null (column.DataGridView);
            Assert.Equal (-1, column.Index);
            Assert.Empty (control.Columns);
        }

        [Fact]
        public void Columns_AddHeaderText_ReturnsColumnWithHeaderText ()
        {
            using var control = new DataGridView ();

            var column = control.Columns.Add ("MyHeader");

            Assert.Equal ("MyHeader", column.HeaderText);
            Assert.Same (control, column.DataGridView);
            Assert.Equal (0, column.Index);
        }

        [Fact]
        public void Columns_AddNameAndHeaderText_ReturnsConfiguredColumn ()
        {
            using var control = new DataGridView ();

            var column = control.Columns.Add ("colName", "Header");

            Assert.Equal ("colName", column.Name);
            Assert.Equal ("Header", column.HeaderText);
        }

        [Fact]
        public void Columns_AddHeaderTextAndWidth_ReturnsConfiguredColumn ()
        {
            using var control = new DataGridView ();

            var column = control.Columns.Add ("Header", 150);

            Assert.Equal ("Header", column.HeaderText);
            Assert.Equal (150, column.Width);
        }

        [Fact]
        public void Columns_IndexerByName_FindsByNameOrHeaderText ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ("HeaderValue") { Name = "NameValue" };
            control.Columns.Add (column);

            Assert.Same (column, control.Columns["NameValue"]);
            Assert.Same (column, control.Columns["nameVALUE"]);
            Assert.Same (column, control.Columns["HeaderValue"]);
            Assert.Null (control.Columns["does-not-exist"]);
            Assert.True (control.Columns.Contains ("NameValue"));
            Assert.False (control.Columns.Contains ("nope"));
        }
    }
}
