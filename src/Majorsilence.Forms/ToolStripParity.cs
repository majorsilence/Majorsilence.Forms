using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The Control-parity surface of ToolStripItem and ToolStrip (docs/winforms-gap-plan.md, item 4).
    //
    // Most of what was missing here is not toolbar-specific: it is the ambient-appearance events, the
    // drag family, the accessibility properties and the Reset* methods that WinForms puts on every
    // item so that code written against Control keeps working when it is handed a ToolStripItem.
    // That is why the plan called for doing it as one pass rather than piecemeal — the members share
    // a shape, and the interesting question for each is only whether this layer can drive it yet.
    //
    // Three groups, and the distinction is stated per member rather than left to the reader:
    //   * Real     — backed by state this layer already has (Available, Width, Owner, Invalidate...).
    //   * Raisable — a real event with a protected raiser, which the item raises where it can and a
    //                derived item can raise itself. Some are not yet raised by the framework.
    //   * Stored   — round-trips, and the drawing/input path does not consult it yet.
    //
    // Per COMPATIBILITY_MATRIX.md's stub policy, none of these throw.

    public partial class ToolStripItem
    {
#pragma warning disable CS0067 // Several of these have no framework trigger yet; see the file header.
        /// <summary>Occurs when <see cref="Available"/> changes.</summary>
        public event EventHandler? AvailableChanged;

        /// <summary>Occurs when the background color changes.</summary>
        public event EventHandler? BackColorChanged;

        /// <summary>Occurs when the foreground color changes.</summary>
        public event EventHandler? ForeColorChanged;

        /// <summary>Occurs when <see cref="Enabled"/> changes.</summary>
        public event EventHandler? EnabledChanged;

        /// <summary>Occurs when the item's text changes.</summary>
        public event EventHandler? TextChanged;

        /// <summary>Occurs when the item's visibility changes.</summary>
        public event EventHandler? VisibleChanged;

        /// <summary>Occurs when the item's location changes.</summary>
        public event EventHandler? LocationChanged;

        /// <summary>Occurs when the item's owner changes.</summary>
        public event EventHandler? OwnerChanged;

        /// <summary>Occurs when the item's selected state changes.</summary>
        public event EventHandler? SelectedChanged;

        /// <summary>Occurs when <see cref="DisplayStyle"/> changes.</summary>
        public event EventHandler? DisplayStyleChanged;

        /// <summary>Occurs when the right-to-left setting changes.</summary>
        public event EventHandler? RightToLeftChanged;

        /// <summary>Occurs when the item is double-clicked.</summary>
        public event EventHandler? DoubleClick;

        /// <summary>Occurs when the item is painted.</summary>
        public event PaintEventHandler? Paint;

        /// <summary>Occurs when a mouse button is pressed on the item.</summary>
        public event MouseEventHandler? MouseDown;

        /// <summary>Occurs when a mouse button is released on the item.</summary>
        public event MouseEventHandler? MouseUp;

        /// <summary>Occurs when the mouse moves over the item.</summary>
        public event MouseEventHandler? MouseMove;

        /// <summary>Occurs when the mouse enters the item.</summary>
        public event EventHandler? MouseEnter;

        /// <summary>Occurs when the mouse leaves the item.</summary>
        public event EventHandler? MouseLeave;

        /// <summary>Occurs when the mouse rests over the item.</summary>
        public event EventHandler? MouseHover;

        /// <summary>Occurs when a drag-and-drop operation completes over the item.</summary>
        public event DragEventHandler? DragDrop;

        /// <summary>Occurs when a drag enters the item.</summary>
        public event DragEventHandler? DragEnter;

        /// <summary>Occurs while a drag is over the item.</summary>
        public event DragEventHandler? DragOver;

        /// <summary>Occurs when a drag leaves the item.</summary>
        public event EventHandler? DragLeave;

        /// <summary>Occurs during a drag to let the source set the cursor.</summary>
        public event GiveFeedbackEventHandler? GiveFeedback;

        /// <summary>Occurs during a drag to let the source cancel it.</summary>
        public event QueryContinueDragEventHandler? QueryContinueDrag;

        /// <summary>Occurs when an accessibility client requests help.</summary>
        public event QueryAccessibilityHelpEventHandler? QueryAccessibilityHelp;

        /// <summary>Occurs when <see cref="Command"/> changes.</summary>
        public event EventHandler? CommandChanged;

        /// <summary>Occurs when the bound command's executability changes.</summary>
        public event EventHandler? CommandCanExecuteChanged;

        /// <summary>Occurs when <see cref="CommandParameter"/> changes.</summary>
        public event EventHandler? CommandParameterChanged;
#pragma warning restore CS0067

        private bool available = true;
        private System.Windows.Input.ICommand? command;

        /// <summary>
        /// Gets or sets whether this item is available to be shown on its parent. Real: it is the
        /// backing state <see cref="MenuItem.Visible"/> reflects.
        /// </summary>
        public bool Available {
            get => available;
            set {
                if (available == value)
                    return;
                available = value;
                Visible = value;
                OnAvailableChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the width of this item, in pixels.</summary>
        public int Width {
            get => Size.Width;
            set => Size = new Size (value, Size.Height);
        }

        /// <summary>Gets whether this item can be selected. False for separators and disabled items.</summary>
        public virtual bool CanSelect => Enabled && Available;

        /// <summary>Gets whether this item currently has the pointer pressed on it.</summary>
        public virtual bool Pressed { get; protected set; }

        /// <summary>Gets whether this item has been disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Gets the rectangle the item draws its content into, inside its margin and padding.</summary>
        public virtual Rectangle ContentRectangle => new (0, 0, Size.Width, Size.Height);

        /// <summary>Gets whether this item sits on a drop-down rather than directly on a strip.</summary>
        public bool IsOnDropDown => GetCurrentParent () is ToolStripDropDown;

        /// <summary>Gets whether this item is currently in its strip's overflow.</summary>
        public bool IsOnOverflow => Placement == ToolStripItemPlacement.Overflow;

        /// <summary>Gets where this item is currently laid out.</summary>
        public ToolStripItemPlacement Placement { get; internal set; } = ToolStripItemPlacement.Main;

        /// <summary>Gets or sets whether the tooltip text is generated from the item's text automatically.</summary>
        public bool AutoToolTip { get; set; }

        /// <summary>Gets or sets whether the item raises <see cref="DoubleClick"/> instead of two clicks.</summary>
        public bool DoubleClickEnabled { get; set; }

        /// <summary>Gets or sets whether the item accepts data dragged onto it.</summary>
        public virtual bool AllowDrop { get; set; }

        /// <summary>Gets or sets which edges of the container the item is anchored to.</summary>
        /// <remarks>Stored: items are laid out by their strip, which does not consult anchoring.</remarks>
        public AnchorStyles Anchor { get; set; } = AnchorStyles.Top | AnchorStyles.Left;

        /// <summary>Gets or sets which edge of the container the item docks to.</summary>
        /// <remarks>Stored, for the same reason as <see cref="Anchor"/>.</remarks>
        public DockStyle Dock { get; set; } = DockStyle.None;

        /// <summary>Gets or sets the background image drawn behind the item.</summary>
        /// <remarks>Stored; the strip paints item backgrounds through its renderer.</remarks>
        public virtual Majorsilence.Forms.Drawing.Image? BackgroundImage { get; set; }

        /// <summary>Gets or sets how <see cref="BackgroundImage"/> is laid out.</summary>
        public virtual ImageLayout BackgroundImageLayout { get; set; } = ImageLayout.Tile;

        /// <summary>Gets or sets the color treated as transparent in the item's image.</summary>
        /// <remarks>Stored; the image is drawn with its own alpha rather than a color key.</remarks>
        public Color ImageTransparentColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets whether the image is mirrored under a right-to-left layout.</summary>
        public bool RightToLeftAutoMirrorImage { get; set; }

        /// <summary>Gets or sets the direction the item's text runs in.</summary>
        public virtual ToolStripTextDirection TextDirection { get; set; } = ToolStripTextDirection.Inherit;

        /// <summary>Gets or sets how this item merges into a target strip.</summary>
        public MergeAction MergeAction { get; set; } = MergeAction.Append;

        /// <summary>Gets or sets the position this item merges into.</summary>
        public int MergeIndex { get; set; } = -1;

        /// <summary>Gets or sets the command invoked when the item is clicked.</summary>
        public System.Windows.Input.ICommand? Command {
            get => command;
            set {
                if (ReferenceEquals (command, value))
                    return;
                command = value;
                CommandChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
        public object? CommandParameter { get; set; }

        /// <summary>Gets or sets the accessible name reported for this item.</summary>
        public string? AccessibleName { get; set; }

        /// <summary>Gets or sets the accessible description reported for this item.</summary>
        public string? AccessibleDescription { get; set; }

        /// <summary>Gets or sets the description of this item's default action.</summary>
        public string? AccessibleDefaultActionDescription { get; set; }

        /// <summary>Gets the accessibility object describing this item to assistive technology.</summary>
        public ToolStripItemAccessibleObject AccessibilityObject => accessibilityObject ??= new ToolStripItemAccessibleObject (this);

        private ToolStripItemAccessibleObject? accessibilityObject;

        /// <summary>Returns the strip this item currently belongs to, or null when it is not on one.</summary>
        /// <remarks>Owner already exists on this type; this is the WinForms-named accessor for it.</remarks>
        public ToolStrip? GetCurrentParent () => Owner;


        /// <summary>Requests that this item be repainted, by invalidating the strip it sits on.</summary>
        public void Invalidate () => Owner?.Invalidate ();

        /// <summary>Requests that the given area of this item be repainted.</summary>
        public void Invalidate (Rectangle r) => Owner?.Invalidate ();

        /// <summary>Selects this item.</summary>
        /// <remarks>Hides <c>MenuItem.Select</c>, which is an event upstream. ToolStripItem derives
        /// from MenuItem in this library but not in WinForms, so the two names meet here; selecting an
        /// item is the meaning callers of <c>ToolStripItem.Select ()</c> expect.</remarks>
        public new void Select ()
        {
            if (!CanSelect)
                return;
            // MenuItem.Selected is computed by the base, so this raises the notification rather than
            // assigning a second, competing flag.
            SelectedChanged?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>
        /// Begins a drag-and-drop operation with this item as the source.
        /// </summary>
        /// <remarks>
        /// Returns <see cref="DragDropEffects.None"/>: there is no OS drag source in this layer yet,
        /// which is the same position <c>Control.DoDragDrop</c> is in (see COMPATIBILITY_MATRIX.md).
        /// </remarks>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects) => DragDropEffects.None;

        /// <summary>Resets the background color to its default.</summary>
        public virtual void ResetBackColor () => BackColor = Color.Empty;

        /// <summary>Resets the foreground color to its default.</summary>
        public virtual void ResetForeColor () => ForeColor = Color.Empty;

        /// <summary>Resets the display style to its default.</summary>
        public virtual void ResetDisplayStyle () => DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;

        /// <summary>Resets the font to its default.</summary>
        public virtual void ResetFont () => Font = null;

        /// <summary>Resets the image to its default.</summary>
        public virtual void ResetImage () => Image = null;

        /// <summary>Resets the margin to its default.</summary>
        public virtual void ResetMargin () => Margin = new Padding (0);

        /// <summary>Resets the padding to its default.</summary>
        public virtual void ResetPadding () => Padding = new Padding (0);

        /// <summary>Resets the right-to-left setting to its default.</summary>
        public virtual void ResetRightToLeft () => RightToLeft = RightToLeft.Inherit;

        /// <summary>Resets the text direction to its default.</summary>
        public virtual void ResetTextDirection () => TextDirection = ToolStripTextDirection.Inherit;

        /// <summary>Raises the <see cref="AvailableChanged"/> event.</summary>
        protected virtual void OnAvailableChanged (EventArgs e) => AvailableChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="OwnerChanged"/> event.</summary>
        protected virtual void OnOwnerChanged (EventArgs e) => OwnerChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="Paint"/> event.</summary>
        protected virtual void OnPaint (PaintEventArgs e) => Paint?.Invoke (this, e);

        /// <summary>Raises the <see cref="DoubleClick"/> event.</summary>
        protected virtual void OnDoubleClick (EventArgs e) => DoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="MouseDown"/> event.</summary>
        protected virtual void OnMouseDown (MouseEventArgs e)
        {
            Pressed = true;
            MouseDown?.Invoke (this, e);
        }

        /// <summary>Raises the <see cref="MouseUp"/> event.</summary>
        protected virtual void OnMouseUp (MouseEventArgs e)
        {
            Pressed = false;
            MouseUp?.Invoke (this, e);
        }

        /// <summary>Raises the <see cref="MouseEnter"/> event.</summary>
        protected virtual void OnMouseEnter (EventArgs e) => MouseEnter?.Invoke (this, e);

        /// <summary>Raises the <see cref="MouseLeave"/> event.</summary>
        protected virtual void OnMouseLeave (EventArgs e) => MouseLeave?.Invoke (this, e);

        /// <summary>Marks this item disposed.</summary>
        protected virtual void OnDisposed () => IsDisposed = true;
    }

    /// <summary>Describes a <see cref="ToolStripItem"/> to assistive technology.</summary>
    public class ToolStripItemAccessibleObject
    {
        /// <summary>Initializes a new instance for the given item.</summary>
        public ToolStripItemAccessibleObject (ToolStripItem ownerItem) => Owner = ownerItem;

        /// <summary>Gets the item this object describes.</summary>
        public ToolStripItem Owner { get; }

        /// <summary>Gets the accessible name, falling back to the item's text.</summary>
        public string Name => Owner.AccessibleName ?? Owner.Text ?? string.Empty;

        /// <summary>Gets the accessible description.</summary>
        public string? Description => Owner.AccessibleDescription;

        /// <summary>Gets the description of the item's default action.</summary>
        public string? DefaultAction => Owner.AccessibleDefaultActionDescription;
    }

    public partial class ToolStrip
    {
#pragma warning disable CS0067 // No framework trigger yet; see the file header.
        /// <summary>Occurs when an item is removed from this strip.</summary>
        public event ToolStripItemEventHandler? ItemRemoved;

        /// <summary>Occurs when the layout of this strip completes.</summary>
        public event EventHandler? LayoutCompleted;

        /// <summary>Occurs when <see cref="LayoutStyle"/> changes.</summary>
        public event EventHandler? LayoutStyleChanged;

        /// <summary>Occurs when the renderer changes.</summary>
        public event EventHandler? RendererChanged;
#pragma warning restore CS0067

        /// <summary>Gets or sets whether a click that activates the strip also activates the item under it.</summary>
        public bool AllowClickThrough { get; set; }

        /// <summary>Gets or sets whether the user can reorder items by dragging.</summary>
        public bool AllowItemReorder { get; set; }

        /// <summary>Gets or sets whether this strip's items can be merged into another strip.</summary>
        public bool AllowMerge { get; set; } = true;

        /// <summary>Gets or sets the direction drop-downs open in by default.</summary>
        public ToolStripDropDownDirection DefaultDropDownDirection { get; set; } = ToolStripDropDownDirection.Default;

        /// <summary>Gets or sets how the move grip is displayed.</summary>
        public ToolStripGripDisplayStyle GripDisplayStyle { get; set; } = ToolStripGripDisplayStyle.Vertical;

        /// <summary>Gets or sets the space around the move grip.</summary>
        public Padding GripMargin { get; set; } = new (2);

        /// <summary>Gets the bounds of the move grip.</summary>
        public Rectangle GripRectangle => GripStyle == ToolStripGripStyle.Visible ? new Rectangle (0, 0, 6, Height) : Rectangle.Empty;

        /// <summary>Gets whether this strip is itself a drop-down.</summary>
        public virtual bool IsDropDown => this is ToolStripDropDown;

        /// <summary>Gets whether an item is currently being dragged within this strip.</summary>
        public bool IsCurrentlyDragging { get; private set; }

        /// <summary>Gets or sets whether this strip lays out horizontally or vertically.</summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        /// <summary>Gets the button that shows items which did not fit.</summary>
        public ToolStripItem? OverflowButton { get; private set; }

        /// <summary>Gets or sets the layout settings for the current <see cref="LayoutStyle"/>.</summary>
        public LayoutSettings? LayoutSettings { get; set; }

        /// <summary>Returns the item at the given point within this strip, or null.</summary>
        public ToolStripItem? GetItemAt (Point point)
        {
            foreach (ToolStripItem item in Items) {
                if (!item.Available)
                    continue;
                if (new Rectangle (item.Bounds.Location, item.Size).Contains (point))
                    return item;
            }
            return null;
        }

        /// <inheritdoc cref="GetItemAt(Point)"/>
        public ToolStripItem? GetItemAt (int x, int y) => GetItemAt (new Point (x, y));

        /// <summary>
        /// Returns the next selectable item from <paramref name="start"/> in the given direction,
        /// wrapping at the ends as WinForms does.
        /// </summary>
        public ToolStripItem? GetNextItem (ToolStripItem? start, ArrowDirection direction)
        {
            if (Items.Count == 0)
                return null;

            var forward = direction is ArrowDirection.Right or ArrowDirection.Down;
            var index = start is null ? -1 : Items.IndexOf (start);

            for (var step = 1; step <= Items.Count; step++) {
                var next = forward
                    ? (index + step) % Items.Count
                    : ((index - step) % Items.Count + Items.Count) % Items.Count;
                if (Items[next] is ToolStripItem candidate && candidate.CanSelect)
                    return candidate;
            }
            return null;
        }

        /// <summary>Signals that an item drag has begun.</summary>
        public void BeginDrag () => IsCurrentlyDragging = true;

        /// <summary>Signals that an item drag has ended.</summary>
        public void EndDrag () => IsCurrentlyDragging = false;

        /// <summary>Paints the move grip. Routed through the renderer so a theme can draw it.</summary>
        public void PaintGrip (PaintEventArgs e)
        {
            if (e is null || GripStyle != ToolStripGripStyle.Visible)
                return;
            Renderer?.DrawGrip (new ToolStripGripRenderEventArgs (e.Graphics, this) {
                GripBounds = GripRectangle,
                GripDisplayStyle = GripDisplayStyle,
                GripStyle = GripStyle,
            });
        }

        /// <summary>Resets <see cref="Control.MinimumSize"/> to its default.</summary>
        public void ResetMinimumSize () => MinimumSize = Size.Empty;
    }
}
