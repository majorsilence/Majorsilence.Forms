using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Reads whether the user prefers reduced motion on a desktop OS: Windows' client-area animation setting, macOS's reduce-motion,
    /// or (on Linux) GNOME's <c>enable-animations</c>. A single desktop build runs on all three, told apart at run time, the way the
    /// rest of the framework already tells desktop OSes apart (<c>OperatingSystemCompat</c>).
    /// </summary>
    internal static class DesktopReducedMotion
    {
        /// <summary>
        /// Reads the current answer. Defensive by design: a missing utility, a locked-down sandbox, a desktop environment that is not
        /// GNOME, or any other read failure answers false — the same conservative default <see cref="SystemInformation.PrefersReducedMotion"/>
        /// itself falls back to when a backend cannot tell at all, because it never suppresses an animation nobody asked to suppress.
        /// </summary>
        public static bool Read ()
            // OperatingSystemCompat, not OperatingSystem.IsWindows/IsMacOS/IsLinux directly: those three are not in netstandard2.0's
            // surface, which this project also targets, and OperatingSystemCompat already exists (NetStandardCompat.cs) to bridge that.
            => Dispatch (OperatingSystemCompat.IsWindows, OperatingSystemCompat.IsMacOS, OperatingSystemCompat.IsLinux,
                () => !WindowsClientAreaAnimationEnabled (), MacOSReduceMotionEnabled, () => ParseGnomeAnimationsEnabled (RunGSettingsGet ()) is false);

        /// <summary>
        /// Which OS reader runs, and the same defensive fallback, with every predicate and reader injected so this is testable without
        /// depending on which OS the test itself happens to run on. <paramref name="isWindows"/>/<paramref name="isMacOS"/>/
        /// <paramref name="isLinux"/> are tried in that order and the first that answers true picks the one reader that runs; a reader
        /// that throws, or no predicate matching, answers false.
        /// </summary>
        internal static bool Dispatch (
            Func<bool> isWindows, Func<bool> isMacOS, Func<bool> isLinux,
            Func<bool> readWindows, Func<bool> readMacOS, Func<bool> readLinux)
        {
            try {
                if (isWindows ())
                    return readWindows ();

                if (isMacOS ())
                    return readMacOS ();

                if (isLinux ())
                    return readLinux ();
            } catch {
                // See the summary: any failure here answers false, not "don't know".
            }

            return false;
        }

        // ---- Linux / GNOME: `gsettings get org.gnome.desktop.interface enable-animations` ----------
        // Measured on a GNOME desktop (Ubuntu, Wayland via Xwayland): with the setting at its default (animations on), this reads
        // "true" and Read() answers false; not measured with the setting actually turned off, nor on a non-GNOME desktop environment,
        // where gsettings can still run but answers for a schema nothing is honouring.

        private static string RunGSettingsGet ()
        {
            using var process = Process.Start (new ProcessStartInfo ("gsettings", "get org.gnome.desktop.interface enable-animations") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException ("gsettings did not start.");

            var output = process.StandardOutput.ReadToEnd ();
            process.WaitForExit (2000);
            return output;
        }

        /// <summary>Parses <c>gsettings get</c>'s output for a boolean key ("true" or "false", nothing quoted). Null when it is neither.</summary>
        internal static bool? ParseGnomeAnimationsEnabled (string raw)
        {
            var trimmed = raw.Trim ();
            if (string.Equals (trimmed, "true", StringComparison.Ordinal))
                return true;
            if (string.Equals (trimmed, "false", StringComparison.Ordinal))
                return false;
            return null;
        }

        // ---- Windows: SPI_GETCLIENTAREAANIMATION -------------------------------------------------
        // Written from the documented Win32 API (SystemParametersInfo, SPI_GETCLIENTAREAANIMATION = 0x1042); not run on Windows --
        // this machine has none. A failed call (SystemParametersInfo returns false) is treated as "animations enabled" by Read()'s
        // negation only through the catch above if it throws; here it answers the conservative "enabled" itself if the call reports
        // failure without throwing.
        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo (uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

        private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

        private static bool WindowsClientAreaAnimationEnabled ()
        {
            var enabled = true;
            return !SystemParametersInfo (SPI_GETCLIENTAREAANIMATION, 0, ref enabled, 0) || enabled;
        }

        // ---- macOS: NSWorkspace.accessibilityDisplayShouldReduceMotion ---------------------------
        // Written from the documented AppKit API via the Objective-C runtime (no Xamarin.Mac/.NET-for-macOS binding is referenced
        // here, so this calls objc_msgSend directly, the same mechanism those bindings are generated on top of); not run on macOS --
        // this machine has none.
        private const string ObjCLibrary = "/usr/lib/libobjc.dylib";

        // CharSet.Ansi: a class name or selector is always plain ASCII, and on a non-Windows OS .NET's "Ansi" P/Invoke marshaling is
        // UTF-8, which is what libobjc expects; the same declaration is correct on every TFM this project targets, unlike
        // UnmanagedType.LPUTF8Str (not in netstandard2.0's surface). CA2101 flags an explicit CharSet here anyway (it wants
        // LPUTF8Str specifically); suppressed with that reason, not left for a NoWarn a later contributor cannot trace back to this
        // comment.
#pragma warning disable CA2101
        [DllImport (ObjCLibrary, CharSet = CharSet.Ansi)]
        private static extern IntPtr objc_getClass (string name);

        [DllImport (ObjCLibrary, CharSet = CharSet.Ansi)]
        private static extern IntPtr sel_registerName (string name);
#pragma warning restore CA2101

        [DllImport (ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_IntPtr (IntPtr receiver, IntPtr selector);

        [DllImport (ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern byte objc_msgSend_Bool (IntPtr receiver, IntPtr selector);

        private static bool MacOSReduceMotionEnabled ()
        {
            var workspaceClass = objc_getClass ("NSWorkspace");
            var sharedWorkspace = objc_msgSend_IntPtr (workspaceClass, sel_registerName ("sharedWorkspace"));
            if (sharedWorkspace == IntPtr.Zero)
                return false;

            return objc_msgSend_Bool (sharedWorkspace, sel_registerName ("accessibilityDisplayShouldReduceMotion")) != 0;
        }
    }
}
