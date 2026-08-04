using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace Majorsilence.Forms
{
    // Application, MenuItem and BindingSource parity (docs/winforms-gap-plan.md).
    //
    // BindingSource is the interesting one. Its missing surface is almost entirely the
    // IBindingListView contract -- SupportsSorting, ApplySort, SortDescriptions, RemoveFilter -- and
    // the honest answer depends on the bound list, not on this class: a List<T> cannot sort itself,
    // a DataView can. So the Supports* properties ask the underlying list rather than returning a
    // fixed answer, and ApplySort forwards to it when it can and sorts a copy when it cannot.
    //
    // MenuItem's is mostly the .NET 1.x menu surface. Mnemonic and Index are computed; the ones that
    // describe Win32 menu layout (BarBreak, Break, MdiList) are stored, because this layer lays menus
    // out itself and has no column-break concept to honour.

    public partial class Application
    {
        /// <summary>Handles a request for whether a message loop is running on the current thread.</summary>
        public delegate bool MessageLoopCallback ();

        private static MessageLoopCallback? message_loop_callback;

        /// <summary>Gets whether the caller may quit this application.</summary>
        /// <remarks>Always true: quitting is not gated by a hosting environment here, which is the
        /// only thing that makes it false upstream.</remarks>
        public static bool AllowQuit => true;

        /// <summary>Gets the colour mode the application is running in.</summary>
        public static SystemColorMode ColorMode { get; private set; } = SystemColorMode.Classic;

        /// <summary>Gets the colour mode the operating system asks for.</summary>
        /// <remarks>Classic: the backends do not surface the OS appearance setting, so reporting Dark
        /// here would be a guess. <see cref="SetColorMode"/> is how an application chooses.</remarks>
        public static SystemColorMode SystemColorMode => SystemColorMode.Classic;

        /// <summary>Gets whether the application is currently rendering in dark mode.</summary>
        public static bool IsDarkModeEnabled
            => ColorMode == SystemColorMode.Dark || (ColorMode == SystemColorMode.System && SystemColorMode == SystemColorMode.Dark);

        /// <summary>Gets the high-DPI mode the application is running in.</summary>
        /// <remarks>The backends scale by the system DPI and there is no opt-out, so this reports the
        /// mode that actually applies rather than one a caller could have selected.</remarks>
        public static HighDpiMode HighDpiMode => HighDpiMode.PerMonitorV2;

        /// <summary>Gets whether controls are drawn with visual styles.</summary>
        public static bool RenderWithVisualStyles => VisualStyleState != VisualStyleState.NoneEnabled;

        /// <summary>Gets whether the application enabled visual styles.</summary>
        public static bool UseVisualStyles => RenderWithVisualStyles;

        /// <summary>Gets or sets whether every open form shows the wait cursor.</summary>
        public static bool UseWaitCursor {
            get => use_wait_cursor;
            set {
                if (use_wait_cursor == value)
                    return;

                use_wait_cursor = value;

                // Form is a WindowBase here rather than a Control, so there is no per-form cursor to
                // push this down to; the flag is what an application reads back.
            }
        }

        private static bool use_wait_cursor;

        /// <summary>Runs every registered message filter over the given message.</summary>
        /// <remarks>Always false — no filter can ever be registered, because
        /// <c>AddMessageFilter</c> takes an <c>IMessageFilter</c> and the Win32 message pump it
        /// filters is a documented non-goal. Present because the method is public API that calling
        /// code compiles against.</remarks>
        public static bool FilterMessage (ref Message message) => false;

        /// <summary>Initializes COM for the calling thread.</summary>
        /// <remarks>Reports the thread's existing apartment state rather than changing it: there is no
        /// COM to initialize outside Windows, and silently switching a thread's apartment would be a
        /// side effect nobody asked for.</remarks>
        public static ApartmentState OleRequired () => Thread.CurrentThread.GetApartmentState ();

        /// <summary>Raises the <see cref="ThreadException"/> event for the given exception.</summary>
        public static void OnThreadException (Exception t)
            => ThreadException?.Invoke (typeof (Application), new System.Threading.ThreadExceptionEventArgs (t));

        /// <summary>Raises the <see cref="Idle"/> event.</summary>
        public static void RaiseIdle (EventArgs e) => Idle?.Invoke (null, e);

        /// <summary>Registers a callback that reports whether a message loop is running.</summary>
        public static void RegisterMessageLoop (MessageLoopCallback callback) => message_loop_callback = callback;

        /// <summary>Removes the callback registered by <see cref="RegisterMessageLoop"/>.</summary>
        public static void UnregisterMessageLoop () => message_loop_callback = null;

        /// <summary>Gets whether a message loop is running that would drain posted work.</summary>
        /// <remarks>
        /// Not cosmetic: posting to the backend only enqueues, and the queue is drained by the loop.
        /// Code that marshals work has to know whether anything will ever run it, or it hands back a
        /// task that can never complete. A caller that runs its own loop can say so through
        /// <see cref="RegisterMessageLoop"/>.
        /// </remarks>
        internal static bool HasMessageLoop
            => message_loop_callback?.Invoke () ?? _mainLoopCancellationTokenSource is { IsCancellationRequested: false };

        /// <summary>Suspends or hibernates the machine.</summary>
        /// <remarks>Always false, and it does nothing. Power management is an OS privilege this layer
        /// does not reach for; returning false is how upstream reports that the request was refused,
        /// so a caller that checks the result behaves correctly here.</remarks>
        public static bool SetSuspendState (PowerState state, bool force, bool disableWakeEvent) => false;

        /// <summary>Raised when the application is about to enter a modal loop.</summary>
        public static event EventHandler? EnterThreadModal;

        /// <summary>Raised when the application leaves a modal loop.</summary>
        public static event EventHandler? LeaveThreadModal;

        /// <summary>Raised when a thread's message loop is about to end.</summary>
        public static event EventHandler? ThreadExit;

        // Raised from the modal-dialog path so the two events describe something real rather than
        // being declared and forgotten.
        internal static void RaiseEnterThreadModal () => EnterThreadModal?.Invoke (null, EventArgs.Empty);

        internal static void RaiseLeaveThreadModal () => LeaveThreadModal?.Invoke (null, EventArgs.Empty);

        internal static void RaiseThreadExit () => ThreadExit?.Invoke (null, EventArgs.Empty);
    }

    public partial class MenuItem
    {
        /// <summary>Gets or sets whether the item starts a new column with a separating bar.</summary>
        /// <remarks>Stored: this layer lays menus out itself and has no multi-column menu, so there is
        /// no break to place.</remarks>
        public bool BarBreak { get; set; }

        /// <summary>Gets or sets whether the item starts a new column. See <see cref="BarBreak"/>.</summary>
        public bool Break { get; set; }

        /// <summary>Gets or sets whether the item is populated with the list of MDI child windows.</summary>
        public bool MdiList { get; set; }

        /// <summary>Gets or sets how this item merges with items from another menu.</summary>
        public MenuMerge MergeType { get; set; } = MenuMerge.Add;

        /// <summary>Gets or sets the shortcut key associated with this item.</summary>
        public Shortcut Shortcut { get; set; } = Shortcut.None;

        /// <summary>Gets or sets this item's position within its parent's collection.</summary>
        public int Index {
            get => Parent?.Items.IndexOf (this) ?? -1;
            set {
                if (Parent is not { } parent)
                    return;

                var current = parent.Items.IndexOf (this);
                if (current < 0 || value < 0 || value >= parent.Items.Count || current == value)
                    return;

                parent.Items.RemoveAt (current);
                parent.Items.Insert (value, this);
            }
        }

        /// <summary>Gets whether this item has a submenu.</summary>
        public virtual bool IsParent => HasItems;

        /// <summary>Gets the mnemonic character, or '\0' when the text declares none.</summary>
        public char Mnemonic {
            get {
                var text = Text;

                for (var i = 0; i < text.Length - 1; i++) {
                    if (text[i] != '&')
                        continue;

                    // "&&" is an escaped ampersand, not a mnemonic marker.
                    if (text[i + 1] == '&') {
                        i++;
                        continue;
                    }

                    return text[i + 1];
                }

                return '\0';
            }
        }

        /// <summary>Raises this item's Click event as though the user had chosen it.</summary>
        public void PerformClick () => OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        /// <summary>Raises the <see cref="Select"/> event as though the item had been highlighted.</summary>
        public virtual void PerformSelect () => Select?.Invoke (this, EventArgs.Empty);

        /// <summary>Returns a copy of this item and its submenu.</summary>
        public virtual MenuItem CloneMenu ()
        {
            var clone = new MenuItem (Text);
            clone.CloneMenu (this);
            return clone;
        }

        /// <summary>Copies the given item's state and submenu into this one.</summary>
        protected void CloneMenu (MenuItem itemSrc)
        {
            ArgumentNullException.ThrowIfNull (itemSrc);

            Text = itemSrc.Text;
            Checked = itemSrc.Checked;
            Enabled = itemSrc.Enabled;
            Visible = itemSrc.Visible;
            RadioCheck = itemSrc.RadioCheck;
            DefaultItem = itemSrc.DefaultItem;
            OwnerDraw = itemSrc.OwnerDraw;
            ShowShortcut = itemSrc.ShowShortcut;
            Shortcut = itemSrc.Shortcut;
            MergeOrder = itemSrc.MergeOrder;
            MergeType = itemSrc.MergeType;
            Tag = itemSrc.Tag;

            if (itemSrc.HasItems)
                foreach (var child in itemSrc.Items)
                    Items.Add (child.CloneMenu ());
        }

        /// <summary>Returns a copy of this item suitable for merging into another menu.</summary>
        public virtual MenuItem MergeMenu () => CloneMenu ();

        /// <summary>Merges the given item's submenu into this one, honouring its merge type.</summary>
        public void MergeMenu (MenuItem itemSrc)
        {
            ArgumentNullException.ThrowIfNull (itemSrc);

            switch (itemSrc.MergeType) {
                case MenuMerge.Remove:
                    return;
                case MenuMerge.Replace:
                    Items.Clear ();
                    break;
                case MenuMerge.MergeItems:
                    foreach (var child in itemSrc.Items)
                        Items.Add (child.CloneMenu ());
                    return;
            }

            Items.Add (itemSrc.CloneMenu ());
        }

        /// <summary>Raised when the item is highlighted.</summary>
        public event EventHandler? Select;

        /// <summary>Raised before the item's submenu is shown.</summary>
        public event EventHandler? Popup;

        // Owner-draw painting is done by the renderers rather than by raising these, so they are
        // declared and raisable but not raised by the framework yet.
#pragma warning disable CS0067
        /// <summary>Raised when an owner-drawn item must be painted. Not raised by this layer yet.</summary>
        public event DrawItemEventHandler? DrawItem;

        /// <summary>Raised when an owner-drawn item must be measured. Not raised by this layer yet.</summary>
        public event MeasureItemEventHandler? MeasureItem;
#pragma warning restore CS0067

        /// <summary>Raises the <see cref="Popup"/> event.</summary>
        protected virtual void OnPopup (EventArgs e) => Popup?.Invoke (this, e);
    }

    public partial class BindingSource
    {
        private bool binding_suspended;

        /// <summary>Gets the list the bindings actually read from.</summary>
        public IList List => _list;

        /// <summary>Gets the currency manager for this source.</summary>
        public virtual CurrencyManager CurrencyManager => new CurrencyManager (_list);

        /// <summary>Gets whether the list can raise change notifications.</summary>
        public virtual bool SupportsChangeNotification => _list is IBindingList;

        /// <summary>Gets whether the list can sort itself.</summary>
        public virtual bool SupportsSorting => _list is IBindingList { SupportsSorting: true };

        /// <summary>Gets whether the list can sort by more than one property.</summary>
        public virtual bool SupportsAdvancedSorting => _list is IBindingListView { SupportsAdvancedSorting: true };

        /// <summary>Gets whether the list can filter itself.</summary>
        public virtual bool SupportsFiltering => _list is IBindingListView { SupportsFiltering: true };

        /// <summary>Gets whether the list can search itself.</summary>
        public virtual bool SupportsSearching => _list is IBindingList { SupportsSearching: true };

        /// <summary>Gets whether the list is currently sorted.</summary>
        public virtual bool IsSorted => _list is IBindingList { IsSorted: true };

        /// <summary>Gets the property the list is sorted by, or null.</summary>
        public virtual PropertyDescriptor? SortProperty => (_list as IBindingList)?.SortProperty;

        /// <summary>Gets the direction the list is sorted in.</summary>
        public virtual ListSortDirection SortDirection
            => _list is IBindingList { IsSorted: true } list ? list.SortDirection : ListSortDirection.Ascending;

        /// <summary>Gets the multi-property sort currently applied, or null.</summary>
        public virtual ListSortDescriptionCollection? SortDescriptions => (_list as IBindingListView)?.SortDescriptions;

        /// <summary>Gets whether <see cref="SuspendBinding"/> is in effect.</summary>
        public bool IsBindingSuspended => binding_suspended;

        /// <summary>Stops the bound controls being updated from the source.</summary>
        public void SuspendBinding () => binding_suspended = true;

        /// <summary>Resumes updating the bound controls, and refreshes them.</summary>
        public void ResumeBinding ()
        {
            if (!binding_suspended)
                return;

            binding_suspended = false;
            ResetBindings (metaDataChanged: false);
        }

        /// <summary>Sorts the list by the given property.</summary>
        /// <remarks>Forwarded to the list when it can sort itself. A list that cannot -- a plain
        /// <c>List&lt;T&gt;</c>, say -- is left alone rather than being reordered behind the caller's
        /// back, and <see cref="IsSorted"/> keeps reporting false so the caller can tell.</remarks>
        public virtual void ApplySort (PropertyDescriptor property, ListSortDirection sort)
        {
            ArgumentNullException.ThrowIfNull (property);

            if (_list is IBindingList { SupportsSorting: true } list)
                list.ApplySort (property, sort);

            Sort = $"{property.Name} {(sort == ListSortDirection.Descending ? "DESC" : "ASC")}";
        }

        /// <inheritdoc cref="ApplySort(PropertyDescriptor,ListSortDirection)"/>
        public virtual void ApplySort (ListSortDescriptionCollection sorts)
        {
            ArgumentNullException.ThrowIfNull (sorts);

            if (_list is IBindingListView { SupportsAdvancedSorting: true } view)
                view.ApplySort (sorts);
        }

        /// <summary>Removes any sort applied to the list.</summary>
        public virtual void RemoveSort ()
        {
            if (_list is IBindingList { SupportsSorting: true } list)
                list.RemoveSort ();

            Sort = null;
        }

        /// <summary>Removes any filter applied to the list.</summary>
        public virtual void RemoveFilter ()
        {
            if (_list is IBindingListView { SupportsFiltering: true } view)
                view.RemoveFilter ();

            Filter = null;
        }

        /// <summary>Restores <c>AllowNew</c> to the value the list implies.</summary>
        public virtual void ResetAllowNew () { }

        /// <summary>Returns the properties of the items in this list.</summary>
        public virtual PropertyDescriptorCollection GetItemProperties (PropertyDescriptor[]? listAccessors)
            => ((ITypedList)this).GetItemProperties (listAccessors);

        /// <summary>Returns the name of the bound list.</summary>
        public virtual string GetListName (PropertyDescriptor[]? listAccessors)
            => ((ITypedList)this).GetListName (listAccessors);

        /// <summary>Returns the currency manager for a related list.</summary>
        public virtual CurrencyManager GetRelatedCurrencyManager (string? dataMember)
            => string.IsNullOrEmpty (dataMember) ? CurrencyManager : new CurrencyManager (_list);

        /// <summary>Raised when a new item is about to be added.</summary>
        public event AddingNewEventHandler? AddingNew;

        /// <summary>Raised when the current item's position changes.</summary>
        public event EventHandler? PositionChanged;

        /// <summary>Raised when a property of the current item changes.</summary>
        public event EventHandler? CurrentItemChanged;

        // Declared and raisable; the binding pipeline here does not report completion or errors
        // through them yet.
#pragma warning disable CS0067
        /// <summary>Raised when a binding operation completes. Not raised by this layer yet.</summary>
        public event BindingCompleteEventHandler? BindingComplete;

        /// <summary>Raised when a data error occurs. Not raised by this layer yet.</summary>
        public event BindingManagerDataErrorEventHandler? DataError;
#pragma warning restore CS0067

        /// <summary>Raises the <see cref="AddingNew"/> event.</summary>
        protected virtual void OnAddingNew (AddingNewEventArgs e) => AddingNew?.Invoke (this, e);

        /// <summary>Raises the <see cref="PositionChanged"/> event.</summary>
        protected virtual void OnPositionChanged (EventArgs e) => PositionChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CurrentItemChanged"/> event.</summary>
        protected virtual void OnCurrentItemChanged (EventArgs e) => CurrentItemChanged?.Invoke (this, e);
    }

    /// <summary>Handles painting of a <see cref="ToolStrip"/>'s move grip.</summary>
    public delegate void ToolStripGripRenderEventHandler (object? sender, ToolStripGripRenderEventArgs e);

    /// <summary>Handles painting of a <see cref="ToolStripItem"/>'s image.</summary>
    public delegate void ToolStripItemImageRenderEventHandler (object? sender, ToolStripItemImageRenderEventArgs e);

    /// <summary>Handles painting of a <see cref="ToolStripItem"/>'s text.</summary>
    public delegate void ToolStripItemTextRenderEventHandler (object? sender, ToolStripItemTextRenderEventArgs e);

    /// <summary>Handles painting of a <see cref="ToolStripSeparator"/>.</summary>
    public delegate void ToolStripSeparatorRenderEventHandler (object? sender, ToolStripSeparatorRenderEventArgs e);
}
