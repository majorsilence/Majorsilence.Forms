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
makes three independent passes over `Majorsilence.Forms`:

1. Every public, non-sealed type deriving from `Component`, `Control` or `WindowBase` gets a
   same-named subclass with forwarding constructors (e.g. `Button`, `Form`, `DataGridView`, …).
2. Every public enum (`DialogResult`, `MessageBoxButtons`, `Keys`, …) gets an identical, same-valued
   copy — needed because #1 and #3's forwarded signatures surface these constantly, and code that
   only imports `System.Windows.Forms` has no other way to name them.
3. Every public static utility class (`Application`, `MessageBox`, `Clipboard`,
   `SystemInformation`, `SystemColors`, `ControlPaint`, `TextRenderer`, …) gets a same-named static
   class that forwards each member whose signature is fully translatable by #1 and #2's rules. A
   member is silently dropped, not emitted broken, when its signature can't be translated — see
   below for what that excludes.

Out of scope:

- `Majorsilence.Forms.Drawing`'s sealed leaf types (`Font`, `Pen`, `Brush`, …).
- Events typed to Majorsilence-specific `EventArgs`.
- A static-class member whose signature involves a plain Majorsilence.Forms class or interface with
  no `Component` ancestor (`FormCollection`, `ApplicationContext`, `IMessageFilter`, …), an array, a
  `ref`/`out`/`in` parameter, a generic method, or an extension method. `Application.OpenForms`,
  `Application.Run(ApplicationContext)` and `Application.AddMessageFilter` are concrete examples that
  fall out this way today; `Application.Run(Form)`, `Application.MainForm` and `MessageBox.Show(...)`
  do not.

Expect to hit all of these on a non-trivial WinForms project. The compatibility matrix in the
repository records what the underlying layer does and does not implement, which applies here
unchanged: this package changes the *namespace* your code compiles against, not the behaviour
behind it.
