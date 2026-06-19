// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DataGridViewCellStyleTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using System.Globalization;
using Modern.Forms;
using Xunit;
using Font = Modern.Drawing.Font;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DataGridViewCellStyleTests, adapted to
    // the Modern.Forms compat API (DataGridViewCellStyle lives in DataGridViewCompat.cs). They pin
    // ctor defaults, property get/set round-trips, the IsXxxDefault flags, Padding clamping, and the
    // copy-constructor / ApplyStyle / Clone / Equals semantics. Upstream-only concerns (Scope flags
    // beyond None, designer/serialization, ToString formatting, InvalidEnumArgumentException) are
    // omitted because the Modern.Forms compat layer does not implement them.
    public class DataGridViewCellStyleTests
    {
        private static Font CreateFont () => new Font ("Arial", 12f);

        [Fact]
        public void Ctor_Default ()
        {
            var style = new DataGridViewCellStyle ();

            Assert.Equal (DataGridViewCellStyleScopes.None, style.Scope);
            Assert.Equal (DataGridViewContentAlignment.NotSet, style.Alignment);
            Assert.Equal (Color.Empty, style.BackColor);
            Assert.Equal (DBNull.Value, style.DataSourceNullValue);
            Assert.Null (style.Font);
            Assert.Equal (Color.Empty, style.ForeColor);
            Assert.Equal (string.Empty, style.Format);
            Assert.Equal (CultureInfo.CurrentCulture, style.FormatProvider);
            Assert.True (style.IsDataSourceNullValueDefault);
            Assert.True (style.IsFormatProviderDefault);
            Assert.True (style.IsNullValueDefault);
            Assert.Equal (string.Empty, style.NullValue);
            Assert.Equal (Padding.Empty, style.Padding);
            Assert.Equal (Color.Empty, style.SelectionBackColor);
            Assert.Equal (Color.Empty, style.SelectionForeColor);
            Assert.Null (style.Tag);
            Assert.Equal (DataGridViewTriState.NotSet, style.WrapMode);
        }

        [Fact]
        public void Ctor_NonEmptyDataGridViewCellStyle_Success ()
        {
            var formatProvider = new NumberFormatInfo ();
            var font = CreateFont ();
            var source = new DataGridViewCellStyle {
                Alignment = DataGridViewContentAlignment.BottomCenter,
                BackColor = Color.Red,
                DataSourceNullValue = "dbNull",
                Font = font,
                ForeColor = Color.Blue,
                Format = "format",
                FormatProvider = formatProvider,
                NullValue = "null",
                Padding = new Padding (1, 2, 3, 4),
                SelectionBackColor = Color.Green,
                SelectionForeColor = Color.Yellow,
                Tag = "tag",
                WrapMode = DataGridViewTriState.True
            };
            var style = new DataGridViewCellStyle (source);

            Assert.Equal (DataGridViewCellStyleScopes.None, style.Scope);
            Assert.Equal (DataGridViewContentAlignment.BottomCenter, style.Alignment);
            Assert.Equal (Color.Red, style.BackColor);
            Assert.Equal ("dbNull", style.DataSourceNullValue);
            Assert.Equal (font, style.Font);
            Assert.Equal (Color.Blue, style.ForeColor);
            Assert.Equal ("format", style.Format);
            Assert.Equal (formatProvider, style.FormatProvider);
            Assert.False (style.IsDataSourceNullValueDefault);
            Assert.False (style.IsFormatProviderDefault);
            Assert.False (style.IsNullValueDefault);
            Assert.Equal ("null", style.NullValue);
            Assert.Equal (new Padding (1, 2, 3, 4), style.Padding);
            Assert.Equal (Color.Green, style.SelectionBackColor);
            Assert.Equal (Color.Yellow, style.SelectionForeColor);
            Assert.Equal ("tag", style.Tag);
            Assert.Equal (DataGridViewTriState.True, style.WrapMode);
        }

        [Fact]
        public void Ctor_EmptyDataGridViewCellStyle_Success ()
        {
            var source = new DataGridViewCellStyle ();
            var style = new DataGridViewCellStyle (source);

            Assert.Equal (DataGridViewCellStyleScopes.None, style.Scope);
            Assert.Equal (DataGridViewContentAlignment.NotSet, style.Alignment);
            Assert.Equal (Color.Empty, style.BackColor);
            Assert.Equal (DBNull.Value, style.DataSourceNullValue);
            Assert.Null (style.Font);
            Assert.Equal (Color.Empty, style.ForeColor);
            Assert.Equal (string.Empty, style.Format);
            Assert.Equal (CultureInfo.CurrentCulture, style.FormatProvider);
            Assert.True (style.IsDataSourceNullValueDefault);
            Assert.True (style.IsFormatProviderDefault);
            Assert.True (style.IsNullValueDefault);
            Assert.Equal (string.Empty, style.NullValue);
            Assert.Equal (Padding.Empty, style.Padding);
            Assert.Equal (Color.Empty, style.SelectionBackColor);
            Assert.Equal (Color.Empty, style.SelectionForeColor);
            Assert.Null (style.Tag);
            Assert.Equal (DataGridViewTriState.NotSet, style.WrapMode);
        }

        [Fact]
        public void Ctor_NullDataGridViewCellStyle_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException> (() => new DataGridViewCellStyle (null!));
        }

        [Theory]
        [InlineData (DataGridViewContentAlignment.NotSet)]
        [InlineData (DataGridViewContentAlignment.TopLeft)]
        [InlineData (DataGridViewContentAlignment.TopCenter)]
        [InlineData (DataGridViewContentAlignment.TopRight)]
        [InlineData (DataGridViewContentAlignment.MiddleLeft)]
        [InlineData (DataGridViewContentAlignment.MiddleCenter)]
        [InlineData (DataGridViewContentAlignment.MiddleRight)]
        [InlineData (DataGridViewContentAlignment.BottomLeft)]
        [InlineData (DataGridViewContentAlignment.BottomCenter)]
        [InlineData (DataGridViewContentAlignment.BottomRight)]
        public void Alignment_Set_GetReturnsExpected (DataGridViewContentAlignment value)
        {
            var style = new DataGridViewCellStyle { Alignment = value };
            Assert.Equal (value, style.Alignment);

            // Set same.
            style.Alignment = value;
            Assert.Equal (value, style.Alignment);
        }

        [Fact]
        public void BackColor_Set_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { BackColor = Color.Red };
            Assert.Equal (Color.Red, style.BackColor);

            // Set same.
            style.BackColor = Color.Red;
            Assert.Equal (Color.Red, style.BackColor);
        }

        [Fact]
        public void BackColor_SetEmpty_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { BackColor = Color.Red };
            style.BackColor = Color.Empty;
            Assert.Equal (Color.Empty, style.BackColor);
        }

        [Fact]
        public void DataSourceNullValue_SetValue_NotDefault ()
        {
            var style = new DataGridViewCellStyle { DataSourceNullValue = "value" };
            Assert.Equal ("value", style.DataSourceNullValue);
            Assert.False (style.IsDataSourceNullValueDefault);

            // Set same.
            style.DataSourceNullValue = "value";
            Assert.Equal ("value", style.DataSourceNullValue);
            Assert.False (style.IsDataSourceNullValueDefault);
        }

        [Fact]
        public void DataSourceNullValue_SetNull_NotDefault ()
        {
            var style = new DataGridViewCellStyle { DataSourceNullValue = null };
            Assert.Null (style.DataSourceNullValue);
            Assert.False (style.IsDataSourceNullValueDefault);
        }

        [Fact]
        public void DataSourceNullValue_SetDBNull_IsDefault ()
        {
            var style = new DataGridViewCellStyle { DataSourceNullValue = "value" };
            style.DataSourceNullValue = DBNull.Value;
            Assert.Equal (DBNull.Value, style.DataSourceNullValue);
            Assert.True (style.IsDataSourceNullValueDefault);
        }

        [Fact]
        public void Font_Set_GetReturnsExpected ()
        {
            var font = CreateFont ();
            var style = new DataGridViewCellStyle { Font = font };
            Assert.Equal (font, style.Font);

            // Set same.
            style.Font = font;
            Assert.Equal (font, style.Font);
        }

        [Fact]
        public void Font_SetNull_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { Font = CreateFont () };
            style.Font = null;
            Assert.Null (style.Font);
        }

        [Fact]
        public void ForeColor_Set_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { ForeColor = Color.Red };
            Assert.Equal (Color.Red, style.ForeColor);

            // Set same.
            style.ForeColor = Color.Red;
            Assert.Equal (Color.Red, style.ForeColor);
        }

        [Fact]
        public void ForeColor_SetEmpty_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { ForeColor = Color.Red };
            style.ForeColor = Color.Empty;
            Assert.Equal (Color.Empty, style.ForeColor);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("format", "format")]
        public void Format_Set_GetReturnsExpected (string? value, string expected)
        {
            var style = new DataGridViewCellStyle { Format = value! };
            Assert.Equal (expected, style.Format);

            // Set same.
            style.Format = value!;
            Assert.Equal (expected, style.Format);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("format", "format")]
        public void Format_SetWithNonNullOldValue_GetReturnsExpected (string? value, string expected)
        {
            var style = new DataGridViewCellStyle { Format = "value" };
            style.Format = value!;
            Assert.Equal (expected, style.Format);
        }

        [Fact]
        public void FormatProvider_SetValue_NotDefault ()
        {
            var formatProvider = new NumberFormatInfo ();
            var style = new DataGridViewCellStyle { FormatProvider = formatProvider };
            Assert.Equal (formatProvider, style.FormatProvider);
            Assert.False (style.IsFormatProviderDefault);

            // Set same.
            style.FormatProvider = formatProvider;
            Assert.Equal (formatProvider, style.FormatProvider);
            Assert.False (style.IsFormatProviderDefault);
        }

        [Fact]
        public void FormatProvider_SetNull_GetReturnsDefault ()
        {
            var style = new DataGridViewCellStyle { FormatProvider = new NumberFormatInfo () };
            style.FormatProvider = null!;
            Assert.Equal (CultureInfo.CurrentCulture, style.FormatProvider);
            Assert.True (style.IsFormatProviderDefault);
        }

        [Fact]
        public void NullValue_SetValue_NotDefault ()
        {
            var style = new DataGridViewCellStyle { NullValue = "value" };
            Assert.Equal ("value", style.NullValue);
            Assert.False (style.IsNullValueDefault);

            // Set same.
            style.NullValue = "value";
            Assert.Equal ("value", style.NullValue);
            Assert.False (style.IsNullValueDefault);
        }

        [Fact]
        public void NullValue_SetNull_NotDefault ()
        {
            var style = new DataGridViewCellStyle { NullValue = null };
            Assert.Null (style.NullValue);
            Assert.False (style.IsNullValueDefault);
        }

        [Fact]
        public void NullValue_SetDBNull_NotDefault ()
        {
            var style = new DataGridViewCellStyle { NullValue = DBNull.Value };
            Assert.Equal (DBNull.Value, style.NullValue);
            Assert.False (style.IsNullValueDefault);
        }

        [Fact]
        public void NullValue_SetEmpty_IsDefault ()
        {
            var style = new DataGridViewCellStyle { NullValue = "value" };
            style.NullValue = string.Empty;
            Assert.Equal (string.Empty, style.NullValue);
            Assert.True (style.IsNullValueDefault);
        }

        [Theory]
        [InlineData (0, 0, 0, 0, 0, 0, 0, 0)]
        [InlineData (2, 2, 2, 2, 2, 2, 2, 2)]
        [InlineData (1, 2, 3, 4, 1, 2, 3, 4)]
        [InlineData (-1, -1, -1, -1, 0, 0, 0, 0)]
        [InlineData (-1, 2, 3, 4, 0, 2, 3, 4)]
        [InlineData (1, -2, 3, 4, 1, 0, 3, 4)]
        [InlineData (1, 2, -3, 4, 1, 2, 0, 4)]
        [InlineData (1, 2, 3, -4, 1, 2, 3, 0)]
        public void Padding_Set_GetReturnsExpected (int left, int top, int right, int bottom, int expectedLeft, int expectedTop, int expectedRight, int expectedBottom)
        {
            var value = new Padding (left, top, right, bottom);
            var expected = new Padding (expectedLeft, expectedTop, expectedRight, expectedBottom);
            var style = new DataGridViewCellStyle { Padding = value };
            Assert.Equal (expected, style.Padding);

            // Set same.
            style.Padding = value;
            Assert.Equal (expected, style.Padding);
        }

        [Fact]
        public void SelectionBackColor_Set_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { SelectionBackColor = Color.Red };
            Assert.Equal (Color.Red, style.SelectionBackColor);

            // Set same.
            style.SelectionBackColor = Color.Red;
            Assert.Equal (Color.Red, style.SelectionBackColor);
        }

        [Fact]
        public void SelectionBackColor_SetEmpty_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { SelectionBackColor = Color.Red };
            style.SelectionBackColor = Color.Empty;
            Assert.Equal (Color.Empty, style.SelectionBackColor);
        }

        [Fact]
        public void SelectionForeColor_Set_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { SelectionForeColor = Color.Red };
            Assert.Equal (Color.Red, style.SelectionForeColor);

            // Set same.
            style.SelectionForeColor = Color.Red;
            Assert.Equal (Color.Red, style.SelectionForeColor);
        }

        [Fact]
        public void SelectionForeColor_SetEmpty_GetReturnsExpected ()
        {
            var style = new DataGridViewCellStyle { SelectionForeColor = Color.Red };
            style.SelectionForeColor = Color.Empty;
            Assert.Equal (Color.Empty, style.SelectionForeColor);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("tag")]
        public void Tag_Set_GetReturnsExpected (object? value)
        {
            var style = new DataGridViewCellStyle { Tag = value };
            Assert.Same (value, style.Tag);

            // Set same.
            style.Tag = value;
            Assert.Same (value, style.Tag);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("tag")]
        public void Tag_SetWithNonNullOldValue_GetReturnsExpected (object? value)
        {
            var style = new DataGridViewCellStyle { Tag = "old" };
            style.Tag = value;
            Assert.Equal (value, style.Tag);
        }

        [Theory]
        [InlineData (DataGridViewTriState.NotSet)]
        [InlineData (DataGridViewTriState.True)]
        [InlineData (DataGridViewTriState.False)]
        public void WrapMode_Set_GetReturnsExpected (DataGridViewTriState value)
        {
            var style = new DataGridViewCellStyle { WrapMode = value };
            Assert.Equal (value, style.WrapMode);

            // Set same.
            style.WrapMode = value;
            Assert.Equal (value, style.WrapMode);
        }

        [Fact]
        public void ApplyStyle_NonEmptyDataGridViewCellStyle_Success ()
        {
            var formatProvider = new NumberFormatInfo ();
            var font = CreateFont ();
            var source = new DataGridViewCellStyle {
                Alignment = DataGridViewContentAlignment.BottomCenter,
                BackColor = Color.Red,
                DataSourceNullValue = "dbNull",
                Font = font,
                ForeColor = Color.Blue,
                Format = "format",
                FormatProvider = formatProvider,
                NullValue = "null",
                Padding = new Padding (1, 2, 3, 4),
                SelectionBackColor = Color.Green,
                SelectionForeColor = Color.Yellow,
                Tag = "tag",
                WrapMode = DataGridViewTriState.True
            };
            var style = new DataGridViewCellStyle ();
            style.ApplyStyle (source);

            Assert.Equal (DataGridViewContentAlignment.BottomCenter, style.Alignment);
            Assert.Equal (Color.Red, style.BackColor);
            Assert.Equal ("dbNull", style.DataSourceNullValue);
            Assert.Equal (font, style.Font);
            Assert.Equal (Color.Blue, style.ForeColor);
            Assert.Equal ("format", style.Format);
            Assert.Equal (formatProvider, style.FormatProvider);
            Assert.False (style.IsDataSourceNullValueDefault);
            Assert.False (style.IsFormatProviderDefault);
            Assert.False (style.IsNullValueDefault);
            Assert.Equal ("null", style.NullValue);
            Assert.Equal (new Padding (1, 2, 3, 4), style.Padding);
            Assert.Equal (Color.Green, style.SelectionBackColor);
            Assert.Equal (Color.Yellow, style.SelectionForeColor);
            Assert.Equal ("tag", style.Tag);
            Assert.Equal (DataGridViewTriState.True, style.WrapMode);
        }

        [Fact]
        public void ApplyStyle_EmptyDataGridViewCellStyle_Nop ()
        {
            var formatProvider = new NumberFormatInfo ();
            var font = CreateFont ();
            var source = new DataGridViewCellStyle ();
            var style = new DataGridViewCellStyle {
                Alignment = DataGridViewContentAlignment.BottomCenter,
                BackColor = Color.Red,
                DataSourceNullValue = "dbNull",
                Font = font,
                ForeColor = Color.Blue,
                Format = "format",
                FormatProvider = formatProvider,
                NullValue = "null",
                Padding = new Padding (1, 2, 3, 4),
                SelectionBackColor = Color.Green,
                SelectionForeColor = Color.Yellow,
                Tag = "tag",
                WrapMode = DataGridViewTriState.True
            };
            style.ApplyStyle (source);

            Assert.Equal (DataGridViewContentAlignment.BottomCenter, style.Alignment);
            Assert.Equal (Color.Red, style.BackColor);
            Assert.Equal ("dbNull", style.DataSourceNullValue);
            Assert.Equal (font, style.Font);
            Assert.Equal (Color.Blue, style.ForeColor);
            Assert.Equal ("format", style.Format);
            Assert.Equal (formatProvider, style.FormatProvider);
            Assert.Equal ("null", style.NullValue);
            Assert.Equal (new Padding (1, 2, 3, 4), style.Padding);
            Assert.Equal (Color.Green, style.SelectionBackColor);
            Assert.Equal (Color.Yellow, style.SelectionForeColor);
            Assert.Equal ("tag", style.Tag);
            Assert.Equal (DataGridViewTriState.True, style.WrapMode);
        }

        [Fact]
        public void ApplyStyle_NullDataGridViewCellStyle_ThrowsArgumentNullException ()
        {
            var style = new DataGridViewCellStyle ();
            Assert.Throws<ArgumentNullException> (() => style.ApplyStyle (null!));
        }

        [Fact]
        public void Clone_NonEmptyDataGridViewCellStyle_Success ()
        {
            var formatProvider = new NumberFormatInfo ();
            var font = CreateFont ();
            var source = new DataGridViewCellStyle {
                Alignment = DataGridViewContentAlignment.BottomCenter,
                BackColor = Color.Red,
                DataSourceNullValue = "dbNull",
                Font = font,
                ForeColor = Color.Blue,
                Format = "format",
                FormatProvider = formatProvider,
                NullValue = "null",
                Padding = new Padding (1, 2, 3, 4),
                SelectionBackColor = Color.Green,
                SelectionForeColor = Color.Yellow,
                Tag = "tag",
                WrapMode = DataGridViewTriState.True
            };
            var style = source.Clone ();

            Assert.Equal (DataGridViewContentAlignment.BottomCenter, style.Alignment);
            Assert.Equal (Color.Red, style.BackColor);
            Assert.Equal ("dbNull", style.DataSourceNullValue);
            Assert.Equal (font, style.Font);
            Assert.Equal (Color.Blue, style.ForeColor);
            Assert.Equal ("format", style.Format);
            Assert.Equal (formatProvider, style.FormatProvider);
            Assert.False (style.IsDataSourceNullValueDefault);
            Assert.False (style.IsFormatProviderDefault);
            Assert.False (style.IsNullValueDefault);
            Assert.Equal ("null", style.NullValue);
            Assert.Equal (new Padding (1, 2, 3, 4), style.Padding);
            Assert.Equal (Color.Green, style.SelectionBackColor);
            Assert.Equal (Color.Yellow, style.SelectionForeColor);
            Assert.Equal ("tag", style.Tag);
            Assert.Equal (DataGridViewTriState.True, style.WrapMode);
        }

        [Fact]
        public void Clone_EmptyDataGridViewCellStyle_Success ()
        {
            var source = new DataGridViewCellStyle ();
            var style = source.Clone ();

            Assert.Equal (DataGridViewContentAlignment.NotSet, style.Alignment);
            Assert.Equal (Color.Empty, style.BackColor);
            Assert.Equal (DBNull.Value, style.DataSourceNullValue);
            Assert.Null (style.Font);
            Assert.Equal (Color.Empty, style.ForeColor);
            Assert.Equal (string.Empty, style.Format);
            Assert.Equal (CultureInfo.CurrentCulture, style.FormatProvider);
            Assert.True (style.IsDataSourceNullValueDefault);
            Assert.True (style.IsFormatProviderDefault);
            Assert.True (style.IsNullValueDefault);
            Assert.Equal (string.Empty, style.NullValue);
            Assert.Equal (Padding.Empty, style.Padding);
            Assert.Equal (Color.Empty, style.SelectionBackColor);
            Assert.Equal (Color.Empty, style.SelectionForeColor);
            Assert.Null (style.Tag);
            Assert.Equal (DataGridViewTriState.NotSet, style.WrapMode);
        }

        [Fact]
        public void Clone_ReturnsIndependentInstance ()
        {
            var source = new DataGridViewCellStyle { BackColor = Color.Red };
            var clone = source.Clone ();
            Assert.NotSame (source, clone);

            clone.BackColor = Color.Blue;
            Assert.Equal (Color.Red, source.BackColor);
        }

        [Fact]
        public void ICloneableClone_NonEmptyDataGridViewCellStyle_Success ()
        {
            ICloneable source = new DataGridViewCellStyle {
                Alignment = DataGridViewContentAlignment.BottomCenter,
                BackColor = Color.Red,
                WrapMode = DataGridViewTriState.True
            };
            var style = Assert.IsType<DataGridViewCellStyle> (source.Clone ());

            Assert.Equal (DataGridViewContentAlignment.BottomCenter, style.Alignment);
            Assert.Equal (Color.Red, style.BackColor);
            Assert.Equal (DataGridViewTriState.True, style.WrapMode);
        }

        // Equals tests are written as plain Facts (not MemberData) because xunit.v3 cannot
        // serialize DataGridViewCellStyle theory arguments. Each asserts both Equals and, for
        // equal pairs, GetHashCode agreement (value-equality contract).
        private static void AssertEqual (DataGridViewCellStyle a, DataGridViewCellStyle b)
        {
            Assert.True (a.Equals (b));
            Assert.True (b.Equals (a));
            Assert.Equal (a.GetHashCode (), b.GetHashCode ());
        }

        private static void AssertNotEqual (DataGridViewCellStyle a, DataGridViewCellStyle b)
        {
            Assert.False (a.Equals (b));
            Assert.False (b.Equals (a));
        }

        [Fact]
        public void Equals_EmptyStyles_ReturnsTrue ()
        {
            AssertEqual (new DataGridViewCellStyle (), new DataGridViewCellStyle ());
        }

        [Fact]
        public void Equals_Alignment_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.BottomCenter },
                new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.BottomCenter });
            AssertNotEqual (
                new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.BottomCenter },
                new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.BottomRight });
        }

        [Fact]
        public void Equals_BackColor_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { BackColor = Color.Red },
                new DataGridViewCellStyle { BackColor = Color.Red });
            AssertNotEqual (
                new DataGridViewCellStyle { BackColor = Color.Red },
                new DataGridViewCellStyle { BackColor = Color.Blue });
        }

        [Fact]
        public void Equals_ForeColor_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { ForeColor = Color.Red },
                new DataGridViewCellStyle { ForeColor = Color.Red });
            AssertNotEqual (
                new DataGridViewCellStyle { ForeColor = Color.Red },
                new DataGridViewCellStyle { ForeColor = Color.Blue });
        }

        [Fact]
        public void Equals_SelectionBackColor_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { SelectionBackColor = Color.Red },
                new DataGridViewCellStyle { SelectionBackColor = Color.Red });
            AssertNotEqual (
                new DataGridViewCellStyle { SelectionBackColor = Color.Red },
                new DataGridViewCellStyle { SelectionBackColor = Color.Blue });
        }

        [Fact]
        public void Equals_SelectionForeColor_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { SelectionForeColor = Color.Red },
                new DataGridViewCellStyle { SelectionForeColor = Color.Red });
            AssertNotEqual (
                new DataGridViewCellStyle { SelectionForeColor = Color.Red },
                new DataGridViewCellStyle { SelectionForeColor = Color.Blue });
        }

        [Fact]
        public void Equals_Format_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { Format = "format" },
                new DataGridViewCellStyle { Format = "format" });
            AssertNotEqual (
                new DataGridViewCellStyle { Format = "format" },
                new DataGridViewCellStyle { Format = "other" });
        }

        [Fact]
        public void Equals_NullValue_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { NullValue = "null" },
                new DataGridViewCellStyle { NullValue = "null" });
            AssertNotEqual (
                new DataGridViewCellStyle { NullValue = "null" },
                new DataGridViewCellStyle { NullValue = "other" });
        }

        [Fact]
        public void Equals_DataSourceNullValue_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { DataSourceNullValue = "dbNull" },
                new DataGridViewCellStyle { DataSourceNullValue = "dbNull" });
            AssertNotEqual (
                new DataGridViewCellStyle { DataSourceNullValue = "dbNull" },
                new DataGridViewCellStyle { DataSourceNullValue = "other" });
        }

        [Fact]
        public void Equals_Padding_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { Padding = new Padding (1, 2, 3, 4) },
                new DataGridViewCellStyle { Padding = new Padding (1, 2, 3, 4) });
            AssertNotEqual (
                new DataGridViewCellStyle { Padding = new Padding (1, 2, 3, 4) },
                new DataGridViewCellStyle { Padding = new Padding (2, 3, 4, 5) });
        }

        [Fact]
        public void Equals_Tag_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { Tag = "tag" },
                new DataGridViewCellStyle { Tag = "tag" });
            AssertNotEqual (
                new DataGridViewCellStyle { Tag = "tag" },
                new DataGridViewCellStyle { Tag = "other" });
        }

        [Fact]
        public void Equals_WrapMode_ComparesByValue ()
        {
            AssertEqual (
                new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
                new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True });
            AssertNotEqual (
                new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
                new DataGridViewCellStyle { WrapMode = DataGridViewTriState.False });
        }

        [Fact]
        public void Equals_Font_ComparesByReference ()
        {
            var font = CreateFont ();
            AssertEqual (
                new DataGridViewCellStyle { Font = font },
                new DataGridViewCellStyle { Font = font });
        }

        [Fact]
        public void Equals_OtherType_ReturnsFalse ()
        {
            Assert.False (new DataGridViewCellStyle ().Equals (new object ()));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse ()
        {
            Assert.False (new DataGridViewCellStyle ().Equals (null));
        }
    }
}
