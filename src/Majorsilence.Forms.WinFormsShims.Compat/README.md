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
runs the same six passes independently for each of eight **namespace mappings** it knows about:
`Majorsilence.Forms` -> `System.Windows.Forms`, `Majorsilence.Forms.Drawing` -> `System.Drawing` (the
GDI+-shaped drawing types real WinForms code also expects: `Font`, `Brush`, `Pen`, `Bitmap`, ...), and
one each for `Printing` -> `System.Drawing.Printing`, `Design`/`Design.Behavior` ->
`System.ComponentModel.Design`/`System.Windows.Forms.Design.Behavior`, and
`Drawing.Drawing2D`/`Drawing.Imaging`/`Drawing.Imaging.Metafiles`/`Drawing.Text` -> their real
`System.Drawing.*` counterparts — real WinForms code reaches into all of these (`PrintDocument`,
`UITypeEditor`, `GraphicsPath`, `PixelFormat`, `TextRenderingHint`, ...) about as often as it reaches
into the two original mappings. A member on one mapping's static classes (or a wrapper's constructor,
from #1b) can return or accept a type from *any* other mapping too (translation always consults all
of them).

1. Every public, non-sealed, non-generic **class** with an accessible constructor gets a same-named
   subclass with forwarding constructors — not just `Component`/`Control`/`WindowBase` descendants
   like `Button`, `Form` and `DataGridView`, but any plain class, like `ApplicationContext` or
   `FormCollection`. `System.EventArgs` descendants (`PaintEventArgs`, `MouseEventArgs`, …) are
   excluded, because #6 gives them a different, purpose-built treatment — and so is a small, curated
   set of **forced polymorphic roots** (today: just `Brush`), which get #1b's wrapper treatment
   instead even though they aren't sealed; see #1b's own entry for why.
1b. Every public, non-generic **sealed** class with an accessible constructor — the shape #1 always
   excludes, on purpose, and which for `Majorsilence.Forms.Drawing` covers exactly the leaf types real
   drawing code needs most: `Font`, `SolidBrush`, `Pen`, `Bitmap`, `Icon`, `FontFamily`, … — gets a
   **wrapper** class instead of a subclass: it holds the real instance, forwards every translatable
   constructor/method/property, walking the real type's *entire base chain* so inherited API is
   forwarded too (`Bitmap.Width`/`.Height`, declared on `Image`, not `Bitmap` itself), implements
   `IDisposable` and forwards `Dispose()` when the real type does, and declares an implicit conversion
   operator in each direction. That last part is what makes it work as more than a read-only view:
   `new Font(FontFamily.GenericMonospace, 13)` constructs a real `Majorsilence.Forms.Drawing.Font`
   under the hood, and passing that compat `Font` to a still-Majorsilence-typed API (e.g.
   `Graphics.DrawString`, since `Graphics` itself has no accessible constructor and so never becomes a
   wrapper) compiles with **no cast anywhere** — the conversion operator applies automatically, the
   same as any user-defined implicit conversion.

   `Brush` is wrapped for a different reason than sealedness: pass 1's subclasses are *flat* --
   `SolidBrush` derives directly from real `Majorsilence.Forms.Drawing.SolidBrush`, never from a compat
   `Brush` -- so a plain `Brush b = someSolidBrush;` used to fail even though every name in it is
   ordinary GDI+. Wrapping `Brush` fixes that for every existing Drawing leaf with no change to the
   leaf types themselves: a leaf that's a pass-1 *subclass* reaches it for free (one built-in upcast
   plus `Brush`'s own real-to-compat operator, which is exactly the one user-defined conversion C#
   allows per implicit conversion); a leaf that's itself a pass-1b *wrapper* (true of every real GDI+
   brush, since they're all sealed) needs one extra, directly-emitted bridging operator instead, since
   two sibling wrappers can't chain their own conversions into each other (that would need two
   user-defined operators, which C# refuses) -- see the `ForcedPolymorphicRootNames` remarks in
   `WinFormsCompatGenerator.cs` for exactly how each case is handled. **`Control` cannot get the same
   treatment** despite being the far more common case: real code genuinely subclasses `Control`
   directly (`samples/WinFormsCompatDemo/BinaryRainPanel.cs`'s `RainCanvas : Control` does, to override
   `OnPaint`), and a wrapper can never be subclassed. Making `Control` a wrapper breaks that outright
   (`CS0509: cannot derive from sealed type 'Control'`) — confirmed empirically, not assumed. A bare
   `Control v = someTextBox;` still needs the workaround: declare `v` as the real
   `Majorsilence.Forms.Control` instead (still satisfied by every compat leaf, which transitively *is*
   one) -- see BACKLOG.md's "what a real internal-code migration surfaces" for the full investigation.
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
4b. A type with **no accessible constructor at all** (`Graphics` is the concrete example) gets no
   instance type from #1 or #1b, but its *static* members (`Graphics.FromImage`, `.FromHwnd`, ...) are
   still useful on their own and get the same static-forwarding treatment as #4, honestly: the value a
   factory like `Graphics.FromImage` returns is still the real Majorsilence.Forms type, since there's
   no compat instance type for it to become.
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

- **Polymorphic storage through `Control` itself** — see #1b above; this is the current headline gap,
  not a minor edge case. `Brush` has the equivalent problem fixed; `Control` cannot be, for reasons
  intrinsic to C#'s single inheritance, not an oversight here.
- A wrapper's (#1b) own `Equals`/`GetHashCode`/`ToString`, if the original overrides them: they get
  forwarded as ordinary (non-`override`) methods that hide `object`'s, which works for direct,
  statically-typed calls but not through a boxed/`object`-typed reference.
- Any event whose delegate isn't declared directly on `Control` itself — a `TreeView`-specific event
  like `AfterSelect`, say. #5 is scoped to exactly what `Control` declares; extending it to
  control-specific event families is future work, not attempted here.
- A static-class member (or #1b wrapper member) whose signature involves: a plain Majorsilence.Forms
  type with no compat counterpart from #1/#1b/#3 (a struct, a delegate, or a class with no accessible
  constructor and no public static members of its own); handing an interface-typed value (#3) back out
  as the compat type, rather than accepting one as a parameter; an array; a `ref`/`out`/`in` parameter;
  a generic method; or an extension method.
- An `EventArgs` wrapper's (#5) methods (only its properties are forwarded) and public constructors (a
  wrapper can only be received from a compat event/override, never constructed directly with `new`) --
  unlike #1b's wrappers, which forward methods too and support `new`.

**A correctness note on returned values.** A static-class member (or `EventArgs` wrapper property)
that hands back a value typed to a *subclass*-mapped type (#1) casts it to the compat subclass with
`as`, not a hard cast -- deliberately, because the actual instance isn't always one. `SystemFonts`'s
members are the concrete example: each is typed `Font`, but `Font` is a #1b *wrapper* now (not a
subclass), so those go through the wrapper's implicit conversion instead and always succeed --
`Brushes.Black` used to be this section's example before `Brush` became a wrapper for the same
reason; a genuinely-illustrative remaining case is anything returning a *non-wrapped* subclass-mapped
type where the framework built the instance itself and cached it, rather than the caller having
constructed it through the compat subclass: a hard cast there would throw on every access, so a
failed `as` becomes `null` instead. The common case -- `Application.MainForm`, say, where the instance
really is whatever compat `Form` the caller constructed -- still gets the real value either way.

Expect to hit all of these on a non-trivial WinForms project. The compatibility matrix in the
repository records what the underlying layer does and does not implement, which applies here
unchanged: this package changes the *namespace* your code compiles against, not the behaviour
behind it.
