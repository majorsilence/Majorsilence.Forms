// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (ColumnStyleTests.cs, RowStyleTests.cs,
// TableLayoutPanelCellPositionTests.cs under src/test/unit/System.Windows.Forms/System/Windows/Forms/),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class ColumnStyleTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var style = new ColumnStyle ();
            Assert.Equal (SizeType.AutoSize, style.SizeType);
            Assert.Equal (0, style.Width);
        }

        [Theory]
        [InlineData (SizeType.AutoSize)]
        [InlineData (SizeType.Absolute)]
        [InlineData (SizeType.Percent)]
        public void Ctor_SizeType (SizeType sizeType)
        {
            var style = new ColumnStyle (sizeType);
            Assert.Equal (sizeType, style.SizeType);
            Assert.Equal (0, style.Width);
        }

        [Theory]
        [InlineData (SizeType.Absolute, 0)]
        [InlineData (SizeType.Percent, 1)]
        [InlineData (SizeType.AutoSize, 100)]
        public void Ctor_SizeTypeWidth (SizeType sizeType, float width)
        {
            var style = new ColumnStyle (sizeType, width);
            Assert.Equal (sizeType, style.SizeType);
            Assert.Equal (width, style.Width);
        }

        [Fact]
        public void Width_GetSet ()
        {
            var style = new ColumnStyle { Width = 10 };
            Assert.Equal (10, style.Width);
        }

        [Fact]
        public void Width_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            var style = new ColumnStyle ();
            Assert.Throws<ArgumentOutOfRangeException> (() => style.Width = -1);
        }

        [Fact]
        public void SizeType_GetSet ()
        {
            var style = new ColumnStyle { SizeType = SizeType.Percent };
            Assert.Equal (SizeType.Percent, style.SizeType);
        }
    }

    public class RowStyleTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var style = new RowStyle ();
            Assert.Equal (SizeType.AutoSize, style.SizeType);
            Assert.Equal (0, style.Height);
        }

        [Theory]
        [InlineData (SizeType.AutoSize)]
        [InlineData (SizeType.Absolute)]
        [InlineData (SizeType.Percent)]
        public void Ctor_SizeType (SizeType sizeType)
        {
            var style = new RowStyle (sizeType);
            Assert.Equal (sizeType, style.SizeType);
            Assert.Equal (0, style.Height);
        }

        [Theory]
        [InlineData (SizeType.Absolute, 0)]
        [InlineData (SizeType.Percent, 1)]
        [InlineData (SizeType.AutoSize, 100)]
        public void Ctor_SizeTypeHeight (SizeType sizeType, float height)
        {
            var style = new RowStyle (sizeType, height);
            Assert.Equal (sizeType, style.SizeType);
            Assert.Equal (height, style.Height);
        }

        [Fact]
        public void Height_GetSet ()
        {
            var style = new RowStyle { Height = 10 };
            Assert.Equal (10, style.Height);
        }

        [Fact]
        public void Height_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            var style = new RowStyle ();
            Assert.Throws<ArgumentOutOfRangeException> (() => style.Height = -1);
        }
    }

    public class TableLayoutPanelCellPositionTests
    {
        [Fact]
        public void Ctor_Default_IsZeroZero ()
        {
            var pos = default (TableLayoutPanelCellPosition);
            Assert.Equal (0, pos.Column);
            Assert.Equal (0, pos.Row);
        }

        [Theory]
        [InlineData (0, 0)]
        [InlineData (1, 2)]
        [InlineData (-1, -1)]
        public void Ctor_ColumnRow (int column, int row)
        {
            var pos = new TableLayoutPanelCellPosition (column, row);
            Assert.Equal (column, pos.Column);
            Assert.Equal (row, pos.Row);
        }

        [Fact]
        public void Ctor_RowLessThanMinusOne_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new TableLayoutPanelCellPosition (0, -2));
        }

        [Fact]
        public void Ctor_ColumnLessThanMinusOne_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new TableLayoutPanelCellPosition (-2, 0));
        }

        [Fact]
        public void Row_Column_AreSettable ()
        {
            var pos = new TableLayoutPanelCellPosition (1, 1) { Row = 5, Column = 9 };
            Assert.Equal (5, pos.Row);
            Assert.Equal (9, pos.Column);
        }

        [Fact]
        public void Equality_EqualValues ()
        {
            var a = new TableLayoutPanelCellPosition (1, 2);
            var b = new TableLayoutPanelCellPosition (1, 2);

            Assert.True (a == b);
            Assert.False (a != b);
            Assert.True (a.Equals (b));
            Assert.Equal (a.GetHashCode (), b.GetHashCode ());
        }

        [Fact]
        public void Equality_DifferentValues ()
        {
            var a = new TableLayoutPanelCellPosition (1, 2);
            var b = new TableLayoutPanelCellPosition (3, 4);

            Assert.False (a == b);
            Assert.True (a != b);
            Assert.False (a.Equals (b));
        }

        [Fact]
        public void Equals_NonCellPosition_ReturnsFalse ()
        {
            var a = new TableLayoutPanelCellPosition (1, 2);
            Assert.False (a.Equals ("not a position"));
        }

        [Fact]
        public void ToString_ReturnsColumnCommaRow ()
        {
            var pos = new TableLayoutPanelCellPosition (3, 7);
            Assert.Equal ("3,7", pos.ToString ());
        }
    }
}
