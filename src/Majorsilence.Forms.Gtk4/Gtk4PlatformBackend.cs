using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// An <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms on GTK 4 through the
    /// <see href="https://github.com/gircore/gir.core">gir.core</see> bindings. Majorsilence.Forms does
    /// its own SkiaSharp drawing; this backend presents each window through a <c>Gtk.DrawingArea</c>
    /// (Skia rendered straight into a Cairo image surface) and routes GTK event-controller input into
    /// the neutral <c>WindowBase.Handle*</c> path.
    ///
    /// The message loop is GLib's — <see cref="RunMainLoop"/> iterates <c>GLib.MainContext.Default()</c>
    /// on the calling thread, the same shape as the Headless backend's work-queue loop.
    /// </summary>
    public sealed class Gtk4PlatformBackend : IPlatformBackend, IDisposable
    {
        private static bool s_gtkInitialized;

        private readonly ConcurrentQueue<Action> _queue = new ();
        private int _uiThreadId = -1;
        private volatile bool _running;
        private string _clipboard = string.Empty;

        /// <inheritdoc/>
        public string Name => "Gtk4";

        /// <inheritdoc/>
        public void Initialize ()
        {
            if (!s_gtkInitialized) {
                // gir.core registers each namespace's type marshalling explicitly; the leaf call
                // cascades to its dependencies. Idempotent (guarded internally).
                Gio.Module.Initialize ();
                Gdk.Module.Initialize ();
                global::Gtk.Module.Initialize ();

                if (!global::Gtk.Functions.InitCheck ())
                    throw new InvalidOperationException (
                        "GTK 4 could not be initialized. A display server (X11/Wayland) must be available; " +
                        "this backend cannot run headless — use Majorsilence.Forms.Headless for that.");

                s_gtkInitialized = true;
            }

            if (_uiThreadId == -1)
                _uiThreadId = Environment.CurrentManagedThreadId;
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
        {
            Initialize ();
            _uiThreadId = Environment.CurrentManagedThreadId;
            _running = true;

            var ctx = GLib.MainContext.Default ();
            using var registration = token.Register (() => { _running = false; ctx.Wakeup (); });

            while (_running && !token.IsCancellationRequested) {
                DrainQueue ();

                if (ctx.Pending ()) {
                    ctx.Iteration (false);
                    continue;
                }

                // Nothing left to do — WinForms Application.Idle. Raised after the queue is drained so a
                // handler that queues work sees it drained first.
                Majorsilence.Forms.Application.RaiseIdle ();

                ctx.Iteration (true);   // blocks until the next event; Wakeup() breaks it on cancel/Post
            }

            DrainQueue ();
        }

        /// <inheritdoc/>
        public void Stop ()
        {
            _running = false;
            GLib.MainContext.Default ().Wakeup ();
        }

        /// <inheritdoc/>
        public void Post (Action action)
        {
            _queue.Enqueue (action);
            // IdleAdd is safe to call from any thread and wakes the main context.
            GLib.Functions.IdleAdd (0, () => { DrainQueue (); return false; });
        }

        /// <inheritdoc/>
        public void Invoke (Action action)
        {
            if (CheckAccess ()) {
                action ();
                return;
            }

            using var done = new ManualResetEventSlim (false);
            Exception? error = null;
            Post (() => {
                try { action (); }
                catch (Exception ex) { error = ex; }
                finally { done.Set (); }
            });
            done.Wait ();
            if (error is not null)
                throw error;
        }

        /// <inheritdoc/>
        public T Invoke<T> (Func<T> func)
        {
            if (CheckAccess ())
                return func ();

            T result = default!;
            Invoke (() => { result = func (); });
            return result;
        }

        /// <inheritdoc/>
        public bool CheckAccess () => _uiThreadId == -1 || Environment.CurrentManagedThreadId == _uiThreadId;

        /// <inheritdoc/>
        public void DoEvents ()
        {
            DrainQueue ();
            var ctx = GLib.MainContext.Default ();
            while (ctx.Pending ())
                ctx.Iteration (false);
        }

        private void DrainQueue ()
        {
            while (_queue.TryDequeue (out var action)) {
                try {
                    action ();
                } catch (Exception ex) when (Majorsilence.Forms.Application.RaiseThreadException (ex)) {
                    // Reported to Application.ThreadException; the loop keeps running, matching upstream.
                }
            }
        }

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup) => new Gtk4WindowHost (owner, isPopup);

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new Gtk4Timer ();

        // ── Clipboard ────────────────────────────────────────────────────────────
        // GDK's clipboard read is async-only; we own the main loop, so pump it until the read completes.
        // An in-process fallback covers the no-display / read-failure case.

        /// <inheritdoc/>
        public string GetClipboardText ()
        {
            try {
                var display = Gdk.Display.GetDefault ();
                if (display is null)
                    return _clipboard;

                var task = display.GetClipboard ().ReadTextAsync ();
                var ctx = GLib.MainContext.Default ();
                while (!task.IsCompleted)
                    ctx.Iteration (true);

                return task.Result ?? _clipboard;
            } catch {
                return _clipboard;
            }
        }

        /// <inheritdoc/>
        public void SetClipboardText (string text)
        {
            _clipboard = text ?? string.Empty;
            try { Gdk.Display.GetDefault ()?.GetClipboard ().SetText (_clipboard); }
            catch { /* best effort */ }
        }

        /// <inheritdoc/>
        public void ClearClipboard () => SetClipboardText (string.Empty);

        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
        {
            try {
                var display = Gdk.Display.GetDefault ();
                var monitors = display?.GetMonitors ();
                var count = monitors?.GetNItems () ?? 0;

                var screens = new List<ScreenInfo> ();
                for (uint i = 0; i < count; i++) {
                    if (monitors!.GetObject (i) is not Gdk.Monitor monitor)
                        continue;

                    monitor.GetGeometry (out var g);
                    var scale = Math.Max (1, monitor.GetScaleFactor ());
                    // Geometry is in application (logical) pixels; ScreenInfo is documented as device pixels.
                    var bounds = new System.Drawing.Rectangle (g.X * scale, g.Y * scale, g.Width * scale, g.Height * scale);
                    screens.Add (new ScreenInfo (monitor.GetConnector () ?? $"Monitor {i}", bounds, bounds, isPrimary: i == 0));
                }

                if (screens.Count > 0)
                    return screens.ToArray ();
            } catch {
                // fall through to the placeholder
            }

            var fallback = new System.Drawing.Rectangle (0, 0, 1920, 1080);
            return new[] { new ScreenInfo ("Gtk4", fallback, fallback, isPrimary: true) };
        }

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            var ctx = GLib.MainContext.Default ();

            // The task may complete on a background thread with nothing queued to wake the blocking
            // iteration; make its completion break the loop.
            completed.ContinueWith (_ => ctx.Wakeup (), System.Threading.Tasks.TaskScheduler.Default);

            while (!completed.IsCompleted) {
                DrainQueue ();
                if (completed.IsCompleted)
                    break;
                ctx.Iteration (true);
            }
            DrainQueue ();
        }

        /// <summary>Requests the loop to stop; does not tear down the GTK library (process-global).</summary>
        public void Dispose () => _running = false;

        private sealed class Gtk4Timer : IPlatformTimer
        {
            private uint _sourceId;
            private double _interval = 100;

            public double IntervalMilliseconds {
                get => _interval;
                set {
                    _interval = value <= 0 ? 1 : value;
                    if (_sourceId != 0) {
                        Stop ();
                        Start ();
                    }
                }
            }

            public event Action? Tick;

            public void Start ()
            {
                if (_sourceId != 0)
                    return;
                _sourceId = GLib.Functions.TimeoutAdd (0, (uint) Math.Max (1, _interval), () => {
                    Tick?.Invoke ();
                    return true;   // keep the source alive
                });
            }

            public void Stop ()
            {
                if (_sourceId == 0)
                    return;
                GLib.Functions.SourceRemove (_sourceId);
                _sourceId = 0;
            }

            public void Dispose () => Stop ();
        }
    }
}
