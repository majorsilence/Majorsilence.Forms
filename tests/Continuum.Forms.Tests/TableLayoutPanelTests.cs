// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/TableLayoutPanelTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms TableLayoutPanelTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility/Moq plumbing). They pin the
    // ColumnCount/RowCount, GrowStyle, CellBorderStyle, ColumnStyles/RowStyles collection
    // semantics, and the Get/Set Column/Row/ColumnSpan/RowSpan/CellPosition extender behavior.
    public class TableLayoutPanelTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new TableLayoutPanel ();

            Assert.Equal (0, control.ColumnCount);
            Assert.Equal (0, control.RowCount);
            Assert.Equal (TableLayoutPanelGrowStyle.AddRows, control.GrowStyle);
            Assert.Equal (TableLayoutPanelCellBorderStyle.None, control.CellBorderStyle);
            Assert.NotNull (control.ColumnStyles);
            Assert.NotNull (control.RowStyles);
            Assert.Empty (control.ColumnStyles);
            Assert.Empty (control.RowStyles);
        }

        #region ColumnCount

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (10)]   // MF lays out eagerly on set, so use an allocatable count (WinForms defers layout, allowing int.MaxValue)
        public void ColumnCount_Set_GetReturnsExpected (int value)
        {
            using var control = new TableLayoutPanel { ColumnCount = value };
            Assert.Equal (value, control.ColumnCount);

            // Set same.
            control.ColumnCount = value;
            Assert.Equal (value, control.ColumnCount);
        }

        [Fact]
        public void ColumnCount_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.ColumnCount = -1);
        }

        #endregion

        #region RowCount

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (10)]   // MF lays out eagerly on set, so use an allocatable count (WinForms defers layout, allowing int.MaxValue)
        public void RowCount_Set_GetReturnsExpected (int value)
        {
            using var control = new TableLayoutPanel { RowCount = value };
            Assert.Equal (value, control.RowCount);

            // Set same.
            control.RowCount = value;
            Assert.Equal (value, control.RowCount);
        }

        [Fact]
        public void RowCount_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.RowCount = -1);
        }

        #endregion

        #region GrowStyle

        [Theory]
        [InlineData (TableLayoutPanelGrowStyle.FixedSize)]
        [InlineData (TableLayoutPanelGrowStyle.AddRows)]
        [InlineData (TableLayoutPanelGrowStyle.AddColumns)]
        public void GrowStyle_Set_GetReturnsExpected (TableLayoutPanelGrowStyle value)
        {
            using var control = new TableLayoutPanel { GrowStyle = value };
            Assert.Equal (value, control.GrowStyle);

            // Set same.
            control.GrowStyle = value;
            Assert.Equal (value, control.GrowStyle);
        }

        #endregion

        #region CellBorderStyle

        [Theory]
        [InlineData (TableLayoutPanelCellBorderStyle.None)]
        [InlineData (TableLayoutPanelCellBorderStyle.Single)]
        [InlineData (TableLayoutPanelCellBorderStyle.Inset)]
        [InlineData (TableLayoutPanelCellBorderStyle.InsetDouble)]
        [InlineData (TableLayoutPanelCellBorderStyle.Outset)]
        [InlineData (TableLayoutPanelCellBorderStyle.OutsetDouble)]
        [InlineData (TableLayoutPanelCellBorderStyle.OutsetPartial)]
        public void CellBorderStyle_Set_GetReturnsExpected (TableLayoutPanelCellBorderStyle value)
        {
            using var control = new TableLayoutPanel { CellBorderStyle = value };
            Assert.Equal (value, control.CellBorderStyle);

            // Set same.
            control.CellBorderStyle = value;
            Assert.Equal (value, control.CellBorderStyle);
        }

        [Theory]
        [InlineData ((TableLayoutPanelCellBorderStyle)(-1))]
        [InlineData ((TableLayoutPanelCellBorderStyle)7)]
        public void CellBorderStyle_SetInvalid_ThrowsInvalidEnumArgumentException (TableLayoutPanelCellBorderStyle value)
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.CellBorderStyle = value);
        }

        #endregion

        #region ColumnStyles / RowStyles

        [Fact]
        public void ColumnStyles_Add_IncreasesCount ()
        {
            using var control = new TableLayoutPanel ();
            var style = new ColumnStyle (SizeType.Percent, 50);

            var index = control.ColumnStyles.Add (style);

            Assert.Equal (0, index);
            Assert.Equal (1, control.ColumnStyles.Count);
            Assert.Same (style, control.ColumnStyles[0]);
            Assert.True (control.ColumnStyles.Contains (style));
            Assert.Equal (0, control.ColumnStyles.IndexOf (style));
        }

        [Fact]
        public void ColumnStyles_Remove_DecreasesCount ()
        {
            using var control = new TableLayoutPanel ();
            var style = new ColumnStyle ();
            control.ColumnStyles.Add (style);

            control.ColumnStyles.Remove (style);

            Assert.Equal (0, control.ColumnStyles.Count);
            Assert.False (control.ColumnStyles.Contains (style));
        }

        [Fact]
        public void RowStyles_Add_IncreasesCount ()
        {
            using var control = new TableLayoutPanel ();
            var style = new RowStyle (SizeType.Absolute, 20);

            var index = control.RowStyles.Add (style);

            Assert.Equal (0, index);
            Assert.Equal (1, control.RowStyles.Count);
            Assert.Same (style, control.RowStyles[0]);
            Assert.True (control.RowStyles.Contains (style));
            Assert.Equal (0, control.RowStyles.IndexOf (style));
        }

        [Fact]
        public void RowStyles_Remove_DecreasesCount ()
        {
            using var control = new TableLayoutPanel ();
            var style = new RowStyle ();
            control.RowStyles.Add (style);

            control.RowStyles.Remove (style);

            Assert.Equal (0, control.RowStyles.Count);
            Assert.False (control.RowStyles.Contains (style));
        }

        #endregion

        #region Column

        [Fact]
        public void GetColumn_NoSuchControl_ReturnsNegativeOne ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();

            Assert.Equal (-1, control.GetColumn (child));
        }

        [Fact]
        public void GetColumn_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.GetColumn (null!));
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (5)]
        public void SetColumn_Invoke_GetReturnsExpected (int value)
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            control.SetColumn (child, value);
            Assert.Equal (value, control.GetColumn (child));

            // Set same.
            control.SetColumn (child, value);
            Assert.Equal (value, control.GetColumn (child));
        }

        [Fact]
        public void SetColumn_LessThanNegativeOne_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SetColumn (child, -2));
        }

        [Fact]
        public void SetColumn_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.SetColumn (null!, 1));
        }

        #endregion

        #region Row

        [Fact]
        public void GetRow_NoSuchControl_ReturnsNegativeOne ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();

            Assert.Equal (-1, control.GetRow (child));
        }

        [Fact]
        public void GetRow_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.GetRow (null!));
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (5)]
        public void SetRow_Invoke_GetReturnsExpected (int value)
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            control.SetRow (child, value);
            Assert.Equal (value, control.GetRow (child));

            // Set same.
            control.SetRow (child, value);
            Assert.Equal (value, control.GetRow (child));
        }

        [Fact]
        public void SetRow_LessThanNegativeOne_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SetRow (child, -2));
        }

        [Fact]
        public void SetRow_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.SetRow (null!, 1));
        }

        #endregion

        #region ColumnSpan

        [Fact]
        public void GetColumnSpan_NoSuchControl_ReturnsOne ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();

            Assert.Equal (1, control.GetColumnSpan (child));
        }

        [Fact]
        public void GetColumnSpan_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.GetColumnSpan (null!));
        }

        [Theory]
        [InlineData (1)]
        [InlineData (5)]
        public void SetColumnSpan_Invoke_GetReturnsExpected (int value)
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            control.SetColumnSpan (child, value);
            Assert.Equal (value, control.GetColumnSpan (child));
        }

        [Theory]
        [InlineData (0)]
        [InlineData (-1)]
        public void SetColumnSpan_LessThanOne_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SetColumnSpan (child, value));
        }

        [Fact]
        public void SetColumnSpan_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.SetColumnSpan (null!, 1));
        }

        #endregion

        #region RowSpan

        [Fact]
        public void GetRowSpan_NoSuchControl_ReturnsOne ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();

            Assert.Equal (1, control.GetRowSpan (child));
        }

        [Fact]
        public void GetRowSpan_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.GetRowSpan (null!));
        }

        [Theory]
        [InlineData (1)]
        [InlineData (5)]
        public void SetRowSpan_Invoke_GetReturnsExpected (int value)
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            control.SetRowSpan (child, value);
            Assert.Equal (value, control.GetRowSpan (child));
        }

        [Theory]
        [InlineData (0)]
        [InlineData (-1)]
        public void SetRowSpan_LessThanOne_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SetRowSpan (child, value));
        }

        [Fact]
        public void SetRowSpan_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.SetRowSpan (null!, 1));
        }

        #endregion

        #region CellPosition

        [Fact]
        public void GetCellPosition_NoSuchControl_ReturnsNegativeOneNegativeOne ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();

            Assert.Equal (new TableLayoutPanelCellPosition (-1, -1), control.GetCellPosition (child));
        }

        [Fact]
        public void GetCellPosition_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.GetCellPosition (null!));
        }

        [Fact]
        public void SetCellPosition_Invoke_GetReturnsExpected ()
        {
            using var control = new TableLayoutPanel ();
            using var child = new Control ();
            control.Controls.Add (child);

            var position = new TableLayoutPanelCellPosition (3, 4);
            control.SetCellPosition (child, position);

            Assert.Equal (position, control.GetCellPosition (child));
            Assert.Equal (3, control.GetColumn (child));
            Assert.Equal (4, control.GetRow (child));
        }

        [Fact]
        public void SetCellPosition_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new TableLayoutPanel ();
            Assert.Throws<ArgumentNullException> (() => control.SetCellPosition (null!, new TableLayoutPanelCellPosition (0, 0)));
        }

        #endregion
    }
}
