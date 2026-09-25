using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ListViewItem.
    /// </summary>
    public partial class ListViewItem
    {
        /// <summary>Initializes a new instance of ListViewItem with no text.</summary>
        public ListViewItem ()
        {
            SubItems = new SubItemCollection (this);

            // WinForms puts the item's own text at SubItems[0], so column i is always SubItems[i].
            // This used to hold Text in a separate field with SubItems starting at column 1, which
            // meant migrated code reading item.SubItems[1].Text got the third column's value.
            SubItems.Add (new ListViewSubItem ());
        }

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

        /// <summary>Initializes an item with sub-items, an image index, colours and a font.</summary>
        public ListViewItem (string[] items, int imageIndex, Color foreColor, Color backColor,
                             Majorsilence.Forms.Drawing.Font? font) : this (items, imageIndex)
        {
            ForeColor = foreColor;
            BackColor = backColor;
            Font = font;
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
        /// Gets the current bounding box of the item, in logical units -- the space
        /// <see cref="Control.Bounds"/> and <see cref="MouseEventArgs"/> are in.
        /// </summary>
        /// <remarks>
        /// LAY-38: this used to report <see cref="DeviceBounds"/> directly. The layout lays items out
        /// against <c>ScaledRowHeight</c>, so those are device pixels, and every caller inside this
        /// assembly knows it -- an application does not, and
        /// <c>listView.Items[i].Bounds.Contains (e.Location)</c> was wrong by the display scale. The
        /// public member answers in the same space as the point an application has to hand.
        /// </remarks>
        public Rectangle Bounds
            => Parent is { } parent ? parent.DeviceToLogicalUnits (DeviceBounds) : DeviceBounds;

        /// <summary>
        /// The laid-out rectangle in device pixels -- the space the renderer, the layout and the
        /// hit-test all work in.
        /// </summary>
        internal Rectangle DeviceBounds { get; private set; }

        private Majorsilence.Forms.Drawing.Image? _image;
        private SKBitmap? _imageSK;

        /// <summary>
        /// Gets or sets the image displayed on the item. Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Image? Image {
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
        /// <remarks>
        /// Announces through the parent as of W5.6 (LST-17). It was an auto-property, so
        /// <c>listView.Items[i].Selected = true</c> -- the standard way to select programmatically --
        /// flipped a field and updated nothing: no <c>SelectedIndexChanged</c>, no
        /// <c>ItemSelectionChanged</c>, no repaint, and with <c>MultiSelect = false</c> every item so
        /// assigned stayed selected at once.
        /// </remarks>
        public bool Selected {
            get => selected;
            set {
                if (selected == value)
                    return;

                selected = value;
                Parent?.OnItemSelectedChanged (this, value);
            }
        }

        private bool selected;

        // Assigns without announcing: used by the parent while it deselects the others for a
        // single-select change it is already reporting.
        internal void SetSelectedInternal (bool value) => selected = value;

        /// <summary>Gets or sets whether the item is checked.</summary>
        /// <remarks>Routed through the parent's cancellable <c>ItemCheck</c> and then
        /// <c>ItemChecked</c>, as upstream (LST-18) -- a check-box list whose Apply button is enabled
        /// from <c>ItemChecked</c> was previously dead.</remarks>
        public bool Checked {
            get => checked_state;
            set {
                if (checked_state == value)
                    return;

                // The handler may rewrite the new value, which is what makes ItemCheck a veto.
                var accepted = Parent?.RaiseItemCheck (this, value) ?? value;

                if (checked_state == accepted)
                    return;

                checked_state = accepted;
                Parent?.RaiseItemChecked (this);
            }
        }

        private bool checked_state;

        // The state a virtual-mode slot carries over when its placeholder is replaced (ListView.ResolveVirtualItem).
        internal void SetCheckedInternal (bool value) => checked_state = value;

        /// <summary>True for the empty stand-in a virtual-mode list holds at an index it has not yet
        /// asked <see cref="ListView.RetrieveVirtualItem"/> for (W6 mechanisms).</summary>
        internal bool IsVirtualPlaceholder { get; set; }

        /// <summary>Gets or sets the foreground color.</summary>
        public Color ForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the background color.</summary>
        public Color BackColor { get; set; } = Color.Empty;

        /// <summary>Gets the collection of sub-items.</summary>
        public SubItemCollection SubItems { get; }

        /// <summary>Gets or sets the group the item belongs to (stub).</summary>
        public ListViewGroup? Group {
            get => group;
            set {
                if (ReferenceEquals (group, value))
                    return;

                // Both sides are kept in step: upstream treats group.Items and item.Group as one
                // relationship, and code that walks either has to see the same membership.
                group?.Items.Remove (this);
                group = value;
                group?.Items.Add (this);

                Parent?.RefreshGroups ();
            }
        }

        private ListViewGroup? group;

        /// <summary>
        /// Sets the bounding box of the item. This is internal API and should not be called.
        /// </summary>
        public void SetBounds (int x, int y, int width, int height)
        {
            // The layout calls this with device-pixel values (see LAY-38); Bounds converts on the way
            // back out.
            DeviceBounds = new Rectangle (x, y, width, height);
        }

        /// <summary>
        /// Gets or sets an object with additional user data about this item.
        /// </summary>
        public object? Tag { get; set; }


        /// <summary>
        /// Gets or sets the text displayed on the item.
        /// </summary>
        public string Text {
            get => SubItems[0].Text;
            set => SubItems[0].Text = value ?? string.Empty;
        }

        /// <summary>Gets or sets whether subitems inherit the style of the parent item. Stub in Majorsilence.Forms.</summary>
        public bool UseItemStyleForSubItems { get; set; } = true;

        /// <summary>Gets or sets the tooltip text for the item. Stub in Majorsilence.Forms.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the index of the state image for the item. Stub in Majorsilence.Forms.</summary>
        public int StateImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the number of small image widths by which to indent the item.</summary>
        /// <remarks>Read by the renderer as of W6 mechanisms: the item's first Details cell starts that
        /// many small-image widths in from the column's edge.</remarks>
        public int IndentCount {
            get => indent_count;
            set {
                if (indent_count == value)
                    return;

                indent_count = Math.Max (0, value);
                ListView?.Invalidate ();
            }
        }

        private int indent_count;

        /// <summary>Gets the zero-based index of the item within its ListView, or -1 if not in a list.</summary>
        public int Index => Parent?.Items.IndexOf (this) ?? -1;

        /// <summary>Gets the ListView that contains this item (same as Parent).</summary>
        public ListView? ListView => Parent;

        /// <summary>Gets or sets the item's position in the list's client area, in logical units.</summary>
        /// <remarks>Real as of W6 mechanisms: reads the laid-out location, and a value set on an item in
        /// a list whose <see cref="ListView.AutoArrange"/> is off places its tile at exactly that client
        /// location on the next layout -- upstream's free-placement behaviour for the icon views. A row
        /// view arranges its rows regardless, as upstream does.</remarks>
        public System.Drawing.Point Position {
            get => Parent is not null && !Parent.AutoArrange && placed_position is { } placed ? placed
                : Parent is null && placed_position is { } detached ? detached
                : Bounds.Location;
            set {
                placed_position = value;
                Parent?.Invalidate ();
            }
        }

        /// <summary>Where the application put the item, when it did; null means "arranged by the list".</summary>
        internal System.Drawing.Point? PlacedPosition => placed_position;

        private System.Drawing.Point? placed_position;

        /// <summary>Ensures the item is scrolled into view.</summary>
        public void EnsureVisible () => Parent?.EnsureVisible (Index);

        /// <summary>Begins in-place editing of the item's label.</summary>
        /// <remarks>Real as of W6 mechanisms; see <see cref="ListView.LabelEdit"/>. Throws when the item
        /// is not in a list or the list does not allow label editing, as upstream does.</remarks>
        public void BeginEdit ()
        {
            if (Parent is not { } list)
                throw new InvalidOperationException ("The item must belong to a ListView before its label can be edited.");

            list.BeginLabelEdit (this);
        }

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
            // Skips index 0: `new ListViewItem (Text)` has already produced it.
            for (var i = 1; i < SubItems.Count; i++)
                clone.SubItems.Add (new ListViewSubItem { Text = SubItems[i].Text, Tag = SubItems[i].Tag });
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

            /// <summary>
            /// Gets the sub-item's bounding rectangle in logical units, from the list's last Details
            /// layout; <see cref="Rectangle.Empty"/> before the first layout or in the other views.
            /// </summary>
            public Rectangle Bounds
                => Owner?.Parent is { } parent ? parent.DeviceToLogicalUnits (DeviceBounds) : DeviceBounds;

            /// <summary>The laid-out sub-item rectangle in device pixels (LAY-38).</summary>
            internal Rectangle DeviceBounds { get; set; }

            /// <summary>The item this sub-item belongs to, so it can find the display scale.</summary>
            internal ListViewItem? Owner { get; set; }

#pragma warning disable CA1416
            /// <summary>Gets or sets the foreground color for this sub-item. Empty means use parent item color.</summary>
            public System.Drawing.Color ForeColor { get; set; } = System.Drawing.Color.Empty;

            /// <summary>Gets or sets the background color for this sub-item. Empty means use parent item color.</summary>
            public System.Drawing.Color BackColor { get; set; } = System.Drawing.Color.Empty;

            /// <summary>Gets or sets the font for this sub-item. Null means use parent item font.</summary>
            public Majorsilence.Forms.Drawing.Font? Font { get; set; }
#pragma warning restore CA1416

            /// <summary>Initializes a new instance of ListViewSubItem.</summary>
            public ListViewSubItem () { }

            /// <summary>Initializes a new instance with the specified text.</summary>
            public ListViewSubItem (string text) { Text = text; }

#pragma warning disable CA1416
            /// <summary>Initializes a new instance with text and style.</summary>
            public ListViewSubItem (string text, System.Drawing.Color foreColor, System.Drawing.Color backColor, Majorsilence.Forms.Drawing.Font font)
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
            public ListViewSubItem Add (string text, System.Drawing.Color foreColor, System.Drawing.Color backColor, Majorsilence.Forms.Drawing.Font font)
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
