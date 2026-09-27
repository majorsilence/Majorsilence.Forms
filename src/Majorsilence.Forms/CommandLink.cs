using System;
using System.Windows.Input;

namespace Majorsilence.Forms
{
    // What a button and a tool strip item share for a System.Windows.Input.ICommand, so the two cannot drift: run it on click with the
    // CommandParameter, only when it can execute; let Enabled follow CanExecute; and release the subscription when the command changes.
    // WinForms on modern .NET does the same, and an MVVM toolkit's RelayCommand is an ICommand, so it binds without a helper.
    internal sealed class CommandLink (Func<object?> parameter, Func<bool> enabled, Action<bool> setEnabled, Action canExecuteChanged)
    {
        private ICommand? command;

        // What Enabled was before a command took it over, so clearing the command gives it back instead of leaving the last CanExecute.
        private bool? enabledBeforeCommand;

        public ICommand? Command => command;

        // Returns false when the same command was already set, so the caller knows whether to raise CommandChanged.
        public bool SetCommand (ICommand? value)
        {
            if (ReferenceEquals (command, value))
                return false;

            if (command is not null)
                command.CanExecuteChanged -= OnCanExecuteChanged;

            if (command is null && value is not null)
                enabledBeforeCommand = enabled ();

            command = value;

            if (command is null) {
                if (enabledBeforeCommand is bool before)
                    setEnabled (before);

                enabledBeforeCommand = null;
            } else {
                command.CanExecuteChanged += OnCanExecuteChanged;
                Refresh ();
            }

            return true;
        }

        // The parameter is an input to CanExecute, so a new one can enable or disable the control.
        public void ParameterChanged () => Refresh ();

        public void Execute ()
        {
            if (command is null)
                return;

            var argument = parameter ();

            if (command.CanExecute (argument))
                command.Execute (argument);
        }

        private void OnCanExecuteChanged (object? sender, EventArgs e)
        {
            canExecuteChanged ();
            Refresh ();
        }

        private void Refresh ()
        {
            if (command is not null)
                setEnabled (command.CanExecute (parameter ()));
        }
    }

    /// <summary>Adapts the framework's <see cref="ICommandExecutor"/> to the <see cref="ICommand"/> a button or tool strip item takes.</summary>
    public static class CommandExecutorExtensions
    {
        /// <summary>
        /// Wraps an <see cref="ICommandExecutor"/> as an <see cref="ICommand"/>. The executor has no parameter and no way to say it
        /// cannot run, so the command always can, ignores the parameter, and relays the executor's
        /// <see cref="ICommandExecutor.CommandCanExecuteChanged"/> as <see cref="ICommand.CanExecuteChanged"/>.
        /// </summary>
        public static ICommand AsCommand (this ICommandExecutor executor)
        {
            Guard.ThrowIfNull (executor);

            return new ExecutorCommand (executor);
        }

        private sealed class ExecutorCommand (ICommandExecutor executor) : ICommand
        {
            public event EventHandler? CanExecuteChanged {
                add => executor.CommandCanExecuteChanged += value;
                remove => executor.CommandCanExecuteChanged -= value;
            }

            public bool CanExecute (object? parameter) => true;

            public void Execute (object? parameter) => executor.Execute ();
        }
    }
}
