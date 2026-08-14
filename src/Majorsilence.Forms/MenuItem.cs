using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a MenuItem menu item.
    /// </summary>
    public partial class MenuItem : ILayoutable
    {
        private MenuItemCollection? items;
        private MenuDropDown? dropdown;
        private bool enabled = true;
        private bool selected;

        /// <summary>
        /// Initializes a new instance of the MenuItem class.
        /// </summary>
        public MenuItem ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the MenuItem class.
        /// </summary>
        public MenuItem (string text, SKBitmap? image = null, EventHandler? onClick = null)
        {
            Text = text;
            SetImageSK (image);
            Click += onClick;
        }

        /// <summary>
        /// Initializes a new instance of the MenuItem class (WinForms compatibility overload).
        /// </summary>
#pragma warning disable CA1416
        public MenuItem (string text, Majorsilence.Forms.Drawing.Image? image, EventHandler? onClick = null)
        {
            Text = text;
            Image = image;
            Click += onClick;
        }
#pragma warning restore CA1416

        /// <summary>
        /// Gets the bounding box of this menu item.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        /// Raised when the menu item is clicked. Carries <see cref="EventArgs"/>, as WinForms'
        /// <c>ToolStripItem.Click</c> does — designer code wires this up with a plain
        /// <c>new EventHandler(...)</c>, which a typed delegate would reject.
        /// </summary>
        public event EventHandler? Click;

        /// <summary>
        /// Gets or sets a value indicating whether the menu item is enabled.
        /// </summary>
        public bool Enabled {
            get => enabled && (OwnerControl?.Enabled ?? true);
            set {
                if (enabled != value) {
                    enabled = value;
                    OwnerControl?.Invalidate ();
                }
            }
        }

        /// <summary>
        /// Returns a preferred size the menu item would like to be.
        /// </summary>
        public virtual Size GetPreferredSize (Size proposedSize)
        {
            var owner = OwnerControl;

            if (owner is null)
                return proposedSize;

            var renderer = RenderManager.GetRenderer<Renderer> (owner);

            // Ordered most-derived first: Menu and MenuDropDown both derive from ToolStrip (and so from
            // ToolBar), so the ToolBar arm has to come last or it would swallow them.
            // Every renderer below measures text at the DEVICE font size, because that is what produces
            // correct glyph metrics, and returns a size in those units. Item bounds are logical, so the
            // result is converted back here rather than in each renderer -- one place, and it cannot be
            // forgotten by the next renderer added. Identity at scaling 1, which is why device-sized
            // items looked right until a scaled display made them overflow the bar they sit in.
            if (owner is Menu menu && renderer is MenuRenderer menu_renderer)
                return owner.DeviceToLogicalUnits (menu_renderer.GetPreferredItemSize (menu, this, proposedSize));

            if (owner is MenuDropDown mdd && renderer is MenuDropDownRenderer mdd_renderer)
                return owner.DeviceToLogicalUnits (mdd_renderer.GetPreferredItemSize (mdd, this, proposedSize));

            if (owner is ToolBar tb && renderer is ToolBarRenderer tb_renderer)
                return owner.DeviceToLogicalUnits (tb_renderer.GetPreferredItemSize (tb, this, proposedSize));

            if (owner is Ribbon rb && renderer is RibbonRenderer rb_renderer)
                return owner.DeviceToLogicalUnits (rb_renderer.GetPreferredItemSize (rb, this, proposedSize));

            return proposedSize;
        }

        // Traverses MenuItems and MenuDropDowns to get the top menu
        internal MenuBase? GetTopMenu ()
        {
            var root = this;

            while (root.Parent != null)
                root = root.Parent;

            return (root.OwnerControl as MenuBase);
        }

        /// <summary>
        /// Gets a value indicating if this menu item has any child items.
        /// </summary>
        public bool HasItems => items?.Any () == true;

        /// <summary>
        /// Closes the menu item's drop down.
        /// </summary>
        public void HideDropDown ()
        {
            selected = false;
            dropdown?.Hide ();
            IsDropDownOpened = false;

            // Recursively close any child dropdowns
            foreach (var child in Items)
                child.HideDropDown ();
        }

        /// <summary>
        /// Gets a value indicating the mouse cursor is currently hovering over this menu item.
        /// </summary>
        public bool Hovered { get; internal set; }

        private Majorsilence.Forms.Drawing.Image? _image;
        private SKBitmap? _imageSK;

        /// <summary>
        /// Gets or sets an image to be displayed on the menu item. Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
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

        /// <summary>
        /// Gets the SKBitmap representation of the image (used by renderers, for both measuring and
        /// drawing). Virtual so <see cref="ToolStripItem"/> can fall back to the image its
        /// <see cref="ToolStripItem.ImageIndex"/> names in the owning strip's ImageList.
        /// </summary>
        internal virtual SKBitmap? ImageSK => _imageSK;

        /// <summary>Sets the image directly from an SKBitmap (internal use).</summary>
        internal void SetImageSK (SKBitmap? bmp) { _image = null; _imageSK = bmp; }

        /// <summary>
        /// Gets a value indicating this menu item's drop down is currently open.
        /// </summary>
        public bool IsDropDownOpened { get; private set; }

        /// <summary>
        /// Gets the collection of menu items contained by this menu item.
        /// </summary>
        public MenuItemCollection Items => items ??= new MenuItemCollection (this);

        /// <summary>
        /// Gets or sets the margin of this menu item.
        /// </summary>
        public Padding Margin { get; set; } = Padding.Empty;

        /// <summary>
        /// Raises the Click event.
        /// </summary>
        protected internal virtual void OnClick (MouseEventArgs e)
        {
            Click?.Invoke (this, e);
        }

        // The Control that owns this menu item. Internal rather than private because
        // ToolStripItem.Owner reports it -- an item reached through a root item's chain has no
        // ParentControl of its own.
        internal Control? OwnerControl {
            get {
                if (ParentControl != null)
                    return ParentControl;

                if (this is MenuRootItem root)
                    return root.Control;

                return Parent?.OwnerControl;
            }
        }

        /// <summary>
        /// Gets or sets the amount of padding to apply to the menu item.
        /// </summary>
        public Padding Padding { get; set; } = new Padding (14, 3, 14, 3);

        /// <summary>
        /// The parent menu item this item belongs to, if any.
        /// </summary>
        public MenuItem? Parent { get; internal set; }

        // The control this MenuItem is parented to, for example a MenuDropDown or a Menu
        internal Control? ParentControl { get; set; }

        /// <summary>
        /// Gets a value indicating if this menu item is currently selected.
        /// </summary>
        public bool Selected {
            get => selected;
            internal set {
                if (selected != value) {
                    selected = value;

                    if (value)
                        ShowDropDown ();
                    else
                        HideDropDown ();
                }
            }
        }

        /// <summary>
        /// Sets the bounds of the menu item. This API is considered internal and is not intended for public use.
        /// </summary>
        public void SetBounds (int x, int y, int width, int height, BoundsSpecified specified = BoundsSpecified.All)
        {
            Bounds = new Rectangle (x, y, width, height);
        }

        /// <summary>
        /// Shows this menu items drop down, if any.
        /// </summary>
        public void ShowDropDown ()
        {
            if (HasItems && OwnerControl != null) {
                dropdown = dropdown ??= new MenuDropDown (this);

                var dropdown_location = Point.Empty;

                // A submenu opens to the SIDE of its parent drop down, everything else opens BELOW its
                // host bar. MenuDropDown must be tested first: it now derives from ToolStrip (and so
                // from ToolBar), so the bar arm would otherwise claim it and drop submenus downwards.
                if (OwnerControl is MenuDropDown)
                    dropdown_location = OwnerControl.PointToScreen (new Point (Bounds.Right - 1, Bounds.Top));
                else if (OwnerControl is Menu || OwnerControl is ToolBar)
                    dropdown_location = OwnerControl.PointToScreen (new Point (Bounds.Left + 1, Bounds.Bottom));

                dropdown.Show (OwnerControl, dropdown_location);
                IsDropDownOpened = true;
            }
        }

        private string text = string.Empty;

        /// <summary>
        /// Gets or sets the text of the menu item.
        /// </summary>
        /// <remarks>
        /// Assigning this repaints the owner, as every other visible property here does. As a plain
        /// auto-property it changed nothing on screen: a status-bar label updated from a key handler kept
        /// showing its old text until something *else* happened to invalidate the control, so a caret
        /// position indicator appeared to lag a keystroke behind. The owner is laid out as well as
        /// invalidated because an item's width is measured from its text.
        /// </remarks>
        public virtual string Text {
            get => text;
            set {
                if (text == value)
                    return;

                text = value;

                OwnerControl?.PerformLayout ();
                OwnerControl?.Invalidate ();
            }
        }

        /// <summary>Gets or sets whether the item has a check mark. WinForms compat — use sub-classes for implementation.</summary>
        public bool Checked { get; set; }

        /// <summary>Gets or sets whether this is the default item. Stub in Majorsilence.Forms.</summary>
        public bool DefaultItem { get; set; }

        /// <summary>Gets or sets whether the item is drawn by the owner. Stub in Majorsilence.Forms.</summary>
        public bool OwnerDraw { get; set; }

        /// <summary>Gets or sets whether the item appears as a radio button when checked. Stub in Majorsilence.Forms.</summary>
        public bool RadioCheck { get; set; }

        /// <summary>Gets or sets whether the shortcut key is shown in the label. Stub in Majorsilence.Forms.</summary>
        public bool ShowShortcut { get; set; } = true;

        /// <summary>Gets or sets the merge order for menu merging. Stub in Majorsilence.Forms.</summary>
        public int MergeOrder { get; set; }

        /// <summary>Gets or sets the tag object. Stub in Majorsilence.Forms.</summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets whether the menu item is visible. Hidden items are skipped during layout,
        /// hit-testing, and rendering by <see cref="Menu"/>/<see cref="MenuDropDown"/> (WinForms compat).
        /// </summary>
        public bool Visible { get; set; } = true;

        /// <summary>Whether the item is displayed. WinForms alias of <see cref="Visible"/>.</summary>
        /// <remarks>
        /// ToolStripItem overrides this to raise AvailableChanged; here it is a plain alias.
        /// </remarks>
        public virtual bool Available {
            get => Visible;
            set => Visible = value;
        }
    }
}
