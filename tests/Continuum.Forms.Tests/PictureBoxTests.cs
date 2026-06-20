// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/PictureBoxTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using System.Drawing;
using Continuum.Drawing;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms PictureBoxTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility/ImeMode/Site plumbing, and no
    // disk/network image loading). They pin the default ctor values, Image/ImageLocation
    // get/set semantics, SizeMode get/set + the SizeModeChanged event + invalid-enum
    // validation, and BorderStyle round-tripping.
    public class PictureBoxTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new PictureBox ();

            Assert.Null (control.Image);
            Assert.Null (control.ImageLocation);
            Assert.False (control.IsErrored);
            Assert.Equal (PictureBoxSizeMode.Normal, control.SizeMode);
            Assert.Equal (BorderStyle.None, control.BorderStyle);
            Assert.Equal (PictureBoxBorderStyle.None, control.PictureBoxBorderStyle);
            Assert.Empty (control.Text);
            Assert.Equal (new Size (100, 50), control.Size);
        }

        [Fact]
        public void Image_Set_GetReturnsExpected ()
        {
            using var image = new Bitmap (10, 10);
            using var control = new PictureBox { Image = image };

            Assert.Same (image, control.Image);

            // Set same.
            control.Image = image;
            Assert.Same (image, control.Image);
        }

        [Fact]
        public void Image_SetWithNonNullOldValue_GetReturnsExpected ()
        {
            using var oldImage = new Bitmap (10, 10);
            using var image = new Bitmap (20, 20);
            using var control = new PictureBox { Image = oldImage };

            control.Image = image;
            Assert.Same (image, control.Image);

            // Set same.
            control.Image = image;
            Assert.Same (image, control.Image);
        }

        [Fact]
        public void Image_SetNull_GetReturnsNull ()
        {
            using var image = new Bitmap (10, 10);
            using var control = new PictureBox { Image = image };
            Assert.Same (image, control.Image);

            control.Image = null;
            Assert.Null (control.Image);
        }

        [Fact]
        public void Image_Set_ResetsIsErrored ()
        {
            using var image = new Bitmap (10, 10);
            using var control = new PictureBox { Image = image };

            Assert.False (control.IsErrored);
        }

        [Fact]
        public void ImageLocation_SetInvalidPath_SetsLocationAndLeavesImageNull ()
        {
            using var control = new PictureBox ();

            // A non-existent local path cannot be decoded; the load fails gracefully,
            // the location is still recorded and Image (the Continuum.Drawing.Image
            // property) remains null because only the SKBitmap path is touched.
            control.ImageLocation = "no-such-file.png";

            Assert.Equal ("no-such-file.png", control.ImageLocation);
            Assert.Null (control.Image);
        }

        [Fact]
        public void ImageLocation_SetNull_ClearsLocation ()
        {
            using var control = new PictureBox ();
            control.ImageLocation = "no-such-file.png";
            Assert.Equal ("no-such-file.png", control.ImageLocation);

            control.ImageLocation = null;
            Assert.Null (control.ImageLocation);
            Assert.Null (control.Image);
        }

        [Theory]
        [InlineData (PictureBoxSizeMode.Normal)]
        [InlineData (PictureBoxSizeMode.StretchImage)]
        [InlineData (PictureBoxSizeMode.AutoSize)]
        [InlineData (PictureBoxSizeMode.CenterImage)]
        [InlineData (PictureBoxSizeMode.Zoom)]
        public void SizeMode_Set_GetReturnsExpected (PictureBoxSizeMode value)
        {
            using var control = new PictureBox { SizeMode = value };

            Assert.Equal (value, control.SizeMode);

            // Set same.
            control.SizeMode = value;
            Assert.Equal (value, control.SizeMode);
        }

        [Fact]
        public void SizeMode_SetWithHandler_CallsSizeModeChanged ()
        {
            using var control = new PictureBox { SizeMode = PictureBoxSizeMode.Normal };
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.SizeModeChanged += handler;

            // Set different.
            control.SizeMode = PictureBoxSizeMode.StretchImage;
            Assert.Equal (PictureBoxSizeMode.StretchImage, control.SizeMode);
            Assert.Equal (1, callCount);

            // Set same.
            control.SizeMode = PictureBoxSizeMode.StretchImage;
            Assert.Equal (PictureBoxSizeMode.StretchImage, control.SizeMode);
            Assert.Equal (1, callCount);

            // Set different.
            control.SizeMode = PictureBoxSizeMode.Normal;
            Assert.Equal (PictureBoxSizeMode.Normal, control.SizeMode);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.SizeModeChanged -= handler;
            control.SizeMode = PictureBoxSizeMode.StretchImage;
            Assert.Equal (PictureBoxSizeMode.StretchImage, control.SizeMode);
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData ((PictureBoxSizeMode)(-1))]
        [InlineData ((PictureBoxSizeMode)5)]
        public void SizeMode_SetInvalid_ThrowsInvalidEnumArgumentException (PictureBoxSizeMode value)
        {
            using var control = new PictureBox ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.SizeMode = value);
        }

        [Theory]
        [InlineData (BorderStyle.None)]
        [InlineData (BorderStyle.FixedSingle)]
        [InlineData (BorderStyle.Fixed3D)]
        public void BorderStyle_Set_GetReturnsExpected (BorderStyle value)
        {
            using var control = new PictureBox { BorderStyle = value };

            Assert.Equal (value, control.BorderStyle);
            Assert.Equal ((PictureBoxBorderStyle)(int)value, control.PictureBoxBorderStyle);

            // Set same.
            control.BorderStyle = value;
            Assert.Equal (value, control.BorderStyle);
        }
    }
}
