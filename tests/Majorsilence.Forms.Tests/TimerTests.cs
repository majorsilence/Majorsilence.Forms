// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/TimerTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms TimerTests, adapted to the Majorsilence.Forms
    // API (plain xUnit, real Timer surface; no Moq/ISite/DesignMode/CreateParams plumbing).
    // They pin the same defaults, Interval validation, Enabled/Start/Stop toggling, Tick
    // subscription, Tag round-trip, and Dispose semantics.
    //
    // NOTE: these tests deliberately never assert that the Tick event fires over real wall-clock
    // time. A real tick requires a running message loop, so the platform timer would Post the
    // callback onto a dispatcher that is not pumped under xUnit. We only exercise the
    // enabling/configuration surface, which is fully deterministic.
    public class TimerTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var timer = new Timer ();

            Assert.False (timer.Enabled);
            Assert.Equal (100, timer.Interval);
            Assert.Null (timer.Tag);
        }

        [Fact]
        public void Ctor_NullContainer_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException> (() => new Timer (null!));
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Enabled_Set_GetReturnsExpected (bool value)
        {
            using var timer = new Timer { Enabled = value };
            Assert.Equal (value, timer.Enabled);

            // Set same.
            timer.Enabled = value;
            Assert.Equal (value, timer.Enabled);
        }

        [Theory]
        [InlineData (1)]
        [InlineData (100)]
        public void Interval_Set_GetReturnsExpected (int value)
        {
            using var timer = new Timer { Interval = value };
            Assert.Equal (value, timer.Interval);

            // Set same.
            timer.Interval = value;
            Assert.Equal (value, timer.Interval);
        }

        [Theory]
        [InlineData (1)]
        [InlineData (100)]
        public void Interval_SetStarted_GetReturnsExpected (int value)
        {
            using var timer = new Timer ();
            timer.Start ();

            timer.Interval = value;
            Assert.Equal (value, timer.Interval);

            // Set same.
            timer.Interval = value;
            Assert.Equal (value, timer.Interval);
        }

        [Theory]
        [InlineData (1)]
        [InlineData (100)]
        public void Interval_SetStopped_GetReturnsExpected (int value)
        {
            using var timer = new Timer ();
            timer.Start ();
            timer.Stop ();

            timer.Interval = value;
            Assert.Equal (value, timer.Interval);

            // Set same.
            timer.Interval = value;
            Assert.Equal (value, timer.Interval);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (-1)]
        public void Interval_SetInvalid_ThrowsArgumentOutOfRangeException (int value)
        {
            using var timer = new Timer ();
            Assert.Throws<ArgumentOutOfRangeException> (() => timer.Interval = value);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("value")]
        public void Tag_Set_GetReturnsExpected (object? value)
        {
            using var timer = new Timer { Tag = value };
            Assert.Same (value, timer.Tag);

            // Set same.
            timer.Tag = value;
            Assert.Same (value, timer.Tag);
        }

        [Fact]
        public void Start_Stop_Success ()
        {
            using var timer = new Timer ();

            // Start.
            timer.Start ();
            Assert.True (timer.Enabled);

            // Stop.
            timer.Stop ();
            Assert.False (timer.Enabled);
        }

        [Fact]
        public void Start_MultipleTimes_Success ()
        {
            using var timer = new Timer ();

            timer.Start ();
            Assert.True (timer.Enabled);

            // Start again.
            timer.Start ();
            Assert.True (timer.Enabled);
        }

        [Fact]
        public void Stop_Restart_Success ()
        {
            using var timer = new Timer ();

            timer.Start ();
            Assert.True (timer.Enabled);

            timer.Stop ();
            Assert.False (timer.Enabled);

            // Start again.
            timer.Start ();
            Assert.True (timer.Enabled);

            // Stop again.
            timer.Stop ();
            Assert.False (timer.Enabled);
        }

        [Fact]
        public void Stop_MultipleTimes_Success ()
        {
            using var timer = new Timer ();

            timer.Start ();
            Assert.True (timer.Enabled);

            timer.Stop ();
            Assert.False (timer.Enabled);

            // Stop again.
            timer.Stop ();
            Assert.False (timer.Enabled);
        }

        [Fact]
        public void Tick_AddRemove_DoesNotThrow ()
        {
            using var timer = new Timer ();
            var callCount = 0;
            EventHandler handler = (sender, e) => callCount++;

            // Add and remove without ever raising the event (no message loop in tests).
            timer.Tick += handler;
            timer.Tick -= handler;

            Assert.Equal (0, callCount);
        }

        [Fact]
        public void Dispose_NotStarted_Success ()
        {
            using var timer = new Timer ();

            timer.Dispose ();
            Assert.False (timer.Enabled);

            // Call again.
            timer.Dispose ();
            Assert.False (timer.Enabled);
        }

        [Fact]
        public void Dispose_Started_Success ()
        {
            using var timer = new Timer ();
            timer.Start ();

            timer.Dispose ();
            Assert.False (timer.Enabled);

            // Call again.
            timer.Dispose ();
            Assert.False (timer.Enabled);
        }

        [Fact]
        public void Dispose_Stopped_Success ()
        {
            using var timer = new Timer ();
            timer.Start ();
            timer.Stop ();

            timer.Dispose ();
            Assert.False (timer.Enabled);

            // Call again.
            timer.Dispose ();
            Assert.False (timer.Enabled);
        }

        [Fact]
        public void ToString_Invoke_ReturnsExpected ()
        {
            using var timer = new Timer ();
            Assert.Equal ("Majorsilence.Forms.Timer, Interval: 100", timer.ToString ());
        }
    }
}
