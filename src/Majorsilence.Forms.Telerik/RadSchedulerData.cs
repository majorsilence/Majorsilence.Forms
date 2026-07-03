using System.ComponentModel;
using System.Data;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat scheduler appointment. Mirrors the property set migrated code reads/writes
    /// directly (<c>Reminders.vb</c>'s <c>addTaskAppointments</c>-style helpers in <c>frmShedular.vb</c>)
    /// and the ones materialized from a bound <see cref="DataTable"/> via <see cref="AppointmentMappingInfo"/>.
    /// </summary>
    public class Appointment
    {
        /// <summary>Initializes a new, empty appointment.</summary>
        public Appointment () { }

        /// <summary>Initializes a new appointment with a start/end/summary (the common 3-arg Telerik constructor).</summary>
        public Appointment (DateTime start, DateTime end, string summary)
        {
            Start = start;
            End = end;
            Summary = summary;
        }

        /// <summary>Initializes a new appointment with start/end/summary/description/location (the 5-arg Telerik constructor).</summary>
        public Appointment (DateTime start, DateTime end, string summary, string description, string location)
        {
            Start = start;
            End = end;
            Summary = summary;
            Description = description;
            Location = location;
        }

        /// <summary>Gets or sets a unique identifier for the appointment. Populated from the bound data source's ID/key column when mapped.</summary>
        public object? Id { get; set; }

        /// <summary>Gets or sets the appointment's start date/time.</summary>
        public DateTime Start { get; set; }

        /// <summary>Gets or sets the appointment's end date/time.</summary>
        public DateTime End { get; set; }

        /// <summary>Gets or sets the reminder lead time (e.g. how long before <see cref="Start"/> to notify). Stored as a plain object so both <see cref="TimeSpan"/> and data-source-native representations (seconds) round-trip through a <see cref="ConvertCallback"/>.</summary>
        public object? Reminder { get; set; }

        /// <summary>Gets or sets the appointment's title/summary text.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Gets or sets the appointment's description/body text.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the appointment's location text.</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>Gets or sets the background/category identifier. Compat for Telerik's <c>AppointmentBackground</c> enum, but stored as a plain object so caller-supplied enum values (or raw data-source values) both work.</summary>
        public object? BackgroundId { get; set; }

        /// <summary>Gets or sets the status identifier. Compat for Telerik's <c>AppointmentStatus</c> enum, stored as a plain object for the same reason as <see cref="BackgroundId"/>.</summary>
        public object? StatusId { get; set; }

        /// <summary>Gets or sets the recurrence rule (iCalendar RRULE-style string), or empty for a non-recurring appointment.</summary>
        public string RecurrenceRule { get; set; } = string.Empty;

        /// <summary>Gets or sets the identifier of the resource (person/equipment/etc.) this appointment is assigned to.</summary>
        public object? ResourceId { get; set; }

        /// <summary>Gets the resource identifiers assigned to this appointment (an appointment may be assigned to more than one resource).</summary>
        public IList<object> ResourceIds { get; } = new List<object> ();

        /// <summary>Gets or sets the underlying data row this appointment was materialized from, if any (used to write edits back).</summary>
        public DataRow? DataRow { get; internal set; }

        /// <summary>Returns the appointment's summary text.</summary>
        public override string ToString () => Summary;
    }

    /// <summary>
    /// Converts a value between an <see cref="Appointment"/> property's in-memory representation and the
    /// bound data source's column representation (e.g. a <see cref="TimeSpan"/> reminder stored as seconds
    /// in the database). Compat for Telerik's <c>ConvertCallback</c> delegate.
    /// </summary>
    /// <param name="value">The value being converted (scheduler-side when converting to the data source; data-source-side when converting to the scheduler).</param>
    /// <returns>The converted value.</returns>
    public delegate object? ConvertCallback (object? value);

    /// <summary>
    /// Per-field mapping entry returned by <see cref="AppointmentMappingInfo.FindByDataSourceProperty(string)"/>.
    /// Carries the data source's column name plus optional two-way <see cref="ConvertCallback"/>s, mirroring
    /// the pattern used by <c>Reminders.vb</c> (<c>mapping.FindByDataSourceProperty("Reminder").ConvertToDataSource = New ConvertCallback(...)</c>).
    /// </summary>
    public class SchedulerMapping
    {
        /// <summary>Initializes a new instance for the specified data source column.</summary>
        public SchedulerMapping (string dataSourceProperty) => DataSourceProperty = dataSourceProperty;

        /// <summary>Gets or sets the data source's column/property name this mapping targets.</summary>
        public string DataSourceProperty { get; set; }

        /// <summary>Gets or sets a callback invoked when writing an <see cref="Appointment"/> field's value back to the data source.</summary>
        public ConvertCallback? ConvertToDataSource { get; set; }

        /// <summary>Gets or sets a callback invoked when reading a data source value into an <see cref="Appointment"/> field.</summary>
        public ConvertCallback? ConvertToScheduler { get; set; }
    }

    /// <summary>
    /// Maps <see cref="Appointment"/> properties to columns of a bound data source (typically a
    /// <see cref="DataTable"/>). Every field is a settable string naming the source column; leaving a
    /// field empty means that <see cref="Appointment"/> property is not populated/written back.
    /// </summary>
    public class AppointmentMappingInfo
    {
        // Field name -> per-field mapping, created lazily so FindByDataSourceProperty always returns a
        // usable (if not-yet-named) SchedulerMapping instance callers can attach ConvertCallbacks to.
        private readonly Dictionary<string, SchedulerMapping> _fieldMappings = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.Id"/>.</summary>
        public string Id { get => Get (nameof (Id)); set => Set (nameof (Id), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.Start"/>.</summary>
        public string Start { get => Get (nameof (Start)); set => Set (nameof (Start), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.End"/>.</summary>
        public string End { get => Get (nameof (End)); set => Set (nameof (End), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.Reminder"/>.</summary>
        public string Reminder { get => Get (nameof (Reminder)); set => Set (nameof (Reminder), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.Summary"/>.</summary>
        public string Summary { get => Get (nameof (Summary)); set => Set (nameof (Summary), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.Description"/>.</summary>
        public string Description { get => Get (nameof (Description)); set => Set (nameof (Description), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.Location"/>.</summary>
        public string Location { get => Get (nameof (Location)); set => Set (nameof (Location), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.BackgroundId"/>.</summary>
        public string BackgroundId { get => Get (nameof (BackgroundId)); set => Set (nameof (BackgroundId), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.StatusId"/>.</summary>
        public string StatusId { get => Get (nameof (StatusId)); set => Set (nameof (StatusId), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.RecurrenceRule"/>.</summary>
        public string RecurrenceRule { get => Get (nameof (RecurrenceRule)); set => Set (nameof (RecurrenceRule), value); }

        /// <summary>Gets or sets the name of the related-table relation carrying an appointment's assigned resources (Telerik's typed-DataSet many-to-many pattern; stored for API compatibility — Majorsilence.Forms's DataTable-only binder does not walk relations).</summary>
        public string Resources { get => Get (nameof (Resources)); set => Set (nameof (Resources), value); }

        /// <summary>Gets or sets the data source column mapped to <see cref="Appointment.ResourceId"/>.</summary>
        public string ResourceId { get => Get (nameof (ResourceId)); set => Set (nameof (ResourceId), value); }

        private string Get (string field) => _fieldMappings.TryGetValue (field, out var m) ? m.DataSourceProperty : string.Empty;

        private void Set (string field, string dataSourceProperty)
        {
            if (_fieldMappings.TryGetValue (field, out var existing))
                existing.DataSourceProperty = dataSourceProperty;
            else
                _fieldMappings[field] = new SchedulerMapping (dataSourceProperty);
        }

        /// <summary>
        /// Returns the <see cref="SchedulerMapping"/> for the <see cref="Appointment"/> field whose mapped
        /// data source column matches <paramref name="dataSourceProperty"/> (case-insensitive), so a caller
        /// can attach <see cref="SchedulerMapping.ConvertToDataSource"/>/<see cref="SchedulerMapping.ConvertToScheduler"/>
        /// callbacks (e.g. <c>mapping.FindByDataSourceProperty("Reminder").ConvertToDataSource = New ConvertCallback(...)</c>).
        /// If no field is currently mapped to that column name, a new (unattached) mapping is created and
        /// returned so the call still succeeds.
        /// </summary>
        public SchedulerMapping FindByDataSourceProperty (string dataSourceProperty)
        {
            foreach (var mapping in _fieldMappings.Values)
                if (string.Equals (mapping.DataSourceProperty, dataSourceProperty, StringComparison.OrdinalIgnoreCase))
                    return mapping;

            var created = new SchedulerMapping (dataSourceProperty);
            _fieldMappings[dataSourceProperty] = created;
            return created;
        }

        // Internal enumeration used by SchedulerBindingDataSource to materialize/write back Appointments.
        internal IEnumerable<(string Field, SchedulerMapping Mapping)> FieldMappings ()
        {
            foreach (var kvp in _fieldMappings)
                yield return (kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Maps scheduler resources (people/equipment/rooms) to columns of a bound data source. Compat carrier
    /// for Telerik's <c>ResourceMappingInfo</c> — Financial's usage only ever constructs and assigns a
    /// default instance to <see cref="SchedulerEventProvider.Mapping"/>/a resource provider without setting
    /// individual fields, so this mirrors <see cref="AppointmentMappingInfo"/>'s shape without wiring
    /// materialization (there being no audited resource-provider consumer to round-trip against).
    /// </summary>
    public class ResourceMappingInfo
    {
        /// <summary>Gets or sets the data source column mapped to the resource's unique identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the data source column mapped to the resource's display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the data source column mapped to the resource's color.</summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>Gets or sets the data source column mapped to the resource's image.</summary>
        public string Image { get; set; } = string.Empty;
    }

    /// <summary>
    /// One side (events or resources) of a <see cref="SchedulerBindingDataSource"/>: a <see cref="Mapping"/>
    /// naming which data source columns feed which fields, plus the bound <see cref="DataSource"/> itself.
    /// Compat for Telerik's <c>SchedulerEventProvider</c>/<c>SchedulerResourceProvider</c> (reached via
    /// <c>SchedulerBindingDataSource.EventProvider</c>/<c>.ResourceProvider</c>).
    /// </summary>
    public class SchedulerEventProvider : ISupportInitialize
    {
        private object? _dataSource;

        /// <summary>Gets or sets the appointment field mapping.</summary>
        public AppointmentMappingInfo Mapping { get; set; } = new ();

        /// <summary>
        /// Gets or sets the bound data source. A <see cref="DataTable"/> is materialized into
        /// <see cref="Appointment"/>s via <see cref="Mapping"/> and kept in sync with
        /// <see cref="DataTable.RowChanged"/>; any other <see cref="System.Collections.IList"/> is used
        /// as-is when its items are already <see cref="Appointment"/>s.
        /// </summary>
        public object? DataSource {
            get => _dataSource;
            set {
                if (ReferenceEquals (_dataSource, value))
                    return;

                if (_dataSource is DataTable oldTable)
                    oldTable.RowChanged -= OnRowChanged;

                _dataSource = value;

                if (_dataSource is DataTable newTable)
                    newTable.RowChanged += OnRowChanged;

                DataSourceChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="DataSource"/> is set to a new value, or the bound <see cref="DataTable"/> reports a row change.</summary>
        internal event EventHandler? DataSourceChanged;

        private void OnRowChanged (object sender, DataRowChangeEventArgs e) => DataSourceChanged?.Invoke (this, EventArgs.Empty);

        /// <inheritdoc/>
        public void BeginInit () { }

        /// <inheritdoc/>
        public void EndInit () { }
    }

    /// <summary>
    /// The resource-side counterpart of <see cref="SchedulerEventProvider"/>: a <see cref="Mapping"/> plus
    /// bound <see cref="DataSource"/> for scheduler resources (people/equipment/rooms). Compat for
    /// Telerik's <c>SchedulerResourceProvider</c> (reached via <c>SchedulerBindingDataSource.ResourceProvider</c>).
    /// </summary>
    public class SchedulerResourceProvider : ISupportInitialize
    {
        /// <summary>Gets or sets the resource field mapping.</summary>
        public ResourceMappingInfo Mapping { get; set; } = new ();

        /// <summary>Gets or sets the bound resource data source.</summary>
        public object? DataSource { get; set; }

        /// <inheritdoc/>
        public void BeginInit () { }

        /// <inheritdoc/>
        public void EndInit () { }
    }

    /// <summary>
    /// Telerik-compat scheduler data source. A <see cref="Component"/> (so it can live in a form's designer
    /// component tray, like the real Telerik control) exposing an <see cref="EventProvider"/> (appointment
    /// mapping + data source) and a <see cref="ResourceProvider"/> (resource mapping + data source).
    /// Materializes <see cref="Appointment"/>s from a bound <see cref="DataTable"/> and republishes them
    /// through <see cref="Appointments"/>, refreshing whenever the table (or its mapping) changes; edits
    /// (see <see cref="WriteBack(Appointment)"/>) are written back through the mapping's
    /// <see cref="SchedulerMapping.ConvertToDataSource"/> callbacks when present.
    /// </summary>
    public class SchedulerBindingDataSource : Component, ISupportInitialize
    {
        /// <summary>Initializes a new instance of the <see cref="SchedulerBindingDataSource"/> class.</summary>
        public SchedulerBindingDataSource ()
        {
            EventProvider.DataSourceChanged += (_, _) => Refresh ();
        }

        /// <summary>Initializes a new instance and adds it to the specified container.</summary>
        public SchedulerBindingDataSource (IContainer container) : this () => container.Add (this);

        /// <summary>Gets the appointment (event) side of this binding source.</summary>
        public SchedulerEventProvider EventProvider { get; } = new ();

        /// <summary>Gets the resource side of this binding source.</summary>
        public SchedulerResourceProvider ResourceProvider { get; } = new ();

        /// <summary>Raised after <see cref="Appointments"/> has been rebuilt from the bound data source.</summary>
        public event EventHandler? DataSourceChanged;

        /// <summary>Gets the appointments currently materialized from the bound data source.</summary>
        public IReadOnlyList<Appointment> Appointments { get; private set; } = [];

        /// <summary>Rebuilds <see cref="Appointments"/> from the currently-bound data source and mapping.</summary>
        public void Refresh ()
        {
            Appointments = Materialize (EventProvider.DataSource, EventProvider.Mapping);
            DataSourceChanged?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>
        /// Writes an appointment's field values back to its originating <see cref="DataRow"/> (see
        /// <see cref="Appointment.DataRow"/>), applying each mapped field's
        /// <see cref="SchedulerMapping.ConvertToDataSource"/> callback when one is set. No-ops for an
        /// appointment with no originating row (e.g. one added directly to <see cref="Appointments"/>
        /// rather than materialized from the data source).
        /// </summary>
        public void WriteBack (Appointment appointment)
        {
            if (appointment.DataRow is not DataRow row || row.Table is null)
                return;

            foreach (var (field, mapping) in EventProvider.Mapping.FieldMappings ()) {
                if (string.IsNullOrEmpty (mapping.DataSourceProperty) || !row.Table.Columns.Contains (mapping.DataSourceProperty))
                    continue;

                var value = GetAppointmentField (appointment, field);
                if (mapping.ConvertToDataSource != null)
                    value = mapping.ConvertToDataSource (value);

                row[mapping.DataSourceProperty] = value ?? DBNull.Value;
            }
        }

        private static List<Appointment> Materialize (object? dataSource, AppointmentMappingInfo mapping)
        {
            if (dataSource is DataTable table)
                return MaterializeFromTable (table, mapping);

            if (dataSource is IEnumerable<Appointment> appointments)
                return appointments.ToList ();

            return [];
        }

        private static List<Appointment> MaterializeFromTable (DataTable table, AppointmentMappingInfo mapping)
        {
            var result = new List<Appointment> ();

            foreach (DataRow row in table.Rows) {
                if (row.RowState == DataRowState.Deleted)
                    continue;

                var appointment = new Appointment { DataRow = row };

                foreach (var (field, fieldMapping) in mapping.FieldMappings ()) {
                    if (string.IsNullOrEmpty (fieldMapping.DataSourceProperty) || !table.Columns.Contains (fieldMapping.DataSourceProperty))
                        continue;

                    var raw = row[fieldMapping.DataSourceProperty];
                    object? value = raw is DBNull ? null : raw;

                    if (fieldMapping.ConvertToScheduler != null)
                        value = fieldMapping.ConvertToScheduler (value);

                    SetAppointmentField (appointment, field, value);
                }

                result.Add (appointment);
            }

            return result;
        }

        private static object? GetAppointmentField (Appointment appointment, string field) => field switch {
            nameof (Appointment.Id) => appointment.Id,
            nameof (Appointment.Start) => appointment.Start,
            nameof (Appointment.End) => appointment.End,
            nameof (Appointment.Reminder) => appointment.Reminder,
            nameof (Appointment.Summary) => appointment.Summary,
            nameof (Appointment.Description) => appointment.Description,
            nameof (Appointment.Location) => appointment.Location,
            nameof (Appointment.BackgroundId) => appointment.BackgroundId,
            nameof (Appointment.StatusId) => appointment.StatusId,
            nameof (Appointment.RecurrenceRule) => appointment.RecurrenceRule,
            nameof (Appointment.ResourceId) => appointment.ResourceId,
            _ => null,
        };

        private static void SetAppointmentField (Appointment appointment, string field, object? value)
        {
            switch (field) {
                case nameof (Appointment.Id): appointment.Id = value; break;
                case nameof (Appointment.Start): appointment.Start = ToDateTime (value); break;
                case nameof (Appointment.End): appointment.End = ToDateTime (value); break;
                case nameof (Appointment.Reminder): appointment.Reminder = value; break;
                case nameof (Appointment.Summary): appointment.Summary = value?.ToString () ?? string.Empty; break;
                case nameof (Appointment.Description): appointment.Description = value?.ToString () ?? string.Empty; break;
                case nameof (Appointment.Location): appointment.Location = value?.ToString () ?? string.Empty; break;
                case nameof (Appointment.BackgroundId): appointment.BackgroundId = value; break;
                case nameof (Appointment.StatusId): appointment.StatusId = value; break;
                case nameof (Appointment.RecurrenceRule): appointment.RecurrenceRule = value?.ToString () ?? string.Empty; break;
                case nameof (Appointment.ResourceId): appointment.ResourceId = value; break;
            }
        }

        private static DateTime ToDateTime (object? value) => value switch {
            DateTime dt => dt,
            null => default,
            _ => Convert.ToDateTime (value, System.Globalization.CultureInfo.InvariantCulture),
        };

        /// <inheritdoc/>
        public void BeginInit ()
        {
            EventProvider.BeginInit ();
            ResourceProvider.BeginInit ();
        }

        /// <inheritdoc/>
        public void EndInit ()
        {
            EventProvider.EndInit ();
            ResourceProvider.EndInit ();
            Refresh ();
        }
    }
}
