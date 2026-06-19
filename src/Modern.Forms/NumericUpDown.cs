using System.Drawing;
using Modern.Forms.Renderers;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a NumericUpDown spin box control.
    /// </summary>
    public class NumericUpDown : Control, System.ComponentModel.ISupportInitialize
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

        /// <summary>Gets a value indicating whether the user has edited the text in the spin box. Stub in Modern.Forms.</summary>
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

        /// <summary>Gets or sets the alignment of the text in the control. Stub in Modern.Forms.</summary>
        public HorizontalAlignment TextAlign { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets whether the up/down buttons are aligned to the left. Stub in Modern.Forms.</summary>
        public LeftRightAlignment UpDownAlign { get; set; } = LeftRightAlignment.Right;

        /// <summary>Raised when the value changes.</summary>
        public event EventHandler? ValueChanged;

        /// <summary>Raises the ValueChanged event.</summary>
        protected virtual void OnValueChanged (EventArgs e) => ValueChanged?.Invoke (this, e);

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

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.Border.Width = 1);

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            if (GetIncrementArea ().Contains (e.Location))
                Value = Math.Min (Value + 1, Maximum);
            else if (GetDecrementArea ().Contains (e.Location))
                Value = Math.Max (Value - 1, Minimum);

            base.OnClick (e);
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
    }
}
