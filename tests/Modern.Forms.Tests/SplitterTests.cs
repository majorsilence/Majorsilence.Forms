// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/SplitterTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms SplitterTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing, no SubSplitter
    // protected-member harness). They pin the constructor defaults, the Dock coupling, the
    // MinSize/MinExtra negative-value coercion, and the SplitPosition/SplitterWidth aliasing.
    // Modern.Forms-specific behavior (the Orientation property, the Drag event) is also covered.
    public class SplitterTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new Splitter ();

            // The base Control DefaultSize is empty, so a fresh Splitter is zero-sized.
            Assert.Equal (Size.Empty, control.Size);
            Assert.Equal (0, control.Width);
            Assert.Equal (0, control.Height);

            // The constructor docks left and uses the west-east resize cursor.
            Assert.Equal (DockStyle.Left, control.Dock);
            Assert.Same (Cursors.SizeWestEast, control.Cursor);

            Assert.Equal (Orientation.Horizontal, control.Orientation);

            // WinForms-compatible defaults.
            Assert.Equal (25, control.MinSize);
            Assert.Equal (25, control.MinExtra);
        }

        [Theory]
        [InlineData (DockStyle.Left)]
        [InlineData (DockStyle.Right)]
        [InlineData (DockStyle.Top)]
        [InlineData (DockStyle.Bottom)]
        public void Dock_Set_GetReturnsExpected (DockStyle value)
        {
            using var control = new Splitter { Dock = value };
            Assert.Equal (value, control.Dock);

            // Set same.
            control.Dock = value;
            Assert.Equal (value, control.Dock);
        }

        [Theory]
        [InlineData (-1, 0)]
        [InlineData (0, 0)]
        [InlineData (25, 25)]
        [InlineData (50, 50)]
        public void MinSize_Set_GetReturnsExpected (int value, int expected)
        {
            using var control = new Splitter { MinSize = value };
            Assert.Equal (expected, control.MinSize);

            // Set same.
            control.MinSize = value;
            Assert.Equal (expected, control.MinSize);
        }

        [Theory]
        [InlineData (-1, 0)]
        [InlineData (0, 0)]
        [InlineData (25, 25)]
        [InlineData (50, 50)]
        public void MinExtra_Set_GetReturnsExpected (int value, int expected)
        {
            using var control = new Splitter { MinExtra = value };
            Assert.Equal (expected, control.MinExtra);

            // Set same.
            control.MinExtra = value;
            Assert.Equal (expected, control.MinExtra);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (25)]
        public void SplitterWidth_Set_GetReturnsExpected_Horizontal (int value)
        {
            // Default orientation is Horizontal, so SplitterWidth maps to Width.
            using var control = new Splitter { SplitterWidth = value };

            Assert.Equal (value, control.SplitterWidth);
            Assert.Equal (value, control.Width);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (25)]
        public void SplitterWidth_Set_GetReturnsExpected_Vertical (int value)
        {
            using var control = new Splitter { Orientation = Orientation.Vertical };

            control.SplitterWidth = value;

            // Vertical orientation maps SplitterWidth to Height.
            Assert.Equal (value, control.SplitterWidth);
            Assert.Equal (value, control.Height);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (25)]
        public void SplitPosition_IsAliasForSplitterWidth (int value)
        {
            using var control = new Splitter { SplitPosition = value };

            Assert.Equal (value, control.SplitPosition);
            Assert.Equal (value, control.SplitterWidth);
            Assert.Equal (value, control.Width);
        }

        [Fact]
        public void Orientation_SetVertical_UpdatesDockAndCursor ()
        {
            using var control = new Splitter ();

            control.Orientation = Orientation.Vertical;

            Assert.Equal (Orientation.Vertical, control.Orientation);
            Assert.Equal (DockStyle.Top, control.Dock);
            Assert.Same (Cursors.SizeNorthSouth, control.Cursor);
        }

        [Fact]
        public void Orientation_SetHorizontal_UpdatesDockAndCursor ()
        {
            using var control = new Splitter { Orientation = Orientation.Vertical };

            control.Orientation = Orientation.Horizontal;

            Assert.Equal (Orientation.Horizontal, control.Orientation);
            Assert.Equal (DockStyle.Left, control.Dock);
            Assert.Same (Cursors.SizeWestEast, control.Cursor);
        }

        [Fact]
        public void Orientation_SetSame_NoChange ()
        {
            using var control = new Splitter ();
            control.Cursor = Cursors.Hand;
            control.Dock = DockStyle.Right;

            // Setting the already-current orientation is a no-op and must not
            // clobber the explicitly-assigned Dock/Cursor.
            control.Orientation = Orientation.Horizontal;

            Assert.Equal (Orientation.Horizontal, control.Orientation);
            Assert.Equal (DockStyle.Right, control.Dock);
            Assert.Same (Cursors.Hand, control.Cursor);
        }

        [Fact]
        public void Orientation_SetVertical_SwapsSize ()
        {
            using var control = new Splitter ();
            control.Width = 40;
            control.Height = 5;

            control.Orientation = Orientation.Vertical;

            // The setter swaps the dimensions: new Size (Height, Width).
            Assert.Equal (5, control.Width);
            Assert.Equal (40, control.Height);
        }

        [Fact]
        public void Drag_AddRemoveHandler_DoesNotThrow ()
        {
            using var control = new Splitter ();

            EventHandler<EventArgs<Point>> handler = (sender, e) => { };

            control.Drag += handler;
            control.Drag -= handler;
        }

        [Fact]
        public void SplitterMoved_AddRemoveHandler_DoesNotThrow ()
        {
            // SplitterMoved is a no-op stub in Modern.Forms; subscribing/unsubscribing must be safe.
            using var control = new Splitter ();

            EventHandler<SplitterEventArgs> handler = (sender, e) => { };

            control.SplitterMoved += handler;
            control.SplitterMoved -= handler;
        }

        [Fact]
        public void SplitterMoving_AddRemoveHandler_DoesNotThrow ()
        {
            // SplitterMoving is a no-op stub in Modern.Forms; subscribing/unsubscribing must be safe.
            using var control = new Splitter ();

            EventHandler<SplitterCancelEventArgs> handler = (sender, e) => { };

            control.SplitterMoving += handler;
            control.SplitterMoving -= handler;
        }
    }
}
