using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Backends;
// Both Majorsilence.Forms.Automation and System.Windows.Automation declare an AutomationElement;
// in this file the bare name always means the Majorsilence tree node.
using MfAutomationElement = Majorsilence.Forms.Automation.AutomationElement;

namespace Majorsilence.Forms.WindowsUIAutomation
{
    /// <summary>
    /// Bridges one <see cref="WindowBase"/> to Windows UI Automation. It subclasses the host HWND to answer
    /// <c>WM_GETOBJECT</c> with a provider tree built from <see cref="AutomationProvider"/>, resolves tree
    /// nodes on the UI thread, and turns <see cref="AutomationObserver"/> focus/value events into UIA
    /// automation events. One bridge per window; dispose to detach.
    /// </summary>
    internal sealed class UiaBridge : IDisposable
    {
        private static readonly UIntPtr SubclassId = (UIntPtr) 0x4D460A11;  // arbitrary, stable per-process id ('MF'..)

        private readonly WindowBase _window;
        private readonly IntPtr _hwnd;
        private readonly Native.SubclassProc _proc;   // kept alive for the lifetime of the subclass
        private readonly AutomationObserver _observer;
        private bool _disposed;

        public UiaBridge (WindowBase window, IntPtr hwnd)
        {
            _window = window;
            _hwnd = hwnd;
            Root = new ElementProvider (this, Array.Empty<int> ());
            HostProvider = AutomationInteropProvider.HostProviderFromHandle (hwnd);

            _proc = SubclassWndProc;
            Native.SetWindowSubclass (hwnd, _proc, SubclassId, IntPtr.Zero);

            _observer = new AutomationObserver (window);
            _observer.FocusChanged += OnFocusChanged;
            _observer.ValueChanged += OnValueChanged;
        }

        /// <summary>The window-root provider (fragment root).</summary>
        public ElementProvider Root { get; }

        /// <summary>The HWND host provider the root reports as its <c>HostRawElementProvider</c>.</summary>
        public IRawElementProviderSimple HostProvider { get; }

        // ── WM_GETOBJECT interception ──────────────────────────────────────────────

        private IntPtr SubclassWndProc (IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
        {
            // Only hijack the UI Automation request; chain everything else (incl. MSAA) to the backend.
            // Compare in pointer width (lParam is sign-extended) to dodge the IntPtr->int narrowing (CA2020).
            if (msg == Native.WM_GETOBJECT && lParam == (IntPtr) Native.UiaRootObjectId)
                return AutomationInteropProvider.ReturnRawElementProvider (hWnd, wParam, lParam, Root);

            return Native.DefSubclassProc (hWnd, msg, wParam, lParam);
        }

        // ── Tree resolution (all reads marshalled to the UI thread; pure walks live in UiaTree) ────

        public MfAutomationElement? Resolve (int[] path) => OnUi (() => UiaTree.Follow (AutomationProvider.BuildTree (_window), path));

        public Rect ScreenRect (int[] path) => OnUi (() => {
            var el = UiaTree.Follow (AutomationProvider.BuildTree (_window), path);
            if (el == null)
                return Rect.Empty;
            var tl = _window.PointToScreen (el.Bounds.Location);   // logical client → physical screen px
            var s = _window.Scaling;
            return new Rect (tl.X, tl.Y, el.Bounds.Width * s, el.Bounds.Height * s);
        });

        // ── Navigation ─────────────────────────────────────────────────────────────

        public IRawElementProviderFragment? Navigate (int[] path, NavigateDirection direction)
        {
            var target = OnUi (() => {
                var root = AutomationProvider.BuildTree (_window);
                return direction switch {
                    NavigateDirection.Parent => UiaTree.Parent (path),
                    NavigateDirection.FirstChild => UiaTree.FirstChild (root, path),
                    NavigateDirection.LastChild => UiaTree.LastChild (root, path),
                    NavigateDirection.NextSibling => UiaTree.NextSibling (root, path),
                    NavigateDirection.PreviousSibling => UiaTree.PreviousSibling (root, path),
                    _ => null
                };
            });
            return target == null ? null : new ElementProvider (this, target);
        }

        // ── Hit-testing & focus ────────────────────────────────────────────────────

        public IRawElementProviderFragment? FromPoint (double x, double y)
        {
            var path = OnUi (() => {
                var client = _window.PointToClient (new System.Drawing.Point ((int) x, (int) y));
                return UiaTree.HitTest (AutomationProvider.BuildTree (_window), client);
            });
            return new ElementProvider (this, path);   // empty path => the window root (no deeper hit)
        }

        public ElementProvider? FocusedProvider ()
        {
            var path = OnUi (() => UiaTree.FindFocused (AutomationProvider.BuildTree (_window)));
            return path == null ? null : new ElementProvider (this, path);
        }

        // ── Actions ──────────────────────────────────────────────────────────────────

        public void InvokePath (int[] path) => OnUi (() => {
            var el = UiaTree.Follow (AutomationProvider.BuildTree (_window), path);
            if (el != null)
                new AutomationSession (_window).Click (el);
        });

        public void SetValuePath (int[] path, string value) => OnUi (() => {
            var el = UiaTree.Follow (AutomationProvider.BuildTree (_window), path);
            if (el == null)
                return;
            var session = new AutomationSession (_window);
            session.Clear (el);
            session.SendKeys (el, value);
        });

        // ── UIA events from the observer ───────────────────────────────────────────

        private void OnFocusChanged (object? sender, MfAutomationElement? _)
        {
            if (!AutomationInteropProvider.ClientsAreListening)
                return;

            // The Focused flag in a fresh snapshot is the source of truth; locate that provider and announce.
            if (FocusedProvider () is { } provider)
                AutomationInteropProvider.RaiseAutomationEvent (
                    AutomationElementIdentifiers.AutomationFocusChangedEvent,
                    provider,
                    new AutomationFocusChangedEventArgs (0, 0));
        }

        private void OnValueChanged (object? sender, MfAutomationElement? _)
        {
            if (!AutomationInteropProvider.ClientsAreListening)
                return;

            if (FocusedProvider () is { } provider)
                AutomationInteropProvider.RaiseAutomationPropertyChangedEvent (
                    provider,
                    new AutomationPropertyChangedEventArgs (ValuePatternIdentifiers.ValueProperty, null, provider.Value));
        }

        // ── UI-thread marshalling (mirrors the WebDriver server) ───────────────────

        private static T OnUi<T> (Func<T> func) =>
            Platform.Backend.CheckAccess () ? func () : Platform.Backend.Invoke (func);

        private static void OnUi (Action action)
        {
            if (Platform.Backend.CheckAccess ())
                action ();
            else
                Platform.Backend.Invoke (action);
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;

            _observer.FocusChanged -= OnFocusChanged;
            _observer.ValueChanged -= OnValueChanged;
            _observer.Dispose ();

            Native.RemoveWindowSubclass (_hwnd, _proc, SubclassId);
        }
    }
}
