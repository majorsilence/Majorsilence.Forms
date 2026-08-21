using System.Collections;
using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Manages the position of (and access to) a data-bound list. Minimal cross-platform analogue
    /// of System.Windows.Forms.BindingManagerBase: the list is resolved once from the data source
    /// and the manager tracks a current position over it. Editing/notification plumbing
    /// (IEditableObject, ListChanged re-sync) is not implemented.
    /// </summary>
    public partial class BindingManagerBase
    {
        private readonly IList? list;
        private int position = -1;

        internal BindingManagerBase (IList? list)
        {
            this.list = list;

            if (Count > 0)
                position = 0;
        }

        /// <summary>Gets the number of items in the bound list.</summary>
        /// <remarks>Virtual because a <see cref="PropertyManager"/> manages a single object rather than a
        /// list and has to answer for it.</remarks>
        public virtual int Count => list?.Count ?? 0;

        /// <summary>Gets the item at the current position, or null when the list is empty.</summary>
        /// <inheritdoc cref="Count" path="/remarks"/>
        public virtual object? Current => list is not null && position >= 0 && position < list.Count ? list[position] : null;

        /// <summary>Gets or sets the current position in the list (clamped to the valid range).</summary>
        public int Position {
            get => position;
            set {
                var clamped = Count == 0 ? -1 : Math.Clamp (value, 0, Count - 1);

                if (clamped == position)
                    return;

                position = clamped;
                PositionChanged?.Invoke (this, EventArgs.Empty);
                CurrentChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="Position"/> changes.</summary>
        public event EventHandler? PositionChanged;

        /// <summary>Raised when the current item changes.</summary>
        public event EventHandler? CurrentChanged;

        /// <summary>Commits any pending edit on the current item. Stub in Majorsilence.Forms.</summary>
        public void EndCurrentEdit () { }

        /// <summary>Cancels any pending edit on the current item. Stub in Majorsilence.Forms.</summary>
        public void CancelCurrentEdit () { }

        /// <summary>Re-reads the bound list. Stub in Majorsilence.Forms (the manager reads the live list).</summary>
        public void Refresh () { }

        /// <summary>Suspends data binding. Stub in Majorsilence.Forms.</summary>
        public void SuspendBinding () { }

        /// <summary>Resumes data binding. Stub in Majorsilence.Forms.</summary>
        public void ResumeBinding () { }
    }

    /// <summary>The list-backed binding manager (WinForms compatibility name).</summary>
    public partial class CurrencyManager : BindingManagerBase
    {
        internal CurrencyManager (IList? list) : base (list) => List = list;

        /// <summary>Gets the bound list.</summary>
        /// <remarks>The constructor takes the list but used to leave this property unset, so
        /// <c>new CurrencyManager (list).List</c> was always null.</remarks>
        public IList? List { get; internal init; }
    }

    /// <summary>
    /// Maps (dataSource, dataMember) pairs to <see cref="BindingManagerBase"/> instances, mirroring
    /// System.Windows.Forms.BindingContext. Managers are cached per pair so repeated lookups share
    /// position state.
    /// </summary>
    public partial class BindingContext
    {
        private readonly Dictionary<(object, string), BindingManagerBase> managers = new ();

        /// <summary>Gets the binding manager for the data source.</summary>
        public BindingManagerBase this[object? dataSource] => this[dataSource, string.Empty];

        /// <summary>Gets the binding manager for the (data source, data member) pair.</summary>
        public BindingManagerBase this[object? dataSource, string? dataMember] {
            get {
                var member = dataMember ?? string.Empty;
                var key = (dataSource ?? this, member);

                if (!managers.TryGetValue (key, out var manager)) {
                    // A list source gets a CurrencyManager with a position; anything else is a single
                    // object, which is what PropertyManager is for. This used to hand back a
                    // CurrencyManager over a null list for every scalar source -- Current was always
                    // null, so a binding to a plain object had nothing to read.
                    var list = ResolveList (dataSource, member);

                    manager = list is not null
                        ? new CurrencyManager (list)
                        : new PropertyManager { DataSource = dataSource };

                    managers[key] = manager;
                }

                return manager;
            }
        }

        // Resolves the effective IList for common ADO.NET and collection data sources.
        private static IList? ResolveList (object? dataSource, string dataMember)
        {
            switch (dataSource) {
                case System.Data.DataView view:
                    return view;
                case System.Data.DataTable table:
                    return table.DefaultView;
                case System.Data.DataSet set:
                    return dataMember.Length > 0 && set.Tables.Contains (dataMember)
                        ? set.Tables[dataMember]!.DefaultView
                        : set.Tables.Count > 0 ? set.Tables[0].DefaultView : null;
                case IListSource source:
                    return source.GetList ();
                case IList list:
                    return list;
                default:
                    return null;
            }
        }
    }

}
