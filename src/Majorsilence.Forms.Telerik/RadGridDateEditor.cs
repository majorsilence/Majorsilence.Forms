using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// The in-place calendar editor shown when a <see cref="GridViewDateTimeColumn"/> cell is opened for
    /// editing. Picking a date calls back with the chosen <see cref="DateTime"/>.
    /// </summary>
    internal static class RadGridDateEditor
    {
        public static void Show (RadGridView grid, DateTime? current, Point screenLocation, Action<DateTime> onPick)
        {
            if (grid.FindWindow () is not WindowBase window)
                return;

            const int width = 240;
            const int height = 200;

            var popup = new PopupWindow (window);
            var calendar = new MonthCalendar { Left = 0, Top = 0, Width = width, Height = height };

            // Seed the selection before subscribing so the programmatic set doesn't fire the callback.
            if (current.HasValue)
                calendar.SetDate (current.Value);

            calendar.DateChanged += (_, e) => {
                onPick (e.Start);
                popup.Hide ();
            };

            popup.Controls.Add (calendar);
            popup.Size = new Size (width, height);
            popup.Show (screenLocation);
        }
    }
}
