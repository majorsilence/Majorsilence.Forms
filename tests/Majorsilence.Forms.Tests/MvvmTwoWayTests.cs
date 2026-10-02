using System;
using System.Collections.Generic;
using System.ComponentModel;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Mvvm;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The two-way bindings of Majorsilence.Forms.Mvvm. Each direction, the guard that stops them triggering each other, disposal, and
    // marshalling through a fake dispatcher. [Collection ("Headless")] for the same reason MvvmHelpersTests carries it.
    [Collection ("Headless")]
    public class MvvmTwoWayTests
    {
        private sealed class FakeDispatcher : IUiDispatcher
        {
            private readonly Queue<Action> posted = new ();

            public bool OnUiThread { get; set; } = true;

            public bool CheckAccess () => OnUiThread;

            public void Post (Action action) => posted.Enqueue (action);

            public void Pump ()
            {
                while (posted.Count > 0)
                    posted.Dequeue () ();
            }
        }

        private sealed class Model : INotifyPropertyChanged
        {
            private string name = "";
            private bool on;
            private int index = -1;
            private decimal count;

            public event PropertyChangedEventHandler? PropertyChanged;

            // When set, the view model rewrites what it is given, as a real one that normalises input does.
            public bool Shout { get; set; }

            public int NameWrites { get; private set; }

            public string Name { get => name; set => SetName (value); }

            private void SetName (string value)
            {
                if (name == value)
                    return;

                name = value;
                NameWrites++;
                Raise (nameof (Name));
                var shouted = name.ToUpperInvariant ();
                if (Shout && !string.Equals (name, shouted, StringComparison.Ordinal))
                    SetName (shouted);
            }

            public bool On { get => on; set { on = value; Raise (nameof (On)); } }

            public int Index { get => index; set { index = value; Raise (nameof (Index)); } }

            public decimal Count { get => count; set { count = value; Raise (nameof (Count)); } }

            public void Announce () => Raise (nameof (Name));

            private void Raise (string property) => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (property));
        }

        public MvvmTwoWayTests () => HeadlessRenderer.Use ();

        // Every binding that is not about marshalling is given a dispatcher that says "this is the UI
        // thread". The default one asks the shared Headless backend, whose UI thread is whichever thread
        // built the first window anywhere in the run (see MvvmHelpersTests' UiDispatcher test), so these
        // failed whenever xunit scheduled this class on another thread -- adding an unrelated test class
        // to the collection was enough to make three of them fail every run.
        private readonly FakeDispatcher ui = new ();

        [Fact]
        public void BindText_PushesTheValueNow_AndFollowsTheViewModel ()
        {
            var model = new Model { Name = "Pip" };
            var box = new TextBox ();
            using var binding = box.BindText (model, nameof (Model.Name), m => m.Name, (m, v) => m.Name = v, ui);

            Assert.Equal ("Pip", box.Text);

            model.Name = "Sunny";
            Assert.Equal ("Sunny", box.Text);
        }

        [Fact]
        public void BindText_WritesTheControlsTypingBack ()
        {
            var model = new Model ();
            var box = new TextBox ();
            using var binding = box.BindText (model, nameof (Model.Name), m => m.Name, (m, v) => m.Name = v, ui);

            box.Text = "Bo";

            Assert.Equal ("Bo", model.Name);
            Assert.Equal (1, model.NameWrites);
        }

        [Fact]
        public void BindText_DoesNotAnswerAChangeTheViewModelMakesWhileHandlingTheControls ()
        {
            // The view model upper-cases what it is given. Answering that by writing "AB" into the box would move the caret under the
            // person typing, and would raise TextChanged a second time.
            var model = new Model { Shout = true };
            var box = new TextBox ();
            var changes = 0;
            using var binding = box.BindText (model, nameof (Model.Name), m => m.Name, (m, v) => m.Name = v, ui);
            box.TextChanged += (_, _) => changes++;

            box.Text = "ab";

            Assert.Equal ("AB", model.Name);
            Assert.Equal ("ab", box.Text);
            Assert.Equal (1, changes);
        }

        private sealed class CountingTextBox : TextBox
        {
            public int Writes { get; private set; }

            public override string Text {
                get => base.Text;
                set {
                    Writes++;
                    base.Text = value;
                }
            }
        }

        [Fact]
        public void BindText_WritesTheControlOnlyWhenTheValueDiffers ()
        {
            var model = new Model { Name = "same" };
            var box = new CountingTextBox ();
            using var binding = box.BindText (model, nameof (Model.Name), m => m.Name, (m, v) => m.Name = v, ui);
            var writesAfterBinding = box.Writes;

            // The same value announced again (a "changed" event with nothing new in it) must not touch the control, or the caret moves.
            model.Name = "same";
            model.Announce ();

            Assert.Equal (writesAfterBinding, box.Writes);
        }

        [Fact]
        public void Disposing_StopsBothDirections ()
        {
            var model = new Model { Name = "a" };
            var box = new TextBox ();
            var binding = box.BindText (model, nameof (Model.Name), m => m.Name, (m, v) => m.Name = v, ui);
            binding.Dispose ();
            binding.Dispose ();

            model.Name = "from the model";
            Assert.Equal ("a", box.Text);

            box.Text = "from the box";
            Assert.Equal ("from the model", model.Name);
        }

        [Fact]
        public void ViewModelChanges_FromAnotherThread_AreMarshalledToTheUiThread ()
        {
            var dispatcher = new FakeDispatcher ();
            var model = new Model ();
            var box = new TextBox ();
            using var binding = box.BindText (model, nameof (Model.Name), m => m.Name, (m, v) => m.Name = v, dispatcher);

            dispatcher.OnUiThread = false;
            model.Name = "later";
            Assert.Equal ("", box.Text);

            dispatcher.Pump ();
            Assert.Equal ("later", box.Text);
        }

        [Fact]
        public void BindChecked_BindSelectedIndex_AndBindValue_GoBothWays ()
        {
            var model = new Model ();
            var check = new CheckBox ();
            var combo = new ComboBox ();
            combo.Items.Add ("a");
            combo.Items.Add ("b");
            var number = new NumericUpDown { Minimum = 1, Maximum = 240 };
            using var scope = new BindingScope ();
            scope.Add (check.BindChecked (model, nameof (Model.On), m => m.On, (m, v) => m.On = v, ui));
            scope.Add (combo.BindSelectedIndex (model, nameof (Model.Index), m => m.Index, (m, v) => m.Index = v, ui));
            scope.Add (number.BindValue (model, nameof (Model.Count), m => m.Count, (m, v) => m.Count = v, ui));

            check.Checked = true;
            Assert.True (model.On);
            model.On = false;
            Assert.False (check.Checked);

            combo.SelectedIndex = 1;
            Assert.Equal (1, model.Index);
            model.Index = 0;
            Assert.Equal (0, combo.SelectedIndex);

            number.Value = 30;
            Assert.Equal (30m, model.Count);
            model.Count = 999;
            Assert.Equal (240m, number.Value);
        }
    }
}
