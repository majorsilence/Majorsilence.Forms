using System;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Majorsilence.Forms.Backends;
using SkiaSharp.Views.Windows;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Uno
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Majorsilence.Forms window through a Uno
    /// <c>SKXamlCanvas</c>: its <c>PaintSurface</c> calls <c>WindowBase.RenderFrame</c>, and Uno
    /// pointer/keyboard/character events are translated into the neutral <c>WindowBase.Handle*</c> path.
    ///
    /// Top-level windows use a <see cref="Window"/>. Popups (menus, combo dropdowns) use an in-window
    /// <see cref="Popup"/> overlay parented to the main window's XamlRoot — Uno's macOS Skia head does
    /// not place/strip-chrome secondary <see cref="Window"/>s correctly (AppWindow.Position returns
    /// (0,0), Move uses an AppKit bottom-left origin, and the title bar can't be removed).
    /// </summary>
    internal sealed class UnoWindowHost : IWindowBackend, IUnoHostSurface, INativeControlHostBackend
    {
        private readonly WindowBase _owner;
        private readonly bool _isPopup;
        private readonly IUnoHostSurface? _parentHost;
        private readonly CursorCanvas _canvas;
        private readonly Microsoft.UI.Xaml.Controls.Grid _root;
        private readonly System.Collections.Generic.Dictionary<Majorsilence.Forms.NativeControlHost, Microsoft.UI.Xaml.UIElement> _overlays = new ();

        // Exactly one of these is non-null: _window for top-level windows, _popup for overlays.
        private readonly Window? _window;
        private readonly Popup? _popup;

        // Exposes the wrapped native window to UnoHostInterop.ToUnoWindow, for host apps that want to
        // show/use it directly as a native WinUI window rather than through Form.Show(). Null for popups.
        internal Window? NativeWindow => _window;

        private Size _size = new (800, 600);
        private Point _location;
        private bool _systemDecorations;

        private bool PopupMode => _popup is not null;

        // Font warmup runs once — subsequent calls are instant (caches are already populated).
        private static bool _fontWarmedUp;

        private static void EnsureFontsWarmedUp ()
        {
            if (_fontWarmedUp) return;
            _fontWarmedUp = true;
            Majorsilence.Forms.Theme.WarmupFonts ();
        }

        // Xlib's default error handler calls exit() on ANY unhandled X protocol error — fatal for the
        // whole process over what is very often a harmless, transient race (e.g. querying a window
        // that another part of the app — Uno's own internals included, not just code here — just
        // destroyed, like a short-lived wait/splash form closing during startup). Observed in practice:
        // Uno's X11 Skia head issues an XQueryTree that raced a closing window and took the entire app
        // down with it, unrelated to anything in Majorsilence.Forms.Uno. Xlib's error handler is
        // process-global (not per-Display), so installing a non-fatal one here, once, up front,
        // protects every X11 call in the process for its whole lifetime, not just calls made from
        // here. This is strictly a robustness improvement: Majorsilence.Forms.Uno never depended on
        // the process dying on an X error, and no error handled this way was otherwise going to be
        // acted upon (the default handler's only alternative action is exit()).
        private static bool _x11ErrorHandlerInstalled;

        private static void EnsureX11ErrorHandlerInstalled ()
        {
            if (_x11ErrorHandlerInstalled || !OperatingSystem.IsLinux ())
                return;
            _x11ErrorHandlerInstalled = true;
            try {
                _ = XSetErrorHandler (s_ignoreXErrorsPtr);
            } catch {
                // Best-effort — if libX11 isn't even loadable this can't matter either way.
            }
        }

        public UnoWindowHost (WindowBase owner, bool isPopup, IUnoHostSurface? parentHost)
        {
            EnsureFontsWarmedUp ();
            EnsureX11ErrorHandlerInstalled ();
            _owner = owner;
            _isPopup = isPopup;
            _parentHost = parentHost;
            _systemDecorations = !isPopup;

            // Wrap the drawing canvas in a Grid so native controls hosted inside the Majorsilence scene
            // (NativeControlHost / airspace overlay) can be layered above it.
            _root = new Microsoft.UI.Xaml.Controls.Grid ();

            if (isPopup && parentHost is not null) {
                // In-window overlay — no separate OS window, so no chrome / positioning quirks.
                _popup = new Popup { Child = _root };
            } else {
                // Create the Window before the canvas: on X11, SKXamlCanvas.Initialize() calls
                // DisplayInformation.GetForCurrentView() which requires a X11XamlRootHost, and that
                // host is only available once a Window exists.
                _window = new Window { Content = _root };
                WireLifecycle ();
                ApplyDecorations ();
            }

            _canvas = new CursorCanvas ();
            _canvas.PaintSurface += OnPaintSurface;
            // On the Windows/X11 heads the canvas only receives keyboard events while it holds focus; grab it
            // once it's in the visual tree (and on Show / pointer press). On macOS routed key events never
            // reach the canvas, so also hook the native host's KeyDown (see WireMacOSKeyboard). On at least
            // one real Linux/X11 environment the routed events never fire either — WireX11KeyboardFallback
            // detects that and takes over if so (see its own comment).
            _canvas.Loaded += (_, _) => { TryFocus (); WireMacOSKeyboard (); WireX11KeyboardFallback (); };

            WireInput ();
            UnoGestureWiring.Attach (_canvas, _owner, () => Scaling);
            _root.Children.Add (_canvas);
        }

        public Microsoft.UI.Xaml.XamlRoot? CanvasXamlRoot => _canvas.XamlRoot;
        public double HostScaling => _canvas.XamlRoot?.RasterizationScale ?? 1.0;

        private Microsoft.UI.Windowing.OverlappedPresenter? Presenter
            => _window?.AppWindow?.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;

        // ── Geometry ──
        public Point Location {
            get {
                if (PopupMode)
                    return _location;
                // macOS: AppWindow.Position is unreliable (stale (0,0) until moved, and bottom-left origin),
                // so read the true top-left from the native NSWindow frame. See NativeWindowHandle.
                if (OperatingSystem.IsMacOS () && TryGetNativeTopLeft (out var nativeTopLeft))
                    return nativeTopLeft;
                try {
                    var p = _window!.AppWindow?.Position;
                    if (p is not null)
                        return new Point (p.Value.X, p.Value.Y);
                } catch { }
                return _location;
            }
            set { _location = value; if (!PopupMode) TryMove (value); }
        }

        public Size Size {
            get => _size;
            set {
                _size = value;
                if (PopupMode) {
                    _canvas.Width = value.Width;
                    _canvas.Height = value.Height;
                } else {
                    TryResize (value);
                }
            }
        }

        public Size ClientSize => _size;

        public double Scaling => _canvas.XamlRoot?.RasterizationScale ?? _parentHost?.HostScaling ?? 1.0;

        private void TryResize (Size size)
        {
            var s = Scaling;
            try { _window!.AppWindow?.Resize (new Windows.Graphics.SizeInt32 { Width = (int) (size.Width * s), Height = (int) (size.Height * s) }); } catch { }
        }

        private void TryMove (Point location)
        {
            // macOS: place by true top-left (convert to the AppKit bottom-left origin AppWindow.Move wants).
            if (OperatingSystem.IsMacOS () && TryMoveNativeTopLeft (location))
                return;
            try { _window!.AppWindow?.Move (new Windows.Graphics.PointInt32 { X = location.X, Y = location.Y }); } catch { }
        }

        // ── Lifecycle ──
        public void Show ()
        {
            if (PopupMode) {
                ShowPopup ();
                return;
            }

            ApplyDecorations ();
            _window!.Activate ();
            TryFocus ();   // also re-asserts real X11 WM focus (FixX11InputHint) — see TryFocus's comment.
            // Activation/focus and the native host can settle a tick later; retry on the dispatcher too.
            // Re-declare caption regions then as well — the AppWindow id and scaling are reliably available.
            _canvas.DispatcherQueue?.TryEnqueue (() => { TryFocus (); WireMacOSKeyboard (); WireX11KeyboardFallback (); ApplyCaptionRegions (); });
            WireMacOSKeyboard ();
            WireX11KeyboardFallback ();
        }

        // On the X11 Skia head, Uno.WinUI.Runtime.Skia.X11 creates the window without a WM_HINTS
        // property carrying the ICCCM InputHint flag. Per ICCCM 4.1.7, a window that declares
        // neither WM_HINTS.input nor WM_TAKE_FOCUS is a "No Input" client — the window manager
        // (confirmed on GNOME/mutter) then never transfers keyboard/pointer focus to it on click, so
        // every click on the window is silently swallowed and the app looks completely frozen (it
        // still responds fine to programmatic activation, e.g. via a _NET_ACTIVE_WINDOW client
        // message or the taskbar — just not to a normal click). Setting WM_HINTS ourselves via Xlib
        // fixes click-to-focus; verified live against a running window with this exact property
        // change before writing the interop below.
        //
        // WinRT.Interop.WindowNative.GetWindowHandle(_window) does NOT return the real X11 Window
        // XID on this head (verified: passing it to XSetWMHints raised X_ChangeProperty BadWindow),
        // and the actual XID lives on Uno's internal X11Window struct with no supported way to reach
        // it (reflection into the current Uno.UI.Runtime.Skia.X11 internals is brittle across
        // versions and several dependent types wouldn't even load outside the full Uno host).
        // Instead, find our own top-level window the same way any X11 client-side tool would: walk
        // the root window's children and match by title, which we already set on this exact _window
        // and can read back — no dependency on Uno internals at all.
        //
        // This runs on every TryFocus() call (see its comment), not just once at Show(). Two things
        // are cached across calls, and one deliberately is not:
        //  - The Display* connection IS cached/reused: XOpenDisplay's connection handshake is the
        //    expensive part, and redoing it on every single click was enough added latency to make
        //    the whole window feel unresponsive to both mouse and keyboard (measured).
        //  - The _NET_ACTIVE_WINDOW atom IS cached: interned atoms never change for a given
        //    connection.
        //  - The window's XID is deliberately NOT cached — it's re-resolved by title on every call.
        //    A cached XID can go stale (the WM/compositor can reparent or recreate the underlying
        //    X11 window across state changes) and EnsureX11ErrorHandlerInstalled's handler swallows
        //    the resulting BadWindow silently, so a stale cached XID doesn't throw or log anything —
        //    it just quietly stops doing anything, which is what "worked for a bit, then keyboard
        //    input stopped" looks like from the outside. Re-resolving is cheap once the connection is
        //    already open (one XQueryTree round trip plus XFetchName per candidate, all on a local
        //    socket), so this stays self-healing without reintroducing the earlier per-click latency.
        private IntPtr _x11FocusDisplay;
        private IntPtr _x11ActiveWindowAtom;

        private void FixX11InputHint ()
        {
            if (!OperatingSystem.IsLinux () || _window is null)
                return;

            try {
                if (_x11FocusDisplay == IntPtr.Zero) {
                    var display = XOpenDisplay (IntPtr.Zero);
                    if (display == IntPtr.Zero)
                        return;
                    _x11ActiveWindowAtom = XInternAtom (display, "_NET_ACTIVE_WINDOW", false);
                    _x11FocusDisplay = display;
                } else {
                    // This connection never calls XSelectInput on anything, so it shouldn't accumulate
                    // a real event backlog — but it's long-lived (kept open for the window's whole
                    // lifetime) and this housekeeping is nearly free, so discard anything queued up
                    // rather than assume that's true. An unread backlog on a long-lived connection is
                    // a classic way for later requests on it to start behaving oddly.
                    _ = XSync (_x11FocusDisplay, discard: true);
                }

                var title = _window.Title;
                if (string.IsNullOrEmpty (title))
                    return;

                // A BadWindow race here is expected and harmless (see EnsureX11ErrorHandlerInstalled):
                // XQueryTree returns a snapshot of the root's children, and some of them (this app's
                // own splash/wait form included) can be destroyed before we get to XFetchName on them.
                var root = XRootWindow (_x11FocusDisplay, XDefaultScreen (_x11FocusDisplay));
                if (!TryFindTopLevelWindowByTitle (_x11FocusDisplay, root, title, out var xid))
                    return;

                var hints = new XWMHints { flags = checked ((IntPtr) InputHint), input = 1 };
                _ = XSetWMHints (_x11FocusDisplay, xid, ref hints);

                var clientMessage = new XClientMessageEvent {
                    type = 33, // ClientMessage
                    window = xid,
                    message_type = _x11ActiveWindowAtom,
                    format = 32,
                    l0 = 1, // source indication: normal application
                };
                const long SubstructureRedirectMask = 1 << 20;
                const long SubstructureNotifyMask = 1 << 19;
                _ = XSendEvent (_x11FocusDisplay, root, 0, checked ((IntPtr) (SubstructureRedirectMask | SubstructureNotifyMask)), ref clientMessage);
                const int RevertToParent = 2;
                _ = XSetInputFocus (_x11FocusDisplay, xid, RevertToParent, IntPtr.Zero);
                _ = XFlush (_x11FocusDisplay);
            } catch {
                // Best-effort: if this fails (e.g. libX11 unavailable, or a future Uno release fixes
                // the missing hint upstream), the window just keeps needing the taskbar/Alt+Tab to
                // gain focus instead of a direct click.
            }
        }

        private void CloseX11KeyboardFallback ()
        {
            _x11KeyFallbackTimer?.Stop ();
            _x11KeyFallbackTimer = null;
            if (_x11KeyListenerDisplay == IntPtr.Zero)
                return;
            try { _ = XCloseDisplay (_x11KeyListenerDisplay); } catch { }
            _x11KeyListenerDisplay = IntPtr.Zero;
        }

        private void CloseX11FocusDisplay ()
        {
            if (_x11FocusDisplay == IntPtr.Zero)
                return;
            try { _ = XCloseDisplay (_x11FocusDisplay); } catch { }
            _x11FocusDisplay = IntPtr.Zero;
        }

        // Kept as static fields, not a local/lambda: a delegate passed to native code must stay
        // reachable for as long as native code might invoke it, or the GC can collect it out from
        // under the callback and crash the process on the next X error. The function pointer is
        // computed once and handled as an opaque IntPtr from here on — converting a delegate to a
        // native pointer to pass as an argument is the well-supported direction; the reverse
        // (treating whatever XSetErrorHandler previously returned as an invokable delegate) is not
        // something this needs, since the handler installed here is meant to stay for good.
        private static readonly XErrorHandler s_ignoreXErrors = (_, _) => 0;
        private static readonly IntPtr s_ignoreXErrorsPtr =
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate (s_ignoreXErrors);

        [System.Runtime.InteropServices.UnmanagedFunctionPointer (System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int XErrorHandler (IntPtr display, IntPtr errorEvent);

        private static bool TryFindTopLevelWindowByTitle (IntPtr display, IntPtr root, string title, out IntPtr match)
        {
            match = IntPtr.Zero;
            if (XQueryTree (display, root, out _, out _, out var childrenPtr, out var count) == 0 || childrenPtr == IntPtr.Zero)
                return false;

            try {
                for (var i = 0; i < count; i++) {
                    var child = System.Runtime.InteropServices.Marshal.ReadIntPtr (childrenPtr, i * IntPtr.Size);
                    if (XFetchName (display, child, out var namePtr) == 0 || namePtr == IntPtr.Zero)
                        continue;
                    try {
                        var name = System.Runtime.InteropServices.Marshal.PtrToStringAnsi (namePtr);
                        if (name == title) {
                            match = child;
                            return true;
                        }
                    } finally {
                        _ = XFree (namePtr);
                    }
                }
            } finally {
                _ = XFree (childrenPtr);
            }
            return false;
        }

        [System.Runtime.InteropServices.StructLayout (System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct XWMHints
        {
            public IntPtr flags;
            public int input;
            public int initial_state;
            public IntPtr icon_pixmap;
            public IntPtr icon_window;
            public int icon_x, icon_y;
            public IntPtr icon_mask;
            public IntPtr window_group;
        }

        private const long InputHint = 1L;

        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern IntPtr XOpenDisplay (IntPtr display);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XCloseDisplay (IntPtr display);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XSetWMHints (IntPtr display, IntPtr w, ref XWMHints hints);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XFlush (IntPtr display);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XDefaultScreen (IntPtr display);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern IntPtr XRootWindow (IntPtr display, int screenNumber);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XQueryTree (IntPtr display, IntPtr w, out IntPtr rootReturn, out IntPtr parentReturn, out IntPtr childrenReturn, out uint childrenCountReturn);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XFetchName (IntPtr display, IntPtr w, out IntPtr windowNameReturn);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XFree (IntPtr data);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern IntPtr XSetErrorHandler (IntPtr handler);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XSetInputFocus (IntPtr display, IntPtr focus, int revertTo, IntPtr time);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XSync (IntPtr display, bool discard);

        // On the macOS Skia head the canvas never receives routed XAML key events (focus is correct, but the
        // managed input pipeline doesn't surface KeyDown to an SKXamlCanvas). The native MacOSWindowHost does
        // expose a KeyDown event, so hook that directly (via reflection — the host type is internal). No-op on
        // other heads (the type isn't present), where the routed handlers above carry the keyboard.
        private bool _macKeyboardWired;

        private void WireMacOSKeyboard ()
        {
            if (_macKeyboardWired || _window is null)
                return;
            try {
                var hostType = Type.GetType ("Uno.UI.Runtime.Skia.MacOS.MacOSWindowHost, Uno.UI.Runtime.Skia.MacOS");
                if (hostType is null)
                    return;   // not the macOS head

                var windowsField = hostType.GetField ("_windows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var winField = hostType.GetField ("_winUIWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var keyDownEvt = hostType.GetEvent ("KeyDown");
                var keyUpEvt = hostType.GetEvent ("KeyUp");
                if (windowsField?.GetValue (null) is not System.Collections.IDictionary dict || winField is null || keyDownEvt is null)
                    return;

                foreach (var value in dict.Values) {
                    if (value is null)
                        continue;
                    var tryGet = value.GetType ().GetMethod ("TryGetTarget");
                    if (tryGet is null)
                        continue;
                    var args = new object?[] { null };
                    if (tryGet.Invoke (value, args) is not true || args[0] is not { } host)
                        continue;
                    if (!ReferenceEquals (winField.GetValue (host), _window))
                        continue;

                    keyDownEvt.AddEventHandler (host, new Windows.Foundation.TypedEventHandler<object, Windows.UI.Core.KeyEventArgs> (OnMacKeyDown));
                    keyUpEvt?.AddEventHandler (host, new Windows.Foundation.TypedEventHandler<object, Windows.UI.Core.KeyEventArgs> (OnMacKeyUp));
                    _macKeyboardWired = true;
                    return;
                }
            } catch {
                // Reflection into the internal host is best-effort; the routed handlers remain as a fallback.
            }
        }

        // ── X11 keyboard fallback ───────────────────────────────────────────────────────────────────
        // Confirmed on GNOME/mutter (Wayland session, XWayland X11 backend): real X11 keyboard focus is
        // genuinely granted to this window (_NET_WM_STATE_FOCUSED set) and real X11 KeyPress/KeyRelease
        // events independently confirmed to reach it, yet the routed KeyDownEvent/KeyUpEvent handlers
        // above never fire for them — an Uno.WinUI.Runtime.Skia.X11 bug/incompatibility below where this
        // class hooks in, not anything fixable by asking harder for focus (already tried, several times).
        //
        // Reads raw X11 KeyPress/KeyRelease events directly, via our own dedicated connection and
        // XLookupString (the same call Uno's own X11KeyboardInputSource uses internally, so this
        // resolves keysyms/text the same way — respecting the real X11 keymap, not a hand-rolled ASCII
        // table), translated to a Majorsilence.Forms Keys the same way OnMacKeyDown/OnMacKeyUp above
        // already do for the analogous macOS gap: straight to _owner.HandleKeyDown/HandleKeyUp/
        // HandleTextInput, bypassing WinUI's routed-event system (and this class's own
        // KeyDownEvent/KeyUpEvent handlers) entirely, so it can never re-trigger them.
        //
        // Only takes over if the native routed handlers above never fire within a grace window — polling
        // continues indefinitely (so a delayed first real keypress still gets picked up), but this stays
        // fully inert wherever the native pipeline already works (confirmed working: Avalonia, macOS via
        // the hook above, and this class's own routed handlers on whichever heads they're not broken on),
        // which avoids ever double-handling a keypress there.
        private bool _unoNativeKeyPipelineConfirmedWorking;
        private IntPtr _x11KeyListenerDisplay;
        private long _x11FallbackArmAtTicks;
        private Microsoft.UI.Xaml.DispatcherTimer? _x11KeyFallbackTimer;

        private void WireX11KeyboardFallback ()
        {
            if (!OperatingSystem.IsLinux () || _window is null || _x11KeyFallbackTimer is not null)
                return;

            // Give the native routed handlers a couple of seconds to prove themselves (from any keyboard
            // activity at all, not necessarily from the user's own typing) before this is willing to act
            // on anything it reads — see the class-level comment above for why that matters.
            _x11FallbackArmAtTicks = Environment.TickCount64 + 2000;

            _x11KeyFallbackTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromMilliseconds (25) };
            _x11KeyFallbackTimer.Tick += (_, _) => {
                if (_unoNativeKeyPipelineConfirmedWorking) {
                    _x11KeyFallbackTimer!.Stop ();
                    _x11KeyFallbackTimer = null;
                    if (_x11KeyListenerDisplay != IntPtr.Zero) {
                        try { _ = XCloseDisplay (_x11KeyListenerDisplay); } catch { }
                        _x11KeyListenerDisplay = IntPtr.Zero;
                    }
                    return;
                }
                PumpX11KeyboardFallback ();
            };
            _x11KeyFallbackTimer.Start ();
        }

        private void PumpX11KeyboardFallback ()
        {
            try {
                if (_x11KeyListenerDisplay == IntPtr.Zero) {
                    var display = XOpenDisplay (IntPtr.Zero);
                    if (display == IntPtr.Zero)
                        return;
                    var title = _window!.Title;
                    if (string.IsNullOrEmpty (title)) {
                        _ = XCloseDisplay (display);
                        return;
                    }
                    var root = XRootWindow (display, XDefaultScreen (display));
                    if (!TryFindTopLevelWindowByTitle (display, root, title, out var xid)) {
                        _ = XCloseDisplay (display);
                        return;   // window not mapped yet; retry on a later tick
                    }
                    const long KeyPressMask = 1 << 0;
                    const long KeyReleaseMask = 1 << 1;
                    _ = XSelectInput (display, xid, KeyPressMask | KeyReleaseMask);
                    _x11KeyListenerDisplay = display;
                }

                while (XPending (_x11KeyListenerDisplay) > 0) {
                    _ = XNextEvent (_x11KeyListenerDisplay, out var xkey);
                    var pressed = xkey.type == 2;   // KeyPress=2, KeyRelease=3
                    if (xkey.type != 2 && xkey.type != 3)
                        continue;

                    var armed = Environment.TickCount64 >= _x11FallbackArmAtTicks;

                    var textBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal (8);
                    try {
                        var byteCount = XLookupString (ref xkey, textBuffer, 8, out var keysym, IntPtr.Zero);
                        var virtualKey = X11KeyTransformVirtualKeyFromKeySym (keysym);
                        var keys = virtualKey is { } vk ? UnoKeyInterop.ToKeys (vk) : Keys.None;

                        if (!armed || keys == Keys.None)
                            continue;

                        if (pressed) {
                            _owner.HandleKeyDown (keys);
                            if (byteCount > 0) {
                                var text = System.Runtime.InteropServices.Marshal.PtrToStringAnsi (textBuffer, byteCount);
                                if (!string.IsNullOrEmpty (text) && !char.IsControl (text[0]))
                                    _owner.HandleTextInput (text);
                            }
                        } else {
                            _owner.HandleKeyUp (keys);
                        }
                    } finally {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal (textBuffer);
                    }
                }
            } catch {
                // Best-effort — see FixX11InputHint's own catch for the same reasoning.
            }
        }

        // X11KeyTransform (public) lives in Uno.WinUI.Runtime.Skia.X11, which Majorsilence.Forms.Uno
        // deliberately doesn't reference at compile time (it would tie this platform-neutral backend
        // library to one specific Skia runtime target) — reached via reflection instead, the same way
        // WireMacOSKeyboard above reaches into the macOS-only internal host. Reuses Uno's own keysym
        // table rather than hand-rolling a second one that could drift from it.
        private static System.Reflection.MethodInfo? s_x11KeyTransformMethod;
        private static bool s_x11KeyTransformResolved;

        private static Windows.System.VirtualKey? X11KeyTransformVirtualKeyFromKeySym (IntPtr keysym)
        {
            if (!s_x11KeyTransformResolved) {
                s_x11KeyTransformResolved = true;
                try {
                    // The type's C# namespace ("Uno.WinUI.Runtime.Skia.X11") and its containing assembly's
                    // name ("Uno.UI.Runtime.Skia.X11", matching the DLL filename) differ — easy to get
                    // wrong (confirmed live: using "Uno.WinUI..." for the assembly half silently fails to
                    // resolve the type, so every keysym translated to Keys.None). Same split naming
                    // WireMacOSKeyboard's own reflection above already accounts for (Uno.UI.Runtime.Skia.MacOS).
                    var type = Type.GetType ("Uno.WinUI.Runtime.Skia.X11.X11KeyTransform, Uno.UI.Runtime.Skia.X11");
                    s_x11KeyTransformMethod = type?.GetMethod ("VirtualKeyFromKeySym", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                } catch { /* leave null; fallback simply won't translate keys on this head */ }
            }
            if (s_x11KeyTransformMethod is null)
                return null;
            try { return (Windows.System.VirtualKey?) s_x11KeyTransformMethod.Invoke (null, new object[] { keysym }); }
            catch { return null; }
        }

        // ── macOS true window geometry ──────────────────────────────────────────────────────────────────
        // On the macOS Skia head AppWindow.Position is unreliable: it stays (0,0) for a window that hasn't
        // been moved programmatically (even though the OS placed it elsewhere), and it reports the AppKit
        // BOTTOM-LEFT origin rather than the top-left origin the rest of Majorsilence.Forms assumes. That
        // breaks cross-window screen hit-testing — e.g. re-attaching a RadTabbedForm tab onto another
        // window's tab strip, where the two windows must agree on a common screen coordinate space.
        //
        // The native NSWindow frame is always accurate, so on macOS we read it directly (via objc) and
        // convert AppKit bottom-left to top-left. The window's NSWindow* is reached by reflecting into Uno's
        // internal MacOSWindowHost (the same host we hook for keyboard above). Non-macOS heads keep using
        // AppWindow.Position, where it works.
        private IntPtr _nsWindowHandle;

        private IntPtr NativeWindowHandle ()
        {
            if (_nsWindowHandle != IntPtr.Zero || _window is null)
                return _nsWindowHandle;
            try {
                var hostType = Type.GetType ("Uno.UI.Runtime.Skia.MacOS.MacOSWindowHost, Uno.UI.Runtime.Skia.MacOS");
                if (hostType is null)
                    return IntPtr.Zero;   // not the macOS head

                var windowsField = hostType.GetField ("_windows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var winField = hostType.GetField ("_winUIWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (windowsField?.GetValue (null) is not System.Collections.IDictionary dict || winField is null)
                    return IntPtr.Zero;

                foreach (var value in dict.Values) {
                    var tryGet = value?.GetType ().GetMethod ("TryGetTarget");
                    if (tryGet is null)
                        continue;
                    var args = new object?[] { null };
                    if (tryGet.Invoke (value, args) is not true || args[0] is not { } host)
                        continue;
                    if (!ReferenceEquals (winField.GetValue (host), _window))
                        continue;

                    var native = host.GetType ().GetField ("_nativeWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue (host);
                    var handle = native?.GetType ().GetProperty ("Handle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue (native);
                    if (handle is IntPtr h && h != IntPtr.Zero)
                        _nsWindowHandle = h;   // cache; NSWindow* is stable for the window's lifetime
                    return _nsWindowHandle;
                }
            } catch {
                // Reflection into the internal host is best-effort; callers fall back to AppWindow.Position.
            }
            return IntPtr.Zero;
        }

        // The window's true top-left screen position (logical points), from the native NSWindow frame.
        private bool TryGetNativeTopLeft (out Point topLeft)
        {
            topLeft = default;
            var h = NativeWindowHandle ();
            if (h == IntPtr.Zero)
                return false;
            var frame = ObjcRect (h, "frame");
            var screenH = PrimaryScreenHeight ();
            if (frame.W <= 0 || frame.H <= 0 || screenH <= 0)
                return false;
            // AppKit frames are bottom-left origin: top-left Y = screenHeight - (frame.Y + frame.Height).
            topLeft = new Point ((int) Math.Round (frame.X), (int) Math.Round (screenH - frame.Y - frame.H));
            return true;
        }

        // Moves the window so its top-left lands at the given screen point (inverse of TryGetNativeTopLeft).
        private bool TryMoveNativeTopLeft (Point topLeft)
        {
            var h = NativeWindowHandle ();
            if (h == IntPtr.Zero)
                return false;
            var frame = ObjcRect (h, "frame");
            var screenH = PrimaryScreenHeight ();
            if (frame.H <= 0 || screenH <= 0)
                return false;
            // AppWindow.Move sets the AppKit bottom-left origin, so convert top-left back to bottom-left.
            var bottomY = screenH - topLeft.Y - frame.H;
            try { _window!.AppWindow?.Move (new Windows.Graphics.PointInt32 { X = topLeft.X, Y = (int) Math.Round (bottomY) }); return true; }
            catch { return false; }
        }

        private static double PrimaryScreenHeight ()
        {
            try { return ObjcRect (ObjcPtr (ObjcGetClass ("NSScreen"), "mainScreen"), "frame").H; }
            catch { return 0; }
        }

        // ── objc interop (macOS only; resolved lazily, no-op on other heads) ──
        [System.Runtime.InteropServices.StructLayout (System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NSRect { public double X, Y, W, H; }
        [System.Runtime.InteropServices.DllImport ("/usr/lib/libobjc.A.dylib", BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern IntPtr sel_registerName ([System.Runtime.InteropServices.MarshalAs (System.Runtime.InteropServices.UnmanagedType.LPStr)] string name);
        [System.Runtime.InteropServices.DllImport ("/usr/lib/libobjc.A.dylib", BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern IntPtr objc_getClass ([System.Runtime.InteropServices.MarshalAs (System.Runtime.InteropServices.UnmanagedType.LPStr)] string name);
        [System.Runtime.InteropServices.DllImport ("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr objc_msgSend_ptr (IntPtr receiver, IntPtr sel);
        [System.Runtime.InteropServices.DllImport ("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern NSRect objc_msgSend_rect (IntPtr receiver, IntPtr sel);
        private static IntPtr ObjcGetClass (string name) => objc_getClass (name);
        private static IntPtr ObjcPtr (IntPtr receiver, string selector) => objc_msgSend_ptr (receiver, sel_registerName (selector));
        private static NSRect ObjcRect (IntPtr receiver, string selector) => objc_msgSend_rect (receiver, sel_registerName (selector));

        private void OnMacKeyDown (object sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (_owner.HandleKeyDown (UnoKeyInterop.ToKeys (e.VirtualKey))) e.Handled = true;
        }

        private void OnMacKeyUp (object sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (_owner.HandleKeyUp (UnoKeyInterop.ToKeys (e.VirtualKey))) e.Handled = true;
        }

        // Give the drawing canvas keyboard focus so KeyDown/KeyUp/character events route to it (Windows/X11).
        // Parity with the Avalonia host: there, every focus request (Focus()/a pointer press) reliably
        // transfers real OS-level keyboard focus, because Avalonia's own X11 window is created with a
        // proper WM_HINTS/WM_TAKE_FOCUS from the start, so the WM always honours it. Uno's X11 window
        // is not (see FixX11InputHint), so a single one-shot fix at Show() time isn't enough — once the
        // user clicks away to another application and back, or the WM's own focus-follows-click state
        // otherwise drifts, WinUI's *logical* focus (_canvas.Focus()) can succeed while the *real* X11
        // window manager focus silently doesn't follow, leaving keyboard input dead until an explicit
        // Alt+Tab. Re-assert the X11-level fix on every focus attempt, not just once, so this host is
        // exactly as robust as Avalonia's about keyboard focus following every click.
        private void TryFocus ()
        {
            try { _canvas.Focus (FocusState.Programmatic); } catch { }
            FixX11InputHint ();
        }

        private void ShowPopup ()
        {
            _canvas.Width = _size.Width;
            _canvas.Height = _size.Height;

            var xamlRoot = _parentHost?.CanvasXamlRoot;
            if (xamlRoot is not null)
                _popup!.XamlRoot = xamlRoot;

            // _location is physical, relative to the parent window's origin (Control.PointToScreen adds
            // the parent window position, which is (0,0) on Uno macOS). Popup offsets are logical and
            // window-relative, so subtract the parent's reported position and divide by scaling.
            var scaling = _parentHost?.HostScaling ?? 1.0;
            if (scaling <= 0) scaling = 1.0;
            var parentPos = _parentHost?.Location ?? Point.Empty;

            _popup!.HorizontalOffset = (_location.X - parentPos.X) / scaling;
            _popup!.VerticalOffset = (_location.Y - parentPos.Y) / scaling;

            _popup!.IsOpen = true;
            _owner.OnBackendActivated ();
            TryFocus ();
            _canvas.Invalidate ();
        }

        public void ShowDialog (IWindowBackend? owner) => Show ();

        public void Hide ()
        {
            if (PopupMode) {
                _popup!.IsOpen = false;
                return;
            }
            try { _window!.AppWindow?.Hide (); } catch { }
        }

        public void Close ()
        {
            if (PopupMode) {
                _popup!.IsOpen = false;
                _owner.OnBackendClosed ();
                return;
            }
            _window!.Close ();
        }

        public void Activate ()
        {
            if (PopupMode)
                _owner.OnBackendActivated ();
            else
                _window!.Activate ();
        }

        /// <summary>
        /// Whether Show() also activates. WinUI's Window.Activate is the only way to show a window here,
        /// so a non-activating window is honoured by skipping the Activate call rather than by a flag.
        /// </summary>
        public bool ShowActivated { get; set; } = true;

        private void WireLifecycle ()
        {
            _window!.Activated += (_, e) => {
                if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
                    _owner.OnBackendDeactivated ();
                else
                    _owner.OnBackendActivated ();
            };

            _window!.Closed += (_, _) => { CloseX11FocusDisplay (); CloseX11KeyboardFallback (); _owner.OnBackendClosed (); };
        }

        // ── Appearance / behaviour ──
        public string Title { set { try { if (_window is not null) _window.Title = value; } catch { } } }
        public bool Topmost { get; set; }

        public void SetSystemDecorations (bool useSystemDecorations)
        {
            _systemDecorations = useSystemDecorations;
            ApplyDecorations ();
        }

        // Borderless when system decorations are off (top-level windows only; popups are chromeless overlays).
        private void ApplyDecorations ()
        {
            if (PopupMode)
                return;
            try {
                if (Presenter is { } p) {
                    // A borderless OverlappedPresenter keeps OS resize margins, so even with self-drawn
                    // chrome the window stays resizable — gate it on CanResize, never force it off.
                    p.IsResizable = _canResize;
                    p.IsMaximizable = _canResize;
                    p.IsMinimizable = true;
                    p.SetBorderAndTitleBar (_systemDecorations, _systemDecorations);
                }
                _window!.ExtendsContentIntoTitleBar = !_systemDecorations;
            } catch { /* presenter not available on every target */ }
        }

        public void SetCursor (CursorType cursor) => _canvas.SetCursorShape (UnoKeyInterop.ToCursorShape (cursor));

        public void SetIcon (byte[]? iconPng)
        {
            if (PopupMode)
                return;
            try {
                if (iconPng is null || iconPng.Length == 0)
                    return;

                var path = System.IO.Path.Combine (System.IO.Path.GetTempPath (), $"mf-uno-icon-{Guid.NewGuid ():N}.png");
                System.IO.File.WriteAllBytes (path, iconPng);
                _window!.AppWindow?.SetIcon (path);
            } catch { /* icon is best-effort */ }
        }

        public Size MinimumSize { set { } }
        public Size MaximumSize { set { } }
        private bool _canResize = true;
        public bool CanResize { get => _canResize; set { _canResize = value; ApplyDecorations (); } }
        public bool ShowInTaskbar { get; set; } = true;
        public double Opacity { get; set; } = 1.0;

        // Was a plain auto-property with no side effect at all: setting WindowState = Maximized (e.g.
        // clicking Majorsilence.Forms' own custom-drawn maximize button) stored the value and did
        // nothing else. Tried forwarding to the AppWindow's OverlappedPresenter next (Maximize/
        // Minimize/Restore/State), matching what the Avalonia backend does with its own native
        // Window.WindowState — but on this X11 Skia head OverlappedPresenter.Maximize() is a no-op:
        // State reads back as Restored immediately after calling it, even well after the window is
        // shown and activated. Whatever visual "maximize" a caller might have observed before this
        // fix must have come from something else assigning Size/Location directly, which — being a
        // real bounds change flush against the screen edges rather than an OS-recognized maximized
        // state — leaves no margin for the OS's own edge-resize handling to grab, matching the
        // "maximized but can't resize" symptom.
        //
        // So: track state ourselves and drive maximize/restore purely through Size/Location against
        // the display's work area, the same way MdiChildWindow.Maximize/Restore already do for MDI
        // children (RestoreBounds captured before maximizing, reapplied on restore) — not through the
        // presenter at all. Still tell the presenter about Minimize/Restore best-effort, since that
        // one wasn't observed broken and Windows/macOS heads may genuinely need it.
        private FormWindowState _windowState = FormWindowState.Normal;
        private Rectangle? _restoreBounds;

        public FormWindowState WindowState {
            get => _windowState;
            set {
                if (PopupMode || _window is null)
                    return;
                // Deliberately NOT gated on "_windowState == value already" for the Maximized case: an
                // early call (e.g. RdlDesigner requesting Maximized from its own constructor, before
                // Show()/Activate() have run) can find no work area yet and be a no-op on the bounds
                // below, but it still recorded _windowState = Maximized on the way out. A second,
                // later call requesting the same state (e.g. right after Show()) must not be treated
                // as "no-op, already there" and skipped — it's the one actually capable of succeeding.
                if (value == FormWindowState.Normal && _windowState == value)
                    return;

                if (value == FormWindowState.Maximized) {
                    if (_windowState != FormWindowState.Maximized || _restoreBounds is null)
                        _restoreBounds = new Rectangle (Location, _size);
                    if (GetWorkAreaBounds () is { } workArea) {
                        TryMove (workArea.Location);
                        TryResize (workArea.Size);
                    }
                } else if (value == FormWindowState.Normal && _restoreBounds is { } bounds) {
                    TryMove (bounds.Location);
                    TryResize (bounds.Size);
                    _restoreBounds = null;
                    // Our maximize above never asked the WM to actually maximize (see the comment on
                    // this property) — it only pushed bounds to fill the work area — but on GNOME/
                    // mutter the WM can still end up tracking the window as _NET_WM_STATE_MAXIMIZED_*
                    // (e.g. because those bounds happen to exactly fill the work area, or from an
                    // edge-snap gesture). A window flagged maximized in the WM's own state is one
                    // mutter refuses to interactively RESIZE via _NET_WM_MOVERESIZE (it still allows
                    // MOVE, which is its "drag the title bar to unmaximize" escape hatch) — so leaving
                    // that flag set here silently breaks drag-to-resize after any maximize/restore
                    // cycle. Clear it explicitly so the WM's state matches ours.
                    ClearWmMaximizedState ();
                }

                try {
                    if (Presenter is { } p) {
                        switch (value) {
                            case FormWindowState.Minimized: p.Minimize (); break;
                            case FormWindowState.Normal: p.Restore (); break;
                        }
                    }
                } catch {
                    // Best-effort; the bounds-based transition above is what actually matters here.
                }

                _windowState = value;
            }
        }

        // The active display's usable area (excludes taskbars/docks/panels), in the same units
        // TryMove/TryResize already expect (see their own comments: Location is used as-is, Size is
        // logical and TryResize itself multiplies by Scaling). Uses Majorsilence.Forms' own
        // backend-neutral Screen API (UnoPlatformBackend.GetScreens()) rather than
        // Microsoft.UI.Windowing.DisplayArea.GetFromWindowId, which throws NotImplementedException on
        // this Uno version's X11 Skia head (verified at runtime) — Screen.WorkingArea comes back in
        // physical pixels from the same source, so only its Size half needs dividing down here.
        private Rectangle? GetWorkAreaBounds ()
        {
            try {
                var screen = Majorsilence.Forms.Screen.PrimaryScreen;
                if (screen is null)
                    return null;
                var wa = screen.WorkingArea;
                var s = Scaling;
                if (s <= 0) s = 1.0;
                return new Rectangle (wa.X, wa.Y, (int) (wa.Width / s), (int) (wa.Height / s));
            } catch {
                return null;
            }
        }

        public bool Enabled { get; set; } = true;

        // ── Coordinate conversion ──
        // screen coords are physical, window-relative (parent position is (0,0) on Uno macOS); client logical.
        public Point PointToClient (Point screen)
        {
            var pos = Location;
            var s = Scaling;
            return new Point ((int) ((screen.X - pos.X) / s), (int) ((screen.Y - pos.Y) / s));
        }

        public Point PointToScreen (Point client)
        {
            var pos = Location;
            var s = Scaling;
            return new Point (pos.X + (int) (client.X * s), pos.Y + (int) (client.Y * s));
        }

        // ── Drag (custom chrome) ──
        // WinUI/Uno has no "begin drag from code" API, so these were no-ops, relying on the
        // borderless OverlappedPresenter's retained resize margins to make edge-resize work passively
        // (see ApplyDecorations) and on declarative caption regions for title-bar drag (see
        // SetCaptionRegions). Neither actually functions on this Uno version's X11 Skia head: caption
        // regions are documented Windows-desktop-only (a no-op, caught, on X11 — see
        // ApplyCaptionRegions), and OverlappedPresenter has already been found unreliable here for
        // basic state transitions (see WindowState/GetWorkAreaBounds). With both mechanisms
        // ineffective, Majorsilence.Forms' own border hit-testing (Form.cs) calls these two methods
        // and nothing happened — the window could never be moved or resized by dragging.
        //
        // Implement both directly via the standard EWMH _NET_WM_MOVERESIZE client message instead:
        // the same protocol GTK/Qt/Chromium use for interactive move/resize on borderless windows.
        // The window manager takes over the pointer grab itself once it receives this, so it works
        // regardless of what OverlappedPresenter does or doesn't support.
        public void BeginMoveDrag () => BeginWmMoveResize (8 /* _NET_WM_MOVERESIZE_MOVE */);

        public void BeginResizeDrag (WindowEdge edge) => BeginWmMoveResize (edge switch {
            WindowEdge.NorthWest => 0,
            WindowEdge.North => 1,
            WindowEdge.NorthEast => 2,
            WindowEdge.East => 3,
            WindowEdge.SouthEast => 4,
            WindowEdge.South => 5,
            WindowEdge.SouthWest => 6,
            WindowEdge.West => 7,
            _ => 8,
        });

        private void BeginWmMoveResize (int direction)
        {
            if (!OperatingSystem.IsLinux () || _window is null)
                return;
            var title = _window.Title;
            if (string.IsNullOrEmpty (title))
                return;

            // The press that led here already gave the canvas its own WinUI-level pointer capture
            // (see WireInput's PointerPressed); the WM is about to grab the pointer itself for the
            // interactive move/resize, and a lingering client-side capture would fight it.
            try { _canvas.ReleasePointerCaptures (); } catch { }

            try {
                var display = XOpenDisplay (IntPtr.Zero);
                if (display == IntPtr.Zero)
                    return;
                try {
                    var root = XRootWindow (display, XDefaultScreen (display));
                    if (!TryFindTopLevelWindowByTitle (display, root, title, out var xid))
                        return;

                    // See ClearWmMaximizedState: a window the WM still tracks as
                    // _NET_WM_STATE_MAXIMIZED_* refuses interactive resize even though the
                    // _NET_WM_MOVERESIZE message itself is delivered successfully. Clear it
                    // unconditionally before every drag (move or resize) so a stray maximized flag —
                    // however it got set — never silently blocks the resize that follows.
                    SendClearMaximizedState (display, root, xid);

                    var qp = XQueryPointer (display, root, out _, out _, out var rootX, out var rootY, out _, out _, out _);
                    if (qp == 0)
                        return;

                    var messageType = XInternAtom (display, "_NET_WM_MOVERESIZE", false);
                    var ev = new XClientMessageEvent {
                        type = 33, // ClientMessage
                        window = xid,
                        message_type = messageType,
                        format = 32,
                        l0 = rootX,
                        l1 = rootY,
                        l2 = direction,
                        l3 = 1, // button 1 (left)
                        l4 = 1, // source indication: normal application
                    };

                    const long SubstructureRedirectMask = 1 << 20;
                    const long SubstructureNotifyMask = 1 << 19;
                    _ = XSendEvent (display, root, 0, checked ((IntPtr) (SubstructureRedirectMask | SubstructureNotifyMask)), ref ev);
                    _ = XFlush (display);
                } finally {
                    _ = XCloseDisplay (display);
                }
            } catch {
                // Best-effort: worst case the window just doesn't move/resize for this one drag.
            }
        }

        [System.Runtime.InteropServices.StructLayout (System.Runtime.InteropServices.LayoutKind.Explicit, Size = 192)]
        private struct XClientMessageEvent
        {
            [System.Runtime.InteropServices.FieldOffset (0)] public int type;
            [System.Runtime.InteropServices.FieldOffset (8)] public IntPtr serial;
            [System.Runtime.InteropServices.FieldOffset (16)] public int send_event;
            [System.Runtime.InteropServices.FieldOffset (24)] public IntPtr display;
            [System.Runtime.InteropServices.FieldOffset (32)] public IntPtr window;
            [System.Runtime.InteropServices.FieldOffset (40)] public IntPtr message_type;
            [System.Runtime.InteropServices.FieldOffset (48)] public int format;
            [System.Runtime.InteropServices.FieldOffset (56)] public long l0;
            [System.Runtime.InteropServices.FieldOffset (64)] public long l1;
            [System.Runtime.InteropServices.FieldOffset (72)] public long l2;
            [System.Runtime.InteropServices.FieldOffset (80)] public long l3;
            [System.Runtime.InteropServices.FieldOffset (88)] public long l4;
        }

        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XQueryPointer (IntPtr display, IntPtr w, out IntPtr rootReturn, out IntPtr childReturn, out int rootXReturn, out int rootYReturn, out int winXReturn, out int winYReturn, out uint maskReturn);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6", BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern IntPtr XInternAtom (IntPtr display, [System.Runtime.InteropServices.MarshalAs (System.Runtime.InteropServices.UnmanagedType.LPStr)] string atomName, bool onlyIfExists);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XSendEvent (IntPtr display, IntPtr w, int propagate, IntPtr eventMask, ref XClientMessageEvent eventSend);

        // Same verified field layout/offsets as XClientMessageEvent above (both are members of the same
        // 192-byte native XEvent union; the type/serial/send_event/display/window prefix is identical —
        // only what follows differs per event type).
        [System.Runtime.InteropServices.StructLayout (System.Runtime.InteropServices.LayoutKind.Explicit, Size = 192)]
        private struct XKeyEvent
        {
            [System.Runtime.InteropServices.FieldOffset (0)] public int type;
            [System.Runtime.InteropServices.FieldOffset (8)] public IntPtr serial;
            [System.Runtime.InteropServices.FieldOffset (16)] public int send_event;
            [System.Runtime.InteropServices.FieldOffset (24)] public IntPtr display;
            [System.Runtime.InteropServices.FieldOffset (32)] public IntPtr window;
            [System.Runtime.InteropServices.FieldOffset (40)] public IntPtr root;
            [System.Runtime.InteropServices.FieldOffset (48)] public IntPtr subwindow;
            [System.Runtime.InteropServices.FieldOffset (56)] public IntPtr time;
            [System.Runtime.InteropServices.FieldOffset (64)] public int x;
            [System.Runtime.InteropServices.FieldOffset (68)] public int y;
            [System.Runtime.InteropServices.FieldOffset (72)] public int x_root;
            [System.Runtime.InteropServices.FieldOffset (76)] public int y_root;
            [System.Runtime.InteropServices.FieldOffset (80)] public uint state;
            [System.Runtime.InteropServices.FieldOffset (84)] public uint keycode;
            [System.Runtime.InteropServices.FieldOffset (88)] public int same_screen;
        }

        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XSelectInput (IntPtr display, IntPtr w, long eventMask);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XPending (IntPtr display);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XNextEvent (IntPtr display, out XKeyEvent eventReturn);
        [System.Runtime.InteropServices.DllImport ("libX11.so.6")] private static extern int XLookupString (ref XKeyEvent eventStruct, IntPtr bufferReturn, int bytesBuffer, out IntPtr keysymReturn, IntPtr statusInOut);

        // Opens its own display / re-finds the window by title — used from WindowState's restore path,
        // which doesn't already have a display handle open (unlike BeginWmMoveResize, which calls
        // SendClearMaximizedState directly with the display/root/xid it already has). See the callers'
        // comments for why this needs to happen at all.
        private void ClearWmMaximizedState ()
        {
            if (!OperatingSystem.IsLinux () || _window is null)
                return;
            var title = _window.Title;
            if (string.IsNullOrEmpty (title))
                return;
            try {
                var display = XOpenDisplay (IntPtr.Zero);
                if (display == IntPtr.Zero)
                    return;
                try {
                    var root = XRootWindow (display, XDefaultScreen (display));
                    if (TryFindTopLevelWindowByTitle (display, root, title, out var xid))
                        SendClearMaximizedState (display, root, xid);
                } finally {
                    _ = XCloseDisplay (display);
                }
            } catch {
                // Best-effort — worst case the WM keeps thinking the window is maximized and a later
                // resize drag silently no-ops, same as before this fix existed.
            }
        }

        // Sends an EWMH _NET_WM_STATE client message asking the WM to drop both maximize axes. Needed
        // because our maximize/restore (see WindowState) drives bounds directly rather than asking the
        // WM to maximize, but the WM can still end up tracking _NET_WM_STATE_MAXIMIZED_* on its own
        // (e.g. our bounds happen to fill the work area, or a prior edge-snap gesture) — and a window
        // in that state refuses interactive RESIZE via _NET_WM_MOVERESIZE on mutter, even though the
        // move direction and the message delivery itself both succeed regardless of this flag.
        private static void SendClearMaximizedState (IntPtr display, IntPtr root, IntPtr xid)
        {
            var stateAtom = XInternAtom (display, "_NET_WM_STATE", false);
            var horzAtom = XInternAtom (display, "_NET_WM_STATE_MAXIMIZED_HORZ", false);
            var vertAtom = XInternAtom (display, "_NET_WM_STATE_MAXIMIZED_VERT", false);

            var ev = new XClientMessageEvent {
                type = 33, // ClientMessage
                window = xid,
                message_type = stateAtom,
                format = 32,
                l0 = 0, // _NET_WM_STATE_REMOVE
                l1 = (long) horzAtom,
                l2 = (long) vertAtom,
                l3 = 1, // source indication: normal application
            };

            const long SubstructureRedirectMask = 1 << 20;
            const long SubstructureNotifyMask = 1 << 19;
            _ = XSendEvent (display, root, 0, checked ((IntPtr) (SubstructureRedirectMask | SubstructureNotifyMask)), ref ev);
            _ = XFlush (display);
        }

        // The Form's draggable title-bar region(s) in logical, window-relative pixels (see SetCaptionRegions).
        private System.Collections.Generic.IReadOnlyList<Rectangle> _captionRects = System.Array.Empty<Rectangle> ();

        public void SetCaptionRegions (System.Collections.Generic.IReadOnlyList<Rectangle> captionRects)
        {
            _captionRects = captionRects;
            ApplyCaptionRegions ();
        }

        // Declares the caption regions to the OS so it handles window drag and Snap Layouts over them.
        // Supported on the Windows-desktop / WinAppSDK heads only; a no-op (caught) on X11/macOS.
        private void ApplyCaptionRegions ()
        {
            if (PopupMode || _window is null)
                return;
            try {
                var windowId = _window.AppWindow?.Id;
                if (windowId is null)
                    return;

                var source = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId (windowId.Value);

                // Region rects are physical pixels (not scaled points) and must track size/DPI changes —
                // the Form re-declares them on every resize via OnClientLayoutChanged.
                var scaling = Scaling;
                if (scaling <= 0) scaling = 1.0;

                var rects = new Windows.Graphics.RectInt32[_captionRects.Count];
                for (var i = 0; i < _captionRects.Count; i++) {
                    var r = _captionRects[i];
                    rects[i] = new Windows.Graphics.RectInt32 {
                        X = (int) (r.X * scaling), Y = (int) (r.Y * scaling),
                        Width = (int) (r.Width * scaling), Height = (int) (r.Height * scaling)
                    };
                }

                source.SetRegionRects (Microsoft.UI.Input.NonClientRegionKind.Caption, rects);
            } catch {
                // InputNonClientPointerSource is unavailable on non-Windows-desktop heads; OS drag isn't
                // offered there (custom-chrome windows simply aren't draggable, as before).
            }
        }

        // ── INativeControlHostBackend (native Uno UIElements hosted inside the Majorsilence scene) ────────
        public void AttachNativeControl (Majorsilence.Forms.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not Microsoft.UI.Xaml.UIElement element)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, element))
                _root.Children.Remove (existing);

            _overlays[host] = element;
            if (!_root.Children.Contains (element))
                _root.Children.Add (element);
        }

        public void UpdateNativeControl (Majorsilence.Forms.NativeControlHost host, System.Drawing.Rectangle logicalBounds, System.Drawing.Rectangle clipBounds, bool visible)
        {
            if (!_overlays.TryGetValue (host, out var element) || element is not Microsoft.UI.Xaml.FrameworkElement fe)
                return;

            fe.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            fe.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top;
            fe.Margin = new Microsoft.UI.Xaml.Thickness (logicalBounds.X, logicalBounds.Y, 0, 0);
            fe.Width = logicalBounds.Width;
            fe.Height = logicalBounds.Height;
            fe.Visibility = visible ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

            // Clip to the visible viewport (local to the element). Null when fully visible.
            fe.Clip = clipBounds == logicalBounds
                ? null
                : new Microsoft.UI.Xaml.Media.RectangleGeometry {
                    Rect = new Windows.Foundation.Rect (
                        clipBounds.X - logicalBounds.X, clipBounds.Y - logicalBounds.Y,
                        clipBounds.Width, clipBounds.Height)
                };
        }

        public void DetachNativeControl (Majorsilence.Forms.NativeControlHost host)
        {
            if (_overlays.Remove (host, out var element))
                _root.Children.Remove (element);
        }

        // ── File/folder pickers (top-level windows only) ──
        public async Task<string[]> ShowOpenFileDialog (OpenFileRequest request)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker ();
            ApplyFilters (picker.FileTypeFilter, request.Filters);
            InitializeWithWindow (picker);

            if (request.AllowMultiple) {
                var files = await picker.PickMultipleFilesAsync ();
                var paths = new System.Collections.Generic.List<string> ();
                foreach (var f in files)
                    if (f is not null) paths.Add (f.Path);
                return paths.ToArray ();
            }

            var file = await picker.PickSingleFileAsync ();
            return file is null ? Array.Empty<string> () : new[] { file.Path };
        }

        public async Task<string?> ShowSaveFileDialog (SaveFileRequest request)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker ();
            if (!string.IsNullOrEmpty (request.SuggestedFileName))
                picker.SuggestedFileName = request.SuggestedFileName;
            InitializeWithWindow (picker);

            var file = await picker.PickSaveFileAsync ();
            return file?.Path;
        }

        public async Task<string?> ShowOpenFolderDialog (FolderDialogRequest request)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker ();
            picker.FileTypeFilter.Add ("*");
            InitializeWithWindow (picker);

            var folder = await picker.PickSingleFolderAsync ();
            return folder?.Path;
        }

        private static void ApplyFilters (System.Collections.Generic.IList<string> target, System.Collections.Generic.IReadOnlyList<FileDialogFilter> filters)
        {
            foreach (var filter in filters)
                foreach (var pattern in filter.Patterns) {
                    var ext = pattern.StartsWith ("*.", StringComparison.Ordinal) ? pattern[1..] : pattern;
                    if (!target.Contains (ext))
                        target.Add (ext);
                }

            if (target.Count == 0)
                target.Add ("*");
        }

        private void InitializeWithWindow (object picker)
        {
            if (_window is null)
                return;
            try {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle (_window);
                WinRT.Interop.InitializeWithWindow.Initialize (picker, hwnd);
            } catch { /* not required / unsupported on some Skia desktop targets */ }
        }

        // ── Rendering ──
        // On Uno's macOS Skia head SKXamlCanvas.Invalidate() paints synchronously, so an Invalidate()
        // raised *during* a paint (controls routinely invalidate while rendering — e.g. TextBox updating
        // its scrollbars) would re-enter OnPaintSurface and recurse until the stack overflows. Coalesce
        // any such re-entrant invalidations into a single repaint scheduled on the next dispatcher tick,
        // matching the async-invalidation behaviour the Avalonia backend gets for free.
        private bool _painting;
        private bool _invalidatePending;
        private bool _paintPending;

        public void Invalidate ()
        {
            if (_painting) {
                _invalidatePending = true;
                return;
            }
            if (_paintPending)
                return;
            _paintPending = true;
            var dq = _canvas.DispatcherQueue;
            if (dq is null) {
                _paintPending = false;
                _canvas.Invalidate ();
                return;
            }
            dq.TryEnqueue (() => {
                _paintPending = false;
                _canvas.Invalidate ();
            });
        }

        private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
        {
            var info = e.Info;
            var scaling = Scaling;
            _size = new Size ((int) (info.Width / scaling), (int) (info.Height / scaling));

            _painting = true;
            _invalidatePending = false;
            try {
                _owner.RenderFrame (e.Surface.Canvas, info.Width, info.Height, scaling);
            } finally {
                _painting = false;
            }

            // A control changed something that needs redrawing while we were painting. Route through
            // our deferred Invalidate() so _paintPending coalescing still applies.
            if (_invalidatePending)
                Invalidate ();
        }

        // ── Input ──
        private long _lastPointerRightTicks;
        private bool _pointerCaptured;

        private void WireInput ()
        {
            // WinUI/Uno does NOT implicitly capture the pointer on press (unlike Avalonia, which routes all
            // move/release events to the pressed control until release). Without capture, the canvas stops
            // receiving PointerMoved/Released the instant the pointer leaves its bounds — which breaks any
            // press-drag-release gesture that wanders outside the window, e.g. tearing a RadTabbedForm tab
            // off (drag below the strip) or re-attaching it onto another window (release over a different
            // form). Capture on press / release on up so the managed Control.Capture path actually holds the
            // pointer for the whole gesture and the terminating mouse-up is always delivered here.
            _canvas.PointerPressed += (_, e) => {
                TryFocus ();
                _pointerCaptured = _canvas.CapturePointer (e.Pointer);
                DispatchPointer (e, _owner.HandlePointerPressed);
            };
            _canvas.PointerReleased += (_, e) => {
                var button = DispatchPointer (e, _owner.HandlePointerReleased);
                if (_pointerCaptured) {
                    _pointerCaptured = false;
                    _canvas.ReleasePointerCapture (e.Pointer);
                }
                if (button == MouseButtons.Right)
                    _lastPointerRightTicks = Environment.TickCount64;
            };
            _canvas.PointerMoved += (_, e) => DispatchPointer (e, _owner.HandlePointerMoved);
            _canvas.PointerExited += (_, e) => DispatchPointer (e, _owner.HandlePointerExited);
            // Mouse wheel AND trackpad two-finger scroll both arrive as PointerWheelChanged in WinUI —
            // there's no separate touch-scroll event to also wire up. This was never hooked up on the
            // main window host at all (only MajorsilenceFormsPresenter, the embedding host, had it),
            // so ScrollBars/ScrollableControl never saw any wheel or two-finger-scroll input here.
            _canvas.PointerWheelChanged += (_, e) => DispatchWheel (e);

            // If the OS yanks the capture mid-drag (focus change, system gesture) PointerReleased may never
            // arrive. Synthesize a release at the last position so the in-progress drag (tab reorder /
            // tear-off) completes instead of wedging with Control.Capture stuck on.
            _canvas.PointerCaptureLost += (_, e) => {
                if (!_pointerCaptured)
                    return;   // a normal release already cleared capture
                _pointerCaptured = false;
                DispatchPointer (e, _owner.HandlePointerReleased);
            };

            // Routed key events — the standard path on the Windows/X11 Skia heads. (On the macOS head these
            // never fire for the canvas; WireMacOSKeyboard hooks the native host's KeyDown there instead.)
            // Confirmed broken on at least one real environment (GNOME/mutter, Wayland session, XWayland
            // X11 backend): the WM genuinely grants this window real keyboard focus (_NET_WM_STATE_FOCUSED
            // set, verified) and real X11 KeyPress/KeyRelease events are independently confirmed to reach
            // this window, yet these routed handlers never fire — a bug/incompatibility inside
            // Uno.WinUI.Runtime.Skia.X11's own event bridge, not anything fixable from here. WireX11KeyboardFallback
            // below detects that (this handler firing is the "native pipeline works" signal it watches for)
            // and takes over reading raw X11 key events directly when it doesn't.
            _canvas.AddHandler (UIElement.KeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler ((_, e) => {
                    _unoNativeKeyPipelineConfirmedWorking = true;
                    if (!e.Handled && _owner.HandleKeyDown (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true;
                    // CharacterReceived (below) never fires on this head, so typed text is carried off
                    // the KeyDown itself — see UnoKeyInterop.TryGetTypedCharacter.
                    if (!e.Handled && UnoKeyInterop.TryGetTypedCharacter (e, out var ch) && _owner.HandleTextInput (ch.ToString ())) e.Handled = true;
                }), handledEventsToo: true);
            _canvas.AddHandler (UIElement.KeyUpEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler ((_, e) => {
                    _unoNativeKeyPipelineConfirmedWorking = true;
                    if (!e.Handled && _owner.HandleKeyUp (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true;
                }), handledEventsToo: true);
            // Kept in case a future Uno.WinUI.Runtime.Skia release implements CharacterReceivedEvent —
            // the KeyDown path above already covers text input either way, so this stays inert today.
            _canvas.CharacterReceived += (_, e) => { if (_owner.HandleTextInput (e.Character.ToString ())) e.Handled = true; };

            // The canonical context-menu trigger: covers right-click, macOS two-finger/Ctrl secondary
            // click, touch long-press and the keyboard menu key — many of which don't arrive as a
            // pointer right-button on macOS. Synthesize a right-click so Control.OnClick opens the menu.
            _canvas.ContextRequested += OnContextRequested;
        }

        private void OnContextRequested (UIElement sender, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs e)
        {
            // Skip if a real pointer right-release just handled this same gesture (avoid a double-open).
            if (Environment.TickCount64 - _lastPointerRightTicks < 300) {
                e.Handled = true;
                return;
            }

            var scaling = Scaling;
            int x, y;
            if (e.TryGetPosition (_canvas, out var pos)) {
                x = (int) (pos.X * scaling);
                y = (int) (pos.Y * scaling);
            } else {
                return; // no position (rare); let default handling proceed
            }

            _owner.HandlePointerPressed (MouseButtons.Right, x, y, Keys.None);
            _owner.HandlePointerReleased (MouseButtons.Right, x, y, Keys.None);
            e.Handled = true;
        }

        private delegate void PointerAction (MouseButtons button, int x, int y, Keys keys);

        private MouseButtons DispatchPointer (PointerRoutedEventArgs e, PointerAction action)
        {
            var scaling = Scaling;
            var point = e.GetCurrentPoint (_canvas);
            var x = (int) (point.Position.X * scaling);
            var y = (int) (point.Position.Y * scaling);
            var button = UnoKeyInterop.ToButton (point.Properties);
            action (button, x, y, Keys.None);
            return button;
        }

        private void DispatchWheel (PointerRoutedEventArgs e)
        {
            var scaling = Scaling;
            var point = e.GetCurrentPoint (_canvas);
            var x = (int) (point.Position.X * scaling);
            var y = (int) (point.Position.Y * scaling);
            // WinUI reports one scalar delta per event plus IsHorizontalMouseWheel to say which axis it's
            // on (unlike Avalonia's Vector Delta, which carries both axes on every event) — a trackpad's
            // horizontal two-finger swipe arrives as a separate IsHorizontalMouseWheel=true event, not as
            // an X component alongside a vertical one.
            var notches = NotchesFromWheelDelta (point.Properties.MouseWheelDelta);
            var delta = point.Properties.IsHorizontalMouseWheel
                ? new Point (notches, 0)
                : new Point (0, notches);
            _owner.HandlePointerWheel (MouseButtons.None, x, y, delta, Keys.None);
        }

        // WinUI's PointerPointProperties.MouseWheelDelta follows the Win32 WHEEL_DELTA convention: ±120
        // per full wheel "notch". Majorsilence.Forms' own ScrollBar/ScrollableControl wheel handling
        // multiplies the delta directly by SmallChange (see ScrollBar.OnMouseWheel) expecting a small
        // "how many notches" count, the same shape Avalonia's own (pre-normalized) PointerWheelEventArgs
        // .Delta already provides there — passing WHEEL_DELTA-scaled values straight through un-divided
        // turned a single wheel/trackpad tick into a 120x-too-large jump (confirmed live: one scroll flew
        // from top to bottom). A trackpad's continuous two-finger scroll reports many small per-event
        // deltas rather than one full ±120 per gesture, so round instead of truncating (a truncating /120
        // would silently drop every one of those as zero), and fall back to ±1 for any nonzero delta that
        // still rounds to 0 so gentle scrolling doesn't just do nothing.
        private static int NotchesFromWheelDelta (int rawDelta)
        {
            const int WheelDeltaPerNotch = 120;
            if (rawDelta == 0)
                return 0;
            var notches = (int) Math.Round (rawDelta / (double) WheelDeltaPerNotch, MidpointRounding.AwayFromZero);
            return notches != 0 ? notches : Math.Sign (rawDelta);
        }

        // SKXamlCanvas with a settable cursor (UIElement.ProtectedCursor is protected).
        private sealed class CursorCanvas : SKXamlCanvas
        {
            public CursorCanvas ()
            {
                // Focusable so it can receive keyboard input; no focus rectangle around the drawing surface.
                IsTabStop = true;
                UseSystemFocusVisuals = false;
                // Stop the XAML focus manager from eating arrow keys for directional navigation before they
                // surface as KeyDown — Majorsilence.Forms does its own list/tree/arrow navigation.
                XYFocusKeyboardNavigation = Microsoft.UI.Xaml.Input.XYFocusKeyboardNavigationMode.Disabled;
            }

            public void SetCursorShape (Microsoft.UI.Input.InputSystemCursorShape shape)
            {
                try { ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create (shape); } catch { }
            }
        }
    }
}
