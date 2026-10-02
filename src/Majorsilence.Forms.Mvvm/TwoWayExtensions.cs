using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Majorsilence.Forms.Mvvm
{
    /// <summary>
    /// Keeps an input control and a view model property equal in both directions, with no reflection and no expression trees: the
    /// property is named with <c>nameof (...)</c> and read and written through lambdas, so trimming and NativeAOT can see all of it.
    /// </summary>
    public static class TwoWayExtensions
    {
        /// <summary>Keeps <paramref name="box"/>'s <see cref="Control.Text"/> and a string property of <paramref name="source"/> equal.</summary>
        /// <param name="box">The text box.</param>
        /// <param name="source">The view model.</param>
        /// <param name="propertyName">The property, as <c>PropertyChanged</c> names it; write <c>nameof (...)</c>.</param>
        /// <param name="read">Reads the property.</param>
        /// <param name="write">Writes the property.</param>
        /// <param name="dispatcher">Marshals view model changes to the UI thread. Defaults to <see cref="UiDispatcher.Default"/>.</param>
        /// <returns>Disposing it stops both directions.</returns>
        /// <remarks>
        /// The control is written to only when its value differs, so typing never moves the caret. A change the view model makes while it
        /// is handling the control's own change (normalising what it was given, for example) is not written back into the control, so the
        /// two updates cannot trigger each other and the text under the person's caret is left alone.
        /// </remarks>
        public static IDisposable BindText<TSource> (
            this TextBox box, TSource source, string propertyName, Func<TSource, string> read, Action<TSource, string> write, IUiDispatcher? dispatcher = null)
            where TSource : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (box);
            return Bind (source, propertyName, read, write, () => box.Text, text => box.Text = text,
                handler => box.TextChanged += handler, handler => box.TextChanged -= handler, dispatcher);
        }

        /// <summary>Keeps <paramref name="box"/>'s <see cref="CheckBox.Checked"/> and a bool property of <paramref name="source"/> equal. See <see cref="BindText{TSource}"/>.</summary>
        public static IDisposable BindChecked<TSource> (
            this CheckBox box, TSource source, string propertyName, Func<TSource, bool> read, Action<TSource, bool> write, IUiDispatcher? dispatcher = null)
            where TSource : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (box);
            return Bind (source, propertyName, read, write, () => box.Checked, value => box.Checked = value,
                handler => box.CheckedChanged += handler, handler => box.CheckedChanged -= handler, dispatcher);
        }

        /// <summary>Keeps <paramref name="box"/>'s <see cref="ComboBox.SelectedIndex"/> and an int property of <paramref name="source"/> equal. See <see cref="BindText{TSource}"/>.</summary>
        public static IDisposable BindSelectedIndex<TSource> (
            this ComboBox box, TSource source, string propertyName, Func<TSource, int> read, Action<TSource, int> write, IUiDispatcher? dispatcher = null)
            where TSource : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (box);
            return Bind (source, propertyName, read, write, () => box.SelectedIndex, index => box.SelectedIndex = index,
                handler => box.SelectedIndexChanged += handler, handler => box.SelectedIndexChanged -= handler, dispatcher);
        }

        /// <summary>
        /// Keeps <paramref name="box"/>'s <see cref="NumericUpDown.Value"/> and a decimal property of <paramref name="source"/> equal. See
        /// <see cref="BindText{TSource}"/>. A view-model value outside the box's minimum and maximum is
        /// clamped into range on the way in.
        /// </summary>
        public static IDisposable BindValue<TSource> (
            this NumericUpDown box, TSource source, string propertyName, Func<TSource, decimal> read, Action<TSource, decimal> write, IUiDispatcher? dispatcher = null)
            where TSource : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (box);
            // Clamped here because NumericUpDown.Value throws out of range, as upstream's does (SMP-34);
            // a view model is not obliged to know the box's range.
            return Bind (source, propertyName, read, write, () => box.Value, value => box.Value = Math.Min (Math.Max (value, box.Minimum), box.Maximum),
                handler => box.ValueChanged += handler, handler => box.ValueChanged -= handler, dispatcher);
        }

        private static Subscription Bind<TSource, TValue> (
            TSource source, string propertyName, Func<TSource, TValue> read, Action<TSource, TValue> write,
            Func<TValue> readControl, Action<TValue> writeControl, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe,
            IUiDispatcher? dispatcher)
            where TSource : INotifyPropertyChanged
        {
            ArgumentNullException.ThrowIfNull (source);
            ArgumentNullException.ThrowIfNull (propertyName);
            ArgumentNullException.ThrowIfNull (read);
            ArgumentNullException.ThrowIfNull (write);

            // Both directions raise an event on the other side. While one is being applied the other is ignored: this is the whole guard.
            var applying = false;

            void ToControl (TValue value)
            {
                if (applying || EqualityComparer<TValue>.Default.Equals (readControl (), value))
                    return;

                applying = true;
                try {
                    writeControl (value);
                } finally {
                    applying = false;
                }
            }

            void ToSource (object? sender, EventArgs e)
            {
                if (applying)
                    return;

                applying = true;
                try {
                    write (source, readControl ());
                } finally {
                    applying = false;
                }
            }

            var observation = source.Observe (propertyName, read, ToControl, dispatcher);
            subscribe (ToSource);

            return new Subscription (() => {
                unsubscribe (ToSource);
                observation.Dispose ();
            });
        }

        private sealed class Subscription (Action undo) : IDisposable
        {
            private bool disposed;

            public void Dispose ()
            {
                if (disposed)
                    return;

                disposed = true;
                undo ();
            }
        }
    }
}
