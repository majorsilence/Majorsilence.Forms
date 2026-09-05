# Majorsilence.Forms.WinFormsShims.Compat

A Roslyn **source generator** that emits a `System.Windows.Forms`-namespace compatibility surface
backed by [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms), so source written
against WinForms — including Designer-generated `*.Designer.cs` files — compiles unchanged.

It exists for the case where rewriting namespaces is not an option: a distributed control library
whose own public API exposes `System.Windows.Forms` types, and whose consumers cannot be asked to
change their code.

```
dotnet add package Majorsilence.Forms.WinFormsShims.Compat
```

Installing it brings in `Majorsilence.Forms` as a dependency, because the generated types derive from
that assembly's `Control`/`Component` hierarchy.

## Scope — read this first

This is a **proof of concept**, published so it can be evaluated against real code. The generator
makes five independent passes over `Majorsilence.Forms`:

1. Every public, non-sealed, non-generic **class** with an accessible constructor gets a same-named
   subclass with forwarding constructors — not just `Component`/`Control`/`WindowBase` descendants
   like `Button`, `Form` and `DataGridView`, but any plain class, like `ApplicationContext` or
   `FormCollection`. `System.EventArgs` descendants (`PaintEventArgs`, `MouseEventArgs`, …) are the
   one deliberate exception, because #5 gives them a different, purpose-built treatment.
2. Every public enum (`DialogResult`, `MessageBoxButtons`, `Keys`, …) gets an identical, same-valued
   copy — needed because #1, #3, #4 and #5's forwarded signatures surface these constantly, and code
   that only imports `System.Windows.Forms` has no other way to name them.
3. Every public **interface** (`IMessageFilter`, `IWin32Window`, `IDataObject`, …) gets a same-named,
   empty sub-interface, so it can be named, implemented, and accepted as a parameter under
   `System.Windows.Forms`. This only works as an *input*: a value the framework hands back out was
   never constructed as that marker sub-interface, so it can't safely be cast to it — #4 rejects any
   member that would need to.
4. Every public static utility class (`Application`, `MessageBox`, `Clipboard`,
   `SystemInformation`, `SystemColors`, `ControlPaint`, `TextRenderer`, …) gets a same-named static
   class that forwards each member whose signature is fully translatable by #1-#3's rules. A member
   is silently dropped, not emitted broken, when its signature can't be translated — see below for
   what that excludes.
5. `Control`'s own Paint/Mouse/Key/Drag/gesture event family (every event `Control` itself declares
   whose delegate's second parameter is a Majorsilence-specific `EventArgs` — 26 events across 17
   distinct `EventArgs` types, as of this writing) gets: a compat `EventArgs` **wrapper** class per
   type (forwarding its translatable public properties to the real instance it wraps), a compat
   delegate copy where the original wasn't already the generic `EventHandler<T>`, and, on every
   compat subclass (from #1) that inherits the pair, a `new event` shadow plus a same-named
   `protected virtual On*` hook that a further subclass can override the normal WinForms way. Both
   `control.Paint += handler;` and `protected override void OnPaint(PaintEventArgs e)` work as a
   result — see the class doc comment on `WinFormsCompatGenerator` for the mechanics, and
   `BACKLOG.md` for why this has to be re-emitted per subclass rather than solved once on `Control`.

Out of scope:

- `Majorsilence.Forms.Drawing`'s sealed leaf types (`Font`, `Pen`, `Brush`, …).
- Any event whose delegate isn't declared directly on `Control` itself — a `TreeView`-specific event
  like `AfterSelect`, say. #5 is scoped to exactly what `Control` declares; extending it to
  control-specific event families is future work, not attempted here.
- A static-class member whose signature involves: a plain Majorsilence.Forms type with no compat
  counterpart from #1-#3 (a struct, a delegate, or a class with no accessible constructor at all —
  nothing in the assembly is currently shaped that way, but it's the theoretical gap); handing an
  interface-typed value (#3) back out as the compat type, rather than accepting one as a parameter;
  an array; a `ref`/`out`/`in` parameter; a generic method; or an extension method.
- An `EventArgs` wrapper's methods (only its properties are forwarded) and public constructors (a
  wrapper can only be received from a compat event/override, never constructed directly with `new`).

Expect to hit all of these on a non-trivial WinForms project. The compatibility matrix in the
repository records what the underlying layer does and does not implement, which applies here
unchanged: this package changes the *namespace* your code compiles against, not the behaviour
behind it.
