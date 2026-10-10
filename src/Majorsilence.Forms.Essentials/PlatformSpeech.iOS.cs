using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AVFoundation;
using Foundation;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. iOS is <see cref="AVSpeechSynthesizer"/> below.</summary>
    internal static class PlatformSpeech
    {
        public static ISpeechBackend Create () => new IosSpeechBackend ();
    }

    /// <summary>
    /// <see cref="AVSpeechSynthesizer"/>, shared across calls: creating one per call would drop whatever an earlier call was
    /// still saying the instant it went out of scope.
    /// </summary>
    internal sealed class IosSpeechBackend : ISpeechBackend
    {
        private static readonly AVSpeechSynthesizer synthesizer = new ();

        /// <inheritdoc />
        public bool IsSupported => true;

        /// <inheritdoc />
        public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync ()
        {
            try {
                IReadOnlyList<SpeechVoice> voices = AVSpeechSynthesisVoice.GetSpeechVoices ()
                    .Select (v => new SpeechVoice (v.Identifier, v.Name, v.Language, GenderOf (v)))
                    .OrderBy (v => v.Locale, StringComparer.Ordinal)
                    .ThenBy (v => v.Name, StringComparer.Ordinal)
                    .ToList ();
                return Task.FromResult (voices);
            } catch {
                return Task.FromResult<IReadOnlyList<SpeechVoice>> ([]);
            }
        }

        // The voice's sex is reported from iOS 17; before that it is not said.
        private static VoiceGender GenderOf (AVSpeechSynthesisVoice voice)
        {
            if (!OperatingSystem.IsIOSVersionAtLeast (17))
                return VoiceGender.Unknown;

            return voice.Gender switch {
                AVSpeechSynthesisVoiceGender.Male => VoiceGender.Male,
                AVSpeechSynthesisVoiceGender.Female => VoiceGender.Female,
                _ => VoiceGender.Unknown,
            };
        }

        /// <inheritdoc />
        public Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            try {
                var utterance = new AVSpeechUtterance (text) {
                    PitchMultiplier = Math.Clamp (options.Pitch, 0.5f, 2f),
                    // AVSpeechUtterance's own Rate is 0 (slowest) to 1 (fastest), around the default AVSpeechUtteranceDefaultSpeechRate
                    // (~0.5); this backend's own 0.5..2.0 scale maps onto it around that same centre point.
                    Rate = Math.Clamp (AVSpeechUtterance.DefaultSpeechRate * options.Rate, AVSpeechUtterance.MinimumSpeechRate, AVSpeechUtterance.MaximumSpeechRate),
                    Volume = Math.Clamp (options.Volume, 0f, 1f),
                };

                // A chosen voice wins over a language; one that is no longer installed falls through to the language.
                var chosen = options.Voice is { Length: > 0 } voiceId ? AVSpeechSynthesisVoice.FromIdentifier (voiceId) : null;
                if (chosen is not null)
                    utterance.Voice = chosen;
                else if (options.Locale is { Length: > 0 } locale)
                    utterance.Voice = AVSpeechSynthesisVoice.FromLanguage (locale);

                var doneTcs = new TaskCompletionSource ();
                synthesizer.DidFinishSpeechUtterance += OnFinished;
                synthesizer.DidCancelSpeechUtterance += OnFinished;

                void OnFinished (object? sender, AVSpeechSynthesizerUteranceEventArgs e)
                {
                    if (!ReferenceEquals (e.Utterance, utterance))
                        return;

                    synthesizer.DidFinishSpeechUtterance -= OnFinished;
                    synthesizer.DidCancelSpeechUtterance -= OnFinished;
                    doneTcs.TrySetResult ();
                }

                using var registration = cancellationToken.Register (() => {
                    try { synthesizer.StopSpeaking (AVSpeechBoundary.Immediate); } catch { }
                });

                synthesizer.SpeakUtterance (utterance);
                return doneTcs.Task;
            } catch {
                // Never worth a crash, per the type summary.
                return Task.CompletedTask;
            }
        }
    }
}
