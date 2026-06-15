using System.ComponentModel;
using Avalonia.Threading;

namespace Modern.Forms
{
    /// <summary>
    /// WinForms compatibility: executes an operation on a background thread and marshals
    /// completion and progress notifications back to the UI thread.
    /// This implementation uses Task-based threading with Avalonia's Dispatcher for UI callbacks.
    /// </summary>
    public class BackgroundWorker : Component
    {
        private volatile bool _cancel_pending;
        private volatile bool _is_busy;

        /// <summary>Initializes a new instance of BackgroundWorker.</summary>
        public BackgroundWorker () { }

        /// <summary>Initializes a new instance of BackgroundWorker and adds it to the specified container.</summary>
        public BackgroundWorker (IContainer container) { container.Add (this); }

        /// <summary>Gets whether a cancel request is pending.</summary>
        public bool CancellationPending => _cancel_pending;

        /// <summary>Gets whether the worker is currently running.</summary>
        public bool IsBusy => _is_busy;

        /// <summary>Gets or sets whether the worker supports cancellation.</summary>
        public bool WorkerSupportsCancellation { get; set; }

        /// <summary>Gets or sets whether the worker supports progress reporting.</summary>
        public bool WorkerReportsProgress { get; set; }

        /// <summary>Raised on the background thread to perform the work.</summary>
        public event DoWorkEventHandler? DoWork;

        /// <summary>Raised on the UI thread when the operation completes.</summary>
        public event RunWorkerCompletedEventHandler? RunWorkerCompleted;

        /// <summary>Raised on the UI thread to report progress.</summary>
        public event ProgressChangedEventHandler? ProgressChanged;

        /// <summary>Starts the background operation with optional argument.</summary>
        public void RunWorkerAsync (object? argument = null)
        {
            if (_is_busy) throw new InvalidOperationException ("BackgroundWorker is already running.");
            _is_busy = true;
            _cancel_pending = false;

            Task.Run (() => {
                var args = new DoWorkEventArgs (argument);
                Exception? error = null;

                try {
                    DoWork?.Invoke (this, args);
                } catch (Exception ex) {
                    error = ex;
                } finally {
                    _is_busy = false;
                    var completed = new RunWorkerCompletedEventArgs (args.Result, error, args.Cancel);
                    Dispatcher.UIThread.Post (() => RunWorkerCompleted?.Invoke (this, completed));
                }
            });
        }

        /// <summary>Requests cancellation of the background operation.</summary>
        public void CancelAsync ()
        {
            if (!WorkerSupportsCancellation)
                throw new InvalidOperationException ("WorkerSupportsCancellation must be true to call CancelAsync.");
            _cancel_pending = true;
        }

        /// <summary>Reports progress from the background thread to the UI thread.</summary>
        public void ReportProgress (int percentProgress, object? userState = null)
        {
            if (!WorkerReportsProgress)
                throw new InvalidOperationException ("WorkerReportsProgress must be true to call ReportProgress.");
            var args = new ProgressChangedEventArgs (percentProgress, userState);
            Dispatcher.UIThread.Post (() => ProgressChanged?.Invoke (this, args));
        }
    }

#pragma warning disable CA1711
    /// <summary>Delegate for the BackgroundWorker.DoWork event.</summary>
    public delegate void DoWorkEventHandler (object sender, DoWorkEventArgs e);

    /// <summary>Delegate for the BackgroundWorker.RunWorkerCompleted event.</summary>
    public delegate void RunWorkerCompletedEventHandler (object sender, RunWorkerCompletedEventArgs e);
#pragma warning restore CA1711

    /// <summary>Provides data for the BackgroundWorker.DoWork event.</summary>
    public class DoWorkEventArgs : CancelEventArgs
    {
        /// <summary>Initializes a new instance with the optional argument.</summary>
        public DoWorkEventArgs (object? argument) { Argument = argument; }

        /// <summary>Gets the argument passed to RunWorkerAsync.</summary>
        public object? Argument { get; }

        /// <summary>Gets or sets the result to pass to RunWorkerCompleted.</summary>
        public object? Result { get; set; }
    }

    /// <summary>Provides data for the BackgroundWorker.RunWorkerCompleted event.</summary>
    public class RunWorkerCompletedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public RunWorkerCompletedEventArgs (object? result, Exception? error, bool cancelled)
        {
            Result = result;
            Error = error;
            Cancelled = cancelled;
        }

        /// <summary>Gets the result of the background operation.</summary>
        public object? Result { get; }

        /// <summary>Gets the exception raised during the background operation, or null if none.</summary>
        public Exception? Error { get; }

        /// <summary>Gets whether the operation was cancelled.</summary>
        public bool Cancelled { get; }
    }
}
