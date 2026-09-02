using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Majorsilence.Forms
{
    // Live data binding. Everything here used to be a stub: Binding held its property name and data
    // source and did nothing with them, Format/Parse discarded their handlers, and WriteValue was an empty
    // method -- so `control.DataBindings.Add ("Text", customer, "Name")` compiled, ran, and never moved a
    // value in either direction. That is the single most load-bearing thing a migrated WinForms form does
    // outside of layout, so it is implemented rather than described.
    //
    // Scope: simple property-to-property binding, two-way, over a scalar source, a list source, or a
    // BindingSource -- which is what designer-generated binding code produces. Members on BOTH sides
    // resolve through TypeDescriptor as of W4.2, so ICustomTypeDescriptor sources -- above all
    // DataRowView, whose columns exist ONLY as custom descriptors -- bind like anything else
    // (BND-03/30). Not implemented (and not pretended): ITypedList column discovery for nested paths.

    public partial class Binding
    {
        private IBindableComponent? bindable;
        private PropertyDescriptor? target_property;
        private EventInfo? target_changed_event;
        private Delegate? target_changed_handler;
        private INotifyPropertyChanged? watched_source;
        private BindingManagerBase? watched_manager;

        // Set while this binding is itself assigning one side, so the change notification that assignment
        // raises does not bounce straight back and overwrite the value that caused it. Without it, a
        // two-way binding on a text box turns one keystroke into an unbounded ping-pong.
        private bool syncing;

        /// <summary>Raised so a handler can change how the source value is shown in the control.</summary>
        public event ConvertEventHandler? Format;

        /// <summary>Raised so a handler can change how the control value is written to the source.</summary>
        public event ConvertEventHandler? Parse;

        /// <summary>Gets whether this binding is attached to a component and live.</summary>
        public bool IsBinding => bindable is not null && target_property is not null;

        // Called by ControlBindingsCollection when the binding joins a component's collection.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        internal void Attach (IBindableComponent component)
        {
            if (bindable is not null)
                throw new ArgumentException ("This binding is already attached to a component.");

            bindable = component;
            BindableComponent = component;

            // Through TypeDescriptor and case-insensitively, as upstream (BND-30):
            // `DataBindings.Add ("text", ...)` -- a case slip that works in WinForms -- worked nowhere
            // here, and a component with custom descriptors was invisible.
            target_property = TypeDescriptor.GetProperties (component).Find (PropertyName, ignoreCase: true);

            // A property name that does not resolve is a programming error worth surfacing: WinForms
            // throws too, and a silently dead binding is exactly the failure this file exists to remove.
            // A READ-ONLY target is fine when nothing will ever be written to it (BND-30).
            if (target_property is null
                || (target_property.IsReadOnly && ControlUpdateMode != ControlUpdateMode.Never))
                throw new ArgumentException (
                    $"'{PropertyName}' is not a settable public property of {component.GetType ().Name}.");

            BindingManagerBase = component.BindingContext?[DataSource, BindingMemberInfo.BindingPath];

            // Membership, not just a reference (BND-16): Bindings is what the manager pulls and pushes
            // through, so a binding that never joins it is invisible to EndCurrentEdit, ResetBindings
            // and everything else that acts on "the bindings of this source".
            BindingManagerBase?.Bindings.Add (this);

            SubscribeToSource ();
            SubscribeToTarget ();
            ReadValue ();
        }

        // Called when the binding is removed from a component's collection.
        internal void Detach ()
        {
            BindingManagerBase?.Bindings.Remove (this);
            UnsubscribeFromSource ();

            if (target_changed_event is not null && target_changed_handler is not null && bindable is not null)
                target_changed_event.RemoveEventHandler (bindable, target_changed_handler);

            target_changed_event = null;
            target_changed_handler = null;
            target_property = null;
            bindable = null;
            BindableComponent = null;
        }

        /// <summary>Reads the value from the data source into the bound property.</summary>
        /// <remarks>Always reads, whatever <see cref="ControlUpdateMode"/> says (BND-23): an explicit
        /// call is exactly how a <c>Never</c>-mode binding is refreshed on demand -- upstream's
        /// <c>ReadValue</c> is <c>PushData(force: true)</c>. The mode gates the EVENT-driven pushes,
        /// which check it before calling here.</remarks>
        public void ReadValue ()
        {
            if (!IsBinding)
                return;

            if (BindingManagerBase is { IsBindingSuspended: true })
                return;

            var source = CurrentSource ();

            if (source is null) {
                // A list that has EMPTIED pushes the null representation into the control, so deleting
                // the last row clears the detail fields instead of leaving the previous record's
                // values on screen looking current (BND-02's Count == 0 branch). A merely-missing
                // manager or a not-yet-resolved source changes nothing.
                if (BindingManagerBase is { Count: 0 })
                    Assign (() => target_property!.SetValue (bindable, TryCoerce (NullValue, target_property.PropertyType, out var empty) ? empty : null));

                return;
            }

            var member = SourceProperty (source);

            if (member is null)
                return;

            var raw = member.GetValue (source);
            var value = raw is null or DBNull ? NullValue : raw;

            value = ApplyFormat (value, target_property!.PropertyType);

            if (Format is { } handler) {
                var args = new ConvertEventArgs (value, target_property.PropertyType);
                handler (this, args);
                value = args.Value;
            }

            if (!TryCoerce (value, target_property.PropertyType, out var coerced)) {
                ReportCompletion (BindingCompleteContext.ControlUpdate,
                    new FormatException ($"Cannot format '{value}' as {target_property.PropertyType.Name} for '{PropertyName}'."));
                return;
            }

            Assign (() => target_property.SetValue (bindable, coerced));
            ReportCompletion (BindingCompleteContext.ControlUpdate, exception: null);
        }

        // The event-driven push: respects ControlUpdateMode, where the public ReadValue does not.
        internal void PushValue ()
        {
            if (ControlUpdateMode != ControlUpdateMode.Never)
                ReadValue ();
        }

        /// <summary>Writes the bound property's value back to the data source.</summary>
        public void WriteValue () => TryWriteValue ();

        /// <summary>
        /// Writes the bound property's value back to the data source, reporting whether it could.
        /// </summary>
        /// <remarks>
        /// The failure half is W4.4 (BND-13): a value that cannot become the member's type -- a
        /// half-typed "4-", a cleared box over an int -- used to be coerced to null and written, and
        /// null into a value-type property is <c>default(T)</c>, so editing an Age box silently zeroed
        /// the record on every focus change. A failed write now leaves the source alone, resets the
        /// control to the source's value (upstream's recovery), reports through
        /// <see cref="BindingComplete"/> when <see cref="FormattingEnabled"/>, and cancels the
        /// validation that triggered it (BND-07).
        /// </remarks>
        internal bool TryWriteValue ()
        {
            if (!IsBinding)
                return true;

            if (BindingManagerBase is { IsBindingSuspended: true })
                return true;

            var source = CurrentSource ();

            if (source is null)
                return true;

            var member = SourceProperty (source);

            if (member is null || member.IsReadOnly)
                return true;

            var value = target_property!.GetValue (bindable);

            if (Parse is { } handler) {
                var args = new ConvertEventArgs (value, member.PropertyType);
                handler (this, args);
                value = args.Value;
            }

            // The empty-control rules (BND-13/24). "" INTO A STRING MEMBER IS THE STRING "" -- writing
            // null there fails the NOT NULL column upstream writes "" to. For anything else, empty
            // means "no value": with FormattingEnabled that is DataSourceNullValue (DBNull by default,
            // which a DataRowView column accepts); without it, there is no null representation for a
            // value type, so the write is a failure that leaves the source alone.
            if (value is null || value is string { Length: 0 }) {
                if (member.PropertyType == typeof (string))
                    value = value ?? string.Empty;
                else if (FormattingEnabled)
                    value = DataSourceNullValue;
                else if (member.PropertyType.IsValueType && Nullable.GetUnderlyingType (member.PropertyType) is null)
                    return Fail (new FormatException (
                        $"Cannot write an empty value into {member.PropertyType.Name} member '{member.Name}'."));
                else
                    value = null;
            }

            if (!TryCoerce (value, member.PropertyType, out var coerced))
                return Fail (new FormatException (
                    $"Cannot parse '{value}' as {member.PropertyType.Name} for member '{member.Name}'."));

            try {
                Assign (() => member.SetValue (source, coerced));
            } catch (Exception e) when (e is ArgumentException or InvalidCastException or FormatException or System.Reflection.TargetInvocationException) {
                // A DBNull into a POCO int lands here, as does a setter that rejects the value; both
                // are upstream BindingComplete material, not silent zeroes.
                return Fail (e);
            }

            ReportCompletion (BindingCompleteContext.DataSourceUpdate, exception: null);

            return true;

            bool Fail (Exception error)
            {
                ReportCompletion (BindingCompleteContext.DataSourceUpdate, error);

                // Upstream's recovery: the control is reset to the value the source still holds, so
                // what the user sees is what the record contains.
                ReadValue ();

                return false;
            }
        }

        // BindingComplete carries success or the exception, on the binding, its manager, and the
        // BindingSource above them -- but only when FormattingEnabled, as upstream: the legacy path
        // predates the event and code written for it does not expect the raise.
        private void ReportCompletion (BindingCompleteContext context, Exception? exception)
        {
            if (!FormattingEnabled)
                return;

            var args = new BindingCompleteEventArgs (this, context) {
                BindingCompleteState = exception is null ? BindingCompleteState.Success : BindingCompleteState.Exception,
                Exception = exception,
                ErrorText = exception?.Message ?? string.Empty,
            };

            RaiseBindingComplete (args);
        }

        // The object whose property this binding reads and writes: the data source itself for a scalar
        // source, or the manager's current item for a list.
        private object? CurrentSource ()
        {
            if (BindingManagerBase is { } manager && manager.Current is { } current)
                return current;

            // No manager (a binding attached to a component with no BindingContext) still binds a plain
            // object directly, rather than doing nothing.
            return BindingManagerBase is null && DataSource is not IList and not IListSource ? DataSource : null;
        }

        // A PropertyDescriptor, not a PropertyInfo (BND-03): DataRowView -- the row every DataTable
        // binding actually binds -- exposes its columns ONLY through ICustomTypeDescriptor, so its CLR
        // properties are Row, RowVersion and friends, and a reflection lookup for a column name came
        // back null. Every typed-DataSet form bound through here showed nothing and saved nothing,
        // silently. TypeDescriptor answers for POCOs too, so there is nothing to fall back to.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        private PropertyDescriptor? SourceProperty (object source)
        {
            var name = BindingMemberInfo.BindingField;

            return name.Length == 0
                ? null
                : TypeDescriptor.GetProperties (source).Find (name, ignoreCase: true);
        }

        private void SubscribeToSource ()
        {
            if (BindingManagerBase is { } manager) {
                manager.CurrentChanged += OnSourceCurrentChanged;
                watched_manager = manager;
            }

            if (CurrentSource () is INotifyPropertyChanged notifier) {
                notifier.PropertyChanged += OnSourcePropertyChanged;
                watched_source = notifier;
            }
        }

        private void UnsubscribeFromSource ()
        {
            if (watched_manager is not null)
                watched_manager.CurrentChanged -= OnSourceCurrentChanged;

            if (watched_source is not null)
                watched_source.PropertyChanged -= OnSourcePropertyChanged;

            watched_manager = null;
            watched_source = null;
        }

        private void OnSourceCurrentChanged (object? sender, EventArgs e)
        {
            // The item being watched for property changes is the OLD current one, so move the
            // subscription before reading or an edit to the new item is missed.
            if (watched_source is not null) {
                watched_source.PropertyChanged -= OnSourcePropertyChanged;
                watched_source = null;
            }

            if (CurrentSource () is INotifyPropertyChanged notifier) {
                notifier.PropertyChanged += OnSourcePropertyChanged;
                watched_source = notifier;
            }

            PushValue ();
        }

        private void OnSourcePropertyChanged (object? sender, PropertyChangedEventArgs e)
        {
            // An empty or null name means "everything changed", which is the convention callers use to
            // force a refresh.
            if (string.IsNullOrEmpty (e.PropertyName)
                || string.Equals (e.PropertyName, BindingMemberInfo.BindingField, StringComparison.Ordinal))
                PushValue ();
        }

        // Two-way binding needs to know when the bound property changes on the component. WinForms uses
        // the `<Property>Changed` event convention for this, and so does every control here -- Text has
        // TextChanged, Checked has CheckedChanged -- so the convention is the mechanism.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        private void SubscribeToTarget ()
        {
            if (DataSourceUpdateMode == DataSourceUpdateMode.Never || bindable is null)
                return;

            var type = bindable.GetType ();

            // OnValidation watches Validating, not Validated (BND-07): the write has to happen INSIDE
            // the cancellable event, so a Validating handler that inspects the source sees the new
            // value, and a value that cannot be written cancels the focus change instead of being
            // silently dropped one event later.
            target_changed_event = DataSourceUpdateMode == DataSourceUpdateMode.OnPropertyChanged
                ? type.GetEvent (PropertyName + "Changed")
                : type.GetEvent ("Validating") ?? type.GetEvent ("Validated") ?? type.GetEvent ("LostFocus");

            if (target_changed_event?.EventHandlerType is null)
                return;

            // Bind through an explicit MethodInfo: the by-name overload of CreateDelegate does not find a
            // PRIVATE method, so it returned null here and the write-back half of every two-way binding
            // was silently never wired up. Validating carries CancelEventArgs, so it gets the handler
            // whose signature can cancel it.
            var handlerName = target_changed_event.EventHandlerType == typeof (System.ComponentModel.CancelEventHandler)
                ? nameof (OnTargetValidating)
                : nameof (OnTargetChanged);
            var handler = typeof (Binding).GetMethod (handlerName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            target_changed_handler = handler is null
                ? null
                : Delegate.CreateDelegate (target_changed_event.EventHandlerType, this, handler,
                    throwOnBindFailure: false);

            if (target_changed_handler is not null)
                target_changed_event.AddEventHandler (bindable, target_changed_handler);
            else
                target_changed_event = null;
        }

        private void OnTargetChanged (object? sender, EventArgs e) => WriteValue ();

        // Moves this binding onto another manager: membership, subscriptions, and the value shown.
        internal void Rehome (BindingManagerBase? manager)
        {
            if (ReferenceEquals (BindingManagerBase, manager))
                return;

            BindingManagerBase?.Bindings.Remove (this);
            UnsubscribeFromSource ();

            BindingManagerBase = manager;

            BindingManagerBase?.Bindings.Add (this);
            SubscribeToSource ();
            PushValue ();
        }

        private void OnTargetValidating (object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // A value that cannot be written keeps the focus where the bad value is (BND-07/13). Never
            // UN-cancel: another Validating handler may already have objected.
            if (!TryWriteValue ())
                e.Cancel = true;
        }

        // Swaps the watched event when DataSourceUpdateMode changes on an already-attached binding.
        internal void ResubscribeToTarget ()
        {
            if (bindable is null)
                return;

            if (target_changed_event is not null && target_changed_handler is not null)
                target_changed_event.RemoveEventHandler (bindable, target_changed_handler);

            target_changed_event = null;
            target_changed_handler = null;

            SubscribeToTarget ();
        }

        // Runs an assignment with the re-entrancy guard held, so the change notification it provokes on
        // the other side is ignored rather than answered.
        private void Assign (Action assignment)
        {
            if (syncing)
                return;

            syncing = true;

            try {
                assignment ();
            } finally {
                syncing = false;
            }
        }

        private object? ApplyFormat (object? value, Type targetType)
        {
            if (!FormattingEnabled || value is null || FormatString.Length == 0)
                return value;

            // Only worth formatting into text; formatting into a non-string target would just be undone
            // by the conversion below.
            if (targetType != typeof (string) || value is not IFormattable formattable)
                return value;

            return formattable.ToString (FormatString, FormatInfo ?? CultureInfo.CurrentCulture);
        }

        // "Could not convert" and "converted to null" are DIFFERENT ANSWERS, and conflating them was
        // BND-13: Coerce used to return null for both, and null written into an int property is 0 --
        // so a half-typed number zeroed the record. The failure now travels as a return value the
        // caller must look at.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        private static bool TryCoerce (object? value, Type targetType, out object? result)
        {
            if (value is null) {
                // Null into a non-nullable value type is a FAILURE now, not a freshly-minted default:
                // Activator.CreateInstance(typeof(int)) is the zero this whole finding is about.
                result = null;
                return !targetType.IsValueType || Nullable.GetUnderlyingType (targetType) is not null;
            }

            var wanted = Nullable.GetUnderlyingType (targetType) ?? targetType;

            if (wanted.IsInstanceOfType (value) || value is DBNull) {
                // DBNull passes through untouched: the member that accepts it (a DataRowView column)
                // knows what it means, and the member that does not will reject it at SetValue, which
                // is the failure channel too.
                result = value;
                return true;
            }

            try {
                if (wanted == typeof (string))
                    result = value.ToString ();
                else if (wanted.IsEnum)
                    result = value is string text ? Enum.Parse (wanted, text, ignoreCase: true) : Enum.ToObject (wanted, value);
                else
                    result = Convert.ChangeType (value, wanted, CultureInfo.CurrentCulture);

                return true;
            } catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException or ArgumentException) {
                result = null;
                return false;
            }
        }
    }

    public partial class PropertyManager
    {
        /// <inheritdoc/>
        /// <remarks>A property manager holds ONE object and that object is its current item. The base
        /// reports from a list, which a property manager does not have.</remarks>
        public override object? Current => DataSource;

        /// <inheritdoc/>
        public override int Count => DataSource is null ? 0 : 1;

        /// <inheritdoc/>
        /// <remarks>The constant 0, as upstream (BND-31): the base evaluated its list-derived position
        /// before <see cref="DataSource"/> was assigned, so this reported -1 forever.</remarks>
        public override int Position {
            get => 0;
            set { }
        }
    }

    public partial class ControlBindingsCollection
    {
        /// <inheritdoc/>
        protected override void InsertItem (int index, Binding item)
        {
            base.InsertItem (index, item);

            // Every Add overload funnels through here, including Add(Binding) called directly, so this is
            // the one place a binding can be made live from.
            item.Attach (Control);
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            this[index].Detach ();
            base.RemoveItem (index);
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var binding in this)
                binding.Detach ();

            base.ClearItems ();
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, Binding item)
        {
            this[index].Detach ();
            base.SetItem (index, item);
            item.Attach (Control);
        }
    }
}
