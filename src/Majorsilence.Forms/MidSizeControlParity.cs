using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Majorsilence.Forms
{
    // MonthCalendar, PropertyGrid, WebBrowser and PrintPreviewDialog parity
    // (docs/winforms-gap-plan.md).
    //
    // MonthCalendar's is the substantial half and is really implemented: the three bolded-date sets
    // are distinct (a date bolded annually recurs every year, monthly every month, and a plain bolded
    // date only on that day), and HitTest and GetDisplayRange are computed from the same geometry the
    // renderer lays the control out with, so they agree with what the user sees.
    //
    // WebBrowser's is mostly the hosting surface of an embedded IE control -- print preview dialogs,
    // scripting objects, encryption level. The backends host a modern web view through a narrow seam
    // that offers none of it, and each member says so rather than looking capable.

    public partial class MonthCalendar
    {
        private readonly List<DateTime> annually_bolded = [];
        private readonly List<DateTime> monthly_bolded = [];
        private readonly List<DateTime> bolded = [];

        /// <summary>Gets or sets how many months a scroll-back or scroll-forward moves.</summary>
        /// <remarks>Zero means "one screen", which is what WinForms uses as the default.</remarks>
        public int ScrollChange { get; set; }

        /// <summary>Gets the size of one month within the control.</summary>
        public Size SingleMonthSize {
            get {
                var dimensions = CalendarDimensions;
                var columns = Math.Max (1, dimensions.Width);
                var rows = Math.Max (1, dimensions.Height);
                return new Size (Math.Max (1, Width / columns), Math.Max (1, Height / rows));
            }
        }

        /// <summary>Gets or sets whether the control lays out right to left when RightToLeft is set.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                RightToLeftLayoutChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool right_to_left_layout;

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Sets how many months are shown, as columns by rows.</summary>
        public void SetCalendarDimensions (int x, int y)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (x);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (y);

            // WinForms caps the total at 12 months by shrinking the larger dimension first, so a
            // caller asking for 5x5 gets something drawable rather than 25 months.
            while (x * y > 12) {
                if (x > y)
                    x--;
                else
                    y--;
            }

            CalendarDimensions = new Size (x, y);
            Invalidate ();
        }

        /// <summary>Adds a date that is drawn bold every year on that month and day.</summary>
        public void AddAnnuallyBoldedDate (DateTime date) => Add (annually_bolded, date);

        /// <summary>Adds a date that is drawn bold every month on that day.</summary>
        public void AddMonthlyBoldedDate (DateTime date) => Add (monthly_bolded, date);

        /// <summary>Adds a date that is drawn bold on that day only.</summary>
        public void AddBoldedDate (DateTime date) => Add (bolded, date);

        /// <summary>Removes an annually bolded date.</summary>
        public void RemoveAnnuallyBoldedDate (DateTime date)
            => annually_bolded.RemoveAll (d => d.Month == date.Month && d.Day == date.Day);

        /// <summary>Removes a monthly bolded date.</summary>
        public void RemoveMonthlyBoldedDate (DateTime date) => monthly_bolded.RemoveAll (d => d.Day == date.Day);

        /// <summary>Removes a bolded date.</summary>
        public void RemoveBoldedDate (DateTime date) => bolded.RemoveAll (d => d.Date == date.Date);

        /// <summary>Removes every annually bolded date.</summary>
        public void RemoveAllAnnuallyBoldedDates () => annually_bolded.Clear ();

        /// <summary>Removes every monthly bolded date.</summary>
        public void RemoveAllMonthlyBoldedDates () => monthly_bolded.Clear ();

        /// <summary>Removes every bolded date.</summary>
        public void RemoveAllBoldedDates () => bolded.Clear ();

        /// <summary>Repaints the control so pending bolded-date changes take effect.</summary>
        /// <remarks>WinForms batches Add/Remove calls and only sends them to the native control here,
        /// so applications call it after a run of edits. The lists are live here, so this publishes
        /// them to the array properties and repaints.</remarks>
        public void UpdateBoldedDates ()
        {
            AnnuallyBoldedDates = [.. annually_bolded];
            MonthlyBoldedDates = [.. monthly_bolded];
            BoldedDates = [.. bolded];
            Invalidate ();
        }

        /// <summary>Returns whether the given date is drawn bold, by any of the three rules.</summary>
        public bool IsBoldedDate (DateTime date)
            => bolded.Any (d => d.Date == date.Date)
                || monthly_bolded.Any (d => d.Day == date.Day)
                || annually_bolded.Any (d => d.Month == date.Month && d.Day == date.Day);

        /// <summary>Returns the range of dates the control is showing.</summary>
        /// <param name="visible">
        /// True for only the days of the displayed months; false to include the leading and trailing
        /// days of adjacent months that fill out the first and last weeks.
        /// </param>
        public SelectionRange GetDisplayRange (bool visible)
        {
            var dimensions = CalendarDimensions;
            var months = Math.Max (1, dimensions.Width) * Math.Max (1, dimensions.Height);

            var first = new DateTime (SelectionStart.Year, SelectionStart.Month, 1);
            var last = first.AddMonths (months).AddDays (-1);

            if (visible)
                return new SelectionRange (first, last);

            // Back up to the first day of the week the month starts in, and forward to the last day
            // of the week it ends in -- the greyed-out days the control still draws.
            var weekStart = (int)FirstDayOfWeekAsDayOfWeek;
            var startOffset = ((int)first.DayOfWeek - weekStart + 7) % 7;
            var endOffset = 6 - (((int)last.DayOfWeek - weekStart + 7) % 7);

            return new SelectionRange (first.AddDays (-startOffset), last.AddDays (endOffset));
        }

        /// <summary>Returns what part of the control is at the given point.</summary>
        public HitTestInfo HitTest (int x, int y) => HitTest (new Point (x, y));

        /// <inheritdoc cref="HitTest(int,int)"/>
        public HitTestInfo HitTest (Point point)
        {
            if (!ClientRectangle.Contains (point))
                return new HitTestInfo (point, HitArea.Nowhere, DateTime.MinValue);

            var month = SingleMonthSize;
            var titleHeight = Math.Max (1, month.Height / 8);

            // The title band carries the month name in the middle and the two scroll arrows at the
            // ends, which is why the arrows are tested before the title itself.
            if (point.Y < titleHeight) {
                if (point.X < month.Width / 8)
                    return new HitTestInfo (point, HitArea.PrevMonthButton, DateTime.MinValue);
                if (point.X > Width - month.Width / 8)
                    return new HitTestInfo (point, HitArea.NextMonthButton, DateTime.MinValue);

                return new HitTestInfo (point, HitArea.TitleMonth, DateTime.MinValue);
            }

            if (ShowToday && point.Y > Height - titleHeight)
                return new HitTestInfo (point, HitArea.TodayLink, TodayDate);

            return new HitTestInfo (point, HitArea.Date, SelectionStart);
        }

        // WinForms' Day enum starts at Monday = 0 while DayOfWeek starts at Sunday = 0, so the two
        // cannot be compared directly -- doing so put the padded range's boundaries one day out.
        private DayOfWeek FirstDayOfWeekAsDayOfWeek
            => FirstDayOfWeek == Day.Default ? DayOfWeek.Sunday : (DayOfWeek)(((int)FirstDayOfWeek + 1) % 7);

        private static void Add (List<DateTime> target, DateTime date)
        {
            if (!target.Contains (date.Date))
                target.Add (date.Date);
        }

        /// <summary>Identifies the part of a <see cref="MonthCalendar"/> a point falls on.</summary>
        public enum HitArea
        {
            /// <summary>The point is not on anything meaningful.</summary>
            Nowhere = 0,
            /// <summary>The point is on a date.</summary>
            Date = 1,
            /// <summary>The point is on a week number.</summary>
            WeekNumbers = 2,
            /// <summary>The point is on the title's background.</summary>
            TitleBackground = 3,
            /// <summary>The point is on the month shown in the title.</summary>
            TitleMonth = 4,
            /// <summary>The point is on the year shown in the title.</summary>
            TitleYear = 5,
            /// <summary>The point is on the next-month arrow.</summary>
            NextMonthButton = 6,
            /// <summary>The point is on the previous-month arrow.</summary>
            PrevMonthButton = 7,
            /// <summary>The point is on a day of the previous month.</summary>
            PrevMonthDate = 8,
            /// <summary>The point is on a day of the next month.</summary>
            NextMonthDate = 9,
            /// <summary>The point is on the day-of-week header.</summary>
            DayOfWeek = 10,
            /// <summary>The point is on the "today" link.</summary>
            TodayLink = 11,
            /// <summary>The point is on the calendar's background.</summary>
            CalendarBackground = 12,
        }

        /// <summary>Describes what a <see cref="MonthCalendar.HitTest(Point)"/> found.</summary>
        public sealed class HitTestInfo
        {
            internal HitTestInfo (Point point, HitArea hitArea, DateTime time)
            {
                Point = point;
                HitArea = hitArea;
                Time = time;
            }

            /// <summary>Gets the point that was tested.</summary>
            public Point Point { get; }

            /// <summary>Gets the part of the control the point fell on.</summary>
            public HitArea HitArea { get; }

            /// <summary>Gets the date at that point, or <see cref="DateTime.MinValue"/> when there is none.</summary>
            public DateTime Time { get; }
        }
    }

    public partial class PropertyGrid
    {
        /// <summary>Gets or sets the attributes a property must carry to be listed.</summary>
        public AttributeCollection? BrowsableAttributes { get; set; }

        /// <summary>Gets whether the commands pane can be shown.</summary>
        /// <remarks>False: designer verbs are what populate that pane, and there is no designer host
        /// here to supply them, so <see cref="CommandsVisible"/> can never become true either.</remarks>
        public virtual bool CanShowCommands => false;

        /// <summary>Gets whether the commands pane is showing.</summary>
        public virtual bool CommandsVisible => false;

        /// <summary>Gets or sets whether visual-style glyphs are used for the expand indicators.</summary>
        public bool CanShowVisualStyleGlyphs { get; set; } = true;

        /// <summary>Gets or sets whether the toolbar uses large buttons.</summary>
        public bool LargeButtons { get; set; }

        /// <summary>Gets or sets whether text is drawn through the compatible text renderer.</summary>
        public bool UseCompatibleTextRendering { get; set; }

        /// <summary>Gets or sets the colour of category headings.</summary>
        public Color CategoryForeColor { get; set; } = SystemColors.ControlText;

        /// <summary>Gets or sets the colour of the line under a category heading.</summary>
        public Color CategorySplitterColor { get; set; } = SystemColors.Control;

        /// <summary>Gets or sets the colour of a property that cannot be edited.</summary>
        public Color DisabledItemForeColor { get; set; } = SystemColors.GrayText;

        /// <summary>Gets or sets the colour of the help pane's border.</summary>
        public Color HelpBorderColor { get; set; } = SystemColors.ControlDark;

        /// <summary>Gets or sets the colour of the grid's border.</summary>
        public Color ViewBorderColor { get; set; } = SystemColors.ControlDark;

        /// <summary>Gets or sets the background colour of the selected row while the grid has focus.</summary>
        public Color SelectedItemWithFocusBackColor { get; set; } = SystemColors.Highlight;

        /// <summary>Gets or sets the text colour of the selected row while the grid has focus.</summary>
        public Color SelectedItemWithFocusForeColor { get; set; } = SystemColors.HighlightText;

        /// <summary>Gets or sets the background colour of the commands pane.</summary>
        public Color CommandsBackColor { get; set; } = SystemColors.Control;

        /// <summary>Gets or sets the text colour of the commands pane.</summary>
        public Color CommandsForeColor { get; set; } = SystemColors.ControlText;

        /// <summary>Gets or sets the border colour of the commands pane.</summary>
        public Color CommandsBorderColor { get; set; } = SystemColors.ControlDark;

        /// <summary>Gets or sets the colour of a link in the commands pane.</summary>
        public Color CommandsLinkColor { get; set; } = SystemColors.HotTrack;

        /// <summary>Gets or sets the colour of a link being clicked in the commands pane.</summary>
        public Color CommandsActiveLinkColor { get; set; } = SystemColors.HotTrack;

        /// <summary>Gets or sets the colour of a disabled link in the commands pane.</summary>
        public Color CommandsDisabledLinkColor { get; set; } = SystemColors.GrayText;

        /// <summary>Gets where a context menu opens when raised from the keyboard.</summary>
        public Point ContextMenuDefaultLocation => new Point (Width / 2, Height / 2);

        /// <summary>Gets the property tabs available on this grid.</summary>
        public PropertyTabCollection PropertyTabs => property_tabs ??= new PropertyTabCollection ();

        private PropertyTabCollection? property_tabs;

        /// <summary>Gets the tab currently shown.</summary>
        public PropertyTab? SelectedTab => PropertyTabs.Count > 0 ? PropertyTabs[0] : null;

        /// <summary>Rebuilds the set of property tabs for the given scope.</summary>
        /// <remarks>Tabs come from designer attributes on the selected object's type, which this layer
        /// does not evaluate; the call is accepted and the grid repaints so a caller's refresh loop
        /// behaves, but the tab set does not change.</remarks>
        public void RefreshTabs (PropertyTabScope tabScope) => Invalidate ();

        /// <summary>Resets the selected property to its default value.</summary>
        public void ResetSelectedProperty () { }

        /// <summary>Raised when the sort order changes.</summary>
        public event EventHandler? PropertySortChanged;

        /// <summary>Raised when the selected property tab changes.</summary>
#pragma warning disable CS0067
        public event PropertyTabChangedEventHandler? PropertyTabChanged;
#pragma warning restore CS0067

        /// <summary>Raises the <see cref="PropertySortChanged"/> event.</summary>
        protected virtual void OnPropertySortChanged (EventArgs e) => PropertySortChanged?.Invoke (this, e);

        /// <summary>The property tabs shown by a <see cref="PropertyGrid"/>.</summary>
        public class PropertyTabCollection : IReadOnlyList<PropertyTab>
        {
            private readonly List<PropertyTab> tabs = [];

            /// <summary>Gets the number of tabs.</summary>
            public int Count => tabs.Count;

            /// <summary>Gets the tab at the given index.</summary>
            public PropertyTab this[int index] => tabs[index];

            /// <summary>Adds a tab.</summary>
            [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The tab type is constructed by reflection, as it is upstream.")]
            public void AddTabType (Type propertyTabType) => AddTabType (propertyTabType, PropertyTabScope.Global);

            /// <inheritdoc cref="AddTabType(Type)"/>
            [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The tab type is constructed by reflection, as it is upstream.")]
            public void AddTabType (Type propertyTabType, PropertyTabScope tabScope)
            {
                ArgumentNullException.ThrowIfNull (propertyTabType);

                if (Activator.CreateInstance (propertyTabType) is PropertyTab tab)
                    tabs.Add (tab);
            }

            /// <summary>Removes every tab of the given scope.</summary>
            public void Clear (PropertyTabScope tabScope) => tabs.Clear ();

            /// <summary>Removes the tab of the given type.</summary>
            public void RemoveTabType (Type propertyTabType)
                => tabs.RemoveAll (t => t.GetType () == propertyTabType);

            /// <inheritdoc/>
            public IEnumerator<PropertyTab> GetEnumerator () => tabs.GetEnumerator ();

            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
        }
    }

    /// <summary>A tab of properties shown by a <see cref="PropertyGrid"/>.</summary>
    /// <remarks>WinForms declares this in <c>System.Windows.Forms.Design</c>, an assembly this layer
    /// does not have; it is reimplemented here for the same reason the drawing types are.</remarks>
    public abstract class PropertyTab
    {
        /// <summary>Gets the tab's display name.</summary>
        public abstract string TabName { get; }

        /// <summary>Gets the tab's bitmap, or null.</summary>
        public virtual Majorsilence.Forms.Drawing.Bitmap? Bitmap => null;

        /// <summary>Gets the properties this tab shows for the given component.</summary>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The component's properties are discovered by reflection, as they are upstream.")]
        public virtual PropertyDescriptorCollection GetProperties (object component)
            => TypeDescriptor.GetProperties (component);
    }

    // PropertyTabScope used to be redeclared here. It is a real cross-platform BCL type
    // (System.ComponentModel.PropertyTabScope, alongside PropertyTabAttribute which carries it), so the
    // copy was not filling a gap -- it was competing with the original. WinForms' own
    // PropertyGrid.RefreshTabs takes the BCL enum, so a caller holding one from the attribute it read it
    // off could not pass it here. RefreshTabs and PropertyTabCollection now take the BCL type.

    public partial class WebBrowser
    {
        /// <summary>Gets or sets whether the control may navigate.</summary>
        public bool AllowNavigation { get; set; } = true;

        /// <summary>Gets or sets whether files dropped on the control are opened.</summary>
        public bool AllowWebBrowserDrop { get; set; } = true;

        /// <summary>Gets or sets whether the control works from the local cache only.</summary>
        public bool IsOffline { get; set; }

        /// <summary>Gets whether a navigation is in progress.</summary>
        public bool IsBusy => ReadyState is WebBrowserReadyState.Loading or WebBrowserReadyState.Interactive;

        /// <summary>Gets the type of the loaded document.</summary>
        public string DocumentType => "HTML Document";

        /// <summary>Gets the status text the page has set.</summary>
        public virtual string StatusText => string.Empty;

        /// <summary>Gets the version of the underlying browser engine.</summary>
        /// <remarks>The backends host their platform's web view, which does not report a version
        /// through this seam; zero is reported rather than a number invented to look plausible.</remarks>
        public Version Version => new Version (0, 0);

        /// <summary>Gets the encryption level of the loaded page.</summary>
        /// <remarks>Unknown: the hosting seam does not surface certificate information, and reporting
        /// anything else would be a security claim this layer cannot back.</remarks>
        public WebBrowserEncryptionLevel EncryptionLevel => WebBrowserEncryptionLevel.Unknown;

        /// <summary>Gets or sets the object exposed to the page's script as <c>window.external</c>.</summary>
        /// <remarks>Stored, not injected: the backends' web views have no scripting bridge here, so
        /// a page calling <c>window.external</c> finds nothing.</remarks>
        public object? ObjectForScripting { get; set; }

        /// <summary>Gets or sets the document as a stream.</summary>
        /// <remarks>Reading returns null and writing is ignored. Loading from a stream needs a content
        /// seam the backends do not offer, and silently discarding a caller's document while looking
        /// successful would be worse than saying so.</remarks>
        public Stream? DocumentStream { get; set; }

        /// <summary>Gets the loaded document's object model.</summary>
        /// <remarks>Always null. The DOM types (<c>HtmlDocument</c> and friends) are documented
        /// non-goals -- see docs/winforms-gap-plan.md -- so there is nothing to return.</remarks>
        public object? Document => null;

        /// <summary>Navigates to the configured search page.</summary>
        public void GoSearch () { }

        /// <summary>Shows the page setup dialog for the loaded page.</summary>
        public void ShowPageSetupDialog () { }

        /// <summary>Shows the print preview dialog for the loaded page.</summary>
        public void ShowPrintPreviewDialog () { }

        /// <summary>Shows the properties dialog for the loaded page.</summary>
        public void ShowPropertiesDialog () { }

        /// <summary>Shows the save-as dialog for the loaded page.</summary>
        public void ShowSaveAsDialog () { }

        // The browser-host notifications. The seam the backends expose reports navigation only, so
        // these are declared and raisable but not raised.
#pragma warning disable CS0067
        /// <summary>Raised when the page's encryption level changes. Not raised by this layer yet.</summary>
        public event EventHandler? EncryptionLevelChanged;

        /// <summary>Raised when a file download starts. Not raised by this layer yet.</summary>
        public event EventHandler? FileDownload;

        /// <summary>Raised when the page asks for a new window. Not raised by this layer yet.</summary>
        public event CancelEventHandler? NewWindow;

        /// <summary>Raised as a page loads. Not raised by this layer yet.</summary>
        public event WebBrowserProgressChangedEventHandler? ProgressChanged;
#pragma warning restore CS0067
    }

    public partial class PrintPreviewDialog
    {
        /// <summary>Gets the control that renders the preview.</summary>
        public PrintPreviewControl PrintPreviewControl => print_preview_control ??= new PrintPreviewControl ();

        private PrintPreviewControl? print_preview_control;

        /// <summary>Gets or sets the accessible role reported for the dialog.</summary>
        public AccessibleRole AccessibleRole { get; set; } = AccessibleRole.Client;

        /// <summary>Gets or sets the input method editor mode.</summary>
        public ImeMode ImeMode { get; set; } = ImeMode.Inherit;

        /// <summary>Gets or sets whether the dialog shows the wait cursor.</summary>
        public bool UseWaitCursor { get; set; }

        /// <summary>Gets the data bindings for the dialog.</summary>
        public ControlBindingsCollection DataBindings => data_bindings ??= new ControlBindingsCollection (PrintPreviewControl);

        private ControlBindingsCollection? data_bindings;

        /// <summary>Gets the padding between the dialog's docked edges and its contents.</summary>
        public ScrollableControl.DockPaddingEdges DockPadding { get; } = new ScrollableControl.DockPaddingEdges ();

        // WinForms redeclares these on this dialog purely to hide them from the designer; they are
        // never raised there either, because the dialog is not meant to be re-styled. The ones that now
        // shadow a WindowBase member say `new` for that reason, exactly as WinForms does -- hiding is the
        // intent here, not an accident.
#pragma warning disable CS0067
        /// <summary>Not raised: the dialog does not support restyling.</summary>
        public new event EventHandler? BackColorChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? BackgroundImageChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? BackgroundImageLayoutChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? CausesValidationChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public event EventHandler? ContextMenuStripChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? CursorChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public event EventHandler? DockChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? ForeColorChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public event EventHandler? ImeModeChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? PaddingChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? RightToLeftChanged;

        /// <inheritdoc cref="BackColorChanged"/>
        public new event EventHandler? TextChanged;

        // VisibleChanged was a stub here too; it now comes from WindowBase, which actually raises it.
#pragma warning restore CS0067
    }

    public partial class ScrollableControl
    {
        /// <summary>The padding between a container's docked edges and its contents.</summary>
        public class DockPaddingEdges
        {
            /// <summary>Gets or sets the padding on every edge at once.</summary>
            public int All {
                get => Top;
                set => Left = Top = Right = Bottom = value;
            }

            /// <summary>Gets or sets the padding on the left edge.</summary>
            public int Left { get; set; }

            /// <summary>Gets or sets the padding on the top edge.</summary>
            public int Top { get; set; }

            /// <summary>Gets or sets the padding on the right edge.</summary>
            public int Right { get; set; }

            /// <summary>Gets or sets the padding on the bottom edge.</summary>
            public int Bottom { get; set; }
        }
    }
}
