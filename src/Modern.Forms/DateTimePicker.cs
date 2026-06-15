using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// Specifies the date/time format displayed by a DateTimePicker.
    /// </summary>
    public enum DateTimePickerFormat
    {
        /// <summary>Long date format.</summary>
        Long,
        /// <summary>Short date format.</summary>
        Short,
        /// <summary>Time format.</summary>
        Time,
        /// <summary>Custom format string.</summary>
        Custom
    }

    /// <summary>
    /// Represents a DateTimePicker control for selecting a date and/or time.
    /// Built on TimePicker for Modern.Forms compatibility.
    /// </summary>
    public class DateTimePicker : TextBox
    {
        private DateTime _value = DateTime.Now;
        private string _customFormat = "G";
        private DateTimePickerFormat _format = DateTimePickerFormat.Long;

        /// <summary>Initializes a new instance of the DateTimePicker class.</summary>
        public DateTimePicker ()
        {
            UpdateText ();
        }

        /// <summary>Gets or sets the custom format string (used when Format = Custom).</summary>
        public string CustomFormat {
            get => _customFormat;
            set {
                _customFormat = value ?? "G";
                if (_format == DateTimePickerFormat.Custom)
                    UpdateText ();
            }
        }

        /// <summary>Gets or sets the display format.</summary>
        public DateTimePickerFormat Format {
            get => _format;
            set {
                _format = value;
                UpdateText ();
            }
        }

        /// <summary>Gets or sets whether a spin button is shown instead of a drop-down calendar.</summary>
        public bool ShowUpDown { get; set; }

        /// <summary>Gets or sets the font used for the calendar portion. Stub in Modern.Forms.</summary>
        public Modern.Drawing.Font? CalendarFont { get; set; }

        /// <summary>Gets or sets the minimum date value.</summary>
        public DateTime MinDate { get; set; } = new DateTime (1753, 1, 1);

        /// <summary>Gets or sets the maximum date value.</summary>
        public DateTime MaxDate { get; set; } = new DateTime (9998, 12, 31);

        /// <summary>Gets or sets the current date/time value.</summary>
        public DateTime Value {
            get {
                if (DateTime.TryParse (Text, out var parsed))
                    return parsed;
                return _value;
            }
            set {
                _value = value;
                UpdateText ();
                ValueChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when the Value property changes.</summary>
        public event EventHandler? ValueChanged;

        /// <summary>Gets or sets whether a checkbox is displayed to the left of the selected date. Stub in Modern.Forms.</summary>
        public bool ShowCheckBox { get; set; }

        /// <summary>Gets or sets whether the date/time value is enabled (meaningful when ShowCheckBox is true). Stub in Modern.Forms.</summary>
        public bool Checked { get; set; } = true;

        /// <summary>Gets or sets the calendar's foreground color. Stub in Modern.Forms.</summary>
        public System.Drawing.Color CalendarForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's background color. Stub in Modern.Forms.</summary>
        public System.Drawing.Color CalendarMonthBackground { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's title foreground color. Stub in Modern.Forms.</summary>
        public System.Drawing.Color CalendarTitleForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's title background color. Stub in Modern.Forms.</summary>
        public System.Drawing.Color CalendarTitleBackColor { get; set; } = System.Drawing.Color.Empty;

        private void UpdateText ()
        {
            var fmt = _format switch {
                DateTimePickerFormat.Custom => _customFormat,
                DateTimePickerFormat.Short => "d",
                DateTimePickerFormat.Time => "t",
                _ => "D"
            };
            Text = _value.ToString (fmt);
        }
    }
}
