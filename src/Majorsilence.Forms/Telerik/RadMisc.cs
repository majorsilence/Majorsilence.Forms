using System;
using System.Collections.Generic;
using Majorsilence.Forms;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat status strip. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadStatusStrip : Control
    {
        /// <summary>Gets the items hosted in the status strip.</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Sets whether the last item springs to fill remaining space. Stub.</summary>
        public void SetSpring (bool spring) { }
    }

    /// <summary>Telerik-compat command bar. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadCommandBar : Control
    {
        /// <summary>Gets the command-bar rows.</summary>
        public List<CommandBarRowElement> Rows { get; } = new ();
    }

    /// <summary>Telerik-compat command-bar row.</summary>
    public class CommandBarRowElement
    {
        /// <summary>Gets the strips in this row.</summary>
        public List<CommandBarStripElement> Strips { get; } = new ();
    }

    /// <summary>Telerik-compat command-bar strip.</summary>
    public class CommandBarStripElement
    {
        /// <summary>Gets the items in this strip.</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the text.</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat menu. Backed by <see cref="Majorsilence.Forms.Menu"/>.</summary>
    public class RadMenu : Menu { }

    /// <summary>Telerik-compat menu item. Backed by <see cref="Majorsilence.Forms.MenuItem"/>.</summary>
    public class RadMenuItem : MenuItem
    {
        /// <summary>Initializes a new instance.</summary>
        public RadMenuItem () { }
        /// <summary>Initializes a new instance with the specified text.</summary>
        public RadMenuItem (string text) : base (text) { }
        /// <summary>Initializes a new instance with the specified text and tag (Telerik (text, data) ctor).</summary>
        public RadMenuItem (string text, object? tag) : base (text) { Tag = tag; }
    }

    /// <summary>Telerik-compat menu separator.</summary>
    public class RadMenuSeparatorItem : MenuSeparatorItem { }

    /// <summary>Telerik-compat property grid. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadPropertyGrid : PropertyGrid
    {
        // SelectedObject, PropertySort, ToolbarVisible, SelectedGridItem and the rendering of the
        // inspected object's properties are inherited from Majorsilence.Forms.PropertyGrid.

        /// <summary>Gets the property items. Stub list (Telerik exposes individual property items).</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Gets the property groups. Stub list.</summary>
        public List<object> Groups { get; } = new ();
        /// <summary>Gets or sets the sort order. Stub.</summary>
        public object? SortOrder { get; set; }
        /// <summary>Gets or sets whether sorting is enabled. Stub.</summary>
        public bool EnableSorting { get; set; } = true;
        /// <summary>Gets the sort descriptors. Stub list.</summary>
        public List<object> SortDescriptors { get; } = new ();
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Begins editing the selected item. Stub.</summary>
        public void BeginEdit () { }

        /// <summary>Raised when an item is being formatted. Stub.</summary>
        public event EventHandler? ItemFormatting { add { } remove { } }
        /// <summary>Raised when an item has been edited. Stub.</summary>
        public event EventHandler? Edited { add { } remove { } }
        /// <summary>Raised when an editor is initialized. Stub.</summary>
        public event EventHandler? EditorInitialized { add { } remove { } }
        /// <summary>Raised when an editor is required. Stub.</summary>
        public event EventHandler? EditorRequired { add { } remove { } }
        /// <summary>Raised on item mouse-click. Stub.</summary>
        public event EventHandler? ItemMouseClick { add { } remove { } }
        /// <summary>Raised when the context menu is opening. Stub.</summary>
        public event EventHandler? ContextMenuOpening { add { } remove { } }
    }

    /// <summary>Telerik-compat scheduler. Backed by <see cref="Majorsilence.Forms.Control"/>. No scheduling UI is implemented.</summary>
    public class RadScheduler : Control
    {
        /// <summary>Gets or sets the active view. Stub.</summary>
        public object? ActiveView { get; set; }
        /// <summary>Gets or sets the active view type.</summary>
        public SchedulerViewType ActiveViewType { get; set; } = SchedulerViewType.Week;
        /// <summary>Gets the appointments. Stub list.</summary>
        public List<object> Appointments { get; } = new ();
        /// <summary>Gets or sets whether inline appointment creation is allowed. Stub.</summary>
        public bool AllowAppointmentCreateInline { get; set; } = true;

        /// <summary>Gets the month view. Stub.</summary>
        public object GetMonthView () => new object ();
        /// <summary>Gets the week view. Stub.</summary>
        public object GetWeekView () => new object ();
        /// <summary>Gets the timeline view. Stub.</summary>
        public object GetTimelineView () => new object ();
        /// <summary>Gets the day view. Stub.</summary>
        public object GetDayView () => new object ();

        /// <summary>Raised when the active view changes. Stub.</summary>
        public event EventHandler? ActiveViewChanged { add { } remove { } }
        /// <summary>Raised when a screen tip is needed. Stub.</summary>
        public event EventHandler? ScreenTipNeeded { add { } remove { } }
        /// <summary>Raised when the appointment edit dialog is showing. Stub.</summary>
        public event EventHandler? AppointmentEditDialogShowing { add { } remove { } }
        /// <summary>Raised when the context menu is opening. Stub.</summary>
        public event EventHandler? ContextMenuOpening { add { } remove { } }
        /// <summary>Raised when the view is navigated. Stub.</summary>
        public event EventHandler? ViewNavigated { add { } remove { } }
    }

    /// <summary>Telerik-compat scheduler navigator. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadSchedulerNavigator : Control
    {
        /// <summary>Gets or sets the associated scheduler.</summary>
        public RadScheduler? AssociatedScheduler { get; set; }
        /// <summary>Gets or sets whether the weekend check box is shown.</summary>
        public bool ShowWeekendCheckBox { get; set; }
        /// <summary>Gets or sets the date format. Stub.</summary>
        public string DateFormat { get; set; } = string.Empty;
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when navigating backwards. Stub.</summary>
        public event EventHandler? NavigateBackwardsClick { add { } remove { } }
        /// <summary>Raised when navigating forwards. Stub.</summary>
        public event EventHandler? NavigateForwardsClick { add { } remove { } }
        /// <summary>Raised when the weekend state changes. Stub.</summary>
        public event EventHandler? ShowWeekendStateChanged { add { } remove { } }
    }

    /// <summary>Telerik-compat layout control. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadLayoutControl : Panel
    {
        /// <summary>Gets the layout items.</summary>
        public List<LayoutControlItem> Items { get; } = new ();
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat layout item. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class LayoutControlItem : Panel { }

    /// <summary>Telerik-compat layout group. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class LayoutControlGroup : Panel { }

    /// <summary>Specifies a Telerik scheduler view type.</summary>
    public enum SchedulerViewType
    {
        /// <summary>Day view.</summary>
        Day = 0,
        /// <summary>Week view.</summary>
        Week = 1,
        /// <summary>Work-week view.</summary>
        WorkWeek = 2,
        /// <summary>Month view.</summary>
        Month = 3,
        /// <summary>Timeline view.</summary>
        Timeline = 4
    }
}
