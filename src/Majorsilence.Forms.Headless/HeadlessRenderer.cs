using System;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Headless
{
    /// <summary>
    /// Helpers for rendering a Majorsilence.Forms window offscreen on the <see cref="HeadlessPlatformBackend"/>.
    /// Useful for tests, server-side image generation, and proving the gallery renders without a
    /// windowing toolkit.
    /// </summary>
    public static class HeadlessRenderer
    {
        /// <summary>Gets the active Headless backend, for the members below that read or set state on it.</summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        private static HeadlessPlatformBackend Backend
            => Platform.ConfiguredBackend as HeadlessPlatformBackend
                ?? throw new InvalidOperationException ("The Headless backend is not the active platform; call HeadlessRenderer.Use () first.");

        /// <summary>
        /// Gets the animation frames of the active Headless backend, which run only when stepped by hand:
        /// <c>HeadlessRenderer.AnimationClock.Step (10)</c> runs ten frames. Call <see cref="Use"/> first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static HeadlessAnimationClock AnimationClock => Backend.AnimationClock;

        /// <summary>
        /// Gets or sets what <see cref="SystemInformation.PrefersReducedMotion"/> reports while the Headless backend is active, so a
        /// test can set the reduced-motion answer directly instead of a real system setting existing to poll. Setting it to the value
        /// it already holds raises no change event. Call <see cref="Use"/> first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static bool PrefersReducedMotion {
            get => Backend.PrefersReducedMotion;
            set => Backend.PrefersReducedMotion = value;
        }

        /// <summary>
        /// Gets every <see cref="Media.SoundPlayer"/>/<see cref="Media.SystemSounds"/> play request the active Headless backend has
        /// recorded, in order, so a test can assert what was asked for without a real audio engine. Call <see cref="Use"/> first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static System.Collections.Generic.IReadOnlyList<AudioPlayRequest> AudioRequests => Backend.AudioRequests;

        /// <summary>Clears <see cref="AudioRequests"/> between tests. Call <see cref="Use"/> first.</summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static void ClearAudioRequests () => Backend.ClearAudioRequests ();

        /// <summary>
        /// Gets or sets whether the active Headless backend answers a play request at all -- false by default (see the
        /// remarks on <see cref="HeadlessPlatformBackend.AudioIsSupported"/> for why), so a test that wants to assert
        /// backend-first routing sets this true itself. Call <see cref="Use"/> first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static bool AudioIsSupported {
            get => Backend.AudioIsSupported;
            set => Backend.AudioIsSupported = value;
        }

        /// <summary>
        /// Gets every <see cref="Media.AudioPlayer.Play"/> request the active Headless backend has recorded, in order
        /// (including tracks no longer playing), so a test can assert what was asked for without a real audio engine.
        /// Call <see cref="Use"/> first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static System.Collections.Generic.IReadOnlyList<AudioTrackRequest> AudioTrackRequests => Backend.AudioTrackRequests;

        /// <summary>Gets the number of tracks started via <see cref="Media.AudioPlayer.Play"/> that have not completed or been stopped yet. Call <see cref="Use"/> first.</summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static int ActiveAudioTrackCount => Backend.ActiveAudioTrackCount;

        /// <summary>Clears <see cref="AudioTrackRequests"/> and any still-live fake tracks between tests. Call <see cref="Use"/> first.</summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static void ClearAudioTrackRequests () => Backend.ClearAudioTrackRequests ();

        /// <summary>
        /// Raises <see cref="Media.IAudioTrack.Completed"/> on the oldest track started via <see cref="Media.AudioPlayer.Play"/>
        /// that has not completed or been stopped yet, simulating it finishing on its own. Returns <c>false</c> if none is
        /// pending. Call <see cref="Use"/> first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Headless backend is not the active platform.</exception>
        public static bool CompleteNextAudioTrack () => Backend.CompleteNextAudioTrack ();

        /// <summary>
        /// What an open-file picker on the headless backend returns: the files "chosen" for the request it
        /// is handed, or none (a cancel) when null -- the default. Lets a test drive the real
        /// <c>OpenFileDialog</c> code path: the extension, FilterIndex and FileOk handling after the pick.
        /// </summary>
        public static System.Func<Majorsilence.Forms.Backends.OpenFileRequest, string[]>? OpenFileResponse { get; set; }

        /// <summary>What a save-file picker on the headless backend returns; null (the default) cancels.</summary>
        public static System.Func<Majorsilence.Forms.Backends.SaveFileRequest, string?>? SaveFileResponse { get; set; }

        /// <summary>
        /// Installs the headless backend as the active platform. Call once before creating any window.
        /// </summary>
        public static void Use ()
        {
            // ConfiguredBackend, not Backend: the getter resolves (and throws for) a default backend,
            // so asking it here threw "No platform backend is configured" in exactly the case this
            // method exists to handle — a process with no backend referenced at all.
            if (Platform.ConfiguredBackend is not HeadlessPlatformBackend)
                Platform.Backend = new HeadlessPlatformBackend ();

            // Every test calls this first, on its own thread: that thread is the UI thread for the test (see ClaimUiThread).
            ((HeadlessPlatformBackend)Platform.ConfiguredBackend!).ClaimUiThread ();
        }

        /// <summary>
        /// Whether the window currently owns a shown OS window — what <c>Form.TopLevel</c> decides.
        /// </summary>
        public static bool OwnsShownWindow (WindowBase window)
        {
            ArgumentNullException.ThrowIfNull (window);
            return window.Backend is HeadlessWindowHost host && host.IsShown;
        }

        /// <summary>
        /// How many times a window has been asked to start a caption move-drag since this was last
        /// reset. Lets a test assert that a claimed caption gesture did not move the window.
        /// </summary>
        public static int MoveDragCount {
            get => HeadlessWindowHost.MoveDragCount;
            set => HeadlessWindowHost.MoveDragCount = value;
        }

        /// <summary>
        /// Simulates a platform that draws chrome above/left of the client area (a native title bar), so
        /// a window's client origin no longer coincides with its own <see cref="WindowBase.Location"/>.
        /// Default is none. Reset it in a test's finally, as it is process-wide.
        /// </summary>
        public static System.Drawing.Size ChromeOffset {
            get => HeadlessWindowHost.ChromeOffset;
            set => HeadlessWindowHost.ChromeOffset = value;
        }

        /// <summary>
        /// Renders the given window to PNG bytes at the specified size. The window must have been
        /// created on the headless backend (call <see cref="Use"/> before constructing it).
        /// </summary>
        public static byte[] CapturePng (WindowBase window, int width = 0, int height = 0)
        {
            ArgumentNullException.ThrowIfNull (window);

            if (window.Backend is not HeadlessWindowHost host)
                throw new InvalidOperationException (
                    "Window is not hosted on the Headless backend. Call HeadlessRenderer.Use () before creating it.");

            if (width > 0 && height > 0)
                host.Size = new System.Drawing.Size (width, height);

            return host.CapturePng ();
        }

        // ── Input injection (drives the same neutral input path a real backend uses) ──

        // The coordinates below are LOGICAL client coordinates -- the same units a caller reads off
        // Control.Bounds, which is what makes "click the centre of that button" expressible. The window's
        // pointer handlers take device pixels (a real backend multiplies by its render scaling before
        // calling them), so convert here. At scaling 1 this is identity, which is why passing them
        // straight through worked until a scaled display was simulated: every injected click then landed
        // at 1/scale of its intended position and hit whatever was there instead.
        private static int ToDevice (WindowBase window, int logical)
            => (int)System.Math.Round (logical * window.Scaling);

        /// <summary>Sends a pointer-moved event (logical client coordinates) to the window.</summary>
        public static void MouseMove (WindowBase window, int x, int y, MouseButtons buttons = MouseButtons.None)
            => window.HandlePointerMoved (buttons, ToDevice (window, x), ToDevice (window, y), Keys.None);

        /// <summary>Sends a pointer-pressed event (logical client coordinates) to the window.</summary>
        public static void MouseDown (WindowBase window, int x, int y, MouseButtons button = MouseButtons.Left)
            => window.HandlePointerPressed (button, ToDevice (window, x), ToDevice (window, y), Keys.None);

        /// <summary>Sends a pointer-released event (logical client coordinates) to the window.</summary>
        public static void MouseUp (WindowBase window, int x, int y, MouseButtons button = MouseButtons.Left)
            => window.HandlePointerReleased (button, ToDevice (window, x), ToDevice (window, y), Keys.None);

        /// <summary>Sends a full click (move → down → up) at the given client coordinates.</summary>
        public static void Click (WindowBase window, int x, int y, MouseButtons button = MouseButtons.Left)
        {
            MouseMove (window, x, y, button);
            MouseDown (window, x, y, button);
            MouseUp (window, x, y, button);
        }

        /// <summary>
        /// Sends a mouse-wheel event (logical client coordinates) to the window. <paramref name="delta"/>
        /// is in WinForms units (a notch is 120); positive scrolls up.
        /// </summary>
        public static void MouseWheel (WindowBase window, int x, int y, int delta)
            => window.HandlePointerWheel (MouseButtons.None, ToDevice (window, x), ToDevice (window, y),
                                          new System.Drawing.Point (0, delta), Keys.None);

        /// <summary>Sends a key-down event; returns whether the window handled it.</summary>
        public static bool KeyDown (WindowBase window, Keys keys) => window.HandleKeyDown (keys);

        /// <summary>Sends a key-up event; returns whether the window handled it.</summary>
        public static bool KeyUp (WindowBase window, Keys keys) => window.HandleKeyUp (keys);

        /// <summary>Sends text input; returns whether the window handled it.</summary>
        public static bool TextInput (WindowBase window, string text) => window.HandleTextInput (text);
    }
}
