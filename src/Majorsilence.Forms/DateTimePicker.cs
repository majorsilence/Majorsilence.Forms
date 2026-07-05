using System.Drawing;

namespace Majorsilence.Forms
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
    /// Built on TimePicker for Majorsilence.Forms compatibility.
    /// </summary>
    public class DateTimePicker : TextBox
    {
        /// <summary>The minimum date value supported by the DateTimePicker (January 1, 1753).</summary>
        public static readonly DateTime MinDateTime = new DateTime (1753, 1, 1);

        /// <summary>The maximum date value supported by the DateTimePicker (December 31, 9998).</summary>
        public static readonly DateTime MaxDateTime = new DateTime (9998, 12, 31);

        private DateTime _value = DateTime.Now;
        private DateTime _min = MinDateTime;
        private DateTime _max = MaxDateTime;
        private string _customFormat = "G";
        private DateTimePickerFormat _format = DateTimePickerFormat.Long;

        /// <summary>Initializes a new instance of the DateTimePicker class.</summary>
        public DateTimePicker ()
        {
            UpdateText ();
        }

        /// <summary>Gets the minimum date/time value supported by the control.</summary>
        public static DateTime MinimumDateTime => MinDateTime;

        /// <summary>Gets the maximum date/time value supported by the control.</summary>
        public static DateTime MaximumDateTime => MaxDateTime;

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

        /// <summary>Gets or sets the font used for the calendar portion. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Font? CalendarFont { get; set; }

        /// <summary>Gets or sets the minimum date value.</summary>
        public DateTime MinDate {
            get => _min;
            set {
                if (value == _min)
                    return;

                if (value < MinDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"MinDate cannot be less than {MinDateTime}.");
                if (value > MaxDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"MinDate cannot be greater than {MaxDateTime}.");
                if (value > _max)
                    throw new ArgumentOutOfRangeException (nameof (value), value, "MinDate cannot be greater than MaxDate.");

                _min = value;

                // Coerce Value up to the new minimum if needed.
                if (_value < _min)
                    Value = _min;
            }
        }

        /// <summary>Gets or sets the maximum date value.</summary>
        public DateTime MaxDate {
            get => _max;
            set {
                if (value == _max)
                    return;

                if (value > MaxDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"MaxDate cannot be greater than {MaxDateTime}.");
                if (value < MinDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"MaxDate cannot be less than {MinDateTime}.");
                if (value < _min)
                    throw new ArgumentOutOfRangeException (nameof (value), value, "MaxDate cannot be less than MinDate.");

                _max = value;

                // Coerce Value down to the new maximum if needed.
                if (_value > _max)
                    Value = _max;
            }
        }

        /// <summary>Gets or sets the current date/time value.</summary>
        public DateTime Value {
            get => _value;
            set {
                if (value < MinDateTime || value > MaxDateTime)
                    throw new ArgumentOutOfRangeException (nameof (value), value, $"Value must be between {MinDateTime} and {MaxDateTime}.");
                if (value < _min || value > _max)
                    throw new ArgumentOutOfRangeException (nameof (value), value, "Value must be between MinDate and MaxDate.");

                if (value == _value)
                    return;

                _value = value;
                UpdateText ();
                OnValueChanged (EventArgs.Empty);
            }
        }

        /// <summary>Raised when the Value property changes.</summary>
        /// <summary>Raised when the drop-down calendar opens. Mirrors WinForms DateTimePicker.DropDown.</summary>
#pragma warning disable CS0067 // raised once the popup pipeline exposes open/close notifications
        public event EventHandler? DropDown;

        /// <summary>Raised when the drop-down calendar closes. Mirrors WinForms DateTimePicker.CloseUp.</summary>
        public event EventHandler? CloseUp;
#pragma warning restore CS0067

        /// <summary>Raised when the date value changes. Mirrors WinForms DateTimePicker.ValueChanged.</summary>
        public event EventHandler? ValueChanged;

        /// <summary>Raises the ValueChanged event. Mirrors WinForms DateTimePicker.OnValueChanged.</summary>
        protected virtual void OnValueChanged (EventArgs e) => ValueChanged?.Invoke (this, e);

        /// <summary>Gets or sets whether a checkbox is displayed to the left of the selected date. Stub in Majorsilence.Forms.</summary>
        public bool ShowCheckBox { get; set; }

        /// <summary>Gets or sets whether the date/time value is enabled (meaningful when ShowCheckBox is true). Stub in Majorsilence.Forms.</summary>
        public bool Checked { get; set; } = true;

        /// <summary>Gets or sets the calendar's foreground color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's background color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarMonthBackground { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's title foreground color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarTitleForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's title background color. Stub in Majorsilence.Forms.</summary>
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
