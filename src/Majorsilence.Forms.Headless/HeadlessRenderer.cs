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

        /// <summary>Sends a key-down event; returns whether the window handled it.</summary>
        public static bool KeyDown (WindowBase window, Keys keys) => window.HandleKeyDown (keys);

        /// <summary>Sends a key-up event; returns whether the window handled it.</summary>
        public static bool KeyUp (WindowBase window, Keys keys) => window.HandleKeyUp (keys);

        /// <summary>Sends text input; returns whether the window handled it.</summary>
        public static bool TextInput (WindowBase window, string text) => window.HandleTextInput (text);
    }
}
