// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms Control.ControlCollection unit-test behavior,
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests for Control.ControlCollection (the Control.Controls collection),
    // pinning the WinForms-compatible semantics that the Majorsilence.Forms API exposes:
    // Add sets Parent and increments Count, Add is idempotent (re-parents/reorders),
    // Remove/Clear reset Parent, Contains/IndexOf, the integer and string indexers,
    // key-based lookup (ContainsKey/IndexOfKey/RemoveByKey/Find via control Name), and
    // GetChildIndex/SetChildIndex reordering. These exercise the real Majorsilence.Forms
    // collection (no Handle/CreateParams plumbing).
    public class ControlCollectionTests
    {
        [Fact]
        public void Ctor_Default_Empty ()
        {
            using var owner = new Control ();

            Assert.Equal (0, owner.Controls.Count);
            Assert.Same (owner, owner.Controls.Owner);
            Assert.False (owner.Controls.IsReadOnly);
        }

        [Fact]
        public void Ctor_NullOwner_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException> (() => new Control.ControlCollection (null!));
        }

        [Fact]
        public void Add_SetsParentAndIncrementsCount ()
        {
            using var owner = new Control ();
            var child = new Control ();

            owner.Controls.Add (child);

            Assert.Equal (1, owner.Controls.Count);
            Assert.Same (owner, child.Parent);
            Assert.Contains (child, owner.Controls);
            Assert.Same (child, owner.Controls[0]);
        }

        [Fact]
        public void Add_ReturnsAddedControl ()
        {
            using var owner = new Control ();
            var child = new Button ();

            var result = owner.Controls.Add (child);

            Assert.Same (child, result);
        }

        [Fact]
        public void Add_Multiple_AppendsInOrder ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            var third = new Control ();

            owner.Controls.Add (first);
            owner.Controls.Add (second);
            owner.Controls.Add (third);

            Assert.Equal (3, owner.Controls.Count);
            Assert.Same (first, owner.Controls[0]);
            Assert.Same (second, owner.Controls[1]);
            Assert.Same (third, owner.Controls[2]);
        }

        [Fact]
        public void Add_AlreadyChild_IsIdempotentAndMovesToBack ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();

            owner.Controls.Add (first);
            owner.Controls.Add (second);

            // Re-adding an existing child should not increase Count; it sends it to back.
            owner.Controls.Add (first);

            Assert.Equal (2, owner.Controls.Count);
            Assert.Same (second, owner.Controls[0]);
            Assert.Same (first, owner.Controls[1]);
        }

        [Fact]
        public void Add_ChildOfAnotherParent_Reparents ()
        {
            using var owner1 = new Control ();
            using var owner2 = new Control ();
            var child = new Control ();

            owner1.Controls.Add (child);
            Assert.Same (owner1, child.Parent);
            Assert.Equal (1, owner1.Controls.Count);

            owner2.Controls.Add (child);

            Assert.Same (owner2, child.Parent);
            Assert.Equal (0, owner1.Controls.Count);
            Assert.Equal (1, owner2.Controls.Count);
            Assert.DoesNotContain (child, owner1.Controls);
            Assert.Contains (child, owner2.Controls);
        }

        [Fact]
        public void Add_Null_DoesNothing ()
        {
            using var owner = new Control ();

            owner.Controls.Add<Control> (null!);

            Assert.Equal (0, owner.Controls.Count);
        }

        [Fact]
        public void AddRange_AddsAllAndSetsParents ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();

            owner.Controls.AddRange (first, second);

            Assert.Equal (2, owner.Controls.Count);
            Assert.Same (owner, first.Parent);
            Assert.Same (owner, second.Parent);
            Assert.Same (first, owner.Controls[0]);
            Assert.Same (second, owner.Controls[1]);
        }

        [Fact]
        public void AddRange_Empty_DoesNothing ()
        {
            using var owner = new Control ();

            owner.Controls.AddRange ();

            Assert.Equal (0, owner.Controls.Count);
        }

        [Fact]
        public void AddRange_Null_ThrowsArgumentNullException ()
        {
            using var owner = new Control ();

            Assert.Throws<ArgumentNullException> (() => owner.Controls.AddRange (null!));
        }

        [Fact]
        public void Remove_RemovesAndClearsParent ()
        {
            using var owner = new Control ();
            var child = new Control ();
            owner.Controls.Add (child);

            var result = owner.Controls.Remove (child);

            Assert.True (result);
            Assert.Equal (0, owner.Controls.Count);
            Assert.Null (child.Parent);
            Assert.DoesNotContain (child, owner.Controls);
        }

        [Fact]
        public void Remove_NotAChild_DoesNotThrowOrAffectCount ()
        {
            using var owner = new Control ();
            var child = new Control ();
            var stranger = new Control ();
            owner.Controls.Add (child);

            var result = owner.Controls.Remove (stranger);

            Assert.False (result);
            Assert.Equal (1, owner.Controls.Count);
            Assert.Same (owner, child.Parent);
        }

        [Fact]
        public void Remove_Null_DoesNothing ()
        {
            using var owner = new Control ();
            var child = new Control ();
            owner.Controls.Add (child);

            var result = owner.Controls.Remove (null!);

            Assert.False (result);
            Assert.Equal (1, owner.Controls.Count);
        }

        [Fact]
        public void RemoveAt_RemovesControlAtIndex ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            owner.Controls.RemoveAt (0);

            Assert.Equal (1, owner.Controls.Count);
            Assert.Same (second, owner.Controls[0]);
            Assert.Null (first.Parent);
        }

        [Fact]
        public void Clear_RemovesAllAndClearsParents ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            owner.Controls.Clear ();

            Assert.Equal (0, owner.Controls.Count);
            Assert.Null (first.Parent);
            Assert.Null (second.Parent);
        }

        [Fact]
        public void Contains_ReturnsExpected ()
        {
            using var owner = new Control ();
            var child = new Control ();
            var stranger = new Control ();
            owner.Controls.Add (child);

            Assert.Contains (child, owner.Controls);
            Assert.DoesNotContain (stranger, owner.Controls);
        }

        [Fact]
        public void IndexOf_ReturnsExpected ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            var stranger = new Control ();
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            Assert.Equal (0, owner.Controls.IndexOf (first));
            Assert.Equal (1, owner.Controls.IndexOf (second));
            Assert.Equal (-1, owner.Controls.IndexOf (stranger));
        }

        [Fact]
        public void Indexer_Int_Get_ReturnsExpected ()
        {
            using var owner = new Control ();
            var child = new Control ();
            owner.Controls.Add (child);

            Assert.Same (child, owner.Controls[0]);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (1)]
        public void Indexer_Int_GetInvalidIndex_ThrowsArgumentOutOfRangeException (int index)
        {
            using var owner = new Control ();
            // Empty collection: every index is invalid.
            Assert.Throws<ArgumentOutOfRangeException> (() => owner.Controls[index]);
        }

        [Fact]
        public void CopyTo_CopiesControls ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            var array = new Control[3];
            owner.Controls.CopyTo (array, 1);

            Assert.Null (array[0]);
            Assert.Same (first, array[1]);
            Assert.Same (second, array[2]);
        }

        [Fact]
        public void Enumerator_IteratesInOrder ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            var collected = new System.Collections.Generic.List<Control> ();
            foreach (var c in owner.Controls)
                collected.Add (c);

            Assert.Equal (2, collected.Count);
            Assert.Same (first, collected[0]);
            Assert.Same (second, collected[1]);
        }

        [Fact]
        public void Parent_Set_AddsToParentControls ()
        {
            using var owner = new Control ();
            var child = new Control ();

            child.Parent = owner;

            Assert.Same (owner, child.Parent);
            Assert.Contains (child, owner.Controls);
            Assert.Equal (1, owner.Controls.Count);
        }

        [Fact]
        public void Parent_SetNull_RemovesFromParentControls ()
        {
            using var owner = new Control ();
            var child = new Control ();
            owner.Controls.Add (child);

            child.Parent = null;

            Assert.Null (child.Parent);
            Assert.Equal (0, owner.Controls.Count);
        }

        // --- Key-based members (use the control Name as the key) ---

        [Fact]
        public void Indexer_String_Get_ReturnsControlWithMatchingName ()
        {
            using var owner = new Control ();
            var child = new Control { Name = "child1" };
            owner.Controls.Add (child);

            Assert.Same (child, owner.Controls["child1"]);
            // Key match is case-insensitive.
            Assert.Same (child, owner.Controls["CHILD1"]);
        }

        [Fact]
        public void Indexer_String_GetNoMatch_ReturnsNull ()
        {
            using var owner = new Control ();
            owner.Controls.Add (new Control { Name = "child1" });

            Assert.Null (owner.Controls["nope"]);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        public void Indexer_String_GetNullOrEmpty_ReturnsNull (string? key)
        {
            using var owner = new Control ();
            owner.Controls.Add (new Control { Name = "child1" });

            Assert.Null (owner.Controls[key!]);
        }

        [Fact]
        public void ContainsKey_ReturnsExpected ()
        {
            using var owner = new Control ();
            owner.Controls.Add (new Control { Name = "child1" });

            Assert.True (owner.Controls.ContainsKey ("child1"));
            Assert.True (owner.Controls.ContainsKey ("CHILD1"));
            Assert.False (owner.Controls.ContainsKey ("missing"));
        }

        [Fact]
        public void IndexOfKey_ReturnsExpected ()
        {
            using var owner = new Control ();
            owner.Controls.Add (new Control { Name = "first" });
            owner.Controls.Add (new Control { Name = "second" });

            Assert.Equal (0, owner.Controls.IndexOfKey ("first"));
            Assert.Equal (1, owner.Controls.IndexOfKey ("second"));
            Assert.Equal (-1, owner.Controls.IndexOfKey ("missing"));
            Assert.Equal (-1, owner.Controls.IndexOfKey (string.Empty));
        }

        [Fact]
        public void RemoveByKey_RemovesMatchingControl ()
        {
            using var owner = new Control ();
            var first = new Control { Name = "first" };
            var second = new Control { Name = "second" };
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            owner.Controls.RemoveByKey ("first");

            Assert.Equal (1, owner.Controls.Count);
            Assert.Same (second, owner.Controls[0]);
            Assert.Null (first.Parent);
        }

        [Fact]
        public void RemoveByKey_NoMatch_DoesNothing ()
        {
            using var owner = new Control ();
            owner.Controls.Add (new Control { Name = "first" });

            owner.Controls.RemoveByKey ("missing");

            Assert.Equal (1, owner.Controls.Count);
        }

        [Fact]
        public void Find_NonRecursive_ReturnsMatchingChildren ()
        {
            using var owner = new Control ();
            var first = new Control { Name = "target" };
            var second = new Control { Name = "other" };
            var third = new Control { Name = "target" };
            owner.Controls.Add (first);
            owner.Controls.Add (second);
            owner.Controls.Add (third);

            var found = owner.Controls.Find ("target", searchAllChildren: false);

            Assert.Equal (2, found.Length);
            Assert.Contains (first, found);
            Assert.Contains (third, found);
        }

        [Fact]
        public void Find_Recursive_SearchesNestedChildren ()
        {
            using var owner = new Control ();
            using var panel = new Panel ();
            var nested = new Control { Name = "deep" };

            panel.Controls.Add (nested);
            owner.Controls.Add (panel);

            var shallow = owner.Controls.Find ("deep", searchAllChildren: false);
            Assert.Empty (shallow);

            var deep = owner.Controls.Find ("deep", searchAllChildren: true);
            Assert.Single (deep);
            Assert.Same (nested, deep[0]);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        public void Find_NullOrEmptyKey_Throws (string? key)
        {
            using var owner = new Control ();

            Assert.ThrowsAny<ArgumentException> (() => owner.Controls.Find (key!, false));
        }

        // --- Child index ordering ---

        [Fact]
        public void GetChildIndex_ReturnsExpected ()
        {
            using var owner = new Control ();
            var first = new Control ();
            var second = new Control ();
            owner.Controls.Add (first);
            owner.Controls.Add (second);

            Assert.Equal (0, owner.Controls.GetChildIndex (first));
            Assert.Equal (1, owner.Controls.GetChildIndex (second));
        }

        [Fact]
        public void GetChildIndex_NotChild_Throws ()
        {
            using var owner = new Control ();
            var stranger = new Control ();

            Assert.Throws<ArgumentException> (() => owner.Controls.GetChildIndex (stranger));
        }

        [Fact]
        public void GetChildIndex_NotChildNoThrow_ReturnsNegativeOne ()
        {
            using var owner = new Control ();
            var stranger = new Control ();

            Assert.Equal (-1, owner.Controls.GetChildIndex (stranger, throwException: false));
        }

        [Fact]
        public void SetChildIndex_MovesControlForward ()
        {
            using var owner = new Control ();
            var a = new Control ();
            var b = new Control ();
            var c = new Control ();
            owner.Controls.Add (a);
            owner.Controls.Add (b);
            owner.Controls.Add (c);

            // Move 'a' from index 0 to index 2.
            owner.Controls.SetChildIndex (a, 2);

            Assert.Same (b, owner.Controls[0]);
            Assert.Same (c, owner.Controls[1]);
            Assert.Same (a, owner.Controls[2]);
        }

        [Fact]
        public void SetChildIndex_MovesControlBackward ()
        {
            using var owner = new Control ();
            var a = new Control ();
            var b = new Control ();
            var c = new Control ();
            owner.Controls.Add (a);
            owner.Controls.Add (b);
            owner.Controls.Add (c);

            // Move 'c' from index 2 to index 0.
            owner.Controls.SetChildIndex (c, 0);

            Assert.Same (c, owner.Controls[0]);
            Assert.Same (a, owner.Controls[1]);
            Assert.Same (b, owner.Controls[2]);
        }

        [Fact]
        public void SetChildIndex_SameIndex_IsNoOp ()
        {
            using var owner = new Control ();
            var a = new Control ();
            var b = new Control ();
            owner.Controls.Add (a);
            owner.Controls.Add (b);

            owner.Controls.SetChildIndex (a, 0);

            Assert.Same (a, owner.Controls[0]);
            Assert.Same (b, owner.Controls[1]);
        }

        [Fact]
        public void SetChildIndex_BeyondCount_ClampsToLast ()
        {
            using var owner = new Control ();
            var a = new Control ();
            var b = new Control ();
            var c = new Control ();
            owner.Controls.Add (a);
            owner.Controls.Add (b);
            owner.Controls.Add (c);

            // newIndex past the end is clamped to Count - 1.
            owner.Controls.SetChildIndex (a, 99);

            Assert.Same (a, owner.Controls[2]);
        }

        [Fact]
        public void SetChildIndex_Null_Throws ()
        {
            using var owner = new Control ();

            Assert.Throws<ArgumentNullException> (() => owner.Controls.SetChildIndex (null!, 0));
        }

        [Fact]
        public void Insert_AtSpecificIndex_PlacesControl ()
        {
            using var owner = new Control ();
            var a = new Control ();
            var b = new Control ();
            var c = new Control ();
            owner.Controls.Add (a);
            owner.Controls.Add (c);

            owner.Controls.Insert (1, b);

            Assert.Equal (3, owner.Controls.Count);
            Assert.Same (a, owner.Controls[0]);
            Assert.Same (b, owner.Controls[1]);
            Assert.Same (c, owner.Controls[2]);
            Assert.Same (owner, b.Parent);
        }
    }
}
