// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/BindingSourceTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms BindingSourceTests, adapted to the
    // Majorsilence.Forms API. Majorsilence.Forms' BindingSource is a Component : IList backed by an internal
    // list (no CurrencyManager / BindingList / data-binding machinery), so these pin only the
    // surface that actually exists: the IList contract, Position/Current navigation, and the
    // DataMember/DataSource round-trips. Stubbed members (Filter/Sort/events/AddNew/Find) are
    // intentionally not covered.
    public class BindingSourceTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var source = new BindingSource ();

            Assert.True (source.AllowEdit);
            Assert.True (source.AllowNew);
            Assert.True (source.AllowRemove);
            Assert.Empty (source);
            Assert.Equal (0, source.Count);
            Assert.Null (source.Current);
            Assert.Equal (string.Empty, source.DataMember);
            Assert.Null (source.DataSource);
            Assert.Null (source.Filter);
            Assert.False (source.IsFixedSize);
            Assert.False (source.IsReadOnly);
            Assert.Equal (-1, source.Position);
            Assert.True (source.RaiseListChangedEvents);
            Assert.Null (source.Sort);
        }

        [Fact]
        public void Ctor_Object_String_RoundTripsDataSourceAndMember ()
        {
            var data = new List<object?> { 1, 2, 3 };
            using var source = new BindingSource (data, "member");

            Assert.Same (data, source.DataSource);
            Assert.Equal ("member", source.DataMember);
            Assert.Equal (3, source.Count);
        }

        [Fact]
        public void Ctor_Object_String_NullEmptyDataSource_IsEmpty ()
        {
            using var source = new BindingSource (new List<object?> (), string.Empty);

            Assert.Empty (source);
            Assert.Equal (-1, source.Position);
            Assert.Null (source.Current);
        }

        [Fact]
        public void DataMember_Set_GetReturnsExpected ()
        {
            using var source = new BindingSource { DataMember = "member" };
            Assert.Equal ("member", source.DataMember);

            source.DataMember = "other";
            Assert.Equal ("other", source.DataMember);
        }

        [Fact]
        public void DataSource_SetNonEmptyList_PositionsAtFirstItem ()
        {
            var data = new List<object?> { "a", "b", "c" };
            using var source = new BindingSource { DataSource = data };

            Assert.Same (data, source.DataSource);
            Assert.Equal (3, source.Count);
            Assert.Equal (0, source.Position);
            Assert.Equal ("a", source.Current);
        }

        [Fact]
        public void DataSource_SetEmptyList_PositionRemainsInvalid ()
        {
            using var source = new BindingSource { DataSource = new List<object?> () };

            Assert.Equal (0, source.Count);
            Assert.Equal (-1, source.Position);
            Assert.Null (source.Current);
        }

        [Fact]
        public void DataSource_SetNull_IsEmpty ()
        {
            using var source = new BindingSource { DataSource = new List<object?> { 1 } };
            source.DataSource = null;

            Assert.Null (source.DataSource);
            Assert.Equal (0, source.Count);
            Assert.Equal (-1, source.Position);
        }

        [Fact]
        public void DataSource_SetNonList_IsEmpty ()
        {
            using var source = new BindingSource { DataSource = new object () };

            Assert.Equal (0, source.Count);
            Assert.Equal (-1, source.Position);
        }

        [Fact]
        public void Add_Invoke_IncrementsCount ()
        {
            using var source = new BindingSource ();

            var index0 = source.Add ("a");
            Assert.Equal (0, index0);
            Assert.Equal (1, source.Count);

            var index1 = source.Add ("b");
            Assert.Equal (1, index1);
            Assert.Equal (2, source.Count);

            Assert.Equal ("a", source[0]);
            Assert.Equal ("b", source[1]);
        }

        [Fact]
        public void Add_Null_IsStored ()
        {
            using var source = new BindingSource ();

            source.Add (null);
            Assert.Equal (1, source.Count);
            Assert.Null (source[0]);
        }

        [Fact]
        public void Insert_Invoke_InsertsAtIndex ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("c");

            source.Insert (1, "b");

            Assert.Equal (3, source.Count);
            Assert.Equal ("a", source[0]);
            Assert.Equal ("b", source[1]);
            Assert.Equal ("c", source[2]);
        }

        [Fact]
        public void Remove_Invoke_RemovesItem ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            source.Remove ("a");

            Assert.Equal (1, source.Count);
            Assert.Equal ("b", source[0]);
        }

        [Fact]
        public void RemoveAt_Invoke_RemovesItem ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            source.RemoveAt (0);

            Assert.Equal (1, source.Count);
            Assert.Equal ("b", source[0]);
        }

        [Fact]
        public void Clear_Invoke_EmptiesList ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            source.Clear ();

            Assert.Equal (0, source.Count);
            Assert.Empty (source);
        }

        [Fact]
        public void Item_SetGet_RoundTrips ()
        {
            using var source = new BindingSource ();
            source.Add ("a");

            source[0] = "b";
            Assert.Equal ("b", source[0]);
        }

        [Fact]
        public void Contains_Invoke_ReturnsExpected ()
        {
            using var source = new BindingSource ();
            source.Add ("a");

            Assert.True (source.Contains ("a"));
            Assert.False (source.Contains ("b"));
        }

        [Fact]
        public void IndexOf_Invoke_ReturnsExpected ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            Assert.Equal (0, source.IndexOf ("a"));
            Assert.Equal (1, source.IndexOf ("b"));
            Assert.Equal (-1, source.IndexOf ("c"));
        }

        [Fact]
        public void GetEnumerator_Invoke_EnumeratesItems ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            var items = new List<object?> ();
            foreach (var item in source)
                items.Add (item);

            Assert.Equal (new object?[] { "a", "b" }, items);
        }

        [Fact]
        public void CopyTo_Invoke_CopiesItems ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            var array = new object?[2];
            source.CopyTo (array, 0);

            Assert.Equal (new object?[] { "a", "b" }, array);
        }

        [Fact]
        public void Position_DefaultEmpty_IsNegativeOne ()
        {
            using var source = new BindingSource ();
            Assert.Equal (-1, source.Position);
            Assert.Null (source.Current);
        }

        [Fact]
        public void MoveFirst_NonEmpty_SetsPositionToZero ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");

            source.MoveFirst ();

            Assert.Equal (0, source.Position);
            Assert.Equal ("a", source.Current);
        }

        [Fact]
        public void MoveFirst_Empty_LeavesPositionUnchanged ()
        {
            using var source = new BindingSource ();

            source.MoveFirst ();

            Assert.Equal (-1, source.Position);
            Assert.Null (source.Current);
        }

        [Fact]
        public void MoveLast_NonEmpty_SetsPositionToLast ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");
            source.Add ("c");

            source.MoveLast ();

            Assert.Equal (2, source.Position);
            Assert.Equal ("c", source.Current);
        }

        [Fact]
        public void MoveNext_Invoke_AdvancesAndClamps ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");
            source.MoveFirst ();

            source.MoveNext ();
            Assert.Equal (1, source.Position);
            Assert.Equal ("b", source.Current);

            // Already at the last item: clamps.
            source.MoveNext ();
            Assert.Equal (1, source.Position);
        }

        [Fact]
        public void MovePrevious_Invoke_RetreatsAndClamps ()
        {
            using var source = new BindingSource ();
            source.Add ("a");
            source.Add ("b");
            source.MoveLast ();

            source.MovePrevious ();
            Assert.Equal (0, source.Position);
            Assert.Equal ("a", source.Current);

            // Already at the first item: clamps.
            source.MovePrevious ();
            Assert.Equal (0, source.Position);
        }

        [Fact]
        public void Current_ReflectsPosition ()
        {
            var data = new List<object?> { "a", "b", "c" };
            using var source = new BindingSource { DataSource = data };

            source.Position = 2;
            Assert.Equal ("c", source.Current);

            source.Position = 1;
            Assert.Equal ("b", source.Current);
        }

        [Fact]
        public void Current_PositionOutOfRange_IsNull ()
        {
            using var source = new BindingSource ();
            source.Add ("a");

            source.Position = 5;
            Assert.Null (source.Current);

            source.Position = -1;
            Assert.Null (source.Current);
        }

        [Fact]
        public void IsList_AssignableToIList ()
        {
            using var source = new BindingSource ();
            IList list = source;

            list.Add ("a");
            Assert.Equal (1, list.Count);
            Assert.Equal ("a", list[0]);
        }
    }
}
