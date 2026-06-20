using System;
using Continuum.Forms.Backends;

namespace Continuum.Forms.Headless
{
    /// <summary>
    /// Helpers for rendering a Continuum.Forms window offscreen on the <see cref="HeadlessPlatformBackend"/>.
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
            if (Platform.Backend is not HeadlessPlatformBackend)
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

        /// <summary>Sends a pointer-moved event (client coordinates) to the window.</summary>
        public static void MouseMove (WindowBase window, int x, int y, MouseButtons buttons = MouseButtons.None)
            => window.HandlePointerMoved (buttons, x, y, Keys.None);

        /// <summary>Sends a pointer-pressed event (client coordinates) to the window.</summary>
        public static void MouseDown (WindowBase window, int x, int y, MouseButtons button = MouseButtons.Left)
            => window.HandlePointerPressed (button, x, y, Keys.None);

        /// <summary>Sends a pointer-released event (client coordinates) to the window.</summary>
        public static void MouseUp (WindowBase window, int x, int y, MouseButtons button = MouseButtons.Left)
            => window.HandlePointerReleased (button, x, y, Keys.None);

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
