// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/SystemInformationTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms SystemInformationTests, adapted to the
    // Modern.Forms API. SystemInformation in Modern.Forms is a cross-platform stub provider: most
    // metrics are fixed constants rather than OS-queried, so these tests pin the exact constants
    // Modern.Forms returns. The screen-derived members (WorkingArea, PrimaryMonitorSize,
    // MonitorCount, VirtualScreen) resolve through the Headless backend the test assembly installs,
    // which reports a single 1920x1080 primary monitor — so those assertions are deterministic too.
    public class SystemInformationTests
    {
        // The Headless backend (installed by the test assembly's ModuleInitializer) reports a
        // single primary screen with bounds and working area both (0, 0, 1920, 1080).
        private static readonly Size HeadlessMonitorSize = new Size (1920, 1080);
        private static readonly Rectangle HeadlessScreenBounds = new Rectangle (0, 0, 1920, 1080);

        // --- Scroll bar metrics (fixed constants) ---

        [Fact]
        public void HorizontalScrollBarHeight_Get_ReturnsExpected ()
        {
            Assert.Equal (17, SystemInformation.HorizontalScrollBarHeight);
        }

        [Fact]
        public void VerticalScrollBarWidth_Get_ReturnsExpected ()
        {
            Assert.Equal (17, SystemInformation.VerticalScrollBarWidth);
        }

        [Fact]
        public void HorizontalScrollBarThumbWidth_Get_ReturnsExpected ()
        {
            Assert.Equal (18, SystemInformation.HorizontalScrollBarThumbWidth);
        }

        [Fact]
        public void VerticalScrollBarThumbHeight_Get_ReturnsExpected ()
        {
            Assert.Equal (18, SystemInformation.VerticalScrollBarThumbHeight);
        }

        // --- Screen-derived metrics (resolved via the Headless backend) ---

        [Fact]
        public void WorkingArea_Get_ReturnsExpected ()
        {
            var workingArea = SystemInformation.WorkingArea;

            Assert.True (workingArea.X >= 0);
            Assert.True (workingArea.Y >= 0);
            Assert.True (workingArea.Width > 0);
            Assert.True (workingArea.Height > 0);
            Assert.Equal (HeadlessScreenBounds, workingArea);
            // Stable across calls.
            Assert.Equal (workingArea, SystemInformation.WorkingArea);
        }

        [Fact]
        public void PrimaryMonitorSize_Get_ReturnsExpected ()
        {
            var size = SystemInformation.PrimaryMonitorSize;

            Assert.True (size.Width > 0);
            Assert.True (size.Height > 0);
            Assert.Equal (HeadlessMonitorSize, size);
        }

        [Fact]
        public void MonitorCount_Get_ReturnsExpected ()
        {
            var count = SystemInformation.MonitorCount;

            Assert.True (count > 0);
            Assert.Equal (Screen.AllScreens.Length, count);
            Assert.Equal (1, count);
        }

        [Fact]
        public void VirtualScreen_Get_ReturnsExpected ()
        {
            var screen = SystemInformation.VirtualScreen;

            Assert.NotEqual (0, screen.Width);
            Assert.NotEqual (0, screen.Height);
            Assert.Equal (HeadlessScreenBounds, screen);
        }

        // --- Window / caption / border metrics (fixed constants) ---

        [Fact]
        public void MenuHeight_Get_ReturnsExpected ()
        {
            Assert.Equal (24, SystemInformation.MenuHeight);
        }

        [Fact]
        public void CaptionHeight_Get_ReturnsExpected ()
        {
            Assert.Equal (30, SystemInformation.CaptionHeight);
        }

        [Fact]
        public void BorderSize_Get_ReturnsExpected ()
        {
            Assert.Equal (1, SystemInformation.BorderSize);
        }

        [Fact]
        public void FixedFrameBorderSize_Get_ReturnsExpected ()
        {
            Assert.Equal (3, SystemInformation.FixedFrameBorderSize);
        }

        [Fact]
        public void FrameBorderSize_Get_ReturnsExpected ()
        {
            Assert.Equal (4, SystemInformation.FrameBorderSize);
        }

        [Fact]
        public void SizingBorderWidth_Get_ReturnsExpected ()
        {
            // Upstream asserts width > 0; Modern.Forms pins it to a fixed value.
            Assert.True (SystemInformation.SizingBorderWidth > 0);
            Assert.Equal (4, SystemInformation.SizingBorderWidth);
        }

        [Fact]
        public void MinimumWindowSize_Get_ReturnsExpected ()
        {
            // Modern.Forms exposes this as a single int (not a Size, unlike WinForms).
            Assert.True (SystemInformation.MinimumWindowSize > 0);
            Assert.Equal (112, SystemInformation.MinimumWindowSize);
        }

        [Fact]
        public void MaxWindowTrackSize_Get_ReturnsExpected ()
        {
            // Modern.Forms exposes this as a single int (not a Size, unlike WinForms).
            Assert.Equal (int.MaxValue, SystemInformation.MaxWindowTrackSize);
        }

        // --- Mouse / input metrics ---

        [Fact]
        public void DoubleClickTime_Get_ReturnsExpected ()
        {
            Assert.True (SystemInformation.DoubleClickTime > 0);
            Assert.Equal (500, SystemInformation.DoubleClickTime);
        }

        [Fact]
        public void DoubleClickSize_Get_ReturnsExpected ()
        {
            Assert.Equal (new Size (4, 4), SystemInformation.DoubleClickSize);
        }

        [Fact]
        public void DragSize_Get_ReturnsExpected ()
        {
            Assert.Equal (new Size (4, 4), SystemInformation.DragSize);
        }

        [Fact]
        public void MousePresent_Get_ReturnsExpected ()
        {
            Assert.True (SystemInformation.MousePresent);
        }

        [Fact]
        public void MouseButtons_Get_ReturnsExpected ()
        {
            Assert.True (SystemInformation.MouseButtons >= 0);
            Assert.Equal (3, SystemInformation.MouseButtons);
        }

        [Fact]
        public void MouseButtonsSwapped_Get_ReturnsExpected ()
        {
            Assert.False (SystemInformation.MouseButtonsSwapped);
        }

        [Fact]
        public void MouseWheelScrollLines_Get_ReturnsExpected ()
        {
            Assert.True (SystemInformation.MouseWheelScrollLines > 0);
            Assert.Equal (3, SystemInformation.MouseWheelScrollLines);
        }

        // --- Icon / cursor sizes ---

        [Fact]
        public void SmallIconSize_Get_ReturnsExpected ()
        {
            var size = SystemInformation.SmallIconSize;

            Assert.True (size.Width > 0);
            Assert.True (size.Height > 0);
            Assert.Equal (new Size (16, 16), size);
        }

        [Fact]
        public void IconSize_Get_ReturnsExpected ()
        {
            var size = SystemInformation.IconSize;

            Assert.True (size.Width >= 32);
            Assert.True (size.Height >= 32);
            Assert.Equal (new Size (32, 32), size);
        }

        [Fact]
        public void CursorSize_Get_ReturnsExpected ()
        {
            Assert.Equal (new Size (32, 32), SystemInformation.CursorSize);
        }

        // --- Boolean / environment metrics ---

        [Fact]
        public void Network_Get_ReturnsExpected ()
        {
            Assert.True (SystemInformation.Network);
        }

        [Fact]
        public void SlowMachine_Get_ReturnsExpected ()
        {
            Assert.False (SystemInformation.SlowMachine);
        }

        // --- Enums ---

        [Fact]
        public void ScreenOrientation_Get_ReturnsExpected ()
        {
            Assert.Equal (ScreenOrientation.Angle0, SystemInformation.ScreenOrientation);
        }

        // --- PowerStatus ---

        [Fact]
        public void PowerStatus_Get_ReturnsNotNull ()
        {
            Assert.NotNull (SystemInformation.PowerStatus);
        }

        [Fact]
        public void PowerStatus_BatteryChargeStatus_ReturnsExpected ()
        {
            Assert.Equal (BatteryChargeStatus.High, SystemInformation.PowerStatus.BatteryChargeStatus);
        }

        [Fact]
        public void PowerStatus_BatteryLifePercent_ReturnsExpected ()
        {
            Assert.Equal (1.0f, SystemInformation.PowerStatus.BatteryLifePercent);
        }

        [Fact]
        public void PowerStatus_BatteryLifeRemaining_ReturnsExpected ()
        {
            Assert.Equal (-1, SystemInformation.PowerStatus.BatteryLifeRemaining);
        }

        [Fact]
        public void PowerStatus_BatteryFullLifetime_ReturnsExpected ()
        {
            Assert.Equal (-1, SystemInformation.PowerStatus.BatteryFullLifetime);
        }

        [Fact]
        public void PowerStatus_PowerLineStatus_ReturnsExpected ()
        {
            Assert.Equal (PowerLineStatus.Online, SystemInformation.PowerStatus.PowerLineStatus);
        }

        // --- Enum value definitions (guard against accidental renumbering) ---

        [Theory]
        [InlineData (BatteryChargeStatus.High, 1)]
        [InlineData (BatteryChargeStatus.Low, 2)]
        [InlineData (BatteryChargeStatus.Critical, 4)]
        [InlineData (BatteryChargeStatus.Charging, 8)]
        [InlineData (BatteryChargeStatus.NoSystemBattery, 128)]
        [InlineData (BatteryChargeStatus.Unknown, 255)]
        public void BatteryChargeStatus_HasExpectedValue (BatteryChargeStatus status, int value)
        {
            Assert.Equal (value, (int) status);
        }

        [Theory]
        [InlineData (PowerLineStatus.Offline, 0)]
        [InlineData (PowerLineStatus.Online, 1)]
        [InlineData (PowerLineStatus.Unknown, 255)]
        public void PowerLineStatus_HasExpectedValue (PowerLineStatus status, int value)
        {
            Assert.Equal (value, (int) status);
        }

        [Theory]
        [InlineData (ScreenOrientation.Angle0, 0)]
        [InlineData (ScreenOrientation.Angle90, 1)]
        [InlineData (ScreenOrientation.Angle180, 2)]
        [InlineData (ScreenOrientation.Angle270, 3)]
        public void ScreenOrientation_HasExpectedValue (ScreenOrientation orientation, int value)
        {
            Assert.Equal (value, (int) orientation);
        }
    }
}
