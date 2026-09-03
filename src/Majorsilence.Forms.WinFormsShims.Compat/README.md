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
covers public, non-sealed types deriving from `Component`, `Control` or `WindowBase`. Out of scope:

- `Majorsilence.Forms.Drawing`'s sealed leaf types (`Font`, `Pen`, `Brush`, …).
- Events typed to Majorsilence-specific `EventArgs`.

Expect to hit both on a non-trivial WinForms project. The compatibility matrix in the repository
records what the underlying layer does and does not implement, which applies here unchanged: this
package changes the *namespace* your code compiles against, not the behaviour behind it.
