using System;

namespace Continuum.Forms.Backends
{
    /// <summary>
    /// A UI-thread timer provided by an <see cref="IPlatformBackend"/>. Its <see cref="Tick"/> fires
    /// on the UI thread at the configured interval. Backs the Continuum.Forms <see cref="Continuum.Forms.Timer"/>.
    /// </summary>
    public interface IPlatformTimer : IDisposable
    {
        /// <summary>Gets or sets the tick interval, in milliseconds.</summary>
        double IntervalMilliseconds { get; set; }

        /// <summary>Raised on the UI thread each time the interval elapses while started.</summary>
        event Action Tick;

        /// <summary>Starts the timer.</summary>
        void Start ();

        /// <summary>Stops the timer.</summary>
        void Stop ();
    }
}
