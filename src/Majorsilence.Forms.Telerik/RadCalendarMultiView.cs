namespace Majorsilence.Forms.Telerik
{
    // RadCalendar's multi-month view, as Telerik's (measured on UI.for.WinForms 2026.2.520): the months are laid out
    // over the control's bounds, which neither grow to fit them nor decide how many there are -- a tall calendar with
    // one month shows one tall month. MultiViewRows by MultiViewColumns months are shown, without upstream
    // MonthCalendar's twelve-month cap, once AllowMultipleView is set; a row or column count set before then is
    // ignored, and turning it off goes back to one month.
    public partial class RadCalendar
    {
        private bool allow_multiple_view;
        private int multi_view_rows = 1;
        private int multi_view_columns = 1;

        /// <inheritdoc/>
        internal override bool SizesToMonths => false;

        /// <summary>Gets or sets whether the calendar shows <see cref="MultiViewRows"/> by <see cref="MultiViewColumns"/> months. Default false.</summary>
        /// <remarks>Turning it off sets both counts back to one.</remarks>
        public bool AllowMultipleView {
            get => allow_multiple_view;
            set {
                if (allow_multiple_view == value)
                    return;

                allow_multiple_view = value;

                if (!value) {
                    multi_view_rows = 1;
                    multi_view_columns = 1;
                }

                ApplyMultiView ();
            }
        }

        /// <summary>Gets or sets how many rows of months a multiple view shows. Default 1.</summary>
        /// <remarks>Ignored unless <see cref="AllowMultipleView"/> is set. A count below one shows one row.</remarks>
        public int MultiViewRows {
            get => multi_view_rows;
            set {
                if (!allow_multiple_view)
                    return;

                multi_view_rows = value;
                ApplyMultiView ();
            }
        }

        /// <summary>Gets or sets how many columns of months a multiple view shows. Default 1.</summary>
        /// <remarks>Ignored unless <see cref="AllowMultipleView"/> is set. A count below one shows one column.</remarks>
        public int MultiViewColumns {
            get => multi_view_columns;
            set {
                if (!allow_multiple_view)
                    return;

                multi_view_columns = value;
                ApplyMultiView ();
            }
        }

        private void ApplyMultiView ()
            => SetMonthLayout (allow_multiple_view ? multi_view_columns : 1, allow_multiple_view ? multi_view_rows : 1);
    }
}
