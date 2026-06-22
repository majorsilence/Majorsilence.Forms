// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/PaddingTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class PaddingTests
    {
        [Fact]
        public void Empty_IsAllZero ()
        {
            var p = Padding.Empty;
            Assert.Equal (0, p.All);
            Assert.Equal (0, p.Left);
            Assert.Equal (0, p.Top);
            Assert.Equal (0, p.Right);
            Assert.Equal (0, p.Bottom);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        [InlineData (-3)]
        public void Ctor_All_SetsAllSides (int all)
        {
            var p = new Padding (all);

            Assert.Equal (all, p.All);
            Assert.Equal (all, p.Left);
            Assert.Equal (all, p.Top);
            Assert.Equal (all, p.Right);
            Assert.Equal (all, p.Bottom);
            Assert.Equal (all * 2, p.Horizontal);
            Assert.Equal (all * 2, p.Vertical);
        }

        [Fact]
        public void Ctor_FourSides_SetsEachSide ()
        {
            var p = new Padding (1, 2, 3, 4);

            Assert.Equal (1, p.Left);
            Assert.Equal (2, p.Top);
            Assert.Equal (3, p.Right);
            Assert.Equal (4, p.Bottom);
        }

        [Fact]
        public void All_ReturnsMinusOne_WhenSidesDiffer ()
        {
            var p = new Padding (1, 2, 3, 4);
            Assert.Equal (-1, p.All);
        }

        [Fact]
        public void All_ReturnsValue_WhenSidesEqual ()
        {
            var p = new Padding (7, 7, 7, 7);
            Assert.Equal (7, p.All);
        }

        [Fact]
        public void All_Set_OverwritesAllSides ()
        {
            var p = new Padding (1, 2, 3, 4) { All = 9 };

            Assert.Equal (9, p.All);
            Assert.Equal (9, p.Left);
            Assert.Equal (9, p.Top);
            Assert.Equal (9, p.Right);
            Assert.Equal (9, p.Bottom);
        }

        [Fact]
        public void Horizontal_IsLeftPlusRight ()
        {
            var p = new Padding (3, 5, 7, 9);
            Assert.Equal (10, p.Horizontal);   // 3 + 7
        }

        [Fact]
        public void Vertical_IsTopPlusBottom ()
        {
            var p = new Padding (3, 5, 7, 9);
            Assert.Equal (14, p.Vertical);     // 5 + 9
        }

        [Fact]
        public void Size_IsHorizontalByVertical ()
        {
            var p = new Padding (3, 5, 7, 9);
            Assert.Equal (new Size (10, 14), p.Size);
        }

        [Fact]
        public void Sides_AreSettable ()
        {
            var p = new Padding (0) { Left = 1, Top = 2, Right = 3, Bottom = 4 };

            Assert.Equal (1, p.Left);
            Assert.Equal (2, p.Top);
            Assert.Equal (3, p.Right);
            Assert.Equal (4, p.Bottom);
        }

        [Fact]
        public void Equality_EqualValues ()
        {
            var a = new Padding (1, 2, 3, 4);
            var b = new Padding (1, 2, 3, 4);

            Assert.True (a == b);
            Assert.False (a != b);
            Assert.True (a.Equals (b));
            Assert.Equal (a.GetHashCode (), b.GetHashCode ());
        }

        [Fact]
        public void Equality_DifferentValues ()
        {
            var a = new Padding (1, 2, 3, 4);
            var b = new Padding (4, 3, 2, 1);

            Assert.False (a == b);
            Assert.True (a != b);
            Assert.False (a.Equals (b));
        }

        [Fact]
        public void Equals_NonPadding_ReturnsFalse ()
        {
            var a = new Padding (1);
            Assert.False (a.Equals ("not a padding"));
        }

        [Fact]
        public void ToString_ReturnsAllSides ()
        {
            var p = new Padding (1, 2, 3, 4);
            Assert.Equal ("{Left=1,Top=2,Right=3,Bottom=4}", p.ToString ());
        }
    }
}
