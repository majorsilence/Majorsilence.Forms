using System;
using System.Diagnostics;

namespace Majorsilence.Forms.Media
{
    /// <summary>A sound that is currently playing: disposing stops it, <see cref="Wait"/> blocks until it ends.</summary>
    internal interface IPlayingSound : IDisposable
    {
        /// <summary>Blocks until playback finishes (or is stopped).</summary>
        void Wait ();
    }

    /// <summary>
    /// Plays audio through the operating system's own playback utility -- <c>afplay</c> on macOS,
    /// <c>paplay</c>/<c>aplay</c> on Linux, PowerShell's <c>System.Media</c> on Windows.
    /// </summary>
    /// <remarks>
    /// This is the whole audio engine, and its size is the point. The alternatives are embedding a
    /// playback library (a native dependency this repo has deliberately avoided everywhere else) or
    /// per-TFM platform SDKs (which would put mobile workloads into the core build). Spawning the OS
    /// utility gets real sound on every desktop with zero dependencies, and the process handle gives
    /// Stop/PlaySync their semantics for free: kill it, or wait for it.
    ///
    /// Each play is a short-lived child process, so there is ~50-200ms of launch latency. That is the
    /// deliberate trade: these APIs play alert sounds and short .wav cues, not soundtracks. On platforms
    /// with no utility to spawn (mobile), playback is silent -- the stub policy's neutral outcome --
    /// until a backend supplies a native path; the seam for that is this class.
    ///
    /// Nothing here ever throws: a missing utility, an unplayable file, or a dead audio daemon degrades
    /// to silence, exactly as the stubs did -- the upgrade is that when the OS can play, it does.
    /// </remarks>
    internal static class NativeAudio
    {
        /// <summary>Test seam: replaces process launching so the suite can assert commands without making noise.</summary>
        internal static Func<ProcessStartInfo, IPlayingSound?>? LauncherOverride;

        /// <summary>The commands that can play the given audio file, most preferred first.</summary>
        /// <remarks>
        /// A list because Linux has no single guaranteed utility: <c>paplay</c> (PulseAudio/PipeWire) is
        /// tried first since it decodes more than WAV, then ALSA's <c>aplay</c> (WAV only -- which is the
        /// full upstream SoundPlayer contract anyway). The chosen child process lives for the duration of
        /// playback on every platform, which is what makes Stop (kill) and PlaySync (wait) work; on
        /// Windows that is why the child calls <c>PlaySync</c>, not <c>Play</c>.
        /// </remarks>
        internal static ProcessStartInfo[] FileCommands (string path)
        {
            if (OperatingSystemCompat.IsMacOS ())
                return [Command ("afplay", path)];

            if (OperatingSystemCompat.IsLinux ())
                return [Command ("paplay", path), Command ("aplay", path)];

            if (OperatingSystemCompat.IsWindows ())
                return [PowerShell ($"(New-Object System.Media.SoundPlayer('{EscapePs (path)}')).PlaySync()")];

            return [];
        }

        /// <summary>The commands that can play the named system alert sound, most preferred first.</summary>
        /// <remarks>
        /// Windows asks its own <c>SystemSounds</c> (with a short sleep so the async native play is not
        /// cut off by the child exiting). macOS and Linux have no SystemSounds notion, so the five names
        /// map onto each platform's stock alert set -- the mapping is a judgment call, chosen for rough
        /// emotional equivalence (Hand, the error sound, gets the deepest/most negative tone) and
        /// documented here as the single place to retune it. Missing theme files degrade to silence.
        /// </remarks>
        internal static ProcessStartInfo[] SystemSoundCommands (string name)
        {
            if (OperatingSystemCompat.IsWindows ())
                return [PowerShell ($"[System.Media.SystemSounds]::{name}.Play(); Start-Sleep -Milliseconds 700")];

            if (OperatingSystemCompat.IsMacOS ()) {
                var file = name switch {
                    nameof (SystemSounds.Asterisk) => "Glass",
                    nameof (SystemSounds.Exclamation) => "Sosumi",
                    nameof (SystemSounds.Hand) => "Basso",
                    nameof (SystemSounds.Question) => "Purr",
                    _ => "Tink",   // Beep, and anything unrecognised
                };
                return [Command ("afplay", $"/System/Library/Sounds/{file}.aiff")];
            }

            if (OperatingSystemCompat.IsLinux ()) {
                var file = name switch {
                    nameof (SystemSounds.Asterisk) => "dialog-information",
                    nameof (SystemSounds.Exclamation) => "dialog-warning",
                    nameof (SystemSounds.Hand) => "dialog-error",
                    nameof (SystemSounds.Question) => "dialog-question",
                    _ => "bell",
                };
                return [
                    Command ("paplay", $"/usr/share/sounds/freedesktop/stereo/{file}.oga"),
                    Command ("paplay", "/usr/share/sounds/freedesktop/stereo/bell.oga"),
                ];
            }

            return [];
        }

        /// <summary>Starts the first launchable candidate; null when none launches.</summary>
        internal static IPlayingSound? Start (ProcessStartInfo[] candidates)
        {
            foreach (var candidate in candidates) {
                try {
                    if (LauncherOverride is { } launcher)
                        return launcher (candidate);

                    var process = Process.Start (candidate);
                    if (process is not null)
                        return new PlayingProcess (process);
                } catch {
                    // Utility not installed, daemon down, file unplayable: try the next candidate,
                    // and past the last one degrade to silence rather than surface an exception from
                    // an API whose upstream contract is fire-and-forget.
                }
            }

            return null;
        }

        private static ProcessStartInfo Command (string fileName, string argument)
        {
            var info = new ProcessStartInfo (fileName) {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            AddArgument (info, argument);
            return info;
        }

        // ProcessStartInfo.ArgumentList is a .NET Core 2.1 addition; on netstandard2.0 the arguments
        // are composed into the single Arguments string with the same Windows-style quoting.
        private static void AddArgument (ProcessStartInfo info, string argument)
        {
#if NETSTANDARD2_0
            var quoted = argument.Length > 0 && argument.IndexOf (' ') < 0 && argument.IndexOf ('"') < 0
                ? argument
                : "\"" + argument.Replace ("\"", "\\\"") + "\"";
            info.Arguments = string.IsNullOrEmpty (info.Arguments) ? quoted : info.Arguments + " " + quoted;
#else
            info.ArgumentList.Add (argument);
#endif
        }

        private static ProcessStartInfo PowerShell (string script)
        {
            var info = new ProcessStartInfo ("powershell") {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            AddArgument (info, "-NoProfile");
            AddArgument (info, "-NonInteractive");
            AddArgument (info, "-Command");
            AddArgument (info, script);
            return info;
        }

        // PowerShell single-quoted strings escape embedded quotes by doubling them; nothing else is
        // special inside single quotes, which is why the path is passed this way and not interpolated
        // into double quotes.
        private static string EscapePs (string s) => s.Replace ("'", "''");

        private sealed class PlayingProcess (Process process) : IPlayingSound
        {
            public void Wait ()
            {
                try { process.WaitForExit (); } catch { }
            }

            public void Dispose ()
            {
                try {
                    if (!process.HasExited)
#if NETSTANDARD2_0
                        process.Kill ();
#else
                        process.Kill (entireProcessTree: true);
#endif
                } catch { }

                process.Dispose ();
            }
        }
    }
}
