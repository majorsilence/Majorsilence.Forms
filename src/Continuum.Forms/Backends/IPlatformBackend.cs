using System;
using System.Threading;

namespace Continuum.Forms.Backends
{
    /// <summary>
    /// Abstraction over the host UI platform (windowing toolkit + message loop) that Continuum.Forms
    /// runs on. Continuum.Forms does all of its own drawing with SkiaSharp; a backend only provides
    /// application bootstrap, the UI thread / message loop, and (in later phases) windows and input.
    ///
    /// The default backend is Avalonia (<see cref="AvaloniaPlatformBackend"/>); a Uno backend can be
    /// substituted via <see cref="Platform.Backend"/> before the first window is created.
    /// </summary>
    public interface IPlatformBackend
    {
        /// <summary>A short identifier for the backend (e.g. "Avalonia", "Uno").</summary>
        string Name { get; }

        /// <summary>Performs one-time platform initialization. Safe to call repeatedly (idempotent).</summary>
        void Initialize ();

        /// <summary>Runs the UI message loop, blocking until the token is cancelled.</summary>
        void RunMainLoop (CancellationToken token);

        /// <summary>Requests the running message loop to exit.</summary>
        void Stop ();

        /// <summary>Posts an action to run asynchronously on the UI thread.</summary>
        void Post (Action action);

        /// <summary>Runs an action synchronously on the UI thread (marshalling if needed).</summary>
        void Invoke (Action action);

        /// <summary>Runs a function synchronously on the UI thread (marshalling if needed) and returns its result.</summary>
        T Invoke<T> (Func<T> func);

        /// <summary>Returns whether the calling thread is the UI thread.</summary>
        bool CheckAccess ();

        /// <summary>Processes pending UI work without exiting the loop (WinForms Application.DoEvents).</summary>
        void DoEvents ();

        /// <summary>
        /// Creates a native window for the given owner. <paramref name="isPopup"/> requests a
        /// borderless, top-most popup (e.g. a ComboBox dropdown or context menu) rather than a
        /// top-level window.
        /// </summary>
        IWindowBackend CreateWindow (WindowBase owner, bool isPopup);

        /// <summary>Creates a UI-thread timer (used by <see cref="Continuum.Forms.Timer"/>).</summary>
        IPlatformTimer CreateTimer ();

        // ── Clipboard (synchronous; the backend marshals to the UI thread as needed) ──
        /// <summary>Gets the clipboard text, or an empty string.</summary>
        string GetClipboardText ();
        /// <summary>Sets the clipboard text.</summary>
        void SetClipboardText (string text);
        /// <summary>Clears the clipboard.</summary>
        void ClearClipboard ();

        /// <summary>Enumerates the connected display devices. Returns an empty array if unknown.</summary>
        ScreenInfo[] GetScreens ();

        /// <summary>
        /// Runs a nested message loop (blocking the caller) until <paramref name="completed"/> finishes,
        /// so a modal dialog can pump input without returning to the outer loop.
        /// </summary>
        void RunModalLoop (System.Threading.Tasks.Task completed);
    }
}
