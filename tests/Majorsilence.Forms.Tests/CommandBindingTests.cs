using System;
using System.Collections.Generic;
using System.Windows.Input;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A button or a tool strip item bound to a System.Windows.Input.ICommand, which is what WinForms on modern .NET takes and what an
    // MVVM toolkit's RelayCommand is. The command runs on click with CommandParameter, only when it can execute, and Enabled follows
    // CanExecute, with the subscription released when the command changes. The doubles below stand in for a toolkit command.
    public class CommandBindingTests
    {
        private sealed class TestCommand (Func<object?, bool>? canExecute = null) : ICommand
        {
            public List<object?> Executed { get; } = [];

            public List<object?> Asked { get; } = [];

            public bool Allowed { get; set; } = true;

            public Action<object?>? OnExecute { get; set; }

            public event EventHandler? CanExecuteChanged;

            public int Subscribers => CanExecuteChanged?.GetInvocationList ().Length ?? 0;

            public bool CanExecute (object? parameter)
            {
                Asked.Add (parameter);
                return canExecute is null ? Allowed : canExecute (parameter);
            }

            public void Execute (object? parameter)
            {
                Executed.Add (parameter);
                OnExecute?.Invoke (parameter);
            }

            public void Raise () => CanExecuteChanged?.Invoke (this, EventArgs.Empty);
        }

        // ---- ToolStripItem: takes ICommand today but never ran it ---------------------------------

        [Fact]
        public void Choosing_a_tool_strip_item_runs_its_command_with_the_parameter ()
        {
            var item = new ToolStripButton { CommandParameter = "grown-up" };
            var command = new TestCommand ();
            item.Command = command;

            item.PerformClick ();

            Assert.Equal (new object?[] { "grown-up" }, command.Executed);
        }

        [Fact]
        public void A_tool_strip_item_does_not_run_a_command_that_cannot_execute ()
        {
            var item = new ToolStripButton ();
            var command = new TestCommand { Allowed = false };
            item.Command = command;

            item.PerformClick ();

            Assert.Empty (command.Executed);
        }

        [Fact]
        public void A_tool_strip_item_is_enabled_only_while_its_command_can_execute ()
        {
            var item = new ToolStripButton ();
            var command = new TestCommand { Allowed = false };

            item.Command = command;
            Assert.False (item.Enabled);

            command.Allowed = true;
            command.Raise ();
            Assert.True (item.Enabled);

            command.Allowed = false;
            command.Raise ();
            Assert.False (item.Enabled);
        }

        [Fact]
        public void Clearing_a_tool_strip_items_command_restores_its_earlier_enabled_state_and_stops_listening ()
        {
            var item = new ToolStripButton ();
            var command = new TestCommand { Allowed = false };
            item.Command = command;
            Assert.False (item.Enabled);

            item.Command = null;

            Assert.True (item.Enabled);
            Assert.Equal (0, command.Subscribers);
        }

        [Fact]
        public void A_tool_strip_item_re_asks_when_its_parameter_changes ()
        {
            var item = new ToolStripButton ();
            var command = new TestCommand (p => p is int n && n > 0);
            item.Command = command;
            Assert.False (item.Enabled);

            item.CommandParameter = 3;

            Assert.True (item.Enabled);
            Assert.Equal (3, command.Asked[^1]);
        }

        [Fact]
        public void A_tool_strip_items_click_handlers_run_before_its_command ()
        {
            var item = new ToolStripButton ();
            var order = new List<string> ();
            item.Click += (_, _) => order.Add ("handler");
            item.Command = new TestCommand { OnExecute = _ => order.Add ("command") };

            item.PerformClick ();

            Assert.Equal (new[] { "handler", "command" }, order);
        }

        [Fact]
        public void A_tool_strip_item_on_a_disabled_strip_keeps_its_own_state_when_its_command_is_cleared ()
        {
            // Enabled folds in the strip's state. Remembering that, instead of the item's own flag, would leave the item disabled after
            // the strip is enabled again.
            using var strip = new ToolStrip ();
            var item = new ToolStripButton ();
            strip.Items.Add (item);
            strip.Enabled = false;
            item.Command = new TestCommand ();

            item.Command = null;
            strip.Enabled = true;

            Assert.True (item.Enabled);
        }

        // ---- ButtonBase: was typed to the framework's own ICommandExecutor ------------------------

        [Fact]
        public void A_button_takes_an_ICommand_and_runs_it_on_click_with_the_parameter ()
        {
            using var button = new Button { CommandParameter = 42 };
            var command = new TestCommand ();
            button.Command = command;

            button.PerformClick ();

            Assert.Equal (new object?[] { 42 }, command.Executed);
        }

        [Fact]
        public void A_button_does_not_run_a_command_that_cannot_execute ()
        {
            using var button = new Button ();
            var command = new TestCommand { Allowed = false };
            button.Command = command;

            button.PerformClick ();

            Assert.Empty (command.Executed);
        }

        [Fact]
        public void A_button_is_enabled_only_while_its_command_can_execute ()
        {
            using var button = new Button ();
            var command = new TestCommand { Allowed = false };

            button.Command = command;
            Assert.False (button.Enabled);

            command.Allowed = true;
            command.Raise ();
            Assert.True (button.Enabled);

            command.Allowed = false;
            command.Raise ();
            Assert.False (button.Enabled);
        }

        [Fact]
        public void A_button_re_asks_when_its_parameter_changes ()
        {
            using var button = new Button ();
            var command = new TestCommand (p => p is int n && n > 0);
            button.Command = command;
            Assert.False (button.Enabled);

            button.CommandParameter = 3;

            Assert.True (button.Enabled);
            Assert.Equal (3, command.Asked[^1]);
        }

        [Fact]
        public void Clearing_a_buttons_command_restores_its_earlier_enabled_state_and_stops_listening ()
        {
            using var button = new Button ();
            var command = new TestCommand { Allowed = false };
            button.Command = command;
            Assert.False (button.Enabled);

            button.Command = null;

            Assert.True (button.Enabled);
            Assert.Equal (0, command.Subscribers);
        }

        [Fact]
        public void A_button_that_was_disabled_before_its_command_stays_disabled_when_the_command_is_cleared ()
        {
            using var button = new Button { Enabled = false };
            button.Command = new TestCommand ();

            button.Command = null;

            Assert.False (button.Enabled);
        }

        [Fact]
        public void A_button_in_a_disabled_parent_keeps_its_own_state_when_its_command_is_cleared ()
        {
            // Enabled folds in the parent's state. Remembering that, instead of the button's own flag, would leave the button disabled
            // after the parent is enabled again.
            using var panel = new Panel ();
            using var button = new Button ();
            panel.Controls.Add (button);
            panel.Enabled = false;
            button.Command = new TestCommand ();

            button.Command = null;
            panel.Enabled = true;

            Assert.True (button.Enabled);
        }

        [Fact]
        public void Replacing_a_buttons_command_releases_the_old_one_and_follows_the_new_one ()
        {
            using var button = new Button ();
            var first = new TestCommand ();
            var second = new TestCommand { Allowed = false };
            button.Command = first;

            button.Command = second;

            Assert.Equal (0, first.Subscribers);
            Assert.Equal (1, second.Subscribers);
            Assert.False (button.Enabled);
        }

        [Fact]
        public void A_buttons_own_CommandCanExecuteChanged_is_raised_when_its_command_raises_it ()
        {
            using var button = new Button ();
            var command = new TestCommand ();
            button.Command = command;
            var fired = 0;
            button.CommandCanExecuteChanged += (s, _) => { fired++; Assert.Same (button, s); };

            command.Raise ();

            Assert.Equal (1, fired);
        }

        [Fact]
        public void The_click_handlers_run_before_the_command ()
        {
            using var button = new Button ();
            var order = new List<string> ();
            var command = new TestCommand { OnExecute = _ => order.Add ("command") };
            button.Click += (_, _) => order.Add ("handler");
            button.Command = command;

            button.PerformClick ();

            Assert.Equal (new[] { "handler", "command" }, order);
        }

        // ---- the framework's own ICommandExecutor, adapted ----------------------------------------

        private sealed class Executor : ICommandExecutor
        {
            public int Runs { get; private set; }

            public event EventHandler? CommandCanExecuteChanged;

            public void Execute () => Runs++;

            public void Raise () => CommandCanExecuteChanged?.Invoke (this, EventArgs.Empty);
        }

        [Fact]
        public void An_ICommandExecutor_is_adapted_to_an_ICommand ()
        {
            var executor = new Executor ();
            var command = executor.AsCommand ();
            var raised = 0;
            command.CanExecuteChanged += (_, _) => raised++;

            Assert.True (command.CanExecute (null));
            command.Execute ("ignored");
            executor.Raise ();

            Assert.Equal (1, executor.Runs);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void Adapting_no_executor_is_refused ()
        {
            Assert.Throws<ArgumentNullException> (() => ((ICommandExecutor) null!).AsCommand ());
        }

        [Fact]
        public void An_adapted_ICommandExecutor_runs_when_a_button_is_clicked ()
        {
            using var button = new Button ();
            var executor = new Executor ();
            button.Command = executor.AsCommand ();

            button.PerformClick ();

            Assert.Equal (1, executor.Runs);
        }
    }
}
