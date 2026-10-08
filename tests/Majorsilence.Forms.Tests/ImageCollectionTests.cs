using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using SkiaSharp;
using Xunit;
using DrawingBitmap = Majorsilence.Forms.Drawing.Bitmap;

namespace Majorsilence.Forms.Tests
{
    // SMP-55: ImageList.Images has upstream's shape (Controls/ImageList/ImageList.ImageCollection.cs) --
    // an IList yielding Image, a StringCollection of keys, upstream's key rules and exceptions -- so
    // ported code such as `pictureBox.Image = imageList.Images[0]` compiles and behaves.
    public class ImageCollectionTests
    {
        private static SKBitmap Solid (SKColor color, int size = 16)
        {
            var bitmap = new SKBitmap (size, size);
            bitmap.Erase (color);
            return bitmap;
        }

        private static ImageList RedThenBlue ()
        {
            var list = new ImageList ();
            using var red = Solid (SKColors.Red);
            using var blue = Solid (SKColors.Blue);
            list.Images.Add ("red", red);
            list.Images.Add ("blue", blue);
            return list;
        }

        [Fact]
        public void Images_is_an_IList ()
        {
            using var list = new ImageList ();

            Assert.IsAssignableFrom<IList> (list.Images);
        }

        [Fact]
        public void Indexer_result_assigns_to_a_PictureBox_and_shows_the_image ()
        {
            using var list = RedThenBlue ();
            using var picture = new PictureBox { Width = 16, Height = 16 };

            // The line SMP-55 is about. A guard rather than a regression test: it already compiled and
            // showed the image while the indexer returned SKBitmap, through Image's implicit conversion
            // from SKBitmap -- which handed the PictureBox the list's own bitmap to own. The copy that
            // fixes that is pinned by Indexer_returns_a_copy_the_caller_owns.
            picture.Image = list.Images[1];

            using var rendered = PaintSurface.Render (picture);
            Assert.Equal (SKColors.Blue, rendered.GetPixel (rendered.Width / 2, rendered.Height / 2));
        }

        [Fact]
        public void Foreach_yields_each_Image_in_order ()
        {
            using var list = RedThenBlue ();
            var colors = new System.Collections.Generic.List<System.Drawing.Color> ();

            foreach (Image image in list.Images)
                colors.Add (((DrawingBitmap)image).GetPixel (4, 4));

            Assert.Equal (new[] { System.Drawing.Color.FromArgb (255, 255, 0, 0), System.Drawing.Color.FromArgb (255, 0, 0, 255) }, colors);
        }

        [Fact]
        public void Indexer_returns_a_copy_the_caller_owns ()
        {
            // Upstream's indexer builds a new Bitmap on every read (ImageList.GetBitmap), so a caller may
            // draw on or dispose what it gets back. Writing to it must not reach the stored image.
            using var list = RedThenBlue ();

            // Not disposed here: were the indexer to hand out the stored bitmap, disposing it would
            // crash the test host instead of failing this test.
            ((Bitmap)list.Images[0]).SetPixel (4, 4, System.Drawing.Color.Lime);
            ((Bitmap)list.Images["red"]!).SetPixel (5, 5, System.Drawing.Color.Lime);

            Assert.Equal (SKColors.Red, list.Images.GetBitmap (0).GetPixel (4, 4));
            Assert.Equal (SKColors.Red, list.Images.GetBitmap (0).GetPixel (5, 5));
        }

        [Fact]
        public void Indexer_set_replaces_the_image_keeps_the_key_and_resizes ()
        {
            using var list = RedThenBlue ();
            using var green = new DrawingBitmap (8, 8);
            green.SetPixel (0, 0, System.Drawing.Color.Lime);

            list.Images[0] = green;

            Assert.Equal (0, list.Images.IndexOfKey ("red"));
            Assert.Equal (16, list.Images.GetBitmap (0).Width);
            Assert.Equal (2, list.Images.Count);
        }

        [Fact]
        public void Keys_is_a_StringCollection_with_empty_keys_for_unnamed_images ()
        {
            using var list = RedThenBlue ();
            using var unnamed = Solid (SKColors.Green);
            list.Images.Add (unnamed);

            StringCollection keys = list.Images.Keys;

            Assert.Equal (new[] { "red", "blue", "" }, keys.Cast<string> ());
        }

        [Fact]
        public void Keys_match_case_insensitively ()
        {
            using var list = RedThenBlue ();

            Assert.Equal (1, list.Images.IndexOfKey ("BLUE"));
            Assert.True (list.Images.ContainsKey ("Red"));
            Assert.NotNull (list.Images["bLuE"]);
            Assert.Equal (SKColors.Blue, list.Images.GetBitmap ("BLUE")!.GetPixel (4, 4));
        }

        [Fact]
        public void String_indexer_returns_null_for_a_missing_null_or_empty_key ()
        {
            using var list = RedThenBlue ();

            Assert.Null (list.Images["missing"]);
            Assert.Null (list.Images[(string)null!]);
            Assert.Null (list.Images[""]);
            Assert.Equal (-1, list.Images.IndexOfKey (""));
        }

        [Fact]
        public void SetKeyName_and_RemoveByKey_work_through_the_new_key ()
        {
            using var list = RedThenBlue ();

            list.Images.SetKeyName (0, "first");
            list.Images.RemoveByKey ("FIRST");

            Assert.Equal (1, list.Images.Count);
            Assert.Equal (new[] { "blue" }, list.Images.Keys.Cast<string> ());
        }

        [Fact]
        public void Empty_and_IsReadOnly ()
        {
            using var list = new ImageList ();
            Assert.True (list.Images.Empty);
            Assert.False (list.Images.IsReadOnly);

            using var red = Solid (SKColors.Red);
            list.Images.Add (red);
            Assert.False (list.Images.Empty);
        }

        [Fact]
        public void AddRange_adds_every_image ()
        {
            using var list = new ImageList ();
            using var a = new DrawingBitmap (16, 16);
            using var b = new DrawingBitmap (16, 16);

            list.Images.AddRange (new Image[] { a, b });

            Assert.Equal (2, list.Images.Count);
        }

        [Fact]
        public void Add_with_a_transparent_color_keys_that_color ()
        {
            using var list = new ImageList ();
            using var image = new DrawingBitmap (16, 16);

            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    image.SetPixel (x, y, x < 8 ? System.Drawing.Color.Red : System.Drawing.Color.Magenta);

            var index = list.Images.Add (image, System.Drawing.Color.Magenta);

            Assert.Equal (0, index);
            Assert.Equal (0, list.Images.GetBitmap (0).GetPixel (12, 4).Alpha);
            Assert.Equal (255, list.Images.GetBitmap (0).GetPixel (3, 4).Alpha);
        }

        [Fact]
        public void IList_members_take_and_give_Image ()
        {
            using var list = RedThenBlue ();
            IList ilist = list.Images;
            using var image = new DrawingBitmap (16, 16);

            Assert.IsAssignableFrom<Image> (ilist[0]);
            Assert.Equal (2, ilist.Add (image));
            Assert.Throws<ArgumentException> (() => ilist.Add ("not an image"));
            Assert.Throws<ArgumentException> (() => ilist[0] = "not an image");
            Assert.False (ilist.Contains ("not an image"));
            Assert.Equal (-1, ilist.IndexOf ("not an image"));
            Assert.Throws<NotSupportedException> (() => ilist.Insert (0, image));

            var copy = new object[3];
            ilist.CopyTo (copy, 0);
            Assert.All (copy, item => Assert.IsAssignableFrom<Image> (item));
        }

        // ── Upstream's argument validation (ImageList.ImageCollection.cs) ─────────────────────────────

        [Fact]
        public void Null_arguments_throw_ArgumentNullException ()
        {
            using var list = new ImageList ();

            Assert.Throws<ArgumentNullException> (() => list.Images.Add ((Image)null!));
            Assert.Throws<ArgumentNullException> (() => list.Images.Add ((Icon)null!));
            Assert.Throws<ArgumentNullException> (() => list.Images.Add ("key", (Image)null!));
            Assert.Throws<ArgumentNullException> (() => list.Images.Add ("key", (Icon)null!));
            Assert.Throws<ArgumentNullException> (() => list.Images.AddRange (null!));
            Assert.Throws<ArgumentNullException> (() => list.Images.AddStrip (null!));
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (2)]
        public void Index_outside_the_collection_throws (int index)
        {
            using var list = RedThenBlue ();
            using var image = new DrawingBitmap (16, 16);

            Assert.Throws<ArgumentOutOfRangeException> (() => list.Images[index]);
            Assert.Throws<ArgumentOutOfRangeException> (() => list.Images[index] = image);
            Assert.Throws<ArgumentOutOfRangeException> (() => list.Images.GetBitmap (index));
            Assert.Throws<ArgumentOutOfRangeException> (() => list.Images.RemoveAt (index));
            Assert.Throws<IndexOutOfRangeException> (() => list.Images.SetKeyName (index, "x"));
        }

        [Fact]
        public void Setting_null_throws_ArgumentNullException ()
        {
            using var list = RedThenBlue ();

            Assert.Throws<ArgumentNullException> (() => list.Images[0] = null!);
        }

        [Fact]
        public void Image_identity_lookups_are_not_supported_as_upstream ()
        {
            using var list = RedThenBlue ();
            using var image = new DrawingBitmap (16, 16);

            Assert.Throws<NotSupportedException> (() => list.Images.Contains (image));
            Assert.Throws<NotSupportedException> (() => list.Images.IndexOf (image));
            Assert.Throws<NotSupportedException> (() => list.Images.Remove (image));
        }

        [Fact]
        public void AddStrip_rejects_a_strip_whose_height_is_not_the_image_height ()
        {
            using var list = new ImageList ();
            using var strip = new DrawingBitmap (64, 20);

            Assert.Throws<ArgumentException> (() => list.Images.AddStrip (strip));
        }
    }
}
