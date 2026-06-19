// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DataGridViewColumnCollectionTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DataGridViewColumnCollectionTests, adapted
    // to the Modern.Forms compat layer. The upstream suite leans heavily on element-state plumbing
    // (DisplayIndex reordering, frozen/auto-size validation, IsHandleCreated, ISupportInitialize)
    // that Modern.Forms intentionally does not model, so those cases are omitted. What remains pins
    // the collection mechanics Modern.Forms actually implements: Add/AddRange/Insert/Remove/RemoveAt/
    // Clear updating Count, the Index/DataGridView wiring driven by the owning grid, the int and
    // string indexers, and Contains/IndexOf. Per-column property defaults and the single-column
    // Add/Remove/Clear wiring already live in DataGridViewColumnTests, so they are not repeated here.
    public class DataGridViewColumnCollectionTests
    {
        [Fact]
        public void Columns_Empty_HasZeroCount ()
        {
            using var control = new DataGridView ();

            Assert.Empty (control.Columns);
            Assert.Equal (0, control.Columns.Count);
        }

        [Fact]
        public void Add_Column_AppendsAndWiresOwner ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();

            control.Columns.Add (column1);
            Assert.Equal (1, control.Columns.Count);
            Assert.Same (control, column1.DataGridView);
            Assert.Equal (0, column1.Index);
            Assert.Equal (0, column1.DisplayIndex);

            control.Columns.Add (column2);
            Assert.Equal (2, control.Columns.Count);
            Assert.Same (column1, control.Columns[0]);
            Assert.Same (column2, control.Columns[1]);
            Assert.Equal (0, column1.Index);
            Assert.Equal (1, column2.Index);
            Assert.Same (control, column2.DataGridView);
        }

        [Fact]
        public void AddRange_Columns_AppendsAllInOrder ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            var column3 = new DataGridViewColumn ();

            control.Columns.AddRange (column1, column2, column3);

            Assert.Equal (3, control.Columns.Count);
            Assert.Same (column1, control.Columns[0]);
            Assert.Same (column2, control.Columns[1]);
            Assert.Same (column3, control.Columns[2]);
            Assert.Equal (0, column1.Index);
            Assert.Equal (1, column2.Index);
            Assert.Equal (2, column3.Index);
            Assert.Same (control, column1.DataGridView);
            Assert.Same (control, column2.DataGridView);
            Assert.Same (control, column3.DataGridView);
        }

        [Fact]
        public void AddRange_Empty_DoesNotChangeCount ()
        {
            using var control = new DataGridView ();

            control.Columns.AddRange ();

            Assert.Empty (control.Columns);
        }

        [Fact]
        public void AddRange_Null_ThrowsArgumentNullException ()
        {
            using var control = new DataGridView ();

            Assert.Throws<ArgumentNullException> (() => control.Columns.AddRange (null!));
        }

        [Fact]
        public void Insert_Column_ShiftsSubsequentIndexes ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            var inserted = new DataGridViewColumn ();
            control.Columns.Add (column1);
            control.Columns.Add (column2);

            control.Columns.Insert (1, inserted);

            Assert.Equal (3, control.Columns.Count);
            Assert.Same (column1, control.Columns[0]);
            Assert.Same (inserted, control.Columns[1]);
            Assert.Same (column2, control.Columns[2]);
            Assert.Equal (0, column1.Index);
            Assert.Equal (1, inserted.Index);
            Assert.Equal (2, column2.Index);
            Assert.Same (control, inserted.DataGridView);
        }

        [Fact]
        public void Insert_AtZero_PrependsColumn ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ();
            var inserted = new DataGridViewColumn ();
            control.Columns.Add (column);

            control.Columns.Insert (0, inserted);

            Assert.Same (inserted, control.Columns[0]);
            Assert.Same (column, control.Columns[1]);
            Assert.Equal (0, inserted.Index);
            Assert.Equal (1, column.Index);
        }

        [Fact]
        public void Remove_Column_UpdatesCountAndReindexesRemaining ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            var column3 = new DataGridViewColumn ();
            control.Columns.AddRange (column1, column2, column3);

            control.Columns.Remove (column2);

            Assert.Equal (2, control.Columns.Count);
            Assert.Same (column1, control.Columns[0]);
            Assert.Same (column3, control.Columns[1]);
            Assert.Equal (0, column1.Index);
            Assert.Equal (1, column3.Index);

            // The removed column is detached from the grid.
            Assert.Null (column2.DataGridView);
            Assert.Equal (-1, column2.Index);
        }

        [Fact]
        public void RemoveAt_Index_UpdatesCountAndReindexesRemaining ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            var column3 = new DataGridViewColumn ();
            control.Columns.AddRange (column1, column2, column3);

            control.Columns.RemoveAt (0);

            Assert.Equal (2, control.Columns.Count);
            Assert.Same (column2, control.Columns[0]);
            Assert.Same (column3, control.Columns[1]);
            Assert.Equal (0, column2.Index);
            Assert.Equal (1, column3.Index);
            Assert.Null (column1.DataGridView);
            Assert.Equal (-1, column1.Index);
        }

        [Fact]
        public void Clear_RemovesAllAndDetaches ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            control.Columns.AddRange (column1, column2);

            control.Columns.Clear ();

            Assert.Empty (control.Columns);
            Assert.Equal (0, control.Columns.Count);
            Assert.Null (column1.DataGridView);
            Assert.Null (column2.DataGridView);
            Assert.Equal (-1, column1.Index);
            Assert.Equal (-1, column2.Index);
        }

        [Fact]
        public void IndexOf_ReturnsPositionOrNegativeOne ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            var notAdded = new DataGridViewColumn ();
            control.Columns.AddRange (column1, column2);

            Assert.Equal (0, control.Columns.IndexOf (column1));
            Assert.Equal (1, control.Columns.IndexOf (column2));
            Assert.Equal (-1, control.Columns.IndexOf (notAdded));
        }

        [Fact]
        public void Contains_Column_ReturnsExpected ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ();
            var notAdded = new DataGridViewColumn ();
            control.Columns.Add (column);

            Assert.Contains (column, control.Columns);
            Assert.DoesNotContain (notAdded, control.Columns);
        }

        [Fact]
        public void IntIndexer_ReturnsColumnAtPosition ()
        {
            using var control = new DataGridView ();
            var column1 = new DataGridViewColumn ();
            var column2 = new DataGridViewColumn ();
            control.Columns.AddRange (column1, column2);

            Assert.Same (column1, control.Columns[0]);
            Assert.Same (column2, control.Columns[1]);
        }

        [Fact]
        public void StringIndexer_FindsByNameOrHeaderTextCaseInsensitively ()
        {
            using var control = new DataGridView ();
            var named = new DataGridViewColumn ("HeaderA") { Name = "NameA" };
            var byHeader = new DataGridViewColumn ("HeaderB");
            control.Columns.AddRange (named, byHeader);

            Assert.Same (named, control.Columns["NameA"]);
            Assert.Same (named, control.Columns["namea"]);
            Assert.Same (named, control.Columns["HeaderA"]);
            Assert.Same (byHeader, control.Columns["HeaderB"]);
            Assert.Same (byHeader, control.Columns["headerb"]);
            Assert.Null (control.Columns["missing"]);
        }

        [Fact]
        public void Contains_Name_ReturnsExpected ()
        {
            using var control = new DataGridView ();
            var column = new DataGridViewColumn ("Header") { Name = "Name" };
            control.Columns.Add (column);

            Assert.True (control.Columns.Contains ("Name"));
            Assert.True (control.Columns.Contains ("name"));
            Assert.True (control.Columns.Contains ("Header"));
            Assert.False (control.Columns.Contains ("missing"));
        }

        [Fact]
        public void Add_HeaderTextOverload_ReturnsConfiguredColumn ()
        {
            using var control = new DataGridView ();

            var column = control.Columns.Add ("MyHeader");

            Assert.Equal ("MyHeader", column.HeaderText);
            Assert.Same (column, control.Columns[0]);
            Assert.Same (control, column.DataGridView);
            Assert.Equal (1, control.Columns.Count);
        }

        [Fact]
        public void Add_MultipleHeaderTextOverloads_AppendSequentially ()
        {
            using var control = new DataGridView ();

            var first = control.Columns.Add ("First");
            var second = control.Columns.Add ("Second");

            Assert.Equal (2, control.Columns.Count);
            Assert.Equal (0, first.Index);
            Assert.Equal (1, second.Index);
            Assert.Same (first, control.Columns["First"]);
            Assert.Same (second, control.Columns["Second"]);
        }
    }
}
