# PoC results

Validates the feasibility plan's step 4/5 ("take one small unmodified WinForms sample... report
what fraction compiles unmodified"). `Form1.cs`/`Form1.Designer.cs` in this project are exactly
that: an ordinary designer-generated `Form` (a `Button`, `Label`, `TextBox`, `Click`/`TextChanged`
handlers, `AcceptButton`, layout suspend/resume) with **zero source changes**, no `using
Majorsilence.Forms` anywhere, referencing only `Majorsilence.Forms` + the
`Majorsilence.Forms.WinFormsShims.Compat` generator.

## Result: compiles clean, 0 errors, 0 warnings

The generator emitted 101 subclasses (`Button`, `Form`, `Label`, `TextBox`, `ComboBox`,
`DataGridView`, `TreeView`, `ListView`, `MenuStrip`, `ToolStrip`, ... — see `Generated/` if you
build with `EmitCompilerGeneratedFiles=true`), each as predicted in the feasibility plan: e.g.

```csharp
namespace System.Windows.Forms
{
    public class Button : global::Majorsilence.Forms.Button
    {
        public Button() : base()
        {
        }
    }
}
```

## Confirmed boundary: two categories the plan flagged as out of scope, verified empirically

Isolated single-statement checks (not committed — reproduce by pasting into a scratch `.cs` file
in this project):

- `Application.Run(new Form1())` → `CS0103: The name 'Application' does not exist in the current
  context`, at the time this was written. `Majorsilence.Forms.Application` is a `static` class, so
  the generator's `Component`-derived eligibility filter correctly excludes it — but so does every
  other static WinForms utility class (`MessageBox`, `SystemInformation`, `Clipboard`, ...); none of
  these were covered by this generator, and they can't be subclassed — there's nothing to subclass.
  **Superseded below** (2026-09-05): a second generator pass now forwards these member-by-member
  instead. `Cursor` is the one static-shaped exception left uncovered — it's not actually a `static`
  class (it has instance members alongside `Cursor.Current`), so it falls under neither pass here.
- `private void Form2_Paint(object sender, PaintEventArgs e)` wired to `Control.Paint` →
  `CS0246: The type or namespace name 'PaintEventArgs' could not be found`. This is the
  event/delegate covariance gap from the plan: `Majorsilence.Forms.PaintEventArgs` isn't part of
  the generated surface (it doesn't derive from `Component`), so a handler typed to a
  `System.Windows.Forms.PaintEventArgs` has nothing to bind to. Confirmed real and confirmed to be
  the *first* thing that breaks once a form's code-behind does anything beyond plain-`EventHandler`
  events (`Click`, `TextChanged`, `Resize`, ...), which is why `Form1.cs`/`Form1.Designer.cs` here
  deliberately only uses `Click`/`TextChanged`.

## Reading this result

For a typical designer-generated form that mostly sets properties and wires plain `Click`/
`TextChanged`/similar `EventHandler`-typed events — a large share of real WinForms code — this
compiles unmodified today.

## Increment: static utility classes + enum copies (2026-09-05)

The first of the two further increments named above is done. `Program.cs` calls
`Application.Run(new Form1())`, and `Form1.button1_Click` calls `MessageBox.Show(this, text,
caption, MessageBoxButtons.OKCancel, MessageBoxIcon.Question)` and compares the result against
`DialogResult.OK`, then `Application.Exit()` — all through unmodified `using
System.Windows.Forms;` source. **Compiles clean, 0 errors, 0 warnings.**

This project (renamed from `Majorsilence.Forms.WinFormsShims.Compat.PoCTest`, moved from `src/` to
`samples/`) now references the Avalonia backend and builds `WinExe`, so it isn't just a compile-time
check any more — `dotnet run --project samples/WinFormsCompatDemo` opens a real window and the
`Application.Run`/`MessageBox.Show` calls above actually execute.

This needed a second generator pass most of the way there for free: `MessageBoxButtons` and
`DialogResult` are Majorsilence.Forms enums, and code with only `using System.Windows.Forms;` in
scope has no way to name a type that isn't in that namespace -- so the generator now also emits an
identical, same-valued copy of every public Majorsilence.Forms enum (**197** as of this run) under
`System.Windows.Forms`, converting with an explicit cast at each forwarding call site (safe because
the values are documented to match upstream WinForms). A third pass then forwards every public
static utility class (**24**: `Application`, `MessageBox`, `Clipboard`, `SystemInformation`,
`SystemColors`/`SystemBrushes`/`SystemPens`, `ControlPaint`, `TextRenderer`, `SendKeys`,
`ToolStripManager`, `Help`, `ColorTranslator`, …) member-by-member, dropping (not breaking) any
member whose signature it can't translate -- see the README's scope section for the exact rejection
list. `Application.Run(Form)`, `Application.MainForm` and the full `MessageBox.Show` overload set
all survive that filter; `Application.OpenForms` (returns `FormCollection`, a plain class with no
`Component` ancestor), `Application.Run(ApplicationContext)` and `Application.AddMessageFilter`
(interface parameter) do not.

Still open: the `new event` shadow + `On*` override generation for Majorsilence-specific `EventArgs`
events (`Paint`, mouse, keyboard) described in the feasibility plan — not attempted here.

## Increment: subclass every eligible class, not just Component descendants, plus interfaces (2026-09-05)

The three rejects named directly above are now generated correctly:

```csharp
public static void Run (global::System.Windows.Forms.ApplicationContext context)
    => global::Majorsilence.Forms.Application.Run (context);
public static void AddMessageFilter (global::System.Windows.Forms.IMessageFilter value)
    => global::Majorsilence.Forms.Application.AddMessageFilter (value);
public static global::System.Windows.Forms.FormCollection OpenForms
    => (global::System.Windows.Forms.FormCollection)(global::Majorsilence.Forms.Application.OpenForms);
```

The generator's first pass no longer requires `Component`/`Control`/`WindowBase` ancestry to subclass
a class — only that it's public, non-sealed, non-generic, and not a `System.EventArgs` descendant
(deliberately excluded: see the README for why a compat `PaintEventArgs` subclass wouldn't actually
help `Control.Paint`). `ApplicationContext` and `FormCollection` are ordinary classes under that
rule now, same mechanism as `Button`/`Form` — subclass count went from 101 to **252**. `FormCollection`
in particular has no *explicit* constructor at all in Majorsilence.Forms; the compiler-synthesized
implicit public parameterless one is enough for the generator to subclass it, which wasn't obvious
going in.

A new pass alongside the enum copies does the same for public interfaces (**20**: `IMessageFilter`,
`IWin32Window`, `IDataObject`, `IBindableComponent`, …) — an empty sub-interface extending the
original, usable as a parameter type but *not* as a return type (a value the framework hands back was
never constructed as the marker sub-interface, so casting one back out would fail at runtime for
every real value; the generator rejects that direction outright rather than emit it). That's exactly
why `AddMessageFilter`/`RemoveMessageFilter` now generate but `Clipboard.GetDataObject(): IDataObject`
still doesn't.

Two more static classes crossed the "has at least one forwardable member" threshold as a result:
`Cursors` (returns `Cursor`, a plain non-`Component` class, now subclassed) and `DataFormats` (returns
`DataFormat`, likewise). Static-class count: 24 → **26**.

`samples/WinFormsCompatDemo` builds clean, 0 errors/0 warnings, and runs, unchanged by this increment.

Still open: the `new event`/`On*` event-shadowing increment (see below, now done for `Control`'s own
family), and the theoretical gap this widening exposed rather than closed — a Majorsilence.Forms type
with **no** compat counterpart at all (a struct, a delegate, or a class with truly no accessible
constructor). Nothing in the assembly is currently shaped that way, so it hasn't blocked anything yet.

## Increment: event shadowing for Control's Paint/Mouse/Key/Drag/gesture family (2026-09-05)

Both halves of the last named gap now work, through unmodified `System.Windows.Forms` source:

```csharp
public class PaintDemoPanel : Panel
{
    public int PaintCount { get; private set; }

    protected override void OnPaint (PaintEventArgs e)   // the compat PaintEventArgs, overriding
    {                                                      // the compat-declared virtual hook
        PaintCount++;
        base.OnPaint (e);                                 // keeps the shadowed Paint event working
    }
}

// Form1.cs:
paintPanel.MouseDown += (sender, e) => label1.Text = $"Mouse {e.Button} at {e.X},{e.Y}";
paintPanel.KeyDown += (sender, e) => { label1.Text = $"Key {e.KeyCode}"; e.Handled = true; };
paintPanel.Paint += (sender, e) => label1.Text = $"Painted {paintPanel.PaintCount} time(s)";
```

**Compiles clean, 0 errors/0 warnings, and runs** (`dotnet run --project samples/WinFormsCompatDemo`)
without the app crashing — the override chain executes on every real paint cycle Avalonia drives, not
just once at startup.

The mechanism, discovered by walking `Control`'s own declared events rather than hand-listing them
(which found more than a manual read of `Control.cs` alone did — `Control.Events.cs` and other
partials carry several of the `On*` methods): every event `Control` declares whose delegate's second
parameter is a Majorsilence-specific `EventArgs` becomes an *event family*. This run found **26**
across **17** distinct `EventArgs` types (`PaintEventArgs`, `MouseEventArgs`, `KeyEventArgs`,
`KeyPressEventArgs`, `PreviewKeyDownEventArgs`, `DragEventArgs`, `GiveFeedbackEventArgs`,
`QueryContinueDragEventArgs`, `ControlEventArgs`, `InvalidateEventArgs`, `LayoutEventArgs`,
`UICuesEventArgs`, `HelpEventArgs`, and the gesture family `LongPressEventArgs`/
`PinchGestureEventArgs`/`ScrollGestureEventArgs`/`SwipeGestureEventArgs`) — more than the Paint/mouse/
keyboard set originally scoped in BACKLOG.md, because the discovery is driven by the real event
declarations rather than a curated list. `Scroll` and `QueryAccessibilityHelp` are declared as
`add {} remove {}` no-op stubs with no backing `On*` method at all, so they correctly fall out rather
than get a broken shadow.

Each distinct `EventArgs` type gets a wrapper class holding the real instance and forwarding its
translatable public properties (settable ones too — `KeyEventArgs.Handled`/`SuppressKeyPress` round-trip
to the real object, so setting `e.Handled = true` in a compat handler genuinely suppresses the key
the way it does upstream). 13 of the 26 families use a named custom delegate (`PaintEventHandler`,
`MouseEventHandler`, ...) and get a compat delegate copy; the four gesture events use the generic
`EventHandler<T>`, which needed no copy — just reusing the BCL delegate with the compat args type as
its argument — and, along the way, surfaced a real bug: naively naming a generated file after
`delegateType.Name` collided, since every constructed `EventHandler<T>` shares that same short name
regardless of its type argument. Fixed by detecting the generic case and skipping delegate-copy
generation for it entirely.

**104** of the 252 compat subclasses reach at least one of these families (found via
`FindReachableOverridableMethod`/`FindReachableEvent` walking each subclass's own Majorsilence base
chain, not just `Control`) and got a second partial-class file with the override/shadow/hook triple —
confirming the BACKLOG.md finding that this can't be solved once on a shared compat `Control`, since
compat subclasses are flat.

Still explicitly out of scope: any event family not declared directly on `Control` (a `TreeView` or
`DataGridView`-specific `EventArgs` event, say), an `EventArgs` wrapper's methods (only properties are
forwarded) and public constructors (a wrapper can only be received from a compat event, never
constructed with `new`), and `DragEventArgs.Data` (typed `IDataObject`, dropped for the same
interface-return-safety reason as `Clipboard.GetDataObject()`).
