using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Desktop dispatches to Windows, macOS or Linux at run time.</summary>
    internal static class PlatformLauncher
    {
        public static ILauncherBackend Create () => new DesktopLauncherBackend ();
    }

    /// <summary>
    /// The shell: <c>UseShellExecute</c> on Windows, <c>open</c> on macOS, <c>xdg-open</c> on Linux. The browser (WebAssembly)
    /// reports unsupported, since it can start no process; opening a link there needs JS interop this package does not carry.
    /// </summary>
    internal sealed class DesktopLauncherBackend : ILauncherBackend
    {
        /// <inheritdoc />
        public bool IsSupported => !OperatingSystem.IsBrowser () && (OperatingSystem.IsWindows () || OperatingSystem.IsMacOS () || OperatingSystem.IsLinux ());

        /// <inheritdoc />
        public Task<bool> OpenAsync (Uri uri)
        {
            try {
                ProcessStartInfo start;
                if (OperatingSystem.IsWindows ()) {
                    start = new ProcessStartInfo (uri.AbsoluteUri) { UseShellExecute = true };
                } else {
                    // Argument list, not a command string: the URI is one argument and is never parsed by a shell.
                    start = new ProcessStartInfo (OperatingSystem.IsMacOS () ? "open" : "xdg-open") {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                    start.ArgumentList.Add (uri.AbsoluteUri);
                }

                using var process = Process.Start (start);
                return Task.FromResult (process is not null);
            } catch {
                return Task.FromResult (false);
            }
        }
    }
}
