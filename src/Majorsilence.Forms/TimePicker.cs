using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a control for selecting a time value.
    /// </summary>
    public class TimePicker : TextBox
    {
        private DateTime value = DateTime.Now;

        /// <summary>Initializes a new instance of the TimePicker class.</summary>
        public TimePicker ()
        {
            Text = value.ToString ("HH:mm");
        }

        /// <summary>Gets or sets the selected time value.</summary>
        public DateTime? Value {
            get {
                if (DateTime.TryParse (Text, out var dt))
                    return dt;

                return value;
            }
            set {
                if (value.HasValue) {
                    this.value = value.Value;
                    Text = value.Value.ToString ("HH:mm");
                }
            }
        }

        /// <summary>Gets or sets the minimum allowed value.</summary>
        public DateTime MinValue { get; set; } = DateTime.MinValue;

        /// <summary>Gets or sets the maximum allowed value.</summary>
        public DateTime MaxValue { get; set; } = DateTime.MaxValue;
    }
}
