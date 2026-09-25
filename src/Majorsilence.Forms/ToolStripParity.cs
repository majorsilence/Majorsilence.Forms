using System;
using System.Linq;
using System.Collections.Generic;
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

        /// <summary>Occurs when <see cref="MenuItem.Enabled"/> changes.</summary>
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

        internal void RaiseMouseHover () => MouseHover?.Invoke (this, EventArgs.Empty);

        // Raised by the strip as its hovered item changes (W6); the events were declared and, the scan
        // said, raised -- by a same-named method on another type. They were not.
        internal void RaiseMouseEnter () => OnMouseEnter (EventArgs.Empty);
        internal void RaiseMouseLeave () => OnMouseLeave (EventArgs.Empty);

        /// <summary>Occurs when a drag-and-drop operation completes over the item.</summary>
        /// <remarks>Real as of W6 mechanisms: the owning strip forwards its own drag events to the item
        /// under the pointer when the item's <see cref="AllowDrop"/> is set.</remarks>
        public event DragEventHandler? DragDrop;

        /// <summary>Occurs when a drag enters the item.</summary>
        /// <inheritdoc cref="DragDrop" path="/remarks"/>
        public event DragEventHandler? DragEnter;

        /// <summary>Occurs while a drag is over the item.</summary>
        /// <inheritdoc cref="DragDrop" path="/remarks"/>
        public event DragEventHandler? DragOver;

        /// <summary>Occurs when a drag leaves the item.</summary>
        /// <inheritdoc cref="DragDrop" path="/remarks"/>
        public event EventHandler? DragLeave;

        /// <summary>Occurs during a drag to let the source set the cursor.</summary>
        /// <remarks>Real as of W6 mechanisms: raised on every pointer move of a drag this item started
        /// through <see cref="DoDragDrop(object, DragDropEffects)"/>.</remarks>
        public event GiveFeedbackEventHandler? GiveFeedback;

        /// <summary>Occurs during a drag to let the source cancel it.</summary>
        /// <inheritdoc cref="GiveFeedback" path="/remarks"/>
        public event QueryContinueDragEventHandler? QueryContinueDrag;

        /// <summary>Begins a drag-and-drop operation with this item as the source.</summary>
        /// <remarks>Real as of W6 mechanisms; the same in-process session as
        /// <see cref="Control.DoDragDrop(object, DragDropEffects)"/>, with this item receiving <see cref="GiveFeedback"/> and
        /// <see cref="QueryContinueDrag"/>. Returns <see cref="DragDropEffects.None"/> when the item is
        /// not on a strip.</remarks>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects)
        {
            if (OwnerControl is not { } owner)
                return DragDropEffects.None;

            var session = DragDropSession.Begin (owner, data, allowedEffects, this);

            return Form.RunModal (session.Completion);
        }

        // The strip's forwarding (MenuBase.OnDrag*) and the session's feedback reach the field events here.
        internal void RaiseDragEnter (DragEventArgs e) => DragEnter?.Invoke (this, e);
        internal void RaiseDragOver (DragEventArgs e) => DragOver?.Invoke (this, e);
        internal void RaiseDragDrop (DragEventArgs e) => DragDrop?.Invoke (this, e);
        internal void RaiseDragLeave () => DragLeave?.Invoke (this, EventArgs.Empty);
        internal void RaiseGiveFeedback (GiveFeedbackEventArgs e) => GiveFeedback?.Invoke (this, e);
        internal void RaiseQueryContinueDrag (QueryContinueDragEventArgs e) => QueryContinueDrag?.Invoke (this, e);

        /// <summary>Occurs when an accessibility client requests help.</summary>
        public event QueryAccessibilityHelpEventHandler? QueryAccessibilityHelp;

        /// <summary>Occurs when <see cref="Command"/> changes.</summary>
        public event EventHandler? CommandChanged;

        /// <summary>Occurs when the bound command's executability changes.</summary>
        public event EventHandler? CommandCanExecuteChanged;

        private void OnCommandCanExecuteChangedRelay (object? sender, EventArgs e) => CommandCanExecuteChanged?.Invoke (this, e);

        /// <summary>Occurs when <see cref="CommandParameter"/> changes.</summary>
        public event EventHandler? CommandParameterChanged;
#pragma warning restore CS0067

        // `private bool available` went with the second store it backed: Available reads Visible now.
        private System.Windows.Input.ICommand? command;

        /// <summary>
        /// Gets or sets whether this item is available to be shown on its parent. Real: it is the
        /// backing state <see cref="MenuItem.Visible"/> reflects.
        /// </summary>
        /// <remarks>Delegates to <see cref="MenuItem.Visible"/> rather than keeping a second flag: the
        /// AvailableChanged and VisibleChanged raises both hang off that setter's hook now, and raising
        /// here as well announced an availability change twice per assignment.</remarks>
        public override bool Available {
            get => Visible;
            set => Visible = value;
        }

        /// <summary>Gets or sets the width of this item, in pixels.</summary>
        public new int Width {
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
        /// <remarks>Read as of W6 mechanisms: the owning strip forwards a drag over the item to the
        /// item's own <see cref="DragEnter"/> / <see cref="DragOver"/> / <see cref="DragLeave"/> /
        /// <see cref="DragDrop"/> when this is set.</remarks>
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

        /// <summary>Gets or sets the direction the item's text is drawn in; Inherit takes the strip's.</summary>
        /// <remarks>Read by the strip renderer as of W6 mechanisms: vertical text is measured and drawn rotated.</remarks>
        public virtual ToolStripTextDirection TextDirection {
            get => text_direction;
            set {
                if (text_direction == value)
                    return;

                text_direction = value;
                OwnerControl?.PerformLayout ();
                OwnerControl?.Invalidate ();
            }
        }

        private ToolStripTextDirection text_direction = ToolStripTextDirection.Inherit;

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

                // Relay the command's CanExecuteChanged as our own, as upstream does (W6.1 sweep).
                if (command is not null)
                    command.CanExecuteChanged -= OnCommandCanExecuteChangedRelay;
                command = value;
                if (command is not null)
                    command.CanExecuteChanged += OnCommandCanExecuteChangedRelay;
                CommandChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        // Notifies on change; the event was declared and raised by nothing (W6.1).
        private object? command_parameter;

        /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
        public object? CommandParameter {
            get => command_parameter;
            set {
                if (ReferenceEquals (command_parameter, value))
                    return;

                command_parameter = value;
                CommandParameterChanged?.Invoke (this, EventArgs.Empty);
            }
        }

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

        /// <summary>Raises the <see cref="TextChanged"/> event.</summary>
        protected virtual void OnTextChanged (EventArgs e) => TextChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="LocationChanged"/> event.</summary>
        protected virtual void OnLocationChanged (EventArgs e) => LocationChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="SelectedChanged"/> event.</summary>
        protected virtual void OnSelectedChanged (EventArgs e) => SelectedChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="BackColorChanged"/> event.</summary>
        protected virtual void OnBackColorChanged (EventArgs e) => BackColorChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ForeColorChanged"/> event.</summary>
        protected virtual void OnForeColorChanged (EventArgs e) => ForeColorChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RightToLeftChanged"/> event.</summary>
        protected virtual void OnRightToLeftChanged (EventArgs e) => RightToLeftChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="MouseMove"/> event.</summary>
        protected virtual void OnMouseMove (MouseEventArgs e) => MouseMove?.Invoke (this, e);

        // The MenuItem seams, routed to the WinForms-named raisers above. Seven of these events were
        // declared behind the CS0067 pragma at the top of this class and raised by nothing (W6.1);
        // upstream raises every one of them from the corresponding setter.
        internal override void OnTextChangedCore () => OnTextChanged (EventArgs.Empty);
        internal override void OnLocationChangedCore () => OnLocationChanged (EventArgs.Empty);
        internal override void OnHoveredChangedCore () => OnSelectedChanged (EventArgs.Empty);
        internal override void OnParentChangedCore () => OnOwnerChanged (EventArgs.Empty);

        // Called from MenuBase.OnMouseMove with the strip's logical point; upstream's item-level
        // MouseMove is ITEM-relative, so the item's own logical origin is subtracted first.
        internal void RaiseMouseMove (MouseEventArgs e)
            => OnMouseMove (new MouseEventArgs (e.Button, e.Clicks,
                e.X - Bounds.Left, e.Y - Bounds.Top, e.Delta));

        /// <summary>Raises the <see cref="Paint"/> event.</summary>
        protected virtual void OnPaint (PaintEventArgs e) => Paint?.Invoke (this, e);

        /// <summary>Raises the <see cref="DoubleClick"/> event.</summary>
        protected virtual void OnDoubleClick (EventArgs e) => DoubleClick?.Invoke (this, e);

        // The strip's double-click reaches the item under the pointer here. Upstream raises the item's
        // DoubleClick only with DoubleClickEnabled; otherwise a double-click is two clicks (W6).
        internal void RaiseDoubleClick (MouseEventArgs e)
        {
            if (DoubleClickEnabled)
                OnDoubleClick (e);
        }

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
    public class ToolStripItemAccessibleObject : AccessibleObject
    {
        /// <summary>Initializes a new instance for the given item.</summary>
        public ToolStripItemAccessibleObject (ToolStripItem ownerItem) => Owner = ownerItem;

        /// <summary>Gets the item this object describes.</summary>
        public ToolStripItem Owner { get; }

        /// <summary>Gets the accessible name, falling back to the item's text.</summary>
        public override string? Name {
            get => Owner.AccessibleName ?? Owner.Text ?? string.Empty;
            set => Owner.AccessibleName = value;
        }

        /// <summary>Gets the accessible description.</summary>
        public override string? Description => Owner.AccessibleDescription;

        /// <summary>Gets the description of the item's default action.</summary>
        public override string? DefaultAction => Owner.AccessibleDefaultActionDescription;
    }

    public partial class ToolStrip
    {
        /// <summary>Occurs when an item is removed from this strip.</summary>
        /// <remarks>Raised via <c>OnItemRemoved</c> (WinFormsCompat.cs) — wired, not a stub.</remarks>
        public event ToolStripItemEventHandler? ItemRemoved;

#pragma warning disable CS0067 // No framework trigger yet; see the file header.
        /// <summary>Occurs when the layout of this strip completes.</summary>
        public event EventHandler? LayoutCompleted;

        internal override void OnLayoutCompletedCore () => LayoutCompleted?.Invoke (this, EventArgs.Empty);

        /// <summary>Occurs when <see cref="LayoutStyle"/> changes.</summary>
        public event EventHandler? LayoutStyleChanged;

        /// <summary>Occurs when the renderer changes.</summary>
        public event EventHandler? RendererChanged;
#pragma warning restore CS0067

        /// <summary>Gets or sets whether a click that activates the strip also activates the item under it.</summary>
        public bool AllowClickThrough { get; set; }

        // Divider, Appearance and Wrappable belong to the legacy ToolBar; a ToolStrip has none of them upstream.
        internal override bool LegacyChrome => false;

        /// <summary>Gets or sets whether the user can reorder items by dragging.</summary>
        /// <remarks>Read as of W6 mechanisms: with Alt held, dragging an item past the drag threshold
        /// starts a drag of that item, and dropping it on the strip moves it to the slot under the
        /// pointer -- upstream's Alt-drag reorder. A drag from another strip that also allows
        /// reordering moves the item across, as upstream's does.</remarks>
        public bool AllowItemReorder { get; set; }

        /// <summary>Gets or sets whether this strip's items can be merged into another strip.</summary>
        public bool AllowMerge { get; set; } = true;

        /// <summary>Gets or sets the direction drop-downs open in by default.</summary>
        public ToolStripDropDownDirection DefaultDropDownDirection { get; set; } = ToolStripDropDownDirection.Default;

        /// <summary>Gets or sets how the move grip is displayed.</summary>
        public ToolStripGripDisplayStyle GripDisplayStyle { get; set; } = ToolStripGripDisplayStyle.Vertical;

        /// <summary>Gets or sets the space around the move grip.</summary>
        public Padding GripMargin { get; set; } = new (2);

        /// <inheritdoc/>
        /// <remarks>
        /// Both knobs are honoured: upstream derives <c>GripVisible</c> from <c>GripStyle</c>, while
        /// this layer declares them separately, so a strip that turned either one off gets no grip.
        /// </remarks>
        internal override int GripBandWidth
            => GripVisible && GripStyle == ToolStripGripStyle.Visible
                ? GripMargin.Horizontal + GripRuleWidth
                : 0;

        /// <summary>Gets the bounds of the move grip.</summary>
        public Rectangle GripRectangle => GripStyle == ToolStripGripStyle.Visible ? new Rectangle (0, 0, 6, Height) : Rectangle.Empty;

        /// <summary>Gets whether this strip is itself a drop-down.</summary>
        public virtual bool IsDropDown => this is ToolStripDropDown;

        /// <summary>Gets whether an item is currently being dragged within this strip.</summary>
        public bool IsCurrentlyDragging { get; private set; }

        /// <summary>Gets or sets whether this strip lays out horizontally or vertically.</summary>
        /// <remarks>Derived, as upstream's is: the two stack styles fix it, and StackWithOverflow follows
        /// the strip's docked edge unless it was set by hand (W6 mechanisms).</remarks>
        public Orientation Orientation {
            get => LayoutStyle switch {
                ToolStripLayoutStyle.VerticalStackWithOverflow => Orientation.Vertical,
                ToolStripLayoutStyle.HorizontalStackWithOverflow => Orientation.Horizontal,
                _ => orientation_override ?? (Dock is DockStyle.Left or DockStyle.Right ? Orientation.Vertical : Orientation.Horizontal),
            };
            set {
                if (orientation_override == value)
                    return;

                orientation_override = value;
                PerformLayout ();
                Invalidate ();
            }
        }

        private Orientation? orientation_override;

        /// <summary>Gets the button that shows items which did not fit.</summary>
        /// <remarks>Real as of W6 mechanisms: the items that do not fit a stack layout move into its
        /// drop-down and it is laid out at the strip's trailing edge while any have; see <see cref="CanOverflow"/>.</remarks>
        public ToolStripOverflowButton OverflowButton => overflow_button ??= CreateOverflowButton ();

        private ToolStripOverflowButton? overflow_button;

        /// <summary>Gets or sets the layout settings for the current <see cref="LayoutStyle"/>.</summary>
        /// <remarks>Stored only: the <see cref="ToolStripLayoutStyle.Flow"/> arrangement here wraps with
        /// no settings and <see cref="ToolStripLayoutStyle.Table"/> is laid out as Flow.</remarks>
        public LayoutSettings? LayoutSettings { get; set; }

        // ── layout styles and overflow (W6 mechanisms) ──────────────────────────────────────────
        // Items live in Items (the facade) and in base.Items (the collection layout, paint and hit-
        // testing read). Overflow moves items from base.Items into the overflow button's own drop-down
        // items -- so they stay in Items, as upstream keeps them, while the strip stops laying them out
        // -- and every layout pass starts by moving them back, so the decision is fresh.
        internal ToolStripLayoutStyle EffectiveLayoutStyle
            => LayoutStyle == ToolStripLayoutStyle.StackWithOverflow
                ? (Orientation == Orientation.Vertical ? ToolStripLayoutStyle.VerticalStackWithOverflow : ToolStripLayoutStyle.HorizontalStackWithOverflow)
                : LayoutStyle;

        // True while items move between the strip and its overflow: ItemAdded/ItemRemoved are for the
        // application's changes, not the layout's.
        internal bool suppress_item_notifications;

        private ToolStripOverflowButton CreateOverflowButton ()
        {
            var button = new ToolStripOverflowButton ();

            suppress_item_notifications = true;

            try {
                base.Items.Add (button);   // on the strip, never in the facade -- upstream's is not in Items either
            } finally {
                suppress_item_notifications = false;
            }

            return button;
        }

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            RestoreOverflowedItems ();
            base.LayoutItems ();
        }

        private void RestoreOverflowedItems ()
        {
            if (overflow_button is null || overflow_button.Items.Count == 0)
                return;

            var mirror = base.Items;

            suppress_item_notifications = true;

            try {
                foreach (var item in overflow_button.Items.ToList ()) {
                    overflow_button.Items.Remove (item);

                    var facade_index = Items.IndexOf (item);

                    // Removed from Items while it was overflowed: it is gone for good.
                    if (facade_index < 0)
                        continue;

                    // Back at its facade position: after every facade item before it that is on the strip.
                    var insert_at = 0;

                    for (var i = 0; i < facade_index; i++)
                        if (mirror.Contains (Items[i]))
                            insert_at++;

                    mirror.Insert (Math.Min (insert_at, mirror.Count), item);

                    if (item is ToolStripItem strip_item)
                        strip_item.Placement = ToolStripItemPlacement.Main;
                }
            } finally {
                suppress_item_notifications = false;
            }
        }

        private void MoveToOverflow (List<MenuItem> overflowed)
        {
            if (overflowed.Count == 0)
                return;

            var button = OverflowButton;

            suppress_item_notifications = true;

            try {
                foreach (var item in overflowed) {
                    base.Items.Remove (item);
                    button.Items.Add (item);

                    if (item is ToolStripItem strip_item)
                        strip_item.Placement = ToolStripItemPlacement.Overflow;
                }
            } finally {
                suppress_item_notifications = false;
            }
        }

        internal override Rectangle ReserveGripBand (Rectangle area, int grip)
            => Orientation == Orientation.Vertical
                ? new Rectangle (area.Left, area.Top + grip, area.Width, Math.Max (0, area.Height - grip))
                : base.ReserveGripBand (area, grip);

        internal override void ArrangeItems (Rectangle area, List<MenuItem> visible)
        {
            var style = EffectiveLayoutStyle;
            var main = visible.Where (i => !ReferenceEquals (i, overflow_button)).ToList ();

            if (style is ToolStripLayoutStyle.Flow or ToolStripLayoutStyle.Table) {
                // Flow: preferred sizes, wrapped into rows; no overflow. Table is laid out the same way.
                StackLayoutEngine.Horizontal.Layout (area, main.Cast<ILayoutable> ());
                WrapIntoRows (main, area, grow: false);
                overflow_button?.SetBounds (0, 0, 0, 0);

                foreach (var item in main.OfType<ToolStripItem> ())
                    item.Placement = ToolStripItemPlacement.Main;

                return;
            }

            var vertical = style == ToolStripLayoutStyle.VerticalStackWithOverflow;
            var overflowed = new List<MenuItem> ();

            int Extent (MenuItem item)
            {
                var size = item.GetPreferredSize (Size.Empty);
                return vertical ? size.Height + item.Margin.Vertical : size.Width + item.Margin.Horizontal;
            }

            // A strip that has not been sized yet (a docked strip in a form that is not shown has no
            // width at all) has no capacity to measure against: nothing overflows until it does.
            var capacity = vertical ? area.Height : area.Width;

            if (CanOverflow && capacity > 0) {
                var needed = main.Sum (Extent);

                if (needed > capacity) {
                    var budget = capacity - Extent (OverflowButton);

                    // Overflow.Always items go first, whatever the room; then AsNeeded items from the
                    // trailing end until the rest fits beside the button; Never items stay put.
                    foreach (var item in main.Where (i => i is ToolStripItem { Overflow: ToolStripItemOverflow.Always }).ToList ()) {
                        overflowed.Add (item);
                        main.Remove (item);
                        needed -= Extent (item);
                    }

                    for (var i = main.Count - 1; i >= 0 && needed > budget; i--) {
                        if (main[i] is ToolStripItem { Overflow: ToolStripItemOverflow.Never })
                            continue;

                        needed -= Extent (main[i]);
                        overflowed.Insert (0, main[i]);
                        main.RemoveAt (i);
                    }
                }
            }

            MoveToOverflow (overflowed);

            if (overflowed.Count > 0)
                main.Add (OverflowButton);
            else
                overflow_button?.SetBounds (0, 0, 0, 0);

            (vertical ? StackLayoutEngine.VerticalExpand : StackLayoutEngine.HorizontalExpand).Layout (area, main.Cast<ILayoutable> ());
            PinTrailing (main, area, vertical);

            foreach (var item in main.OfType<ToolStripItem> ())
                item.Placement = ToolStripItemPlacement.Main;
        }

        // ToolStripItemAlignment.Right pins to the trailing edge: the right of a horizontal strip, the
        // bottom of a vertical one. Sizes are kept; only the position moves.
        private static void PinTrailing (List<MenuItem> main, Rectangle area, bool vertical)
        {
            var trailing = main.OfType<ToolStripItem> ().Where (i => i.Alignment == ToolStripItemAlignment.Right).ToList ();
            var edge = vertical ? area.Bottom : area.Right;

            for (var i = trailing.Count - 1; i >= 0; i--) {
                var bounds = trailing[i].Bounds;

                if (vertical) {
                    edge -= bounds.Height;
                    trailing[i].SetBounds (bounds.X, edge, bounds.Width, bounds.Height);
                } else {
                    edge -= bounds.Width;
                    trailing[i].SetBounds (edge, bounds.Y, bounds.Width, bounds.Height);
                }
            }
        }

        /// <summary>The direction an item's text is drawn in: its own, or the strip's when it inherits.</summary>
        internal ToolStripTextDirection TextDirectionFor (MenuItem item)
            => item is ToolStripItem { TextDirection: not ToolStripTextDirection.Inherit } strip_item
                ? strip_item.TextDirection
                : TextDirection == ToolStripTextDirection.Inherit ? ToolStripTextDirection.Horizontal : TextDirection;

        // A vertical strip is as wide as its widest item and as tall as its items stacked (W6).
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            if (EffectiveLayoutStyle != ToolStripLayoutStyle.VerticalStackWithOverflow)
                return base.GetPreferredSizeCore (proposedSize);

            var width = 0;
            var height = 0;

            foreach (MenuItem item in Items) {
                var size = item.GetPreferredSize (Size.Empty);

                width = Math.Max (width, size.Width + item.Margin.Horizontal);
                height += size.Height + item.Margin.Vertical;
            }

            return new Size (Math.Max (Width, width + Padding.Horizontal), Math.Max (0, height + Padding.Vertical + GripBandWidth));
        }

        /// <summary>Returns the item at the given point within this strip, or null.</summary>
        /// <param name="point">
        /// A point in this strip's client coordinates, in LOGICAL units -- the same space
        /// <see cref="MouseEventArgs.Location"/> arrives in, and the space
        /// <see cref="MenuBase.GetItemAtLocation"/> takes.
        /// </param>
        /// <remarks>
        /// Tests <see cref="MenuItem.Bounds"/>, the rectangle layout actually placed the item at.
        /// This used to build its rectangle from <c>Bounds.Location</c> paired with
        /// <see cref="ToolStripItem.Size"/>, which is a different store: Size is the size the
        /// application REQUESTED, and stays <c>0, 0</c> on an item that was never explicitly sized --
        /// so the hit rectangle was empty and this returned null for every point. Where Size had been
        /// set it disagreed with the laid-out extent whenever AutoSize was on, which is the default.
        /// </remarks>
        public ToolStripItem? GetItemAt (Point point)
        {
            foreach (ToolStripItem item in Items) {
                if (!item.Available)
                    continue;
                if (item.Bounds.Contains (point))
                    return item;
            }
            return null;
        }

        /// <inheritdoc cref="GetItemAt(Point)"/>
        public ToolStripItem? GetItemAt (int x, int y) => GetItemAt (new Point (x, y));

        /// <inheritdoc/>
        /// <remarks>
        /// <see cref="ShowItemToolTips"/> and <see cref="ToolStripItem.ToolTipText"/>, both stored and
        /// read by nothing before (<c>LST-59</c>).
        /// </remarks>
        internal override string? GetToolTipText (Point location)
        {
            if (!ShowItemToolTips || GetItemAt (location) is not { } item)
                return null;

            // AutoToolTip: an item with no ToolTipText of its own shows its Text, as upstream does.
            return !string.IsNullOrEmpty (item.ToolTipText) ? item.ToolTipText
                : item is ToolStripItem { AutoToolTip: true } strip_item ? strip_item.Text
                : null;
        }

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
