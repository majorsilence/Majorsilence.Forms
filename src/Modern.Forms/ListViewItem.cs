using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a ListViewItem.
    /// </summary>
    public class ListViewItem
    {
        /// <summary>Initializes a new instance of ListViewItem with no text.</summary>
        public ListViewItem () { SubItems = new SubItemCollection (this); }

        /// <summary>Initializes a new instance of ListViewItem with the specified text.</summary>
        public ListViewItem (string text) : this ()
        {
            Text = text;
        }

        /// <summary>Initializes a new instance of ListViewItem with the specified sub-item strings.</summary>
        public ListViewItem (string[] items) : this (items.Length > 0 ? items[0] : string.Empty)
        {
            for (var i = 1; i < items.Length; i++)
                SubItems.Add (new ListViewSubItem { Text = items[i] });
        }

        /// <summary>Initializes a new instance with text and an image index.</summary>
        public ListViewItem (string text, int imageIndex) : this (text)
        {
            ImageIndex = imageIndex;
        }

        /// <summary>Initializes a new instance with sub-item strings and an image index.</summary>
        public ListViewItem (string[] items, int imageIndex) : this (items)
        {
            ImageIndex = imageIndex;
        }

        /// <summary>Initializes a new instance with text and an image key.</summary>
        public ListViewItem (string text, string imageKey) : this (text)
        {
            ImageKey = imageKey;
        }

        /// <summary>Initializes a new instance with sub-item strings and an image key.</summary>
        public ListViewItem (string[] items, string imageKey) : this (items)
        {
            ImageKey = imageKey;
        }

        /// <summary>
        /// Gets the current bounding box of the item.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        private Modern.Drawing.Image? _image;
        private SKBitmap? _imageSK;

        /// <summary>
        /// Gets or sets the image displayed on the item. Accepts <see cref="Modern.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
#pragma warning disable CA1416
        public Modern.Drawing.Image? Image {
            get => _image;
            set {
                _image = value;
                _imageSK?.Dispose ();
                _imageSK = value?.ToSKBitmap ();
            }
        }
#pragma warning restore CA1416

        /// <summary>Gets the SKBitmap representation of the image (used by renderers).</summary>
        internal SKBitmap? ImageSK => _imageSK;

        /// <summary>Sets the image directly from an SKBitmap (internal use).</summary>
        internal void SetImageSK (SKBitmap? bmp) { _image = null; _imageSK = bmp; }

        /// <summary>Gets or sets the index into the ImageList of the image for this item.</summary>
        public int ImageIndex { get; set; } = -1;

        private string _imageKey = string.Empty;

        /// <summary>Gets or sets the key into the ImageList of the image for this item.</summary>
        public string ImageKey {
            get => _imageKey;
            set => _imageKey = value ?? string.Empty;
        }

        /// <summary>
        /// Gets the ListView this item is currently a part of.
        /// </summary>
        public ListView? Parent { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating if the item is currently selected.
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>Gets or sets whether the item is checked.</summary>
        public bool Checked { get; set; }

        /// <summary>Gets or sets the foreground color.</summary>
        public Color ForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the background color.</summary>
        public Color BackColor { get; set; } = Color.Empty;

        /// <summary>Gets the collection of sub-items.</summary>
        public SubItemCollection SubItems { get; }

        /// <summary>Gets or sets the group the item belongs to (stub).</summary>
        public object? Group { get; set; }

        /// <summary>
        /// Sets the bounding box of the item. This is internal API and should not be called.
        /// </summary>
        public void SetBounds (int x, int y, int width, int height)
        {
            Bounds = new Rectangle (x, y, width, height);
        }

        /// <summary>
        /// Gets or sets an object with additional user data about this item.
        /// </summary>
        public object? Tag { get; set; }

        private string _text = string.Empty;

        /// <summary>
        /// Gets or sets the text displayed on the item.
        /// </summary>
        public string Text {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        /// <summary>Gets or sets whether subitems inherit the style of the parent item. Stub in Modern.Forms.</summary>
        public bool UseItemStyleForSubItems { get; set; } = true;

        /// <summary>Gets or sets the tooltip text for the item. Stub in Modern.Forms.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the index of the state image for the item. Stub in Modern.Forms.</summary>
        public int StateImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the number of small image widths by which to indent the item. Stub in Modern.Forms.</summary>
        public int IndentCount { get; set; }

        /// <summary>Gets the zero-based index of the item within its ListView, or -1 if not in a list.</summary>
        public int Index => Parent?.Items.IndexOf (this) ?? -1;

        /// <summary>Gets the ListView that contains this item (same as Parent).</summary>
        public ListView? ListView => Parent;

        /// <summary>Gets or sets the position of this item in virtual coordinates. Stub in Modern.Forms.</summary>
        public System.Drawing.Point Position { get; set; }

        /// <summary>Ensures the item is scrolled into view.</summary>
        public void EnsureVisible () => Parent?.EnsureVisible (Index);

        /// <summary>Begins in-place editing of the item's label. Stub in Modern.Forms.</summary>
        public void BeginEdit () { }

        /// <summary>Removes this item from its parent ListView.</summary>
        public void Remove () => Parent?.Items.Remove (this);

        /// <summary>Creates a copy of this item.</summary>
        public ListViewItem Clone ()
        {
            var clone = new ListViewItem (Text) {
                Tag = Tag,
                Name = Name,
                Checked = Checked,
                ImageIndex = ImageIndex,
                ImageKey = ImageKey,
                ForeColor = ForeColor,
                BackColor = BackColor
            };
            foreach (ListViewSubItem sub in SubItems)
                clone.SubItems.Add (new ListViewSubItem { Text = sub.Text, Tag = sub.Tag });
            return clone;
        }

        private string _name = string.Empty;

        /// <summary>Gets or sets the name/key of the item.</summary>
        public string Name {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        /// <summary>Represents a single sub-item in a ListViewItem.</summary>
        public class ListViewSubItem
        {
            /// <summary>Gets or sets the sub-item text.</summary>
            public string Text { get; set; } = string.Empty;

            /// <summary>Gets or sets an object with additional user data.</summary>
            public object? Tag { get; set; }

#pragma warning disable CA1416
            /// <summary>Gets or sets the foreground color for this sub-item. Empty means use parent item color.</summary>
            public System.Drawing.Color ForeColor { get; set; } = System.Drawing.Color.Empty;

            /// <summary>Gets or sets the background color for this sub-item. Empty means use parent item color.</summary>
            public System.Drawing.Color BackColor { get; set; } = System.Drawing.Color.Empty;

            /// <summary>Gets or sets the font for this sub-item. Null means use parent item font.</summary>
            public Modern.Drawing.Font? Font { get; set; }
#pragma warning restore CA1416

            /// <summary>Initializes a new instance of ListViewSubItem.</summary>
            public ListViewSubItem () { }

            /// <summary>Initializes a new instance with the specified text.</summary>
            public ListViewSubItem (string text) { Text = text; }

#pragma warning disable CA1416
            /// <summary>Initializes a new instance with text and style.</summary>
            public ListViewSubItem (string text, System.Drawing.Color foreColor, System.Drawing.Color backColor, Modern.Drawing.Font font)
            {
                Text = text;
                ForeColor = foreColor;
                BackColor = backColor;
                Font = font;
            }
#pragma warning restore CA1416
        }

        /// <summary>Collection of sub-items for a ListViewItem.</summary>
        public class SubItemCollection : List<ListViewSubItem>
        {
            private readonly ListViewItem _owner;

            internal SubItemCollection (ListViewItem owner) => _owner = owner;

            /// <summary>Adds a sub-item with the specified text.</summary>
            public ListViewSubItem Add (string text)
            {
                var item = new ListViewSubItem { Text = text };
                Add (item);
                return item;
            }

#pragma warning disable CA1416
            /// <summary>Adds a sub-item with text and style.</summary>
            public ListViewSubItem Add (string text, System.Drawing.Color foreColor, System.Drawing.Color backColor, Modern.Drawing.Font font)
            {
                var item = new ListViewSubItem (text, foreColor, backColor, font);
                Add (item);
                return item;
            }
#pragma warning restore CA1416

            /// <summary>Gets the text of a sub-item by index, or empty string if out of range.</summary>
            public string GetText (int index) => index < Count ? this[index].Text : string.Empty;
        }
    }
}
