using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ControlGallery.ViewModels;

/// <summary>
/// A small CommunityToolkit.Mvvm view model backing <see cref="Panels.CommunityToolkitMvvmPanel"/>:
/// a counter (demonstrating <see cref="RelayCommandAttribute"/> with a <c>CanExecute</c> that gets
/// re-evaluated as state changes) and a name/greeting pair (demonstrating a computed property kept in
/// sync via the generated property's <c>On...Changed</c> partial method hook).
/// </summary>
public partial class CounterViewModel : ObservableObject
{
    [ObservableProperty]
    private int count;

    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>Derived from <see cref="Name"/>; not itself an [ObservableProperty] since it has no
    /// backing field, just a change notification raised from <see cref="OnNameChanged"/> below.</summary>
    public string Greeting => string.IsNullOrWhiteSpace (Name) ? "Hello, stranger!" : $"Hello, {Name}!";

    // Source-generated partial method hook: CommunityToolkit.Mvvm calls this automatically whenever
    // the generated Name property's setter runs, before it's invoked, add "partial void
    // OnNameChanging(string value)" too if the check should happen before the field is assigned.
    partial void OnNameChanged (string value) => OnPropertyChanged (nameof (Greeting));

    // Re-evaluates DecrementCommand.CanExecute whenever Count changes, so its bound button greys out
    // exactly when the count reaches zero rather than only checking the very first time.
    partial void OnCountChanged (int value) => DecrementCommand.NotifyCanExecuteChanged ();

    [RelayCommand]
    private void Increment () => Count++;

    [RelayCommand (CanExecute = nameof (CanDecrement))]
    private void Decrement () => Count--;

    private bool CanDecrement () => Count > 0;

    [RelayCommand]
    private void Reset () => Count = 0;
}
