# Data binding runtime — findings

## Summary

The binding runtime went live on 2026-08-21 and the happy path it was written against genuinely works: a
`TextBox` bound to a POCO or a `List<T>`/`BindingSource` reads, writes back, formats, and follows `Position`.
Everything outside that path is where it breaks, and it breaks silently. The dominant pattern is that the
**CurrencyManager is a snapshot, not a live object**: it never listens to its list, `BindingSource` throws it
away and builds a new one whenever `DataSource`/`DataMember` re-resolve (including at `EndInit`), and `Binding`
never registers with it (`Bindings` is always empty), so the manager cannot push or pull. In the exact order
designer code runs — `BeginInit`, `DataBindings.Add(..., bindingSource, ...)`, `EndInit`, then fill the data
in `Load` — every simple binding to a `BindingSource` is orphaned and every binding to a still-empty list stays
at `Position -1` forever. The second pattern is that the source side is resolved through CLR reflection only,
so a `DataRowView` column (the typed-DataSet form) is never found. Third, the OnValidation/IEditableObject
half of the contract (`Validating`, `EndCurrentEdit`, `CancelCurrentEdit`, `BeginEdit`, `ICancelAddNew`) is
stubbed, so Save/Cancel buttons commit or roll back nothing. `BindingNavigator` is a stored `BindingSource`
with no wiring, and its `EndInit` destroys the designer's items.

Count: **P0 × 3, P1 × 14, P2 × 12, P3 × 6** (35 findings).

## Findings

### BND-01 — `BindingSource.DataSource`/`EndInit` orphan every attached Binding (CurrencyManager rebuilt) — Cat A — P0 — High
- **Ours:** `ResolveList()` calls `ForgetCurrencyManager()` and the next reader builds a *new* `CurrencyManager` over the new list (`src/Majorsilence.Forms/BindingSource.cs:119-122`, `src/Majorsilence.Forms/AppMenuBindingParity.cs:300-314`). A `Binding` captured the *old* manager at attach time and subscribed to its `CurrentChanged` (`src/Majorsilence.Forms/BindingRuntime.cs:60,168-171`); `DataSourceBinding.ListSourceTracker` does the same for `ListBox`/`ComboBox` (`src/Majorsilence.Forms/DataSourceBinding.cs:79-83`). Nothing re-fetches. `ResolveList` also sets `Position` through the property, which only raises `CurrentChanged` when the *index* changes, so 0 → 0 across two different lists raises nothing (`BindingSource.cs:123,186-187`).
- **Upstream:** one `CurrencyManager` for the life of the `BindingSource` (`src/System.Windows.Forms/System/Windows/Forms/DataBinding/BindingSource.cs:84` `_currencyManager = new CurrencyManager(this)`); `SetList` rewires the inner list and calls `ResetBindings` (`BindingSource.cs:1142-1197`), which the manager sees as a `Reset` and answers with `ChangeRecordState(0, validating:true, …)` → `OnCurrentChanged` → push to every binding (`CurrencyManager.cs:683-695, 832-867`).
- **Impact:** the designer sequence is `((ISupportInitialize)bs).BeginInit(); … textBox.DataBindings.Add("Text", bs, "Name"); … bs.EndInit();` then `bs.DataSource = adapter.GetData()` in `Load`. `Attach` runs between the init calls, builds a manager over the initial empty `List<object?>`, and `EndInit`/`Load` forget it. Every bound TextBox/CheckBox/NumericUpDown on the form stays blank and never writes back; a bound `ListBox` no longer follows `Position`. Also hits `bs.DataSource = typeof(Customer)` followed by the real list.
- **Fix:** create the `CurrencyManager` once in the ctor and give it a `SetList(IList)` (swap `list`, clamp `position`, raise `CurrentChanged`+`PositionChanged`+`ListChanged(Reset)`/`MetaDataChanged`); make `ResolveList` call it instead of `ForgetCurrencyManager`. Then `ItemChanged`/`MetaDataChanged` in `FinalParity.cs` have a trigger.
- **Test:** `bs.BeginInit(); box.DataBindings.Add("Text", bs, "Name"); bs.DataSource = list; bs.EndInit(); Assert.Equal(list[0].Name, box.Text); bs.Position = 1; Assert.Equal(list[1].Name, box.Text);` — and the same with `bs.DataSource` reassigned after the binding exists.
- **Tests today:** none (BindingSourceRuntimeTests sets `DataSource` before binding; BindingSourceInitializeTests never binds a control).

### BND-02 — `CurrencyManager`/`BindingSource` ignore list changes: `Position`/`Current` never move with the list — Cat B — P0 — High
- **Ours:** `BindingManagerBase` holds an `IList` and an `int`; it never subscribes to `IBindingList.ListChanged` (the class comment says so, `src/Majorsilence.Forms/BindingContext.cs:6-23`). `BindingSource` sets `Position` once at resolve (`BindingSource.cs:123`), forwards the inner list's `ListChanged` verbatim (`BindingSource.cs:362-363`), and its own `Add/Insert/Remove/RemoveAt/Clear/RemoveCurrent` never touch `position` (`BindingSource.cs:414-471, 535-545`).
- **Upstream:** `CurrencyManager.List_ListChanged` (`CurrencyManager.cs:618-820`): first `ItemAdded` on an empty list → `ChangeRecordState(0,…)` (`:723-727`); delete of the current row → clamp + `CurrentChanged` (`:737-761`); `Count == 0` → `-1` with `PositionChanged`/`CurrentChanged` (`:643-652`); `Reset` re-validates the position (`:683-695`).
- **Impact:** `bs.DataSource = ds.Customers` (empty) in `InitializeComponent`, `adapter.Fill(ds.Customers)` in `Load`: the grid fills (it listens to `ListChanged`) but `bs.Current` is null and every bound TextBox is blank until the user clicks a row. `bs.RemoveCurrent()`/`bs.Remove(x)` leaves `Position` pointing at the *next* item with no `CurrentChanged`, so detail fields show a stale record; `bs.Clear()` leaves `Position == 0` and `Current == null`. `bs.Add(item)` to an empty source never makes it current.
- **Fix:** in the manager, subscribe to `ListChanged` (and to the `BindingSource`'s own `ListChanged` when it is the list) and mirror the `ItemAdded`/`ItemDeleted`/`Reset`/`Count==0` branches above; have `BindingSource` route its self-mutations through the same path.
- **Test:** `var bl = new BindingList<Person>(); bs.DataSource = bl; box.DataBindings.Add("Text", bs, "Name"); bl.Add(new("Ada")); Assert.Equal(0, bs.Position); Assert.Equal("Ada", box.Text); bs.RemoveCurrent(); Assert.Equal(-1, bs.Position); Assert.Equal("", box.Text);`
- **Tests today:** none (BindingSourceTests `Remove_Invoke_RemovesItem` asserts `Count` only).

### BND-03 — `Binding` cannot bind a `DataRowView` column (CLR reflection only) — Cat B — P0 — High
- **Ours:** `SourceProperty` resolves the member with `source.GetType().GetProperty(name)` (`src/Majorsilence.Forms/BindingRuntime.cs:157-164`) and the file header says `ICustomTypeDescriptor` is "not implemented" (`BindingRuntime.cs:15-17`). `DataRowView` exposes columns only through `ICustomTypeDescriptor`; its CLR properties are `Row`, `RowVersion`, `IsNew`, … So `GetProperty("Name")` is null and `ReadValue`/`WriteValue` return silently (`BindingRuntime.cs:93-96, 123-126`).
- **Upstream:** `BindToObject` resolves the field through `PropertyDescriptor`s from `BindingManagerBase.GetItemProperties()` (`Binding.BindToObject.cs`, `BindingManagerBase.cs:74-110`), which `TypeDescriptor` supplies for `DataRowView`. The list controls here already do this (`DataSourceBinding.MemberValue`, `DataSourceBinding.cs:142-153`).
- **Impact:** the typed-DataSet form — `textBox.DataBindings.Add("Text", customersBindingSource, "Name")` over a `DataTable`/`DataSet`, or `("Text", dataSet, "Customers.Name")` — shows nothing and saves nothing, with no exception. COMPATIBILITY_MATRIX row `Binding` claims DataView/DataTable sources work; only the list side does.
- **Fix:** in `SourceProperty`/`ReadValue`/`WriteValue` use `TypeDescriptor.GetProperties(source)[name]` (a `PropertyDescriptor`) first and fall back to `PropertyInfo`; `DataRowView` also needs `PropertyDescriptor.AddValueChanged` or the manager's `ListChanged(ItemChanged)` for source→control updates.
- **Test:** `var t = new DataTable(); t.Columns.Add("Name"); t.Rows.Add("Ada"); box.DataBindings.Add("Text", t, "Name"); Assert.Equal("Ada", box.Text); box.Text = "Grace"; box.DataBindings[0].WriteValue(); Assert.Equal("Grace", t.Rows[0]["Name"]);`
- **Tests today:** none (DataTableBindingTests cover `ComboBox`/`ListBox` only).

### BND-04 — `BindingSource.DataSource = typeof(T)` yields an untyped empty list — Cat A — P1 — High
- **Ours:** a `Type` is neither `IList` nor `IEnumerable`, so `ResolveList` falls to `_ => new List<object?>()` (`src/Majorsilence.Forms/BindingSource.cs:107-117`). `ITypedList.GetItemProperties` then has no element type and returns an empty collection (`BindingSource.cs:147-178`); `AddNew` throws "Cannot determine the type of item to add" (`BindingSource.cs:502-513`).
- **Upstream:** `ResetList` → `GetListFromType(type)` → `CreateBindingList(ListBindingHelper.GetListItemType(type))`, a real `BindingList<T>` (`BindingSource.cs:1083, 662-676, 517-523`).
- **Impact:** the designer emits `this.customerBindingSource.DataSource = typeof(App.Customer);` for every object data source. Grids bound to it show no columns before data arrives; `AddNew()`/`BindingNavigator` Add throws; `AllowNew` is meaningless.
- **Fix:** in `ResolveList`, `if (src is Type t) src = typeof(IList).IsAssignableFrom(t) ? Activator.CreateInstance(t) : Activator.CreateInstance(typeof(BindingList<>).MakeGenericType(t))`.
- **Test:** `bs.DataSource = typeof(Person); Assert.IsType<BindingList<Person>>(bs.List); Assert.Contains("Name", ((ITypedList)bs).GetItemProperties(null).Cast<PropertyDescriptor>().Select(p=>p.Name)); Assert.IsType<Person>(bs.AddNew());`
- **Tests today:** none (BindingSourceTests `DataSource_SetNonList_IsEmpty` asserts the *wrong* behaviour for a scalar, see BND-05).

### BND-05 — `BindingSource.DataSource = scalarObject` yields an empty list — Cat A — P1 — High
- **Ours:** same `_ => new List<object?>()` branch (`src/Majorsilence.Forms/BindingSource.cs:117`); `Current` is null.
- **Upstream:** "If its some random non-list object, just wrap it in a list" — `WrapObjectInBindingList(obj)` (`BindingSource.cs:1113-1120, 1218-1223`), so `Count == 1` and `Current == obj`.
- **Impact:** `viewModelBindingSource.DataSource = new CustomerViewModel();` (a common MVP shape, and what the designer produces when the data source is a single object) binds nothing.
- **Fix:** replace the default arm with `WrapObjectInBindingList` semantics: `var bl = (IList)Activator.CreateInstance(typeof(BindingList<>).MakeGenericType(src.GetType())); bl.Add(src);`.
- **Test:** `bs.DataSource = new Person{Name="Ada"}; Assert.Equal(1, bs.Count); Assert.Equal("Ada", ((Person)bs.Current!).Name);`
- **Tests today:** BindingSourceTests.cs `DataSource_SetNonList_IsEmpty` (asserts current divergent behaviour; must be inverted).

### BND-06 — `BindingSource.DataMember` ignored except for `DataSet`; no master/detail re-targeting — Cat A — P1 — High
- **Ours:** `ResolveList` only consults `_dataMember` when the source is a `DataSet` (`src/Majorsilence.Forms/BindingSource.cs:98-106`); for anything else the source itself becomes the list. There is no subscription to the parent's current item. `GetRelatedCurrencyManager(member)` returns `new CurrencyManager(_list)` — a manager over the *same* list positioned at 0 (`src/Majorsilence.Forms/AppMenuBindingParity.cs:455-456`).
- **Upstream:** `ListBindingHelper.GetList(ds, member)` reads the member off the source's current item (`ListBindingHelper.cs:28-73`); `WireDataSource` hooks the parent manager's `CurrentItemChanged` (`BindingSource.cs:1283-1294`) and `ParentCurrencyManager_CurrentItemChanged` re-`SetList`s the child and resets `Position` (`BindingSource.cs:865-952`); `GetRelatedCurrencyManager` creates a cached related `BindingSource(this, member)` (`BindingSource.cs:141-186`).
- **Impact:** `ordersBindingSource = new BindingSource(customersBindingSource, "Orders")` — the designer's master/detail shape — makes the Orders grid display the *customers* list, and it never changes when the customer changes. `textBox.DataBindings.Add("Text", bs, "Address.City")` resolves nothing.
- **Fix:** when `_dataSource is ICurrencyManagerProvider p && _dataMember != ""`, resolve `_list` from `TypeDescriptor.GetProperties(p.CurrencyManager.Current)[_dataMember]`, subscribe to the parent's `CurrentChanged` and re-resolve + `Position = Count>0?0:-1` on each; make `GetRelatedCurrencyManager` return a cached `new BindingSource(this, member).CurrencyManager`.
- **Test:** `var child = new BindingSource(parentBs, "Orders"); Assert.Same(parentBs.Current.Orders[0], child.Current); parentBs.Position = 1; Assert.Same(((Customer)parentBs.Current).Orders[0], child.Current);`
- **Tests today:** RemainingParityTests `ListBindingHelper_follows_a_data_member_off_the_current_item` (helper only; `BindingSource` path untested).

### BND-07 — `Binding` OnValidation writes on `Validated`/`LostFocus`, not `Validating`; cannot cancel — Cat A — P1 — High
- **Ours:** for `DataSourceUpdateMode.OnValidation` the target event is `Validated`, falling back to `LostFocus` (`src/Majorsilence.Forms/BindingRuntime.cs:228-230`); the handler is a plain `EventHandler` (`:252`) so no `Cancel` path exists.
- **Upstream:** `CheckBinding` finds the `Validating` event (`Binding.cs:544-557`); `Target_Validate` pulls and sets `e.Cancel = true` on parse failure or exception (`Binding.cs:1114-1127`), which keeps focus on the control.
- **Impact:** a `Validating` handler that checks the *source* (`if (person.Age < 0) e.Cancel = true`) sees the pre-edit value, because the write happens one event later; a bad value never blocks focus. `Control.Validate(bool)` has the same order (`src/Majorsilence.Forms/Control.Compat.cs:401-414`), so `Form.ValidateChildren()` validates stale data.
- **Fix:** subscribe to `Validating` with a `CancelEventHandler`; in it call `WriteValue` and set `e.Cancel` when the parse fails (needs BND-12's failure signal).
- **Test:** `box.DataBindings.Add("Text", person, "Age"); box.Validating += (_, e) => seen = person.Age; box.Text = "42"; box.Validate(true); Assert.Equal(42, seen);`
- **Tests today:** none for OnValidation (BindingRuntimeTests use `OnPropertyChanged`/`Never`).

### BND-08 — `BindingManagerBase.EndCurrentEdit`/`CancelCurrentEdit` no-op; `BindingSource.EndEdit`/`CancelEdit` skip PullData/PushData and `ICancelAddNew` — Cat B — P1 — High
- **Ours:** `EndCurrentEdit`/`CancelCurrentEdit` are empty (`src/Majorsilence.Forms/BindingContext.cs:55-59`, in NoOpStubBaseline). `BindingSource.EndEdit`/`CancelEdit` only call `IEditableObject` on `Current` (`src/Majorsilence.Forms/BindingSource.cs:550-561`).
- **Upstream:** `CurrencyManager.EndCurrentEdit` → `PullData` on every binding, then `IEditableObject.EndEdit`, then `ICancelAddNew.EndNew(Position)` (`CurrencyManager.cs:466-487`); `CancelCurrentEdit` → `CancelEdit`, `ICancelAddNew.CancelNew`, then `OnItemChanged` → `PushData` re-reads every control (`CurrencyManager.cs:297-319, 883-909`). `BindingSource.EndEdit`/`CancelEdit` forward (`BindingSource.cs:612-628, 483`).
- **Impact:** Save via a `ToolStripButton`/menu/`Ctrl+S` (nothing takes focus, so `Validated` never fires) followed by `bs.EndEdit()` loses the pending value of every OnValidation binding — the default mode. `Cancel` → `bs.CancelEdit()` does not restore the controls' text, and a row from `BindingList<T>.AddNew()` is never removed.
- **Fix:** give the manager a `Bindings` that `Binding.Attach` populates (BND-16), then implement `EndCurrentEdit` = `foreach b in Bindings: b.WriteValue(); (Current as IEditableObject)?.EndEdit(); (List as ICancelAddNew)?.EndNew(Position)`, and `CancelCurrentEdit` = `CancelEdit(); CancelNew(Position); foreach b: b.ReadValue()`. Route `BindingSource.EndEdit/CancelEdit` through them.
- **Test:** `box.DataBindings.Add("Text", bs, "Name"); box.Text = "Grace"; bs.EndEdit(); Assert.Equal("Grace", person.Name);` and `var bl = new BindingList<Person>(); bs.DataSource = bl; bs.AddNew(); bs.CancelEdit(); Assert.Empty(bl);`
- **Tests today:** BindingSourceRuntimeTests `EndEdit_and_CancelEdit_reach_an_editable_current_item` (IEditableObject only).

### BND-09 — No `IEditableObject.BeginEdit` when an item becomes current, so `CancelEdit` cannot roll back a `DataRowView` — Cat B — P1 — High
- **Ours:** nothing calls `BeginEdit` (`src/Majorsilence.Forms/BindingContext.cs:35-47`, `src/Majorsilence.Forms/BindingSource.cs:182-198`).
- **Upstream:** `CurrencyManager.OnCurrentChanged` calls `editableObject.BeginEdit()` on the new current item (`CurrencyManager.cs:843-850`).
- **Impact:** `DataRowView` commits each column write immediately unless inside `BeginEdit`, so after editing two bound text boxes, `bs.CancelEdit()` reverts nothing and `dataSet.HasChanges()` is already true. This is the standard Cancel button of every DataSet form.
- **Fix:** in the manager's position-change path (and on initial resolve), `(Current as IEditableObject)?.BeginEdit()` after raising `CurrentChanged`.
- **Test:** `bs.DataSource = table; box.DataBindings.Add("Text", bs, "Name"); box.Text = "X"; box.DataBindings[0].WriteValue(); bs.CancelEdit(); Assert.Equal("Ada", table.Rows[0]["Name"]);`
- **Tests today:** none.

### BND-10 — `BindingSource.PositionChanged` never raised — Cat D — P1 — High
- **Ours:** `Position` setter raises `CurrentChanged` and `CurrentItemChanged` only (`src/Majorsilence.Forms/BindingSource.cs:182-198`); `PositionChanged`/`OnPositionChanged` exist but have no caller (`src/Majorsilence.Forms/AppMenuBindingParity.cs:461-462, 480-481`).
- **Upstream:** `CurrencyManager_PositionChanged` → `OnPositionChanged` (`BindingSource.cs:558-561`), fed by `ChangeRecordState` (`CurrencyManager.cs:379-383`).
- **Impact:** any `bs.PositionChanged += UpdateStatusBar` handler is dead; upstream `BindingNavigator` refreshes on it (`BindingNavigator.cs:899`).
- **Fix:** in the `Position` setter call `OnPositionChanged(EventArgs.Empty)` after `OnCurrentChanged` (upstream order: CurrentChanged, CurrentItemChanged, then PositionChanged).
- **Test:** `int n = 0; bs.PositionChanged += (_,_) => n++; bs.Position = 1; Assert.Equal(1, n);`
- **Tests today:** none.

### BND-11 — `BindingNavigator.BindingSource` and item properties are stored-only; nothing is wired — Cat C — P1 — High
- **Ours:** `BindingSource`, `MoveFirstItem` … `CountItem` are auto-properties (`src/Majorsilence.Forms/WinFormsCompat.cs:2446-2471`); `AddStandardItems` creates buttons with no `Click` handlers and never updates `PositionItem.Text`/`CountItem.Text`, `Enabled` (`src/Majorsilence.Forms/RemainingParity.cs:235-258`). No `RefreshItemsCore`, no `Validate()`.
- **Upstream:** `WireUpBindingSource` subscribes to `PositionChanged/CurrentChanged/…/ListChanged` (`BindingNavigator.cs:882-911`); `RefreshItemsCore` sets `Enabled`, `PositionItem.Text = position+1`, `CountItem.Text = string.Format(CountItemFormat, count)` (`:493-569`); item setters hook `Click` → `MoveFirst/MovePrevious/…/AddNew/RemoveCurrent` (`:707-732`); `PositionItem` Enter/LostFocus → `AcceptNewPosition` (`:597, 737-756`).
- **Impact:** every `BindingNavigator` on a migrated form is a row of dead buttons showing "0" and "of {0}".
- **Fix:** implement the `BindingSource` setter as `WireUpBindingSource`; give each item setter a `Click` hook; add `RefreshItemsCore` and call it from every subscribed event and from `EndInit`.
- **Test:** `nav.AddStandardItems(); nav.BindingSource = bs; Assert.Equal("1", nav.PositionItem.Text); Assert.Equal("of 2", nav.CountItem.Text); nav.MoveNextItem.PerformClick(); Assert.Equal(1, bs.Position);`
- **Tests today:** RemainingParityTests `BindingNavigator_AddStandardItems_*` (item names only).

### BND-12 — `BindingNavigator.EndInit` calls `AddStandardItems`, which `Items.Clear()`s the designer's items — Cat A — P1 — High
- **Ours:** `EndInit() => AddStandardItems()` and `AddStandardItems` starts with `Items.Clear()` and assigns fresh buttons to `MoveFirstItem` etc. (`src/Majorsilence.Forms/RemainingParity.cs:235-264`).
- **Upstream:** `EndInit` only `RefreshItemsInternal()` (`BindingNavigator.cs:90-94`); `AddStandardItems` "does NOT remove any previous items" and is called by the designer only when the control is *dropped*, not from `InitializeComponent` (`:109-126`).
- **Impact:** `InitializeComponent` creates `bindingNavigatorMoveFirstItem` etc., adds them plus any custom `ToolStripButton`s (`SaveItem`), assigns `nav.MoveFirstItem = bindingNavigatorMoveFirstItem`, wires `bindingNavigatorSaveItem.Click += Save`, then `EndInit()` — which throws all of them away and replaces them with unwired copies. The Save button vanishes from the strip and the `Click` handler is attached to an orphan.
- **Fix:** `EndInit` → refresh only; remove `Items.Clear()` from `AddStandardItems` (or keep it only when `Items.Count == 0`).
- **Test:** `nav.BeginInit(); var save = new ToolStripButton("Save"); nav.Items.Add(save); nav.EndInit(); Assert.Contains(save, nav.Items.Cast<ToolStripItem>());`
- **Tests today:** RemainingParityTests `BindingNavigator_AddStandardItems_is_idempotent` (asserts the clearing behaviour indirectly).

### BND-13 — `Binding.WriteValue` writes `default(T)` into a value-type source on parse failure or empty text — Cat A — P1 — High
- **Ours:** `Coerce` returns `null` when `Convert.ChangeType` throws (`src/Majorsilence.Forms/BindingRuntime.cs:314-324`) and `member.SetValue(source, null)` on an `int` property assigns 0; an empty string becomes `DataSourceNullValue` (null) which `Coerce` turns into `Activator.CreateInstance(int)` = 0 (`BindingRuntime.cs:136-141, 302-305`). BindingRuntimeTests.cs:155-166 is named "leaves the source alone" but asserts `Assert.Equal(0, person.Age)`.
- **Upstream:** `PullData`: a parse exception sets `parseFailed`, the control is reset to the source value and **the source is not written** (`Binding.cs:873-915`); with `FormattingEnabled` a `BindingComplete` with `Exception` is raised instead (`:927-944`). `Formatter.ParseObjectInternal("", int)` throws via `int.Parse` (`Formatter.cs:310-317, 374`).
- **Impact:** typing "4-" or clearing an `Age`/`Price`/`Quantity` box silently zeroes the record. With `OnValidation` that is on every focus change.
- **Fix:** make `Coerce` signal failure (return a sentinel / `out bool ok`); in `WriteValue` on failure re-run `ReadValue()` (upstream's reset) and skip `SetValue`; only map `""` → `DataSourceNullValue` when `FormattingEnabled` and the target is nullable/reference, otherwise treat it as a parse failure for value types.
- **Test:** `person.Age = 7; box.Text = "4-"; box.DataBindings[0].WriteValue(); Assert.Equal(7, person.Age); Assert.Equal("7", box.Text);`
- **Tests today:** BindingRuntimeTests.cs `A_half_typed_number_does_not_throw_and_leaves_the_source_alone` (asserts the divergent behaviour; must be inverted).

### BND-14 — `BindingSource.ResetBindings`/`ResetCurrentItem`/`ResetItem` do not refresh simple-bound controls — Cat B — P1 — High
- **Ours:** these raise `ListChanged` only (`src/Majorsilence.Forms/BindingSource.cs:382-388, 565-566`). `Binding` subscribes to `manager.CurrentChanged` and the current item's `INotifyPropertyChanged` only (`src/Majorsilence.Forms/BindingRuntime.cs:166-177`); `CurrencyManager.ItemChanged/ListChanged/MetaDataChanged` are never raised (`src/Majorsilence.Forms/FinalParity.cs:302-317`), nor is `BindingManagerBase.CurrentItemChanged` (`src/Majorsilence.Forms/TailParity.cs:303-304`).
- **Upstream:** `ListChanged` reaches the manager (`BindingSource` *is* its list), `List_ListChanged` → `OnItemChanged` → `PushData` to every binding (`CurrencyManager.cs:762-770, 883-909`); `ItemChanged` at the current index also raises `CurrentItemChanged` (`:764-767, 878-881`).
- **Impact:** the canonical "I changed the POCO in code, refresh the form" call — `bs.ResetCurrentItem()` / `bs.ResetBindings(false)` — does nothing for any source that does not implement `INotifyPropertyChanged`. `bs.CurrentItemChanged` never fires for a property change (only for a position move).
- **Fix:** have the manager subscribe to its list's `ListChanged` (BND-02) and raise `ItemChanged`/`CurrentItemChanged`; have `Binding` subscribe to `ItemChanged` (index == -1 or == Position) and `ReadValue`.
- **Test:** `var p = new PlainPerson{Name="Ada"}; bs.DataSource = new List<PlainPerson>{p}; box.DataBindings.Add("Text", bs, "Name"); p.Name = "Grace"; bs.ResetCurrentItem(); Assert.Equal("Grace", box.Text);`
- **Tests today:** none.

### BND-15 — `Control.BindingContext` auto-creates a private context and never re-homes bindings when parented — Cat A — P1 — High
- **Ours:** getter is `binding_context ?? Parent?.BindingContext ?? (binding_context = new BindingContext())` (`src/Majorsilence.Forms/Control.Compat.cs:494-497`): an unparented control mints its own. `Binding.Attach` captures that manager once (`src/Majorsilence.Forms/BindingRuntime.cs:60`); setting `BindingContext` or `Parent` later does not re-resolve, and `BindingContextChanged` is a no-op stub (COMPATIBILITY_MATRIX.md:148). `BindingContext.UpdateBinding` only swaps the property and uses `BindingMember` where `Attach` uses `BindingPath`, so it lands on a different key (`src/Majorsilence.Forms/TailParity.Two.cs:142-148` vs `BindingRuntime.cs:60`).
- **Upstream:** `BindingContextInternal` is own-or-parent's, **null** when unparented (`Control.cs:1033-1050`); `OnParentBindingContextChanged` → `OnBindingContextChanged` → `UpdateBindings` re-homes every binding into the form's context (`Control.cs:6740-6752, 7007-7013, 10932`); `UpdateBinding` moves the binding between managers' `Bindings` (`BindingContext.cs:345-364`).
- **Impact:** designer code calls `textBox1.DataBindings.Add("Text", ds.Customers, "Name")` *before* `Controls.Add(textBox1)`. Every control bound to a plain `DataTable`/`List<T>` therefore gets its own `CurrencyManager`: two text boxes bound to the same table do not move together, and neither follows a grid bound to that table. (Sources that are a `BindingSource` escape because it hands out its own manager.)
- **Fix:** return `Parent?.BindingContext` (null when unparented) and defer `Attach`'s manager lookup until a context exists; on `Parent`/`BindingContext` change, raise `BindingContextChanged` and for each binding `Detach` subscriptions → re-resolve manager via `BindingPath` → resubscribe → `ReadValue`. Fix `UpdateBinding` to use `BindingPath`.
- **Test:** `var a = new TextBox(); var b = new TextBox(); a.DataBindings.Add("Text", list, "Name"); b.DataBindings.Add("Text", list, "Name"); form.Controls.AddRange(a, b); form.BindingContext[list].Position = 1; Assert.Equal(list[1].Name, a.Text); Assert.Equal(a.Text, b.Text);`
- **Tests today:** WindowDataBindingParityTests (form-level only), none for re-homing.

### BND-16 — `BindingManagerBase.Bindings` is always empty; `Binding` never registers with its manager — Cat A — P1 — High
- **Ours:** `Bindings => bindings ??= new BindingsCollection()` (`src/Majorsilence.Forms/TailParity.cs:281-283`) and nothing ever `Add`s; `Attach` only stores `BindingManagerBase` (`src/Majorsilence.Forms/BindingRuntime.cs:60`).
- **Upstream:** `UpdateBinding`/`SetBindingManagerBase` add the binding to `newManager.Bindings` (`BindingContext.cs:361-362`); `PullData`/`PushData` iterate it (`BindingManagerBase.cs:237-275`).
- **Impact:** root cause of BND-08 and BND-14 — the manager has nothing to pull/push; `form.BindingContext[ds].Bindings.Count` (used by generic "commit all" helpers) is 0.
- **Fix:** `Attach` → `BindingManagerBase?.Bindings.Add(this)`; `Detach` → `Remove`; then the manager can drive `ReadValue`/`WriteValue` instead of each binding subscribing individually.
- **Test:** `box.DataBindings.Add("Text", list, "Name"); Assert.Equal(1, box.BindingContext[list].Bindings.Count);`
- **Tests today:** none.

### BND-17 — `ControlBindingsCollection.Add` ignores `DefaultDataSourceUpdateMode`, allows duplicates and null sources — Cat A — P1 — Medium
- **Ours:** the 3/4-arg `Add` builds a `Binding` whose mode is the constant `OnValidation` (`src/Majorsilence.Forms/WinFormsCompat.cs:158, 263-268`); `DefaultDataSourceUpdateMode` is a stored auto-property (`:281`); `InsertItem` never checks for an existing binding on the same property (`src/Majorsilence.Forms/BindingRuntime.cs:342-349`); `dataSource` may be null.
- **Upstream:** short `Add` overloads pass `DefaultDataSourceUpdateMode` (`ControlBindingsCollection.cs:60-84`); `AddCore`/`CheckDuplicates` throw `ArgumentException` for a second binding to the same property (`:169-203`); `ArgumentNullException.ThrowIfNull(dataSource)` (`:147`).
- **Impact:** `DataBindings.DefaultDataSourceUpdateMode = OnPropertyChanged` (the usual "make it live" line) has no effect; a re-run `Bind()` silently stacks a second binding and both write, last wins.
- **Fix:** short overloads → `updateMode: DefaultDataSourceUpdateMode`; in `InsertItem` throw on a duplicate `PropertyName` and on a null `DataSource`.
- **Test:** `c.DataBindings.DefaultDataSourceUpdateMode = OnPropertyChanged; var b = c.DataBindings.Add("Text", p, "Name"); Assert.Equal(OnPropertyChanged, b.DataSourceUpdateMode); Assert.Throws<ArgumentException>(() => c.DataBindings.Add("Text", p, "Name"));`
- **Tests today:** none.

### BND-18 — `Binding.BindingComplete` (and `BindingManagerBase`/`BindingSource.BindingComplete`, `DataError`) never raised — Cat D — P2 — High
- **Ours:** all declared under `#pragma warning disable CS0067` (`src/Majorsilence.Forms/TailParity.cs:272-275, 299-308`; `src/Majorsilence.Forms/AppMenuBindingParity.cs:467-475`); `BindingCompleteEventArgs.ErrorText/Exception` are settable stubs (`src/Majorsilence.Forms/WinFormsCompat.cs:3855-3875`).
- **Upstream:** raised after every push/pull when `FormattingEnabled`, carrying `BindingCompleteState.Exception` and the exception (`Binding.cs:927-944, 1004-1012`); forwarded by the manager and `BindingSource` (`BindingSource.cs:574-577`).
- **Impact:** the documented way to show a conversion error (`bs.BindingComplete += (s,e) => { if (e.Exception != null) errorProvider.SetError(...) }`) never fires; errors are swallowed (or, per BND-13, become zeros).
- **Fix:** in `ReadValue`/`WriteValue` when `FormattingEnabled`, wrap conversion in try/catch, build `BindingCompleteEventArgs` with state/exception, raise on the binding, then on `BindingManagerBase`, then `BindingSource`.
- **Test:** `var b = box.DataBindings.Add("Text", p, "Age", true); BindingCompleteEventArgs? got = null; b.BindingComplete += (_,e) => got = e; box.Text = "x"; b.WriteValue(); Assert.Equal(BindingCompleteState.Exception, got!.BindingCompleteState);`
- **Tests today:** none.

### BND-19 — `BindingSource.SuspendBinding`/`ResumeBinding` only mute `ListChanged`; manager suspend is a no-op — Cat B — P2 — High
- **Ours:** `SuspendBinding` sets a flag consulted only by `OnListChanged` (`src/Majorsilence.Forms/AppMenuBindingParity.cs:365-379`, `src/Majorsilence.Forms/BindingSource.cs:327-331`); `BindingManagerBase.SuspendBinding/ResumeBinding` are empty (`src/Majorsilence.Forms/BindingContext.cs:65-68`) and `IsBindingSuspended` is a settable flag nothing reads (`src/Majorsilence.Forms/TailParity.cs:286`).
- **Upstream:** the manager's `ShouldBind` flips, `UpdateIsBinding` sets `IsBinding = false` on every binding so reads/writes stop; `ResumeBinding` resets position to 0 (`CurrencyManager.cs:963-995`; `BindingManagerBase.cs:281`).
- **Impact:** the batch-edit idiom `bs.SuspendBinding(); foreach (…) row[...] = …; bs.ResumeBinding();` still pushes every intermediate value into the controls (INPC path) and, worse, control edits during suspension still write back.
- **Fix:** make `IsBindingSuspended` real on the manager, have `Binding.ReadValue/WriteValue` return early when the manager is suspended, and `ResumeBinding` → `Position = Count>0?0:-1` + push.
- **Test:** `bs.SuspendBinding(); box.Text = "X"; Assert.Equal("Ada", person.Name); person.Name = "Y"; Assert.Equal("X", box.Text); bs.ResumeBinding(); Assert.Equal("Y", box.Text);`
- **Tests today:** none.

### BND-20 — `CurrentChanged`/`PositionChanged` order inverted — Cat A — P2 — High
- **Ours:** `PositionChanged` then `CurrentChanged` (`src/Majorsilence.Forms/BindingContext.cs:44-45`).
- **Upstream:** `OnCurrentChanged` (which also fires `CurrentItemChanged`) then `OnPositionChanged` (`CurrencyManager.cs:374-383, 863-866`).
- **Impact:** a `PositionChanged` handler that reads a bound control sees the previous record's value (the binding re-reads on `CurrentChanged`).
- **Fix:** swap the two `Invoke`s; raise `CurrentItemChanged` with `CurrentChanged`.
- **Test:** record event names in a list from both handlers; assert `["CurrentChanged","PositionChanged"]`.
- **Tests today:** none.

### BND-21 — `BindingSource.Position` unclamped and drifts from its `CurrencyManager` — Cat A — P2 — High
- **Ours:** "Stored as given, not clamped" (`src/Majorsilence.Forms/BindingSource.cs:182-198`); the pushed manager clamps (`src/Majorsilence.Forms/BindingContext.cs:38`) while the guard stops it pushing back, so `bs.Position == 99` while `bs.CurrencyManager.Position == Count-1`; `bs.Position = -1` on a non-empty list leaves `Current == null` while the manager is at 0.
- **Upstream:** `CurrencyManager.Position` clamps `<0 → 0`, `>= count → count-1`, ignores the set when the list is empty (`CurrencyManager.cs:219-245`); `BindingSource.Position` is just a forwarder (`BindingSource.cs:359-372`).
- **Impact:** `bs.Position = bs.Count` (a common off-by-one after `AddNew`) leaves `Current` null here and every bound control blank; upstream lands on the last row.
- **Fix:** delegate `Position` to the manager (get and set) once BND-01 gives it a stable manager.
- **Test:** `bs.Position = 99; Assert.Equal(1, bs.Position); Assert.Same(list[1], bs.Current); bs.Position = -1; Assert.Equal(0, bs.Position);`
- **Tests today:** BindingSourceTests `MoveNext_Invoke_AdvancesAndClamps` (through Move*, not the setter).

### BND-22 — `BindingSource.AllowNew`/`AllowEdit`/`AllowRemove` are constant `true`; `ResetAllowNew` no-op — Cat A — P2 — High
- **Ours:** auto-properties initialised to `true` (`src/Majorsilence.Forms/BindingSource.cs:607-614`); `AllowNew` setter raises nothing (`src/Majorsilence.Forms/AppMenuBindingParity.cs:444`).
- **Upstream:** derived from the list — `IBindingList.AllowNew`, else `!IsReadOnly && !IsFixedSize && has default ctor`; `AllowRemove` likewise; enumerable snapshots report `false`; setting `AllowNew` fires `ListChanged(Reset)` and throws on a read-only list (`BindingSource.cs:105-133, 1642-1674`).
- **Impact:** a grid/navigator over an array or a read-only list shows an add row / enabled Add; `AddNew` then throws `NotSupportedException` from the list. Setting `AllowNew = false` does not remove the grid's new row because no reset is raised.
- **Fix:** compute from `_list` unless explicitly set; raise `ListChanged(Reset)` in the setter.
- **Test:** `bs.DataSource = new Person[2]; Assert.False(bs.AllowNew); Assert.False(bs.AllowRemove);`
- **Tests today:** none.

### BND-23 — `Binding.ReadValue` honours `ControlUpdateMode.Never`; upstream forces the push — Cat A — P2 — High
- **Ours:** `if (!IsBinding || ControlUpdateMode == ControlUpdateMode.Never) return;` (`src/Majorsilence.Forms/BindingRuntime.cs:85-86`).
- **Upstream:** `ReadValue() => PushData(force: true)` and `force` bypasses the `Never` check (`Binding.cs:961-970, 1023`).
- **Impact:** an explicit `binding.ReadValue()` is exactly how a `Never`-mode binding is refreshed on demand; here it is dead.
- **Fix:** move the `Never` check into the event-driven callers (`OnSourceCurrentChanged`/`OnSourcePropertyChanged`) and let `ReadValue` always read.
- **Test:** `var b = box.DataBindings.Add("Text", p, "Name"); b.ControlUpdateMode = Never; p.Name = "X"; b.ReadValue(); Assert.Equal("X", box.Text);`
- **Tests today:** none.

### BND-24 — `Binding.DataSourceNullValue` defaults to `null` (upstream `DBNull.Value`) and empty text → null even with `FormattingEnabled == false` — Cat E — P2 — High
- **Ours:** `public object? DataSourceNullValue { get; set; }` (`src/Majorsilence.Forms/TailParity.cs:257`); `WriteValue` maps `""` to it unconditionally (`src/Majorsilence.Forms/BindingRuntime.cs:136-139`).
- **Upstream:** default `DBNull.Value` for value types / null for reference types (`Formatter.cs:538-543`); the `""` → null mapping only exists on the `FormattingEnabled` path and only when the text equals `NullValue` (`Binding.cs:706-735`, `Formatter.cs:289-292, 397-410`); a legacy binding writes `""` to a string property.
- **Impact:** clearing a bound text box writes `null` into a `string` property that upstream would set to `""` — a `NOT NULL` column then fails on save; code that reads `DataSourceNullValue` gets null instead of `DBNull`.
- **Fix:** default to `DBNull.Value`; only substitute it when `FormattingEnabled` and (`value == NullValue` or target is not `string`).
- **Test:** `box.DataBindings.Add("Text", p, "Name"); box.Text = ""; box.DataBindings[0].WriteValue(); Assert.Equal("", p.Name);`
- **Tests today:** BindingRuntimeTests `NullValue_stands_in_for_a_null_source_value` (read side only).

### BND-25 — `BindableComponent.DataBindings` is built over a null component → NRE on first `Add` — Cat A — P2 — High
- **Ours:** `data_bindings ??= new ControlBindingsCollection (null!)` (`src/Majorsilence.Forms/MissingTypesParity.cs:844`); `Attach(null)` dereferences `component.GetType()` (`src/Majorsilence.Forms/BindingRuntime.cs:44-51`).
- **Upstream:** `new ControlBindingsCollection(this)` (`BindableComponent.cs:77-78`).
- **Impact:** any user type deriving `BindableComponent` (the upstream base for bindable non-controls) crashes on `DataBindings.Add`.
- **Fix:** pass `this`.
- **Test:** `new MyBindable().DataBindings.Add("Tag", p, "Name")` does not throw.
- **Tests today:** none.

### BND-26 — `ListBox`/`ComboBox` do not share a `CurrencyManager` through `BindingContext` for plain sources — Cat A — P2 — Medium
- **Ours:** `ListSourceTracker.Attach` only tracks position when the source is an `ICurrencyManagerProvider` (`src/Majorsilence.Forms/DataSourceBinding.cs:76-83`); a `DataTable`/`List<T>` source has no manager at all.
- **Upstream:** `SetDataConnection` takes `BindingContext[newDataSource, displayMember.BindingPath]` and follows its `PositionChanged`/`ItemChanged` (`ListControl.cs:672-750, 394-420`).
- **Impact:** `listBox.DataSource = table; textBox.DataBindings.Add("Text", table, "Name")` — selecting in the list does not move the text box (works only if both go through a `BindingSource`).
- **Fix:** resolve `BindingContext[dataSource, ""]` as a `CurrencyManager` in `Attach` when the source is a list and share it (depends on BND-15).
- **Test:** same as BND-15 with a `ListBox` and a `TextBox` over one `List<Person>`.
- **Tests today:** ListControlDataSourceTrackingTests (BindingSource path only).

### BND-27 — `ListControl.FormattingEnabled`/`FormatString`/`FormatInfo`/`Format` are stored-only — Cat C — P2 — High
- **Ours:** auto-properties and an event with no raiser (`src/Majorsilence.Forms/WinFormsBaseControls.cs:84-133`; `src/Majorsilence.Forms/ListBox.cs:671`; `src/Majorsilence.Forms/ComboBox.cs:144`); `GetItemText` goes straight to `DataSourceBinding.DisplayText` (`ListBox.cs:229-233`).
- **Upstream:** `GetItemText` raises `Format` and applies `FormatString`/`FormatInfo` when `FormattingEnabled` (`ListControl.cs:537` and surrounding).
- **Impact:** a combo of dates/decimals shows raw `ToString()`; `comboBox.Format += (s,e) => e.Value = …` (the standard "display Last, First" trick) never runs.
- **Fix:** in `GetItemText`, when `FormattingEnabled`: raise `Format(new ListControlConvertEventArgs(value, typeof(string), item))`, then apply `FormatString` via `IFormattable`.
- **Test:** `lb.FormattingEnabled = true; lb.Format += (_,e) => e.Value = "X"; Assert.Equal("X", lb.GetItemText(item));`
- **Tests today:** none.

### BND-28 — `BindingContext.UpdateBinding` keys on `BindingMember`, does not move subscriptions — Cat A — P2 — High
- **Ours:** `newBindingContext[binding.DataSource, binding.BindingMemberInfo.BindingMember]` and only sets the property (`src/Majorsilence.Forms/TailParity.Two.cs:142-148`); `Attach` keys on `BindingPath` (`src/Majorsilence.Forms/BindingRuntime.cs:60`).
- **Upstream:** `EnsureListManager(binding.DataSource, binding.BindingMemberInfo.BindingPath)` and moves the binding between `Bindings` collections (`BindingContext.cs:345-364`).
- **Impact:** after `UpdateBinding`, the binding reports a manager keyed `(ds, "Name")` that no other binding shares, while still listening to the old one.
- **Fix:** use `BindingPath`; unsubscribe/resubscribe and `ReadValue`.
- **Test:** `BindingContext.UpdateBinding(ctx, b); Assert.Same(ctx[list], b.BindingManagerBase);`
- **Tests today:** none.

### BND-29 — `BindingContext.CollectionChanged` never raised — Cat D — P2 — High
- **Ours:** `#pragma warning disable CS0067` (`src/Majorsilence.Forms/TailParity.Two.cs:151-154`); the indexer adds to `managers` without raising (`src/Majorsilence.Forms/BindingContext.cs:108-120`).
- **Upstream:** `AddCore`/`RemoveCore` raise `OnCollectionChanged` (`BindingContext.cs:98-140`).
- **Impact:** niche (designers/tooling).
- **Fix:** raise `CollectionChanged(Add)` when a manager is created.
- **Test:** count handler calls across two distinct lookups.
- **Tests today:** none.

### BND-30 — `Binding` target property lookup is case-sensitive CLR-only and rejects read-only targets even with `ControlUpdateMode.Never` — Cat A — P3 — High
- **Ours:** `GetType().GetProperty(PropertyName, Public|Instance)` and throws when `!CanWrite` (`src/Majorsilence.Forms/BindingRuntime.cs:51-58`).
- **Upstream:** `TypeDescriptor.GetProperties`, `OrdinalIgnoreCase`, read-only allowed when `ControlUpdateMode.Never` (`Binding.cs:502-534`).
- **Impact:** `DataBindings.Add("text", …)` (case slip, works upstream) throws here.
- **Fix:** `TypeDescriptor.GetProperties(component).Find(PropertyName, true)`.
- **Tests today:** BindingRuntimeTests `Binding_a_property_that_does_not_exist_is_reported_not_ignored`.

### BND-31 — `PropertyManager.Position` is `-1` (upstream constant 0) — Cat A — P3 — High
- **Ours:** base ctor evaluates `Count` before `DataSource` is set, so `position` stays -1 (`src/Majorsilence.Forms/BindingContext.cs:17-23`, `src/Majorsilence.Forms/BindingRuntime.cs:328-337`).
- **Upstream:** `Position => 0`, `Count => 1` (`PropertyManager.cs:181-193`).
- **Fix:** override `Position` in `PropertyManager` to return 0.
- **Tests today:** none.

### BND-32 — `Binding.NullValue`/`FormatString`/`FormatInfo`/`FormattingEnabled`/`ControlUpdateMode` setters do not re-push — Cat C — P3 — High
- **Ours:** plain setters (`src/Majorsilence.Forms/WinFormsCompat.cs:142, 161, 164`; `src/Majorsilence.Forms/TailParity.cs:251-254`).
- **Upstream:** each setter calls `PushData()` when `IsBinding` (`Binding.cs:296-421`).
- **Fix:** call `ReadValue()` in each setter when attached.
- **Tests today:** none.

### BND-33 — `Binding` ctor overloads with `nullValue`/`formatString`/`formatInfo` missing — Cat E — P3 — High
- **Ours:** only the 4-arg (defaulted) and 5-arg ctors (`src/Majorsilence.Forms/WinFormsCompat.cs:115-130`); `ControlBindingsCollection.Add` has the long overloads (`src/Majorsilence.Forms/OverloadParity.cs:317-345`) but `new Binding(…, nullValue, formatString, formatInfo)` does not compile.
- **Upstream:** 6/7/8-arg ctors (`Binding.cs:99-160`).
- **Fix:** add the three ctors forwarding to the property setters.

### BND-34 — `BindingSource.ApplySort(ListSortDescriptionCollection)` does not record `Sort`; `Filter` setter raises no `ListChanged` on a non-view list — Cat A — P3 — High
- **Ours:** `src/Majorsilence.Forms/AppMenuBindingParity.cs:399-405`; `src/Majorsilence.Forms/BindingSource.cs:214-222`.
- **Upstream:** `ApplySort` sets `_sort` from the descriptions (`BindingSource.cs:~1000-1010`).
- **Fix:** build the expression string and `RecordSortExpression`.

### BND-35 — `ListBox.SelectedValue` setter leaves the selection unchanged when the value is not found — Cat A — P3 — Medium
- **Ours:** loop falls through without assigning (`src/Majorsilence.Forms/ListBox.cs:621-640`; same shape in `ComboBox.cs:475-495`).
- **Upstream:** `SelectedIndex = DataManager.Find(...)` → `-1` deselects (`ListControl.cs:354-386`).
- **Fix:** set `SelectedIndex = -1` after the loop.

## Low-priority / Win32-only (P3) — one line each
- `Binding.ControlAtDesignTime` skip of push/pull — designer-host only; no portable meaning.
- `Binding.IsBinding` true without a manager (`BindingRuntime.cs:40`) vs upstream requiring `DataSource`+manager+`ComponentCreated` (`Binding.cs:1129-1140`) — only observable via the flag itself.
- `BindingSource.Find(string)` walking a non-searchable list instead of throwing — documented deliberate divergence.
- `BindingSource.ResetAllowNew` no-op — already in NoOpStubBaseline; moot until BND-22 makes `AllowNew` computed.
- `BindingNavigator.BeginInit` no-op — harmless once BND-12 stops `EndInit` clearing items.
- `BindingNavigator.MoveFirstItem`… typed `ToolStripButton?` vs upstream `ToolStripItem?` — a designer assigning a `ToolStripMenuItem` would not compile; cosmetic otherwise.

## Systemic patterns
- **The CurrencyManager is a value, not an object.** It is constructed over an `IList` reference, never listens to that list, and `BindingSource` discards and rebuilds it on every re-resolve. Everything that depends on a stable, live manager — bindings attached before data arrives (BND-01), position tracking through list mutation (BND-02, BND-21), `ItemChanged`/`MetaDataChanged`/`CurrentItemChanged` (BND-14), suspend/resume (BND-19), `PositionChanged` on the source (BND-10) — fails together. One fix (a single long-lived manager with `SetList` + `ListChanged` subscription) unblocks all of them.
- **`Binding` is a peer of the manager instead of a member of it.** Each binding subscribes to events itself and `Bindings` stays empty (BND-16), so the manager cannot `PullData`/`PushData`; that is why `EndCurrentEdit`/`CancelCurrentEdit` (BND-08), `ResetBindings` (BND-14), suspend (BND-19) and `UpdateBinding` (BND-28) have nothing to act on. Register bindings with the manager and route reads/writes through it.
- **CLR reflection where upstream uses `TypeDescriptor`.** The source member (BND-03), the target property (BND-30) and `ListBindingHelper.GetProperty` use `GetProperty`; only the list controls use descriptors. `DataRowView`, `DynamicObject`s and `ICustomTypeDescriptor` sources are invisible to `Binding`.
- **Conversion failure is coerced to `default(T)` instead of reported.** `Coerce` returning `null` for "could not convert" is indistinguishable from "convert to null", so value types get 0/false written (BND-13); the same swallowing is why `BindingComplete` has nothing to carry (BND-18) and `Validating` cannot cancel (BND-07). Give conversion a failure channel.
- **`ResolveList` has a catch-all `_ => new List<object?>()`.** `Type`, scalar objects and non-DataSet `DataMember` paths all fall into it (BND-04, BND-05, BND-06) and come out as an empty, untyped, silent list.
- **Events with a natural trigger that is never pulled.** `BindingSource.PositionChanged`, `BindingComplete`, `DataError`, `CurrencyManager.ItemChanged/ListChanged/MetaDataChanged`, `BindingContext.CollectionChanged`, `BindingContextChanged` — all declared, all have an `On*` or an obvious call site, none called.
- **Two tests lock in divergent behaviour** and will need inverting with the fixes: `BindingRuntimeTests.A_half_typed_number_does_not_throw_and_leaves_the_source_alone` (asserts 0 written) and `BindingSourceTests.DataSource_SetNonList_IsEmpty` (asserts a scalar source is empty).
