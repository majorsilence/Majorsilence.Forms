using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ToolBar control.
    /// </summary>
    public partial class ToolBar : MenuBase
    {
        private ToolBarButtonCollection? _buttons;

        /// <summary>Gets the collection of ToolBarButtons in this toolbar.</summary>
        /// <remarks>
        /// Real as of W6 mechanisms. Each <see cref="ToolBarButton"/> is mirrored by a strip item in
        /// <see cref="MenuBase.Items"/> -- a <see cref="ToolStripButton"/>, or a separator for
        /// <see cref="ToolBarButtonStyle.Separator"/> -- that follows the button's text, image, enabled,
        /// visible, pushed and tooltip state, so the legacy <c>Buttons</c> surface is laid out, painted,
        /// hit-tested and clicked by the same machinery as a <see cref="ToolStrip"/>. A click raises
        /// <see cref="ButtonClick"/> (toggling a <see cref="ToolBarButtonStyle.ToggleButton"/>'s
        /// <see cref="ToolBarButton.Pushed"/>); the arrow of a <see cref="ToolBarButtonStyle.DropDownButton"/>
        /// raises <see cref="ButtonDropDown"/> and opens <see cref="ToolBarButton.DropDownMenu"/>.
        /// </remarks>
        public ToolBarButtonCollection Buttons => _buttons ??= new ToolBarButtonCollection (this);

        /// <summary>Fires when a ToolBarButton is clicked.</summary>
        /// <remarks>Real as of W6 mechanisms; see <see cref="Buttons"/>.</remarks>
        public event EventHandler<ToolBarButtonClickEventArgs>? ButtonClick;

        /// <summary>Raises the <see cref="ButtonClick"/> event.</summary>
        protected virtual void OnButtonClick (ToolBarButtonClickEventArgs e) => ButtonClick?.Invoke (this, e);

        // ── the Buttons -> Items mirror (W6 mechanisms) ───────────────────────────────────────────
        private readonly Dictionary<ToolBarButton, MenuItem> button_items = new ();

        internal void ButtonsChanged ()
        {
            // Rebuilt in order rather than patched: a button's Style decides which item type mirrors
            // it, and the order in Buttons is the order on the bar.
            foreach (var item in button_items.Values)
                Items.Remove (item);

            button_items.Clear ();

            foreach (var button in Buttons) {
                button.Parent = this;

                MenuItem item = button.Style == ToolBarButtonStyle.Separator
                    ? new MenuSeparatorItem ()
                    : new ToolBarButtonItem (this, button);

                button_items[button] = item;
                Items.Add (item);
            }

            SyncButtons ();
        }

        internal void ButtonChanged (ToolBarButton button)
        {
            if (button_items.TryGetValue (button, out var item) && item is ToolBarButtonItem mirror)
                mirror.Sync ();
            else
                ButtonsChanged ();

            PerformLayout ();
            Invalidate ();
        }

        private void SyncButtons ()
        {
            foreach (var item in button_items.Values.OfType<ToolBarButtonItem> ())
                item.Sync ();

            PerformLayout ();
            Invalidate ();
        }

        // The image a button names, by key first then by index, from the bar's ImageList.
        internal SkiaSharp.SKBitmap? ImageFor (ToolBarButton button)
        {
            if (ImageList is not { } images)
                return null;

            if (!string.IsNullOrEmpty (button.ImageKey)) {
                var index = images.Images.IndexOfKey (button.ImageKey);

                if (index >= 0)
                    return images.Images[index];
            }

            return button.ImageIndex >= 0 && button.ImageIndex < images.Images.Count ? images.Images[button.ImageIndex] : null;
        }

        internal void RaiseButtonClick (ToolBarButton button) => OnButtonClick (new ToolBarButtonClickEventArgs (button));

        /// <summary>The button a strip item mirrors, or null for an item that is not one.</summary>
        internal ToolBarButton? ButtonFor (MenuItem item) => (item as ToolBarButtonItem)?.Button;

        /// <inheritdoc/>
        /// <remarks><see cref="ShowToolTips"/> and <see cref="ToolBarButton.ToolTipText"/> (W6 mechanisms).</remarks>
        internal override string? GetToolTipText (Point location)
        {
            if (!ShowToolTips || GetItemAtLocation (location) is not ToolBarButtonItem item)
                return null;

            return string.IsNullOrEmpty (item.Button.ToolTipText) ? null : item.Button.ToolTipText;
        }

        // Legacy-bar chrome (Divider, Appearance, Wrappable) applies to the ToolBar proper; the
        // ToolStrip family that derives from it has its own look and upstream ToolStrip has none of them.
        internal virtual bool LegacyChrome => true;

        private ImageList? image_list;

        /// <summary>
        /// Gets or sets the ImageList this strip's items index into through
        /// <see cref="ToolStripItem.ImageIndex"/> / <see cref="ToolStripItem.ImageKey"/>.
        /// </summary>
        /// <remarks>
        /// Assigning this re-lays out the strip as well as repainting it, because an item's preferred
        /// size is measured from the image it will draw.
        /// </remarks>
        public ImageList? ImageList {
            get => image_list;
            set {
                if (ReferenceEquals (image_list, value))
                    return;

                image_list = value;

                PerformLayout ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the size of the buttons on the toolbar.</summary>
        /// <remarks>Read as of W6 mechanisms: the smallest size a mirrored button is laid out at.</remarks>
        public System.Drawing.Size ButtonSize {
            get => button_size;
            set {
                if (button_size == value)
                    return;

                button_size = value;
                PerformLayout ();
                Invalidate ();
            }
        }

        private System.Drawing.Size button_size = new System.Drawing.Size (24, 22);
        /// <summary>
        /// Initializes a new instance of the ToolBar class.
        /// </summary>
        public ToolBar ()
        {
            Dock = DockStyle.Top;
        }

        /// <summary>
        /// Initializes a new instance of the ToolBar class with the provided root MenuItem. Exists so
        /// derived strips further down the chain (ToolStrip -> MenuDropDown) can still reach
        /// <see cref="MenuBase(MenuItem)"/>, which is what backs every item's sub-menu drop down.
        /// </summary>
        protected ToolBar (MenuItem root) : base (root)
        {
            Dock = DockStyle.Top;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 34);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
          (style) => {
              style.Border.Bottom.Width = 1;
          });

        // Part styles for the items (CSS `ToolBar::item` and `ToolBar::item:hover`). The hover style
        // layers on the item style, so an item text colour carries into the hovered state.

        /// <summary>The default style of an item: its text colour (the background is the strip itself unless set). CSS: <c>ToolBar::item</c>.</summary>
        public static readonly ControlStyle DefaultItemStyle = new ControlStyle (null,
            (style) => { style.ForegroundColor = Theme.ForegroundColor; });

        /// <summary>The default style of a hovered (or open) item. CSS: <c>ToolBar::item:hover</c>.</summary>
        public static readonly ControlStyle DefaultItemHoverStyle = new ControlStyle (DefaultItemStyle,
            (style) => style.BackgroundColor = Theme.ControlHighlightLowColor);

        /// <inheritdoc/>
        protected override bool IsTopLevelMenu => true;

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            // Hidden items are excluded, as Menu, MenuDropDown and StatusStrip all already do. Laying
            // them out left a button that was painted but skipped by hit-testing -- a dead, visible
            // button -- which is what permission-based toolbar trimming produced (TSM-04).
            var visible = Items.Cast<MenuItem> ().Where (i => i.Visible).ToList ();

            // The grip is drawn in a band reserved out of the layout rectangle rather than painted
            // over the first item: GripVisible and GripMargin were stored and read by nothing, so a
            // strip that asked for a drag grip got no grip AND no space for one (TSM-43).
            var area = LogicalClientRectangle;
            var grip = GripBandWidth;

            if (grip > 0)
                area = ReserveGripBand (area, grip);

            ArrangeItems (area, visible);

            foreach (var (button, item) in button_items)
                button.Rectangle = item.Bounds;

            OnLayoutCompletedCore ();
        }

        // The band the grip takes: the leading edge of a horizontal bar. A vertical ToolStrip reserves
        // the top instead (W6 mechanisms).
        internal virtual Rectangle ReserveGripBand (Rectangle area, int grip)
            => new Rectangle (area.Left + grip, area.Top, Math.Max (0, area.Width - grip), area.Height);

        // The legacy bar's arrangement: one horizontal row, expanded to the bar's height, trailing
        // items pinned, wrapping when Wrappable. ToolStrip overrides this with its layout styles.
        internal virtual void ArrangeItems (Rectangle area, List<MenuItem> visible)
        {
            StackLayoutEngine.HorizontalExpand.Layout (area, visible.Cast<ILayoutable> ());

            PinTrailingItems (visible);

            if (Wrappable && LegacyChrome)
                WrapIntoRows (visible, area, grow: true);
        }

        // Wrappable (W6 mechanisms): items that ran past the bar's right edge start a new row, and --
        // when asked -- the bar grows to hold the rows, as upstream's auto-sized ToolBar does. Also the
        // arrangement behind ToolStripLayoutStyle.Flow.
        internal void WrapIntoRows (List<MenuItem> visible, Rectangle area, bool grow)
        {
            if (visible.Count == 0 || visible[visible.Count - 1].Bounds.Right <= area.Right)
                return;

            var row_height = visible.Max (i => i.Bounds.Height);
            var x = area.Left;
            var y = area.Top;
            var rows = 1;

            foreach (var item in visible) {
                var width = item.Bounds.Width;

                if (x > area.Left && x + width > area.Right) {
                    x = area.Left;
                    y += row_height;
                    rows++;
                }

                item.SetBounds (x, y, width, row_height);
                x += width;
            }

            var wanted = y + row_height - area.Top + (Height - LogicalClientRectangle.Height);

            if (grow && rows > 1 && Height < wanted)
                Height = wanted;
        }

        // ToolStrip raises LayoutCompleted from this; the layout itself lives here (W6.1 sweep).
        internal virtual void OnLayoutCompletedCore () { }

        /// <summary>
        /// The logical width reserved at the strip's leading edge for the drag grip, or 0 when none is
        /// shown. Read by both <see cref="LayoutItems"/> and the renderer, so the space reserved and
        /// the space painted cannot disagree.
        /// </summary>
        internal virtual int GripBandWidth => 0;

        /// <summary>
        /// The rectangle the grip is drawn in -- the same one <see cref="LayoutItems"/> measures
        /// against, exposed for the renderer because <c>LogicalClientRectangle</c> is protected.
        /// </summary>
        internal System.Drawing.Rectangle GripBandBounds => LogicalClientRectangle;

        /// <summary>The width of the grip's dotted rule itself, excluding its margin.</summary>
        internal const int GripRuleWidth = 3;

        // ToolStripItem.Alignment = Right pins an item to the strip's trailing edge -- the Help or
        // Settings button that sits apart from the rest. It was stored and read by nothing, so every
        // item was laid out left to right in declaration order whatever the property said.
        //
        // Applied after the stack layout rather than instead of it, so the items keep the widths that
        // layout measured for them; only their X moves. Right-aligned items keep their relative order,
        // which is why this walks backwards from the edge.
        private void PinTrailingItems (List<MenuItem> visible)
        {
            var trailing = visible
                .OfType<ToolStripItem> ()
                .Where (item => item.Alignment == ToolStripItemAlignment.Right)
                .ToList ();

            if (trailing.Count == 0)
                return;

            var x = LogicalClientRectangle.Right;

            for (var i = trailing.Count - 1; i >= 0; i--) {
                var bounds = trailing[i].Bounds;

                x -= bounds.Width;
                trailing[i].SetBounds (x, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>Represents a button on a ToolBar control.</summary>
    /// <remarks>Every property below reaches the bar as of W6 mechanisms; see <see cref="ToolBar.Buttons"/>.</remarks>
    public partial class ToolBarButton
    {
        private string text = string.Empty;
        private string tool_tip_text = string.Empty;
        private ToolBarButtonStyle style = ToolBarButtonStyle.PushButton;
        private bool enabled = true;
        private bool visible = true;
        private bool pushed;
        private bool partial_push;
        private int image_index = -1;
        private string image_key = string.Empty;
        private ContextMenu? drop_down_menu;

        /// <summary>Gets or sets the text of the button.</summary>
        public string Text { get => text; set => Set (ref text, value ?? string.Empty); }

        /// <summary>Gets or sets the tooltip text of the button, shown when <see cref="ToolBar.ShowToolTips"/> is set.</summary>
        public string ToolTipText { get => tool_tip_text; set => Set (ref tool_tip_text, value ?? string.Empty); }

        /// <summary>Gets or sets the style of the button.</summary>
        public ToolBarButtonStyle Style {
            get => style;
            set {
                if (style == value)
                    return;

                style = value;
                Parent?.ButtonsChanged ();   // the style decides which item type mirrors the button
            }
        }

        /// <summary>Gets or sets whether the button is enabled.</summary>
        public bool Enabled { get => enabled; set => Set (ref enabled, value); }

        /// <summary>Gets or sets whether the button is visible.</summary>
        public bool Visible { get => visible; set => Set (ref visible, value); }

        /// <summary>Gets or sets whether the button is in a pushed state (toggle).</summary>
        public bool Pushed { get => pushed; set => Set (ref pushed, value); }

        /// <summary>Gets or sets whether the button is partially pushed (an indeterminate toggle).</summary>
        public bool PartialPush { get => partial_push; set => Set (ref partial_push, value); }

        /// <summary>Gets or sets the image index in the parent ToolBar's ImageList.</summary>
        public int ImageIndex { get => image_index; set => Set (ref image_index, value); }

        /// <summary>Gets or sets the image key in the parent ToolBar's ImageList.</summary>
        public string ImageKey { get => image_key; set => Set (ref image_key, value ?? string.Empty); }

        /// <summary>Gets or sets an object with additional user data about this button.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the name of the button.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the menu a <see cref="ToolBarButtonStyle.DropDownButton"/> opens from its arrow.</summary>
        public ContextMenu? DropDownMenu { get => drop_down_menu; set => Set (ref drop_down_menu, value); }

        private void Set<T> (ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals (field, value))
                return;

            field = value;
            Parent?.ButtonChanged (this);
        }
    }

    // The strip item that stands in for a ToolBarButton on the bar (W6 mechanisms).
    internal sealed class ToolBarButtonItem : ToolStripButton
    {
        private readonly ToolBar bar;

        internal ToolBarButtonItem (ToolBar bar, ToolBarButton button)
        {
            this.bar = bar;
            Button = button;
            Sync ();
        }

        internal ToolBarButton Button { get; }

        internal void Sync ()
        {
            Text = Button.Text;
            Enabled = Button.Enabled;
            Visible = Button.Visible;
            Checked = Button.Pushed;
            ToolTipText = Button.ToolTipText;
            SetImageSK (bar.ImageFor (Button));

            // ToolBar.TextAlign: Underneath stacks the caption below the image, Right puts it beside.
            TextImageRelation = bar.TextAlign == ToolBarTextAlign.Underneath
                ? TextImageRelation.ImageAboveText
                : TextImageRelation.ImageBeforeText;
        }

        // The arrow gutter of a drop-down button, in the strip's logical coordinates.
        internal bool InArrowGutter (Point location)
            => Button.Style == ToolBarButtonStyle.DropDownButton && bar.DropDownArrows && location.X >= Bounds.Right - 16;

        protected internal override void OnClick (MouseEventArgs e)
        {
            // A drop-down button's arrow opens the menu; without arrows the whole button does. Upstream
            // raises ButtonDropDown for that and ButtonClick for a press on the body.
            if (Button.Style == ToolBarButtonStyle.DropDownButton && (InArrowGutter (e.Location) || !bar.DropDownArrows)) {
                bar.RaiseButtonDropDown (Button);
                Button.DropDownMenu?.Show (bar, new Point (Bounds.Left, Bounds.Bottom));
                return;
            }

            if (Button.Style == ToolBarButtonStyle.ToggleButton)
                Button.Pushed = !Button.Pushed;

            base.OnClick (e);
            bar.RaiseButtonClick (Button);
        }
    }

    /// <summary>A collection of ToolBarButton objects.</summary>
    public class ToolBarButtonCollection : Collection<ToolBarButton>
    {
        /// <summary>The bar whose items mirror this collection, when it belongs to one (W6 mechanisms).</summary>
        internal ToolBar? Bar { get; set; }

        /// <summary>Adds a button with the specified text.</summary>
        public ToolBarButton Add (string text)
        {
            var button = new ToolBarButton { Text = text };
            Add (button);
            return button;
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, ToolBarButton item)
        {
            Guard.ThrowIfNull (item);
            base.InsertItem (index, item);
            Bar?.ButtonsChanged ();
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            this[index].Parent = null;
            base.RemoveItem (index);
            Bar?.ButtonsChanged ();
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, ToolBarButton item)
        {
            Guard.ThrowIfNull (item);
            this[index].Parent = null;
            base.SetItem (index, item);
            Bar?.ButtonsChanged ();
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var button in this)
                button.Parent = null;

            base.ClearItems ();
            Bar?.ButtonsChanged ();
        }
    }

    /// <summary>Provides data for the ToolBar.ButtonClick event.</summary>
    public class ToolBarButtonClickEventArgs : EventArgs
    {
        /// <summary>Gets the button that was clicked.</summary>
        public ToolBarButton Button { get; }

        /// <summary>Initializes a new instance.</summary>
        public ToolBarButtonClickEventArgs (ToolBarButton button) => Button = button;
    }

    /// <summary>Specifies the style of a ToolBarButton.</summary>
    public enum ToolBarButtonStyle
    {
        /// <summary>A standard push button.</summary>
        PushButton = 1,
        /// <summary>A toggle button.</summary>
        ToggleButton = 2,
        /// <summary>A separator.</summary>
        Separator = 3,
        /// <summary>A drop-down button.</summary>
        DropDownButton = 4
    }
}
