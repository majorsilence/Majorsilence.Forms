using System;
using System.ComponentModel;

namespace Majorsilence.Forms.Mvvm
{
    /// <summary>Pushes a view model's state into a control.</summary>
    public static class ObserveExtensions
    {
        /// <summary>
        /// Pushes the current value of a property into <paramref name="apply"/> now, and again each time
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/> reports that property, on the UI thread.
        /// </summary>
        /// <param name="source">The view model.</param>
        /// <param name="propertyName">The property to follow, as <c>PropertyChanged</c> names it; write <c>nameof (...)</c>. An event that names no property means every property changed and refreshes too.</param>
        /// <param name="read">Reads the value from the view model. A lambda, so nothing is looked up by name at run time and trimming and NativeAOT can see it.</param>
        /// <param name="apply">Puts the value into the control.</param>
        /// <param name="dispatcher">Marshals to the UI thread. Defaults to <see cref="UiDispatcher.Default"/>.</param>
        /// <returns>Disposing it stops the following, and a change already queued but not yet applied is dropped.</returns>
        /// <remarks>
        /// When several changes queue before the UI thread runs them, each applies the latest value, not the value at the time of its
        /// change, so a control never shows a stale one after a newer one.
        /// </remarks>
        public static IDisposable Observe<TSource, TValue> (
            this TSource source, string propertyName, Func<TSource, TValue> read, Action<TValue> apply, IUiDispatcher? dispatcher = null)
            where TSource : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (source);
            ArgumentNullException.ThrowIfNull (propertyName);
            ArgumentNullException.ThrowIfNull (read);
            ArgumentNullException.ThrowIfNull (apply);

            var observation = new Observation<TSource, TValue> (source, propertyName, read, apply, dispatcher ?? UiDispatcher.Default);
            observation.Start ();
            return observation;
        }

        private sealed class Observation<TSource, TValue> (
            TSource source, string propertyName, Func<TSource, TValue> read, Action<TValue> apply, IUiDispatcher dispatcher) : IDisposable
            where TSource : INotifyPropertyChanged
        {
            private volatile bool disposed;

            public void Start ()
            {
                // Subscribe first, so a change between the two steps is not missed.
                source.PropertyChanged += OnPropertyChanged;
                Deliver ();
            }

            public void Dispose ()
            {
                disposed = true;
                source.PropertyChanged -= OnPropertyChanged;
            }

            private void OnPropertyChanged (object? sender, PropertyChangedEventArgs e)
            {
                if (string.IsNullOrEmpty (e.PropertyName) || string.Equals (e.PropertyName, propertyName, StringComparison.Ordinal))
                    Deliver ();
            }

            private void Deliver ()
            {
                if (dispatcher.CheckAccess ())
                    Apply ();
                else
                    dispatcher.Post (Apply);
            }

            // Reads at the moment it runs, and not at the moment of the change, and does nothing once disposed.
            private void Apply ()
            {
                if (disposed)
                    return;

                apply (read (source));
            }
        }
    }
}
