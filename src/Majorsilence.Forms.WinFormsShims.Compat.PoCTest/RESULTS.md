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
  context`. `Majorsilence.Forms.Application` is a `static` class, so the generator's
  `Component`-derived eligibility filter correctly excludes it — but so does every other static
  WinForms utility class (`MessageBox`, `Cursor`, `SystemInformation`, `Clipboard`, ...). None of
  these are covered by this generator; they'd need a separate, differently-shaped mechanism (they
  can't be subclassed — there's nothing to subclass).
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
compiles unmodified today. Two further increments would materially widen coverage before this is
more than a PoC: (1) hand-written or generated forwarding wrappers for the handful of static
utility classes real apps always touch (`Application`, `MessageBox` at minimum), and (2) the
`new event` shadow + `On*` override generation for Majorsilence-specific `EventArgs` events
(`Paint`, mouse, keyboard) described in the feasibility plan — not attempted in this PoC.
