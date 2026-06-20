// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (DpiHelperTests.cs under
// src/test/unit/System.Windows.Forms/), rewritten for the Continuum.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Continuum.Forms.Tests
{
    public class DpiHelperTests
    {
        [Fact]
        public void IsScalingRequired_DefaultDpi_IsFalse ()
        {
            // Continuum.Forms reports the device at the logical 96 DPI baseline.
            Assert.False (DpiHelper.IsScalingRequired);
        }

        [Theory]
        [InlineData (10, 96, 10)]    // 1.0x
        [InlineData (10, 192, 20)]   // 2.0x
        [InlineData (10, 144, 15)]   // 1.5x
        [InlineData (100, 144, 150)]
        [InlineData (0, 192, 0)]
        [InlineData (10, 0, 10)]     // devicePixels == 0 → default-DPI (1.0x) path
        public void LogicalToDeviceUnits_Int_ScalesByDpi (int value, int devicePixels, int expected)
        {
            Assert.Equal (expected, DpiHelper.LogicalToDeviceUnits (value, devicePixels));
        }

        [Fact]
        public void LogicalToDeviceUnits_Size_ScalesBothDimensions ()
        {
            var result = DpiHelper.LogicalToDeviceUnits (new Size (10, 20), 192);
            Assert.Equal (new Size (20, 40), result);
        }

        [Fact]
        public void LogicalToDeviceUnits_Padding_ScalesAllSides ()
        {
            var result = DpiHelper.LogicalToDeviceUnits (new Padding (1, 2, 3, 4), 192);
            Assert.Equal (new Padding (2, 4, 6, 8), result);
        }

        [Fact]
        public void LogicalToDeviceUnits_Size_AtBaseline_IsUnchanged ()
        {
            var result = DpiHelper.LogicalToDeviceUnits (new Size (7, 13), 96);
            Assert.Equal (new Size (7, 13), result);
        }
    }
}
