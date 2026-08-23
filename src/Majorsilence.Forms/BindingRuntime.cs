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
    // BindingSource -- which is what designer-generated binding code produces. Not implemented (and not
    // pretended): IEditableObject transactions, ICustomTypeDescriptor, and ITypedList column discovery.

    public partial class Binding
    {
        private IBindableComponent? bindable;
        private PropertyInfo? target_property;
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
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        internal void Attach (IBindableComponent component)
        {
            if (bindable is not null)
                throw new ArgumentException ("This binding is already attached to a component.");

            bindable = component;
            BindableComponent = component;
            target_property = component.GetType ().GetProperty (PropertyName,
                BindingFlags.Instance | BindingFlags.Public);

            // A property name that does not resolve is a programming error worth surfacing: WinForms
            // throws too, and a silently dead binding is exactly the failure this file exists to remove.
            if (target_property is null || !target_property.CanWrite)
                throw new ArgumentException (
                    $"'{PropertyName}' is not a settable public property of {component.GetType ().Name}.");

            BindingManagerBase = component.BindingContext?[DataSource, BindingMemberInfo.BindingPath];

            SubscribeToSource ();
            SubscribeToTarget ();
            ReadValue ();
        }

        // Called when the binding is removed from a component's collection.
        internal void Detach ()
        {
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
        public void ReadValue ()
        {
            if (!IsBinding || ControlUpdateMode == ControlUpdateMode.Never)
                return;

            var source = CurrentSource ();

            if (source is null)
                return;

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

            Assign (() => target_property.SetValue (bindable, Coerce (value, target_property.PropertyType)));
        }

        /// <summary>Writes the bound property's value back to the data source.</summary>
        public void WriteValue ()
        {
            if (!IsBinding)
                return;

            var source = CurrentSource ();

            if (source is null)
                return;

            var member = SourceProperty (source);

            if (member is null || !member.CanWrite)
                return;

            var value = target_property!.GetValue (bindable);

            if (Parse is { } handler) {
                var args = new ConvertEventArgs (value, member.PropertyType);
                handler (this, args);
                value = args.Value;
            }

            // An empty control means "no value", which is a different thing from the empty string --
            // DataSourceNullValue is what WinForms writes for it.
            if (value is null || (value is string { Length: 0 }))
                value = DataSourceNullValue;

            Assign (() => member.SetValue (source, Coerce (value, member.PropertyType)));
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

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        private PropertyInfo? SourceProperty (object source)
        {
            var name = BindingMemberInfo.BindingField;

            return name.Length == 0
                ? null
                : source.GetType ().GetProperty (name, BindingFlags.Instance | BindingFlags.Public);
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

            ReadValue ();
        }

        private void OnSourcePropertyChanged (object? sender, PropertyChangedEventArgs e)
        {
            // An empty or null name means "everything changed", which is the convention callers use to
            // force a refresh.
            if (string.IsNullOrEmpty (e.PropertyName)
                || string.Equals (e.PropertyName, BindingMemberInfo.BindingField, StringComparison.Ordinal))
                ReadValue ();
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

            target_changed_event = DataSourceUpdateMode == DataSourceUpdateMode.OnPropertyChanged
                ? type.GetEvent (PropertyName + "Changed")
                : type.GetEvent ("Validated") ?? type.GetEvent ("LostFocus");

            if (target_changed_event?.EventHandlerType is null)
                return;

            // Bind through an explicit MethodInfo: the by-name overload of CreateDelegate does not find a
            // PRIVATE method, so it returned null here and the write-back half of every two-way binding
            // was silently never wired up.
            var handler = typeof (Binding).GetMethod (nameof (OnTargetChanged),
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

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Data binding resolves members by NAME at runtime, over types the caller supplies -- the whole mechanism is reflective, as it is upstream. A trimmed app has to root the types it binds.")]
        private static object? Coerce (object? value, Type targetType)
        {
            if (value is null)
                return targetType.IsValueType && Nullable.GetUnderlyingType (targetType) is null
                    ? Activator.CreateInstance (targetType)
                    : null;

            var wanted = Nullable.GetUnderlyingType (targetType) ?? targetType;

            if (wanted.IsInstanceOfType (value))
                return value;

            // A value that cannot be converted is left alone rather than throwing mid-paint: a partially
            // typed number in a text box is a normal transient state, not an error.
            try {
                if (wanted == typeof (string))
                    return value.ToString ();

                if (wanted.IsEnum)
                    return value is string text ? Enum.Parse (wanted, text, ignoreCase: true) : Enum.ToObject (wanted, value);

                return Convert.ChangeType (value, wanted, CultureInfo.CurrentCulture);
            } catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException or ArgumentException) {
                return null;
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
