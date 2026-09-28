using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Keeps the screen (and system) from sleeping on a desktop OS: Windows' <c>SetThreadExecutionState</c>,
    /// an IOKit power assertion on macOS, or <c>systemd-inhibit</c> on Linux. A single desktop build runs on
    /// all three, told apart at run time, the same way <see cref="DesktopReducedMotion"/> already is.
    /// </summary>
    internal static class DesktopKeepAwake
    {
        /// <summary>Gets whether an inhibit is currently held.</summary>
        public static bool IsEnabled { get; private set; }

        /// <summary>
        /// Turns the inhibit on or off. Defensive by design, the same reasoning
        /// <see cref="DesktopReducedMotion.Read"/> documents: a missing utility, a locked-down sandbox, or
        /// any other failure degrades to doing nothing rather than throwing -- a screen that fails to stay
        /// awake is a worse bedside-mode experience than the one this exists to fix, but never worth a crash.
        /// </summary>
        public static void Set (bool enabled)
        {
            if (enabled == IsEnabled)
                return;

            Dispatch (OperatingSystemCompat.IsWindows, OperatingSystemCompat.IsMacOS, OperatingSystemCompat.IsLinux,
                () => SetWindows (enabled), () => SetMacOS (enabled), () => SetLinux (enabled));

            IsEnabled = enabled;
        }

        /// <summary>
        /// Which OS setter runs, with every predicate and setter injected so this is testable without
        /// depending on which OS the test itself happens to run on -- the same shape
        /// <see cref="DesktopReducedMotion.Dispatch"/> uses for reading instead of setting.
        /// </summary>
        internal static void Dispatch (
            Func<bool> isWindows, Func<bool> isMacOS, Func<bool> isLinux,
            Action setWindows, Action setMacOS, Action setLinux)
        {
            try {
                if (isWindows ()) {
                    setWindows ();
                    return;
                }

                if (isMacOS ()) {
                    setMacOS ();
                    return;
                }

                if (isLinux ()) {
                    setLinux ();
                    return;
                }
            } catch {
                // See Set's summary: any failure here does nothing, not a crash.
            }
        }

        // ---- Windows: SetThreadExecutionState -----------------------------------------------------
        // Written from the documented Win32 API; not run on Windows -- this machine has none. Continuous
        // is required on every call (it is not sticky): passing it alone on disable clears the display/
        // system flags this process itself set, without touching another process's own execution-state
        // request, exactly the documented contract.

        [DllImport ("kernel32.dll")]
        private static extern uint SetThreadExecutionState (uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        private static void SetWindows (bool enabled)
        {
            // Returns the previous state on success, 0 on failure; nothing actionable to do with either
            // beyond what Set's own defensive contract already covers, so the result is deliberately
            // discarded rather than left to the platform-compat/error-code analyzer's own assumption that
            // an unchecked native return is a bug.
            _ = SetThreadExecutionState (enabled ? ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED : ES_CONTINUOUS);
        }

        // ---- macOS: IOPMAssertionCreateWithName / IOPMAssertionRelease ----------------------------
        // Written from the documented IOKit/CoreFoundation APIs via raw P/Invoke (no Xamarin.Mac/.NET-for-
        // macOS binding is referenced here, the same reasoning DesktopReducedMotion's own macOS section
        // documents for going straight to libobjc there); not run on macOS -- this machine has none.
        // PreventUserIdleDisplaySleep is the modern assertion type for "keep the display on"; unlike
        // PreventUserIdleSystemSleep it does not also block the system from idling to a low-power state
        // while the display itself is what a bedside app actually needs kept on.

        private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string IOKitLibrary = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const uint kCFStringEncodingUTF8 = 0x08000100;
        private const uint kIOPMAssertionLevelOn = 255;

#pragma warning disable CA2101 // see DesktopReducedMotion's own suppression for the same reason: Ansi is UTF-8 on non-Windows P/Invoke marshaling, and LPUTF8Str is not in netstandard2.0's surface.
        [DllImport (CoreFoundationLibrary, CharSet = CharSet.Ansi)]
        private static extern IntPtr CFStringCreateWithCString (IntPtr alloc, string cStr, uint encoding);
#pragma warning restore CA2101

        [DllImport (CoreFoundationLibrary)]
        private static extern void CFRelease (IntPtr cf);

        [DllImport (IOKitLibrary)]
        private static extern int IOPMAssertionCreateWithName (IntPtr assertionType, uint assertionLevel, IntPtr assertionName, out uint assertionId);

        [DllImport (IOKitLibrary)]
        private static extern int IOPMAssertionRelease (uint assertionId);

        private static uint mac_assertion_id;
        private static bool mac_assertion_held;

        private static void SetMacOS (bool enabled)
        {
            if (enabled) {
                if (mac_assertion_held)
                    return;

                var type = CFStringCreateWithCString (IntPtr.Zero, "PreventUserIdleDisplaySleep", kCFStringEncodingUTF8);
                var name = CFStringCreateWithCString (IntPtr.Zero, "Majorsilence.Forms Application.KeepScreenAwake", kCFStringEncodingUTF8);
                try {
                    mac_assertion_held = IOPMAssertionCreateWithName (type, kIOPMAssertionLevelOn, name, out mac_assertion_id) == 0;
                } finally {
                    if (type != IntPtr.Zero)
                        CFRelease (type);
                    if (name != IntPtr.Zero)
                        CFRelease (name);
                }
            } else {
                if (!mac_assertion_held)
                    return;

                _ = IOPMAssertionRelease (mac_assertion_id);   // same reasoning as SetWindows above
                mac_assertion_held = false;
            }
        }

        // ---- Linux: systemd-inhibit, held for as long as a placeholder process runs ---------------
        // systemd-inhibit wraps a command and holds its idle/sleep inhibitor lock (a session D-Bus call
        // under the hood, org.freedesktop.login1's Inhibit) for exactly that command's lifetime, execing
        // it directly -- so the PID Process.Start returns is the held lock, and killing it releases the
        // lock the same way the command exiting on its own would. A conservative no-op (see Set's summary)
        // on a non-systemd distro, or a sandbox without access to it, the same defensive contract
        // DesktopReducedMotion's own gsettings call has.

        private static Process? linux_inhibit_process;

        private static void SetLinux (bool enabled)
        {
            if (enabled) {
                if (linux_inhibit_process is not null)
                    return;

                // Arguments (a single string), not ArgumentList: the latter is not in netstandard2.0's
                // surface, which this project also targets (the same reason DesktopReducedMotion's own
                // gsettings call uses a single string too). Every value here is a fixed literal, never
                // user input, so there is nothing to escape.
                var start = new ProcessStartInfo ("systemd-inhibit", "--what=idle:sleep --who=Majorsilence.Forms --why=KeepScreenAwake sleep infinity") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                linux_inhibit_process = Process.Start (start);
            } else {
                if (linux_inhibit_process is null)
                    return;

                try {
                    linux_inhibit_process.Kill ();
                } finally {
                    linux_inhibit_process.Dispose ();
                    linux_inhibit_process = null;
                }
            }
        }
    }
}
