// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ImageListTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ImageListTests, adapted to the
    // Majorsilence.Forms API. Majorsilence.Forms has no Handle/CreateParams/accessibility plumbing and
    // images are SkiaSharp.SKBitmap (not System.Drawing.Image), so those upstream concerns are
    // dropped. These pin the ColorDepth/ImageSize/TransparentColor property semantics and the
    // Images collection add/remove/count/indexer/key behavior.
    public class ImageListTests
    {
        private static SKBitmap CreateBitmap (int width = 16, int height = 16) => new SKBitmap (width, height);

        [Fact]
        public void Ctor_Default ()
        {
            using var list = new ImageList ();

            Assert.Equal (ColorDepth.Depth32Bit, list.ColorDepth);
            Assert.Equal (0, list.Images.Count);
            Assert.Same (list.Images, list.Images);
            Assert.Equal (new Size (16, 16), list.ImageSize);
            Assert.Null (list.ImageStream);
            Assert.Equal (Color.Transparent, list.TransparentColor);
        }

        [Theory]
        [InlineData (ColorDepth.Depth4Bit)]
        [InlineData (ColorDepth.Depth8Bit)]
        [InlineData (ColorDepth.Depth16Bit)]
        [InlineData (ColorDepth.Depth24Bit)]
        [InlineData (ColorDepth.Depth32Bit)]
        public void ColorDepth_Set_GetReturnsExpected (ColorDepth value)
        {
            using var list = new ImageList { ColorDepth = value };
            Assert.Equal (value, list.ColorDepth);

            // Set same.
            list.ColorDepth = value;
            Assert.Equal (value, list.ColorDepth);
        }

        [Fact]
        public void ColorDepth_SetDifferent_GetReturnsExpected ()
        {
            using var list = new ImageList ();

            list.ColorDepth = ColorDepth.Depth24Bit;
            Assert.Equal (ColorDepth.Depth24Bit, list.ColorDepth);

            list.ColorDepth = ColorDepth.Depth16Bit;
            Assert.Equal (ColorDepth.Depth16Bit, list.ColorDepth);
        }

        [Theory]
        [InlineData (16, 16)]
        [InlineData (17, 16)]
        [InlineData (16, 17)]
        [InlineData (24, 25)]
        [InlineData (256, 26)]
        public void ImageSize_Set_GetReturnsExpected (int width, int height)
        {
            var value = new Size (width, height);
            using var list = new ImageList { ImageSize = value };
            Assert.Equal (value, list.ImageSize);

            // Set same.
            list.ImageSize = value;
            Assert.Equal (value, list.ImageSize);
        }

        [Fact]
        public void ImageSize_SetDifferent_GetReturnsExpected ()
        {
            using var list = new ImageList ();
            Assert.Equal (new Size (16, 16), list.ImageSize);

            list.ImageSize = new Size (32, 32);
            Assert.Equal (new Size (32, 32), list.ImageSize);

            list.ImageSize = new Size (11, 11);
            Assert.Equal (new Size (11, 11), list.ImageSize);
        }

        [Fact]
        public void ImageSize_SetAfterImageAdded_ThrowsInvalidOperationException ()
        {
            using var list = new ImageList ();
            using var image = CreateBitmap ();
            list.Images.Add ("key", image);

            Assert.Throws<InvalidOperationException> (() => list.ImageSize = new Size (32, 32));
        }

        [Fact]
        public void ImageSize_SetSameAfterImageAdded_DoesNotThrow ()
        {
            using var list = new ImageList ();
            using var image = CreateBitmap ();
            list.Images.Add ("key", image);

            // Setting the same size is a no-op even after images are added.
            list.ImageSize = new Size (16, 16);
            Assert.Equal (new Size (16, 16), list.ImageSize);
        }

        [Theory]
        [InlineData (255, 0, 0)]
        [InlineData (0, 0, 255)]
        [InlineData (211, 211, 211)] // LightGray
        public void TransparentColor_Set_GetReturnsExpected (int r, int g, int b)
        {
            var value = Color.FromArgb (r, g, b);
            using var list = new ImageList { TransparentColor = value };
            Assert.Equal (value, list.TransparentColor);

            // Set same.
            list.TransparentColor = value;
            Assert.Equal (value, list.TransparentColor);
        }

        [Fact]
        public void TransparentColor_SetDifferent_GetReturnsExpected ()
        {
            using var list = new ImageList ();
            Assert.Equal (Color.Transparent, list.TransparentColor);

            list.TransparentColor = Color.Red;
            Assert.Equal (Color.Red, list.TransparentColor);

            list.TransparentColor = Color.Blue;
            Assert.Equal (Color.Blue, list.TransparentColor);
        }

        [Fact]
        public void Images_AddWithKey_IncrementsCount ()
        {
            using var list = new ImageList ();
            using var image = CreateBitmap ();

            list.Images.Add ("key", image);

            Assert.Equal (1, list.Images.Count);
            Assert.True (list.Images.ContainsKey ("key"));
        }

        [Fact]
        public void Images_AddAutoKey_IncrementsCount ()
        {
            using var list = new ImageList ();
            using var image1 = CreateBitmap ();
            using var image2 = CreateBitmap ();

            list.Images.Add (image1);
            list.Images.Add (image2);

            Assert.Equal (2, list.Images.Count);
        }

        [Fact]
        public void Images_AddDuplicateKey_ThrowsArgumentException ()
        {
            using var list = new ImageList ();
            using var image = CreateBitmap ();
            list.Images.Add ("key", image);

            Assert.Throws<ArgumentException> (() => list.Images.Add ("key", CreateBitmap ()));
        }

        [Fact]
        public void Images_AddResizesToImageSize ()
        {
            using var list = new ImageList { ImageSize = new Size (16, 16) };
            using var image = CreateBitmap (10, 10);

            list.Images.Add ("key", image);

            var stored = list.Images["key"];
            Assert.Equal (16, stored.Width);
            Assert.Equal (16, stored.Height);
        }

        [Fact]
        public void Images_IndexerByKey_ReturnsImage ()
        {
            using var list = new ImageList ();
            using var image = CreateBitmap ();
            list.Images.Add ("key", image);

            Assert.NotNull (list.Images["key"]);
        }

        [Fact]
        public void Images_IndexerByIndex_ReturnsImage ()
        {
            using var list = new ImageList ();
            using var image = CreateBitmap ();
            list.Images.Add ("key", image);

            Assert.NotNull (list.Images[0]);
        }

        [Fact]
        public void Images_Clear_RemovesAll ()
        {
            using var list = new ImageList ();
            list.Images.Add ("a", CreateBitmap ());
            list.Images.Add ("b", CreateBitmap ());
            Assert.Equal (2, list.Images.Count);

            list.Images.Clear ();
            Assert.Equal (0, list.Images.Count);
        }

        [Fact]
        public void Images_Remove_ExistingKey_ReturnsTrue ()
        {
            using var list = new ImageList ();
            list.Images.Add ("key", CreateBitmap ());

            Assert.True (list.Images.Remove ("key"));
            Assert.Equal (0, list.Images.Count);
            Assert.False (list.Images.ContainsKey ("key"));
        }

        [Fact]
        public void Images_Remove_MissingKey_ReturnsFalse ()
        {
            using var list = new ImageList ();

            Assert.False (list.Images.Remove ("missing"));
        }

        [Fact]
        public void Images_RemoveAt_RemovesImage ()
        {
            using var list = new ImageList ();
            list.Images.Add ("a", CreateBitmap ());
            list.Images.Add ("b", CreateBitmap ());

            list.Images.RemoveAt (0);

            Assert.Equal (1, list.Images.Count);
            Assert.False (list.Images.ContainsKey ("a"));
            Assert.True (list.Images.ContainsKey ("b"));
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (5)]
        public void Images_RemoveAt_InvalidIndex_IsNoOp (int index)
        {
            using var list = new ImageList ();
            list.Images.Add ("a", CreateBitmap ());

            list.Images.RemoveAt (index);

            Assert.Equal (1, list.Images.Count);
        }

        [Fact]
        public void Images_ContainsKey_ReturnsExpected ()
        {
            using var list = new ImageList ();
            list.Images.Add ("key", CreateBitmap ());

            Assert.True (list.Images.ContainsKey ("key"));
            Assert.False (list.Images.ContainsKey ("missing"));
        }

        [Fact]
        public void Images_IndexOfKey_ReturnsExpected ()
        {
            using var list = new ImageList ();
            list.Images.Add ("a", CreateBitmap ());
            list.Images.Add ("b", CreateBitmap ());

            Assert.Equal (0, list.Images.IndexOfKey ("a"));
            Assert.Equal (1, list.Images.IndexOfKey ("b"));
            Assert.Equal (-1, list.Images.IndexOfKey ("missing"));
        }

        [Fact]
        public void Images_Keys_ReturnsAllKeys ()
        {
            using var list = new ImageList ();
            list.Images.Add ("a", CreateBitmap ());
            list.Images.Add ("b", CreateBitmap ());

            Assert.Equal (new[] { "a", "b" }, list.Images.Keys);
        }

        [Fact]
        public void Images_TryGetValue_ReturnsExpected ()
        {
            using var list = new ImageList ();
            list.Images.Add ("key", CreateBitmap ());

            Assert.True (list.Images.TryGetValue ("key", out var found));
            Assert.NotNull (found);

            Assert.False (list.Images.TryGetValue ("missing", out var missing));
            Assert.Null (missing);
        }
    }
}
