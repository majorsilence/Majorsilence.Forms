using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a NumericUpDown spin box control.
    /// </summary>
    public partial class NumericUpDown : Control, System.ComponentModel.ISupportInitialize
    {
        private decimal current_value;
        private decimal minimum;
        private decimal maximum = 100;
        private int decimal_places;
        private bool increment_area_hot;
        private bool decrement_area_hot;

        /// <summary>Initializes a new instance of the NumericUpDown class.</summary>
        public NumericUpDown ()
        {
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);

            // An ordinary child, and FIRST, so Controls[0] is the buttons -- the order WinForms' UpDownBase
            // establishes and the order themers index against. See the UpDownButtons remarks.
            up_down_buttons = new UpDownButtons (this);
            Controls.Add (up_down_buttons);
        }

        private readonly UpDownButtons up_down_buttons;

        /// <summary>Keeps the buttons child over the strip the renderer draws the buttons in.</summary>
        /// <remarks>Its bounds are derived, not stored: the strip is a function of the control's size, so
        /// anything that resizes the control has to move it, and a layout pass is the one place that
        /// reliably runs for all of them.</remarks>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            up_down_buttons?.SetBounds (Width - ButtonWidth, 0, ButtonWidth, Height);
        }

        void System.ComponentModel.ISupportInitialize.BeginInit () { }
        void System.ComponentModel.ISupportInitialize.EndInit () { }

        /// <summary>Gets or sets the number of decimal places shown.</summary>
        public int DecimalPlaces {
            get => decimal_places;
            set {
                if (decimal_places != value) {
                    decimal_places = Math.Max (0, value);
                    Invalidate ();
                }
            }
        }

        /// <summary>Gets or sets the maximum value.</summary>
        public decimal Maximum {
            get => maximum;
            set {
                if (maximum != value) {
                    maximum = value;
                    minimum = Math.Min (minimum, maximum);
                    current_value = Math.Min (Math.Max (current_value, minimum), maximum);
                    Invalidate ();
                }
            }
        }

        /// <summary>Gets or sets the minimum value.</summary>
        public decimal Minimum {
            get => minimum;
            set {
                if (minimum != value) {
                    minimum = value;
                    maximum = Math.Max (minimum, maximum);
                    current_value = Math.Min (Math.Max (current_value, minimum), maximum);
                    Invalidate ();
                }
            }
        }

        /// <summary>Gets or sets the current value.</summary>
        public decimal Value {
            get => current_value;
            set {
                value = Math.Min (Math.Max (value, minimum), maximum);

                if (current_value != value) {
                    current_value = value;
                    Invalidate ();
                    OnValueChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>Gets or sets whether the thousands separator is shown.</summary>
        public bool ThousandsSeparator { get; set; }

        /// <summary>Gets or sets whether arrow keys increment/decrement the value.</summary>
        public bool InterceptArrowKeys { get; set; } = true;

        /// <summary>Gets or sets whether the value is displayed in hexadecimal format.</summary>
        public bool Hexadecimal { get; set; }

        /// <summary>Gets a value indicating whether the user has edited the text in the spin box. Stub in Majorsilence.Forms.</summary>
        public bool UserEdit { get; protected set; }

        /// <summary>Gets or sets whether the control is read-only.</summary>
        public bool ReadOnly { get; set; }

        private decimal increment = 1;

        /// <summary>Gets or sets the value by which to increment or decrement the NumericUpDown.</summary>
        public decimal Increment {
            get => increment;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value, "Increment cannot be negative.");

                increment = value;
            }
        }

        /// <summary>Gets or sets the alignment of the text in the control. Stub in Majorsilence.Forms.</summary>
        public HorizontalAlignment TextAlign { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets whether the up/down buttons are aligned to the left. Stub in Majorsilence.Forms.</summary>
        public LeftRightAlignment UpDownAlign { get; set; } = LeftRightAlignment.Right;

        /// <summary>Selects a range of text in the editable numeric text. No-op stub in Majorsilence.Forms.</summary>
        public void Select (int start, int length) { }

        /// <summary>Raised when the value changes.</summary>
        public event EventHandler? ValueChanged;

        /// <summary>Raises the ValueChanged event.</summary>
        protected virtual void OnValueChanged (EventArgs e) => ValueChanged?.Invoke (this, e);

        /// <summary>Gets or sets the border drawn around the control. Declared here because
        /// System.Windows.Forms puts it on UpDownBase, which this control does not derive from.</summary>
        public BorderStyle BorderStyle {
            get => border_style;
            set {
                if (border_style == value)
                    return;

                border_style = value;
                Style.Border.Width = value == BorderStyle.None ? 0 : 1;
                PerformLayout ();
                Invalidate ();
            }
        }

        private BorderStyle border_style = BorderStyle.Fixed3D;

        // System.Windows.Forms declares these on UpDownBase, which this control cannot derive from here
        // (it is a Control, painting its own edit area rather than hosting a child TextBox). A themed
        // subclass overrides them to repaint when the edit area gains or loses focus, so they have to
        // exist and be raised, or the subclass simply fails to compile. `source` is the edit surface --
        // this control itself, since there is no separate TextBox to hand back.

        /// <summary>Raises the equivalent of UpDownBase's TextBox GotFocus notification.</summary>
        protected virtual void OnTextBoxGotFocus (object source, EventArgs e) { }

        /// <summary>Raises the equivalent of UpDownBase's TextBox LostFocus notification.</summary>
        protected virtual void OnTextBoxLostFocus (object source, EventArgs e) { }

        /// <summary>Raises the equivalent of UpDownBase's TextBox TextChanged notification.</summary>
        protected virtual void OnTextBoxTextChanged (object source, EventArgs e) { }

        /// <inheritdoc/>
        protected override void OnGotFocus (EventArgs e)
        {
            base.OnGotFocus (e);
            OnTextBoxGotFocus (this, e);
        }

        /// <inheritdoc/>
        protected override void OnLostFocus (EventArgs e)
        {
            base.OnLostFocus (e);
            OnTextBoxLostFocus (this, e);
        }

        /// <inheritdoc/>
        protected override void OnTextChanged (EventArgs e)
        {
            base.OnTextChanged (e);
            OnTextBoxTextChanged (this, e);
        }

        /// <summary>Increments the value by the amount of the Increment property.</summary>
        public void UpButton ()
        {
            decimal new_value;

            try {
                new_value = Math.Min (current_value + Increment, maximum);
            } catch (OverflowException) {
                new_value = maximum;
            }

            Value = new_value;
        }

        /// <summary>Decrements the value by the amount of the Increment property.</summary>
        public void DownButton ()
        {
            decimal new_value;

            try {
                new_value = Math.Max (current_value - Increment, minimum);
            } catch (OverflowException) {
                new_value = minimum;
            }

            Value = new_value;
        }

        /// <summary>Increments the value by the Increment amount.</summary>
        public void PerformIncrement () => UpButton ();

        /// <summary>Decrements the value by the Increment amount.</summary>
        public void PerformDecrement () => DownButton ();

        internal Rectangle GetIncrementArea () => new Rectangle (Width - ButtonWidth, 0, ButtonWidth, Height / 2);
        internal Rectangle GetDecrementArea () => new Rectangle (Width - ButtonWidth, Height / 2, ButtonWidth, Height - Height / 2);
        internal int ButtonWidth => LogicalToDeviceUnits (18);

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (3, 0, 0, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (120, 23);

        /// <summary>
        /// Gets the height the control needs to fit one line of text at the current font, as
        /// <c>System.Windows.Forms.UpDownBase.PreferredHeight</c> does.
        /// </summary>
        public int PreferredHeight
            => (int)Math.Ceiling (TextMeasurer.MeasureText ("Wg", this).Height)
                + Padding.Top + Padding.Bottom + 4;   // 4px matches the default border/inset

        /// <summary>
        /// The control's height is fixed by its font, so report that as the preferred one.
        /// </summary>
        /// <remarks>
        /// Same defect and same fix as <see cref="TextBoxBase.GetPreferredSizeCore"/> -- see the long
        /// comment there. The base reports only bounds that were explicitly SET, so an unsized
        /// NumericUpDown reported a preferred height of zero and Krypton's <c>KryptonNumericUpDown</c>,
        /// which takes its own height from the control it hosts, collapsed to a 3px sliver.
        /// </remarks>
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var preferred = base.GetPreferredSizeCore (proposedSize);

            if (preferred.Height <= 0)
                preferred.Height = PreferredHeight;

            return preferred;
        }

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.Border.Width = 1);

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            if (GetIncrementArea ().Contains (e.Location))
                Value = Math.Min (Value + 1, Maximum);
            else if (GetDecrementArea ().Contains (e.Location))
                Value = Math.Max (Value - 1, Minimum);

            base.OnMouseClick (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            var new_inc = GetIncrementArea ().Contains (e.Location);
            var new_dec = GetDecrementArea ().Contains (e.Location);

            if (new_inc != increment_area_hot || new_dec != decrement_area_hot) {
                increment_area_hot = new_inc;
                decrement_area_hot = new_dec;
                Invalidate ();
            }

            base.OnMouseMove (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            if (increment_area_hot || decrement_area_hot) {
                increment_area_hot = false;
                decrement_area_hot = false;
                Invalidate ();
            }

            base.OnMouseLeave (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets whether the increment button area is hot-tracked.</summary>
        internal bool IncrementAreaHot => increment_area_hot;

        /// <summary>Gets whether the decrement button area is hot-tracked.</summary>
        internal bool DecrementAreaHot => decrement_area_hot;

        /// <summary>The up/down buttons, as a real child control.</summary>
        /// <remarks>
        /// WinForms' <c>UpDownBase</c> owns two child controls -- the up/down buttons and an edit box --
        /// and adds the BUTTONS first, so <c>Controls[0]</c> is the buttons. That is not an incidental
        /// detail: it is the documented way to theme one of these, and real code hooks
        /// <c>Controls[0].Paint</c> to draw its own arrows and calls <c>Controls[0].PointToClient</c> to
        /// hit-test them. This control drew itself with no children at all, so that idiom found nothing
        /// and threw before the control existed.
        ///
        /// This child deliberately does NOT paint and does NOT hit-test on its own. It occupies the button
        /// strip, forwards the mouse to the owner's existing logic, and leaves the owner's renderer drawing
        /// the whole control exactly as before -- so the idiom works with no change to how a
        /// NumericUpDown looks or behaves. A themer's Paint handler runs after the owner has painted
        /// (children paint last), which is where they want to draw anyway.
        /// </remarks>
        private sealed class UpDownButtons : Control
        {
            private readonly NumericUpDown owner;

            internal UpDownButtons (NumericUpDown owner)
            {
                this.owner = owner;

                // Transparent so the owner's rendering of the buttons shows through: this child exists to
                // be addressable, not to take over drawing.
                SetControlBehavior (ControlBehaviors.Transparent, true);
                SetControlBehavior (ControlBehaviors.Selectable, false);
            }

            // The owner hit-tests in ITS coordinates, so translate before handing the event over.
            private MouseEventArgs ToOwner (MouseEventArgs e)
                => new MouseEventArgs (e.Button, e.Clicks, e.X + Left, e.Y + Top, e.Delta);

            protected override void OnMouseClick (MouseEventArgs e)
            {
                owner.OnMouseClick (ToOwner (e));
                base.OnMouseClick (e);
            }

            protected override void OnMouseMove (MouseEventArgs e)
            {
                owner.OnMouseMove (ToOwner (e));
                base.OnMouseMove (e);
            }

            protected override void OnMouseLeave (EventArgs e)
            {
                owner.OnMouseLeave (e);
                base.OnMouseLeave (e);
            }
        }
    }
}
