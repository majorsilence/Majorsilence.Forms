using System;
using System.Windows.Input;

namespace Majorsilence.Forms.Mvvm
{
    /// <summary>Wires an <see cref="ICommand"/> to a control or a menu or tool strip item.</summary>
    /// <remarks>
    /// For a control that has no <c>Command</c> property, which is most of them, and for a custom-painted one. A <c>Button</c> or a
    /// <c>ToolStripItem</c> can take the command through its own <c>Command</c> property instead; do not use both on one control, or
    /// the command runs twice per click.
    /// </remarks>
    public static class BindCommandExtensions
    {
        /// <summary>
        /// Sets <see cref="Control.Enabled"/> from the command's <c>CanExecute</c> now and whenever <c>CanExecuteChanged</c> is raised
        /// (on the UI thread), and runs the command with <paramref name="parameter"/> when the control is clicked and it can execute.
        /// </summary>
        /// <param name="control">The control to wire.</param>
        /// <param name="command">The command, for example a toolkit <c>RelayCommand</c>. An async command that reports it cannot run
        /// while it is running disables the control for that time with no extra code.</param>
        /// <param name="parameter">Passed to <c>CanExecute</c> and <c>Execute</c>.</param>
        /// <param name="dispatcher">Marshals <c>CanExecuteChanged</c> to the UI thread. Defaults to <see cref="UiDispatcher.Default"/>.</param>
        /// <returns>Disposing it stops following the command and stops running it. It leaves <c>Enabled</c> as the last evaluation set it.</returns>
        public static IDisposable BindCommand (this Control control, ICommand command, object? parameter = null, IUiDispatcher? dispatcher = null)
        {
            ArgumentNullException.ThrowIfNull (control);
            return Bind (command, parameter, dispatcher, enabled => control.Enabled = enabled, handler => control.Click += handler, handler => control.Click -= handler);
        }

        /// <summary>The same for a menu item or a tool strip item (a <c>ToolStripItem</c> is a <c>MenuItem</c>).</summary>
        /// <inheritdoc cref="BindCommand(Control, ICommand, object, IUiDispatcher)"/>
        public static IDisposable BindCommand (this MenuItem item, ICommand command, object? parameter = null, IUiDispatcher? dispatcher = null)
        {
            ArgumentNullException.ThrowIfNull (item);
            return Bind (command, parameter, dispatcher, enabled => item.Enabled = enabled, handler => item.Click += handler, handler => item.Click -= handler);
        }

        // Internal so a test can see that every handler the binding adds it also removes, which a control's public Click event cannot show.
        internal static IDisposable Bind (
            ICommand command, object? parameter, IUiDispatcher? dispatcher, Action<bool> setEnabled, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe)
        {
            ArgumentNullException.ThrowIfNull (command);

            var binding = new CommandBinding (command, parameter, dispatcher ?? UiDispatcher.Default, setEnabled, subscribe, unsubscribe);
            binding.Start ();
            return binding;
        }

        private sealed class CommandBinding (
            ICommand command, object? parameter, IUiDispatcher dispatcher, Action<bool> setEnabled, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe)
            : IDisposable
        {
            private volatile bool disposed;

            public void Start ()
            {
                command.CanExecuteChanged += OnCanExecuteChanged;
                subscribe (OnClick);
                Refresh ();
            }

            public void Dispose ()
            {
                disposed = true;
                command.CanExecuteChanged -= OnCanExecuteChanged;
                unsubscribe (OnClick);
            }

            private void OnClick (object? sender, EventArgs e)
            {
                if (!disposed && command.CanExecute (parameter))
                    command.Execute (parameter);
            }

            // CanExecuteChanged is often raised from wherever an async command finishes, so the control is only touched on the UI thread.
            private void OnCanExecuteChanged (object? sender, EventArgs e)
            {
                if (dispatcher.CheckAccess ())
                    Refresh ();
                else
                    dispatcher.Post (Refresh);
            }

            private void Refresh ()
            {
                if (!disposed)
                    setEnabled (command.CanExecute (parameter));
            }
        }
    }
}
