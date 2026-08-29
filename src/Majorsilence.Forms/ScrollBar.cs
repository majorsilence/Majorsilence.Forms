using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents the base class of a ScrollBar control.
    /// </summary>
    public abstract partial class ScrollBar : Control
    {
        private int large_change = 10;
        private int maximum = 100;
        private int minimum;
        private int current_value;
        private int small_change = 1;
        private bool thumb_pressed;
        private int thumbclick_offset;              // Position of the last button-down event relative to the thumb edge

        private readonly bool vertical;

        internal int thumb_drag_position;     // Current pixel of the midpoint of the thumb drag 

        /// <summary>
        /// Initializes a new instance of the ScrollBar class.
        /// </summary>
        protected ScrollBar (bool vertical = false)
        {
            this.vertical = vertical;
            TabStop = false;
        }

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.BackgroundColor = Theme.ControlMidHighColor);

        /// <summary>
        /// Gets or sets the amount the ScrollBar will change when clicked in the track area.
        /// </summary>
        public int LargeChange {
            get => Math.Min (large_change, maximum - minimum + 1);
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (LargeChange), $"Value '{value}' must be greater than or equal to 0.");

                if (large_change != value) {
                    large_change = value;
                    UpdateFromValue (Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum value the ScrollBar will allow.
        /// </summary>
        public int Maximum {
            get => maximum;
            set {
                if (maximum != value) {
                    maximum = value;

                    if (maximum < minimum)
                        minimum = maximum;
                    if (Value > maximum)
                        Value = maximum;

                    UpdateFromValue (Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum value the ScrollBar will allow.
        /// </summary>
        public int Minimum {
            get => minimum;
            set {
                if (minimum != value) {
                    minimum = value;

                    if (minimum > maximum)
                        maximum = minimum;
                    if (Value < minimum)
                        Value = minimum;

                    UpdateFromValue (Value);
                }
            }
        }

        /// <summary>
        /// Raised when the ScrollBar is scrolled.
        /// </summary>
        public new event EventHandler<ScrollEventArgs>? Scroll;

        /// <summary>
        /// Gets or sets the amount the ScrollBar will change when the increment or decrement arrows are clicked.
        /// </summary>
        public int SmallChange {
            // We can't have SmallChange > LargeChange, but we shouldn't manipulate
            // the set value, so clamp on the way out to match WinForms behavior.
            get => Math.Min (small_change, LargeChange);
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (SmallChange), $"Value '{value}' must be greater than or equal to 0.");

                if (small_change != value) {
                    small_change = value;
                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets or sets the current value of the ScrollBar.
        /// </summary>
        public int Value {
            get => current_value;
            set {
                if (value < minimum || value > maximum)
                    throw new ArgumentOutOfRangeException (nameof (Value), $"'{value}' is not a valid value for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'");

                UpdateFromValue (value);
            }
        }

        /// <summary>
        /// Raised when the value of the ScrollBar changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        // The number of possible ScrollBar values.
        private int PossibleValuesCount => maximum - minimum + 1;

        // Retrieves the effective track bounds from the renderer.
        private Rectangle GetEffectiveTrackBounds () => RenderManager.GetRenderer<ScrollBarRenderer> ()!.GetEffectiveTrackBounds (this);

        private ScrollBarElement GetElementAtLocation (Point location)
        {
            var renderer = RenderManager.GetRenderer<ScrollBarRenderer> ()!;

            if (renderer.GetDecrementArrowBounds (this).Contains (location))
                return ScrollBarElement.DecrementArrow;

            if (renderer.GetIncrementArrowBounds (this).Contains (location))
                return ScrollBarElement.IncrementArrow;

            if (renderer.GetThumbDragBounds (this).Contains (location))
                return ScrollBarElement.Thumb;

            if (renderer.GetDecrementTrackBounds (this).Contains (location))
                return ScrollBarElement.DecrementTrack;

            if (renderer.GetIncrementTrackBounds (this).Contains (location))
                return ScrollBarElement.IncrementTrack;

            // In theory this shouldn't be possible...
            return ScrollBarElement.None;
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            UpdateFromValue (current_value);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!Enabled || !e.Button.HasFlag (MouseButtons.Left))
                return;

            switch (GetElementAtLocation (e.Location)) {
                case ScrollBarElement.DecrementArrow:
                    PerformScroll (ScrollEventType.SmallDecrement, Value - SmallChange);
                    break;
                case ScrollBarElement.DecrementTrack:
                    PerformScroll (ScrollEventType.LargeDecrement, Value - LargeChange);
                    break;
                case ScrollBarElement.Thumb:
                    thumb_pressed = true;
                    thumbclick_offset = (vertical ? e.Y : e.X) - thumb_drag_position;
                    break;
                case ScrollBarElement.IncrementTrack:
                    PerformScroll (ScrollEventType.LargeIncrement, Value + LargeChange);
                    break;
                case ScrollBarElement.IncrementArrow:
                    PerformScroll (ScrollEventType.SmallIncrement, Value + SmallChange);
                    break;
            }
        }

        // A user-initiated scroll (arrow, track, thumb, wheel). Raises Scroll with the proposed
        // value while Value still holds the OLD one -- so a handler can read the delta and can veto
        // by rewriting e.NewValue -- then commits it, matching WinForms. Before this, arrow/track/
        // wheel raised no Scroll event at all and thumb-drag raised it only after committing (so
        // e.NewValue always equalled Value), which broke every migrated handler that scrolls its
        // view from ScrollBar.Scroll -- e.g. RdlViewer's report pane, whose scrollbar and wheel
        // did nothing.
        private void PerformScroll (ScrollEventType type, int proposedValue)
        {
            proposedValue = Math.Max (minimum, Math.Min (maximum, proposedValue));

            var e = new ScrollEventArgs (type, current_value, proposedValue,
                vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll);
            OnScroll (e);

            if (e.NewValue != current_value)
                UpdateFromValue (e.NewValue);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (thumb_pressed) {
                var pixel = (vertical ? e.Y : e.X) - thumbclick_offset;
                thumb_drag_position = ClampToTrack (pixel);
                PerformScroll (ScrollEventType.ThumbTrack, ValueFromPixel (pixel));
                Invalidate ();   // thumb follows the cursor even between discrete values
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (thumb_pressed) {
                thumb_pressed = false;
                PerformScroll (ScrollEventType.EndScroll, current_value);
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (!Enabled)
                return;

            if (e.Delta != 0)
                PerformScroll (
                    e.Delta > 0 ? ScrollEventType.SmallDecrement : ScrollEventType.SmallIncrement,
                    Value - (e.Delta * SmallChange));
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
        protected virtual void OnScroll (ScrollEventArgs e)
        {
            e.NewValue = Math.Max (e.NewValue, Minimum);
            e.NewValue = Math.Min (e.NewValue, Maximum);

            Scroll?.Invoke (this, e);
        }

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        protected virtual void OnValueChanged (EventArgs e) => ValueChanged?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnVisibleChanged (EventArgs e)
        {
            base.OnVisibleChanged (e);

            if (Visible)
                UpdateFromValue (Value);
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore (x, y, width, height, specified);

            UpdateFromValue (Value);
        }

        // Clamps a raw pixel coordinate to the drawable track.
        private int ClampToTrack (int pixel)
        {
            var track = GetEffectiveTrackBounds ();
            return vertical
                ? Math.Max (track.Top, Math.Min (track.Bottom, pixel))
                : Math.Max (track.Left, Math.Min (track.Right, pixel));
        }

        // The ScrollBar value a thumb-drag pixel position maps to.
        private int ValueFromPixel (int pixel)
        {
            var track = GetEffectiveTrackBounds ();
            pixel = ClampToTrack (pixel);

            var position_percent =
                vertical ? (double)(pixel - track.Top) / track.Height
                         : (double)(pixel - track.Left) / track.Width;

            return minimum + (int)(position_percent * (PossibleValuesCount - 1));
        }

        // Updates thumb drag position from a ScrollBar value.
        private void UpdateFromValue (int value)
        {
            value = Math.Max (value, minimum);
            value = Math.Min (value, maximum);

            var possible = PossibleValuesCount - 1;
            var value_percent = possible > 0 ? (double)(value - minimum) / possible : 0d;

            var effective_track_bounds = GetEffectiveTrackBounds ();

            var new_pos =
                vertical ? effective_track_bounds.Y + (value_percent * effective_track_bounds.Height)
                         : effective_track_bounds.X + (value_percent * effective_track_bounds.Width);

            thumb_drag_position = (int)new_pos;

            Invalidate ();

            if (current_value == value)
                return;

            current_value = value;

            OnValueChanged (EventArgs.Empty);
        }

        private enum ScrollBarElement
        {
            None,
            DecrementArrow,
            DecrementTrack,
            Thumb,
            IncrementTrack,
            IncrementArrow
        }
    }
}
