using System.ComponentModel;
using Modern.Forms.Backends;

namespace Modern.Forms
{
    /// <summary>
    /// Implements a timer that raises an event at user-defined intervals.
    /// This timer is intended for UI-related scenarios and raises its <see cref="Tick"/>
    /// event on the UI thread.
    /// </summary>
    public class Timer : Component
    {
        private IPlatformTimer? platformTimer;
        private int interval = 100;
        private bool enabled;
        private EventHandler? onTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class.
        /// </summary>
        public Timer ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class and adds it to the specified container.
        /// </summary>
        public Timer (IContainer container)
        {
            ArgumentNullException.ThrowIfNull (container);

            container.Add (this);
        }

        /// <summary>
        /// Gets or sets user defined data associated with the timer.
        /// </summary>
        [DefaultValue (null)]
        public object? Tag { get; set; }

        /// <summary>
        /// Occurs when the specified timer interval has elapsed and the timer is enabled.
        /// </summary>
        public event EventHandler Tick {
            add => onTimer += value;
            remove => onTimer -= value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the timer is running.
        /// </summary>
        [DefaultValue (false)]
        public bool Enabled {
            get => enabled;
            set {
                if (enabled == value)
                    return;

                enabled = value;

                if (enabled)
                    StartTimer ();
                else
                    StopTimer ();
            }
        }

        /// <summary>
        /// Gets or sets the time, in milliseconds, between timer ticks.
        /// </summary>
        [DefaultValue (100)]
        public int Interval {
            get => interval;
            set {
                ArgumentOutOfRangeException.ThrowIfLessThan (value, 1);

                if (interval == value)
                    return;

                interval = value;

                if (platformTimer is not null)
                    platformTimer.IntervalMilliseconds = interval;
            }
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        public void Start () => Enabled = true;

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Stop () => Enabled = false;

        /// <summary>
        /// Raises the <see cref="Tick"/> event.
        /// </summary>
        protected virtual void OnTick (EventArgs e)
        {
            onTimer?.Invoke (this, e);
        }

        private void StartTimer ()
        {
            if (platformTimer is null) {
                platformTimer = Platform.Backend.CreateTimer ();
                platformTimer.Tick += () => OnTick (EventArgs.Empty);
            }

            platformTimer.IntervalMilliseconds = interval;
            platformTimer.Start ();
        }

        private void StopTimer ()
        {
            platformTimer?.Stop ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                enabled = false;
                StopTimer ();

                platformTimer?.Dispose ();
                platformTimer = null;
                onTimer = null;
            }

            base.Dispose (disposing);
        }

        /// <inheritdoc/>
        public override string ToString () => $"{base.ToString ()}, Interval: {Interval}";
    }
}
