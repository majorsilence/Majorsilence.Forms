using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat filter comparison operators. Mirrors the commonly-used members of
    /// <c>Telerik.WinControls.Data.FilterOperator</c> so migrated filter code compiles and the
    /// in-UI filter popup can offer the same conditions.
    /// </summary>
    public enum FilterOperator
    {
        /// <summary>No condition (the descriptor matches everything unless a value set is supplied).</summary>
        None,
        /// <summary>The cell text contains the value.</summary>
        Contains,
        /// <summary>The cell text does not contain the value.</summary>
        NotContains,
        /// <summary>The cell text equals the value.</summary>
        IsEqualTo,
        /// <summary>The cell text does not equal the value.</summary>
        IsNotEqualTo,
        /// <summary>The cell text starts with the value.</summary>
        StartsWith,
        /// <summary>The cell text ends with the value.</summary>
        EndsWith,
        /// <summary>The cell value is greater than the value.</summary>
        IsGreaterThan,
        /// <summary>The cell value is greater than or equal to the value.</summary>
        IsGreaterThanOrEqualTo,
        /// <summary>The cell value is less than the value.</summary>
        IsLessThan,
        /// <summary>The cell value is less than or equal to the value.</summary>
        IsLessThanOrEqualTo,
        /// <summary>The cell value is null or empty.</summary>
        IsNull,
        /// <summary>The cell value is not null or empty.</summary>
        IsNotNull
    }

    /// <summary>
    /// Telerik-compat filter descriptor. Describes a per-column filter as either an Excel-style
    /// distinct-value selection (<see cref="SelectedValues"/>) and/or a single
    /// <see cref="Operator"/>/<see cref="Value"/> condition. Both styles round-trip through the
    /// grid's layout XML.
    /// </summary>
    public class FilterDescriptor
    {
        /// <summary>Whether the descriptor came from the filter editor UI. Stored for compat.</summary>
        public bool IsFilterEditor { get; set; }

        /// <summary>Creates a copy of this descriptor.</summary>
        public FilterDescriptor Clone () => (FilterDescriptor)MemberwiseClone ();

        /// <summary>Initializes a new, empty filter descriptor.</summary>
        public FilterDescriptor () { }

        /// <summary>Initializes a filter descriptor with a condition.</summary>
        public FilterDescriptor (string propertyName, FilterOperator op, object? value)
        {
            PropertyName = propertyName;
            Operator = op;
            Value = value;
        }

        /// <summary>Gets or sets the name (column Name/FieldName) the filter applies to.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the comparison operator.</summary>
        public FilterOperator Operator { get; set; } = FilterOperator.None;

        /// <summary>Gets or sets the value compared against the cell.</summary>
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets the set of selected distinct display values (Excel-style checklist). When set,
        /// a row matches only if its cell display text is in this set. Use null to rely solely on
        /// <see cref="Operator"/>/<see cref="Value"/>. The set is case-insensitive.
        /// </summary>
        public HashSet<string>? SelectedValues { get; set; }

        /// <summary>Gets or sets an optional second comparison operator (for a two-condition filter).</summary>
        public FilterOperator SecondOperator { get; set; } = FilterOperator.None;
        /// <summary>Gets or sets the value for the second condition.</summary>
        public object? SecondValue { get; set; }
        /// <summary>When true the two conditions are OR-combined; otherwise AND. Default AND.</summary>
        public bool CombineWithOr { get; set; }

        /// <summary>Gets whether this descriptor would actually filter anything.</summary>
        public bool IsActive => SelectedValues is not null || Operator != FilterOperator.None || SecondOperator != FilterOperator.None;

        /// <summary>Returns whether the supplied cell display text passes this filter.</summary>
        public bool Matches (string cellText)
        {
            cellText ??= string.Empty;

            if (SelectedValues is not null && !SelectedValues.Contains (cellText))
                return false;

            var first = Evaluate (Operator, cellText, Value);
            if (SecondOperator == FilterOperator.None)
                return first;

            var second = Evaluate (SecondOperator, cellText, SecondValue);
            return CombineWithOr ? first || second : first && second;
        }

        // Evaluates a single operator against the cell text (None matches everything).
        private static bool Evaluate (FilterOperator op, string cellText, object? value)
        {
            if (op == FilterOperator.None)
                return true;

            var target = value?.ToString () ?? string.Empty;

            switch (op) {
                case FilterOperator.Contains:
                    return cellText.Contains (target, StringComparison.CurrentCultureIgnoreCase);
                case FilterOperator.NotContains:
                    return cellText.IndexOf (target, StringComparison.CurrentCultureIgnoreCase) < 0;
                case FilterOperator.IsEqualTo:
                    return Compare (cellText, target) == 0;
                case FilterOperator.IsNotEqualTo:
                    return Compare (cellText, target) != 0;
                case FilterOperator.StartsWith:
                    return cellText.StartsWith (target, StringComparison.CurrentCultureIgnoreCase);
                case FilterOperator.EndsWith:
                    return cellText.EndsWith (target, StringComparison.CurrentCultureIgnoreCase);
                case FilterOperator.IsGreaterThan:
                    return Compare (cellText, target) > 0;
                case FilterOperator.IsGreaterThanOrEqualTo:
                    return Compare (cellText, target) >= 0;
                case FilterOperator.IsLessThan:
                    return Compare (cellText, target) < 0;
                case FilterOperator.IsLessThanOrEqualTo:
                    return Compare (cellText, target) <= 0;
                case FilterOperator.IsNull:
                    return string.IsNullOrEmpty (cellText);
                case FilterOperator.IsNotNull:
                    return !string.IsNullOrEmpty (cellText);
                default:
                    return true;
            }
        }

        // Numeric/date-aware comparison, falling back to a case-insensitive string compare. The numeric
        // parse allows currency symbols and thousands separators so a formatted column (e.g. "$85,000"
        // from a "C0" FormatString) still sorts and filters numerically.
        private const NumberStyles NumericStyles = NumberStyles.Any | NumberStyles.AllowCurrencySymbol;

        internal static int Compare (string a, string b)
        {
            if (double.TryParse (a, NumericStyles, CultureInfo.CurrentCulture, out var na)
                && double.TryParse (b, NumericStyles, CultureInfo.CurrentCulture, out var nb))
                return na.CompareTo (nb);

            if (DateTime.TryParse (a, CultureInfo.CurrentCulture, DateTimeStyles.None, out var da)
                && DateTime.TryParse (b, CultureInfo.CurrentCulture, DateTimeStyles.None, out var db))
                return da.CompareTo (db);

            return string.Compare (a, b, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    /// <summary>Telerik-compat sort descriptor. Mirrors <c>SortDescriptor</c>.</summary>
    public class SortDescriptor
    {
        /// <summary>Initializes a new sort descriptor.</summary>
        public SortDescriptor () { }

        /// <summary>Initializes a sort descriptor for a column and direction.</summary>
        public SortDescriptor (string propertyName, ListSortDirection direction)
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        /// <summary>Gets or sets the name (column Name/FieldName) to sort by.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the sort direction.</summary>
        public ListSortDirection Direction { get; set; } = ListSortDirection.Ascending;
    }

    /// <summary>Telerik-compat group descriptor. Mirrors <c>GroupDescriptor</c>.</summary>
    public class GroupDescriptor
    {
        /// <summary>Initializes a new group descriptor.</summary>
        public GroupDescriptor () { }

        /// <summary>Initializes a group descriptor for a column and direction.</summary>
        public GroupDescriptor (string propertyName, ListSortDirection direction = ListSortDirection.Ascending)
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        /// <summary>Gets or sets the name (column Name/FieldName) to group by.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the order groups are arranged in.</summary>
        public ListSortDirection Direction { get; set; } = ListSortDirection.Ascending;
    }

    /// <summary>Telerik-compat pinned-column position. Mirrors <c>PinnedColumnPosition</c>.</summary>
    public enum PinnedColumnPosition
    {
        /// <summary>The column is not pinned.</summary>
        None,
        /// <summary>The column is pinned to the left (frozen during horizontal scroll).</summary>
        Left,
        /// <summary>The column is pinned to the right edge (frozen during horizontal scroll).</summary>
        Right
    }

    /// <summary>
    /// A child (detail) view shown under an expanded master row in a master-detail RadGridView: a simple
    /// read-only table with its own columns and rows. Returned by <c>RadGridView.ChildViewProvider</c>.
    /// </summary>
    public sealed class GridChildView
    {
        /// <summary>Initializes an empty child view.</summary>
        public GridChildView () { }

        /// <summary>Initializes a child view with the given column headers.</summary>
        public GridChildView (params string[] columns) => Columns.AddRange (columns);

        /// <summary>Gets the child column headers.</summary>
        public List<string> Columns { get; } = new ();

        /// <summary>Gets the child rows (each an array of cell display strings aligned to <see cref="Columns"/>).</summary>
        public List<string[]> Rows { get; } = new ();

        /// <summary>Adds a child row.</summary>
        public void AddRow (params string[] cells) => Rows.Add (cells);
    }

    /// <summary>Telerik-compat column auto-size mode. Mirrors the common <c>GridViewAutoSizeColumnsMode</c> values.</summary>
    public enum GridViewAutoSizeColumnsMode
    {
        /// <summary>Columns keep their explicit widths.</summary>
        None,
        /// <summary>Visible columns are sized (by <see cref="DataGridViewColumn.FillWeight"/>) to fill the viewport width.</summary>
        Fill
    }

    /// <summary>Telerik-compat condition types for <see cref="ConditionalFormattingObject"/>. Mirrors <c>ConditionTypes</c>.</summary>
    public enum ConditionTypes
    {
        /// <summary>Cell equals the value.</summary>
        Equal,
        /// <summary>Cell does not equal the value.</summary>
        NotEqual,
        /// <summary>Cell is greater than the value.</summary>
        Greater,
        /// <summary>Cell is less than the value.</summary>
        Less,
        /// <summary>Cell is greater than or equal to the value.</summary>
        GreaterOrEqual,
        /// <summary>Cell is less than or equal to the value.</summary>
        LessOrEqual,
        /// <summary>Cell contains the value.</summary>
        Contains,
        /// <summary>Cell starts with the value.</summary>
        StartsWith,
        /// <summary>Cell ends with the value.</summary>
        EndsWith,
        /// <summary>Cell is between value1 and value2 (inclusive).</summary>
        Between
    }

    /// <summary>
    /// Telerik-compat conditional-formatting rule. Added to a column's
    /// <c>ConditionalFormattingObjectList</c>; when its condition matches a cell, the configured colors
    /// are applied to the cell (or the whole row when <see cref="ApplyToRow"/> is set).
    /// </summary>
    public class ConditionalFormattingObject
    {
        /// <summary>Initializes a new conditional-formatting rule.</summary>
        public ConditionalFormattingObject () { }

        /// <summary>Initializes a conditional-formatting rule.</summary>
        public ConditionalFormattingObject (string name, ConditionTypes condition, string value1, string value2 = "", bool applyToRow = false)
        {
            Name = name;
            Condition = condition;
            Value1 = value1;
            Value2 = value2;
            ApplyToRow = applyToRow;
        }

        /// <summary>Gets or sets the rule name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the comparison condition.</summary>
        public ConditionTypes Condition { get; set; }
        /// <summary>Gets or sets the first comparison value.</summary>
        public string Value1 { get; set; } = string.Empty;
        /// <summary>Gets or sets the second comparison value (used by <see cref="ConditionTypes.Between"/>).</summary>
        public string Value2 { get; set; } = string.Empty;
        /// <summary>Gets or sets whether the formatting applies to the whole row rather than just the cell.</summary>
        public bool ApplyToRow { get; set; }

        /// <summary>Gets or sets the cell background color when the rule matches.</summary>
        public System.Drawing.Color CellBackColor { get; set; } = System.Drawing.Color.Empty;
        /// <summary>Gets or sets the cell text color when the rule matches.</summary>
        public System.Drawing.Color CellForeColor { get; set; } = System.Drawing.Color.Empty;
        /// <summary>Gets or sets the row background color when the rule matches (with <see cref="ApplyToRow"/>).</summary>
        public System.Drawing.Color RowBackColor { get; set; } = System.Drawing.Color.Empty;
        /// <summary>Gets or sets the row text color when the rule matches (with <see cref="ApplyToRow"/>).</summary>
        public System.Drawing.Color RowForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Returns whether the supplied cell display text satisfies this rule.</summary>
        public bool Matches (string cellText)
        {
            cellText ??= string.Empty;

            return Condition switch {
                ConditionTypes.Equal => FilterDescriptor.Compare (cellText, Value1) == 0,
                ConditionTypes.NotEqual => FilterDescriptor.Compare (cellText, Value1) != 0,
                ConditionTypes.Greater => FilterDescriptor.Compare (cellText, Value1) > 0,
                ConditionTypes.Less => FilterDescriptor.Compare (cellText, Value1) < 0,
                ConditionTypes.GreaterOrEqual => FilterDescriptor.Compare (cellText, Value1) >= 0,
                ConditionTypes.LessOrEqual => FilterDescriptor.Compare (cellText, Value1) <= 0,
                ConditionTypes.Contains => cellText.Contains (Value1, StringComparison.CurrentCultureIgnoreCase),
                ConditionTypes.StartsWith => cellText.StartsWith (Value1, StringComparison.CurrentCultureIgnoreCase),
                ConditionTypes.EndsWith => cellText.EndsWith (Value1, StringComparison.CurrentCultureIgnoreCase),
                ConditionTypes.Between => FilterDescriptor.Compare (cellText, Value1) >= 0 && FilterDescriptor.Compare (cellText, Value2) <= 0,
                _ => false
            };
        }
    }

    /// <summary>Telerik-compat aggregate functions for summary rows. Mirrors <c>GridAggregateFunction</c>.</summary>
    public enum GridAggregateFunction
    {
        /// <summary>No aggregate.</summary>
        None,
        /// <summary>Sum of the numeric cell values.</summary>
        Sum,
        /// <summary>Count of rows.</summary>
        Count,
        /// <summary>Average of the numeric cell values.</summary>
        Average,
        /// <summary>Minimum value.</summary>
        Min,
        /// <summary>Maximum value.</summary>
        Max,
        /// <summary>First value.</summary>
        First,
        /// <summary>Last value.</summary>
        Last
    }

    /// <summary>
    /// Telerik-compat summary item. Describes one aggregate (over a named column) shown in a summary row.
    /// </summary>
    public class GridViewSummaryItem
    {
        /// <summary>Initializes a new summary item.</summary>
        public GridViewSummaryItem () { }

        /// <summary>Initializes a summary item for a column, aggregate, and optional format string.</summary>
        public GridViewSummaryItem (string name, GridAggregateFunction aggregate, string formatString = "")
        {
            Name = name;
            Aggregate = aggregate;
            FormatString = formatString;
        }

        /// <summary>Gets or sets the name (column Name/FieldName) the aggregate is computed over and shown in.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the aggregate function.</summary>
        public GridAggregateFunction Aggregate { get; set; } = GridAggregateFunction.Sum;

        /// <summary>
        /// Gets or sets the format applied to the aggregate. Supports a composite form ("Total: {0:C0}")
        /// or a bare numeric/standard format ("C0"); empty uses the raw value.
        /// </summary>
        public string FormatString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Telerik-compat summary row. A collection of <see cref="GridViewSummaryItem"/>s that together make
    /// up one rendered summary row (e.g. a grand total line).
    /// </summary>
    public class GridViewSummaryRowItem : Collection<GridViewSummaryItem>
    {
        /// <summary>Initializes an empty summary row.</summary>
        public GridViewSummaryRowItem () { }

        /// <summary>Initializes a summary row with the specified items.</summary>
        public GridViewSummaryRowItem (params GridViewSummaryItem[] items)
        {
            foreach (var item in items)
                Add (item);
        }
    }

    /// <summary>Specifies the sort order of a grid column. Compat for Telerik <c>RadSortOrder</c>.</summary>
    public enum RadSortOrder
    {
        /// <summary>Not sorted.</summary>
        None = 0,
        /// <summary>Ascending order.</summary>
        Ascending = 1,
        /// <summary>Descending order.</summary>
        Descending = 2
    }

    /// <summary>Specifies how a grid row enters edit mode. Compat for Telerik <c>GridViewEditModes</c> / begin-edit mode.</summary>
    public enum RadGridViewBeginEditMode
    {
        /// <summary>Edit mode begins only when requested programmatically.</summary>
        BeginEditProgrammatically = 0,
        /// <summary>Edit mode begins on a single click.</summary>
        BeginEditOnSingleClick = 1,
        /// <summary>Edit mode begins on a double click.</summary>
        BeginEditOnDoubleClick = 2,
        /// <summary>Edit mode begins as soon as the cell is selected.</summary>
        BeginEditOnKeyPressOrSelectFirstChar = 3
    }

    /// <summary>Specifies how a RadGridView is split into panes. Compat for Telerik <c>RadGridViewSplitMode</c>.</summary>
    public enum RadGridViewSplitMode
    {
        /// <summary>No split.</summary>
        None = 0,
        /// <summary>Split into a vertical pair of panes (side-by-side).</summary>
        Vertical = 1,
        /// <summary>Split into a horizontal pair of panes (stacked).</summary>
        Horizontal = 2
    }

    /// <summary>Specifies whether copy/paste is allowed. Compat for Telerik <c>CopyPasteMode</c>.</summary>
    [Flags]
    public enum CopyPasteMode
    {
        /// <summary>Copy/paste is disallowed entirely.</summary>
        Disallow = 0,
        /// <summary>Copying is allowed.</summary>
        Copy = 1,
        /// <summary>Pasting is allowed.</summary>
        Paste = 2,
        /// <summary>Both copying and pasting are allowed, including header text in the copied content.</summary>
        CopyHeaderText = 4,
        /// <summary>Both copying and pasting are allowed.</summary>
        All = Copy | Paste
    }

    /// <summary>
    /// Telerik-compat enum-to-combo binder. Mirrors <c>Telerik.WinControls.UI.Data.EnumBinder</c>: assigned
    /// to a combo column's <c>DataSource</c> (via <see cref="Target"/>) after setting <see cref="Source"/>
    /// to the enum <see cref="Type"/>, producing the enum's named values as selectable items.
    /// </summary>
    public class EnumBinder : Component
    {
        /// <summary>Initializes a new instance of the EnumBinder class.</summary>
        public EnumBinder () { }

        /// <summary>Initializes a new instance of the EnumBinder class and adds it to the specified container.</summary>
        public EnumBinder (IContainer container) => container.Add (this);

        /// <summary>Gets or sets the enum <see cref="Type"/> whose named values are exposed as items.</summary>
        public Type? Source { get; set; }

        /// <summary>Gets or sets the column (or other data-bindable target) this binder feeds. Designer-shape alias; not itself resolved.</summary>
        public object? Target { get; set; }

        /// <summary>Gets the enum values of <see cref="Source"/> as a list, suitable for combo-column binding. Empty when <see cref="Source"/> is not an enum type.</summary>
        public IList Values => Source is { IsEnum: true } ? Enum.GetValues (Source) : Array.Empty<object> ();
    }

    /// <summary>Provides data for a Telerik grid position-changed event (e.g. paging).</summary>
    public class PositionChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance with the specified position.</summary>
        public PositionChangedEventArgs (int position) => Position = position;

        /// <summary>Gets the new position.</summary>
        public int Position { get; }
    }

    /// <summary>
    /// Mutation-observing descriptor collection used for the grid's sort/group/filter descriptors.
    /// Any change (add/remove/clear/replace) invokes <see cref="Changed"/> so the grid can rebuild
    /// its view.
    /// </summary>
    public class GridDescriptorCollection<T> : Collection<T>
    {
        internal Action? Changed { get; set; }

        /// <summary>Adds several descriptors at once, raising a single change notification.</summary>
        public void AddRange (IEnumerable<T> items)
        {
            var suppress = Changed;
            Changed = null;
            try {
                foreach (var item in items)
                    Add (item);
            } finally {
                Changed = suppress;
            }
            Changed?.Invoke ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, T item) { base.InsertItem (index, item); Changed?.Invoke (); }
        /// <inheritdoc/>
        protected override void RemoveItem (int index) { base.RemoveItem (index); Changed?.Invoke (); }
        /// <inheritdoc/>
        protected override void SetItem (int index, T item) { base.SetItem (index, item); Changed?.Invoke (); }
        /// <inheritdoc/>
        protected override void ClearItems () { base.ClearItems (); Changed?.Invoke (); }

        /// <summary>Returns the first descriptor matching the predicate, or null.</summary>
        public T? Find (Func<T, bool> predicate) => this.FirstOrDefault (predicate);
    }
}
