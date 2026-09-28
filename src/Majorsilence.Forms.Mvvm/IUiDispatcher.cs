using System;

namespace Majorsilence.Forms.Mvvm
{
    /// <summary>
    /// Runs work on the UI thread, so a view model can raise <c>PropertyChanged</c> or <c>CanExecuteChanged</c> from any thread while a
    /// view still touches its controls only on the UI thread.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>Returns whether the calling thread is the UI thread, so work can run at once instead of being posted.</summary>
        bool CheckAccess ();

        /// <summary>Queues <paramref name="action"/> to run on the UI thread.</summary>
        void Post (Action action);
    }

    /// <summary>The dispatcher over the active platform backend, which is what <see cref="Application.RunOnUIThread"/> uses.</summary>
    public sealed class UiDispatcher : IUiDispatcher
    {
        /// <summary>Gets the dispatcher the helpers use when they are not given one.</summary>
        public static IUiDispatcher Default { get; } = new UiDispatcher ();

        private UiDispatcher ()
        {
        }

        /// <inheritdoc/>
        public bool CheckAccess () => Backends.Platform.Backend.CheckAccess ();

        /// <inheritdoc/>
        public void Post (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            Application.RunOnUIThread (action);
        }
    }
}
