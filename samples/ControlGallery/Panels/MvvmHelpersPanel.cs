using ControlGallery.ViewModels;
using Majorsilence.Forms;
using Majorsilence.Forms.Mvvm;

namespace ControlGallery.Panels
{
    /// <summary>
    /// The counter of <see cref="CommunityToolkitMvvmPanel"/>, wired with <c>Majorsilence.Forms.Mvvm</c> instead of by hand:
    /// <c>Observe</c> pushes view-model state into controls, <c>BindCommand</c> follows each command's <c>CanExecute</c> and runs it on
    /// click, and a <c>BindingScope</c> releases every subscription when the panel is disposed. None of it uses reflection, so a trimmed
    /// or NativeAOT app needs nothing rooted for it.
    /// </summary>
    public class MvvmHelpersPanel : Panel
    {
        private readonly BindingScope scope = new ();

        public MvvmHelpersPanel ()
        {
            var vm = new CounterViewModel ();

            var countLabel = new Label { Left = 100, Top = 100, Width = 200, Height = 24 };
            var incrementButton = new Button { Text = "Increment", Left = 100, Top = 140, Width = 100, Height = 30 };
            var decrementButton = new Button { Text = "Decrement", Left = 210, Top = 140, Width = 100, Height = 30 };
            var resetButton = new Button { Text = "Reset", Left = 320, Top = 140, Width = 100, Height = 30 };
            var nameLabel = new Label { Text = "Name:", Left = 100, Top = 195, Width = 60, Height = 24 };
            var nameTextBox = new TextBox { Left = 165, Top = 192, Width = 200, Height = 28 };
            var greetingLabel = new Label { Left = 100, Top = 235, Width = 400, Height = 24 };

            // State into controls: the current value now, and again on every change of that property. The lambda reads the value, so
            // nothing is looked up by name at run time.
            vm.Observe (nameof (CounterViewModel.Count), v => v.Count, count => countLabel.Text = $"Count: {count}").AddTo (scope);
            vm.Observe (nameof (CounterViewModel.Greeting), v => v.Greeting, greeting => greetingLabel.Text = greeting).AddTo (scope);

            // Commands onto controls: Enabled follows CanExecute (the decrement button greys out at zero with no code here, because the
            // view model raises CanExecuteChanged), and a click runs the command.
            incrementButton.BindCommand (vm.IncrementCommand).AddTo (scope);
            decrementButton.BindCommand (vm.DecrementCommand).AddTo (scope);
            resetButton.BindCommand (vm.ResetCommand).AddTo (scope);

            // Text typed into the box goes to the view model, and the greeting above follows it back.
            nameTextBox.TextChanged += (_, _) => vm.Name = nameTextBox.Text;

            Controls.Add (countLabel);
            Controls.Add (incrementButton);
            Controls.Add (decrementButton);
            Controls.Add (resetButton);
            Controls.Add (nameLabel);
            Controls.Add (nameTextBox);
            Controls.Add (greetingLabel);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
                scope.Dispose ();

            base.Dispose (disposing);
        }
    }
}
