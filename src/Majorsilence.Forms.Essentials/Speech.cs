using System;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>
    /// How <see cref="Speech.SpeakAsync"/> reads a line back (register item F15): an early reader's whole reason this exists is
    /// hearing the words alongside seeing them, so the defaults are unhurried, not a screen reader's clipped pace.
    /// </summary>
    public sealed class SpeechOptions
    {
        /// <summary>Pitch, 0.5 (lower) to 2.0 (higher). 1.0 is the voice's own natural pitch.</summary>
        public float Pitch { get; set; } = 1f;

        /// <summary>Speaking rate, 0.5 (slower) to 2.0 (faster). 1.0 is the voice's own natural rate.</summary>
        public float Rate { get; set; } = 1f;

        /// <summary>Volume, 0 (silent) to 1 (full).</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>A BCP-47 language tag ("en-US"), or null for the platform's current default voice.</summary>
        public string? Locale { get; set; }
    }

    /// <summary>
    /// What a platform actually does the speaking. Internal so <see cref="Speech"/> is the only public surface; a test injects
    /// its own via <see cref="Speech.Backend"/>.
    /// </summary>
    internal interface ISpeechBackend
    {
        /// <summary>Whether this platform can speak at all.</summary>
        bool IsSupported { get; }

        /// <summary>Speaks <paramref name="text"/>, completing when it finishes, is cancelled, or fails.</summary>
        Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Reads text aloud with the platform's own voice (register item F15): optional, for a reader who is still learning to
    /// read, not the app's primary way of telling anyone anything. Every member degrades to doing nothing rather than
    /// throwing when the platform cannot help, the same "never worth a crash" contract <c>Haptics</c> and
    /// <see cref="SecureStorage"/> already use for their own capability gaps. Check <see cref="IsSupported"/> before relying
    /// on a line actually having been read.
    /// </summary>
    public static class Speech
    {
        // Not the Backends.Platform seam every UI-facing capability (Haptics, LocalNotifications, KeepScreenAwake) uses, the
        // same reasoning SecureStorage gives for picking its own backend per target framework directly instead.
        internal static ISpeechBackend Backend { get; set; } = PlatformSpeech.Create ();

        /// <summary>Gets whether this platform can speak at all.</summary>
        public static bool IsSupported => Backend.IsSupported;

        /// <summary>
        /// Speaks <paramref name="text"/> aloud, completing when it finishes, is cancelled through <paramref name="cancellationToken"/>,
        /// or the platform reports a failure. Starting a new utterance does not implicitly stop one already in progress on
        /// most platforms; call with a token from the same source and cancel it first for "interrupt and say this instead".
        /// Does nothing if <see cref="IsSupported"/> is false.
        /// </summary>
        public static Task SpeakAsync (string text, SpeechOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty (text);
            return Backend.SpeakAsync (text, options ?? new SpeechOptions (), cancellationToken);
        }
    }
}
