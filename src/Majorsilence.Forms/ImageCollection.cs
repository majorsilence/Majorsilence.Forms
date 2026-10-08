using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using SkiaSharp;
using DrawingBitmap = Majorsilence.Forms.Drawing.Bitmap;
using Icon = Majorsilence.Forms.Drawing.Icon;
using Image = Majorsilence.Forms.Drawing.Image;

#pragma warning disable CA1416

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents the collection of images held by an <see cref="ImageList"/>.
    /// </summary>
    /// <remarks>
    /// <para>The shape of upstream's <c>ImageList.ImageCollection</c>
    /// (Controls/ImageList/ImageList.ImageCollection.cs): an <see cref="IList"/> whose indexers and
    /// enumerator yield <see cref="Image"/>, with <see cref="Keys"/> as a <see cref="StringCollection"/>,
    /// case-insensitive keys, duplicate keys allowed, and upstream's argument validation. Until SMP-55 it
    /// was an <c>IDictionary&lt;string, SKBitmap&gt;</c>, so <c>pictureBox.Image = imageList.Images[0]</c>
    /// and <c>foreach (Image i in imageList.Images)</c> did not compile.</para>
    /// <para>Every image is held as a Skia bitmap resized to <see cref="ImageList.ImageSize"/>. As
    /// upstream's indexer builds a new <c>Bitmap</c> on each read, so does this one: the caller owns what
    /// it gets back. <see cref="GetBitmap(int)"/> and <see cref="GetBitmap(string)"/> are this library's
    /// extension for Skia callers, returning the stored bitmap without a copy.</para>
    /// </remarks>
    public class ImageCollection : IList
    {
        private sealed class Entry
        {
            internal Entry (string name, SKBitmap bitmap)
            {
                Name = name;
                Bitmap = bitmap;
            }

            internal string Name;
            internal SKBitmap Bitmap;
        }

        private readonly List<Entry> images = [];

        internal SKSize ImageSize { get; private set; }

        // Applied to every bitmap as it is added: the owning ImageList's colour key (W6 mechanisms).
        internal Func<SKBitmap, SKBitmap>? Transform { get; set; }

        internal ImageCollection (SKSize imageSize)
        {
            ImageSize = imageSize;
        }

        /// <summary>Gets the key of every image, in order; an image added without a key has an empty one.</summary>
        public StringCollection Keys {
            get {
                var keys = new StringCollection ();

                foreach (var entry in images)
                    keys.Add (entry.Name ?? string.Empty);

                return keys;
            }
        }

        /// <summary>Gets the number of images in the collection.</summary>
        [Browsable (false)]
        public int Count => images.Count;

        /// <summary>Gets a value indicating whether the collection holds no images.</summary>
        public bool Empty => Count == 0;

        /// <summary>Gets a value indicating whether the collection is read-only. This is always <c>false</c>.</summary>
        public bool IsReadOnly => false;

        object ICollection.SyncRoot => this;

        bool ICollection.IsSynchronized => false;

        bool IList.IsFixedSize => false;

        /// <summary>Gets or sets the image at the specified index.</summary>
        /// <remarks>The getter returns a new <see cref="Image"/> over a copy of the stored pixels, as
        /// upstream's does; dispose it when done. Use <see cref="GetBitmap(int)"/> to read without a copy.
        /// Setting resizes the image to <see cref="ImageList.ImageSize"/> and keeps the entry's key.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an image's index.</exception>
        /// <exception cref="ArgumentNullException">The value is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The value is not a <see cref="DrawingBitmap"/>.</exception>
        [Browsable (false)]
        [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
        public Image this[int index] {
            get => new DrawingBitmap (GetBitmap (index).Copy ());
            set {
                ThrowIfBadIndex (index);
                Guard.ThrowIfNull (value);

                if (value is not DrawingBitmap || value.GetSKBitmap () is not { } source)
                    throw new ArgumentException ("Image must be a Bitmap.", nameof (value));

                var entry = images[index];
                var old = entry.Bitmap;
                entry.Bitmap = Normalize (source, Transform);
                old.Dispose ();
            }
        }

        object? IList.this[int index] {
            get => this[index];
            set {
                if (value is not Image image)
                    throw new ArgumentException ("Value must be an Image.", nameof (value));

                this[index] = image;
            }
        }

        /// <summary>Gets the image with the specified key, or <c>null</c> when no image has it.</summary>
        /// <remarks>Keys compare case-insensitively. As with the integer indexer, the result is a new
        /// <see cref="Image"/> the caller owns.</remarks>
        public Image? this[string key] => GetBitmap (key) is { } bitmap ? new DrawingBitmap (bitmap.Copy ()) : null;

        /// <summary>Gets the stored Skia bitmap at the specified index, without copying it.</summary>
        /// <remarks>A Majorsilence.Forms extension, not part of WinForms. The bitmap belongs to the
        /// collection: do not dispose or modify it, and do not keep it past a change to the image list.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an image's index.</exception>
        public SKBitmap GetBitmap (int index)
        {
            ThrowIfBadIndex (index);
            return images[index].Bitmap;
        }

        /// <summary>Gets the stored Skia bitmap with the specified key, without copying it, or <c>null</c>
        /// when no image has the key.</summary>
        /// <remarks>A Majorsilence.Forms extension, not part of WinForms. The bitmap belongs to the
        /// collection: do not dispose or modify it, and do not keep it past a change to the image list.</remarks>
        public SKBitmap? GetBitmap (string key)
        {
            var index = IndexOfKey (key);
            return index >= 0 ? images[index].Bitmap : null;
        }

        /// <summary>Adds an image with the specified key. The key need not be unique.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="image"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="image"/> holds no pixels.</exception>
        public void Add (string key, Image image)
        {
            Guard.ThrowIfNull (image);
            Store (key, PixelsOf (image), Transform);
        }

        /// <summary>Adds an icon with the specified key. The key need not be unique.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="icon"/> is <c>null</c>.</exception>
        public void Add (string key, Icon icon)
        {
            Guard.ThrowIfNull (icon);
            using var bitmap = icon.ToBitmap ();
            Store (key, PixelsOf (bitmap), Transform);
        }

        /// <summary>Adds an icon to the collection.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        public void Add (Icon value)
        {
            Guard.ThrowIfNull (value);
            using var bitmap = value.ToBitmap ();
            Store (null, PixelsOf (bitmap), Transform);
        }

        /// <summary>Adds an image to the collection.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> holds no pixels.</exception>
        public void Add (Image value)
        {
            Guard.ThrowIfNull (value);
            Store (null, PixelsOf (value), Transform);
        }

        /// <summary>Adds an image, treating <paramref name="transparentColor"/> as transparent in it
        /// instead of the list's <see cref="ImageList.TransparentColor"/>.</summary>
        /// <returns>The index of the added image.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        public int Add (Image value, System.Drawing.Color transparentColor)
        {
            Guard.ThrowIfNull (value);
            var key = transparentColor.ToSKColor ();

            // Upstream's OriginalOptions.CustomTransparentColor: this colour replaces the list's own key.
            Store (null, PixelsOf (value), bitmap => {
                var keyed = ColorKeyedBitmaps.Apply (bitmap, key);
                return ReferenceEquals (keyed, bitmap) ? bitmap : keyed.Copy ();
            });

            return Count - 1;
        }

        /// <summary>Adds a Skia bitmap with the specified key. The key need not be unique.</summary>
        /// <remarks>A Majorsilence.Forms extension, not part of WinForms. A resized copy is stored; the
        /// caller still owns <paramref name="value"/>.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        public void Add (string key, SKBitmap value)
        {
            Guard.ThrowIfNull (value);
            Store (key, value, Transform);
        }

        /// <summary>Adds a Skia bitmap to the collection.</summary>
        /// <remarks>A Majorsilence.Forms extension, not part of WinForms. A resized copy is stored; the
        /// caller still owns <paramref name="value"/>.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        public void Add (SKBitmap value)
        {
            Guard.ThrowIfNull (value);
            Store (null, value, Transform);
        }

        int IList.Add (object? value)
        {
            if (value is not Image image)
                throw new ArgumentException ("Value must be an Image.", nameof (value));

            Add (image);
            return Count - 1;
        }

        /// <summary>Adds each image in the array to the collection.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="images"/> is <c>null</c>.</exception>
        public void AddRange (params Image[] images)
        {
            Guard.ThrowIfNull (images);

            foreach (var image in images)
                Add (image);
        }

        /// <summary>
        /// Adds each frame of a horizontal image strip as a separate image.
        /// </summary>
        /// <remarks>
        /// One wide bitmap holding N frames side by side, sliced into <see cref="ImageList.ImageSize"/>-wide
        /// pieces, with upstream's checks: the width a non-zero multiple of the image width and the height
        /// equal to the image height.
        /// </remarks>
        /// <returns>The index of the first frame added.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The strip's width is not a multiple of the image width, or its
        /// height is not the image height.</exception>
        public int AddStrip (Image value)
        {
            Guard.ThrowIfNull (value);

            var frameWidth = (int)ImageSize.Width;
            var frameHeight = (int)ImageSize.Height;
            var strip = PixelsOf (value);

            if (strip.Width == 0 || frameWidth <= 0 || strip.Width % frameWidth != 0)
                throw new ArgumentException (
                    $"Strip width {strip.Width} is not a multiple of the image width {frameWidth}.", nameof (value));

            if (strip.Height != frameHeight)
                throw new ArgumentException (
                    $"Strip height {strip.Height} is not the image height {frameHeight}.", nameof (value));

            var first = Count;

            for (var x = 0; x < strip.Width; x += frameWidth) {
                using var frame = new SKBitmap (frameWidth, frameHeight);

                using (var canvas = new SKCanvas (frame))
                    canvas.DrawBitmap (strip, new SKRect (x, 0, x + frameWidth, frameHeight),
                                       new SKRect (0, 0, frameWidth, frameHeight));

                Store (null, frame, Transform);
            }

            return first;
        }

        /// <summary>Removes all images from the collection.</summary>
        public void Clear ()
        {
            foreach (var entry in images)
                entry.Bitmap.Dispose ();

            images.Clear ();
        }

        /// <summary>Not supported, as upstream: images are stored as copies, so no instance is in the collection.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public bool Contains (Image image) => throw new NotSupportedException ();

        bool IList.Contains (object? value) => value is Image image && Contains (image);

        /// <summary>Gets a value indicating whether an image has the specified key (case-insensitive).</summary>
        public bool ContainsKey (string key) => IndexOfKey (key) >= 0;

        /// <summary>Not supported, as upstream: images are stored as copies, so no instance is in the collection.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public int IndexOf (Image image) => throw new NotSupportedException ();

        int IList.IndexOf (object? value) => value is Image image ? IndexOf (image) : -1;

        /// <summary>Gets the index of the first image with the specified key (case-insensitive), or -1.</summary>
        public int IndexOfKey (string key)
        {
            if (string.IsNullOrEmpty (key))
                return -1;

            // Upstream compares through WindowsFormsUtils.SafeCompareStrings (ignoreCase: true), an
            // invariant-culture comparison.
            for (var i = 0; i < images.Count; i++)
                if (string.Equals (images[i].Name, key, StringComparison.InvariantCultureIgnoreCase))
                    return i;

            return -1;
        }

        void IList.Insert (int index, object? value) => throw new NotSupportedException ();

        void ICollection.CopyTo (Array array, int index)
        {
            for (var i = 0; i < Count; i++)
                array.SetValue (this[i], index++);
        }

        /// <summary>Returns an enumerator over the images, each a new <see cref="Image"/> the caller owns.</summary>
        public IEnumerator GetEnumerator ()
        {
            var result = new Image[Count];

            for (var i = 0; i < result.Length; i++)
                result[i] = this[i];

            return result.GetEnumerator ();
        }

        /// <summary>Not supported, as upstream: use <see cref="RemoveAt"/> or <see cref="RemoveByKey"/>.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Remove (Image image) => throw new NotSupportedException ();

        void IList.Remove (object? value)
        {
            if (value is Image image)
                Remove (image);
        }

        /// <summary>Removes the image at the specified index.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an image's index.</exception>
        public void RemoveAt (int index)
        {
            ThrowIfBadIndex (index);
            images[index].Bitmap.Dispose ();
            images.RemoveAt (index);
        }

        /// <summary>Removes the first image with the specified key; does nothing when no image has it.</summary>
        public void RemoveByKey (string key)
        {
            var index = IndexOfKey (key);

            if (index >= 0)
                RemoveAt (index);
        }

        /// <summary>Sets the key of the image at the given index.</summary>
        /// <remarks>The image keeps its position and its bitmap and answers to the new key, so
        /// <c>ImageKey</c> on a control finds it.</remarks>
        /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is not an image's index, the
        /// exception upstream throws here.</exception>
        public void SetKeyName (int index, string name)
        {
            // Upstream throws IndexOutOfRangeException here, not ArgumentOutOfRangeException; kept so a
            // catch ported with the code still matches.
#pragma warning disable CA2201
            if (index < 0 || index >= images.Count)
                throw new IndexOutOfRangeException ();
#pragma warning restore CA2201

            images[index].Name = name ?? string.Empty;
        }

        // SMP-54: changing ImageSize once images are present resizes them in place, where upstream
        // recreates its handle with every image re-rendered at the new size (Controls/ImageList/
        // ImageList.cs, ImageSize). Deviation: upstream re-renders from the originals; only the stored
        // copies exist here, so shrinking and growing again loses detail.
        internal void SetImageSize (SKSize imageSize)
        {
            if (imageSize == ImageSize)
                return;

            ImageSize = imageSize;

            var size = imageSize.ToSizeI ();

            foreach (var entry in images) {
                var bitmap = entry.Bitmap;

                if (bitmap.Width == size.Width && bitmap.Height == size.Height)
                    continue;

                entry.Bitmap = bitmap.Resize (size, new SKSamplingOptions (SKCubicResampler.Mitchell));
                bitmap.Dispose ();
            }
        }

        internal void Dispose ()
        {
            foreach (var entry in images)
                entry.Bitmap.Dispose ();
        }

        private void ThrowIfBadIndex (int index)
        {
            if (index < 0 || index >= images.Count)
                throw new ArgumentOutOfRangeException (nameof (index), index, $"Index '{index}' is out of range for a collection of {images.Count} images.");
        }

        // Upstream accepts only a Bitmap or an Icon (SR.ImageListBitmap); an image without pixels -- a
        // disposed one -- has nothing to store.
        private static SKBitmap PixelsOf (Image image)
            => image.GetSKBitmap () ?? throw new ArgumentException ("Image must be a Bitmap.", nameof (image));

        private void Store (string? key, SKBitmap source, Func<SKBitmap, SKBitmap>? transform)
            => images.Add (new Entry (key ?? string.Empty, Normalize (source, transform)));

        // A copy at ImageSize, run through the colour key. The source stays the caller's.
        private SKBitmap Normalize (SKBitmap source, Func<SKBitmap, SKBitmap>? transform)
        {
            var sized = source.GetSize ().ToSKSize () == ImageSize
                ? source.Copy ()
                : source.Resize (ImageSize.ToSizeI (), new SKSamplingOptions (SKCubicResampler.Mitchell));

            if (transform is null)
                return sized;

            var keyed = transform (sized);

            if (!ReferenceEquals (keyed, sized))
                sized.Dispose ();

            return keyed;
        }
    }
}
#pragma warning restore CA1416
