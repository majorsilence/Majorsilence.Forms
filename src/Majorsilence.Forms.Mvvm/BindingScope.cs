using System;
using System.Collections.Generic;

namespace Majorsilence.Forms.Mvvm
{
    /// <summary>
    /// Collects the subscriptions a page makes, so leaving the page disposes them all and no view leaks a handler on a view model that
    /// outlives it.
    /// </summary>
    public sealed class BindingScope : IDisposable
    {
        private readonly List<IDisposable> bindings = [];
        private bool disposed;

        /// <summary>Gets how many bindings the scope holds.</summary>
        public int Count {
            get {
                lock (bindings)
                    return bindings.Count;
            }
        }

        /// <summary>Adds a binding to be disposed with the scope, and returns it. A binding added after the scope was disposed is disposed at once.</summary>
        public T Add<T> (T binding) where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull (binding);

            var late = false;
            lock (bindings) {
                if (disposed)
                    late = true;
                else
                    bindings.Add (binding);
            }

            if (late)
                binding.Dispose ();

            return binding;
        }

        /// <summary>
        /// Disposes every binding, latest first, so a binding never outlives what it was made from. One that throws does not stop the
        /// rest: they are all disposed and the failures are rethrown together.
        /// </summary>
        public void Dispose ()
        {
            List<IDisposable> toDispose;
            lock (bindings) {
                if (disposed)
                    return;

                disposed = true;
                toDispose = [.. bindings];
                bindings.Clear ();
            }

            List<Exception>? failures = null;
            for (var i = toDispose.Count - 1; i >= 0; i--) {
                try {
                    toDispose[i].Dispose ();
                } catch (Exception e) {
                    (failures ??= []).Add (e);
                }
            }

            if (failures is not null)
                throw new AggregateException (failures);
        }
    }

    /// <summary>Reads a binding into a scope in one chain: <c>vm.Observe (...).AddTo (scope)</c>.</summary>
    public static class BindingScopeExtensions
    {
        /// <summary>Adds <paramref name="binding"/> to <paramref name="scope"/> and returns it.</summary>
        public static T AddTo<T> (this T binding, BindingScope scope) where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull (scope);
            return scope.Add (binding);
        }
    }
}
