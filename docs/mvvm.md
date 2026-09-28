# MVVM helpers (`Majorsilence.Forms.Mvvm`)

A view model raises `PropertyChanged` and exposes `ICommand`s; a view puts the first into controls and wires the second to clicks.
That wiring is the same in every view, so `Majorsilence.Forms.Mvvm` provides it once. It needs only
`INotifyPropertyChanged` and `ICommand`, so it works with any view model, including one written with CommunityToolkit.Mvvm, and the
package itself does not depend on the toolkit.

```csharp
using Majorsilence.Forms.Mvvm;

var scope = new BindingScope ();

viewModel.Observe (nameof (CounterViewModel.Count), vm => vm.Count, count => label.Text = $"Count: {count}").AddTo (scope);
incrementButton.BindCommand (viewModel.IncrementCommand).AddTo (scope);

// when the page is left
scope.Dispose ();
```

`samples/ControlGallery/Panels/MvvmHelpersPanel.cs` is a complete example, beside `CommunityToolkitMvvmPanel.cs`, which wires the
same view model by hand.

## `Observe`

`source.Observe (propertyName, read, apply)` calls `apply (read (source))` now, and again each time `PropertyChanged` reports that
property. It returns an `IDisposable`.

- **Name it with `nameof`, read it with a lambda.** Nothing is looked up by string at run time, so there is no reflection and
  nothing to root under trimming or NativeAOT.
- A `PropertyChanged` that names no property (`null` or `""`) means every property changed, and refreshes.
- Every push happens on the UI thread. If the change is raised on another thread it is posted through the dispatcher; if it is
  already on the UI thread it applies at once, so order is kept.
- Several changes can queue before the UI thread gets to them. Each queued push reads the value when it runs, so the control never
  shows an older value after a newer one.
- Disposing stops it and drops any push already queued but not yet run.

## `BindCommand`

`control.BindCommand (command, parameter)` sets `Enabled` from `CanExecute` now and whenever `CanExecuteChanged` is raised, and runs
the command with the parameter when the control is clicked and it can execute. There is an overload for menu and tool strip items.

- It works on **any** `Control`, including a custom-painted one, which has no `Command` property.
- A command that reports it cannot run while it is running disables the control for that time with no extra code (CommunityToolkit.Mvvm's
  `AsyncRelayCommand` does this by default; that is its documented behaviour and these tests do not exercise it).
- `CanExecuteChanged` is often raised where an async command finishes, so the control is touched only on the UI thread.
- **Do not combine it with `Button.Command` or `ToolStripItem.Command` on the same control**: both would run the command, so it runs
  twice per click. Use one or the other.
- Disposing stops following and stops running the command, and leaves `Enabled` as the last evaluation set it.

## `BindingScope`

Collects the subscriptions a page makes and disposes them all when the page is left, latest first, so no view leaks a handler on a view
model that outlives it. One that throws does not stop the rest: they are all disposed and the failures come back together in an
`AggregateException`. A binding added after the scope was disposed is disposed at once. `binding.AddTo (scope)` adds and returns it.

## `IUiDispatcher`

```csharp
public interface IUiDispatcher
{
    bool CheckAccess ();
    void Post (Action action);
}
```

The helpers take an optional dispatcher. The default, `UiDispatcher.Default`, asks the active platform backend whether the caller is
on the UI thread and posts through `Application.RunOnUIThread`. In tests pass your own: a fake that reports "not on the UI thread" and
queues what it is given lets a test prove that work is marshalled, and run it when the test chooses.

## What is and is not covered

- Built with the trim and AOT analyzers on, as errors in Release, like the other AOT-compatible packages. It is not part of the
  NativeAOT smoke test.
- The tests use a fake dispatcher. The default dispatcher is tested only for `CheckAccess` on the Headless backend; posting through a
  real backend is what `Application.RunOnUIThread` already does.
- **Two-way text is not included.** The framework's own `DataBindings` does it but is reflective (see "Trimming and NativeAOT" in
  `docs/backends.md`). The sample shows the by-hand form for the direction from control to view model:
  `textBox.TextChanged += (_, _) => vm.Name = textBox.Text;`. A text box that the view model can also change (for example by
  normalising what it is given) needs `Observe` writing back into it as well, and a guard so the two updates do not trigger each
  other; that guard is not provided.
