using System;
using System.Collections.Generic;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Interop
{
    /// <summary>
    /// Forward bridge for an incremental WinForms -> Majorsilence.Forms migration: lets a Majorsilence.Forms
    /// (Avalonia-backed) app create and show a legacy <see cref="System.Windows.Forms.Form"/>.
    ///
    /// Windows-only by definition (System.Windows.Forms does not exist off Windows). This whole assembly is
    /// transitional scaffolding: when the last WinForms form is converted, drop the dependency and the app
    /// goes cross-platform.
    ///
    /// Threading contract: call on the UI (STA) thread — i.e. from a Majorsilence event handler — exactly like
    /// WinForms itself requires. From a background thread, marshal first:
    ///     Majorsilence.Forms.Application.RunOnUIThread(() => WindowsFormsInterop.Show(form));
    /// </summary>
    public static class WindowsFormsInterop
    {
        private static bool _initialized;
        private static readonly List<WF.Form> _openModeless = new();

        /// <summary>
        /// Optional. Once Majorsilence.Forms exposes the host window's Win32 HWND, set this so dialogs can be
        /// owned by (and modal to) their Majorsilence parent. Until set, forms are shown unowned.
        ///
        /// Suggested accessor in the Avalonia backend:
        ///     IntPtr Majorsilence.Forms.Avalonia.GetWin32Handle(Majorsilence.Forms.Form form)
        ///         => /* presenter.TopLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero */;
        /// then:  WindowsFormsInterop.OwnerHandleResolver = Majorsilence.Forms.Avalonia.GetWin32Handle;
        /// </summary>
        public static Func<MF.Form, IntPtr>? OwnerHandleResolver { get; set; }

        /// <summary>
        /// One-time WinForms initialization. Safe to call repeatedly; runs once. Call before showing the first
        /// form (Show/ShowDialog call it for you).
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            EnsureWindows();
            WF.Application.EnableVisualStyles();
            WF.Application.SetCompatibleTextRenderingDefault(false);
            // Match Avalonia's DPI model so the hosted form scales with the rest of the app.
            WF.Application.SetHighDpiMode(WF.HighDpiMode.PerMonitorV2);
            _initialized = true;
        }

        /// <summary>Show a legacy form modeless. Returns immediately; the form runs on the shared Win32 pump.</summary>
        public static void Show(WF.Form form, MF.Form? owner = null)
        {
            ArgumentNullException.ThrowIfNull(form);
            EnsureWindows();
            Initialize();

            _openModeless.Add(form);
            form.FormClosed += (_, _) =>
            {
                _openModeless.Remove(form);
                form.Dispose();
            };

            var ownerWin = WrapOwner(owner);
            if (ownerWin is not null) form.Show(ownerWin);
            else form.Show();
        }

        /// <summary>Show a legacy form modally. Blocks the UI thread (WinForms' own nested Win32 loop) and returns the result.</summary>
        public static WF.DialogResult ShowDialog(WF.Form form, MF.Form? owner = null)
        {
            ArgumentNullException.ThrowIfNull(form);
            EnsureWindows();
            Initialize();

            using (form) // a modal dialog is owned by the caller's scope; dispose when done
            {
                var ownerWin = WrapOwner(owner);
                return ownerWin is not null ? form.ShowDialog(ownerWin) : form.ShowDialog();
            }
        }

        // Factory overloads: construct the form on the UI thread, at show time.
        public static void Show(Func<WF.Form> factory, MF.Form? owner = null)
            => Show((factory ?? throw new ArgumentNullException(nameof(factory)))(), owner);

        public static WF.DialogResult ShowDialog(Func<WF.Form> factory, MF.Form? owner = null)
            => ShowDialog((factory ?? throw new ArgumentNullException(nameof(factory)))(), owner);

        private static Win32Window? WrapOwner(MF.Form? owner)
        {
            var resolver = OwnerHandleResolver;
            if (owner is null || resolver is null) return null;
            var h = resolver(owner);
            return h == IntPtr.Zero ? null : new Win32Window(h);
        }

        private static void EnsureWindows()
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "System.Windows.Forms interop is only available on Windows. On other platforms, open the " +
                    "migrated Majorsilence.Forms equivalent instead.");
        }

        private sealed class Win32Window : WF.IWin32Window
        {
            public Win32Window(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
