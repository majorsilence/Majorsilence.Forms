# Majorsilence.Forms.WinFormsShims.Compat

A Roslyn **source generator** that emits `System.Windows.Forms`- and `System.Drawing`-namespace
compatibility surfaces backed by [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms),
so source written against WinForms — including Designer-generated `*.Designer.cs` files — compiles
unchanged.

It exists for the case where rewriting namespaces is not an option: a distributed control library
whose own public API exposes `System.Windows.Forms`/`System.Drawing` types, and whose consumers
cannot be asked to change their code.

```
dotnet add package Majorsilence.Forms.WinFormsShims.Compat
```

Installing it brings in `Majorsilence.Forms` as a dependency, because the generated types derive from
that assembly's `Control`/`Component` hierarchy.

## Scope — read this first

This is a **proof of concept**, published so it can be evaluated against real code. The generator
runs the same six passes independently for each of two **namespace mappings** it knows about --
`Majorsilence.Forms` -> `System.Windows.Forms` (the WinForms surface) and
`Majorsilence.Forms.Drawing` -> `System.Drawing` (the GDI+-shaped drawing types real WinForms code
also expects: `Font`, `Brush`, `Pen`, `Bitmap`, ...). A member on one mapping's static classes (or a
wrapper's constructor, from #1b) can return or accept a type from the *other* mapping too
(translation always consults both).

1. Every public, non-sealed, non-generic **class** with an accessible constructor gets a same-named
   subclass with forwarding constructors — not just `Component`/`Control`/`WindowBase` descendants
   like `Button`, `Form` and `DataGridView`, but any plain class, like `ApplicationContext` or
   `FormCollection`. `System.EventArgs` descendants (`PaintEventArgs`, `MouseEventArgs`, …) are the
   one deliberate exception, because #6 gives them a different, purpose-built treatment.
1b. Every public, non-generic **sealed** class with an accessible constructor — the shape #1 always
   excludes, on purpose, and which for `Majorsilence.Forms.Drawing` covers exactly the leaf types real
   drawing code needs most: `Font`, `SolidBrush`, `Pen`, `Bitmap`, `Icon`, `FontFamily`, … — gets a
   **wrapper** class instead of a subclass: it holds the real instance, forwards every translatable
   constructor/method/property (instance *and* static — `Brushes.Black`-style framework classes exist
   for these types too) to it, and declares an implicit conversion operator in each direction. That
   last part is what makes it work as more than a read-only view: `new Font(FontFamily.GenericMonospace, 13)`
   constructs a real `Majorsilence.Forms.Drawing.Font` under the hood, and passing that compat `Font`
   to a still-Majorsilence-typed API (e.g. `Graphics.DrawString`, since `Graphics` itself has no
   accessible constructor and so never becomes a wrapper) compiles with **no cast anywhere** — the
   conversion operator applies automatically, the same as any user-defined implicit conversion.
2. Every public enum (`DialogResult`, `MessageBoxButtons`, `Keys`, `FontStyle`, …) gets an identical,
   same-valued copy — needed because #1, #1b, #4, #5 and #6's forwarded signatures surface these
   constantly, and code that only imports the target namespace has no other way to name them.
3. Every public **interface** (`IMessageFilter`, `IWin32Window`, `IDataObject`, …) gets a same-named,
   empty sub-interface, so it can be named, implemented, and accepted as a parameter under
   `System.Windows.Forms`. This only works as an *input*: a value the framework hands back out was
   never constructed as that marker sub-interface, so it can't safely be cast to it — #5 rejects any
   member that would need to.
4. Every public static utility class (`Application`, `MessageBox`, `Clipboard`, `SystemInformation`,
   `SystemColors`, `ControlPaint`, `TextRenderer`, `Brushes`, `Pens`, `ColorTranslator`, …) gets a
   same-named static class that forwards each member whose signature is fully translatable by
   #1/#1b/#2/#3's rules. A member is silently dropped, not emitted broken, when its signature can't
   be translated — see below for what that excludes.
5. `Control`'s own Paint/Mouse/Key/Drag/gesture event family (every event `Control` itself declares
   whose delegate's second parameter is a Majorsilence-specific `EventArgs` — 26 events across 17
   distinct `EventArgs` types, as of this writing) gets: a compat `EventArgs` **wrapper** class per
   type (forwarding its translatable public properties to the real instance it wraps — unlike #1b's
   wrappers, only properties, and no implicit conversions, since these are only ever received from a
   compat event, never constructed with `new`), a compat delegate copy where the original wasn't
   already the generic `EventHandler<T>`, and, on every compat subclass (from #1) that inherits the
   pair, a `new event` shadow plus a same-named `protected virtual On*` hook that a further subclass
   can override the normal WinForms way. Both `control.Paint += handler;` and
   `protected override void OnPaint(PaintEventArgs e)` work as a result — see the class doc comment on
   `WinFormsCompatGenerator` for the mechanics, and `BACKLOG.md` for why this has to be re-emitted per
   subclass rather than solved once on `Control`.

Out of scope:

- Any Majorsilence.Forms.Drawing type with **no accessible constructor at all**, sealed or not —
  `Graphics` itself is the concrete example: every one of its constructors is `internal`/`private`
  (matching real `System.Drawing.Graphics`, which nobody constructs directly either — you get one
  from `CreateGraphics()`, `Graphics.FromImage()`, or a paint event). Neither #1 nor #1b can subclass
  or wrap a type with nothing to call, so it stays exposed as the plain Majorsilence type (still fully
  usable — `PaintEventArgs.Graphics` works exactly as before this package added Drawing coverage;
  passing it to a wrapped type's methods works too, via that type's own conversion operators).
- A wrapper's (#1b) own `Equals`/`GetHashCode`/`ToString`, if the original overrides them: they get
  forwarded as ordinary (non-`override`) methods that hide `object`'s, which works for direct,
  statically-typed calls but not through a boxed/`object`-typed reference.
- Any event whose delegate isn't declared directly on `Control` itself — a `TreeView`-specific event
  like `AfterSelect`, say. #5 is scoped to exactly what `Control` declares; extending it to
  control-specific event families is future work, not attempted here.
- A static-class member (or #1b wrapper member) whose signature involves: a plain Majorsilence.Forms
  type with no compat counterpart from #1/#1b/#3 (a struct, a delegate, or a class with no accessible
  constructor at all, like `Graphics`); handing an interface-typed value (#3) back out as the compat
  type, rather than accepting one as a parameter; an array; a `ref`/`out`/`in` parameter; a generic
  method; or an extension method.
- An `EventArgs` wrapper's (#5) methods (only its properties are forwarded) and public constructors (a
  wrapper can only be received from a compat event/override, never constructed directly with `new`) --
  unlike #1b's wrappers, which forward methods too and support `new`.

**A correctness note on returned values.** A static-class member (or `EventArgs` wrapper property)
that hands a *class* instance back out casts it to the compat subclass with `as`, not a hard cast --
deliberately, because the actual instance isn't always one. `Brushes.Black` is typed `Brush` but is
actually always a `SolidBrush` singleton the framework built once and cached, never constructed
through the compat subclass at all; a hard cast there would throw on every single access. `as` turns
that into `null` instead. The common case -- `Application.MainForm`, say, where the instance really
is whatever compat `Form` the caller constructed -- still gets the real value.

Expect to hit all of these on a non-trivial WinForms project. The compatibility matrix in the
repository records what the underlying layer does and does not implement, which applies here
unchanged: this package changes the *namespace* your code compiles against, not the behaviour
behind it.
