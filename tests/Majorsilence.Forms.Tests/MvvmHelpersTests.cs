using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Mvvm;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Majorsilence.Forms.Mvvm: Observe pushes a view model's state into a control, BindCommand wires an ICommand to a control, and
    // BindingScope disposes them together. Threading is tested through a fake dispatcher, because what is being proved is that work is
    // posted when the caller is not on the UI thread and applied at once when it is.
    //
    // [Collection ("Headless")]: one test below calls HeadlessRenderer.Use (), which sets the truly global, process-wide
    // Backends.Platform.Backend -- missing this tag (found by a real, twice-reproduced Windows CI failure, not guessed) let it run
    // concurrently with any of the 100+ other test classes that also touch that same ambient backend and read back a stale or
    // swapped one mid-assertion. Every one of those already carries this same collection name for the same reason.
    [Collection ("Headless")]
    public class MvvmHelpersTests
    {
        private sealed class FakeDispatcher : IUiDispatcher
        {
            private readonly Queue<Action> posted = new ();

            public bool OnUiThread { get; set; } = true;

            public int PostedCount => posted.Count;

            public bool CheckAccess () => OnUiThread;

            public void Post (Action action) => posted.Enqueue (action);

            // What the UI thread does: run whatever was posted, in order.
            public void Pump ()
            {
                while (posted.Count > 0)
                    posted.Dequeue () ();
            }
        }

        private sealed class Model : INotifyPropertyChanged
        {
            private string name = "";

            public event PropertyChangedEventHandler? PropertyChanged;

            public int Subscribers => PropertyChanged?.GetInvocationList ().Length ?? 0;

            public string Name {
                get => name;
                set {
                    name = value;
                    Raise (nameof (Name));
                }
            }

            public string Other { get; set; } = "";

            public void Raise (string? property) => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (property));
        }

        private sealed class TestCommand (Func<object?, bool>? canExecute = null) : ICommand
        {
            public List<object?> Executed { get; } = [];

            public bool Allowed { get; set; } = true;

            public event EventHandler? CanExecuteChanged;

            public int Subscribers => CanExecuteChanged?.GetInvocationList ().Length ?? 0;

            public bool CanExecute (object? parameter) => canExecute is null ? Allowed : canExecute (parameter);

            public void Execute (object? parameter) => Executed.Add (parameter);

            public void Raise () => CanExecuteChanged?.Invoke (this, EventArgs.Empty);
        }

        // A control with no Command property, which is the case BindCommand exists for.
        private sealed class Clickable : Control
        {
            public void Raise () => OnClick (EventArgs.Empty);
        }

        // ---- Observe ------------------------------------------------------------------------------

        [Fact]
        public void Observe_pushes_the_current_value_at_once ()
        {
            var model = new Model { Name = "Pip" };
            var seen = new List<string> ();

            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, new FakeDispatcher ());

            Assert.Equal (new[] { "Pip" }, seen);
        }

        [Fact]
        public void Observe_pushes_again_each_time_that_property_changes ()
        {
            var model = new Model ();
            var seen = new List<string> ();
            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, new FakeDispatcher ());

            model.Name = "a";
            model.Name = "b";

            Assert.Equal (new[] { "", "a", "b" }, seen);
        }

        [Fact]
        public void Observe_ignores_a_change_to_another_property ()
        {
            var model = new Model ();
            var seen = new List<string> ();
            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, new FakeDispatcher ());

            model.Raise (nameof (Model.Other));

            Assert.Single (seen);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        public void Observe_refreshes_when_the_change_names_no_property (string? property)
        {
            // PropertyChanged with no name means every property changed.
            var model = new Model ();
            var seen = new List<string> ();
            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, new FakeDispatcher ());

            model.Raise (property);

            Assert.Equal (2, seen.Count);
        }

        [Fact]
        public void Observe_stops_and_unsubscribes_after_it_is_disposed ()
        {
            var model = new Model ();
            var seen = new List<string> ();
            var subscription = model.Observe (nameof (Model.Name), m => m.Name, seen.Add, new FakeDispatcher ());
            Assert.Equal (1, model.Subscribers);

            subscription.Dispose ();
            model.Name = "later";

            Assert.Single (seen);
            Assert.Equal (0, model.Subscribers);
        }

        [Fact]
        public void Observe_drops_a_change_that_was_queued_before_it_was_disposed ()
        {
            var dispatcher = new FakeDispatcher ();
            var model = new Model ();
            var seen = new List<string> ();
            var subscription = model.Observe (nameof (Model.Name), m => m.Name, seen.Add, dispatcher);
            dispatcher.OnUiThread = false;
            model.Name = "queued";
            Assert.Equal (1, dispatcher.PostedCount);

            subscription.Dispose ();
            dispatcher.Pump ();

            Assert.Single (seen);
        }

        [Fact]
        public void Observe_applies_at_once_when_already_on_the_ui_thread ()
        {
            var dispatcher = new FakeDispatcher ();
            var model = new Model ();
            var seen = new List<string> ();
            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, dispatcher);

            model.Name = "now";

            Assert.Equal (0, dispatcher.PostedCount);
            Assert.Equal ("now", seen[^1]);
        }

        [Fact]
        public void Observe_posts_a_change_raised_off_the_ui_thread_and_applies_it_when_the_ui_thread_runs_it ()
        {
            var dispatcher = new FakeDispatcher ();
            var model = new Model ();
            var seen = new List<string> ();
            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, dispatcher);
            dispatcher.OnUiThread = false;

            model.Name = "from a worker";

            Assert.Single (seen);
            dispatcher.Pump ();
            Assert.Equal ("from a worker", seen[^1]);
        }

        [Fact]
        public void Observe_posts_the_first_push_too_when_it_is_started_off_the_ui_thread ()
        {
            var dispatcher = new FakeDispatcher { OnUiThread = false };
            var model = new Model { Name = "first" };
            var seen = new List<string> ();

            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, dispatcher);

            Assert.Empty (seen);
            dispatcher.Pump ();
            Assert.Equal (new[] { "first" }, seen);
        }

        [Fact]
        public void Observe_shows_the_latest_value_when_several_changes_queue_up ()
        {
            var dispatcher = new FakeDispatcher ();
            var model = new Model ();
            var seen = new List<string> ();
            model.Observe (nameof (Model.Name), m => m.Name, seen.Add, dispatcher);
            dispatcher.OnUiThread = false;

            model.Name = "a";
            model.Name = "b";
            dispatcher.Pump ();

            // Each queued push reads the value when it runs, so the control never shows "a" after "b" was set.
            Assert.DoesNotContain ("a", seen);
            Assert.Equal ("b", seen[^1]);
        }

        [Fact]
        public void Observe_refuses_missing_arguments ()
        {
            var model = new Model ();

            Assert.Throws<ArgumentNullException> (() => ((Model) null!).Observe ("Name", m => m.Name, _ => { }));
            Assert.Throws<ArgumentNullException> (() => model.Observe (null!, m => m.Name, _ => { }));
            Assert.Throws<ArgumentNullException> (() => model.Observe<Model, string> ("Name", null!, _ => { }));
            Assert.Throws<ArgumentNullException> (() => model.Observe ("Name", m => m.Name, null!));
        }

        [Fact]
        public void The_default_dispatcher_runs_on_the_active_backends_ui_thread ()
        {
            // HeadlessRenderer.Use () is a no-op once any Headless backend is already active (it only replaces a
            // *different* backend type), and HeadlessPlatformBackend.Initialize () pins its UI-thread id once per
            // instance and never again -- so by the time this test runs, the shared Headless backend's UI thread is
            // whichever thread happened to construct the very first window anywhere in the whole assembly's test run,
            // not necessarily this thread. A fresh instance, initialised here, is what actually proves the claim this
            // test makes (found as a real, twice-reproduced Windows/macOS-only CI failure, not guessed: the shared
            // instance's pinned thread and xUnit's own worker-thread scheduling for this specific test happened to
            // differ often enough on those two runners but not on Linux's).
            var previous = Majorsilence.Forms.Backends.Platform.ConfiguredBackend;
            var backend = new HeadlessPlatformBackend ();
            Majorsilence.Forms.Backends.Platform.Backend = backend;
            try {
                backend.Initialize ();

                Assert.Same (UiDispatcher.Default, UiDispatcher.Default);
                Assert.True (UiDispatcher.Default.CheckAccess ());
            } finally {
                if (previous is not null)
                    Majorsilence.Forms.Backends.Platform.Backend = previous;
            }
        }

        // ---- BindCommand --------------------------------------------------------------------------

        [Fact]
        public void BindCommand_enables_the_control_from_CanExecute_and_follows_it ()
        {
            var control = new Clickable ();
            var command = new TestCommand { Allowed = false };

            control.BindCommand (command, dispatcher: new FakeDispatcher ());
            Assert.False (control.Enabled);

            command.Allowed = true;
            command.Raise ();
            Assert.True (control.Enabled);

            command.Allowed = false;
            command.Raise ();
            Assert.False (control.Enabled);
        }

        [Fact]
        public void BindCommand_runs_the_command_with_the_parameter_on_click ()
        {
            var control = new Clickable ();
            var command = new TestCommand ();
            control.BindCommand (command, "grown-up", new FakeDispatcher ());

            control.Raise ();

            Assert.Equal (new object?[] { "grown-up" }, command.Executed);
        }

        [Fact]
        public void BindCommand_does_not_run_a_command_that_cannot_execute ()
        {
            var control = new Clickable ();
            var command = new TestCommand { Allowed = false };
            control.BindCommand (command, dispatcher: new FakeDispatcher ());

            control.Raise ();

            Assert.Empty (command.Executed);
        }

        [Fact]
        public void BindCommand_passes_the_parameter_to_CanExecute_as_well ()
        {
            var control = new Clickable ();
            var command = new TestCommand (p => p is int n && n > 0);

            control.BindCommand (command, 0, new FakeDispatcher ());
            Assert.False (control.Enabled);

            control.BindCommand (new TestCommand (p => p is int n && n > 0), 5, new FakeDispatcher ());
            Assert.True (control.Enabled);
        }

        [Fact]
        public void BindCommand_stops_following_and_stops_running_the_command_when_disposed ()
        {
            var control = new Clickable ();
            var command = new TestCommand ();
            var binding = control.BindCommand (command, dispatcher: new FakeDispatcher ());
            Assert.Equal (1, command.Subscribers);

            binding.Dispose ();
            command.Allowed = false;
            command.Raise ();
            control.Raise ();

            Assert.Equal (0, command.Subscribers);
            Assert.Empty (command.Executed);
            Assert.True (control.Enabled);
        }

        [Fact]
        public void BindCommand_removes_the_click_handler_it_added_when_disposed ()
        {
            // A handler left on a control keeps the binding alive, which is the leak a BindingScope exists to prevent. The flag that stops
            // a leftover handler running would hide it, so count the subscriptions directly.
            var handlers = new List<EventHandler> ();
            var binding = BindCommandExtensions.Bind (new TestCommand (), null, new FakeDispatcher (), _ => { }, handlers.Add, handler => handlers.Remove (handler));
            Assert.Single (handlers);

            binding.Dispose ();

            Assert.Empty (handlers);
        }

        [Fact]
        public void BindCommand_touches_the_control_only_on_the_ui_thread ()
        {
            var dispatcher = new FakeDispatcher ();
            var control = new Clickable ();
            var command = new TestCommand ();
            control.BindCommand (command, dispatcher: dispatcher);
            dispatcher.OnUiThread = false;

            command.Allowed = false;
            command.Raise ();

            Assert.True (control.Enabled);
            Assert.Equal (1, dispatcher.PostedCount);
            dispatcher.Pump ();
            Assert.False (control.Enabled);
        }

        [Fact]
        public void BindCommand_drops_a_refresh_that_was_queued_before_it_was_disposed ()
        {
            var dispatcher = new FakeDispatcher ();
            var control = new Clickable ();
            var command = new TestCommand ();
            var binding = control.BindCommand (command, dispatcher: dispatcher);
            dispatcher.OnUiThread = false;
            command.Allowed = false;
            command.Raise ();

            binding.Dispose ();
            dispatcher.Pump ();

            Assert.True (control.Enabled);
        }

        [Fact]
        public void BindCommand_works_on_a_button ()
        {
            using var button = new Button ();
            var command = new TestCommand ();
            button.BindCommand (command, 7, new FakeDispatcher ());

            button.PerformClick ();

            Assert.Equal (new object?[] { 7 }, command.Executed);
        }

        [Fact]
        public void BindCommand_works_on_a_tool_strip_item ()
        {
            var item = new ToolStripButton ();
            var command = new TestCommand { Allowed = false };
            var binding = item.BindCommand (command, "x", new FakeDispatcher ());
            Assert.False (item.Enabled);
            item.PerformClick ();
            Assert.Empty (command.Executed);

            command.Allowed = true;
            command.Raise ();
            item.PerformClick ();
            Assert.Equal (new object?[] { "x" }, command.Executed);

            binding.Dispose ();
            item.PerformClick ();
            Assert.Single (command.Executed);
            Assert.Equal (0, command.Subscribers);
        }

        [Fact]
        public void BindCommand_refuses_missing_arguments ()
        {
            Assert.Throws<ArgumentNullException> (() => ((Control) null!).BindCommand (new TestCommand ()));
            Assert.Throws<ArgumentNullException> (() => ((MenuItem) null!).BindCommand (new TestCommand ()));
            Assert.Throws<ArgumentNullException> (() => new Clickable ().BindCommand (null!));
        }

        // ---- BindingScope -------------------------------------------------------------------------

        private sealed class Probe (List<string> log, string name, bool fails = false) : IDisposable
        {
            public void Dispose ()
            {
                log.Add (name);
                if (fails)
                    throw new InvalidOperationException (name);
            }
        }

        [Fact]
        public void A_scope_disposes_everything_it_holds_latest_first ()
        {
            var log = new List<string> ();
            var scope = new BindingScope ();
            scope.Add (new Probe (log, "first"));
            scope.Add (new Probe (log, "second"));
            scope.Add (new Probe (log, "third"));
            Assert.Equal (3, scope.Count);

            scope.Dispose ();

            Assert.Equal (new[] { "third", "second", "first" }, log);
            Assert.Equal (0, scope.Count);
        }

        [Fact]
        public void A_scope_disposes_only_once ()
        {
            var log = new List<string> ();
            var scope = new BindingScope ();
            scope.Add (new Probe (log, "only"));

            scope.Dispose ();
            scope.Dispose ();

            Assert.Equal (new[] { "only" }, log);
        }

        [Fact]
        public void A_binding_added_after_the_scope_was_disposed_is_disposed_at_once ()
        {
            var log = new List<string> ();
            var scope = new BindingScope ();
            scope.Dispose ();

            scope.Add (new Probe (log, "late"));

            Assert.Equal (new[] { "late" }, log);
            Assert.Equal (0, scope.Count);
        }

        [Fact]
        public void One_binding_that_throws_does_not_stop_the_others_being_disposed ()
        {
            var log = new List<string> ();
            var scope = new BindingScope ();
            scope.Add (new Probe (log, "a"));
            scope.Add (new Probe (log, "b", fails: true));
            scope.Add (new Probe (log, "c"));

            var error = Assert.Throws<AggregateException> (scope.Dispose);

            Assert.Equal (new[] { "c", "b", "a" }, log);
            Assert.Equal ("b", Assert.Single (error.InnerExceptions).Message);
        }

        [Fact]
        public void AddTo_puts_a_binding_in_the_scope_and_returns_it ()
        {
            var log = new List<string> ();
            var scope = new BindingScope ();
            var probe = new Probe (log, "chained");

            var returned = probe.AddTo (scope);

            Assert.Same (probe, returned);
            Assert.Equal (1, scope.Count);
        }

        [Fact]
        public void A_scope_releases_the_subscriptions_of_a_page_together ()
        {
            var model = new Model ();
            var command = new TestCommand ();
            var control = new Clickable ();
            var scope = new BindingScope ();
            var dispatcher = new FakeDispatcher ();
            model.Observe (nameof (Model.Name), m => m.Name, _ => { }, dispatcher).AddTo (scope);
            control.BindCommand (command, dispatcher: dispatcher).AddTo (scope);
            Assert.Equal (1, model.Subscribers);
            Assert.Equal (1, command.Subscribers);

            scope.Dispose ();

            Assert.Equal (0, model.Subscribers);
            Assert.Equal (0, command.Subscribers);
        }

        [Fact]
        public void A_scope_refuses_a_missing_binding ()
        {
            var scope = new BindingScope ();

            Assert.Throws<ArgumentNullException> (() => scope.Add<IDisposable> (null!));
            Assert.Throws<ArgumentNullException> (() => new Probe (new List<string> (), "x").AddTo (null!));
        }
    }
}
