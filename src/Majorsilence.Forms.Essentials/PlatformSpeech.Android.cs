using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Speech.Tts;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Android is <see cref="Android.Speech.Tts.TextToSpeech"/> below.</summary>
    internal static class PlatformSpeech
    {
        public static ISpeechBackend Create () => new AndroidSpeechBackend ();
    }

    /// <summary>
    /// <see cref="TextToSpeech"/>, shared across calls once its (asynchronous) engine initialisation finishes, so the first
    /// <see cref="SpeakAsync"/> pays that cost and every later one does not.
    /// </summary>
    internal sealed class AndroidSpeechBackend : ISpeechBackend
    {
        private static readonly object gate = new ();
        private static TextToSpeech? engine;
        private static Task<TextToSpeech?>? pendingInit;

        /// <inheritdoc />
        public bool IsSupported => true;

        /// <inheritdoc />
        public async Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync (string? language, bool estimateGender)
        {
            try {
                var tts = await EnsureEngineAsync ().ConfigureAwait (false);
                if (tts?.Voices is not { } installed)
                    return [];

                // A voice that is not downloaded yet is left out (it cannot speak), and one that needs the network is listed and flagged, so an app
                // that must work offline can skip it.
                var usable = installed
                    .Where (v => v.Name is { Length: > 0 } && v.Features?.Contains (TextToSpeech.Engine.KeyFeatureNotInstalled) != true)
                    .ToList ();

                var voices = new List<SpeechVoice> ();
                foreach (var v in usable) {
                    // Android does not say a voice's sex. When asked, an offline voice of the wanted language speaks a sample and its pitch says;
                    // anything else stays unknown rather than guessed from its name.
                    var gender = VoiceGender.Unknown;
                    var estimated = false;
                    if (estimateGender && !v.IsNetworkConnectionRequired && IsOfLanguage (v, language)) {
                        gender = await EstimateGenderAsync (tts, v).ConfigureAwait (false);
                        estimated = gender != VoiceGender.Unknown;
                    }

                    voices.Add (new SpeechVoice (v.Name!, FriendlyName (v), v.Locale?.ToLanguageTag () ?? "", gender, v.IsNetworkConnectionRequired, estimated));
                }

                return voices
                    .OrderBy (v => v.Locale, StringComparer.Ordinal)
                    .ThenBy (v => v.Name, StringComparer.Ordinal)
                    .ToList ();
            } catch {
                return [];
            }
        }

        private static bool IsOfLanguage (Voice voice, string? language)
            => string.IsNullOrEmpty (language) || voice.Locale?.ToLanguageTag () is { } tag && tag.StartsWith (language, StringComparison.OrdinalIgnoreCase);

        // What was heard of each voice, kept for the life of the process: sampling takes a second or so a voice.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, VoiceGender> estimates = new ();

        // One sampling at a time, and not while a line is being said: they share the engine's listener and voice.
        private static readonly SemaphoreSlim sampling = new (1, 1);

        private const string SampleLine = "The quick brown fox jumps over the lazy dog. Please tell a grown up now.";

        /// <summary>Has <paramref name="voice"/> speak a sample into a file and calls its sex from the pitch of what it said.</summary>
        private static async Task<VoiceGender> EstimateGenderAsync (TextToSpeech tts, Voice voice)
        {
            if (estimates.TryGetValue (voice.Name!, out var known))
                return known;

            await sampling.WaitAsync ().ConfigureAwait (false);
            try {
                var file = new Java.IO.File (Application.Context.CacheDir, $"voice-sample-{Guid.NewGuid ():N}.wav");
                try {
                    tts.SetVoice (voice);
                    var utteranceId = Guid.NewGuid ().ToString ();
                    var done = new TaskCompletionSource ();
                    tts.SetOnUtteranceProgressListener (new ProgressListener (utteranceId, done));
                    if (tts.SynthesizeToFile (SampleLine, null, file, utteranceId) != OperationResult.Success)
                        return VoiceGender.Unknown;

                    if (await Task.WhenAny (done.Task, Task.Delay (TimeSpan.FromSeconds (10))).ConfigureAwait (false) != done.Task)
                        return VoiceGender.Unknown;

                    var wav = VoicePitch.ReadWav (System.IO.File.ReadAllBytes (file.AbsolutePath));
                    var pitch = wav is { } w ? VoicePitch.Estimate (w.Samples, w.Rate) : null;
                    var gender = VoicePitch.GenderOf (pitch);
                    if (gender != VoiceGender.Unknown)
                        estimates[voice.Name!] = gender;

                    return gender;
                } finally {
                    try { file.Delete (); } catch { }
                    try { if (tts.DefaultVoice is { } own) tts.SetVoice (own); } catch { }
                }
            } catch {
                return VoiceGender.Unknown;
            } finally {
                sampling.Release ();
            }
        }

        // Google's voice names read "en-gb-x-gba-local": the language, then a variant code. "English (United Kingdom) GBA" tells a person more.
        private static string FriendlyName (Voice voice)
        {
            var language = voice.Locale?.DisplayName;
            if (string.IsNullOrEmpty (language))
                return voice.Name!;

            var parts = voice.Name!.Split ('-');
            var x = Array.IndexOf (parts, "x");
            return x >= 0 && x + 1 < parts.Length && parts[x + 1] is not ("local" or "network")
                ? $"{language} {parts[x + 1].ToUpperInvariant ()}"
                : language;
        }

        /// <inheritdoc />
        public async Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            try {
                var tts = await EnsureEngineAsync ().ConfigureAwait (false);
                if (tts is null)
                    return;

                tts.SetPitch (options.Pitch);
                tts.SetSpeechRate (options.Rate);

                // A chosen voice carries its own language, so it wins over Locale; one that is no longer installed falls through to Locale.
                var chosen = options.Voice is { Length: > 0 } voiceId ? tts.Voices?.FirstOrDefault (v => v.Name == voiceId) : null;
                if (chosen is not null) {
                    try { tts.SetVoice (chosen); } catch { }
                } else if (options.Locale is { Length: > 0 } locale) {
                    // Java.Util.Locale itself is flagged obsolete from API 36; there is no other way to build the
                    // BCP-47-to-Locale value TextToSpeech.SetLanguage takes, and the type still works, only deprecated,
                    // the same reasoning F13's own iOS CA1422 suppression documents for a comparable gap.
#pragma warning disable CA1422
                    try { tts.SetLanguage (new Java.Util.Locale (locale.Replace ('-', '_'))); } catch { }
#pragma warning restore CA1422
                }

                var utteranceId = Guid.NewGuid ().ToString ();
                var doneTcs = new TaskCompletionSource ();
                tts.SetOnUtteranceProgressListener (new ProgressListener (utteranceId, doneTcs));

                using var registration = cancellationToken.Register (() => {
                    try { tts.Stop (); } catch { }
                    doneTcs.TrySetResult ();
                });

                var parameters = new Bundle ();
                parameters.PutFloat (TextToSpeech.Engine.KeyParamVolume, Math.Clamp (options.Volume, 0f, 1f));
                tts.Speak (text, QueueMode.Flush, parameters, utteranceId);

                await doneTcs.Task.ConfigureAwait (false);
            } catch {
                // Never worth a crash, per the type summary.
            }
        }

        // The engine's own initialisation is asynchronous (IOnInitListener.OnInit fires later, once), so every caller that
        // arrives before it has finished shares the same pending Task rather than each starting a separate TextToSpeech.
        private static Task<TextToSpeech?> EnsureEngineAsync ()
        {
            lock (gate) {
                if (engine is not null)
                    return Task.FromResult<TextToSpeech?> (engine);

                if (pendingInit is { } pending)
                    return pending;

                var statusTcs = new TaskCompletionSource<bool> ();
                var created = new TextToSpeech (Application.Context, new InitListener (statusTcs));

                pendingInit = statusTcs.Task.ContinueWith (t => {
                    lock (gate) {
                        engine = t.Result ? created : null;
                        pendingInit = null;
                        return engine;
                    }
                }, TaskScheduler.Default);

                return pendingInit;
            }
        }

        private sealed class InitListener (TaskCompletionSource<bool> tcs) : Java.Lang.Object, TextToSpeech.IOnInitListener
        {
            public void OnInit (OperationResult status) => tcs.TrySetResult (status == OperationResult.Success);
        }

        // OnDone/OnError both mean "finished trying"; OnStart needs no action. This binding's UtteranceProgressListener
        // still only declares the deprecated string-only OnError as abstract (there is no separate OnError(string, int) to
        // override instead), so implementing it is required, not a choice -- the error reason itself adds nothing this
        // backend would act on differently.
        private sealed class ProgressListener : UtteranceProgressListener
        {
            private readonly string expectedUtteranceId;
            private readonly TaskCompletionSource doneTcs;

            public ProgressListener (string expectedUtteranceId, TaskCompletionSource doneTcs)
            {
                this.expectedUtteranceId = expectedUtteranceId;
                this.doneTcs = doneTcs;
            }

            public override void OnStart (string? utteranceId) { }

            public override void OnDone (string? utteranceId)
            {
                if (utteranceId == expectedUtteranceId)
                    doneTcs.TrySetResult ();
            }

            [Obsolete]
            public override void OnError (string? utteranceId)
            {
                if (utteranceId == expectedUtteranceId)
                    doneTcs.TrySetResult ();
            }
        }
    }
}
