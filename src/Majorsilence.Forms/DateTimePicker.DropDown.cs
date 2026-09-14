using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // SMP-40 and SMP-41: the drop-down and the check box.
    //
    // The control painted a drop-down arrow and nothing hit-tested it: there was no OnMouseDown, no
    // popup anywhere, and DropDown/CloseUp sat under a CS0067 suppression. With SMP-39's free-form Text
    // as the only other way in, a DateTimePicker in a migrated app was a read-only display of today's
    // date.
    //
    // ShowCheckBox + Checked is the only way WinForms expresses an OPTIONAL date -- on virtually every
    // "date of X (optional)" field. Both were stored and read by nothing, so the user could neither
    // clear nor set the date and code reading Checked always got true, writing nulls as today.
    public partial class DateTimePicker
    {
        private PopupWindow? drop_down;
        private MonthCalendar? calendar;

        /// <summary>
        /// Gets the height the control needs to fit one line of text at its current font, as
        /// <c>System.Windows.Forms.DateTimePicker.PreferredHeight</c> does.
        /// </summary>
        /// <remarks>
        /// Reparenting to <see cref="Control"/> (SMP-39) lost this: it came from <c>TextBoxBase</c>.
        /// Upstream declares it on <c>DateTimePicker</c> itself, and the API gate caught the gap.
        /// </remarks>
        public int PreferredHeight
            => (int)Math.Ceiling (TextMeasurer.MeasureText ("Wg", this).Height)
                + Padding.Top + Padding.Bottom + 4;   // 4px matches the default border/inset

        /// <summary>The width of the drop-down (or spin) button strip at the control's right edge.</summary>
        internal int ButtonWidth => Math.Min (LogicalToDeviceUnits (16), ScaledSize.Width);

        /// <summary>The button strip's rectangle, in device pixels.</summary>
        internal Rectangle ButtonBounds => new Rectangle (ScaledSize.Width - ButtonWidth, 0, ButtonWidth, ScaledSize.Height);

        /// <summary>
        /// The check box's rectangle when <see cref="ShowCheckBox"/> is on, else empty. At the left,
        /// where upstream puts it.
        /// </summary>
        internal Rectangle CheckBoxBounds {
            get {
                if (!ShowCheckBox)
                    return Rectangle.Empty;

                var size = LogicalToDeviceUnits (12);
                var inset = LogicalToDeviceUnits (2);

                return new Rectangle (inset, Math.Max (0, (ScaledSize.Height - size) / 2), size, size);
            }
        }

        /// <summary>Where the date text is drawn: right of the check box, left of the button strip.</summary>
        internal Rectangle TextBounds {
            get {
                var left = ShowCheckBox ? CheckBoxBounds.Right + LogicalToDeviceUnits (3) : LogicalToDeviceUnits (2);

                return new Rectangle (left, 0, Math.Max (0, ScaledSize.Width - ButtonWidth - left), ScaledSize.Height);
            }
        }

        /// <summary>Gets or sets whether the drop-down calendar is open.</summary>
        public bool DroppedDown {
            get => drop_down?.Visible == true;
            set {
                if (value)
                    OpenDropDown ();
                else
                    CloseDropDown ();
            }
        }

        private void OpenDropDown ()
        {
            // ShowUpDown replaces the calendar with a spin control upstream, so there is no drop-down
            // to open in that mode -- the arrows step the value instead.
            if (DroppedDown || ShowUpDown || !Enabled || FindWindow () is not WindowBase window)
                return;

            calendar ??= BuildCalendar ();
            drop_down ??= new PopupWindow (window);

            calendar.SelectionStart = Value;
            drop_down.Controls.Add (calendar);
            // The calendar's own preferred size: it knows how big a month grid is, and hard-coding one
            // here would clip it the moment its font or CalendarDimensions changed.
            var preferred = calendar.PreferredSize;
            drop_down.Size = preferred.Width > 0 && preferred.Height > 0 ? preferred : new Size (220, 162);
            calendar.Size = drop_down.Size;

            // DropDownAlign decides which edge of the control the popup lines up with.
            var x = DropDownAlign == LeftRightAlignment.Right
                ? DeviceToLogicalUnits (ScaledSize.Width) - drop_down.Size.Width
                : 0;

            drop_down.Show (this, x, DeviceToLogicalUnits (ScaledSize.Height));

            OnDropDown (EventArgs.Empty);
        }

        private void CloseDropDown ()
        {
            if (!DroppedDown)
                return;

            drop_down!.Hide ();
            OnCloseUp (EventArgs.Empty);
        }

        private MonthCalendar BuildCalendar ()
        {
            var built = new MonthCalendar {
                MinDate = MinDate,
                MaxDate = MaxDate,
            };

            // The Calendar* colours were stored and read by nothing (SMP-41). They are the drop-down's
            // colours, so this is where they land.
            if (CalendarFont is { } font)
                built.Font = font;

            if (!CalendarForeColor.IsEmpty)
                built.ForeColor = CalendarForeColor;

            if (!CalendarMonthBackground.IsEmpty)
                built.BackColor = CalendarMonthBackground;

            if (!CalendarTitleForeColor.IsEmpty)
                built.TitleForeColor = CalendarTitleForeColor;

            if (!CalendarTitleBackColor.IsEmpty)
                built.TitleBackColor = CalendarTitleBackColor;

            built.TrailingForeColor = CalendarTrailingForeColor;

            // Picking a date commits it and closes, which is what the drop-down is for. Checked comes on
            // with it: choosing a date in an optional-date field is how the user says there IS one.
            built.DateSelected += (_, e) => {
                Value = e.Start;

                if (ShowCheckBox)
                    Checked = true;

                CloseDropDown ();
            };

            return built;
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            Guard.ThrowIfNull (e);

            if (!Enabled || !e.Button.HasFlag (MouseButtons.Left))
                return;

            // The check box first: it sits inside the control's bounds and is the smaller target.
            if (ShowCheckBox && CheckBoxBounds.Contains (e.Location)) {
                Checked = !Checked;
                return;
            }

            if (ShowUpDown) {
                // In spin mode the strip is two half-height arrows, stepping the date by a day.
                if (!ButtonBounds.Contains (e.Location))
                    return;

                var up = e.Location.Y < ButtonBounds.Top + ButtonBounds.Height / 2;
                StepValue (up ? 1 : -1);
                return;
            }

            if (ButtonBounds.Contains (e.Location))
                DroppedDown = !DroppedDown;
        }

        // A day at a time, clamped -- assigning past MinDate/MaxDate would throw out of a mouse click.
        private void StepValue (int days)
        {
            var next = Value.AddDays (days);

            if (next < MinDate || next > MaxDate)
                return;

            Value = next;

            if (ShowCheckBox)
                Checked = true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Space toggles the check box and F4/Alt+Down opens the drop-down, which are the keyboard
        /// equivalents of the two mouse targets. Without them a keyboard-only user has no way to set an
        /// optional date at all.
        /// </remarks>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            Guard.ThrowIfNull (e);

            if (e.Handled || !Enabled)
                return;

            switch (e.KeyCode) {
                case Keys.Space when ShowCheckBox:
                    Checked = !Checked;
                    e.Handled = true;
                    return;

                case Keys.F4:
                case Keys.Down when e.Alt:
                    DroppedDown = !DroppedDown;
                    e.Handled = true;
                    return;

                case Keys.Escape when DroppedDown:
                    CloseDropDown ();
                    e.Handled = true;
                    return;

                case Keys.Up when ShowUpDown:
                    StepValue (1);
                    e.Handled = true;
                    return;

                case Keys.Down when ShowUpDown:
                    StepValue (-1);
                    e.Handled = true;
                    return;
            }
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                drop_down?.Dispose ();
                calendar?.Dispose ();
                drop_down = null;
                calendar = null;
            }

            base.Dispose (disposing);
        }
    }
}
