using ControlGallery.ViewModels;
using Majorsilence.Forms;

namespace ControlGallery.Panels
{
    /// <summary>
    /// Demonstrates using CommunityToolkit.Mvvm's source-generated <c>ObservableObject</c>/
    /// <c>RelayCommand</c> with Majorsilence.Forms controls. There is no WPF/UWP-style declarative
    /// binding here -- <see cref="Control.DataBindings"/> is a stub in this framework (see
    /// <c>WinFormsCompat.cs</c>) -- so this wires things up the same way real WinForms/MVVM Toolkit
    /// code does outside of XAML: subscribe to <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>
    /// to push view-model state into control properties, and forward control events into the
    /// generated <c>ICommand</c>s.
    /// </summary>
    public class CommunityToolkitMvvmPanel : Panel
    {
        public CommunityToolkitMvvmPanel ()
        {
            var vm = new CounterViewModel ();

            var countLabel = new Label {
                Text = $"Count: {vm.Count}",
                Left = 100, Top = 100, Width = 200, Height = 24
            };

            var incrementButton = new Button {
                Text = "Increment",
                Left = 100, Top = 140, Width = 100, Height = 30
            };
            incrementButton.Click += (_, _) => vm.IncrementCommand.Execute (null);

            var decrementButton = new Button {
                Text = "Decrement",
                Left = 210, Top = 140, Width = 100, Height = 30,
                Enabled = vm.DecrementCommand.CanExecute (null)
            };
            decrementButton.Click += (_, _) => vm.DecrementCommand.Execute (null);

            var resetButton = new Button {
                Text = "Reset",
                Left = 320, Top = 140, Width = 100, Height = 30
            };
            resetButton.Click += (_, _) => vm.ResetCommand.Execute (null);

            var nameLabel = new Label {
                Text = "Name:",
                Left = 100, Top = 195, Width = 60, Height = 24
            };

            var nameTextBox = new TextBox {
                Left = 165, Top = 192, Width = 200, Height = 28
            };
            nameTextBox.TextChanged += (_, _) => vm.Name = nameTextBox.Text;

            var greetingLabel = new Label {
                Text = vm.Greeting,
                Left = 100, Top = 235, Width = 400, Height = 24
            };

            // The single subscription point: push whichever view-model property changed into the
            // matching control(s). DecrementCommand's CanExecute is re-evaluated as part of the
            // ViewModel's own OnCountChanged hook (see CounterViewModel.cs); this just reflects the
            // already-updated CanExecute() result into the button's Enabled state.
            vm.PropertyChanged += (_, e) => {
                switch (e.PropertyName) {
                    case nameof (CounterViewModel.Count):
                        countLabel.Text = $"Count: {vm.Count}";
                        decrementButton.Enabled = vm.DecrementCommand.CanExecute (null);
                        break;
                    case nameof (CounterViewModel.Greeting):
                        greetingLabel.Text = vm.Greeting;
                        break;
                }
            };

            Controls.Add (countLabel);
            Controls.Add (incrementButton);
            Controls.Add (decrementButton);
            Controls.Add (resetButton);
            Controls.Add (nameLabel);
            Controls.Add (nameTextBox);
            Controls.Add (greetingLabel);
        }
    }
}
