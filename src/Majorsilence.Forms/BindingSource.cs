using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: wraps a data list for binding to DataGridView and other controls.
    /// Implements IList so it can be assigned directly to DataGridView.DataSource, and ITypedList so
    /// bound controls can read the schema (column set) of the resolved list -- including the case where
    /// the source is a DataSet and DataMember names one of its tables.
    /// </summary>
    public partial class BindingSource : Component, IList, ITypedList, IBindingList, ICurrencyManagerProvider, ISupportInitialize, ISupportInitializeNotification, ICancelAddNew
    {
        // Forwarded to the inner list, as upstream: the manager's CancelCurrentEdit asks ITS list --
        // which is this BindingSource -- to cancel an uncommitted AddNew row, and without the forward
        // the BindingList underneath never heard, so the phantom row stayed (BND-08).
        void ICancelAddNew.CancelNew (int itemIndex) => (_list as ICancelAddNew)?.CancelNew (itemIndex);

        void ICancelAddNew.EndNew (int itemIndex) => (_list as ICancelAddNew)?.EndNew (itemIndex);

        private IList _list = new List<object?> ();
        private object? _dataSource;
        private string _dataMember = string.Empty;

        /// <summary>Initializes a new instance of BindingSource.</summary>
        public BindingSource () { currency_manager = CreateCurrencyManager (); }

        /// <summary>Initializes a new instance of BindingSource and adds it to the specified container.</summary>
        public BindingSource (IContainer container) : this () { container.Add (this); }

        /// <summary>Initializes a new instance of BindingSource with a data source and data member.</summary>
        public BindingSource (object dataSource, string dataMember) : this () { DataSource = dataSource; DataMember = dataMember; }

        /// <summary>Gets or sets the data member (e.g. a table name within a DataSet source).</summary>
        public string DataMember {
            get => _dataMember;
            set {
                if (string.Equals (_dataMember, value ?? string.Empty, StringComparison.Ordinal))
                    return;

                _dataMember = value ?? string.Empty;
                ResolveList ();
                OnDataMemberChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the underlying data source.</summary>
        public object? DataSource {
            get => _dataSource;
            set {
                if (ReferenceEquals (_dataSource, value))
                    return;

                _dataSource = value;
                ResolveList ();
                OnDataSourceChanged (EventArgs.Empty);
            }
        }

        private bool initializing;

        /// <summary>Gets whether initialization has completed.</summary>
        public bool IsInitialized { get; private set; } = true;

        /// <summary>Raised when <see cref="EndInit"/> completes.</summary>
        public event EventHandler? Initialized;

        /// <summary>
        /// Suspends list resolution until <see cref="EndInit"/>.
        /// </summary>
        /// <remarks>
        /// Designer code sets DataSource and DataMember as separate statements, and each setter
        /// resolves. Between the two the pair is inconsistent -- a DataSet assigned before its member
        /// name resolves to an empty list, which resets Position to -1 and makes bound controls read an
        /// empty schema. Deferring to EndInit means the pair is only ever resolved once, complete.
        /// </remarks>
        public void BeginInit ()
        {
            initializing = true;
            IsInitialized = false;
        }

        /// <summary>Resumes list resolution and resolves once against the fully-assigned state.</summary>
        public void EndInit ()
        {
            if (!initializing)
                return;

            initializing = false;
            ResolveList ();
            IsInitialized = true;
            Initialized?.Invoke (this, EventArgs.Empty);
        }

        // Resolves DataSource (+ DataMember) to the concrete IList that backs this BindingSource:
        // a DataSet resolves through DataMember to that table's DefaultView; a DataTable to its
        // DefaultView; an IListSource via GetList (); an IList is used directly. A Type, a scalar
        // object, and a DataMember over a non-DataSet source each resolve to something real as of
        // W4.5 -- all three used to fall into one catch-all `_ => new List<object?> ()` and come back
        // as a silent empty list (BND-04/05/06).
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Data binding resolves member and element types of caller-supplied objects at runtime, as upstream; a trimmed app roots the types it binds.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Data binding resolves member and element types of caller-supplied objects at runtime, as upstream; a trimmed app roots the types it binds.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Data binding resolves member and element types of caller-supplied objects at runtime, as upstream; a trimmed app roots the types it binds.")]
        private void ResolveList ()
        {
            if (initializing)
                return;

            object? src = _dataSource;

            DetachFromParent ();

            if (src is System.Data.DataSet ds)
                src = !string.IsNullOrEmpty (_dataMember) && ds.Tables.Contains (_dataMember)
                    ? ds.Tables[_dataMember]!.DefaultView
                    : null;
            else if (src is System.Data.DataTable table)
                src = table.DefaultView;
            else if (_dataMember.Length > 0 && src is not null)
                // Master/detail: `new BindingSource (customersBindingSource, "Orders")` means the
                // CURRENT customer's orders, re-resolved whenever the parent's current item moves --
                // it used to mean the customers list itself, unchanging (BND-06).
                src = ResolveDataMember (src, _dataMember);

            declared_element_type = src switch {
                Type type when !typeof (IList).IsAssignableFrom (type) => type,
                not null and not IList and not System.ComponentModel.IListSource and not string
                    and not System.Collections.IEnumerable => src.GetType (),
                _ => null,
            };

            _list = src switch {
                IList list => list,
                System.ComponentModel.IListSource listSource => listSource.GetList (),
                // `DataSource = typeof (Customer)` is what the designer emits for every object data
                // source: an empty typed list whose SCHEMA is real, so a grid shows columns before any
                // data exists and AddNew knows what to create (BND-04).
                Type type => CreateEmptyListOf (type),
                // Any other enumerable is materialised, as WinForms does. Dictionaries, HashSets and LINQ
                // results are all IEnumerable but not IList, and binding a combo to one is ordinary code
                // -- returning an empty list for them made the control render nothing and report
                // Items.Count == 0, which callers then compute indices from.
                string => new List<object?> (),
                System.Collections.IEnumerable enumerable => Materialize (enumerable),
                null => new List<object?> (),
                // "If its some random non-list object, just wrap it in a list" -- upstream's own words.
                // A single view-model IS a data source, and it used to bind nothing (BND-05).
                _ => WrapScalar (src)
            };

            // No manager is dropped here, and that is the W4.1 fix (BND-01): the manager wraps this
            // BindingSource, whose identity survives the re-resolve, and the Reset raised below is how
            // it hears that the world changed -- it re-validates its position and announces the new
            // current item to every binding it carries. Rebuilding it instead is what orphaned every
            // binding attached before the data arrived.
            AttachToList ();
            OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
        }

        // A snapshot, not a live view: an arbitrary IEnumerable has no change notification to forward, so
        // re-reading it later could not be kept in step anyway. Callers who need updates re-assign
        // DataSource or call ResetBindings, both of which re-run this.
        private static List<object?> Materialize (System.Collections.IEnumerable source)
        {
            var list = new List<object?> ();

            foreach (var item in source)
                list.Add (item);

            return list;
        }

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Data binding creates lists of caller-supplied element types at runtime, as upstream.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2070", Justification = "Data binding creates lists of caller-supplied element types at runtime, as upstream.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2071", Justification = "Data binding creates lists of caller-supplied element types at runtime, as upstream.")]
        private static IList CreateEmptyListOf (Type type)
            => typeof (IList).IsAssignableFrom (type)
                ? (IList)Activator.CreateInstance (type)!
                : CreateBindingListOf (type);

        private static BindingList<object?> WrapScalar (object item)
        {
            // A typed BindingList rather than List<object?>, so the schema (ITypedList) still reports
            // the object's real properties -- the wrap must not cost the columns.
            var list = CreateBindingListOf (item.GetType ());
            list.Add (item);

            return list;
        }

        /// <summary>A change-notifying list to hold items of <paramref name="elementType"/>.</summary>
        /// <remarks>
        /// <para>
        /// A CLOSED <c>BindingList&lt;object?&gt;</c>, deliberately, where upstream builds
        /// <c>BindingList&lt;T&gt;</c>. Upstream reaches its typed list through
        /// <c>typeof (BindingList&lt;&gt;).MakeGenericType (t)</c>, which carries
        /// <c>RequiresDynamicCode</c>: the AOT compiler cannot see an instantiation named only at
        /// runtime, so under NativeAOT there may be no native code to run (IL3050). This library is
        /// AOT-clean and states so, and neither escape was acceptable -- suppressing IL3050 would claim
        /// a guarantee the code cannot make, and <c>[RequiresDynamicCode]</c> would propagate out
        /// through <see cref="DataSource"/>'s setter onto public API and every consumer of it.
        /// </para>
        /// <para>
        /// A closed instantiation needs no reflection at all, and costs only the list's STATIC element
        /// type -- which is not where any behaviour lives here. The element type is recorded in
        /// <see cref="declared_element_type"/>, so <c>ITypedList</c> still reports the right schema (a
        /// grid builds its columns before any data arrives) and <see cref="AddNew"/> still creates the
        /// right type. Change notification is kept, which a plain <c>List&lt;object?&gt;</c> would have
        /// lost: this is still an <c>IBindingList</c>, so an add made directly to
        /// <see cref="List"/> still reaches the bound controls.
        /// </para>
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "BindingList<T> raises ListChanged with PropertyDescriptors, which is the mechanism data binding IS -- the same standing position as the rest of this file: a trimmed app has to root the types it binds.")]
        private static BindingList<object?> CreateBindingListOf (Type elementType) => new BindingList<object?> ();

        // The element type the DATA SOURCE declared, as opposed to one recoverable from the list. A
        // `DataSource = typeof (Customer)` or a scalar source knows its element type at the moment it is
        // assigned, and an empty untyped list -- which is what the AOT fallback in CreateBindingListOf
        // produces -- has neither an element to inspect nor a generic argument to read. Recording it
        // keeps the schema and AddNew right in both cases.
        private Type? declared_element_type;

        // The parent whose current item this source's DataMember is read from, held so the
        // subscription can be dropped when DataSource changes.
        private CurrencyManager? parent_manager;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "The data member is followed by TypeDescriptor at runtime, as upstream.")]
        private object? ResolveDataMember (object parent, string member)
        {
            // The parent's current item, live when the parent owns a manager (a BindingSource), the
            // first item when it is a plain list, the object itself when it is a scalar.
            object? current;

            if (parent is ICurrencyManagerProvider provider && provider.CurrencyManager is { } manager) {
                current = manager.Current;

                // Re-resolve when the parent moves: that IS master/detail. The manager re-announces its
                // current item on every reset (forced, see BindingManagerBase), so a parent whose data
                // arrives later lands here again with a real current item.
                manager.CurrentChanged += OnParentCurrentChanged;
                parent_manager = manager;
            } else if (parent is IList { Count: > 0 } items)
                current = items[0];
            else
                current = parent is IList ? null : parent;

            // Resolved against the item TYPE, not the current instance: an empty parent still has a
            // schema, and a member that is not in it is a programming error upstream reports rather
            // than ignores -- silently binding "Orderz" to the whole customers list is how the wrong
            // grid ships.
            var descriptor = ItemProperties (parent, current)[member];

            if (descriptor is null)
                throw new ArgumentException (
                    $"DataMember property '{member}' cannot be found on the DataSource.");

            return current is null ? null : descriptor.GetValue (current);
        }

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "The data member is resolved by TypeDescriptor at runtime, as upstream.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "The data member is resolved by TypeDescriptor at runtime, as upstream.")]
        private static PropertyDescriptorCollection ItemProperties (object parent, object? current)
        {
            if (parent is ITypedList typed)
                return typed.GetItemProperties (null);

            if (current is not null)
                return TypeDescriptor.GetProperties (current);

            return parent is IList
                ? TypeDescriptor.GetProperties (ListBindingHelper.GetListItemType (parent))
                : TypeDescriptor.GetProperties (parent);
        }

        private void OnParentCurrentChanged (object? sender, EventArgs e) => ResolveList ();

        private void DetachFromParent ()
        {
            if (parent_manager is null)
                return;

            parent_manager.CurrentChanged -= OnParentCurrentChanged;
            parent_manager = null;
        }

        // ITypedList — expose the schema of the resolved list so bound controls can build columns even
        // when the list has no rows. Delegates to the list's own ITypedList (DataView) when available,
        // otherwise reflects the element type's properties.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Data binding requires runtime reflection over user-provided types.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Data binding requires runtime reflection over user-provided types.")]
        PropertyDescriptorCollection ITypedList.GetItemProperties (PropertyDescriptor[]? listAccessors)
        {
            if (_list is ITypedList typed)
                return typed.GetItemProperties (listAccessors);

            var elementType = ListElementType ();
            return elementType is null
                ? new PropertyDescriptorCollection (System.Array.Empty<PropertyDescriptor> ())
                : TypeDescriptor.GetProperties (elementType);
        }

        string ITypedList.GetListName (PropertyDescriptor[]? listAccessors) => _dataMember;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        private System.Type? ListElementType ()
        {
            foreach (var iface in _list.GetType ().GetInterfaces ()) {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition () != typeof (IList<>))
                    continue;

                var argument = iface.GetGenericArguments ()[0];

                // A declared element type of object tells us nothing -- and it is exactly what a
                // materialized IEnumerable produces. Prefer what is actually in the list, so binding to a
                // dictionary still exposes Key and Value for DisplayMember/ValueMember to name.
                if (argument != typeof (object))
                    return argument;

                break;
            }

            // The declared type before the first item's: an empty list still has a schema when the
            // source named its type, and that is exactly the case a grid binds to before data arrives.
            return declared_element_type ?? (_list.Count > 0 ? _list[0]?.GetType () : null);
        }

        /// <summary>Gets or sets the zero-based index of the current item, clamped to the valid range.</summary>
        /// <remarks>
        /// A forwarder to the one <see cref="CurrencyManager"/>, as upstream. It used to be a second,
        /// independently-stored position, "parked as given, not clamped" -- which meant
        /// <c>bs.Position = bs.Count</c> (the common off-by-one after <c>AddNew</c>) left
        /// <see cref="Current"/> null and every bound control blank, where WinForms lands on the last
        /// row (BND-21). Clamping is the manager's job so the two can never disagree again.
        /// </remarks>
        public int Position {
            get => CurrencyManager.Position;
            set => CurrencyManager.Position = value;
        }

        /// <summary>Gets the current item at the current position.</summary>
        public object? Current => CurrencyManager.Current;

        /// <summary>Gets or sets a filter expression, applied when the underlying list can filter.</summary>
        /// <remarks>
        /// Sorting and filtering are the UNDERLYING list's job, exactly as in WinForms: a
        /// <c>DataView</c> or any <see cref="IBindingListView"/> applies them, a plain
        /// <c>List&lt;T&gt;</c> cannot and reports so through <see cref="SupportsFiltering"/>. The value
        /// is still stored when the list cannot apply it, so a designer that sets it round-trips.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "A filter expression names members of the caller's own item type at runtime, so trimming cannot see them either way -- a trimmed app has to root the types it filters on. Same position as upstream.")]
        public string? Filter {
            get => _list is IBindingListView { SupportsFiltering: true } view ? view.Filter : filter;
            set {
                filter = value;

                if (_list is IBindingListView { SupportsFiltering: true } view)
                    view.Filter = value;
            }
        }

        private string? filter;

        /// <summary>Gets or sets the sort expression, applied when the underlying list can sort.</summary>
        /// <inheritdoc cref="Filter" path="/remarks"/>
        public string? Sort {
            get => sort;
            set {
                sort = value;
                ApplySortExpression (value);
            }
        }

        private string? sort;

        // ApplySort/RemoveSort record the expression they just carried out. Going through the Sort
        // property would send it straight back through ApplySortExpression and sort the list twice.
        internal void RecordSortExpression (string? value) => sort = value;

        // "Name", "Name DESC", "Name ASC, Age DESC" -- the WinForms sort-expression shape. An advanced
        // view takes the whole string; a plain IBindingList can only sort on one property, so it gets the
        // first clause, which is what WinForms does too.
        private void ApplySortExpression (string? expression)
        {
            if (_list is not IBindingList { SupportsSorting: true } list)
                return;

            if (string.IsNullOrWhiteSpace (expression)) {
                list.RemoveSort ();
                return;
            }

            var properties = ((ITypedList)this).GetItemProperties (null);
            var clauses = new List<ListSortDescription> ();

            foreach (var clause in expression.Split (',', StringSplitOptions.RemoveEmptyEntries)) {
                var parts = clause.Trim ().Split (' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0 || properties[parts[0]] is not { } property)
                    continue;

                var descending = parts.Length > 1
                    && parts[1].StartsWith ("DESC", StringComparison.OrdinalIgnoreCase);

                clauses.Add (new ListSortDescription (property,
                    descending ? ListSortDirection.Descending : ListSortDirection.Ascending));
            }

            if (clauses.Count == 0)
                return;

            // Several clauses need a view that can sort on more than one property; a plain IBindingList
            // sorts on one, so it gets the first, which is what WinForms settles for too.
            if (clauses.Count > 1 && _list is IBindingListView { SupportsAdvancedSorting: true } view)
                view.ApplySort (new ListSortDescriptionCollection (clauses.ToArray ()));
            else
                list.ApplySort (clauses[0].PropertyDescriptor!, clauses[0].SortDirection);
        }

        /// <summary>Raised when the current item changes.</summary>
        public event EventHandler? CurrentChanged;

        /// <summary>Raised when the list, or an item in it, changes.</summary>
        /// <remarks>
        /// This is what makes a bound grid track its data. Controls bind to a BindingSource long before
        /// it has anything in it -- designer code assigns the grid's DataSource in InitializeComponent,
        /// and the form fills the DataSet afterwards -- so without this the grid renders the empty list
        /// it saw at bind time and never updates. Raised both when this BindingSource re-resolves (a new
        /// DataSource or DataMember) and when the resolved list reports its own changes.
        /// </remarks>
        public event System.ComponentModel.ListChangedEventHandler? ListChanged;

        /// <summary>Gets or sets whether <see cref="ListChanged"/> is raised.</summary>
        /// <remarks>
        /// Set false around a bulk load so bound controls rebuild once at the end rather than per row,
        /// then set it back to true -- which itself raises a reset, as WinForms does, since the
        /// suppressed changes still have to reach the bindings somehow.
        /// </remarks>
        public bool RaiseListChangedEvents {
            get => raise_list_changed_events;
            set {
                if (raise_list_changed_events == value)
                    return;

                raise_list_changed_events = value;

                if (value)
                    OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
            }
        }

        private bool raise_list_changed_events = true;

        /// <summary>Raised when the <see cref="DataSource"/> property changes.</summary>
        public event EventHandler? DataSourceChanged;

        /// <summary>Raised when the <see cref="DataMember"/> property changes.</summary>
        public event EventHandler? DataMemberChanged;

        /// <summary>Raises <see cref="ListChanged"/>, unless suppressed or binding is suspended.</summary>
        protected virtual void OnListChanged (ListChangedEventArgs e)
        {
            if (raise_list_changed_events && !binding_suspended)
                ListChanged?.Invoke (this, e);
        }

        /// <summary>Raises <see cref="CurrentChanged"/>.</summary>
        protected virtual void OnCurrentChanged (EventArgs e) => CurrentChanged?.Invoke (this, e);

        /// <summary>Raises <see cref="DataSourceChanged"/>.</summary>
        protected virtual void OnDataSourceChanged (EventArgs e) => DataSourceChanged?.Invoke (this, e);

        /// <summary>Raises <see cref="DataMemberChanged"/>.</summary>
        protected virtual void OnDataMemberChanged (EventArgs e) => DataMemberChanged?.Invoke (this, e);

        // The list we have a ListChanged subscription on, kept so it can be detached on re-resolve.
        // Without this every re-resolve leaves the old list holding a reference to this BindingSource --
        // a DataTable outlives the grids bound to it, so the leak is real, not theoretical.
        private IBindingList? subscribed_list;

        private void AttachToList ()
        {
            if (ReferenceEquals (subscribed_list, _list))
                return;

            if (subscribed_list is not null)
                subscribed_list.ListChanged -= OnUnderlyingListChanged;

            subscribed_list = _list as IBindingList;

            if (subscribed_list is not null)
                subscribed_list.ListChanged += OnUnderlyingListChanged;
        }

        // A DataView raises this when rows are added to its DataTable. Forwarding it is what lets a grid
        // bound before the table was populated catch up.
        private void OnUnderlyingListChanged (object? sender, ListChangedEventArgs e) => OnListChanged (e);

        /// <summary>Moves to the next item.</summary>
        public void MoveNext () { if (Position < _list.Count - 1) Position++; }

        /// <summary>Moves to the previous item.</summary>
        public void MovePrevious () { if (Position > 0) Position--; }

        /// <summary>Moves to the first item.</summary>
        public void MoveFirst () { if (_list.Count > 0) Position = 0; }

        /// <summary>Moves to the last item.</summary>
        public void MoveLast () { if (_list.Count > 0) Position = _list.Count - 1; }

        /// <summary>Tells every bound control to re-read the list.</summary>
        /// <param name="metaDataChanged">
        /// True when the *schema* changed, not just the rows, so bound controls rebuild their columns
        /// as well as their contents.
        /// </param>
        public void ResetBindings (bool metaDataChanged)
            => OnListChanged (new ListChangedEventArgs (
                metaDataChanged ? ListChangedType.PropertyDescriptorChanged : ListChangedType.Reset, -1));

        /// <summary>Tells bound controls to re-read the current item.</summary>
        public void ResetCurrentItem ()
            => OnListChanged (new ListChangedEventArgs (ListChangedType.ItemChanged, Position));

        // IList — delegate to the underlying list ──────────────────────────────

        /// <inheritdoc/>
        public object? this[int index] {
            get => _list[index];
            set => _list[index] = value;
        }

        /// <inheritdoc/>
        public int Count => _list.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => _list.IsReadOnly;

        /// <inheritdoc/>
        public bool IsFixedSize => _list.IsFixedSize;

        /// <inheritdoc/>
        public bool IsSynchronized => _list.IsSynchronized;

        /// <inheritdoc/>
        public object SyncRoot => _list.SyncRoot;

        /// <inheritdoc/>
        public int Add (object? value)
        {
            var index = _list.Add (value);
            NotifySelfMutation (new ListChangedEventArgs (ListChangedType.ItemAdded, index));

            return index;
        }

        // Raises ListChanged for a mutation made THROUGH this BindingSource, but only when the underlying
        // list cannot announce it itself: an IBindingList (a DataView, say) raises its own, which
        // AttachToList already forwards, and raising here as well would deliver it twice.
        //
        // Without this a BindingSource over a plain List<T> -- the commonest case there is -- swallowed
        // every add and remove, so a bound control kept showing the list as it was when it bound.
        private void NotifySelfMutation (ListChangedEventArgs e)
        {
            if (subscribed_list is null)
                OnListChanged (e);
        }

        /// <inheritdoc/>
        public void Clear ()
        {
            _list.Clear ();
            NotifySelfMutation (new ListChangedEventArgs (ListChangedType.Reset, -1));
        }

        /// <inheritdoc/>
        public bool Contains (object? value) => _list.Contains (value);

        /// <inheritdoc/>
        public int IndexOf (object? value) => _list.IndexOf (value);

        /// <inheritdoc/>
        public void Insert (int index, object? value)
        {
            _list.Insert (index, value);
            NotifySelfMutation (new ListChangedEventArgs (ListChangedType.ItemAdded, index));
        }

        /// <inheritdoc/>
        public void Remove (object? value)
        {
            var index = _list.IndexOf (value);

            if (index < 0)
                return;

            _list.RemoveAt (index);
            NotifySelfMutation (new ListChangedEventArgs (ListChangedType.ItemDeleted, index));
        }

        /// <inheritdoc/>
        public void RemoveAt (int index)
        {
            _list.RemoveAt (index);
            NotifySelfMutation (new ListChangedEventArgs (ListChangedType.ItemDeleted, index));
        }

        /// <inheritdoc/>
        public void CopyTo (Array array, int index) => _list.CopyTo (array, index);

        /// <inheritdoc/>
        public IEnumerator GetEnumerator () => _list.GetEnumerator ();

        /// <summary>Adds a new item to the underlying list and makes it current.</summary>
        /// <remarks>
        /// It used to add a literal <c>null</c> and return it, which put a null into the caller's own
        /// collection and made the next bound read fail. The item now comes from an
        /// <see cref="AddingNew"/> handler if one supplies it, then from the list itself if it can create
        /// one, and otherwise from the element type's parameterless constructor.
        /// </remarks>
#pragma warning disable CA1711
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Data binding creates items of user-provided types by reflection, as it does upstream.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Data binding creates items of user-provided types by reflection, as it does upstream.")]
        public object? AddNew ()
        {
            if (!AllowNew)
                throw new InvalidOperationException ("AddNew is not allowed on this BindingSource.");

            var args = new AddingNewEventArgs ();
            OnAddingNew (args);

            var item = args.NewObject;

            // A list this BindingSource built for a declared element type must not create through the
            // list's own AddNew: the backing list is object-typed (see CreateBindingListOf), so its
            // AddNew would hand back a bare object where the caller asked for a Customer. The declared
            // type below is the one to construct from.
            if (item is null && declared_element_type is null && _list is IBindingList { AllowNew: true } bindingList)
                return Select (bindingList.AddNew ());

            if (item is null && ListElementType () is { } elementType && elementType != typeof (object)) {
                try {
                    item = Activator.CreateInstance (elementType);
                } catch (MissingMethodException) {
                    // No parameterless constructor: the caller has to supply the item through AddingNew.
                    throw new InvalidOperationException (
                        $"Cannot create an instance of {elementType.Name}; handle AddingNew to supply one.");
                }
            }

            if (item is null)
                throw new InvalidOperationException (
                    "Cannot determine the type of item to add; handle AddingNew to supply one.");

            _list.Add (item);

            // See RemoveCurrent: one announcement per mutation, whichever side makes it.
            NotifySelfMutation (new ListChangedEventArgs (ListChangedType.ItemAdded, _list.Count - 1));

            return Select (item);
        }
#pragma warning restore CA1711

        // Makes a freshly added item current, which is what a caller adding a row then editing it expects.
        private object? Select (object? item)
        {
            var index = _list.IndexOf (item);

            if (index >= 0)
                Position = index;

            return item;
        }

        /// <summary>Removes the current item from the list.</summary>
        public void RemoveCurrent ()
        {
            if (!AllowRemove)
                throw new InvalidOperationException ("RemoveCurrent is not allowed on this BindingSource.");

            if (Position >= 0 && Position < _list.Count) {
                var removed = Position;
                _list.RemoveAt (removed);

                // Through NotifySelfMutation, not OnListChanged directly: an inner IBindingList (a
                // DataView) announces its own removal, which AttachToList already forwards -- raising
                // here as well delivered the deletion twice, and a manager that counts deletions to
                // keep its position walks it backwards two steps per row.
                NotifySelfMutation (new ListChangedEventArgs (ListChangedType.ItemDeleted, removed));
            }
        }

        /// <summary>Commits any pending edit: every binding's value, then the item's own transaction.</summary>
        /// <remarks>Routed through the currency manager as of W4.3 (BND-08). It used to reach only an
        /// <see cref="IEditableObject"/> current item -- so Save from a toolbar button (which never
        /// moves focus, so no Validated ever fired) lost the pending value of every OnValidation
        /// binding, the default mode.</remarks>
        public void EndEdit () => CurrencyManager.EndCurrentEdit ();

        /// <summary>Rolls back any pending edit, removes an uncommitted new row, and refreshes the
        /// bound controls.</summary>
        /// <inheritdoc cref="EndEdit" path="/remarks"/>
        public void CancelEdit () => CurrencyManager.CancelCurrentEdit ();

        /// <summary>Tells bound controls to re-read the item at the given index.</summary>
        public void ResetItem (int itemIndex)
            => OnListChanged (new ListChangedEventArgs (ListChangedType.ItemChanged, itemIndex));

        /// <summary>Returns the index of the first item whose named property equals the given key.</summary>
        /// <remarks>
        /// Delegates to the underlying list when it can search. When it cannot, this walks the list
        /// instead of throwing -- a DELIBERATE divergence: WinForms raises NotSupportedException for a
        /// list that is not a searchable IBindingList, but the answer is cheap to compute and the
        /// alternative here was the previous behaviour of silently returning -1 for everything, which
        /// reads as "not found" and is worse than either.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding resolves members by name over user-provided types.")]
        public int Find (string propertyName, object key)
        {
            Guard.ThrowIfNullOrEmpty (propertyName);

            if (_list is IBindingList { SupportsSearching: true } searchable
                && ((ITypedList)this).GetItemProperties (null)[propertyName] is { } descriptor)
                return searchable.Find (descriptor, key);

            for (var i = 0; i < _list.Count; i++) {
                var item = _list[i];

                if (item is null)
                    continue;

                var property = item.GetType ().GetProperty (propertyName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                if (property is not null && Equals (property.GetValue (item), key))
                    return i;
            }

            return -1;
        }

        /// <summary>Finds the index of the item whose described property equals the given key.</summary>
        public int Find (PropertyDescriptor property, object key)
        {
            Guard.ThrowIfNull (property);
            return Find (property.Name, key);
        }

        /// <summary>Returns whether the list allows new items. Stub in Majorsilence.Forms.</summary>
        public bool AllowNew { get; set; } = true;

        /// <summary>Returns whether the list allows edits. Stub in Majorsilence.Forms.</summary>
        public bool AllowEdit { get; set; } = true;

        /// <summary>Returns whether the list allows items to be removed. Stub in Majorsilence.Forms.</summary>
        public bool AllowRemove { get; set; } = true;
    }
}
