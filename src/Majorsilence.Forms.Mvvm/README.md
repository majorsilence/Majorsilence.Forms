# Majorsilence.Forms.Mvvm

Wiring between a view model and Majorsilence.Forms controls, without reflection, so it survives trimming and NativeAOT
with nothing rooted. It needs only `INotifyPropertyChanged` and `ICommand`: any view model works, including one written
with CommunityToolkit.Mvvm, and this package does not depend on the toolkit.

```csharp
using Majorsilence.Forms.Mvvm;

var scope = new BindingScope ();

// Pushes the current value now, and again on every change of that property, on the UI thread.
viewModel.Observe (nameof (CounterViewModel.Count), vm => vm.Count, count => label.Text = $"Count: {count}").AddTo (scope);

// Enabled follows CanExecute; a click runs the command.
incrementButton.BindCommand (viewModel.IncrementCommand).AddTo (scope);

// When the page is left:
scope.Dispose ();
```

- **`Observe`** takes the property name (`nameof`) and a lambda that reads it, so nothing is looked up by string at run time.
- **`BindCommand`** works on any `Control`, including custom-painted ones, and on menu and tool strip items. A `Button` or a
  `ToolStripItem` can also take a command through its own `Command` property; use one or the other on a control.
- **`BindingScope`** disposes every binding it collected, latest first.
- **`IUiDispatcher`** marshals to the UI thread. The default runs through the platform backend; pass your own in tests.

See `docs/mvvm.md` in the repository for threading, async commands and what is not covered.
