using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Specifies the date/time format displayed by a DateTimePicker.
    /// </summary>
    public enum DateTimePickerFormat
    {
        /// <summary>Long date format.</summary>
        Long = 1,
        /// <summary>Short date format.</summary>
        Short = 2,
        /// <summary>Time format.</summary>
        Time = 4,
        /// <summary>Custom format string.</summary>
        Custom = 8,
    }

    /// <summary>
    /// Represents a DateTimePicker control for selecting a date and/or time.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="Control"/>, as upstream (SMP-39). It derived from <see cref="TextBox"/>,
    /// which meant the user could delete the date and type anything with nothing validating it,
    /// <c>Text</c> was free-form and never parsed back into <see cref="Value"/>, the surface carried
    /// <c>Multiline</c>/<c>PasswordChar</c>/<c>AcceptsReturn</c>, and
    /// <c>foreach (Control c in ...) if (c is TextBox)</c> sweeps picked up every date picker on a form.
    /// </remarks>
    public partial class DateTimePicker : Control
    {
        /// <summary>Gets the default style for all date pickers: what a <c>DateTimePicker { ... }</c> theme rule sets.</summary>
        /// <remarks>Its own as of #100. The picker stopped deriving from TextBox when it gained a real
        /// drop-down calendar, and the theming reference still said the TextBox rule reached it.
        /// It keeps the input-field look a TextBox has -- a 1px frame on the input background -- which went
        /// with the base class: upstream's picker is always framed, and a bare one read as a label.</remarks>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

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

        /// <summary>
        /// Paints the WinForms-style drop-down button on the right edge, over any text that would
        /// otherwise underlap it. WinForms apps rely on this geometry: a common pattern sizes the
        /// picker down to ~16px so ONLY the button shows (a calendar button beside a separate text
        /// box); without it the compat picker painted its full formatted text into that sliver,
        /// producing clipped garbage.
        /// </summary>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            using var device = e.DeviceSpace ();   // laid out in device pixels (EVT-37)

            var font = GetEffectiveFont ();
            var font_size = LogicalToDeviceUnits (GetEffectiveFontSize ());

            // The date is greyed when the control is disabled OR when an optional date is unset -- the
            // ShowCheckBox/Checked pattern, which used to paint nothing at all (SMP-41).
            var has_value = !ShowCheckBox || Checked;
            var foreground = Enabled && has_value ? GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;

            if (ShowCheckBox)
                DrawCheckBox (e);

            e.Canvas.DrawText (base.Text, font, font_size, TextBounds, foreground, ContentAlignment.MiddleLeft, maxLines: 1);

            var glyph = Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;

            // Upstream's Windows 11 picker: the field drawn like an editable combo's, with its thin chevron
            // and no button face (see OnPaintBackground).
            if (PaintsUpstreamField && !ShowUpDown) {
                ComboBox.DrawUpstreamChevron (e, ScaledWidth, ScaledHeight, Enabled);
                return;
            }

            // The button face sits inside the frame, as upstream's does: filled edge to edge it painted
            // over the right half of the border, so the frame seemed to end where the button began.
            var rect = ButtonBounds;
            var border = LogicalToDeviceUnits (CurrentStyle.Border.GetWidth ());
            rect = Rectangle.FromLTRB (rect.Left, rect.Top + border, rect.Right - border, rect.Bottom - border);
            e.Canvas.FillRectangle (rect.X, rect.Y, rect.Width, rect.Height, Theme.ControlMidColor);

            if (ShowUpDown) {
                // ShowUpDown replaces the drop-down button with a spin control, as upstream does. It was
                // stored and the drop-down arrow was drawn regardless.
                var half = rect.Height / 2;
                ControlPaint.DrawArrowGlyph (e, new Rectangle (rect.X, rect.Y, rect.Width, half), glyph, ArrowDirection.Up);
                ControlPaint.DrawArrowGlyph (e, new Rectangle (rect.X, rect.Y + half, rect.Width, rect.Height - half), glyph, ArrowDirection.Down);
                return;
            }

            e.Canvas.DrawText ("▾", font, font_size, rect, glyph, ContentAlignment.MiddleCenter, maxLines: 1);
        }

        // As for a combo box (FieldUpstreamLook): a ported app that chose its font gets upstream's field.
        private bool PaintsUpstreamField => FieldUpstreamLook.Applies && BackgroundImage is null;

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            if (!PaintsUpstreamField) {
                base.OnPaintBackground (e);
                return;
            }

            using var device = e.DeviceSpace ();

            var face = Enabled ? FieldUpstreamLook.Window : FieldUpstreamLook.Control;
            FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.TextBoxOutline, FieldUpstreamLook.TextBoxBottom);
        }

        // The optional-date check box: a box, and a tick when the date is set.
        private void DrawCheckBox (PaintEventArgs e)
        {
            var box = CheckBoxBounds;

            e.Canvas.FillRectangle (box.X, box.Y, box.Width, box.Height, Enabled ? Theme.BackgroundColor : Theme.ControlMidColor);
            e.Canvas.DrawRectangle (box, Theme.BorderLowColor);

            if (!Checked)
                return;

            var tick = Rectangle.Inflate (box, -LogicalToDeviceUnits (3), -LogicalToDeviceUnits (3));

            if (tick.Width <= 0 || tick.Height <= 0)
                return;

            var ink = Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;
            var mid_x = tick.Left + tick.Width / 3;
            var mid_y = tick.Bottom - 1 - tick.Height / 4;

            e.Canvas.DrawLine (tick.Left, tick.Top + tick.Height / 2, mid_x, mid_y, ink);
            e.Canvas.DrawLine (mid_x, mid_y, tick.Right - 1, tick.Top, ink);
        }

        /// <summary>Gets the minimum date/time value supported by the control.</summary>
        public static DateTime MinimumDateTime => MinDateTime;

        /// <summary>Gets the maximum date/time value supported by the control.</summary>
        public static DateTime MaximumDateTime => MaxDateTime;

        /// <summary>Gets or sets the custom format string (used when Format = Custom).</summary>
        public string CustomFormat {
            get => _customFormat;
            set {
                if (_customFormat == (value ?? "G"))
                    return;

                _customFormat = value ?? "G";

                if (_format == DateTimePickerFormat.Custom)
                    UpdateText ();

                OnFormatChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the display format.</summary>
        public DateTimePickerFormat Format {
            get => _format;
            set {
                if (_format == value)
                    return;

                _format = value;
                UpdateText ();

                // The setter raised nothing, so a handler watching for a format change never ran
                // (SMP-40).
                OnFormatChanged (EventArgs.Empty);
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
        public event EventHandler? DropDown;

        /// <summary>Raised when the drop-down calendar closes. Mirrors WinForms DateTimePicker.CloseUp.</summary>
        public event EventHandler? CloseUp;

        /// <summary>Raised when the date value changes. Mirrors WinForms DateTimePicker.ValueChanged.</summary>
        public event EventHandler? ValueChanged;

        /// <summary>Raises the ValueChanged event. Mirrors WinForms DateTimePicker.OnValueChanged.</summary>
        protected virtual void OnValueChanged (EventArgs e) => ValueChanged?.Invoke (this, e);

        /// <summary>
        /// Gets or sets whether a check box is shown at the left of the date. With
        /// <see cref="Checked"/> it is the only way WinForms expresses an OPTIONAL date, and it is on
        /// virtually every "date of X (optional)" field (SMP-41).
        /// </summary>
        public bool ShowCheckBox {
            get => show_check_box;
            set {
                if (show_check_box == value)
                    return;

                show_check_box = value;
                Invalidate ();
            }
        }

        private bool show_check_box;

        /// <summary>
        /// Gets or sets whether the date is set. Meaningful when <see cref="ShowCheckBox"/> is on:
        /// false greys the date and means "no value", which is how a nullable date reaches the app.
        /// </summary>
        public bool Checked {
            get => is_checked;
            set {
                if (is_checked == value)
                    return;

                is_checked = value;
                OnValueChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool is_checked = true;

        /// <summary>Raises the <see cref="FormatChanged"/> event.</summary>
        protected virtual void OnFormatChanged (EventArgs e) => FormatChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="DropDown"/> event.</summary>
        protected virtual void OnDropDown (EventArgs e) => DropDown?.Invoke (this, e);

        /// <summary>Raises the <see cref="CloseUp"/> event.</summary>
        protected virtual void OnCloseUp (EventArgs e) => CloseUp?.Invoke (this, e);

        /// <summary>Gets or sets the calendar's foreground color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's background color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarMonthBackground { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's title foreground color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarTitleForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the calendar's title background color. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CalendarTitleBackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>
        /// Gets or sets the displayed date as text. Setting it PARSES into <see cref="Value"/>, as
        /// upstream does -- <c>dtp.Text = "2024-01-15"</c> is a common way to seed a picker from a
        /// string, and it used to display the text while leaving Value at today, so the app saved the
        /// wrong date (SMP-39).
        /// </summary>
        public override string Text {
            get => base.Text;
            set {
                // Empty resets to today, which is upstream's ResetValue.
                if (string.IsNullOrWhiteSpace (value)) {
                    Value = DateTime.Now;
                    return;
                }

                if (DateTime.TryParse (value, System.Globalization.CultureInfo.CurrentCulture,
                        System.Globalization.DateTimeStyles.None, out var parsed)
                    && parsed >= _min && parsed <= _max) {
                    Value = parsed;
                    return;
                }

                // Unparseable text is refused rather than displayed: a picker showing "asdf" while
                // Value reads today is the failure this finding describes.
                UpdateText ();
            }
        }

        /// <summary>The format string this control's <see cref="Format"/> selects.</summary>
        internal string FormatString => _format switch {
            DateTimePickerFormat.Custom => LiteralSeparators (_customFormat),
            DateTimePickerFormat.Short => "d",
            DateTimePickerFormat.Time => "t",
            _ => "D"
        };

        // Upstream's picker hands CustomFormat to the native control, whose format strings treat '/' and ':' as
        // the literal characters. In a .NET format string they mean the culture's date and time separators, so
        // CustomFormat = "MM/dd/yyyy" read "10-14-2026" under en-CA instead of "10/14/2026". Escapes them outside
        // quoted literals so .NET prints them as written.
        internal static string LiteralSeparators (string format)
        {
            if (string.IsNullOrEmpty (format) || (format.IndexOf ('/') < 0 && format.IndexOf (':') < 0))
                return format;

            var result = new System.Text.StringBuilder (format.Length + 4);
            char? quote = null;

            for (var i = 0; i < format.Length; i++) {
                var c = format[i];

                if (quote is not null) {
                    if (c == quote)
                        quote = null;
                } else if (c is '\'' or '"') {
                    quote = c;
                } else if (c == '\\' && i + 1 < format.Length) {
                    result.Append (c).Append (format[++i]);
                    continue;
                } else if (c is '/' or ':') {
                    result.Append ('\\');
                }

                result.Append (c);
            }

            return result.ToString ();
        }

        private void UpdateText ()
        {
            base.Text = _value.ToString (FormatString);
            Invalidate ();
        }
    }
}
