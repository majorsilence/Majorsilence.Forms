using System;
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
        public async Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            try {
                var tts = await EnsureEngineAsync ().ConfigureAwait (false);
                if (tts is null)
                    return;

                tts.SetPitch (options.Pitch);
                tts.SetSpeechRate (options.Rate);
                if (options.Locale is { Length: > 0 } locale) {
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
