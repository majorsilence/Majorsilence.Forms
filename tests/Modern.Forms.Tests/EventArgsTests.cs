// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (MouseEventArgsTests.cs, KeyEventArgsTests.cs,
// KeyPressEventArgsTests.cs, ScrollEventArgsTests.cs, LayoutEventArgsTests.cs under
// src/test/unit/System.Windows.Forms/System/Windows/Forms/), rewritten for the Modern.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    public class MouseEventArgsTests
    {
        [Theory]
        [InlineData (MouseButtons.Left, 1, 10, 20)]
        [InlineData (MouseButtons.Right, 2, -5, -6)]
        [InlineData (MouseButtons.None, 0, 0, 0)]
        public void Ctor_SetsProperties (MouseButtons button, int clicks, int x, int y)
        {
            var e = new MouseEventArgs (button, clicks, x, y, new Point (3, 4));

            Assert.Equal (button, e.Button);
            Assert.Equal (clicks, e.Clicks);
            Assert.Equal (x, e.X);
            Assert.Equal (y, e.Y);
            Assert.Equal (new Point (3, 4), e.Delta);
            Assert.Equal (new Point (x, y), e.Location);
        }

        [Fact]
        public void ScreenLocation_DefaultsToClientLocation ()
        {
            var e = new MouseEventArgs (MouseButtons.Left, 1, 7, 8, Point.Empty);
            Assert.Equal (new Point (7, 8), e.ScreenLocation);
        }

        [Fact]
        public void ScreenLocation_UsesExplicitScreenCoordinates ()
        {
            var e = new MouseEventArgs (MouseButtons.Left, 1, 7, 8, Point.Empty, screenX: 100, screenY: 200);
            Assert.Equal (new Point (100, 200), e.ScreenLocation);
        }

        [Fact]
        public void Modifiers_ReflectKeyData ()
        {
            var e = new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty, keyData: Keys.Control | Keys.Shift);

            Assert.True (e.Control);
            Assert.True (e.Shift);
            Assert.False (e.Alt);
            Assert.Equal (Keys.Control | Keys.Shift, e.Modifiers);
        }
    }

    public class KeyEventArgsTests
    {
        [Fact]
        public void Ctor_SetsKeyData ()
        {
            var e = new KeyEventArgs (Keys.A | Keys.Control);

            Assert.Equal (Keys.A | Keys.Control, e.KeyData);
            Assert.Equal (Keys.A, e.KeyCode);
            Assert.Equal ((int) Keys.A, e.KeyValue);
            Assert.Equal (Keys.Control, e.Modifiers);
            Assert.True (e.Control);
            Assert.False (e.Alt);
            Assert.False (e.Shift);
            Assert.False (e.Handled);
        }

        [Fact]
        public void Handled_GetSet ()
        {
            var e = new KeyEventArgs (Keys.A) { Handled = true };
            Assert.True (e.Handled);
        }

        [Fact]
        public void SuppressKeyPress_AlsoSetsHandled ()
        {
            var e = new KeyEventArgs (Keys.A);

            e.SuppressKeyPress = true;
            Assert.True (e.SuppressKeyPress);
            Assert.True (e.Handled);

            e.SuppressKeyPress = false;
            Assert.False (e.SuppressKeyPress);
            Assert.False (e.Handled);
        }

        [Fact]
        public void Modifiers_AllThree ()
        {
            var e = new KeyEventArgs (Keys.B | Keys.Control | Keys.Alt | Keys.Shift);

            Assert.Equal (Keys.B, e.KeyCode);
            Assert.True (e.Control);
            Assert.True (e.Alt);
            Assert.True (e.Shift);
            Assert.Equal (Keys.Control | Keys.Alt | Keys.Shift, e.Modifiers);
        }
    }

    public class KeyPressEventArgsTests
    {
        [Theory]
        [InlineData ("a")]
        [InlineData ("Z")]
        [InlineData ("5")]
        public void Ctor_SetsKeyCharAndText (string text)
        {
            var e = new KeyPressEventArgs (text);

            Assert.Equal (text, e.Text);
            Assert.Equal (text[0], e.KeyChar);
            Assert.False (e.Handled);
        }

        [Fact]
        public void Handled_GetSet ()
        {
            var e = new KeyPressEventArgs ("a") { Handled = true };
            Assert.True (e.Handled);
        }

        [Fact]
        public void KeyChar_GetSet ()
        {
            var e = new KeyPressEventArgs ("a") { KeyChar = 'b' };
            Assert.Equal ('b', e.KeyChar);
        }

        [Fact]
        public void Modifiers_ReflectKeyData ()
        {
            var e = new KeyPressEventArgs ("a", Keys.Alt);
            Assert.True (e.Alt);
            Assert.Equal (Keys.Alt, e.Modifiers);
        }
    }

    public class ScrollEventArgsTests
    {
        [Fact]
        public void Ctor_TypeNewValue_OldValueIsMinusOne ()
        {
            var e = new ScrollEventArgs (ScrollEventType.SmallIncrement, 5);

            Assert.Equal (ScrollEventType.SmallIncrement, e.Type);
            Assert.Equal (5, e.NewValue);
            Assert.Equal (-1, e.OldValue);
        }

        [Fact]
        public void Ctor_TypeNewValueOrientation ()
        {
            var e = new ScrollEventArgs (ScrollEventType.LargeDecrement, 9, ScrollOrientation.VerticalScroll);

            Assert.Equal (ScrollEventType.LargeDecrement, e.Type);
            Assert.Equal (9, e.NewValue);
            Assert.Equal (-1, e.OldValue);
            Assert.Equal (ScrollOrientation.VerticalScroll, e.ScrollOrientation);
        }

        [Fact]
        public void Ctor_TypeOldValueNewValue ()
        {
            var e = new ScrollEventArgs (ScrollEventType.ThumbTrack, 3, 7);

            Assert.Equal (ScrollEventType.ThumbTrack, e.Type);
            Assert.Equal (3, e.OldValue);
            Assert.Equal (7, e.NewValue);
        }

        [Fact]
        public void Ctor_TypeOldValueNewValueOrientation ()
        {
            var e = new ScrollEventArgs (ScrollEventType.ThumbPosition, 3, 7, ScrollOrientation.HorizontalScroll);

            Assert.Equal (3, e.OldValue);
            Assert.Equal (7, e.NewValue);
            Assert.Equal (ScrollOrientation.HorizontalScroll, e.ScrollOrientation);
        }

        [Fact]
        public void NewValue_IsSettable ()
        {
            var e = new ScrollEventArgs (ScrollEventType.EndScroll, 1) { NewValue = 42 };
            Assert.Equal (42, e.NewValue);
        }
    }

    public class LayoutEventArgsTests
    {
        [Fact]
        public void Ctor_ControlAndProperty ()
        {
            using var control = new Control ();
            var e = new LayoutEventArgs (control, "Bounds");

            Assert.Same (control, e.AffectedControl);
            Assert.Equal ("Bounds", e.AffectedProperty);
        }

        [Fact]
        public void Ctor_NullControl ()
        {
            var e = new LayoutEventArgs ((Control?) null, "Visible");

            Assert.Null (e.AffectedControl);
            Assert.Equal ("Visible", e.AffectedProperty);
        }

        [Fact]
        public void Ctor_NullComponent ()
        {
            var e = new LayoutEventArgs ((System.ComponentModel.IComponent?) null, "Text");

            Assert.Null (e.AffectedComponent);
            Assert.Equal ("Text", e.AffectedProperty);
        }
    }
}
