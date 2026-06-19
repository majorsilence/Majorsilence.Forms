// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/LinkLabelTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms LinkLabelTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing). They pin the
    // LinkArea get/set semantics and validation, LinkBehavior, LinkVisited, Links collection
    // behavior, link color properties, TabStop selectability, and the LinkClicked event.
    public class LinkLabelTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new LinkLabel ();

            Assert.True (control.LinkArea.IsEmpty);
            Assert.Equal (0, control.LinkArea.Start);
            Assert.Equal (0, control.LinkArea.Length);
            Assert.Equal (new LinkArea (0, 0), control.LinkArea);
            Assert.Equal (LinkBehavior.SystemDefault, control.LinkBehavior);
            Assert.False (control.LinkVisited);
        }

        [Fact]
        public void FlatStyle_Get_ReturnsExpected ()
        {
            using var control = new LinkLabel ();
            Assert.Equal (FlatStyle.Standard, control.FlatStyle);
        }

        [Theory]
        [InlineData (FlatStyle.Flat)]
        [InlineData (FlatStyle.Popup)]
        [InlineData (FlatStyle.System)]
        [InlineData (FlatStyle.Standard)]
        public void FlatStyle_Set_ReturnsExpected (FlatStyle flatStyle)
        {
            using var control = new LinkLabel { FlatStyle = flatStyle };
            Assert.Equal (flatStyle, control.FlatStyle);
        }

        [Fact]
        public void LinkArea_Get_ReturnsExpected ()
        {
            using var control = new LinkLabel ();
            Assert.Equal (new LinkArea (0, 0), control.LinkArea);
        }

        [Fact]
        public void LinkArea_Set_ReturnsExpected ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };

            var linkArea1 = new LinkArea (1, 2);
            var linkArea2 = new LinkArea (3, 4);

            control.LinkArea = linkArea1;
            Assert.Equal (linkArea1, control.LinkArea);

            control.LinkArea = linkArea2;
            Assert.Equal (linkArea2, control.LinkArea);
        }

        [Fact]
        public void LinkArea_Set_ReplacesLinksCollection ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };

            control.LinkArea = new LinkArea (1, 2);
            Assert.Equal (1, control.Links.Count);
            Assert.Equal (1, control.Links[0].Start);
            Assert.Equal (2, control.Links[0].Length);
        }

        [Fact]
        public void LinkArea_SetEmpty_ClearsLinks ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };

            control.LinkArea = new LinkArea (1, 2);
            Assert.Equal (1, control.Links.Count);

            control.LinkArea = new LinkArea (0, 0);
            Assert.Equal (0, control.Links.Count);
        }

        [Theory]
        [InlineData (-1, 2)]   // Negative Start
        [InlineData (-5, 0)]
        [InlineData (1, -2)]   // Length less than -1
        [InlineData (0, -100)]
        public void LinkArea_Set_InvalidValues_ThrowsArgumentOutOfRangeException (int start, int length)
        {
            using var control = new LinkLabel ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.LinkArea = new LinkArea (start, length));
        }

        [Fact]
        public void LinkArea_Set_UpdatesSelectability ()
        {
            using var control = new LinkLabel { Text = "Text" };

            control.LinkArea = new LinkArea (1, 2);
            Assert.True (control.TabStop);

            control.LinkArea = new LinkArea (0, 0);
            Assert.False (control.TabStop);
        }

        [Fact]
        public void LinkBehavior_Get_ReturnsExpected ()
        {
            using var control = new LinkLabel ();
            Assert.Equal (LinkBehavior.SystemDefault, control.LinkBehavior);
        }

        [Theory]
        [InlineData (LinkBehavior.SystemDefault)]
        [InlineData (LinkBehavior.AlwaysUnderline)]
        [InlineData (LinkBehavior.HoverUnderline)]
        [InlineData (LinkBehavior.NeverUnderline)]
        public void LinkBehavior_Set_ReturnsExpected (LinkBehavior linkBehavior)
        {
            using var control = new LinkLabel { LinkBehavior = linkBehavior };
            Assert.Equal (linkBehavior, control.LinkBehavior);

            // Set same.
            control.LinkBehavior = linkBehavior;
            Assert.Equal (linkBehavior, control.LinkBehavior);
        }

        [Fact]
        public void LinkVisited_Get_ReturnsExpected ()
        {
            using var control = new LinkLabel ();
            Assert.False (control.LinkVisited);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void LinkVisited_Set_ReturnsExpected (bool expectedVisited)
        {
            using var control = new LinkLabel { LinkVisited = expectedVisited };
            Assert.Equal (expectedVisited, control.LinkVisited);
        }

        [Fact]
        public void LinkVisited_Set_AddsLinkIfNoneExists ()
        {
            using var control = new LinkLabel { Text = "Hello" };
            control.Links.Clear ();

            control.LinkVisited = true;

            Assert.Equal (1, control.Links.Count);
            Assert.True (control.Links[0].Visited);
        }

        [Fact]
        public void LinkVisited_Set_UpdatesExistingLink ()
        {
            using var control = new LinkLabel { Text = "Hello" };
            control.Links.Clear ();
            control.Links.Add (new LinkLabel.Link (0, 2) { Visited = false });

            control.LinkVisited = true;
            Assert.True (control.Links[0].Visited);

            control.LinkVisited = false;
            Assert.False (control.Links[0].Visited);
        }

        [Fact]
        public void TabStop_Get_ReturnsExpected ()
        {
            using var control = new LinkLabel ();
            Assert.False (control.TabStop);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void TabStop_Set_ReturnsExpected (bool expectedTabStop)
        {
            using var control = new LinkLabel { TabStop = expectedTabStop };
            Assert.Equal (expectedTabStop, control.TabStop);
        }

        [Fact]
        public void TabStop_Set_RaisesTabStopChangedEvent ()
        {
            using var control = new LinkLabel ();
            // Default TabStop is false (no selectable links), so start from a known state.
            control.TabStop = false;

            var eventRaised = false;
            control.TabStopChanged += (sender, e) => eventRaised = true;

            control.TabStop = true;
            Assert.True (eventRaised);

            eventRaised = false;
            control.TabStop = false;
            Assert.True (eventRaised);
        }

        [Fact]
        public void Padding_Get_ReturnsExpected ()
        {
            using var control = new LinkLabel ();
            Assert.Equal (new Padding (0), control.Padding);
        }

        [Fact]
        public void Padding_Set_ReturnsExpected ()
        {
            using var control = new LinkLabel ();

            var padding1 = new Padding (1, 2, 3, 4);
            var padding2 = new Padding (5);

            control.Padding = padding1;
            Assert.Equal (padding1, control.Padding);

            control.Padding = padding2;
            Assert.Equal (padding2, control.Padding);
        }

        // Color round-trips through SkiaSharp, which discards the named-color identity,
        // so colors are compared by ARGB rather than reference/name equality.
        private static void AssertSameArgb (Color expected, Color actual)
            => Assert.Equal (expected.ToArgb (), actual.ToArgb ());

        [Fact]
        public void LinkColor_Set_ReturnsExpected ()
        {
            using var control = new LinkLabel ();

            control.LinkColor = Color.Red;
            AssertSameArgb (Color.Red, control.LinkColor);

            control.LinkColor = Color.Blue;
            AssertSameArgb (Color.Blue, control.LinkColor);
        }

        [Fact]
        public void ActiveLinkColor_Set_ReturnsExpected ()
        {
            using var control = new LinkLabel ();

            control.ActiveLinkColor = Color.Red;
            AssertSameArgb (Color.Red, control.ActiveLinkColor);

            control.ActiveLinkColor = Color.Blue;
            AssertSameArgb (Color.Blue, control.ActiveLinkColor);
        }

        [Fact]
        public void VisitedLinkColor_Set_ReturnsExpected ()
        {
            using var control = new LinkLabel ();

            control.VisitedLinkColor = Color.Red;
            AssertSameArgb (Color.Red, control.VisitedLinkColor);

            control.VisitedLinkColor = Color.Blue;
            AssertSameArgb (Color.Blue, control.VisitedLinkColor);
        }

        [Fact]
        public void DisabledLinkColor_Set_ReturnsExpected ()
        {
            using var control = new LinkLabel ();

            control.DisabledLinkColor = Color.Red;
            AssertSameArgb (Color.Red, control.DisabledLinkColor);
        }

        [Fact]
        public void Links_Add_IncrementsCount ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();

            var link = control.Links.Add (0, 5);
            Assert.Equal (1, control.Links.Count);
            Assert.Same (link, control.Links[0]);
            Assert.Equal (0, link.Start);
            Assert.Equal (5, link.Length);
        }

        [Fact]
        public void Links_Add_WithLinkData_ReturnsExpected ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();

            var data = new object ();
            var link = control.Links.Add (0, 5, data);
            Assert.Same (data, link.LinkData);
        }

        [Fact]
        public void Links_Add_Null_ThrowsArgumentNullException ()
        {
            using var control = new LinkLabel ();
            Assert.Throws<ArgumentNullException> (() => control.Links.Add (null!));
        }

        [Fact]
        public void Links_Add_OverlappingLinks_ThrowsInvalidOperationException ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            control.Links.Add (0, 5);

            Assert.Throws<InvalidOperationException> (() => control.Links.Add (2, 5));
        }

        [Fact]
        public void Links_Add_SortsByStart ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();

            control.Links.Add (6, 5);
            control.Links.Add (0, 5);

            Assert.Equal (0, control.Links[0].Start);
            Assert.Equal (6, control.Links[1].Start);
        }

        [Fact]
        public void Links_Clear_RemovesAll ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            control.Links.Add (0, 5);

            control.Links.Clear ();
            Assert.Equal (0, control.Links.Count);
        }

        [Fact]
        public void Links_Remove_RemovesLink ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            var link = control.Links.Add (0, 5);

            Assert.True (control.Links.Remove (link));
            Assert.Equal (0, control.Links.Count);
            Assert.False (control.Links.Remove (link));
        }

        [Fact]
        public void Links_RemoveAt_RemovesLink ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            control.Links.Add (0, 5);

            control.Links.RemoveAt (0);
            Assert.Equal (0, control.Links.Count);
        }

        [Fact]
        public void Links_Contains_ReturnsExpected ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            var link = control.Links.Add (0, 5);

            Assert.True (control.Links.Contains (link));
            Assert.False (control.Links.Contains (new LinkLabel.Link (0, 1)));
        }

        [Fact]
        public void Links_IndexOf_ReturnsExpected ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            var link = control.Links.Add (0, 5);

            Assert.Equal (0, control.Links.IndexOf (link));
            Assert.Equal (-1, control.Links.IndexOf (new LinkLabel.Link (0, 1)));
        }

        [Fact]
        public void Links_IndexerByKey_ReturnsExpected ()
        {
            using var control = new LinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            var link = control.Links.Add (0, 5);
            link.Name = "first";

            Assert.Same (link, control.Links["first"]);
            Assert.Same (link, control.Links["FIRST"]);
            Assert.Null (control.Links["missing"]);
            Assert.Null (control.Links[string.Empty]);
        }

        [Fact]
        public void Link_DefaultLength_SpansRemainingText ()
        {
            using var control = new LinkLabel { Text = "Hello" };
            control.Links.Clear ();

            // A length of -1 means the link extends to the end of the text.
            var link = control.Links.Add (0, -1);
            Assert.Equal (5, link.Length);
        }

        [Fact]
        public void Link_Enabled_DefaultsToTrue ()
        {
            var link = new LinkLabel.Link (0, 5);
            Assert.True (link.Enabled);
        }

        [Fact]
        public void Link_Visited_DefaultsToFalse ()
        {
            var link = new LinkLabel.Link (0, 5);
            Assert.False (link.Visited);
        }

        [Fact]
        public void Link_Name_DefaultsToEmpty ()
        {
            var link = new LinkLabel.Link ();
            Assert.Equal (string.Empty, link.Name);
        }

        [Fact]
        public void Link_Ctor_StartLength_SetsProperties ()
        {
            var link = new LinkLabel.Link (3, 4);
            Assert.Equal (3, link.Start);
            Assert.Equal (4, link.Length);
        }

        [Fact]
        public void Link_Ctor_StartLengthData_SetsProperties ()
        {
            var data = new object ();
            var link = new LinkLabel.Link (3, 4, data);
            Assert.Equal (3, link.Start);
            Assert.Equal (4, link.Length);
            Assert.Same (data, link.LinkData);
        }

        [Fact]
        public void LinkClicked_RaisesEvent ()
        {
            using var control = new TestLinkLabel { Text = "Hello, world!" };
            control.Links.Clear ();
            var link = control.Links.Add (0, 5);

            LinkLabelLinkClickedEventArgs? raised = null;
            control.LinkClicked += (sender, e) => raised = e;

            var args = new LinkLabelLinkClickedEventArgs (link);
            control.OnLinkClicked (args);

            Assert.Same (args, raised);
            Assert.Same (link, raised!.Link);
        }

        [Fact]
        public void LinkLabelLinkClickedEventArgs_Ctor_DefaultsToLeftButton ()
        {
            var link = new LinkLabel.Link (0, 5);
            var args = new LinkLabelLinkClickedEventArgs (link);

            Assert.Same (link, args.Link);
            Assert.Equal (MouseButtons.Left, args.Button);
        }

        [Fact]
        public void LinkLabelLinkClickedEventArgs_Ctor_SetsButton ()
        {
            var link = new LinkLabel.Link (0, 5);
            var args = new LinkLabelLinkClickedEventArgs (link, MouseButtons.Right);

            Assert.Same (link, args.Link);
            Assert.Equal (MouseButtons.Right, args.Button);
        }

        [Fact]
        public void LinkArea_Ctor_SetsProperties ()
        {
            var area = new LinkArea (3, 4);
            Assert.Equal (3, area.Start);
            Assert.Equal (4, area.Length);
            Assert.False (area.IsEmpty);
        }

        [Fact]
        public void LinkArea_Empty_IsEmpty ()
        {
            Assert.True (new LinkArea (0, 0).IsEmpty);
            Assert.False (new LinkArea (1, 0).IsEmpty);
            Assert.False (new LinkArea (0, 1).IsEmpty);
        }

        [Fact]
        public void LinkArea_Equality_ReturnsExpected ()
        {
            var area1 = new LinkArea (1, 2);
            var area2 = new LinkArea (1, 2);
            var area3 = new LinkArea (3, 4);

            Assert.True (area1 == area2);
            Assert.True (area1.Equals (area2));
            Assert.Equal (area1.GetHashCode (), area2.GetHashCode ());
            Assert.False (area1 == area3);
            Assert.True (area1 != area3);
        }

        private class TestLinkLabel : LinkLabel
        {
            public new void OnLinkClicked (LinkLabelLinkClickedEventArgs e) => base.OnLinkClicked (e);
        }
    }
}
