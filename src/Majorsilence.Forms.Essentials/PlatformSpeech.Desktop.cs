using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
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
        private string? linux_engine; // "espeak-ng", "espeak", "spd-say", or "" once probed and none is present

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

        /// <inheritdoc />
        public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync () => Dispatch (
            OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
            ListWindowsVoicesAsync, ListMacVoicesAsync, ListLinuxVoicesAsync) ?? Task.FromResult<IReadOnlyList<SpeechVoice>> ([]);

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
                // The name sits in single quotes in the script, so a quote inside it is doubled; a voice that is not installed is skipped.
                (options.Voice is { Length: > 0 } voice ? $"try {{ $s.SelectVoice('{voice.Replace ("'", "''")}') }} catch {{ }}; " : "") +
                $"$s.Rate = {rate.ToString (CultureInfo.InvariantCulture)}; " +
                $"$s.Volume = {volume.ToString (CultureInfo.InvariantCulture)}; " +
                "$text = [Console]::In.ReadToEnd(); " +
                "$s.Speak($text)";

            return RunAsync ("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], text, cancellationToken);
        }

        private static Task<IReadOnlyList<SpeechVoice>> ListWindowsVoicesAsync () => CaptureVoicesAsync (
            "powershell",
            ["-NoProfile", "-NonInteractive", "-Command",
             "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
             "$s.GetInstalledVoices() | ForEach-Object { $i = $_.VoiceInfo; \"$($i.Name)|$($i.Culture.Name)|$($i.Gender)\" }"],
            ParseWindowsVoices);

        /// <summary>Parses the Windows listing script's "name|culture|gender" lines. Unusable lines are skipped.</summary>
        internal static IReadOnlyList<SpeechVoice> ParseWindowsVoices (string output)
        {
            var voices = new List<SpeechVoice> ();
            foreach (var line in output.Split ('\n', StringSplitOptions.RemoveEmptyEntries)) {
                var parts = line.Trim ().Split ('|');
                if (parts.Length < 3 || parts[0].Length == 0)
                    continue;

                var gender = parts[2].Trim () switch { "Male" => VoiceGender.Male, "Female" => VoiceGender.Female, _ => VoiceGender.Unknown };
                voices.Add (new SpeechVoice (parts[0], parts[0], parts[1], gender));
            }

            return voices;
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
            // A chosen voice wins over a language: say's -v takes either a voice name or a language tag.
            if (options.Voice is { Length: > 0 } voice)
                args.AddRange (["-v", voice]);
            else if (options.Locale is { Length: > 0 } locale)
                args.AddRange (["-v", locale]);

            return RunAsync ("say", args.ToArray (), text, cancellationToken);
        }

        private static Task<IReadOnlyList<SpeechVoice>> ListMacVoicesAsync () => CaptureVoicesAsync ("say", ["-v", "?"], ParseMacVoices);

        private static readonly Regex MacVoiceLine = new (@"^(?<name>.+?)\s+(?<locale>[a-z]{2,3}[_-][A-Za-z0-9]+)\s+#", RegexOptions.Compiled);

        /// <summary>Parses <c>say -v ?</c>: "Name  en_US  # sample". macOS does not say a voice's sex, so it is <see cref="VoiceGender.Unknown"/>.</summary>
        internal static IReadOnlyList<SpeechVoice> ParseMacVoices (string output)
        {
            var voices = new List<SpeechVoice> ();
            foreach (var line in output.Split ('\n', StringSplitOptions.RemoveEmptyEntries)) {
                var m = MacVoiceLine.Match (line.TrimEnd ('\r'));
                if (!m.Success)
                    continue;

                var name = m.Groups["name"].Value.Trim ();
                voices.Add (new SpeechVoice (name, name, m.Groups["locale"].Value.Replace ('_', '-'), VoiceGender.Unknown));
            }

            return voices;
        }

        // ---- Linux: espeak-ng, falling back to espeak ----------------------------------------------

        private string? LinuxEngine ()
        {
            if (linux_engine is { } cached)
                return cached.Length == 0 ? null : cached;

            // espeak first (it names its voices and says their sex), then speech-dispatcher, which a stock desktop has when it has no espeak
            // binary of its own: it drives whatever engine the system is set up with, espeak-ng usually.
            linux_engine = ProbeAvailable ("espeak-ng", "--version") ? "espeak-ng"
                : ProbeAvailable ("espeak", "--version") ? "espeak"
                : ProbeAvailable ("spd-say", "--version") ? "spd-say"
                : "";
            return linux_engine.Length == 0 ? null : linux_engine;
        }

        private Task SpeakLinuxAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            if (LinuxEngine () is not { } engine)
                return Task.CompletedTask;

            if (engine == "spd-say")
                return SpeakSpeechDispatcherAsync (text, options, cancellationToken);

            // espeak's own -s is words per minute (default 175); -p is pitch 0..99 (default 50); -a is amplitude 0..200.
            var speed = (int)Math.Round (175 * Math.Clamp (options.Rate, 0.5f, 2f));
            var pitch = (int)Math.Round (50 * Math.Clamp (options.Pitch, 0.5f, 2f));
            var amplitude = (int)Math.Round (Math.Clamp (options.Volume, 0f, 1f) * 200);
            var args = new System.Collections.Generic.List<string> {
                "-s", speed.ToString (CultureInfo.InvariantCulture),
                "-p", Math.Min (99, pitch).ToString (CultureInfo.InvariantCulture),
                "-a", amplitude.ToString (CultureInfo.InvariantCulture),
            };
            if (options.Voice is { Length: > 0 } voice)
                args.AddRange (["-v", voice]);
            else if (options.Locale is { Length: > 0 } locale)
                args.AddRange (["-v", locale]);

            return RunAsync (engine, args.ToArray (), text, cancellationToken);
        }

        private Task<IReadOnlyList<SpeechVoice>> ListLinuxVoicesAsync ()
            => LinuxEngine () switch {
                "spd-say" => CaptureVoicesAsync ("spd-say", ["-L"], ParseSpeechDispatcherVoices),
                { } engine => CaptureVoicesAsync (engine, ["--voices"], ParseEspeakVoices),
                _ => Task.FromResult<IReadOnlyList<SpeechVoice>> ([]),
            };

        // ---- Linux: speech-dispatcher's spd-say ----

        private static readonly string[] SpeechDispatcherTypes = ["male1", "male2", "male3", "female1", "female2", "female3"];

        private static Task SpeakSpeechDispatcherAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            // -w waits until the line is spoken (so completing means it finished), -e reads the text from stdin so nothing is escaped.
            var args = new System.Collections.Generic.List<string> {
                "-w", "-e",
                "-r", SpeechDispatcherScale (options.Rate).ToString (CultureInfo.InvariantCulture),
                "-p", SpeechDispatcherScale (options.Pitch).ToString (CultureInfo.InvariantCulture),
                "-i", SpeechDispatcherVolume (options.Volume).ToString (CultureInfo.InvariantCulture),
            };

            // A chosen voice wins over a language: one of the preferred types (male1 ... female3) says a man's or a woman's voice, any other
            // is a named synthesis voice.
            if (options.Voice is { Length: > 0 } voice) {
                args.AddRange (Array.IndexOf (SpeechDispatcherTypes, voice) >= 0 ? ["-t", voice] : ["-y", voice]);
            } else if (options.Locale is { Length: > 0 } locale) {
                args.AddRange (["-l", locale.Split ('-')[0]]);
            }

            return RunAsync ("spd-say", args.ToArray (), text, cancellationToken);
        }

        /// <summary>Maps a 0.5 to 2.0 rate or pitch (1.0 natural) onto speech-dispatcher's -100 to +100 around zero.</summary>
        internal static int SpeechDispatcherScale (float scale) => (int)Math.Round (Math.Clamp ((scale - 1f) * 100f, -100f, 100f));

        /// <summary>Maps a 0 to 1 volume onto speech-dispatcher's -100 (silent) to 0 (its own full level).</summary>
        internal static int SpeechDispatcherVolume (float volume) => (int)Math.Round (Math.Clamp ((volume - 1f) * 100f, -100f, 0f));

        private static readonly Regex SpeechDispatcherVoiceLine = new (@"^\s*(?<name>.+?)\s{2,}(?<language>\S+)\s{2,}(?<variant>\S.*)$", RegexOptions.Compiled);

        /// <summary>
        /// Parses <c>spd-say -L</c> ("NAME LANGUAGE VARIANT" columns), keeping the plain voice of each language, and puts the six preferred voice types in front: they are the one thing
        /// speech-dispatcher says about a voice's sex, so they are what a person picks a man's or a woman's voice with.
        /// </summary>
        internal static IReadOnlyList<SpeechVoice> ParseSpeechDispatcherVoices (string output)
        {
            var voices = new List<SpeechVoice> ();
            foreach (var type in SpeechDispatcherTypes)
                voices.Add (new SpeechVoice (type, (type.StartsWith ("male", StringComparison.Ordinal) ? "Man " : "Woman ") + type[^1], "",
                    type.StartsWith ("male", StringComparison.Ordinal) ? VoiceGender.Male : VoiceGender.Female));

            foreach (var line in output.Split ('\n', StringSplitOptions.RemoveEmptyEntries)) {
                var m = SpeechDispatcherVoiceLine.Match (line.TrimEnd ('\r'));
                if (!m.Success || m.Groups["name"].Value == "NAME")
                    continue;

                // The listing is every language crossed with every espeak variant (Afrikaans+Adam, Afrikaans+Alex...), thousands of lines, so
                // only the plain voice of each language is offered, and the male and female types above say the rest.
                if (m.Groups["variant"].Value.Trim () != "none")
                    continue;

                var name = m.Groups["name"].Value.Trim ();
                voices.Add (new SpeechVoice (name, name, m.Groups["language"].Value, VoiceGender.Unknown));
            }

            return voices;
        }

        /// <summary>
        /// Parses <c>espeak-ng --voices</c>: "Pty Language Age/Gender VoiceName File Other". The language column is what <c>-v</c> takes, and the
        /// age/gender column ("--/M", "--/F") is the one place a desktop platform says a voice's sex.
        /// </summary>
        internal static IReadOnlyList<SpeechVoice> ParseEspeakVoices (string output)
        {
            var voices = new List<SpeechVoice> ();
            foreach (var line in output.Split ('\n', StringSplitOptions.RemoveEmptyEntries)) {
                var columns = line.Split (' ', StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 5 || !int.TryParse (columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !columns[2].Contains ('/'))
                    continue;

                var gender = columns[2].EndsWith ("/M", StringComparison.Ordinal) ? VoiceGender.Male
                    : columns[2].EndsWith ("/F", StringComparison.Ordinal) ? VoiceGender.Female
                    : VoiceGender.Unknown;
                voices.Add (new SpeechVoice (columns[1], columns[3].Replace ('_', ' '), EspeakLocale (columns[1]), gender));
            }

            return voices;
        }

        // "en-gb-x-rp" is the language "en-GB" with a private-use extension; "af" is just "af".
        private static string EspeakLocale (string language)
        {
            var parts = language.Split ('-');
            if (parts.Length < 2 || parts[1].Length == 1)
                return parts[0];

            return parts[0] + "-" + parts[1].ToUpperInvariant ();
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

        // Runs a listing command and parses its stdout; a missing command, a timeout or a failure is an empty list.
        private static async Task<IReadOnlyList<SpeechVoice>> CaptureVoicesAsync (string fileName, string[] arguments, Func<string, IReadOnlyList<SpeechVoice>> parse)
        {
            try {
                var start = new ProcessStartInfo (fileName) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var arg in arguments)
                    start.ArgumentList.Add (arg);

                using var process = Process.Start (start);
                if (process is null)
                    return [];

                var output = process.StandardOutput.ReadToEndAsync ();
                using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (10));
                await process.WaitForExitAsync (timeout.Token).ConfigureAwait (false);
                return process.ExitCode == 0 ? parse (await output.ConfigureAwait (false)) : [];
            } catch {
                return [];
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
