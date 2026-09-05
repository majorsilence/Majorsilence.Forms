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
makes four independent passes over `Majorsilence.Forms`:

1. Every public, non-sealed, non-generic **class** with an accessible constructor gets a same-named
   subclass with forwarding constructors — not just `Component`/`Control`/`WindowBase` descendants
   like `Button`, `Form` and `DataGridView`, but any plain class, like `ApplicationContext` or
   `FormCollection`. `System.EventArgs` descendants (`PaintEventArgs`, `MouseEventArgs`, …) are the
   one deliberate exception — see below for why.
2. Every public enum (`DialogResult`, `MessageBoxButtons`, `Keys`, …) gets an identical, same-valued
   copy — needed because #1, #3 and #4's forwarded signatures surface these constantly, and code
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

Out of scope:

- `Majorsilence.Forms.Drawing`'s sealed leaf types (`Font`, `Pen`, `Brush`, …).
- Events typed to Majorsilence-specific `EventArgs`. Giving `PaintEventArgs` a compat subclass
  (like #1 gives `Button`) wouldn't actually enable `control.Paint += handler;`: C#'s method-group
  contravariance requires a handler's parameter to be the delegate's declared type *or a base of
  it*, never a more-derived subclass, so a handler typed to the compat subclass could never bind to
  `Control.Paint` (still declared against the original `PaintEventArgs`) anyway. This needs a
  different mechanism — `new event` shadows plus `On*` override generation — not attempted here.
- A static-class member whose signature involves: a plain Majorsilence.Forms type with no compat
  counterpart from #1-#3 (a struct, a delegate, or a class with no accessible constructor at all —
  nothing in the assembly is currently shaped that way, but it's the theoretical gap); handing an
  interface-typed value (#3) back out as the compat type, rather than accepting one as a parameter;
  an array; a `ref`/`out`/`in` parameter; a generic method; or an extension method.

Expect to hit all of these on a non-trivial WinForms project. The compatibility matrix in the
repository records what the underlying layer does and does not implement, which applies here
unchanged: this package changes the *namespace* your code compiles against, not the behaviour
behind it.
