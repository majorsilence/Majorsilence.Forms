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
        public GroupDescriptor ()
        {
            GroupNames = new GroupNameCollection (this);
        }

        /// <summary>Initializes a group descriptor for a column and direction.</summary>
        public GroupDescriptor (string propertyName, ListSortDirection direction = ListSortDirection.Ascending)
            : this ()
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        /// <summary>Gets or sets the name (column Name/FieldName) to group by.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the order groups are arranged in.</summary>
        public ListSortDirection Direction { get; set; } = ListSortDirection.Ascending;

        /// <summary>Telerik compat: the grouping expression. Stored (compat grouping uses PropertyName/GroupNames).</summary>
        public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// Gets the field names this descriptor groups by. Adding the first name also sets
        /// <see cref="PropertyName"/>/<see cref="Direction"/> so the grid's single-field grouping
        /// engine honors it; additional names are stored for Telerik source compatibility.
        /// </summary>
        public GroupNameCollection GroupNames { get; }

        /// <summary>The collection type of <see cref="GroupDescriptor.GroupNames"/>.</summary>
        public class GroupNameCollection
        {
            private readonly GroupDescriptor _owner;
            private readonly List<string> _names = new ();

            internal GroupNameCollection (GroupDescriptor owner) => _owner = owner;

            /// <summary>Gets the number of group names.</summary>
            public int Count => _names.Count;

            /// <summary>Returns whether the specified field name is present.</summary>
            public bool Contains (string name) => _names.Any (n => string.Equals (n, name, StringComparison.OrdinalIgnoreCase));

            /// <summary>Adds a field name with the specified direction.</summary>
            public void Add (string name, ListSortDirection direction)
            {
                _names.Add (name);

                if (_names.Count == 1) {
                    _owner.PropertyName = name;
                    _owner.Direction = direction;
                }
            }
        }
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
    /// <c>ConditionalFormattingObjectList</c>; when its condition matches a cell, the cell takes the cell colors
    /// and, with <see cref="ApplyToRow"/>, the rest of its row the row colors.
    /// </summary>
    public class ConditionalFormattingObject
    {
        /// <summary>Initializes a new conditional-formatting rule.</summary>
        public ConditionalFormattingObject () { }

        /// <summary>Initializes a conditional-formatting rule.</summary>
        public ConditionalFormattingObject (string name, ConditionTypes condition, string value1, string value2 = "", bool applyToRow = false)
        {
            Name = name;
            ConditionType = condition;
            TValue1 = value1;
            TValue2 = value2;
            ApplyToRow = applyToRow;
        }

        /// <summary>Gets or sets the rule name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets whether the rule is evaluated. Stored for Telerik compat.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>Gets or sets the comparison condition.</summary>
        public ConditionTypes ConditionType { get; set; }
        /// <summary>Gets or sets the first comparison value.</summary>
        public string TValue1 { get; set; } = string.Empty;
        /// <summary>Gets or sets the second comparison value (used by <see cref="ConditionTypes.Between"/>).</summary>
        public string TValue2 { get; set; } = string.Empty;
        /// <summary>Gets or sets whether a matching row takes the row colors as well as its cell the cell colors.</summary>
        public bool ApplyToRow { get; set; }
        /// <summary>Gets or sets whether a selected row shows the rule's colors; when not, it keeps the selection's.</summary>
        public bool ApplyOnSelectedRows { get; set; } = true;

        /// <summary>Gets or sets the cell background color when the rule matches.</summary>
        public System.Drawing.Color CellBackColor { get; set; } = System.Drawing.Color.Empty;
        /// <summary>Gets or sets the cell text color when the rule matches.</summary>
        public System.Drawing.Color CellForeColor { get; set; } = System.Drawing.Color.Empty;
        /// <summary>Gets or sets the row background color when the rule matches (with <see cref="ApplyToRow"/>).</summary>
        public System.Drawing.Color RowBackColor { get; set; } = System.Drawing.Color.Empty;
        /// <summary>Gets or sets the row text color when the rule matches (with <see cref="ApplyToRow"/>).</summary>
        public System.Drawing.Color RowForeColor { get; set; } = System.Drawing.Color.Empty;

        // Whether the rule matches a cell of the given row; a condition rule looks only at the cell's text.
        internal virtual bool Matches (RadGridView grid, DataGridViewRow row, string cellText) => Matches (cellText);

        /// <summary>Returns whether the supplied cell display text satisfies this rule.</summary>
        public bool Matches (string cellText)
        {
            if (!Enabled)
                return false;

            cellText ??= string.Empty;

            return ConditionType switch {
                ConditionTypes.Equal => FilterDescriptor.Compare (cellText, TValue1) == 0,
                ConditionTypes.NotEqual => FilterDescriptor.Compare (cellText, TValue1) != 0,
                ConditionTypes.Greater => FilterDescriptor.Compare (cellText, TValue1) > 0,
                ConditionTypes.Less => FilterDescriptor.Compare (cellText, TValue1) < 0,
                ConditionTypes.GreaterOrEqual => FilterDescriptor.Compare (cellText, TValue1) >= 0,
                ConditionTypes.LessOrEqual => FilterDescriptor.Compare (cellText, TValue1) <= 0,
                ConditionTypes.Contains => cellText.Contains (TValue1, StringComparison.CurrentCultureIgnoreCase),
                ConditionTypes.StartsWith => cellText.StartsWith (TValue1, StringComparison.CurrentCultureIgnoreCase),
                ConditionTypes.EndsWith => cellText.EndsWith (TValue1, StringComparison.CurrentCultureIgnoreCase),
                ConditionTypes.Between => FilterDescriptor.Compare (cellText, TValue1) >= 0 && FilterDescriptor.Compare (cellText, TValue2) <= 0,
                _ => false
            };
        }
    }

    /// <summary>
    /// Telerik-compat formatting rule decided by an expression over the row's values, e.g.
    /// <c>Amount &gt; 1 AND Name &lt;&gt; 'Charlie'</c> or <c>When &lt; #2024-06-01#</c>. Added to a column's
    /// <c>ConditionalFormattingObjectList</c> like a condition rule, and coloured the same way: the cell, or with
    /// <c>applyToRow</c> the whole row.
    /// </summary>
    /// <remarks>
    /// Columns are named by their <c>Name</c>. The expression is evaluated with System.Data's expression syntax, which
    /// Telerik's expression language shares for comparisons, AND / OR / NOT, 'string' and #date# literals and IIF; an
    /// expression it cannot evaluate matches nothing. Measured on Telerik UI.for.WinForms 2026.2.520 with the two
    /// expressions above.
    /// </remarks>
    public class ExpressionFormattingObject : ConditionalFormattingObject
    {
        private const string ResultColumn = "\u0001match";

        private bool broken;

        /// <summary>Initializes an empty rule.</summary>
        public ExpressionFormattingObject () { }

        /// <summary>Initializes a rule: its name, its expression, and whether it colours the whole row.</summary>
        public ExpressionFormattingObject (string name, string expression, bool applyToRow)
        {
            Name = name;
            Expression = expression;
            ApplyToRow = applyToRow;
        }

        /// <summary>Gets or sets the expression the row's values are tested with.</summary>
        public string Expression {
            get => expression;
            set {
                expression = value ?? string.Empty;
                broken = false;
            }
        }

        private string expression = string.Empty;

        internal override bool Matches (RadGridView grid, DataGridViewRow row, string cellText)
        {
            if (!Enabled || broken || expression.Trim ().Length == 0)
                return false;

            var columns = ((DataGridView) grid).Columns;
            var names = new List<string> ();
            var values = new List<object?> ();
            var types = new List<Type> ();

            for (var i = 0; i < columns.Count && i < row.Cells.Count; i++) {
                var name = columns[i].Name;

                if (string.IsNullOrEmpty (name) || names.Contains (name, StringComparer.OrdinalIgnoreCase))
                    continue;

                var value = row.Cells[i].Value;
                names.Add (name);
                values.Add (value is DBNull ? null : value);
                types.Add (value is null or DBNull ? columns[i].ValueType ?? typeof (string) : value.GetType ());
            }

            try {
                // A one-row table of the row's values, with the expression as a computed column over them.
                using var table = new System.Data.DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };

                for (var i = 0; i < names.Count; i++)
                    table.Columns.Add (names[i], types[i]);

                table.Columns.Add (ResultColumn, typeof (bool), "IIF(" + expression + ", true, false)");

                var data = table.NewRow ();

                for (var i = 0; i < names.Count; i++)
                    data[i] = values[i] ?? DBNull.Value;

                table.Rows.Add (data);
                return data[ResultColumn] is true;
            } catch (Exception ex) when (ex is System.Data.EvaluateException or System.Data.SyntaxErrorException
                                            or InvalidCastException or FormatException or ArgumentException) {
                broken = true;
                return false;
            }
        }
    }

    /// <summary>A grand summary row of a <see cref="RadGridView"/>: a cell per column, holding the aggregate's value where the
    /// column is summarised (Telerik's GridViewSummaryRowInfo). It is not one of the grid's data rows, so its
    /// <see cref="Index"/> is -1.</summary>
    public class GridViewSummaryRowInfo : GridViewRowInfo
    {
        internal GridViewSummaryRowInfo (DataGridViewRow row) : base (row) { }

        /// <summary>Gets -1: a summary row has no place among the data rows.</summary>
        public new int Index => -1;
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

        /// <summary>Initializes a summary item with Telerik's parameter order (name, format string, aggregate).</summary>
        public GridViewSummaryItem (string name, string formatString, GridAggregateFunction aggregate)
            : this (name, aggregate, formatString)
        {
        }

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

        /// <summary>Telerik compat: an explicit aggregate expression (e.g. "SUM(Amount)"). Stored (compat uses Aggregate/Name).</summary>
        public string AggregateExpression { get; set; } = string.Empty;

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

    /// <summary>Grid event kinds used by Telerik's event-dispatch customization. Enum stub for designer EnumBinder sources.</summary>
    public enum GridEventType
    {
        /// <summary>Both kinds of events.</summary>
        Both = 0,
        /// <summary>Mouse events.</summary>
        Mouse = 1,
        /// <summary>Keyboard events.</summary>
        Keyboard = 2
    }

    /// <summary>Event listener priorities used by Telerik's event-dispatch customization. Enum stub.</summary>
    public enum EventListenerPriority
    {
        /// <summary>Normal priority.</summary>
        Normal = 0,
        /// <summary>High priority.</summary>
        High = 1,
        /// <summary>Low priority.</summary>
        Low = 2
    }

    /// <summary>Grid event process modes used by Telerik's event-dispatch customization. Enum stub.</summary>
    public enum GridEventProcessMode
    {
        /// <summary>Synchronous processing.</summary>
        Synchronous = 0,
        /// <summary>Deferred processing.</summary>
        Deferred = 1
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

    /// <summary>Specifies how columns are auto-sized by BestFitColumns. Compat for Telerik <c>BestFitColumnMode</c>.</summary>
    public enum BestFitColumnMode
    {
        /// <summary>No best-fit is applied.</summary>
        None = 0,
        /// <summary>Size to header cells only.</summary>
        HeaderCells = 1,
        /// <summary>Size to displayed data cells.</summary>
        DisplayedCells = 2,
        /// <summary>Size to all cells including the header.</summary>
        AllCells = 4
    }

    /// <summary>
    /// Telerik-compat group-by expression descriptor. The expression string (e.g.
    /// <c>"Field as Alias format \"{0}: {1}\" Group By Field"</c>) is stored verbatim; the
    /// trailing <c>Group By</c> field drives the compat grid's single-field grouping.
    /// </summary>
    public class GridGroupByExpression : GroupDescriptor
    {
        /// <summary>Initializes the descriptor from a Telerik group-by expression string.</summary>
        public GridGroupByExpression (string expression)
        {
            Expression = expression;

            // "<field> as <alias> ... Group By <field>" -- take the field after the last "group by".
            var idx = expression.LastIndexOf ("group by", StringComparison.OrdinalIgnoreCase);
            var field = idx >= 0 ? expression[(idx + "group by".Length)..].Trim () : expression.Split (' ')[0];

            if (field.Length > 0)
                PropertyName = field;
        }

        /// <summary>Gets the original expression string. (Intentionally hides the settable
        /// GroupDescriptor.Expression compat member -- for a parsed group-by expression this value
        /// is immutable and set from the constructor.)</summary>
        public new string Expression { get; }
    }

    /// <summary>
    /// Represents the method that handles a Telerik position-changed event (e.g. drop-down
    /// SelectedIndexChanged). Args are the Data-nested type to match Telerik's
    /// <c>Telerik.WinControls.UI.Data.PositionChangedEventHandler</c> signature, which is what
    /// migrated handlers are written against.
    /// </summary>
    public delegate void PositionChangedEventHandler (object? sender, Majorsilence.Forms.Telerik.Data.PositionChangedEventArgs e);

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

        private Type? _source;
        private object? _target;

        /// <summary>Gets or sets the enum <see cref="Type"/> whose named values are exposed as items.</summary>
        public Type? Source { get => _source; set { _source = value; Bind (); } }

        /// <summary>
        /// Gets or sets what this binder feeds: a <see cref="GridViewComboBoxColumn"/> or a combo box gets the
        /// enum's values as its <c>DataSource</c> once both this and <see cref="Source"/> are set, in either order.
        /// </summary>
        /// <remarks>W6 mechanisms (#176): designer code sets Source and Target and expects the column filled;
        /// Target was stored only, so the column's drop-down was empty.</remarks>
        public object? Target { get => _target; set { _target = value; Bind (); } }

        private void Bind ()
        {
            if (_source is not { IsEnum: true })
                return;

            switch (_target) {
                case GridViewComboBoxColumn column:
                    column.DataSource = Values;
                    break;
                case Majorsilence.Forms.ComboBox combo:
                    combo.DataSource = Values;
                    break;
            }
        }

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

        // Asked before each change, with what is about to happen and the item it touches; false cancels it.
        // The grid raises its SortChanging from here, as Telerik raises it for every change to the collection.
        internal Func<System.Collections.Specialized.NotifyCollectionChangedAction, T?, bool>? Changing { get; set; }

        private bool Allow (System.Collections.Specialized.NotifyCollectionChangedAction action, T? item)
            => Changing is not { } changing || changing (action, item);

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
        protected override void InsertItem (int index, T item)
        {
            if (!Allow (System.Collections.Specialized.NotifyCollectionChangedAction.Add, item))
                return;
            base.InsertItem (index, item);
            Changed?.Invoke ();
        }
        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            if (!Allow (System.Collections.Specialized.NotifyCollectionChangedAction.Remove, this[index]))
                return;
            base.RemoveItem (index);
            Changed?.Invoke ();
        }
        /// <inheritdoc/>
        protected override void SetItem (int index, T item)
        {
            if (!Allow (System.Collections.Specialized.NotifyCollectionChangedAction.Replace, item))
                return;
            base.SetItem (index, item);
            Changed?.Invoke ();
        }
        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            if (!Allow (System.Collections.Specialized.NotifyCollectionChangedAction.Reset, default))
                return;
            base.ClearItems ();
            Changed?.Invoke ();
        }

        /// <summary>Returns the first descriptor matching the predicate, or null.</summary>
        public T? Find (Func<T, bool> predicate) => this.FirstOrDefault (predicate);
    }
}
