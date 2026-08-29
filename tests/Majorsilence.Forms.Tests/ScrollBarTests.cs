// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ScrollBarTests.cs and H/VScrollBarTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ScrollBar/HScrollBar/VScrollBar tests,
    // adapted to the Majorsilence.Forms API (no Handle/CreateParams/accessibility/ImeMode/RightToLeft
    // plumbing). ScrollBar is abstract, so the concrete HorizontalScrollBar/VerticalScrollBar are
    // exercised instead. They pin the WinForms range semantics: Minimum/Maximum coerce each other
    // (and Value) rather than throwing, Value-out-of-range throws, LargeChange/SmallChange reject
    // negatives, LargeChange is clamped to the range span, SmallChange is clamped to LargeChange,
    // and ValueChanged fires only on a real change.
    public class ScrollBarTests
    {
        // ---- Construction / defaults ----

        [Fact]
        public void HorizontalScrollBar_Ctor_Default ()
        {
            using var control = new HorizontalScrollBar ();

            Assert.Equal (0, control.Minimum);
            Assert.Equal (100, control.Maximum);
            Assert.Equal (0, control.Value);
            Assert.Equal (10, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
            Assert.False (control.TabStop);
            Assert.Equal (new Size (80, 15), control.Size);
        }

        [Fact]
        public void VerticalScrollBar_Ctor_Default ()
        {
            using var control = new VerticalScrollBar ();

            Assert.Equal (0, control.Minimum);
            Assert.Equal (100, control.Maximum);
            Assert.Equal (0, control.Value);
            Assert.Equal (10, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
            Assert.False (control.TabStop);
            Assert.Equal (new Size (15, 80), control.Size);
        }

        // ---- Maximum ----

        [Theory]
        [InlineData (0, 1)]
        [InlineData (8, 9)]
        [InlineData (10, 10)]
        [InlineData (11, 10)]
        public void Maximum_Set_GetReturnsExpected (int value, int expectedLargeChange)
        {
            using var control = new HorizontalScrollBar { Maximum = value };

            Assert.Equal (value, control.Maximum);
            Assert.Equal (0, control.Minimum);
            Assert.Equal (0, control.Value);
            Assert.Equal (expectedLargeChange, control.LargeChange);
            Assert.Equal (1, control.SmallChange);

            // Set same.
            control.Maximum = value;
            Assert.Equal (value, control.Maximum);
            Assert.Equal (0, control.Minimum);
            Assert.Equal (0, control.Value);
            Assert.Equal (expectedLargeChange, control.LargeChange);
        }

        [Fact]
        public void Maximum_SetLessThanValueAndMinimum_SetsValueAndMinimum ()
        {
            using var control = new HorizontalScrollBar {
                Value = 10,
                Minimum = 8,
                Maximum = 5
            };

            Assert.Equal (5, control.Maximum);
            Assert.Equal (5, control.Minimum);
            Assert.Equal (5, control.Value);
            Assert.Equal (1, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
        }

        [Fact]
        public void Maximum_SetNegative_SetsValueAndMinimum ()
        {
            using var control = new HorizontalScrollBar { Maximum = -1 };

            Assert.Equal (-1, control.Maximum);
            Assert.Equal (-1, control.Minimum);
            Assert.Equal (-1, control.Value);
            Assert.Equal (1, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
        }

        // ---- Minimum ----

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (5)]
        public void Minimum_Set_GetReturnsExpected (int value)
        {
            using var control = new HorizontalScrollBar {
                Value = 5,
                Minimum = value
            };

            Assert.Equal (100, control.Maximum);
            Assert.Equal (value, control.Minimum);
            Assert.Equal (5, control.Value);
            Assert.Equal (10, control.LargeChange);
            Assert.Equal (1, control.SmallChange);

            // Set same.
            control.Minimum = value;
            Assert.Equal (100, control.Maximum);
            Assert.Equal (value, control.Minimum);
            Assert.Equal (5, control.Value);
        }

        [Fact]
        public void Minimum_SetGreaterThanValueAndMaximum_SetsValueAndMaximum ()
        {
            using var control = new HorizontalScrollBar {
                Value = 10,
                Maximum = 8,
                Minimum = 12
            };

            Assert.Equal (12, control.Maximum);
            Assert.Equal (12, control.Minimum);
            Assert.Equal (12, control.Value);
            Assert.Equal (1, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
        }

        // ---- Value ----

        [Theory]
        [InlineData (0)]
        [InlineData (5)]
        [InlineData (90)]
        [InlineData (91)]
        [InlineData (100)]
        public void Value_Set_GetReturnsExpected (int value)
        {
            using var control = new HorizontalScrollBar { Value = value };

            Assert.Equal (100, control.Maximum);
            Assert.Equal (0, control.Minimum);
            Assert.Equal (value, control.Value);
            Assert.Equal (10, control.LargeChange);
            Assert.Equal (1, control.SmallChange);

            // Set same.
            control.Value = value;
            Assert.Equal (value, control.Value);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (101)]
        public void Value_SetOutOfRange_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new HorizontalScrollBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = value);
            Assert.Equal (0, control.Value);
        }

        [Fact]
        public void Value_SetWithHandler_CallsValueChanged ()
        {
            using var control = new HorizontalScrollBar ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.ValueChanged += handler;

            // Set different.
            control.Value = 1;
            Assert.Equal (1, control.Value);
            Assert.Equal (1, callCount);

            // Set same.
            control.Value = 1;
            Assert.Equal (1, control.Value);
            Assert.Equal (1, callCount);

            // Set different.
            control.Value = 2;
            Assert.Equal (2, control.Value);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.ValueChanged -= handler;
            control.Value = 1;
            Assert.Equal (1, control.Value);
            Assert.Equal (2, callCount);
        }

        // ---- LargeChange ----

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        [InlineData (10)]
        public void LargeChange_Set_GetReturnsExpected (int value)
        {
            using var control = new HorizontalScrollBar { LargeChange = value };

            Assert.Equal (value, control.LargeChange);

            // Set same.
            control.LargeChange = value;
            Assert.Equal (value, control.LargeChange);
        }

        [Fact]
        public void LargeChange_SetLarge_GetReturnsExpected ()
        {
            // The getter clamps LargeChange to (Maximum - Minimum + 1).
            using var control = new HorizontalScrollBar {
                Minimum = 5,
                Maximum = 10,
                LargeChange = 7
            };

            Assert.Equal (6, control.LargeChange);

            // Widen the range; the stored value (7) now fits.
            control.Maximum = 15;
            Assert.Equal (7, control.LargeChange);
        }

        [Fact]
        public void LargeChange_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new HorizontalScrollBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.LargeChange = -1);
            Assert.Equal (10, control.LargeChange);
        }

        // ---- SmallChange ----

        [Theory]
        [InlineData (0, 0)]
        [InlineData (1, 1)]
        [InlineData (5, 5)]
        [InlineData (10, 10)]
        [InlineData (11, 10)]
        public void SmallChange_Set_GetReturnsExpected (int value, int expected)
        {
            // The getter clamps SmallChange to LargeChange (default 10).
            using var control = new HorizontalScrollBar { SmallChange = value };

            Assert.Equal (expected, control.SmallChange);

            // Set same.
            control.SmallChange = value;
            Assert.Equal (expected, control.SmallChange);
        }

        [Fact]
        public void SmallChange_SetLarge_GetReturnsExpected ()
        {
            using var control = new HorizontalScrollBar {
                LargeChange = 10,
                SmallChange = 11
            };

            Assert.Equal (10, control.SmallChange);

            // Widen LargeChange; the stored value (11) now fits.
            control.LargeChange = 15;
            Assert.Equal (11, control.SmallChange);
        }

        [Fact]
        public void SmallChange_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new HorizontalScrollBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SmallChange = -1);
            Assert.Equal (1, control.SmallChange);
        }

        // ---- VerticalScrollBar shares the same coercion logic ----

        [Fact]
        public void Vertical_Maximum_SetLessThanValueAndMinimum_SetsValueAndMinimum ()
        {
            using var control = new VerticalScrollBar {
                Value = 10,
                Minimum = 8,
                Maximum = 5
            };

            Assert.Equal (5, control.Maximum);
            Assert.Equal (5, control.Minimum);
            Assert.Equal (5, control.Value);
            Assert.Equal (1, control.LargeChange);
        }

        [Fact]
        public void Vertical_Value_SetOutOfRange_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new VerticalScrollBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = 101);
            Assert.Equal (0, control.Value);
        }

        // ---- User-initiated scroll raises Scroll (regression) ----
        //
        // Arrow/track/wheel raised no Scroll event at all, and thumb-drag raised it only after
        // committing Value (so e.NewValue always equalled Value). Every migrated handler that
        // scrolls its view from ScrollBar.Scroll -- RdlViewer's report pane, notably -- did nothing.

        private sealed class TestVScrollBar : VerticalScrollBar
        {
            public TestVScrollBar () { Size = new Size (17, 200); }
            public void Wheel (int delta) => OnMouseWheel (new MouseEventArgs (MouseButtons.None, 0, 8, 100, delta));
            public void ClickTopArrow () => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 8, 2, 0));
            public void ClickBottomArrow () => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 8, Height - 2, 0));
        }

        [Fact]
        public void Wheel_raises_Scroll_with_the_proposed_value_before_Value_updates ()
        {
            using var control = new TestVScrollBar { Minimum = 0, Maximum = 100, SmallChange = 1, Value = 50 };

            int seenNew = -1, seenValueAtEvent = -1, raised = 0;
            control.Scroll += (_, e) => { raised++; seenNew = e.NewValue; seenValueAtEvent = control.Value; };

            control.Wheel (-3);   // wheel down

            Assert.Equal (1, raised);
            Assert.Equal (50, seenValueAtEvent);   // Value not committed yet when Scroll fires
            Assert.Equal (53, seenNew);            // proposed value handed to the handler
            Assert.Equal (53, control.Value);      // committed after
        }

        [Fact]
        public void Down_arrow_raises_Scroll_and_moves_by_SmallChange ()
        {
            using var control = new TestVScrollBar { Minimum = 0, Maximum = 100, SmallChange = 4, Value = 20 };

            var raised = 0;
            control.Scroll += (_, _) => raised++;

            control.ClickBottomArrow ();

            Assert.Equal (1, raised);
            Assert.Equal (24, control.Value);
        }

        [Fact]
        public void A_handler_can_veto_a_scroll_by_rewriting_NewValue ()
        {
            using var control = new TestVScrollBar { Minimum = 0, Maximum = 100, SmallChange = 10, Value = 50 };

            control.Scroll += (_, e) => e.NewValue = 50;   // pin it

            control.Wheel (-1);

            Assert.Equal (50, control.Value);
        }
    }
}
