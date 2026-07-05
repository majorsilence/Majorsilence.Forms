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
    public class BindingManagerBase
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
        public int Count => list?.Count ?? 0;

        /// <summary>Gets the item at the current position, or null when the list is empty.</summary>
        public object? Current => list is not null && position >= 0 && position < list.Count ? list[position] : null;

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
    public class CurrencyManager : BindingManagerBase
    {
        internal CurrencyManager (IList? list) : base (list) { }

        /// <summary>Gets the bound list.</summary>
        public IList? List { get; internal init; }
    }

    /// <summary>
    /// Maps (dataSource, dataMember) pairs to <see cref="BindingManagerBase"/> instances, mirroring
    /// System.Windows.Forms.BindingContext. Managers are cached per pair so repeated lookups share
    /// position state.
    /// </summary>
    public class BindingContext
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
                    manager = new CurrencyManager (ResolveList (dataSource, member));
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
