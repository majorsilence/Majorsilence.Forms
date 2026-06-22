// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/CursorTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using System.Reflection;
using Majorsilence.Forms.Backends;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms CursorTests/CursorsTests, adapted to the
    // Majorsilence.Forms API. Majorsilence.Forms cursors are backend-neutral value descriptors built around a
    // CursorType (no Win32 handles, hotspots, .cur/.ico file loading, or accessibility plumbing), so
    // those upstream cases are intentionally omitted. What remains pins the same observable contract:
    // the Cursors.* statics are non-null and stable, equality/hashing/ToString behave sensibly, and
    // the Current/Position/Clip/Show/Hide statics round-trip without throwing.
    public class CursorTests
    {
        [Fact]
        public void Cursors_Properties_Get_NotNull ()
        {
            foreach (var property in typeof (Cursors).GetProperties (BindingFlags.Static | BindingFlags.Public)) {
                var value = (Cursor?)property.GetValue (null, null);
                Assert.NotNull (value);
            }
        }

        [Fact]
        public void Cursors_Properties_Get_Stable ()
        {
            // Majorsilence.Forms caches each backing cursor (??=), so repeated reads of a property must
            // return the same instance.
            foreach (var property in typeof (Cursors).GetProperties (BindingFlags.Static | BindingFlags.Public)) {
                var first = (Cursor?)property.GetValue (null, null);
                var second = (Cursor?)property.GetValue (null, null);
                Assert.Same (first, second);
            }
        }

        [Fact]
        public void Cursors_ToString_KnownCursor_ReturnsExpected ()
        {
            // Regression analog for https://github.com/dotnet/winforms/issues/9464:
            // ToString must not be the bare type name.
            foreach (var property in typeof (Cursors).GetProperties (BindingFlags.Static | BindingFlags.Public)) {
                var cursor = (Cursor?)property.GetValue (null, null);
                Assert.NotNull (cursor);
                Assert.NotEqual ("Majorsilence.Forms.Cursor", cursor!.ToString ());
                Assert.StartsWith ("[Cursor: ", cursor.ToString ());
            }
        }

        [Fact]
        public void Cursor_ToString_KnownCursor_ReturnsExpected ()
        {
            Assert.Equal ("[Cursor: AppStarting]", Cursors.AppStarting.ToString ());
            Assert.Equal ("[Cursor: Arrow]", Cursors.Arrow.ToString ());
            Assert.Equal ("[Cursor: Ibeam]", Cursors.IBeam.ToString ());
        }

        [Fact]
        public void Cursor_Default_ReturnsArrow ()
        {
            Assert.Same (Cursors.Arrow, Cursor.Default);
            Assert.Same (Cursors.Arrow, Cursors.Default);
        }

        [Fact]
        public void Cursors_Alias_ReturnsSameUnderlying ()
        {
            Assert.Same (Cursors.Arrow, Cursors.Default);
            Assert.Same (Cursors.SizeNorthSouth, Cursors.SizeNS);
            Assert.Same (Cursors.SizeWestEast, Cursors.SizeWE);
            Assert.Same (Cursors.SizeWestEast, Cursors.HSplit);
            Assert.Same (Cursors.SizeNorthSouth, Cursors.VSplit);
            Assert.Same (Cursors.SizeAll, Cursors.NoMove2D);
            Assert.Same (Cursors.Wait, Cursors.WaitCursor);
        }

        [Fact]
        public void Cursor_Equals_SameInstance_ReturnsTrue ()
        {
            var cursor = Cursors.AppStarting;
            Assert.True (cursor.Equals (cursor));
        }

        [Fact]
        public void Cursor_Equals_SameType_ReturnsTrue ()
        {
            var cursor = new Cursor (CursorType.Arrow);
            var other = new Cursor (CursorType.Arrow);

            Assert.True (cursor.Equals (other));
            Assert.Equal (cursor.GetHashCode (), other.GetHashCode ());
        }

        [Fact]
        public void Cursor_Equals_DifferentType_ReturnsFalse ()
        {
            var cursor = new Cursor (CursorType.Arrow);
            var other = new Cursor (CursorType.Hand);

            Assert.False (cursor.Equals (other));
        }

        [Fact]
        public void Cursor_Equals_NonCursor_ReturnsFalse ()
        {
            var cursor = new Cursor (CursorType.Arrow);

            Assert.False (cursor.Equals (new object ()));
            Assert.False (cursor.Equals (null));
        }

        [Fact]
        public void Cursor_EqualityOperators_ReturnExpected ()
        {
            var cursor = new Cursor (CursorType.Arrow);

            AssertEquality (cursor, cursor, true);
            AssertEquality (cursor, new Cursor (CursorType.Arrow), true);
            AssertEquality (cursor, new Cursor (CursorType.Hand), false);
            AssertEquality (null, null, true);
            AssertEquality (null, cursor, false);
            AssertEquality (cursor, null, false);

            static void AssertEquality (Cursor? left, Cursor? right, bool expected)
            {
                Assert.Equal (expected, left == right);
                Assert.Equal (!expected, left != right);
            }
        }

        [Fact]
        public void Cursor_EqualityOperator_CachedSingletons_AreEqual ()
        {
            // DataGridView relies on comparing the live Cursor against the Cursors.* singletons.
            Assert.True (Cursors.Arrow == Cursors.Arrow);
            Assert.True (Cursors.SizeNorthSouth == Cursors.SizeNS);
            Assert.True (Cursors.Arrow != Cursors.Hand);
        }

        [Fact]
        public void Cursor_Current_Set_GetReturnsExpected ()
        {
            var original = Cursor.Current;
            try {
                // Set non-null.
                Cursor.Current = Cursors.Hand;
                Assert.Same (Cursors.Hand, Cursor.Current);

                // Set null.
                Cursor.Current = null;
                Assert.Null (Cursor.Current);
            } finally {
                Cursor.Current = original;
            }
        }

        [Fact]
        public void Cursor_Position_Set_GetReturnsExpected ()
        {
            var original = Cursor.Position;
            try {
                Cursor.Position = new Point (1, 2);
                Assert.Equal (new Point (1, 2), Cursor.Position);
            } finally {
                Cursor.Position = original;
            }
        }

        [Fact]
        public void Cursor_Clip_Set_GetReturnsExpected ()
        {
            var original = Cursor.Clip;
            try {
                Cursor.Clip = new Rectangle (1, 2, 3, 4);
                Assert.Equal (new Rectangle (1, 2, 3, 4), Cursor.Clip);
            } finally {
                Cursor.Clip = original;
            }
        }

        [Fact]
        public void Cursor_Show_InvokeMultipleTimes_Success ()
        {
            Cursor.Show ();
            Cursor.Show ();
        }

        [Fact]
        public void Cursor_Hide_InvokeMultipleTimes_Success ()
        {
            Cursor.Hide ();
            Cursor.Hide ();
        }

        [Fact]
        public void Cursor_Dispose_InvokeMultipleTimes_Success ()
        {
            var cursor = new Cursor (CursorType.Arrow);
            cursor.Dispose ();
            cursor.Dispose ();
        }
    }
}
