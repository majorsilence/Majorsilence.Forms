using System.Collections;
using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Manages the position of (and access to) a data-bound list, as
    /// System.Windows.Forms.BindingManagerBase does.
    /// </summary>
    /// <remarks>
    /// LIVE as of W4.1 (2026-09-01; findings BND-01/02 and the RC-4 root cause). This used to be a
    /// value pretending to be an object: it held an <c>IList</c> reference and an <c>int</c>, never
    /// listened to the list, and <see cref="BindingSource"/> threw it away and built a new one on
    /// every re-resolve -- so a binding attached before the data arrived (the exact order designer
    /// code runs in) was orphaned forever. It now subscribes to the list's
    /// <see cref="IBindingList.ListChanged"/> and keeps its position, its current item, and its
    /// registered <see cref="Bindings"/> in step with what the list does.
    /// </remarks>
    public partial class BindingManagerBase
    {
        private readonly IList? list;
        private int position = -1;

        internal BindingManagerBase (IList? list)
        {
            this.list = list;

            if (Count > 0) {
                position = 0;

                // The item that is current from the start gets its transaction too -- a manager minted
                // over an already-filled DataView never moves before the first edit (BND-09).
                (Current as IEditableObject)?.BeginEdit ();
            }

            // The live half. A list that announces its changes gets a manager that hears them; a plain
            // List<T> has nothing to announce, exactly as upstream. A BindingSource IS an IBindingList,
            // so the manager it owns hears every re-resolve, self-mutation and forwarded inner-list
            // change through this one subscription.
            if (list is IBindingList notifying)
                notifying.ListChanged += OnListChanged;
        }

        /// <summary>Gets the number of items in the bound list.</summary>
        /// <remarks>Virtual because a <see cref="PropertyManager"/> manages a single object rather than a
        /// list and has to answer for it.</remarks>
        public virtual int Count => list?.Count ?? 0;

        /// <summary>Gets the item at the current position, or null when the list is empty.</summary>
        /// <inheritdoc cref="Count" path="/remarks"/>
        public virtual object? Current => list is not null && position >= 0 && position < list.Count ? list[position] : null;

        /// <summary>Gets or sets the current position in the list (clamped to the valid range).</summary>
        /// <remarks>Virtual because a <see cref="PropertyManager"/> holds one object: its position is
        /// the constant 0, not a clamped index into a list it does not have (BND-31).</remarks>
        public virtual int Position {
            get => position;
            set => SetPositionCore (value, forceCurrentChanged: false);
        }

        // The one place position moves. Order is upstream's and it is observable: CurrentChanged (and
        // with it CurrentItemChanged) BEFORE PositionChanged, so a PositionChanged handler that reads a
        // bound control sees the new record, not the old one (BND-20).
        private void SetPositionCore (int value, bool forceCurrentChanged)
        {
            var clamped = Count == 0 ? -1 : MathCompat.Clamp (value, 0, Count - 1);
            var moved = clamped != position;

            if (!moved && !forceCurrentChanged)
                return;

            position = clamped;

            // An item that becomes current opens its edit transaction (BND-09, upstream
            // OnCurrentChanged). DataRowView is the type this exists for: it commits every column
            // write IMMEDIATELY unless inside BeginEdit, so without this the standard Cancel button of
            // every DataSet form -- edit two boxes, click Cancel, bs.CancelEdit() -- reverted nothing
            // and the DataSet already reported changes.
            (Current as IEditableObject)?.BeginEdit ();

            CurrentChanged?.Invoke (this, EventArgs.Empty);
            OnCurrentItemChanged ();

            if (moved)
                PositionChanged?.Invoke (this, EventArgs.Empty);
        }

        private void OnListChanged (object? sender, ListChangedEventArgs e)
        {
            AfterListChanged (e);

            switch (e.ListChangedType) {
            case ListChangedType.ItemAdded:
                // The first item to arrive in an empty list becomes current -- the "bind in
                // InitializeComponent, fill in Load" order every designer form uses (BND-02).
                if (position == -1 && Count > 0)
                    SetPositionCore (0, forceCurrentChanged: true);
                else if (e.NewIndex >= 0 && e.NewIndex <= position && position + 1 < Count) {
                    // An insert above the current item shifts its index; the current OBJECT is
                    // unchanged, so only the number announces the move.
                    position++;
                    PositionChanged?.Invoke (this, EventArgs.Empty);
                }

                break;

            case ListChangedType.ItemDeleted:
                if (Count == 0)
                    SetPositionCore (-1, forceCurrentChanged: true);
                else if (e.NewIndex >= 0 && e.NewIndex < position) {
                    position--;
                    PositionChanged?.Invoke (this, EventArgs.Empty);
                } else if (e.NewIndex == position) {
                    // Deleting the current row makes the NEXT row current. The index often does not
                    // move, so the change has to be forced -- position 1 pointing at a different
                    // object is a current-item change even though the number is still 1 (BND-02).
                    SetPositionCore (position, forceCurrentChanged: true);
                }

                break;

            case ListChangedType.Reset:
                // The list may now be a different list entirely (a BindingSource re-resolving its
                // DataSource raises exactly this), so the current item is re-announced even when the
                // index is unchanged: 0 -> 0 across two lists is a new current item, and the silent
                // version of this transition is what orphaned every designer-ordered binding (BND-01).
                SetPositionCore (position == -1 ? 0 : position, forceCurrentChanged: true);
                break;

            case ListChangedType.ItemChanged:
                // A change INSIDE the current item does not move the position, so the bindings'
                // CurrentChanged subscription never hears it; push to them directly. This is what makes
                // ResetCurrentItem()/ResetBindings() refresh simple-bound controls (BND-14).
                if (e.NewIndex == position || e.NewIndex == -1) {
                    OnCurrentItemChanged ();
                    PushDataToBindings ();
                }

                break;
            }
        }

        // A derived CurrencyManager re-raises the list notifications WinForms exposes there
        // (ListChanged, ItemChanged, MetaDataChanged); the base has no such surface.
        private protected virtual void AfterListChanged (ListChangedEventArgs e) { }

        // Source -> control, for every binding registered with this manager. Through the
        // mode-respecting push: a ControlUpdateMode.Never binding is refreshed by an explicit
        // ReadValue only, never by the machinery (BND-23).
        internal void PushDataToBindings ()
        {
            foreach (Binding? binding in Bindings)
                binding?.PushValue ();
        }

        /// <summary>Raised when <see cref="Position"/> changes.</summary>
        public event EventHandler? PositionChanged;

        /// <summary>Raised when the current item changes.</summary>
        public event EventHandler? CurrentChanged;

        /// <summary>
        /// Commits any pending edit: writes every registered binding back to the source, then completes
        /// the item's own transaction.
        /// </summary>
        /// <remarks>
        /// Real as of W4.1 (BND-08); it was an empty method in the no-op baseline. The write-back half
        /// is what makes "Save from a toolbar button" work: a ToolStripButton never takes focus, so no
        /// Validated ever fired, and <c>bs.EndEdit()</c> used to lose the pending value of every
        /// OnValidation binding -- the default mode.
        /// </remarks>
        public void EndCurrentEdit ()
        {
            foreach (Binding? binding in Bindings)
                binding?.WriteValue ();

            (Current as IEditableObject)?.EndEdit ();

            if (list is ICancelAddNew cancellable && position >= 0)
                cancellable.EndNew (position);
        }

        /// <summary>
        /// Cancels any pending edit: rolls back the item's transaction, removes an uncommitted
        /// <c>AddNew</c> row, and re-reads every registered binding so the controls show the restored
        /// values.
        /// </summary>
        /// <inheritdoc cref="EndCurrentEdit" path="/remarks"/>
        public void CancelCurrentEdit ()
        {
            (Current as IEditableObject)?.CancelEdit ();

            if (list is ICancelAddNew cancellable && position >= 0)
                cancellable.CancelNew (position);

            PushDataToBindings ();
        }

        /// <summary>Re-reads every registered binding from the source.</summary>
        public void Refresh () => PushDataToBindings ();

        /// <summary>Stops the registered bindings moving values in either direction.</summary>
        /// <remarks>Real as of W4.1 (BND-19): the flag existed and nothing read it, so the batch-edit
        /// idiom -- suspend, mutate rows, resume -- still pushed every intermediate value into the
        /// controls, and control edits during the suspension still wrote back.</remarks>
        public void SuspendBinding () => IsBindingSuspended = true;

        /// <summary>Resumes moving values, and refreshes the controls from the source.</summary>
        public void ResumeBinding ()
        {
            if (!IsBindingSuspended)
                return;

            IsBindingSuspended = false;

            if (position == -1 && Count > 0)
                SetPositionCore (0, forceCurrentChanged: true);
            else
                PushDataToBindings ();
        }

        // Raised alongside CurrentChanged (and for an in-place change to the current item), matching
        // upstream's pairing. Declared in TailParity.cs.
        private void OnCurrentItemChanged () => RaiseCurrentItemChanged ();
    }

    /// <summary>The list-backed binding manager (WinForms compatibility name).</summary>
    public partial class CurrencyManager : BindingManagerBase
    {
        internal CurrencyManager (IList? list) : base (list) => List = list;

        /// <summary>Gets the bound list.</summary>
        /// <remarks>The constructor takes the list but used to leave this property unset, so
        /// <c>new CurrencyManager (list).List</c> was always null. For the manager a
        /// <see cref="BindingSource"/> owns, this is the BindingSource itself -- upstream builds
        /// <c>new CurrencyManager(this)</c> and so does this layer as of W4.1, because the
        /// BindingSource IS the stable list identity across re-resolves.</remarks>
        public IList? List { get; internal init; }

        // Re-raise the list's notifications on the WinForms surface that carries them.
        private protected override void AfterListChanged (ListChangedEventArgs e)
        {
            ListChanged?.Invoke (this, e);

            switch (e.ListChangedType) {
            case ListChangedType.ItemChanged:
                ItemChanged?.Invoke (this, EventArgs.Empty);
                break;
            case ListChangedType.PropertyDescriptorAdded:
            case ListChangedType.PropertyDescriptorChanged:
            case ListChangedType.PropertyDescriptorDeleted:
                MetaDataChanged?.Invoke (this, EventArgs.Empty);
                break;
            }
        }
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

                // A source that owns a currency manager (a BindingSource) hands its own over, so its
                // Position and the bound controls' current item are ONE thing. Building a second manager
                // over it here gave them independent positions, and moving the BindingSource did not move
                // what the bound control showed.
                if (dataSource is ICurrencyManagerProvider provider
                    && provider.GetRelatedCurrencyManager (member) is { } owned)
                    return owned;

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
