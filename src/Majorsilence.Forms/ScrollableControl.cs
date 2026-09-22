using System.Drawing;
using Majorsilence.Forms.Layout;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ScrollableControl control.
    /// </summary>
    public partial class ScrollableControl : Control
    {
        private readonly HorizontalScrollBar hscrollbar;
        private readonly VerticalScrollBar vscrollbar;
        private readonly SizeGrip sizegrip;

        private Point scroll_position = Point.Empty;
        private Size canvas_size = Size.Empty;
        private Size auto_scroll_min_size = Size.Empty;
        private Size auto_scroll_margin = Size.Empty;
        private bool auto_scroll;

        /// <summary>
        /// Initializes a new instance of the ScrollableControl class.
        /// </summary>
        public ScrollableControl ()
        {
            hscrollbar = Controls.AddImplicitControl (new HorizontalScrollBar {
                Visible = false
            });

            hscrollbar.ValueChanged += HandleScroll;
            hscrollbar.Scroll += (o, e) => OnScroll (e);

            vscrollbar = Controls.AddImplicitControl (new VerticalScrollBar {
                Visible = false
            });

            vscrollbar.ValueChanged += HandleScroll;
            vscrollbar.Scroll += (o, e) => OnScroll (e);

            sizegrip = Controls.AddImplicitControl (new SizeGrip {
                Visible = false
            });

            SizeChanged += (o, e) => Recalculate (true);
            VisibleChanged += (o, e) => Recalculate (true);
        }

        /// <summary>
        /// Adjusts the scrollbars based on the currently contained controls.
        /// </summary>
        protected virtual void AdjustFormScrollbars (bool displayScrollbars) => Recalculate (false);

        /// <summary>
        /// Gets or sets a value indicating the user can scroll to controls beyond the ScrollableControl's bounds.
        /// </summary>
        public virtual bool AutoScroll {
            get => auto_scroll;
            set {
                if (auto_scroll != value) {
                    auto_scroll = value;
                    PerformLayout (this, nameof (AutoScroll));
                }
            }
        }

        /// <summary>
        /// Gets or sets the extra margin, in pixels, kept around the auto-scroll area -- both added to
        /// the scrollable canvas and left clear when <see cref="Control.ScrollControlIntoView(Control?)"/>
        /// brings a control into view.
        /// </summary>
        /// <remarks>
        /// <para>
        /// LAY-32: this was an auto-property, while <c>Recalculate</c> read a private field of the same
        /// name that nothing ever wrote -- so the margin was stored, reported back, and applied to
        /// nothing. The two are now one store. (W5.25 landed that half and recorded it under
        /// <c>LAY-30</c>, which was the wrong finding; it is this one.)
        /// </para>
        /// <para>
        /// The property REJECTS a negative component where <see cref="SetAutoScrollMargin"/> CLAMPS it,
        /// which looks inconsistent and is upstream's behaviour exactly: the property is what designer
        /// code assigns and a negative there is a bug worth surfacing, while the method is the
        /// programmatic path and has always been forgiving.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Either component of <paramref name="value"/> is negative.</exception>
        public Size AutoScrollMargin {
            get => auto_scroll_margin;
            set {
                if (value.Width < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value.Width, $"'{value.Width}' is not a valid value for 'AutoScrollMargin.Width'. It must be greater than or equal to 0.");

                if (value.Height < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value.Height, $"'{value.Height}' is not a valid value for 'AutoScrollMargin.Height'. It must be greater than or equal to 0.");

                SetAutoScrollMarginCore (value);
            }
        }

        /// <summary>Sets the size of the auto-scroll margin around the control (WinForms compat for AutoScrollMargin property).</summary>
        /// <param name="x">The horizontal margin. A negative value is clamped to zero.</param>
        /// <param name="y">The vertical margin. A negative value is clamped to zero.</param>
        public void SetAutoScrollMargin (int x, int y)
            => SetAutoScrollMarginCore (new Size (Math.Max (0, x), Math.Max (0, y)));

        // The one assignment point, so the property and the method cannot drift apart on what a change
        // actually does -- only on what they accept.
        private void SetAutoScrollMarginCore (Size value)
        {
            if (auto_scroll_margin == value)
                return;

            auto_scroll_margin = value;
            PerformLayout (this, nameof (AutoScrollMargin));
        }

        /// <summary>
        /// Gets or sets the minimum logical size of the auto-scroll area. Setting a non-empty value
        /// enables <see cref="AutoScroll"/> so a custom-drawn control can scroll a virtual canvas
        /// larger than its visible bounds (e.g. a report page surface).
        /// </summary>
        public Size AutoScrollMinSize {
            get => auto_scroll_min_size;
            set {
                if (auto_scroll_min_size != value) {
                    auto_scroll_min_size = value;

                    if (!value.IsEmpty)
                        auto_scroll = true;

                    PerformLayout (this, nameof (AutoScrollMinSize));
                }
            }
        }

        /// <summary>
        /// Gets or sets the current scroll position. Following WinForms semantics the value returned
        /// is expressed as negative offsets; the setter accepts either sign.
        /// </summary>
        public Point AutoScrollPosition {
            get => new Point (-scroll_position.X, -scroll_position.Y);
            set {
                var x = Math.Abs (value.X);
                var y = Math.Abs (value.Y);

                if (hscrollbar.Visible)
                    hscrollbar.Value = Math.Max (hscrollbar.Minimum, Math.Min (x, hscrollbar.Maximum));

                if (vscrollbar.Visible)
                    vscrollbar.Value = Math.Max (vscrollbar.Minimum, Math.Min (y, vscrollbar.Maximum));
            }
        }

        /// <summary>
        /// Pans <see cref="AutoScrollPosition"/> by the gesture's delta, content-follows-finger (a
        /// downward/rightward drag reveals content that was above/to the left). Because the backend's
        /// gesture recognizer keeps delivering this event with a decaying delta during its own
        /// inertia/friction phase after the contact lifts, repeatedly applying it here is the whole
        /// momentum/flick-scrolling implementation -- no deceleration physics needed here.
        /// </summary>
        protected override void OnScrollGesture (ScrollGestureEventArgs e)
        {
            base.OnScrollGesture (e);

            if (!auto_scroll)
                return;

            // This control owns the gesture now; don't let it also bubble to an outer scrollable.
            e.Handled = true;

            // AutoScrollPosition's getter returns WinForms-style negative offsets; its setter takes
            // the (unsigned) magnitude and clamps to the scrollbar range itself -- but only after an
            // internal Math.Abs(), which would silently flip a magnitude that went negative back to
            // positive instead of clamping to zero. Clamp to zero here first to avoid that.
            var current = AutoScrollPosition;
            var x = Math.Max (0, -current.X - e.Delta.X);
            var y = Math.Max (0, -current.Y - e.Delta.Y);
            AutoScrollPosition = new Point (x, y);
        }

        // Calculates and sets the current canvas size.
        private void CalculateCanvasSize ()
        {
            var width = 0;
            var height = 0;
            var extra_width = hscrollbar.Value + Padding.Right;
            var extra_height = vscrollbar.Value + Padding.Bottom;

            foreach (var c in Controls) {
                if (c.Dock == DockStyle.Right)
                    extra_width += c.Width;
                else if (c.Dock == DockStyle.Bottom)
                    extra_height += c.Height;
            }

            if (!auto_scroll_min_size.IsEmpty) {
                width = auto_scroll_min_size.Width;
                height = auto_scroll_min_size.Height;
            }

            foreach (var c in Controls) {
                switch (c.Dock) {
                    case DockStyle.Left:
                        width = Math.Max (width, c.Right + extra_width);
                        continue;
                    case DockStyle.Top:
                        height = Math.Max (height, c.Bottom + extra_height);
                        continue;
                    case DockStyle.Bottom:
                    case DockStyle.Right:
                    case DockStyle.Fill:
                        continue;
                    default:
                        var anchor = c.Anchor;

                        if (anchor.HasFlag (AnchorStyles.Left) && !anchor.HasFlag (AnchorStyles.Right))
                            width = Math.Max (width, c.Right + extra_width);

                        if (anchor.HasFlag (AnchorStyles.Top) && !anchor.HasFlag (AnchorStyles.Bottom))
                            height = Math.Max (height, c.Bottom + extra_height);

                        continue;
                }
            }

            canvas_size.Width = width;
            canvas_size.Height = height;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// LAY-29: this used to report the visible client area with its origin always at the client
        /// origin, whatever the scroll position, and its size always the visible size. Upstream's
        /// carries the scroll offset as a NEGATIVE origin and the scrollable content extent as the
        /// size (<c>ScrollableControl.DisplayRectangle</c>), which is the coordinate space every
        /// anchor delta in the layout engine is expressed in -- the capture subtracts this origin and
        /// the placement adds it back. Anything converting between content and client coordinates,
        /// custom painting and hit-testing included, read <c>(0,0)</c> and mis-placed content by
        /// exactly the scroll amount.
        /// </para>
        /// <para>
        /// The children are still physically moved when the control scrolls, which is not a departure
        /// from upstream: <c>SetDisplayRectLocation</c> scrolls with <c>SW_SCROLLCHILDREN</c>, so the
        /// OS moves the child windows there too. The two stay consistent by construction -- a layout
        /// pass computes a child's position as (delta captured against the old origin) + (the new
        /// origin), which is where scrolling already put it.
        /// </para>
        /// </remarks>
        public override Rectangle DisplayRectangle {
            get {
                // A ScrollableControl DisplayRectangle includes Padding, while a normal Control does not.
                var rect = base.DisplayRectangle;

                if (hscrollbar.Visible)
                    rect.Height -= hscrollbar.Height;

                if (vscrollbar.Visible)
                    rect.Width -= vscrollbar.Width;

                rect.X -= scroll_position.X;
                rect.Y -= scroll_position.Y;

                // The content extent, not the visible extent, on whichever axis actually scrolls --
                // an axis with no scrollbar has no content beyond what is already shown.
                if (hscrollbar.Visible && canvas_size.Width > rect.Width)
                    rect.Width = canvas_size.Width;

                if (vscrollbar.Visible && canvas_size.Height > rect.Height)
                    rect.Height = canvas_size.Height;

                // TODO: Scale padding?
                return LayoutUtils.DeflateRect (rect, Padding);
            }
        }

        // Handles events from the scrollbars to update the window position.
        private void HandleScroll (object? sender, EventArgs e)
        {
            if (sender == vscrollbar && vscrollbar.Visible)
                ScrollWindow (0, vscrollbar.Value - scroll_position.Y);
            else if (sender == hscrollbar && hscrollbar.Visible)
                ScrollWindow (hscrollbar.Value - scroll_position.X, 0);
        }

        /// <summary>
        /// Provides access to the properties of the horizontal scrollbar.
        /// </summary>
        public HScrollProperties HorizontalScrollProperties => new HScrollProperties (hscrollbar);

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            CalculateCanvasSize ();
            AdjustFormScrollbars (AutoScroll);

            base.OnLayout (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the Scroll event.
        /// </summary>
        protected virtual void OnScroll (ScrollEventArgs e) => Scroll?.Invoke (this, e);

        // Recalculates all components of the ScrollableControl.
        private void Recalculate (bool doLayout)
        {
            var canvas = canvas_size;
            var client = Bounds;

            canvas.Width += auto_scroll_margin.Width;
            canvas.Height += auto_scroll_margin.Height;

            var right_edge = client.Width;
            var bottom_edge = client.Height;
            var prev_right_edge = 0;
            var prev_bottom_edge = 0;

            var hscroll_visible = false;
            var vscroll_visible = false;

            var bar_size = 15;

            do {
                prev_right_edge = right_edge;
                prev_bottom_edge = bottom_edge;

                if (canvas.Width > right_edge && auto_scroll && client.Width > 0) {
                    hscroll_visible = true;
                    bottom_edge = client.Height - bar_size;// SystemInformation.HorizontalScrollBarHeight;
                } else {
                    hscroll_visible = false;
                    bottom_edge = client.Height;
                }

                if (canvas.Height > bottom_edge && auto_scroll && client.Height > 0) {
                    vscroll_visible = true;
                    right_edge = client.Width - bar_size;// SystemInformation.VerticalScrollBarWidth;
                } else {
                    vscroll_visible = false;
                    right_edge = client.Width;
                }
            } while (right_edge != prev_right_edge || bottom_edge != prev_bottom_edge);

            right_edge = Math.Max (right_edge, 0);
            bottom_edge = Math.Max (bottom_edge, 0);

            // The flags upstream's ScrollableControl exposes as HScroll/VScroll (W6.2 sweep).
            HScroll = hscroll_visible;
            VScroll = vscroll_visible;

            if (!vscroll_visible)
                vscrollbar.Value = 0;
            if (!hscroll_visible)
                hscrollbar.Value = 0;

            if (hscroll_visible) {
                hscrollbar.LargeChange = right_edge;
                hscrollbar.SmallChange = 5;
                hscrollbar.Maximum = canvas.Width - client.Width + bar_size;

            } else {
                if (hscrollbar.Visible)
                    ScrollWindow (-scroll_position.X, 0);

                scroll_position.X = 0;
            }

            if (vscroll_visible) {
                vscrollbar.LargeChange = bottom_edge;
                vscrollbar.SmallChange = 5;
                vscrollbar.Maximum = canvas.Height - client.Height + bar_size;

            } else {
                if (vscrollbar.Visible)
                    ScrollWindow (0, -scroll_position.Y);

                scroll_position.X = 0;
            }

            SuspendLayout ();

            var sizegrip_visible = hscroll_visible && vscroll_visible;

            hscrollbar.SetBounds (0, client.Height - bar_size, sizegrip_visible ? Bounds.Width - bar_size : Bounds.Height, bar_size);
            hscrollbar.Visible = hscroll_visible;

            vscrollbar.SetBounds (client.Width - bar_size, 0, bar_size, sizegrip_visible ? Bounds.Height - bar_size : Bounds.Height);
            vscrollbar.Visible = vscroll_visible;

            sizegrip.SetBounds (client.Width - bar_size, client.Height - bar_size, bar_size, bar_size);
            sizegrip.Visible = sizegrip_visible;

            ResumeLayout (doLayout);
        }

        /// <summary>
        /// Raised when the ScrollableControl is scrolled.
        /// </summary>
        public new event EventHandler<ScrollEventArgs>? Scroll;

        // Scrolls the control by the requested offsets.
        private void ScrollWindow (int xOffset, int yOffset)
        {
            if (xOffset == 0 && yOffset == 0)
                return;

            SuspendLayout ();

            // Reposition via IArrangedElement.SetBounds with BoundsSpecified.None (as the layout engine
            // itself does when applying computed Dock/Anchor bounds), not the public Location setter.
            // The public setter passes BoundsSpecified.Location through to InitLayoutCore, which
            // re-snapshots any Anchored child's cached "distance from parent edges" -- using bounds that
            // haven't caught up to the parent's current size yet -- permanently freezing that child at
            // whatever size it happened to be the moment a scroll adjustment last ran. A scroll offset
            // is bookkeeping, not a semantic bounds change, so it must not disturb that snapshot.
            foreach (var c in Controls) {
                var newBounds = new Rectangle (c.Left - xOffset, c.Top - yOffset, c.Width, c.Height);
                ((Layout.IArrangedElement) c).SetBounds (newBounds, BoundsSpecified.None);
            }

            scroll_position.Offset (xOffset, yOffset);

            ResumeLayout (false);
        }

        /// <summary>
        /// Provides access to the properties of the vertical scrollbar.
        /// </summary>
        public VScrollProperties VerticalScrollProperties => new VScrollProperties (vscrollbar);

        /// <summary>Gets the horizontal scroll properties (WinForms alias for HorizontalScrollProperties).</summary>
        public HScrollProperties HorizontalScroll => HorizontalScrollProperties;

        /// <summary>Gets the vertical scroll properties (WinForms alias for VerticalScrollProperties).</summary>
        public VScrollProperties VerticalScroll => VerticalScrollProperties;
    }
}
