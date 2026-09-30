using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Desktop dispatches to Windows, macOS or Linux at run time.</summary>
    internal static class PlatformSpeech
    {
        public static ISpeechBackend Create () => new DesktopSpeechBackend ();
    }

    /// <summary>
    /// macOS's <c>say</c>, Linux's <c>espeak</c>/<c>espeak-ng</c>, or Windows' SAPI (via a short PowerShell script over
    /// <c>System.Speech</c>), told apart at run time -- the same shape <see cref="DesktopSecureStorageBackend"/> already uses for
    /// one desktop build to run on all three. The text always travels over the spawned process's stdin, never as a command-line
    /// argument or interpolated into a shell string, so nothing needs escaping regardless of what punctuation or quotes it contains.
    /// </summary>
    internal sealed class DesktopSpeechBackend : ISpeechBackend
    {
        private bool? windows_available;
        private bool? macos_available;
        private string? linux_engine; // "espeak-ng", "espeak", or "" once probed and neither is present

        /// <inheritdoc />
        public bool IsSupported => Dispatch (
            OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
            WindowsAvailable, MacOSAvailable, () => LinuxEngine () is not null);

        /// <inheritdoc />
        public Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken) => Dispatch (
            OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
            () => SpeakWindowsAsync (text, options, cancellationToken),
            () => SpeakMacOSAsync (text, options, cancellationToken),
            () => SpeakLinuxAsync (text, options, cancellationToken));

        // The same injectable shape DesktopSecureStorageBackend.Dispatch uses, so a test can drive all three branches without
        // depending on which OS the test itself happens to run on.
        internal static T Dispatch<T> (Func<bool> isWindows, Func<bool> isMacOS, Func<bool> isLinux, Func<T> onWindows, Func<T> onMacOS, Func<T> onLinux)
        {
            try {
                if (isWindows ())
                    return onWindows ();
                if (isMacOS ())
                    return onMacOS ();
                if (isLinux ())
                    return onLinux ();
            } catch {
                // See the type summary: any platform failure degrades to "not supported"/"did not speak", never a crash.
            }

            return default!;
        }

        // ---- Windows: a short PowerShell script over System.Speech.Synthesis -----------------------
        // Spawns a process rather than referencing System.Speech directly: that assembly is Windows-only, and this project also
        // targets net8.0/net10.0 rows that run on every desktop OS, the same "spawn an OS utility" choice the owner's own
        // decision (PLAN.md section 11.5) keeps for desktop audio. Not run on Windows -- this machine has none, the same honest
        // gap DesktopKeepAwake's and DesktopSecureStorageBackend's own Windows sections already have.

        private bool WindowsAvailable ()
        {
            if (windows_available is { } cached)
                return cached;

            windows_available = ProbeAvailable ("powershell", "-NoProfile", "-NonInteractive", "-Command", "exit 0");
            return windows_available.Value;
        }

        private static Task SpeakWindowsAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            // $input reads the whole of stdin; -Rate is an integer -10..10 (SAPI's own range), so the 0.5..2.0 float rate maps
            // onto it roughly logarithmically the way SAPI's own docs describe "each step is about 10% faster/slower".
            var rate = (int)Math.Round (Math.Clamp (Math.Log (options.Rate, 1.1), -10, 10));
            var volume = (int)Math.Round (Math.Clamp (options.Volume, 0f, 1f) * 100);
            var script = "Add-Type -AssemblyName System.Speech; " +
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                $"$s.Rate = {rate.ToString (CultureInfo.InvariantCulture)}; " +
                $"$s.Volume = {volume.ToString (CultureInfo.InvariantCulture)}; " +
                "$text = [Console]::In.ReadToEnd(); " +
                "$s.Speak($text)";

            return RunAsync ("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], text, cancellationToken);
        }

        // ---- macOS: say -------------------------------------------------------------------------------
        // Not run on macOS -- this machine has none, the same honest gap noted above.

        private bool MacOSAvailable ()
        {
            if (macos_available is { } cached)
                return cached;

            macos_available = ProbeAvailable ("say", "-v", "?");
            return macos_available.Value;
        }

        private static Task SpeakMacOSAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            // say's own -r is words per minute; ~175 wpm is its documented default for rate 1.0.
            var rate = (int)Math.Round (175 * Math.Clamp (options.Rate, 0.5f, 2f));
            var args = new System.Collections.Generic.List<string> { "-r", rate.ToString (CultureInfo.InvariantCulture) };
            if (options.Locale is { Length: > 0 } locale)
                args.AddRange (["-v", locale]);

            return RunAsync ("say", args.ToArray (), text, cancellationToken);
        }

        // ---- Linux: espeak-ng, falling back to espeak ----------------------------------------------

        private string? LinuxEngine ()
        {
            if (linux_engine is { } cached)
                return cached.Length == 0 ? null : cached;

            linux_engine = ProbeAvailable ("espeak-ng", "--version") ? "espeak-ng"
                : ProbeAvailable ("espeak", "--version") ? "espeak"
                : "";
            return linux_engine.Length == 0 ? null : linux_engine;
        }

        private Task SpeakLinuxAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            if (LinuxEngine () is not { } engine)
                return Task.CompletedTask;

            // espeak's own -s is words per minute (default 175); -p is pitch 0..99 (default 50); -a is amplitude 0..200.
            var speed = (int)Math.Round (175 * Math.Clamp (options.Rate, 0.5f, 2f));
            var pitch = (int)Math.Round (50 * Math.Clamp (options.Pitch, 0.5f, 2f));
            var amplitude = (int)Math.Round (Math.Clamp (options.Volume, 0f, 1f) * 200);
            var args = new System.Collections.Generic.List<string> {
                "-s", speed.ToString (CultureInfo.InvariantCulture),
                "-p", Math.Min (99, pitch).ToString (CultureInfo.InvariantCulture),
                "-a", amplitude.ToString (CultureInfo.InvariantCulture),
            };
            if (options.Locale is { Length: > 0 } locale)
                args.AddRange (["-v", locale]);

            return RunAsync (engine, args.ToArray (), text, cancellationToken);
        }

        // ---- Shared process plumbing ----------------------------------------------------------------

        private static bool ProbeAvailable (string fileName, params string[] arguments)
        {
            try {
                var start = new ProcessStartInfo (fileName) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var arg in arguments)
                    start.ArgumentList.Add (arg);

                using var probe = Process.Start (start);
                if (probe is null)
                    return false;

                return probe.WaitForExit (2000) && probe.ExitCode == 0;
            } catch {
                return false;
            }
        }

        // Cancellation kills the spawned process -- there is no "pause and resume" a plain CLI utility offers, only start and
        // stop, the same granularity Speech.SpeakAsync's own doc comment sets expectations for.
        private static async Task RunAsync (string fileName, string[] arguments, string stdin, CancellationToken cancellationToken)
        {
            var start = new ProcessStartInfo (fileName) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in arguments)
                start.ArgumentList.Add (arg);

            using var process = Process.Start (start);
            if (process is null)
                return;

            using var registration = cancellationToken.Register (() => {
                try { if (!process.HasExited) process.Kill (); } catch { }
            });

            try {
                // A cancellation racing with the write (the process killed between Start and here) means the pipe is already
                // gone; that failure is exactly what cancellation asked for, not a real error, so it is swallowed like every
                // other caught failure in this file rather than propagated.
                await process.StandardInput.WriteAsync (stdin);
                process.StandardInput.Close ();
                await process.WaitForExitAsync (CancellationToken.None);
            } catch {
            }
        }
    }
}
