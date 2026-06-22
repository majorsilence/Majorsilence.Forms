using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ProgressBar control.
    /// </summary>
    public class ProgressBar : Control
    {
        private int maximum = 100;
        private int minimum;
        private int current_value;

        /// <summary>
        /// Initializes a new instance of the PictureBox class.
        /// </summary>
        public ProgressBar ()
        {
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (1);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 23);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.Border.Width = 1);

        /// <summary>
        /// Increases the value of the ProgressBar by the specified amount.
        /// </summary>
        public void Increment (int? value = null)
        {
            if (Style == ProgressBarStyle.Marquee)
                throw new InvalidOperationException ("Increment should not be called if the style is Marquee.");

            var new_value = Value + value.GetValueOrDefault (Step);

            new_value = new_value.Clamp (minimum, maximum);

            Value = new_value;
        }

        /// <summary>
        /// Gets or sets the maximum value of the ProgressBar.
        /// </summary>
        public int Maximum {
            get => maximum;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (Maximum), $"Value '{value}' must be greater than or equal to 0.");

                if (maximum != value) {
                    maximum = value;
                    minimum = Math.Min (minimum, maximum);
                    current_value = Math.Min (current_value, maximum);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum value of the ProgressBar.
        /// </summary>
        public int Minimum {
            get => minimum;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (Minimum), $"Value '{value}' must be greater than or equal to 0.");

                if (minimum != value) {
                    minimum = value;
                    maximum = Math.Max (minimum, maximum);
                    current_value = Math.Max (current_value, minimum);
                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets the default amount the ProgressBar will increment or decrement.
        /// </summary>
        public int Step { get; set; } = 10;

        /// <summary>Advances the current position of the ProgressBar by the amount of the Step property.</summary>
        public void PerformStep () => Increment (Step);

        private ProgressBarStyle _barStyle = ProgressBarStyle.Blocks;

        /// <summary>Gets or sets the progress bar display style. Shadows Control.Style for WinForms compatibility.</summary>
        public new ProgressBarStyle Style {
            get => _barStyle;
            set { _barStyle = value; Invalidate (); }
        }

        private int _marqueeAnimationSpeed = 100;

        /// <summary>Gets or sets the animation speed (in ms) when Style is Marquee. Stub in Majorsilence.Forms.</summary>
        public int MarqueeAnimationSpeed {
            get => _marqueeAnimationSpeed;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (MarqueeAnimationSpeed), $"Value '{value}' must be greater than or equal to 0.");

                _marqueeAnimationSpeed = value;
            }
        }

        /// <summary>
        /// Gets or sets the current value of the ProgressBar.
        /// </summary>
        public int Value {
            get => current_value;
            set {
                if (value < Minimum || value > Maximum)
                    throw new ArgumentOutOfRangeException (nameof (Value), $"'{value}' is not a valid value for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'");

                if (current_value != value) {
                    current_value = value;
                    Invalidate ();
                }
            }
        }
    }

    /// <summary>
    /// Specifies the display style of a ProgressBar control.
    /// </summary>
    public enum ProgressBarStyle
    {
        /// <summary>The progress bar displays progress as a series of rectangular blocks.</summary>
        Blocks,
        /// <summary>The progress bar displays progress as a solid bar.</summary>
        Continuous,
        /// <summary>The progress bar animates continuously (indeterminate).</summary>
        Marquee
    }
}
