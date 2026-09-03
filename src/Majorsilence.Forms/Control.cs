using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents the base class for all Controls.
    /// </summary>
    public partial class Control : Component, ILayoutable, IArrangedElement, IDisposable, IWin32Window, IBindableComponent
    {
        /// <summary>Win32 HWND compatibility -- Majorsilence.Forms has no HWND, always IntPtr.Zero.
        /// Implemented so ported WinForms code like `MessageBox.Show(this, ...)` (passing a
        /// Control as an IWin32Window owner) keeps compiling unmodified.</summary>
        System.IntPtr IWin32Window.Handle => System.IntPtr.Zero;


        // Control instance members
        //
        // Note: Do not add anything to this list unless absolutely necessary.
        //       Every control on a form has the overhead of all of these
        //       variables!
        private Control? parent;
        private States _state = States.Visible | States.Enabled | States.TabStop | States.CausesValidation | States.IsDirty;
        private ExtendedStates _extendedState;
        private ControlBehaviors behaviors = ControlBehaviors.Selectable | ControlBehaviors.ReceivesMouseEvents;

        private int _x;
        private int _y;
        private int _width;
        private int _height;

        private int tab_index = -1;
        private string text = string.Empty;
        private byte layout_suspend_count;
        // Set when our own size changes while layout is suspended; consumed by ResumeLayout to
        // re-snapshot anchored children against the display rectangle they never saw.
        private bool _resizedWhileLayoutSuspended;

        private SKBitmap? back_buffer;
        private Control? current_mouse_in;

        private bool is_captured;

        // Property store keys for properties.
        private static readonly int s_controlsCollectionProperty = PropertyStore.CreateKey ();
        private static readonly int s_contextMenuProperty = PropertyStore.CreateKey ();
        private static readonly int s_cursorProperty = PropertyStore.CreateKey ();
        private static readonly int s_namePropertyProperty = PropertyStore.CreateKey ();
        private static readonly int s_tagProperty = PropertyStore.CreateKey ();

        /// <summary>
        /// Initializes a new instance of the Control class.
        /// </summary>
        public Control ()
        {
            // We baked the "default default" margin and min size into CommonProperties
            // so that in the common case the PropertyStore would be empty.  If, however,
            // someone overrides these Default* methods, we need to write the default
            // value into the PropertyStore in the ctor.

            if (DefaultMargin != CommonProperties.DefaultMargin)
                Margin = DefaultMargin;

            if (DefaultMinimumSize != CommonProperties.DefaultMinimumSize)
                MinimumSize = DefaultMinimumSize;

            if (DefaultMaximumSize != CommonProperties.DefaultMaximumSize)
                MaximumSize = DefaultMaximumSize;

            var default_size = DefaultSize;

            _width = default_size.Width;
            _height = default_size.Height;
        }

        /// <summary>
        ///  Assigns a new parent control. Sends out the appropriate property change
        ///  notifications for properties that are affected by the change of parent.
        /// </summary>
        internal virtual void AssignParent (Control? value)
        {
            // Adopt the parent's required scaling bits
            //if (value is not null) {
            //    RequiredScalingEnabled = value.RequiredScalingEnabled;
            //}

            // Store the old values for these properties
            var old_enabled = Enabled;
            var old_visible = Visible;

            // Update the parent
            parent = value;
            OnParentChanged (EventArgs.Empty);

            if (GetAnyDisposingInHierarchy ())
                return;

            // Compare property values with new parent to old values
            if (old_enabled != Enabled)
                OnEnabledChanged (EventArgs.Empty);

            // When a control seems to be going from invisible -> visible,
            // yet its parent is being set to null and it's not top level, do not raise OnVisibleChanged.
            var new_visible = Visible;

            if (old_visible != new_visible && !(!old_visible && new_visible && parent is null))
                OnVisibleChanged (EventArgs.Empty);

            //    if (Properties.GetObject (s_bindingManagerProperty) is null && Created) {
            //        // We do not want to call our parent's BindingContext property here.
            //        // We have no idea if us or any of our children are using data binding,
            //        // and invoking the property would just create the binding manager, which
            //        // we don't need.  We just blindly notify that the binding manager has
            //        // changed, and if anyone cares, they will do the comparison at that time.
            //        //
            //        OnBindingContextChanged (EventArgs.Empty);
            //    }

            if (Parent is not null)
                Parent.LayoutEngine.InitLayout (this, BoundsSpecified.All);
        }

        /// <summary>
        /// Gets the unscaled bottom location of the control.
        /// </summary>
        public int Bottom => _y + _height;

        /// <summary>
        /// Gets or sets the unscaled bounds of the control.
        /// </summary>
        public Rectangle Bounds {
            get => new Rectangle (_x, _y, _width, _height);
            set => SetBounds (value.Left, value.Top, value.Width, value.Height);
        }

        /// <summary>
        /// Moves this control to the front zorder.
        /// </summary>
        public void BringToFront () => UpdateZOrder ();

        /// <summary>
        /// Updates this control's position in its parent's z-order to match its current index. WinForms
        /// exposes this as the protected primitive <see cref="BringToFront"/> is built on, for a control
        /// that needs to re-sync its z-order (e.g. after reparenting itself) without the rest of what a
        /// public <c>BringToFront</c> call might imply on a derived type that overrides it.
        /// </summary>
        protected void UpdateZOrder ()
        {
            if (parent != null)
                parent.Controls.SetChildIndex (this, 0);
        }

        /// <summary>
        /// Gets a value indicating the control can receive focus.
        /// </summary>
        public bool CanSelect {
            get {
                if (!behaviors.HasFlag (ControlBehaviors.Selectable))
                    return false;

                var parent = (Control?)this;

                while (parent != null) {
                    if (!parent.Visible || !parent.Enabled)
                        return false;

                    parent = parent.Parent;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the control is currently getting system mouse events.
        /// </summary>
        /// <remarks>
        /// Capture is EXCLUSIVE, as it is in WinForms: taking it hands it over, and whoever held it
        /// stops receiving mouse events and is told so through <see cref="OnMouseCaptureChanged"/>.
        ///
        /// This used to flag the control *and every ancestor* independently, with no notion of a single
        /// holder, so handing capture over did nothing: the previous holder kept its flag and
        /// <see cref="ControlCollection.FindCapturedChild"/> — which walks to the deepest capturing
        /// control — kept routing every move straight back to it. That is precisely how WinForms code
        /// takes over a drag it started: the control captures on mouse-down, then hands capture to the
        /// form, after which moves arrive at the form instead. Left broken, DockPanelSuite's tab drag
        /// re-entered BeginDrag on every single mouse move, each one building another full-screen drag
        /// outline window, until ~30 of them covered the screen and the app looked hung.
        ///
        /// Ancestors are no longer flagged because the getter already reports true for them (a parent
        /// aggregates its subtree through <see cref="ControlCollection.AnyCaptured"/>), so the routing
        /// walk still finds the holder — while `is_captured` now means "this control IS the holder",
        /// which is the question the mouse dispatch in RaiseMouseMove/RaiseMouseUp actually asks.
        /// </remarks>
        public bool Capture {
            get => is_captured || Controls.AnyCaptured ();
            set {
                if (value) {
                    var previous = s_captureHolder;

                    // Claim it first: releasing the previous holder runs this same setter, and it must
                    // not clear the holder we are in the middle of installing.
                    s_captureHolder = this;

                    if (previous is not null && !ReferenceEquals (previous, this))
                        previous.Capture = false;

                    if (!is_captured) {
                        is_captured = true;
                        OnMouseCaptureChanged (EventArgs.Empty);
                    }
                } else {
                    if (ReferenceEquals (s_captureHolder, this))
                        s_captureHolder = null;

                    if (is_captured) {
                        is_captured = false;
                        OnMouseCaptureChanged (EventArgs.Empty);
                    }
                }
            }
        }

        // The single control currently holding capture. Static because capture is a property of the
        // pointer, not of a window, and these apps run one UI thread -- the same assumption
        // Application's message filter list is built on.
        private static Control? s_captureHolder;

        /// <summary>The control currently holding the mouse capture, or null. See <see cref="Capture"/>.</summary>
        /// <remarks>
        /// Self-healing: a control that was disposed while holding the capture releases it here rather
        /// than swallowing every mouse event in the application for the rest of the session. Disposal
        /// clears this too — this covers a holder torn down by some route that never ran Dispose.
        /// </remarks>
        internal static Control? CaptureHolder {
            get {
                if (s_captureHolder is { IsDisposed: true })
                    s_captureHolder = null;

                return s_captureHolder;
            }
        }

        /// <summary>The top of this control's parent chain — the adapter of the window it lives in.</summary>
        internal Control RootControl {
            get {
                var root = this;
                while (root.Parent is { } parent)
                    root = parent;
                return root;
            }
        }

        /// <summary>
        ///  Searches the parent/owner tree for bottom to find any instance
        ///  of toFind in the parent/owner tree.
        /// </summary>
        internal static void CheckParentingCycle (Control bottom, Control toFind)
        {
            ControlAdapter? lastOwner = null;
            Control? lastParent = null;

            for (var ctl = bottom; ctl is not null; ctl = ctl.Parent) {
                lastParent = ctl;

                if (ctl == toFind)
                    throw new ArgumentException (SR.CircularOwner);
            }

            if (lastParent is not null) {
                if (lastParent is ControlAdapter form) {
                    lastOwner = form;

                    if (form == toFind)
                        throw new ArgumentException (SR.CircularOwner);
                }
            }

            if (lastOwner?.Parent is not null)
                CheckParentingCycle (lastOwner.Parent, toFind);
        }

        /// <summary>
        /// Gets the scaled bounds of the control's canvas minus any borders.
        /// </summary>
        public virtual Rectangle ClientRectangle {
            get {
                // TODO: We should be scaling the Border as well
                var x = CurrentStyle.Border.Left.GetWidth ();
                var y = CurrentStyle.Border.Top.GetWidth ();
                var w = CurrentStyle.Border.Right.GetWidth () + x;
                var h = CurrentStyle.Border.Bottom.GetWidth () + y;

                var bounds = GetScaledBounds (Bounds, ScaleFactor, BoundsSpecified.All);

                return new Rectangle (x, y, bounds.Width - w, bounds.Height - h);
            }
        }

        /// <summary>
        /// Gets or sets the size of the control's client area.
        /// </summary>
        /// <remarks>
        /// The setter grows <see cref="Size"/> by whatever the border currently takes, which is what makes
        /// <c>ClientSize = contentSize</c> mean the same thing here as in WinForms: a caller that has
        /// measured its content and wants exactly that much room inside the border gets it, rather than
        /// losing the border's width off the inside. It was read-only before, so those assignments -- the
        /// normal way a dialog sizes itself to its content -- did not compile.
        /// </remarks>
        public Size ClientSize {
            get => ClientRectangle.Size;
            set {
                var client = ClientRectangle.Size;
                var border = new Size (Width - client.Width, Height - client.Height);

                Size = new Size (value.Width + border.Width, value.Height + border.Height);
            }
        }

        /// <summary>
        /// Gets a value indicating if the specified control is parented to this control or any of its children.
        /// </summary>
        public bool Contains (Control control)
        {
            var start = (Control?)control;

            // Is control one of our children or grandchildren
            while (start != null) {
                start = start.Parent;

                if (start == this)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets or sets the context menu that will be shown for the control.
        /// </summary>
        public virtual ContextMenu? ContextMenu {
            get => (ContextMenu?)Properties.GetObject (s_contextMenuProperty);
            set {
                if (value != ContextMenu) {
                    Properties.SetObject (s_contextMenuProperty, value);
                    OnContextMenuChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the collection of controls contained by the control.
        /// </summary>
        public ControlCollection Controls {
            get {
                var collection = (ControlCollection?)Properties.GetObject (s_controlsCollectionProperty);

                if (collection is null) {
                    collection = CreateControlsInstance ();
                    Properties.SetObject (s_controlsCollectionProperty, collection);
                }

                return collection;
            }
        }

        /// <summary>
        /// This doesn't do much because we don't have native window handles, but having
        /// the created state allows us to avoid some stuff like layouts if the controls
        /// aren't actually being used yet.
        /// </summary>
        public void CreateControl ()
        {
            // Don't run this more than once
            if (Created)
                return;

            SetState (States.Created, true);

            // Majorsilence.Forms has no HWND, but this is the equivalent moment: the control has
            // just gone live, which is what WinForms code hooking HandleCreated is waiting for.
            OnHandleCreated (EventArgs.Empty);

            // Create an array copy in case the collection changes
            foreach (var child in Controls.GetAllControls ().ToArray ())
                child.CreateControl ();

            OnCreateControl ();
        }

        /// <summary>
        ///  Constructs the new instance of the Controls collection objects. Subclasses
        ///  should not call base.CreateControlsInstance.
        /// </summary>
        [EditorBrowsable (EditorBrowsableState.Advanced)]
        protected virtual ControlCollection CreateControlsInstance ()
        {
            return new ControlCollection (this);
        }

        /// <summary>
        ///  Indicates whether the control has been created. This property is read-only.
        /// </summary>
        [Browsable (false)]
        [EditorBrowsable (EditorBrowsableState.Advanced)]
        public bool Created => GetState (States.Created);

        /// <summary>
        /// Gets the current style of this control instance.
        /// </summary>
        public virtual ControlStyle CurrentStyle => IsHovering && Enabled ? StyleHover : Style;

        /// <summary>
        /// Gets or sets the mouse cursor to be shown when the mouse is over the control.
        /// </summary>
        public Cursor Cursor {
            get {
                if (GetState (States.UseWaitCursor))
                    return Cursors.Wait;

                if (override_cursor is not null)
                    return override_cursor;

                if (Properties.GetObject (s_cursorProperty) is Cursor cursor)
                    return cursor;

                return Parent?.Cursor ?? DefaultCursor;
            }
            set {
                var old_cursor = Properties.GetObject (s_cursorProperty) as Cursor;

                if (old_cursor != value) {
                    Properties.SetObject (s_cursorProperty, value);
                    OnCursorChanged (EventArgs.Empty);
                }
            }
        }

        private Cursor? override_cursor;

        /// <summary>
        /// Gets or sets a cursor that takes priority over <see cref="Cursor"/> while it is set, without
        /// disturbing the configured value underneath. A control shows a different cursor over part of
        /// its own surface this way -- <c>LinkLabel</c> is the built-in example, switching to a hand
        /// cursor over link text and back to <c>null</c> off it -- rather than by writing and restoring
        /// <see cref="Cursor"/> itself, which a caller watching <see cref="CursorChanged"/> would then
        /// see fire for a value the control never really adopted.
        /// </summary>
        protected Cursor? OverrideCursor {
            get => override_cursor;
            set {
                if (override_cursor != value) {
                    override_cursor = value;
                    OnCursorChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the default cursor.
        /// </summary>
        protected virtual Cursor DefaultCursor => Cursor.Default;

        /// <summary>
        /// Gets the default margin of the control.
        /// </summary>
        protected virtual Padding DefaultMargin => CommonProperties.DefaultMargin;

        /// <summary>
        /// Gets the default maximum size of the control.
        /// </summary>
        protected virtual Size DefaultMaximumSize => CommonProperties.DefaultMaximumSize;

        /// <summary>
        /// Gets the default minimum size of the control.
        /// </summary>
        protected virtual Size DefaultMinimumSize => CommonProperties.DefaultMinimumSize;

        /// <summary>
        /// Gets the default padding of the control.
        /// </summary>
        protected virtual Padding DefaultPadding => Padding.Empty;

        /// <summary>
        /// Gets the default size of the control.
        /// </summary>
        protected virtual Size DefaultSize => Size.Empty;

        /// <summary>
        /// Gets the default style for all controls of this type.
        /// </summary>
        public static readonly ControlStyle DefaultStyle = new ControlStyle (null,
            (style) => {
                style.ForegroundColor = Theme.ForegroundColor;
                style.BackgroundColor = Theme.BackgroundColor;
                style.Font = Theme.UIFont;
                style.FontSize = Theme.FontSize;
                style.Border.Radius = 0;
                style.Border.Color = Theme.BorderLowColor;
                style.Border.Width = 0;
            });

        /// <summary>
        /// Gets the default style for all controls of this type when the user is hovering over it.
        /// </summary>
        public static readonly ControlStyle DefaultStyleHover = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Removes focus from the control.
        /// </summary>
        internal void Deselect ()
        {
            Selected = false;
            OnDeselected (EventArgs.Empty);

            Invalidate ();
        }

        /// <summary>
        /// Gets the DPI of the current monitor.
        /// </summary>
        public int DeviceDpi => (int)((FindWindow ()?.Scaling ?? 1) * 96);

        /// <summary>
        /// Gets the unscaled bounds of the displayed control.
        /// </summary>
        public virtual Rectangle DisplayRectangle {
            get {
                // TODO
                var x = CurrentStyle.Border.Left.GetWidth ();
                var y = CurrentStyle.Border.Top.GetWidth ();
                var w = CurrentStyle.Border.Right.GetWidth () + x;
                var h = CurrentStyle.Border.Bottom.GetWidth () + y;

                return new Rectangle (x, y, _width - w, _height - h);
            }
        }

        /// <summary>
        ///  Indicates whether the control is in the process of being disposed. This
        ///  property is read-only.
        /// </summary>
        [Browsable (false)]
        [EditorBrowsable (EditorBrowsableState.Advanced)]
        public bool Disposing => GetState (States.Disposing);

        /// <summary>
        /// Gets or sets whether the control can be interacted with.
        /// </summary>
        public bool Enabled {
            get {
                // If we aren't enabled, that's easy
                if (!GetState (States.Enabled))
                    return false;

                // If we don't have a Parent, then we're enabled
                if (Parent is null)
                    return true;

                // If our Parent isn't enabled, neither are we
                return Parent.Enabled;
            }
            set {
                var old_value = Enabled;
                SetState (States.Enabled, value);

                // See if the computed Enabled actually changed
                if (old_value != value) {
                    if (!value)
                        SelectNextIfFocused ();

                    OnEnabledChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the ControlAdapter the control is parented to.
        /// </summary>
        internal ControlAdapter? FindAdapter ()
        {
            if (this is ControlAdapter adapter)
                return adapter;

            return Parent?.FindAdapter ();
        }

        /// <summary>
        /// Gets the Form that the control is parented to.
        /// </summary>
        public virtual Form? FindForm ()
        {
            if (this is ControlAdapter adapter && adapter.ParentForm is Form f)
                return f;

            return Parent?.FindForm ();
        }

        /// <summary>
        /// Gets the Window that the control is parented to. (Different from FindForm because it may return a PopupWindow.)
        /// </summary>
        internal WindowBase? FindWindow ()
        {
            if (this is ControlAdapter adapter && adapter.ParentForm is WindowBase w)
                return w;

            return Parent?.FindWindow ();
        }

        /// <summary>
        /// Gets whether this control currently has keyboard focus.
        /// </summary>
        public virtual bool Focused => Selected;

        /// <summary>
        /// Releases the back buffer.
        /// </summary>
        private void FreeBackBuffer ()
        {
            if (back_buffer != null) {
                back_buffer.Dispose ();
                back_buffer = null;
            }
        }

        internal bool GetAnyDisposingInHierarchy ()
        {
            var up = this;

            while (up is not null) {
                if (up.Disposing)
                    return true;

                up = up.parent;
            }

            return false;
        }

        /// <summary>
        /// The control's existing back buffer, or null when it has none yet. Unlike
        /// <see cref="GetBackBuffer"/> this never creates or replaces one, which is what a caller wanting
        /// to READ the pixels already painted there needs (see
        /// <c>PaintTransparentBackground</c>): rebuilding the buffer would discard them.
        /// </summary>
        internal SKBitmap? BackBufferPixels => back_buffer;

        /// <summary>
        /// Gets or creates a back buffer for rendering the control.
        /// </summary>
        internal SKBitmap GetBackBuffer ()
        {
            if (back_buffer is null || back_buffer.Width != ScaledSize.Width || back_buffer.Height != ScaledSize.Height) {
                FreeBackBuffer ();
                back_buffer = new SKBitmap (ScaledSize.Width, ScaledSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                SetState (States.IsDirty, true);
            }

            return back_buffer;
        }

        /// <summary>
        ///  Returns the closest ContainerControl in the control's chain of parent controls
        ///  and forms.
        /// </summary>
        public IContainerControl? GetContainerControl ()
        {
            var c = this;

            // Refer to IsContainerControl property for more details.
            if (c is not null && IsContainerControl)
                c = c.Parent;

            while (c is not null && !IsFocusManagingContainerControl (c))
                c = c.Parent;

            return (IContainerControl?)c;
        }

        /// <summary>
        /// Gets behavior flag value.
        /// </summary>
        protected internal bool GetControlBehavior (ControlBehaviors behavior) => behaviors.HasFlag (behavior);

        /// <summary>
        ///  Retrieves the current value of the specified bit in the control's state2.
        /// </summary>
        private protected bool GetExtendedState (ExtendedStates flag) => (_extendedState & flag) != 0;

        internal virtual Control? GetFirstChildControlInTabOrder (bool forward, bool includeImplicit)
        {
            Control? found = null;

            var controls = Controls.GetAllControls (includeImplicit).ToArray ();

            if (forward) {
                for (var c = 0; c < controls.Length; c++) {
                    if (found == null || found.TabIndex > controls[c].TabIndex)
                        found = controls[c];
                }
            } else {
                // Cycle through the controls in reverse z-order looking for the one with the highest
                // tab index.
                for (var c = controls.Length - 1; c >= 0; c--) {
                    if (found == null || found.TabIndex < controls[c].TabIndex)
                        found = controls[c];
                }
            }

            return found;
        }

        /// <summary>
        /// Gets the next control in tab order.
        /// </summary>
        /// <param name="start">The control to start from.</param>
        /// <param name="forward">True to get the next control, false to get the previous control.</param>
        public Control? GetNextControl (Control? start, bool forward = true)
            => GetNextControl (start, forward, false);

        // Ported from MS Winforms
        private Control? GetNextControl (Control? start, bool forward, bool includeImplicit)
        {
            if (start is null || !Contains (start))
                start = this;

            if (forward) {
                if (start.Controls.GetAllControls (includeImplicit).Any () && (start == this || !IsFocusManagingContainerControl (start))) {
                    var found = start.GetFirstChildControlInTabOrder (true, includeImplicit);

                    if (found != null)
                        return found;
                }

                while (start != this) {
                    var target_index = start.TabIndex;
                    var hit_control = false;
                    Control? found = null;

                    var p = start.Parent;

                    // Cycle through the controls in z-order looking for the one with the next highest
                    // tab index.  Because there can be dups, we have to start with the existing tab index and
                    // remember to exclude the current control.
                    var parent_controls = p?.Controls.GetAllControls (includeImplicit).ToArray ();
                    var parent_control_count = parent_controls?.Length ?? 0;

                    for (var c = 0; c < parent_control_count; c++) {
                        // The logic for this is a bit lengthy, so I have broken it into separate
                        // clauses:

                        // We are not interested in ourself.
                        if (parent_controls![c] != start) {

                            // We are interested in controls with >= tab indexes to ctl.  We must include those
                            // controls with equal indexes to account for duplicate indexes.
                            if (parent_controls![c].TabIndex >= target_index) {
                                // Check to see if this control replaces the "best match" we've already found.
                                if (found is null || found.TabIndex > parent_controls![c].TabIndex) {
                                    // Finally, check to make sure that if this tab index is the same as ctl,
                                    // that we've already encountered ctl in the z-order.  If it isn't the same,
                                    // than we're more than happy with it.
                                    if (parent_controls![c].TabIndex != target_index || hit_control)
                                        found = parent_controls![c];
                                }
                            }
                        } else {
                            // We track when we have encountered "ctl".  We never want to select ctl again, but
                            // we want to know when we've seen it in case we find another control with the same tab index.
                            hit_control = true;
                        }
                    }

                    if (found != null)
                        return found;

                    start = start.Parent!;
                }
            } else {

                if (start != this) {
                    var target_index = start.TabIndex;
                    var hit_control = false;
                    Control? found = null;

                    var p = start.Parent;

                    // Cycle through the controls in reverse z-order looking for the next lowest tab index.  We must
                    // start with the same tab index as ctl, because there can be dups.
                    var parent_controls = p?.Controls.GetAllControls (includeImplicit).ToArray ();
                    var parent_control_count = parent_controls?.Length ?? 0;

                    for (var c = parent_control_count - 1; c >= 0; c--) {
                        // The logic for this is a bit lengthy, so I have broken it into separate
                        // clauses:

                        // We are not interested in ourself.
                        if (parent_controls![c] != start) {
                            // We are interested in controls with <= tab indexes to ctl.  We must include those
                            // controls with equal indexes to account for duplicate indexes.
                            if (parent_controls![c].TabIndex <= target_index) {
                                // Check to see if this control replaces the "best match" we've already found.
                                if (found is null || found.TabIndex < parent_controls![c].TabIndex) {
                                    // Finally, check to make sure that if this tab index is the same as ctl,
                                    // that we've already encountered ctl in the z-order.  If it isn't the same,
                                    // than we're more than happy with it.
                                    if (parent_controls![c].TabIndex != target_index || hit_control)
                                        found = parent_controls![c];
                                }
                            }
                        } else {
                            // We track when we have encountered "ctl".  We never want to select ctl again, but
                            // we want to know when we've seen it in case we find another control with the same tab index.
                            hit_control = true;
                        }
                    }

                    // If we were unable to find a control we should return the control's parent.  
                    // However, if that parent is us, return NULL.
                    if (found != null)
                        start = found;
                    else
                        return p == this ? null : p;
                }

                // We found a control.  Walk into this control to find the proper sub control within it to select.
                var control_controls = start.Controls.GetAllControls (includeImplicit).ToArray ();

                while (control_controls.Length > 0 && (start == this || !IsFocusManagingContainerControl (start))) {
                    var found = start.GetFirstChildControlInTabOrder (false, includeImplicit);

                    if (found != null) {
                        start = found;
                        control_controls = start.Controls.GetAllControls (includeImplicit).ToArray ();
                    } else {
                        break;
                    }
                }

            }
            return start == this ? null : start;
        }

        /// <summary>
        /// Gets the position of the Control relative to the Form. (Differs from normal when
        /// the Control is parented to other controls.
        /// </summary>
        internal Point GetPositionInForm ()
        {
            var p = Location;
            var parent = Parent;

            while (parent is not null && parent is not ControlAdapter) {
                p.Offset (parent.Location.X, parent.Location.Y);
                parent = parent.Parent;
            }

            return p;
        }

        /// <summary>
        /// Scales bounds by a specified factor.
        /// </summary>
        protected virtual Rectangle GetScaledBounds (Rectangle bounds, SizeF factor, BoundsSpecified specified)
        {
            var dx = factor.Width;
            var dy = factor.Height;

            var left = (int)Math.Truncate (bounds.X * dx);
            var top = (int)Math.Truncate (bounds.Y * dy);

            var sx = bounds.X;
            var sy = bounds.Y;
            var sw = bounds.Width;
            var sh = bounds.Height;

            // Scale the control location (unless this is the top level adapter)
            if (FindAdapter () != this) {
                if (specified.HasFlag (BoundsSpecified.X))
                    sx = left;
                if (specified.HasFlag (BoundsSpecified.Y))
                    sy = top;
            }

            // Don't just scale the Width/Height as it might round incorrectly
            if (specified.HasFlag (BoundsSpecified.Width)) {
                var right = (int)Math.Truncate ((bounds.Right) * dx);
                sw = right - left;
            }

            if (specified.HasFlag (BoundsSpecified.Height)) {
                var bottom = (int)Math.Truncate ((bounds.Bottom) * dy);
                sh = bottom - top;
            }

            return new Rectangle (sx, sy, sw, sh);
        }

        /// <summary>
        ///  Retrieves the current value of the specified bit in the control's state.
        /// </summary>
        private protected bool GetState (States flag) => (_state & flag) != 0;

        /// <summary>
        /// Gets a value indicating if control contains any child controls.
        /// </summary>
        public bool HasChildren => ((Properties.GetObject (s_controlsCollectionProperty) as ControlCollection)?.Count ?? 0) > 0;

        /// <summary>
        /// Gets or sets the unscaled height of the control.
        /// </summary>
        public int Height {
            get => _height;
            set => SetBounds (_x, _y, _width, value, BoundsSpecified.Height);
        }

        /// <summary>
        /// Hide this control from the user.
        /// </summary>
        public void Hide ()
        {
            Visible = false;
        }

        /// <summary>
        /// Marks the entire control as needing to be redrawn.
        /// </summary>
        public void Invalidate () => Invalidate (Bounds);

        /// <summary>Marks the control as needing to be redrawn. Mirrors WinForms
        /// Invalidate(bool invalidateChildren); children repaint with the control here regardless.</summary>
        public void Invalidate (bool invalidateChildren) => Invalidate (Bounds);

        /// <summary>
        /// Marks the specified portion of the control as needing to be redrawn.
        /// </summary>
        /// <param name="rectangle">The portion of the control to be redrawn.</param>
        public void Invalidate (Rectangle rectangle)
        {
            if (!Created)
                return;

            SetState (States.IsDirty, true);

            FindWindow ()?.Invalidate (rectangle);

            OnInvalidated (new InvalidateEventArgs (rectangle));
        }

        /// <summary>
        /// Renders this control and all its visible descendants into a throwaway bitmap,
        /// populating the TextBlock layout cache and pre-rendering child back buffers.
        /// Call on a hidden control before adding it to the form to eliminate first-show lag.
        /// </summary>
        public void PreWarm (float scaling)
        {
            var w = (int)(_width * scaling);
            var h = (int)(_height * scaling);

            if (w <= 0 || h <= 0)
                return;

            var info = new SKImageInfo (w, h, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var bitmap = new SKBitmap (info);
            using var canvas = new SKCanvas (bitmap);
            var args = new PaintEventArgs (info, canvas, scaling);
            RaisePaintBackground (args);
            RaisePaint (args);
        }

        /// <summary>
        /// Is the mouse currently over the control.
        /// </summary>
        public bool IsHovering {
            get => GetState (States.IsHovering);
            private set => SetState (States.IsHovering, value);
        }

        /// <summary>
        ///  Determines if <paramref name="charCode"/> is the mnemonic character in <paramref name="text"/>.
        ///  The mnemonic character is the character immediately following the first
        ///  instance of "&amp;" in text
        /// </summary>
        public static bool IsMnemonic (char charCode, string text)
        {
            // Special case handling:
            if (charCode == '&')
                return false;

            if (text is not null) {
                var pos = -1; // start with -1 to handle double &'s
                var c2 = char.ToUpper (charCode, CultureInfo.CurrentCulture);
                for (; ; )
                {
                    if (pos + 1 >= text.Length)
                        break;

                    pos = text.IndexOf ('&', pos + 1) + 1;

                    if (pos <= 0 || pos >= text.Length)
                        break;

                    var c1 = char.ToUpper (text[pos], CultureInfo.CurrentCulture);

                    if (c1 == c2 || char.ToLower (c1, CultureInfo.CurrentCulture) == char.ToLower (c2, CultureInfo.CurrentCulture))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets or sets the unscaled left boundary of the control.
        /// </summary>
        public int Left {
            get => _x;
            set => SetBounds (value, _y, _width, _height, BoundsSpecified.X);
        }

        /// <summary>
        /// Gets or sets the unscaled location of the control.
        /// </summary>
        public Point Location {
            get => new Point (_x, _y);
            set => SetBounds (value.X, value.Y, _width, _height, BoundsSpecified.Location);
        }

        /// <summary>
        /// Converts an unscaled value to a scaled value.
        /// </summary>
        public int LogicalToDeviceUnits (int value)
        {
            return DpiHelper.LogicalToDeviceUnits (value, DeviceDpi);
        }

        /// <summary>
        /// Converts a scaled (device) value back to an unscaled (logical) one.
        /// </summary>
        /// <remarks>
        /// The inverse of <see cref="LogicalToDeviceUnits(int)"/>, for results that come back from
        /// something measured at the device font size -- text metrics, mainly -- and then have to be
        /// stored somewhere logical such as a control's Bounds.
        /// </remarks>
        public int DeviceToLogicalUnits (int value)
        {
            var dpi = DeviceDpi;
            return dpi <= 0 || dpi == DpiHelper.LogicalDpi
                ? value
                : (int)Math.Round (value * DpiHelper.LogicalDpi / dpi);
        }

        /// <inheritdoc cref="DeviceToLogicalUnits(int)"/>
        public Size DeviceToLogicalUnits (Size value)
            => new Size (DeviceToLogicalUnits (value.Width), DeviceToLogicalUnits (value.Height));

        /// <summary>
        /// Converts an unscaled Padding to a scaled Padding.
        /// </summary>
        public Padding LogicalToDeviceUnits (Padding value)
        {
            return DpiHelper.LogicalToDeviceUnits (value, DeviceDpi);
        }

        /// <summary>
        /// Converts an unscaled Size to a scaled Size.
        /// </summary>
        public Size LogicalToDeviceUnits (Size value)
        {
            return DpiHelper.LogicalToDeviceUnits (value, DeviceDpi);
        }

        /// <summary>
        /// Internal control (like a scrollbar) that should not show up in Controls for a user.
        /// </summary>
        internal bool ImplicitControl {
            get => GetState (States.IsImplicitControl);
            set => SetState (States.IsImplicitControl, value);
        }

        /// <summary>
        /// Gets or sets a user specified name for the control.
        /// The name can be used as a key into the ControlCollection.
        /// </summary>
        [Browsable (false)]
        public string Name {
            get {
                var name = (string?)Properties.GetObject (s_namePropertyProperty);

                if (string.IsNullOrEmpty (name))
                    name = Site?.Name;

                return name ?? string.Empty;
            }
            set {
                var s = string.IsNullOrEmpty (value) ? null : value;
                Properties.SetObject (s_namePropertyProperty, s);
            }
        }

        /// <summary>
        /// Whether the control needs to be repainted.
        /// </summary>
        internal bool NeedsPaint => GetState (States.IsDirty) || Controls.AnyNeedsPaint ();

        /// <summary>
        /// The full control canvas.
        /// </summary>
        internal virtual Rectangle NonClientRectangle {
            get {
                var bounds = GetScaledBounds (Bounds, ScaleFactor, BoundsSpecified.All);
                return new Rectangle (0, 0, bounds.Width, bounds.Height);
            }
        }

        /// <summary>
        ///  Called when a child is about to resume its layout.  The default implementation
        ///  calls OnChildLayoutResuming on the parent.
        /// </summary>
        internal virtual void OnChildLayoutResuming (Control child, bool performLayout)
        {
            Parent?.OnChildLayoutResuming (child, performLayout);
        }

        /// <summary>
        /// Raises the Click event.
        /// </summary>
        /// <remarks>
        /// Takes <see cref="EventArgs"/>, as WinForms does, so <c>protected override void OnClick
        /// (EventArgs e)</c> ports unchanged. Click handling that needs the pointer position belongs in
        /// <see cref="OnMouseClick"/>, which receives the <see cref="MouseEventArgs"/> and runs straight
        /// afterwards.
        /// </remarks>
        protected virtual void OnClick (EventArgs e)
        {
            (Events[s_clickEvent] as EventHandler)?.Invoke (this, e);
        }

        /// <summary>
        /// Raises the LongPress event. Mirrors <see cref="OnClick"/>'s right-click branch: opens
        /// <see cref="ContextMenu"/> if one is set, so a migrated app that already wires up
        /// <see cref="ContextMenuStrip"/> gets a touch long-press-to-open-menu for free.
        /// </summary>
        protected virtual void OnLongPress (LongPressEventArgs e)
        {
            (Events[s_longPressEvent] as EventHandler<LongPressEventArgs>)?.Invoke (this, e);

            if (ContextMenu != null)
                ContextMenu.Show (this, PointToScreen (e.Location));
        }

        /// <summary>
        ///  Raises the <see cref='ContextMenuChanged'/> event.
        /// </summary>
        protected virtual void OnContextMenuChanged (EventArgs e)
        {
            (Events[s_contextMenuChangedEvent] as EventHandler)?.Invoke (this, e);

            // ContextMenuStrip is an alias of ContextMenu here, so its changed notification is the
            // same notification.
            OnContextMenuStripChanged (e);
        }

        /// <summary>
        ///  Raises the <see cref='ControlAdded'/> event.
        /// </summary>
        protected virtual void OnControlAdded (ControlEventArgs e) => (Events[s_controlAddedEvent] as ControlEventHandler)?.Invoke (this, e);

        /// <summary>
        ///  Raises the <see cref='ControlRemoved'/> event.
        /// </summary>
        protected virtual void OnControlRemoved (ControlEventArgs e) => (Events[s_controlRemovedEvent] as ControlEventHandler)?.Invoke (this, e);

        /// <summary>
        ///  Called when the control is first created.
        /// </summary>
        [EditorBrowsable (EditorBrowsableState.Advanced)]
        protected virtual void OnCreateControl ()
        {
        }

        /// <summary>
        /// Raises the CursorChanged event.
        /// </summary>
        protected virtual void OnCursorChanged (EventArgs e) => (Events[s_cursorChangedEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Called when the control is deselected.
        /// </summary>
        protected virtual void OnDeselected (EventArgs e)
        {
            OnLostFocus (e);
        }

        /// <summary>
        /// Raises the DoubleClick event.
        /// </summary>
        protected virtual void OnDoubleClick (MouseEventArgs e)
        {
            (Events[s_doubleClickEvent] as EventHandler)?.Invoke (this, e);
            OnDoubleClick ((EventArgs)e);
            OnMouseDoubleClick (e);
        }

        /// <summary>
        /// Raises the EnabledChanged event.
        /// </summary>
        protected virtual void OnEnabledChanged (EventArgs e)
        {
            Invalidate ();

            (Events[s_enabledChangedEvent] as EventHandler)?.Invoke (this, e);

            // PERFNOTE: This is more efficient than using Foreach.  Foreach
            // forces the creation of an array subset enum each time we enumerate
            if (Properties.GetObject (s_controlsCollectionProperty) is ControlCollection collection)
                for (var i = 0; i < collection.Count; i++)
                    collection[i].OnParentEnabledChanged (e);
        }

        /// <summary>
        /// Raises the Enter event, then the GotFocus event (WinForms order).
        /// </summary>
        /// <remarks>
        /// Raises <see cref="GotFocus"/> only. <see cref="Control.Enter"/> is <em>not</em> raised here:
        /// upstream it belongs to the container, which walks the ancestor path between the old and new
        /// focused controls and raises Enter on each (<c>ContainerControl.UpdateFocusedControl</c>).
        /// <see cref="ControlAdapter"/> does that walk. Raising Enter from here instead meant only the
        /// leaf control ever heard it, so a Panel or UserControl that tracks focus entering it heard
        /// nothing at all.
        /// </remarks>
        protected virtual void OnGotFocus (EventArgs e)
            => (Events[s_gotFocusEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the Invalidated event.
        /// </summary>
        protected virtual void OnInvalidated (InvalidateEventArgs e) => (Events[s_invalidatedEvent] as InvalidateEventHandler)?.Invoke (this, e);

        /// <summary>Raises the LostFocus event.</summary>
        /// <remarks>
        /// <para>
        /// Raises <see cref="LostFocus"/> only. It used to raise <see cref="Control.Leave"/> first and
        /// then run Validating/Validated inline, which put the validation cycle in the one place where
        /// it cannot work: by the time a control has lost focus, <c>e.Cancel</c> has nothing left to
        /// prevent. That is why "cancel to keep focus in the invalid field" — the standard WinForms
        /// validation idiom — did nothing, and why the entering control's
        /// <see cref="CausesValidation"/> and the container's <c>AutoValidate</c> were never consulted.
        /// </para>
        /// <para>
        /// Both now belong to the container: <see cref="ControlAdapter"/> raises Leave on the way out,
        /// runs validation between the two controls, and can abandon the focus change when a handler
        /// cancels. Mirrors upstream, where <c>Control.OnLostFocus</c> is just the WM_KILLFOCUS
        /// notification and <c>ContainerControl</c> owns the rest.
        /// </para>
        /// </remarks>
        protected virtual void OnLostFocus (EventArgs e)
            => (Events[s_lostFocusEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>Raises the Validating event (WinForms validation cycle; fires on focus loss).</summary>
        protected virtual void OnValidating (System.ComponentModel.CancelEventArgs e) => Validating?.Invoke (this, e);

        /// <summary>Raises the Validated event (fires on focus loss when validation is not cancelled).</summary>
        protected virtual void OnValidated (EventArgs e) => Validated?.Invoke (this, e);

        // Raises Enter/GotFocus (or Leave/LostFocus) as an activation notification, without moving real
        // input focus. Used by the docking compat: selecting a document/tool tab must "enter" that window
        // so WinForms/Telerik code that loads a tab's data on the window's Enter event runs (e.g. a
        // customer form that fetches its Receivables grid when the Receivables document window is entered).
        //
        // Both halves are raised explicitly now that OnGotFocus/OnLostFocus are the focus notification
        // alone -- the Enter/Leave half moved to the container's ancestor walk, and these callers want
        // the pair.
        internal void RaiseEnter ()
        {
            OnEnter (EventArgs.Empty);
            OnGotFocus (EventArgs.Empty);
        }

        internal void RaiseLeave ()
        {
            OnLeave (EventArgs.Empty);
            OnLostFocus (EventArgs.Empty);
        }

        // The container's focus walk drives these; Enter/Leave and the validation cycle are protected,
        // so the adapter needs an internal way in.
        /// <summary>
        /// Whether this control is pure structure and should not appear as a node in the accessibility
        /// tree — its children are spliced in at its own position instead.
        /// </summary>
        /// <remarks>
        /// A form's client area is the case this exists for. It is an implementation detail of how the
        /// caption is kept out of the client region, not something a screen reader should announce or a
        /// UI Automation client should have to navigate through, and upstream has no counterpart node.
        /// </remarks>
        internal virtual bool IsAutomationTransparent => false;

        internal void RaiseEnterOnly () => OnEnter (EventArgs.Empty);

        internal void RaiseLeaveOnly () => OnLeave (EventArgs.Empty);

        internal void RaiseGotFocus () => OnGotFocus (EventArgs.Empty);

        // Routed through OnDeselected rather than OnLostFocus directly: ComboBox, TextBox and Menu
        // override it to close a drop-down or drop a text selection, and that has to keep happening.
        internal void RaiseLostFocusOnly () => OnDeselected (EventArgs.Empty);

        // The Selected flag alone, without the notifications -- the adapter raises those itself, in
        // order, and needs the flag to flip between the Leave half and the Enter half.
        internal void MarkSelected () => Selected = true;

        internal void MarkDeselected () => Selected = false;

        /// <summary>
        /// Runs this control's Validating/Validated pair, reporting whether a handler cancelled.
        /// </summary>
        internal bool RaiseValidation ()
        {
            var e = new System.ComponentModel.CancelEventArgs ();
            OnValidating (e);

            if (e.Cancel)
                return false;

            OnValidated (EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Raises the KeyDown event.
        /// </summary>
        protected virtual void OnKeyDown (KeyEventArgs e) => (Events[s_keyDownEvent] as KeyEventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the KeyPress event.
        /// </summary>
        protected virtual void OnKeyPress (KeyPressEventArgs e) => (Events[s_keyPressEvent] as KeyPressEventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the KeyUp event.
        /// </summary>
        protected virtual void OnKeyUp (KeyEventArgs e) => (Events[s_keyUpEvent] as KeyEventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the LocationChanged event.
        /// </summary>
        protected virtual void OnLocationChanged (EventArgs e)
        {
            (Events[s_locationChangedEvent] as EventHandler)?.Invoke (this, e);
            OnMove (e);
        }

        /// <summary>Raises the Move event (WinForms fires Move together with LocationChanged).</summary>
        protected virtual void OnMove (EventArgs e) => Move?.Invoke (this, e);

        /// <summary>
        /// Raises the MarginChanged event.
        /// </summary>
        protected virtual void OnMarginChanged (EventArgs e) => (Events[s_marginChangedEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the MouseDown event.
        /// </summary>
        protected virtual void OnMouseDown (MouseEventArgs e) => (Events[s_mouseDownEvent] as MouseEventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the MouseEnter event.
        /// </summary>
        protected virtual void OnMouseEnter (EventArgs e)
        {
            FindForm ()?.SetCursor (Cursor);

            if (behaviors.HasFlag (ControlBehaviors.Hoverable)) {
                IsHovering = true;
                Invalidate ();
            }

            (Events[s_mouseEnterEvent] as EventHandler)?.Invoke (this, e);

            // Majorsilence.Forms has no hover timer, so hover fires once per entry -- unless the
            // handler re-arms it with ResetMouseEventArgs.
            hover_raised = true;
            OnMouseHover (EventArgs.Empty);
        }

        // Whether MouseHover has already been raised for the current time inside this control.
        private bool hover_raised;

        /// <summary>
        /// Re-arms mouse hover so <see cref="MouseHover"/> can be raised again without the pointer
        /// having to leave and re-enter the control.
        /// </summary>
        /// <remarks>
        /// Called from a MouseHover handler that wants to keep tracking -- an auto-hide tab strip does
        /// this so moving along the strip keeps reporting which tab is under the pointer, instead of
        /// reporting only the tab the pointer first entered on.
        ///
        /// WinForms re-arms a dwell timer, so its next hover comes after the pointer rests for
        /// SystemInformation.MouseHoverTime. There is no such timer here, so the next hover comes on the
        /// next pointer move instead: sooner and more often than WinForms, but reporting the same thing.
        /// </remarks>
        protected void ResetMouseEventArgs () => hover_raised = false;

        /// <summary>
        /// Raises the MouseLeave event.
        /// </summary>
        protected virtual void OnMouseLeave (EventArgs e)
        {
            if (behaviors.HasFlag (ControlBehaviors.Hoverable)) {
                IsHovering = false;
                Invalidate ();
            }

            hover_raised = false;
            (Events[s_mouseLeaveEvent] as EventHandler)?.Invoke (this, e);
        }

        /// <summary>
        /// Raises the MouseMove event.
        /// </summary>
        protected virtual void OnMouseMove (MouseEventArgs e)
        {
            (Events[s_mouseMoveEvent] as MouseEventHandler)?.Invoke (this, e);

            // Only after ResetMouseEventArgs has re-armed it; otherwise hover stays once-per-entry.
            if (!hover_raised) {
                hover_raised = true;
                OnMouseHover (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises the MouseUp event.
        /// </summary>
        protected virtual void OnMouseUp (MouseEventArgs e) => (Events[s_mouseUpEvent] as MouseEventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the MouseWheel event.
        /// </summary>
        protected virtual void OnMouseWheel (MouseEventArgs e) => (Events[s_mouseWheelEvent] as MouseEventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the Pinch event.
        /// </summary>
        protected virtual void OnPinch (PinchGestureEventArgs e) => (Events[s_pinchEvent] as EventHandler<PinchGestureEventArgs>)?.Invoke (this, e);

        /// <summary>
        /// Raises the ScrollGesture event. <see cref="ScrollableControl"/> overrides this to pan
        /// <see cref="ScrollableControl.AutoScrollPosition"/>.
        /// </summary>
        protected virtual void OnScrollGesture (ScrollGestureEventArgs e) => (Events[s_scrollGestureEvent] as EventHandler<ScrollGestureEventArgs>)?.Invoke (this, e);

        /// <summary>
        /// Raises the Swipe event.
        /// </summary>
        protected virtual void OnSwipe (SwipeGestureEventArgs e) => (Events[s_swipeEvent] as EventHandler<SwipeGestureEventArgs>)?.Invoke (this, e);

        /// <summary>
        /// Raises the PaddingChanged event.
        /// </summary>
        protected virtual void OnPaddingChanged (EventArgs e) => (Events[s_paddingChangedEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Paints the control.
        /// </summary>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        protected virtual void OnPaint (PaintEventArgs e)
        {
            // Deliberately empty, matching WinForms. Child controls are NOT painted here -- see
            // PaintChildren, which RaisePaint calls afterwards. In WinForms every child is its own
            // HWND and repaints itself, so a derived control can override OnPaint and skip base
            // without its children disappearing; that idiom is common in ported custom-control
            // code, so the child pass must live outside anything user code can suppress.
        }

        /// <summary>
        /// Draws this control's visible children onto its surface. Runs after OnPaint and the Paint
        /// event, so children sit above whatever the control drew -- the WinForms z-order, where a
        /// child HWND always occludes its parent's client area.
        /// </summary>
        internal void PaintChildren (PaintEventArgs e)
        {
            var offset = ChildPaintOffset;

            // Bottom-to-top: WinForms z-order puts index 0 on TOP, so it must be drawn last.
            foreach (var control in Controls.GetControlsPaintOrder ()) {
                if (!control.Visible || control.Width <= 0 || control.Height <= 0)
                    continue;

                var info = new SKImageInfo (control.ScaledSize.Width, control.ScaledSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                var buffer = control.GetBackBuffer ();

                if (control.NeedsPaint) {
                    using (var canvas = new SKCanvas (buffer)) {
                        // start drawing
                        var args = new PaintEventArgs (info, canvas, Scaling);

                        control.RaisePaintBackground (args);
                        control.RaisePaint (args);

                        canvas.Flush ();
                    }
                }

                e.Canvas.DrawBitmap (buffer, offset.X + control.ScaledLeft, offset.Y + control.ScaledTop);
            }
        }

        /// <summary>
        /// Scaled offset applied to child positions when they are blitted onto this control.
        /// Zero everywhere except the ControlAdapter, which is handed the window's whole native
        /// surface and has to skip past the managed form border.
        /// </summary>
        internal virtual Point ChildPaintOffset => Point.Empty;

        /// <summary>
        /// Paints the control's background.
        /// </summary>
        protected virtual void OnPaintBackground (PaintEventArgs e)
        {
            // The ControlAdapter itself should not have a background/border -- the window paints those
            // (see WindowBase.RenderFrame) and repainting them here would cover what a Form.Paint
            // handler just drew.
            //
            // Its background IMAGE is a different matter: Form.BackgroundImage forwards to the adapter,
            // so returning before drawing it made that property stored-and-never-drawn. A splash screen
            // built the usual way -- a borderless form whose whole content is BackgroundImage -- came up
            // as a blank white rectangle over the application.
            if (this is ControlAdapter) {
                if (BackgroundImage is not null)
                    PaintBackgroundImage (e);

                return;
            }

            // Transparent controls should not draw a background or border
            if (behaviors.HasFlag (ControlBehaviors.Transparent)) {
                e.Canvas.Clear ();
                return;
            }

            e.Canvas.DrawBackground (ScaledBounds, CurrentStyle, GetEffectiveBackgroundColor ());

            if (BackgroundImage is not null)
                PaintBackgroundImage (e);

            e.Canvas.DrawBorder (ScaledBounds, CurrentStyle);
        }

        /// <summary>
        /// Resolves the control's effective background the way WinForms' ambient BackColor does: an
        /// explicit color anywhere in the control's own style chain wins; otherwise the color comes
        /// from the parent control (a Label on a dark panel paints dark), ending at the hosting
        /// window's background (WinForms ambience terminates at Form.BackColor) and only then the
        /// theme default.
        /// </summary>
        internal SKColor GetEffectiveBackgroundColor ()
        {
            var chain = CurrentStyle.TryGetBackgroundColor ();
            if (chain is not null)
                return chain.Value;

            if (Parent is not null)
                return Parent.GetEffectiveBackgroundColor ();

            // Ambience terminates at the hosting window (WinForms: Form.BackColor) -- but a fully
            // transparent window background is an embedding artifact (HostedSurface composites over
            // its host), not an ambient color; fall through to the theme for it.
            var window = FindWindow ()?.CurrentStyle.GetBackgroundColor ();
            if (window is { Alpha: > 0 })
                return window.Value;

            return Theme.BackgroundColor;
        }

        /// <summary>
        /// Resolves the control's effective foreground the way WinForms' ambient ForeColor does: an
        /// explicit color anywhere in the control's own style chain wins; otherwise the color comes
        /// from the parent control (a Button on a panel with white text draws white), ending at the
        /// hosting window (WinForms ambience terminates at Form.ForeColor) and only then the theme
        /// default. Drawing resolves through here too, so what <see cref="ForeColor"/> reports and
        /// what is painted cannot disagree.
        /// </summary>
        internal SKColor GetEffectiveForegroundColor ()
        {
            var chain = CurrentStyle.TryGetForegroundColor ();
            if (chain is not null)
                return chain.Value;

            if (Parent is not null)
                return Parent.GetEffectiveForegroundColor ();

            return FindWindow ()?.CurrentStyle.TryGetForegroundColor () ?? Theme.ForegroundColor;
        }

        /// <summary>
        /// Resolves the font used to draw/measure this control's text the way WinForms' ambient Font
        /// does: an explicit font anywhere in the control's own style chain wins; otherwise it comes
        /// from the parent chain (a Form's designer font reaches every child that never set one),
        /// falling back to the theme font at the top. Keeping DRAWING on the same ambient resolution
        /// as the <see cref="Font"/> getter is what makes designer-fixed control widths (sized for the
        /// form's 8.25pt font in GDI) hold instead of clipping at the larger theme font.
        /// </summary>
        internal SKTypeface GetEffectiveFont ()
            => CurrentStyle.TryGetFont () ?? Parent?.GetEffectiveFont () ?? Majorsilence.Forms.SystemFonts.DefaultTypeface;

        /// <summary>Companion to <see cref="GetEffectiveFont"/> for the font size (logical PIXELS).</summary>
        /// <remarks>
        /// The fallback is the default font's PIXEL size, not its point size. It used to be
        /// <c>(int) SystemFonts.DefaultFontSize</c>, which is 8.25 POINTS truncated to 8, handed
        /// straight to the renderers as a pixel size -- so every control that had no font of its own,
        /// which is almost all of them, drew its text at 8px instead of 11px. That is the tiny-caption
        /// look in a running application, and it is the same points-as-pixels confusion as GFX-25 in a
        /// third place: the measuring call, the Control.Font setter, and this fallback.
        /// </remarks>
        internal int GetEffectiveFontSize ()
            => CurrentStyle.TryGetFontSize ()
                ?? Parent?.GetEffectiveFontSize ()
                ?? (int) System.Math.Round (Majorsilence.Forms.SystemFonts.DefaultFont.PixelSize);

        /// <summary>
        /// Called when the Parent property is changed.
        /// </summary>
        protected virtual void OnParentChanged (EventArgs e) => (Events[s_parentEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Called when the Parent's Enabled property is changed.
        /// </summary>
        [EditorBrowsable (EditorBrowsableState.Advanced)]
        protected virtual void OnParentEnabledChanged (EventArgs e)
        {
            if (GetState (States.Enabled))
                OnEnabledChanged (e);
        }

        /// <summary>
        /// Called when the Parent's Visible property is changed.
        /// </summary>
        protected virtual void OnParentVisibleChanged (EventArgs e)
        {
            // Check our own local visibility flag, not the recursive Visible property.
            // By the time this cascades down from an ancestor whose effective visibility
            // just changed, the recursive Visible getter already reflects the new
            // post-change state for every descendant, which would make this guard
            // trivially match the ancestor's new state and stop the cascade at the
            // first descendant instead of propagating to deeper descendants.
            if (GetState (States.Visible))
                OnVisibleChanged (e);
        }

        /// <summary>
        ///  Retrieves our internal property storage object. If you have a property
        ///  whose value is not always set, you should store it in here to save
        ///  space.
        /// </summary>
        internal PropertyStore Properties { get; } = new PropertyStore ();

        /// <summary>
        ///  Raises the <see cref='Resize'/> event.
        /// </summary>
        [EditorBrowsable (EditorBrowsableState.Advanced)]
        protected virtual void OnResize (EventArgs e)
        {
            // A control that asked for ResizeRedraw wants the WHOLE surface repainted on every resize,
            // not just the strip a grow exposes -- what an owner-drawn control needs when its painting
            // is a function of its size (a centred caption, a border inset, a gradient).
            if (GetStyle (ControlStyles.ResizeRedraw))
                Invalidate ();

            LayoutTransaction.DoLayout (this, this, PropertyNames.Bounds);
            (Events[s_resizeEvent] as EventHandler)?.Invoke (this, e);
        }

        /// <summary>
        /// Raises the SizeChanged event.
        /// </summary>
        protected virtual void OnSizeChanged (EventArgs e)
        {
            OnResize (EventArgs.Empty);

            (Events[s_sizeChangedEvent] as EventHandler)?.Invoke (this, e);
        }

        /// <summary>
        /// Raises the TabIndexChanged event.
        /// </summary>
        protected virtual void OnTabIndexChanged (EventArgs e) => (Events[s_tabIndexChangedEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the TabStopChanged event.
        /// </summary>
        protected virtual void OnTabStopChanged (EventArgs e) => (Events[s_tabStopChangedEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Raises the TextChanged event.
        /// </summary>
        protected virtual void OnTextChanged (EventArgs e) => (Events[s_textChangedEvent] as EventHandler)?.Invoke (this, e);

        /// <summary>
        /// Called when the theme changes.
        /// </summary>
        protected internal virtual void OnThemeChanged (EventArgs e)
        {
            SetState (States.IsDirty, true);

            // Recurse so nested controls also repaint with the new theme. Each control only redraws its
            // back buffer when it (not just an ancestor) is dirty, so every descendant must be marked.
            foreach (var child in Controls.GetAllControls ())
                child.OnThemeChanged (e);
        }

        /// <summary>
        /// Raises the VisibleChanged event.
        /// </summary>
        protected virtual void OnVisibleChanged (EventArgs e)
        {
            // Becoming visible only makes a control live if it is attached to something. WinForms has
            // the same guard (SetVisibleCore creates the handle only when the parent is created), and
            // it matters because OnCreateControl is where a control reaches for its surroundings --
            // FindForm() to hook the form's events being the standard move. A control that sets its own
            // Visible in its constructor, before anything has parented it, would otherwise fire
            // OnCreateControl with no form to find and NullReference inside perfectly ordinary code.
            // Attaching later still creates it: ControlCollection.Add does that after AssignParent.
            if (Parent is not null)
                CreateControl ();

            (Events[s_visibleChangedEvent] as EventHandler)?.Invoke (this, e);

            foreach (var c in Controls.GetAllControls ())
                c.OnParentVisibleChanged (e);

            if (Visible)
                PerformLayout (this, nameof (Visible));
        }

        /// <summary>
        /// The scaled control canvas minus any borders and Padding.
        /// </summary>
        public virtual Rectangle PaddedClientRectangle {
            get {
                var client_rect = ClientRectangle;

                var x = client_rect.Left + Padding.Left;
                var y = client_rect.Top + Padding.Top;
                var w = client_rect.Width - Padding.Horizontal;
                var h = client_rect.Height - Padding.Vertical;
                return new Rectangle (x, y, w, h);
            }
        }

        /// <summary>
        /// Gets or sets the control that contains this control.
        /// </summary>
        public Control? Parent {
            get => parent;
            set {
                if (value == parent)
                    return;

                if (value == this)
                    throw new ArgumentException ("Control cannot be its own Parent.");

                if (value == null) {
                    parent?.Controls.Remove (this);
                    parent = null;
                    return;
                }

                value.Controls.Add (this);

                OnParentChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Converts a point from control coordinates to monitor coordinates.
        /// </summary>
        public Point PointToScreen (Point point)
        {
            // If this is the top, add the point to our location
            if (this is ControlAdapter) {
                var window = FindWindow ();

                if (window is null)
                    return point;

                // The CLIENT origin, not the window's -- they differ by the title bar on a window with
                // native chrome, and everything downstream (Cursor.Position, PointToClient, cross-window
                // hit tests) has to agree on one screen space.
                var window_location = window.ClientOriginOnScreen;

                // Logical in, desktop out: the accumulated offset is in logical units and the window's
                // Location is in desktop ones, so scale by the real display factor. Deliberately
                // DesktopScaling and not Scaling -- Scaling carries Application.UiScale, and zooming the
                // app must not move where its windows think they are on the desktop.
                var scale = window.DesktopScaling;
                point = new Point ((int)Math.Round (point.X * scale), (int)Math.Round (point.Y * scale));

                window_location.Offset (point);

                return window_location;
            }

            // If this isn't the top, we need to add our location to the point
            // and ask our parent to translate that. Logical, matching what callers pass in.
            point.Offset (Bounds.Location);

            // If we aren't parented to a Form, this method is pretty meaningless
            return Parent?.PointToScreen (point) ?? point;
        }

        /// <summary>
        /// Finds the correct control and calls its OnClick method.
        /// </summary>
        internal void RaiseClick (MouseEventArgs e)
        {
            // If something has the mouse captured, they get all the events
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseClick (TranslateMouseEvents (e, captured));
                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null) {
                child.RaiseClick (TranslateMouseEvents (e, child));
                return;
            }

            if (!Enabled)
                return;

            // A right-click over a control with a context menu opens the menu instead of counting as a
            // click, so this runs before either event. It lives here rather than in OnClick now that
            // OnClick takes EventArgs and has no button to test.
            if (e.Button == MouseButtons.Right && ContextMenu != null) {
                ContextMenu.Show (this, PointToScreen (e.Location));
                return;
            }

            // A control that raises its own click -- Krypton's buttons route mouse-up through a view
            // controller and call OnClick themselves -- turns the standard raise OFF with
            // ControlStyles.StandardClick, exactly to prevent the double fire this guard prevents.
            // Ignoring the style meant one click opened two of everything.
            if (!GetStyle (ControlStyles.StandardClick))
                return;

            // WinForms order: Click first, then the typed MouseClick.
            OnClick (e);
            OnMouseClick (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnDoubleClick method.
        /// </summary>
        internal void RaiseDoubleClick (MouseEventArgs e)
        {
            // If something has the mouse captured, they get all the events
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseDoubleClick (TranslateMouseEvents (e, captured));
                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseDoubleClick (TranslateMouseEvents (e, child));
            else if (Enabled && GetStyle (ControlStyles.StandardDoubleClick))   // see RaiseClick's guard
                OnDoubleClick (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnKeyDown method.
        /// </summary>
        internal void RaiseKeyDown (KeyEventArgs e)
        {
            if (this is ControlAdapter adapter) {
                // A panel-hosted form owns its own focus chain, so it has to see the key before the Tab
                // handling below -- otherwise Tab inside the hosted form would move focus among the
                // container's controls instead of cycling within the form, and the hosted form's own
                // AcceptButton/CancelButton would never fire.
                if (adapter.SelectedControl is FormHost host) {
                    e.Handled = host.ForwardKeyDown (e.KeyData);
                    return;
                }

                // Tab is NOT handled here any more: it is a dialog key, and the pre-processing chain
                // (ControlAdapter.ProcessDialogKey) runs before this method is ever reached. Claiming
                // it here meant a control that wants Tab as input -- a multiline text box with
                // AcceptsTab, a grid moving between cells -- could never receive one, because focus
                // moved before the control was asked.
                adapter.SelectedControl?.RaiseKeyDown (e);
                return;
            }

            if (Enabled) {
                OnPreviewKeyDown (new PreviewKeyDownEventArgs (e.KeyData));
                OnKeyDown (e);
            }
        }

        /// <summary>
        /// Finds the correct control and calls its OnKeyPress method.
        /// </summary>
        internal void RaiseKeyPress (KeyPressEventArgs e)
        {
            if (this is ControlAdapter adapter) {
                // Ahead of the Tab handling, for the same reason as RaiseKeyDown.
                if (adapter.SelectedControl is FormHost host) {
                    e.Handled = host.ForwardTextInput (e.KeyChar.ToString ());
                    return;
                }

                // Tab: see RaiseKeyDown. The key-down chain has already moved focus by the time a Tab
                // character arrives, so handling it again here would advance focus twice per press.
                if (e.KeyChar == 9)
                    return;

                adapter.SelectedControl?.RaiseKeyPress (e);
                return;
            }

            if (Enabled)
                OnKeyPress (e);
        }

        // Whether this control belongs to the menu that is currently open -- itself, or anything nested
        // inside it.
        //
        // The test used to be `(this as MenuBase)?.GetTopLevelMenu () != Application.ActiveMenu`, which
        // exempts the menu CONTROL but nothing hosted in it. A ToolStripControlHost puts a real control
        // inside the menu popup, and mouse-down routes to the deepest child -- so the hosted control ran
        // this check, was not a MenuBase, and closed the very menu it was sitting in. Choosing a colour
        // button in a menu dismissed the menu instead of dropping its palette.
        private bool IsWithinActiveMenu ()
        {
            if (Application.ActiveMenu is null)
                return false;

            for (Control? c = this; c is not null; c = c.Parent)
                if (c is MenuBase menu && menu.GetTopLevelMenu () == Application.ActiveMenu)
                    return true;

            return false;
        }

        /// <summary>
        /// Finds the correct control and calls its OnMouseDown method.
        /// </summary>
        internal void RaiseMouseDown (MouseEventArgs e)
        {
            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseMouseDown (TranslateMouseEvents (e, child));
            else {
                // If we're clicking on a Control that isn't part of the active menu,
                // we need to close the active menu (if any)
                if (!IsWithinActiveMenu ())
                    Application.ClosePopups (true, false);

                // If we're clicking on a Control that isn't a child of the active PopupWindow,
                // we need to close the active popup (if any)
                if (FindWindow () != Application.ActivePopupWindow)
                    Application.ClosePopups (false, true);

                if (Enabled) {
                    Select ();
                    Capture = true;
                    OnMouseDown (e);
                }
            }
        }

        /// <summary>
        /// Finds the correct control and calls its OnKeyUp method.
        /// </summary>
        internal void RaiseKeyUp (KeyEventArgs e)
        {
            if (this is ControlAdapter adapter) {
                if (adapter.SelectedControl is FormHost host) {
                    e.Handled = host.ForwardKeyUp (e.KeyData);
                    return;
                }

                adapter.SelectedControl?.RaiseKeyUp (e);
                return;
            }

            OnKeyUp (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnMouseEnter method.
        /// </summary>
        internal void RaiseMouseEnter (MouseEventArgs e)
        {
            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseMouseEnter (TranslateMouseEvents (e, child));
            else if (Enabled) {
                // OnMouseEnter takes EventArgs, as in WinForms, so the position the pointer entered at
                // is recorded here for the few consumers (ToolTip) that need to place something at it.
                LastMousePosition = e.Location;
                OnMouseEnter (e);
            }
        }

        /// <summary>
        /// The most recent pointer position, in client coordinates, seen by this control. Updated on
        /// mouse enter and move. WinForms exposes the equivalent through <c>Control.MousePosition</c>,
        /// which needs a live cursor position the backends don't provide.
        /// </summary>
        internal System.Drawing.Point LastMousePosition { get; private set; }

        /// <summary>
        /// Finds the correct control and calls its OnMouseLeave method.
        /// </summary>
        internal void RaiseMouseLeave (EventArgs e)
        {
            if (current_mouse_in != null)
                current_mouse_in.RaiseMouseLeave (e);

            current_mouse_in = null;

            if (Enabled)
                OnMouseLeave (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnMouseMove method.
        /// </summary>
        internal void RaiseMouseMove (MouseEventArgs e)
        {
            // If something has the mouse captured, they get all the events
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseMouseMove (TranslateMouseEvents (e, captured));
                return;
            }

            // No child holds the capture, but this control does. WinForms routes every move to the
            // capturing control until the button comes up -- over its own children included -- which
            // is what lets a drag that started on a container survive crossing a button sitting on it.
            // Hit-testing instead would hand the move to that button and silently end the drag: the
            // exact failure of a custom title bar with caption buttons on it.
            if (is_captured) {
                // The pointer is no longer interacting with whatever it was hovering; without this the
                // child it crossed would stay painted hot for the rest of the drag.
                if (current_mouse_in != null) {
                    current_mouse_in.RaiseMouseLeave (e);
                    current_mouse_in = null;
                }

                LastMousePosition = e.Location;

                if (Enabled)
                    OnMouseMove (e);

                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            LastMousePosition = e.Location;

            if (current_mouse_in != null && current_mouse_in != child) {
                current_mouse_in.RaiseMouseLeave (e);
                current_mouse_in = null;

                // If we are leaving a child and not entering another child,
                // we need to raise MouseEnter on this control
                if (child == null)
                    OnMouseEnter (e);
            }

            if (current_mouse_in == null && child != null)
                child.RaiseMouseEnter (TranslateMouseEvents (e, child));

            current_mouse_in = child;

            if (child != null)
                child.RaiseMouseMove (TranslateMouseEvents (e, child));
            else if (Enabled)
                OnMouseMove (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnMouseUp method.
        /// </summary>
        internal void RaiseMouseUp (MouseEventArgs e)
        {
            // If something has the mouse captured, they get all the events
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseMouseUp (TranslateMouseEvents (e, captured));
                return;
            }

            // Same rule as RaiseMouseMove: the capture holder gets the release, wherever the pointer
            // ended up. It also has to be the one that drops the capture.
            if (is_captured) {
                if (Enabled) {
                    Capture = false;
                    OnMouseUp (e);
                }

                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseMouseUp (TranslateMouseEvents (e, child));
            else {
                if (Enabled) {
                    Capture = false;
                    OnMouseUp (e);
                }
            }
        }

        /// <summary>
        /// Finds the correct control and calls its OnMouseWheel method.
        /// </summary>
        internal void RaiseMouseWheel (MouseEventArgs e)
        {
            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseMouseWheel (TranslateMouseEvents (e, child));
            else if (Enabled)
                OnMouseWheel (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnLongPress method.
        /// </summary>
        internal void RaiseLongPress (LongPressEventArgs e)
        {
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseLongPress (TranslateLongPressEvent (e, captured));
                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseLongPress (TranslateLongPressEvent (e, child));
            else if (Enabled)
                OnLongPress (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnPinch method.
        /// </summary>
        internal void RaisePinch (PinchGestureEventArgs e)
        {
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaisePinch (TranslatePinchEvent (e, captured));
                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaisePinch (TranslatePinchEvent (e, child));
            else if (Enabled)
                OnPinch (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnSwipe method.
        /// </summary>
        internal void RaiseSwipe (SwipeGestureEventArgs e)
        {
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseSwipe (TranslateSwipeEvent (e, captured));
                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null)
                child.RaiseSwipe (TranslateSwipeEvent (e, child));
            else if (Enabled)
                OnSwipe (e);
        }

        /// <summary>
        /// Finds the correct control and calls its OnScrollGesture method. Once hit-testing bottoms
        /// out at a leaf control, the leaf gets first refusal (this is where <see cref="TreeView"/> and
        /// <see cref="ListBox"/> pan their own scrollbar); if it does not mark the event
        /// <see cref="ScrollGestureEventArgs.Handled"/>, the gesture then bubbles up the ancestor chain
        /// to the nearest <see cref="ScrollableControl"/> that consumes it — so dragging over e.g. a
        /// Label inside a scrollable Panel pans the Panel, whichever child is under the finger.
        /// </summary>
        internal void RaiseScrollGesture (ScrollGestureEventArgs e)
        {
            var captured = Controls.FindCapturedChild ();

            if (captured != null) {
                captured.RaiseScrollGesture (TranslateScrollGestureEvent (e, captured));
                return;
            }

            var child = Controls.FindVisibleChildAt (e.Location);

            if (child != null) {
                child.RaiseScrollGesture (TranslateScrollGestureEvent (e, child));
                return;
            }

            if (!Enabled)
                return;

            OnScrollGesture (e);

            for (var target = Parent; target != null && !e.Handled; target = target.Parent)
                if (target is ScrollableControl)
                    target.OnScrollGesture (e);
        }

        /// <summary>
        /// Calls the OnPaint method.
        /// </summary>
        internal void RaisePaint (PaintEventArgs e)
        {
            // Clear the dirty flag BEFORE painting (not after): a paint handler that calls
            // Invalidate() on itself synchronously (e.g. an async double-buffering handler whose
            // await completes synchronously) must have that request survive. Clearing afterward
            // would silently clobber it, since it was already dirty when this pass started.
            SetState (States.IsDirty, false);
            OnPaint (e);
            Paint?.Invoke (this, e);
            PaintChildren (e);
        }

        /// <summary>
        /// Calls the OnPaintBackground method.
        /// </summary>
        internal void RaisePaintBackground (PaintEventArgs e) => OnPaintBackground (e);

        /// <summary>
        /// Gets the unscaled right boundary of the control.
        /// </summary>
        public int Right => _x + _width;

        /// <summary>
        /// Scales the control and its children by the specified factor.
        /// </summary>
        /// <remarks>One layout transaction around the whole walk, as upstream has: the children are
        /// being moved and resized together, and laying out after each one both wastes work and lets a
        /// half-scaled tree be measured.</remarks>
        public void Scale (SizeF factor)
        {
            using (new Majorsilence.Forms.Layout.LayoutTransaction (this, this, Majorsilence.Forms.Layout.PropertyNames.Bounds))
                ScaleCore (factor.Width, factor.Height);
        }

        /// <summary>
        /// Scales the control by the specified factor.
        /// </summary>
        /// <remarks>The bounds arithmetic that used to live here now sits in
        /// <see cref="ScaleControl"/>, which is the member WinForms documents for this and the one an
        /// app overrides -- see the remarks there (finding <c>LAY-21</c>).</remarks>
        protected virtual void ScaleCore (float dx, float dy)
        {
            SuspendLayout ();

            try {
                ScaleControl (new SizeF (dx, dy), BoundsSpecified.All);

                foreach (var c in Controls.GetAllControls ())
                    c.ScaleCore (dx, dy);

            } finally {
                ResumeLayout ();
            }
        }

        /// <summary>
        /// Gets the scaled bounds of the control.
        /// </summary>
        public Rectangle ScaledBounds => GetScaledBounds (Bounds, ScaleFactor, BoundsSpecified.All);

        /// <summary>
        /// Gets the scaled height of the control.
        /// </summary>
        public int ScaledHeight => (int)(Height * ScaleFactor.Height);

        /// <summary>
        /// Gets the scaled left of the control.
        /// </summary>
        public int ScaledLeft => (int)(Left * ScaleFactor.Width);

        /// <summary>
        /// Gets the scaled size of the control.
        /// </summary>
        public Size ScaledSize => ScaledBounds.Size;

        /// <summary>
        /// Gets the scaled top of the control.
        /// </summary>
        public int ScaledTop => (int)(Top * ScaleFactor.Height);

        /// <summary>
        /// Gets the scaled width of the control.
        /// </summary>
        public int ScaledWidth => (int)(Width * ScaleFactor.Width);

        /// <summary>
        /// Gets the current scale factor of the control.
        /// </summary>
        public SizeF ScaleFactor => new SizeF ((float)(DeviceDpi / DpiHelper.LogicalDpi), (float)(DeviceDpi / DpiHelper.LogicalDpi));

        /// <summary>
        /// Gets the current scale factor of the form.
        /// </summary>
        public double Scaling => FindWindow ()?.Scaling ?? 1;

        /// <summary>
        /// Gives the control focus.
        /// </summary>
        /// <summary>
        /// Selects this control, or -- when <paramref name="directed"/> -- the next selectable control
        /// in tab order. The overridable WinForms routes every selection through; a container that
        /// manages its own focus (a workspace deciding which of its cells takes it) overrides this.
        /// </summary>
        protected virtual void Select (bool directed, bool forward)
        {
            if (directed)
                SelectNextControl (null, forward, tabStopOnly: true, nested: true, wrap: false);
            else
                Select ();
        }

        /// <summary>Selects this control, giving it focus.</summary>
        /// <remarks>
        /// Hands the whole focus change to <see cref="ControlAdapter"/>, which owns the sequence: Leave
        /// up the old chain, validation between the two, then Enter down the new chain. This method
        /// used to set <c>Selected</c> and raise the new control's Enter/GotFocus <em>before</em>
        /// telling the adapter, and the adapter's setter then deselected the old control — so a
        /// mouse-driven focus change raised the entering control's events first and the leaving
        /// control's second, the reverse of WinForms. Tab went through a different path and got the
        /// order right, so the same application saw two different orders depending on how focus moved.
        /// </remarks>
        public void Select ()
        {
            if (Selected || !CanSelect)
                return;

            var adapter = FindAdapter ();

            if (adapter is null) {
                // No container to sequence the change (an unparented control, or one on a window that
                // has not been built yet). Take focus directly so the flag and the notification still
                // agree with each other.
                Selected = true;
                RaiseEnterOnly ();
                RaiseGotFocus ();
                Invalidate ();
                return;
            }

            adapter.SelectedControl = this;
        }

        /// <summary>
        /// Gets a value indicating the control has focus.
        /// </summary>
        public bool Selected {
            get => GetState (States.IsSelected);
            private set => SetState (States.IsSelected, value);
        }

        /// <summary>
        /// Moves focus to the next control.
        /// </summary>
        /// <param name="start">The control to start from.</param>
        /// <param name="forward">True to move focus to the next control, false for the previous control.</param>
        /// <param name="tabStopOnly">True to only move focus to controls with TabStop set to true, false for all selectable controls.</param>
        /// <param name="nested">True to recurse into the control's children, false for only this control's children.</param>
        /// <param name="wrap">True to wrap around if the end is found, false to not select a control if the end is hit.</param>
        /// <returns>A value indicating if a control was selected.</returns>
        public bool SelectNextControl (Control? start, bool forward, bool tabStopOnly, bool nested, bool wrap)
        {
            Control? c;

            if (start == null || !Contains (start) || (!nested && (start.Parent != this)))
                start = null;

            c = start;

            do {
                c = GetNextControl (c, forward, true);

                if (c is null) {
                    if (wrap) {
                        wrap = false;
                        continue;
                    }

                    break;
                }

                if (c.CanSelect && ((c.Parent == this) || nested) && (c.TabStop || !tabStopOnly)) {
                    c.Select ();
                    return true;
                }

            } while (c != start);

            return false;
        }

        /// <summary>
        ///  This is called recursively when visibility is changed for a control, this
        ///  forces focus to be moved to a visible control.
        /// </summary>
        private void SelectNextIfFocused ()
        {
            if (Focused && Parent is not null)
                if (Parent.GetContainerControl () is Control c)
                    c.SelectNextControl (this, true, true, true, true);
        }

        /// <summary>
        /// Sends this control to the back of the zorder.
        /// </summary>
        public void SendToBack ()
        {
            if (parent != null)
                parent.Controls.SetChildIndex (this, parent.Controls.Count);
        }

        /// <summary>
        /// Sets behavior flags.
        /// </summary>
        protected internal void SetControlBehavior (ControlBehaviors behavior, bool value = true)
        {
            if (value)
                behaviors |= behavior;
            else
                behaviors &= ~behavior;
        }

        /// <summary>
        /// Used to break a StackOverflow circular reference
        /// </summary>
        internal void SetParentInternal (Control? control)
        {
            var was_visible = Visible;

            parent = control;

            if (Visible != was_visible)
                OnVisibleChanged (EventArgs.Empty);

            OnParentChanged (EventArgs.Empty);
        }

        /// <summary>
        /// Sets the bounds of the control from scaled dimensions.
        /// </summary>
        internal void SetScaledBounds (int x, int y, int width, int height, BoundsSpecified specified)
        {
            var rect = GetScaledBounds (new Rectangle (x, y, width, height), new SizeF (1 / ScaleFactor.Width, 1 / ScaleFactor.Height), BoundsSpecified.All);
            SetBoundsCore (rect.X, rect.Y, rect.Width, rect.Height, BoundsSpecified.None);
        }

        private protected void SetState (States flag, bool value)
        {
            _state = value ? _state | flag : _state & ~flag;
        }

        private protected void SetExtendedState (ExtendedStates flag, bool value)
        {
            _extendedState = value ? _extendedState | flag : _extendedState & ~flag;
        }

        /// <summary>
        /// Performs the logic needed to change a control's visibility.
        /// </summary>
        protected virtual void SetVisibleCore (bool value)
        {
            if (value != GetState (States.Visible)) {
                if (!value)
                    SelectNextIfFocused ();

                SetState (States.Visible, value);

                if (Parent is not null)
                    using (new LayoutTransaction (Parent, this, PropertyNames.Visible))
                        OnVisibleChanged (EventArgs.Empty);
                else
                    OnVisibleChanged (EventArgs.Empty);

                // A control that just became visible has to repaint. WinForms shows the window
                // handle, which paints it; there is no handle here, so without this the newly
                // revealed surface (and its children) stayed blank until some unrelated event
                // happened to force a paint. Found running ReportDesigner: switching to the Preview
                // tab showed nothing until Run Report was clicked. OnVisibleChanged above has
                // already laid the subtree out, so bounds are real by the time this invalidates.
                if (value && Parent is not null)
                    Invalidate (true);

                // A visible top-level control lives in its own window; keep that window in step.
                // No-op for ordinary controls -- see Control.TopLevel.cs.
                UpdateTopLevelHost (value);
            }
        }

        /// <summary>
        /// Shows this control to the user.
        /// </summary>
        public void Show ()
        {
            Visible = true;
        }

        /// <summary>
        /// Gets a value indicating a focus rectangle should be drawn on the selected control.
        /// </summary>
        /// <remarks>
        /// Protected-internal, not public: WinForms declares this protected, and themed control
        /// libraries reach it by reflecting with BindingFlags.NonPublic -- against a public property
        /// that lookup finds nothing and the caller dereferences null. Internal keeps this library's
        /// own renderers, which read it across instances, compiling.
        /// </remarks>
        protected internal virtual bool ShowFocusCues => FindForm ()?.ShowFocusCues == true;

        /// <summary>
        /// Gets or sets the unscaled size of the control.
        /// </summary>
        public Size Size {
            get => new Size (_width, _height);
            set => SetBounds (_x, _y, value.Width, value.Height, BoundsSpecified.Size);
        }

        /// <summary>
        /// Gets the ControlStyle properties for this instance of the Control.
        /// </summary>
        public virtual ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets the ControlStyle properties for this instance of the Control when the user is hovering over it.
        /// </summary>
        public virtual ControlStyle StyleHover { get; } = new ControlStyle (DefaultStyleHover);

        /// <summary>
        ///  Suspends the layout logic for the control.
        /// </summary>
        public void SuspendLayout () => layout_suspend_count++;

        /// <summary>
        /// Gets or sets a value indicating the order the control is selected when pressing tab.
        /// </summary>
        public int TabIndex {
            get => tab_index != -1 ? tab_index : 0;
            set {
                if (tab_index != value) {
                    tab_index = value;
                    OnTabIndexChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the control is selectable via pressing tab.
        /// </summary>
        public bool TabStop {
            get => GetState (States.TabStop);
            set {
                if (TabStop != value) {
                    SetState (States.TabStop, value);
                    OnTabStopChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control uses its visual style's background color (no-op compatibility property).
        /// </summary>
        public bool UseVisualStyleBackColor { get; set; } = true;

        /// <summary>Gets the default background color of a control. Matches System.Windows.Forms.Control.DefaultBackColor (SystemColors.Control).</summary>
        public static System.Drawing.Color DefaultBackColor => SystemColors.Control;

        /// <summary>
        /// Gets or sets the background color of the control. This is a convenience wrapper over
        /// <see cref="ControlStyle.BackgroundColor"/> using <see cref="System.Drawing.Color"/>.
        /// </summary>
        public virtual System.Drawing.Color BackColor {
            // Ambient like WinForms: with no explicit color anywhere in the style chain, the value
            // reflects the parent control's effective background.
            get => GetEffectiveBackgroundColor ().ToDrawingColor ();
            set {
                var color = value.ToSKColor ();

                // Only a real change notifies -- assigning the same color again must not fire
                // BackColorChanged (nor re-cascade it to every child).
                if (Style.BackgroundColor == color)
                    return;

                Style.BackgroundColor = color;
                OnBackColorChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the foreground (text) color of the control. This is a convenience wrapper
        /// over <see cref="ControlStyle.ForegroundColor"/> using <see cref="System.Drawing.Color"/>.
        /// </summary>
        public virtual System.Drawing.Color ForeColor {
            // Ambient like WinForms: with no explicit color anywhere in the style chain, the value
            // reflects the parent control's effective foreground.
            get => GetEffectiveForegroundColor ().ToDrawingColor ();
            set {
                var color = value.ToSKColor ();

                if (Style.ForegroundColor == color)
                    return;

                Style.ForegroundColor = color;
                OnForeColorChanged (EventArgs.Empty);
            }
        }

        private Majorsilence.Forms.Drawing.Font? _font;

        /// <summary>
        /// Gets or sets the font (WinForms compatibility property; use Theme or Style for full control).
        /// Never returns null: like WinForms it falls back to the parent's font, then the default UI font,
        /// so code such as <c>ctrl.Font.Size</c> can't NullReference on a control whose font was never set.
        /// </summary>
        // [AllowNull]: the getter never returns null (falls back parent -> default UI font), but the setter
        // accepts null to reset the font to inherited/theme -- matching WinForms' [AllowNull] Control.Font.
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public virtual Majorsilence.Forms.Drawing.Font Font {
            get => _font ?? Parent?.Font ?? Majorsilence.Forms.SystemFonts.DefaultFont;
            set {
                // Only a real change notifies. Note this compares the explicitly-set font, not the
                // resolved one: clearing an override (value null) on a control that never had one
                // is a no-op, but clearing a real override is a change even if the inherited font
                // happens to look the same.
                if (_font is null ? value is null : _font.Equals (value))
                    return;

                _font = value;

                // The renderer reads the typeface/size from CurrentStyle (GetFont/GetFontSize),
                // not from _font, so a bare backing-field store would have no visible effect.
                // Bridge the WinForms font onto the render style the same way the
                // DataGridViewCellStyle -> ControlStyle conversion does. A null assignment clears
                // the override so the style falls back through its parent chain to the theme.
                if (value is null) {
                    Style.Font = null;
                    Style.FontSize = null;
                } else {
                    Style.Font = value.GetSKTypeface ();

                    // PixelSize, not SizeInPoints. Style.FontSize is in PIXELS -- Theme.FontSize is
                    // 14, which is a pixel size -- so assigning the point size drew every control's
                    // text about a quarter too small: 9px for the default 9pt font instead of 12px.
                    // This is the same defect as GFX-25 on the measuring side, in the path that
                    // actually renders, and it is what made captions look tiny in a running app.
                    Style.FontSize = (int) System.Math.Round (value.PixelSize);
                }

                OnFontChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets user defined data.
        /// </summary>
        public object? Tag {
            get => Properties.GetObject (s_tagProperty);
            set => Properties.SetObject (s_tagProperty, value);
        }

        /// <summary>
        /// Gets or sets the text of the control.
        /// </summary>
        public virtual string Text {
            get => text;
            set {
                // WinForms compat: Text is never null — a null assignment is coerced to empty.
                value ??= string.Empty;

                if (text == value)
                    return;

                text = value;

                if (behaviors.HasFlag (ControlBehaviors.InvalidateOnTextChanged))
                    Invalidate ();

                OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the unscaled top boundary of the control.
        /// </summary>
        public int Top {
            get => _y;
            set => SetBounds (_x, value, _width, _height, BoundsSpecified.Y);
        }

        /// <summary>
        /// Changes mouse events to control coordinates.
        /// </summary>
        private static MouseEventArgs TranslateMouseEvents (MouseEventArgs e, Control control)
        {
            if (control == null)
                return e;

            return new MouseEventArgs (e.Button, e.Clicks, e.Location.X - control.Left, e.Location.Y - control.Top, e.DeltaPoint, e.Location.X, e.Location.Y, e.Modifiers);
        }

        // The gesture Translate* helpers subtract logical Left/Top, not ScaledLeft/ScaledTop: the
        // gesture point is in logical units by the time it gets here (WindowBase.HandleXxx converts it
        // at the boundary, same as MouseEventArgs), so this matches TranslateMouseEvents.

        /// <summary>
        /// Changes long-press events to control coordinates.
        /// </summary>
        private static LongPressEventArgs TranslateLongPressEvent (LongPressEventArgs e, Control control)
            => new LongPressEventArgs (e.X - control.Left, e.Y - control.Top);

        /// <summary>
        /// Changes pinch events to control coordinates.
        /// </summary>
        private static PinchGestureEventArgs TranslatePinchEvent (PinchGestureEventArgs e, Control control)
            => new PinchGestureEventArgs (e.X - control.Left, e.Y - control.Top, e.Scale, e.Angle, e.AngleDelta);

        /// <summary>
        /// Changes swipe events to control coordinates.
        /// </summary>
        private static SwipeGestureEventArgs TranslateSwipeEvent (SwipeGestureEventArgs e, Control control)
            => new SwipeGestureEventArgs (e.X - control.Left, e.Y - control.Top, e.VelocityX, e.VelocityY, e.Direction);

        /// <summary>
        /// Changes scroll-gesture events to control coordinates.
        /// </summary>
        private static ScrollGestureEventArgs TranslateScrollGestureEvent (ScrollGestureEventArgs e, Control control)
            => new ScrollGestureEventArgs (e.X - control.Left, e.Y - control.Top, e.Delta);

        /// <summary>
        /// Gets or sets whether the control is displayed to the user.
        /// </summary>
        public virtual bool Visible {
            get {
                if (!GetState (States.Visible))
                    return false;

                return parent?.Visible ?? false;
            }
            set => SetVisibleCore (value);
        }

        /// <summary>
        /// Gets or sets the unscaled width of the control.
        /// </summary>
        public int Width {
            get => _width;
            set => SetBounds (_x, _y, value, _height, BoundsSpecified.Width);
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        /// <summary>
        /// Disposes unmanaged resources used by the control.
        /// </summary>
        protected override void Dispose (bool disposing)
        {
            if (!disposedValue) {
                // Only on an explicit Dispose -- never from the finalizer, where running user
                // handlers is not safe. Mirrors WinForms' handle teardown notification.
                if (disposing && GetState (States.Created))
                    DestroyHandle ();

                FreeBackBuffer ();

                // Snapshot: each child now detaches itself from this collection as it is disposed
                // (see the unparenting below), so iterating the live sequence would skip children.
                foreach (var c in Controls.GetAllControls (true).ToArray ())
                    c.Dispose (disposing);

                // WinForms detaches a control from its parent when it is disposed, and a great deal of
                // ported code relies on it: the standard way to swap a page or panel is to dispose the
                // old control and add the new one, without removing the old one explicitly. Leaving it
                // parented meant the old control stayed in the collection and, if it was docked, went
                // on filling its container -- so the first page opened was the only one that ever
                // showed, and every later navigation appeared to do nothing.
                //
                // Explicit children only. Implicit chrome lives in a separate list that
                // ControlCollection.Remove does not touch, and it is owned by the parent that created
                // it -- it goes away with the parent rather than detaching itself.
                if (disposing && !ImplicitControl && Parent is { } parent && parent.Controls.Contains (this))
                    parent.Controls.Remove (this);

                // A disposed top-level control takes its window with it: Krypton dismisses a popup by
                // disposing it, and the host outliving the control would leave an empty floating window.
                if (disposing)
                    TearDownTopLevelHost ();

                // A disposed control must not keep the capture: it would go on being handed every mouse
                // event in the application, with nothing left to deliver them to.
                if (ReferenceEquals (s_captureHolder, this))
                    s_captureHolder = null;

                disposedValue = true;
                _isDisposed = true;
            }

            base.Dispose (disposing);
        }

        /// <summary>
        /// Destroys the control.
        /// </summary>
        ~Control ()
        {
            Dispose (false);
        }
        #endregion
    }
}
