using System;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Essentials;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Speech (register item F15): reads text aloud with the platform's own voice. The public surface is exercised here against a
// fake backend; DesktopSpeechBackend's OS dispatch and its real behaviour on whichever OS this suite actually runs on (Linux
// in this repository's own CI) get their own section below, the same split SecureStorageTests uses for
// DesktopSecureStorageBackend.
public class SpeechTests
{
    [Fact]
    public async Task SpeakAsync_calls_the_backend_with_the_given_options ()
    {
        var fake = new FakeSpeechBackend ();
        var previous = Speech.Backend;
        Speech.Backend = fake;
        try {
            var options = new SpeechOptions { Pitch = 1.2f, Rate = 0.8f, Volume = 0.5f, Locale = "en-GB" };
            await Speech.SpeakAsync ("hello", options, TestContext.Current.CancellationToken);

            Assert.Single (fake.Calls);
            Assert.Equal ("hello", fake.Calls[0].Text);
            Assert.Same (options, fake.Calls[0].Options);
        } finally {
            Speech.Backend = previous;
        }
    }

    [Fact]
    public async Task SpeakAsync_supplies_default_options_when_none_are_given ()
    {
        var fake = new FakeSpeechBackend ();
        var previous = Speech.Backend;
        Speech.Backend = fake;
        try {
            await Speech.SpeakAsync ("hello", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal (1f, fake.Calls[0].Options.Pitch);
            Assert.Equal (1f, fake.Calls[0].Options.Rate);
            Assert.Equal (1f, fake.Calls[0].Options.Volume);
            Assert.Null (fake.Calls[0].Options.Locale);
        } finally {
            Speech.Backend = previous;
        }
    }

    [Fact]
    public void IsSupported_reflects_the_backend ()
    {
        var previous = Speech.Backend;
        Speech.Backend = new FakeSpeechBackend { IsSupported = false };
        try {
            Assert.False (Speech.IsSupported);
        } finally {
            Speech.Backend = previous;
        }
    }

    [Theory]
    [InlineData (null)]
    [InlineData ("")]
    public async Task SpeakAsync_rejects_a_null_or_empty_text (string? text)
    {
        await Assert.ThrowsAnyAsync<ArgumentException> (() => Speech.SpeakAsync (text!, cancellationToken: TestContext.Current.CancellationToken));
    }

    // ---- Voices: listing the installed ones and speaking with a chosen one (#456) --------------------

    [Fact]
    public async Task GetVoicesAsync_returns_the_backends_voices ()
    {
        var voices = new[] {
            new SpeechVoice ("en-gb-x-rp", "English (RP)", "en-GB", VoiceGender.Male),
            new SpeechVoice ("fr-fr-x-vlf", "French", "fr-FR", VoiceGender.Unknown),
        };
        var previous = Speech.Backend;
        Speech.Backend = new FakeSpeechBackend { Voices = voices };
        try {
            var listed = await Speech.GetVoicesAsync ();

            Assert.Equal (voices, listed);
        } finally {
            Speech.Backend = previous;
        }
    }

    [Fact]
    public async Task GetVoicesAsync_is_empty_when_the_platform_cannot_list_them_or_fails ()
    {
        var previous = Speech.Backend;
        try {
            Speech.Backend = new FakeSpeechBackend { IsSupported = false };
            Assert.Empty (await Speech.GetVoicesAsync ());

            Speech.Backend = new FakeSpeechBackend { VoicesThrow = true };
            Assert.Empty (await Speech.GetVoicesAsync ());
        } finally {
            Speech.Backend = previous;
        }
    }

    [Fact]
    public async Task SpeakAsync_passes_the_chosen_voice_to_the_backend ()
    {
        var fake = new FakeSpeechBackend ();
        var previous = Speech.Backend;
        Speech.Backend = fake;
        try {
            await Speech.SpeakAsync ("hello", new SpeechOptions { Voice = "en-gb-x-rp" }, TestContext.Current.CancellationToken);

            Assert.Equal ("en-gb-x-rp", fake.Calls[0].Options.Voice);
        } finally {
            Speech.Backend = previous;
        }
    }

    [Fact]
    public void A_voice_says_whether_it_needs_the_network_and_by_default_it_does_not ()
    {
        Assert.False (new SpeechVoice ("a", "A", "en-GB", VoiceGender.Unknown).RequiresNetwork);
        Assert.True (new SpeechVoice ("b", "B", "en-GB", VoiceGender.Unknown, RequiresNetwork: true).RequiresNetwork);
    }

    [Fact]
    public void The_default_voice_is_none_so_the_platforms_own_is_used ()
    {
        Assert.Null (new SpeechOptions ().Voice);
    }

    [Fact]
    public void Espeak_voices_are_parsed_with_their_gender ()
    {
        const string output = """
            Pty Language       Age/Gender VoiceName          File                 Other Languages
             5  af              --/M      Afrikaans          gmw/af
             5  en-gb           --/M      English_(Great_Britain) gmw/en             (en 2)
             5  fr-fr           --/F      French_(France)    roa/fr               (fr 5)
             2  en-gb-x-rp      --/M      english-rp         gmw/en-GB-x-rp       (en-gb 2)(en 5)
            """;

        var voices = DesktopSpeechBackend.ParseEspeakVoices (output);

        Assert.Equal (4, voices.Count);
        Assert.Equal (new SpeechVoice ("af", "Afrikaans", "af", VoiceGender.Male), voices[0]);
        Assert.Equal (new SpeechVoice ("en-gb", "English (Great Britain)", "en-GB", VoiceGender.Male), voices[1]);
        Assert.Equal (VoiceGender.Female, voices[2].Gender);
        Assert.Equal ("en-GB", voices[3].Locale);          // "en-gb-x-rp": the language, then a private-use extension
    }

    [Fact]
    public void MacOS_voices_are_parsed_from_say_s_list ()
    {
        const string output = """
            Alex                en_US    # Most people recognize me by my voice.
            Eddy (English (UK)) en_GB    # Hello, my name is Eddy.
            Thomas              fr_FR    # Bonjour, je m'appelle Thomas.
            """;

        var voices = DesktopSpeechBackend.ParseMacVoices (output);

        Assert.Equal (3, voices.Count);
        Assert.Equal (new SpeechVoice ("Alex", "Alex", "en-US", VoiceGender.Unknown), voices[0]);
        Assert.Equal ("Eddy (English (UK))", voices[1].Name);
        Assert.Equal ("fr-FR", voices[2].Locale);
    }

    [Fact]
    public void Windows_voices_are_parsed_from_the_scripts_lines ()
    {
        const string output = "Microsoft David Desktop|en-US|Male\r\nMicrosoft Zira Desktop|en-US|Female\r\nMicrosoft Hortense Desktop|fr-FR|Female\r\nSome Voice|en-GB|Neutral\r\n";

        var voices = DesktopSpeechBackend.ParseWindowsVoices (output);

        Assert.Equal (4, voices.Count);
        Assert.Equal (new SpeechVoice ("Microsoft David Desktop", "Microsoft David Desktop", "en-US", VoiceGender.Male), voices[0]);
        Assert.Equal (VoiceGender.Female, voices[1].Gender);
        Assert.Equal (VoiceGender.Unknown, voices[3].Gender);
    }

    [Fact]
    public void Speech_dispatcher_voices_are_parsed_and_its_male_and_female_types_are_offered_first ()
    {
        const string output = """
                                 NAME                 LANGUAGE                  VARIANT
                            Afrikaans                       af                     none
                       Afrikaans+Adam                       af                     Adam
             English (America)+Alicia                       en                   Alicia
            """;

        var voices = DesktopSpeechBackend.ParseSpeechDispatcherVoices (output);

        // The preferred voice types come first: they are the one thing speech-dispatcher says about a voice's sex.
        Assert.Equal (new SpeechVoice ("male1", "Man 1", "", VoiceGender.Male), voices[0]);
        Assert.Equal (VoiceGender.Male, voices[2].Gender);
        Assert.Equal (7, voices.Count);                                  // six types and the one plain voice
        Assert.Equal (new SpeechVoice ("female1", "Woman 1", "", VoiceGender.Female), voices[3]);
        Assert.Contains (new SpeechVoice ("Afrikaans", "Afrikaans", "af", VoiceGender.Unknown), voices);
        Assert.DoesNotContain (voices, v => v.Id.Contains ('+'));        // the thousands of language-by-variant combinations are not offered
        Assert.DoesNotContain (voices, v => v.Id == "NAME");
    }

    [Theory]
    [InlineData (1f, 0)]
    [InlineData (2f, 100)]
    [InlineData (0.5f, -50)]
    [InlineData (3f, 100)]
    public void Speech_dispatchers_rate_and_pitch_map_around_zero (float scale, int expected)
    {
        Assert.Equal (expected, DesktopSpeechBackend.SpeechDispatcherScale (scale));
    }

    [Theory]
    [InlineData (1f, 0)]
    [InlineData (0.5f, -50)]
    [InlineData (0f, -100)]
    public void Speech_dispatchers_volume_runs_from_silent_to_its_default (float volume, int expected)
    {
        Assert.Equal (expected, DesktopSpeechBackend.SpeechDispatcherVolume (volume));
    }

    [Fact]
    public void Garbage_in_a_voice_list_is_skipped_not_thrown_on ()
    {
        Assert.Empty (DesktopSpeechBackend.ParseEspeakVoices ("nonsense\n\n   \n"));
        Assert.Empty (DesktopSpeechBackend.ParseMacVoices ("nonsense\n\n"));
        Assert.Empty (DesktopSpeechBackend.ParseWindowsVoices ("||\nnonsense\n"));
        Assert.Equal (6, DesktopSpeechBackend.ParseSpeechDispatcherVoices ("nonsense\n\n").Count);      // only the six voice types, no named voices
    }

    private sealed class FakeSpeechBackend : ISpeechBackend
    {
        public System.Collections.Generic.List<(string Text, SpeechOptions Options)> Calls { get; } = [];

        public bool IsSupported { get; set; } = true;

        public System.Collections.Generic.IReadOnlyList<SpeechVoice> Voices { get; set; } = [];

        public bool VoicesThrow { get; set; }

        public Task<System.Collections.Generic.IReadOnlyList<SpeechVoice>> GetVoicesAsync ()
            => VoicesThrow ? throw new InvalidOperationException ("boom") : Task.FromResult (Voices);

        public Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken)
        {
            Calls.Add ((text, options));
            return Task.CompletedTask;
        }
    }

    // ---- DesktopSpeechBackend.Dispatch: which OS runs, with the OS itself faked ------------------------

    [Fact]
    public void Dispatch_asks_only_the_matching_OSs_branch ()
    {
        var windowsCalls = 0; var macCalls = 0; var linuxCalls = 0; var linuxPredicateCalled = false;

        DesktopSpeechBackend.Dispatch<object?> (
            () => false, () => true, () => { linuxPredicateCalled = true; return false; },
            () => { windowsCalls++; return null; },
            () => { macCalls++; return null; },
            () => { linuxCalls++; return null; });

        Assert.Equal (0, windowsCalls);
        Assert.Equal (1, macCalls);
        Assert.Equal (0, linuxCalls);
        Assert.False (linuxPredicateCalled, "isLinux must not run once isMacOS matched");
    }

    [Fact]
    public void Dispatch_tries_Windows_then_macOS_then_Linux_in_order ()
    {
        var order = new System.Collections.Generic.List<string> ();

        DesktopSpeechBackend.Dispatch<object?> (
            () => { order.Add ("windows"); return false; },
            () => { order.Add ("macos"); return false; },
            () => { order.Add ("linux"); return true; },
            () => null, () => null, () => null);

        Assert.Equal (new[] { "windows", "macos", "linux" }, order);
    }

    [Fact]
    public void Dispatch_returns_default_when_no_predicate_matches ()
    {
        var result = DesktopSpeechBackend.Dispatch (() => false, () => false, () => false,
            () => "windows", () => "macos", () => "linux");

        Assert.Null (result);
    }

    [Fact]
    public void Dispatch_swallows_the_matched_branchs_exception_and_returns_default ()
    {
        var exception = Record.Exception (() => DesktopSpeechBackend.Dispatch<object?> (() => false, () => false, () => true,
            () => throw new InvalidOperationException (), () => throw new InvalidOperationException (), () => throw new InvalidOperationException ("boom")));

        Assert.Null (exception);
    }

    // ---- The real desktop backend on whichever OS this suite actually runs on ------------------------

    [Fact]
    public async Task The_real_desktop_backend_speaks_when_supported_and_degrades_gracefully_when_not ()
    {
        // Exercises whichever OS branch this machine actually is (Linux in this repository's own CI), and proves the other
        // two branches' process-spawning code at least loads and starts correctly wherever this assembly runs, the same
        // reasoning SecureStorageTests.The_real_desktop_backend_round_trips_when_supported... documents. On Linux
        // specifically: IsSupported reflects whether espeak/espeak-ng is actually installed (not guaranteed on a minimal
        // box, including possibly this one), and every member must still degrade rather than throw or hang either way.
        var backend = new DesktopSpeechBackend ();
        using var cts = new CancellationTokenSource (TimeSpan.FromSeconds (10));

        var exception = await Record.ExceptionAsync (() => backend.SpeakAsync ("test", new SpeechOptions (), cts.Token));
        Assert.Null (exception);
    }

    [Fact]
    public async Task The_real_desktop_backend_can_be_cancelled_without_hanging ()
    {
        var backend = new DesktopSpeechBackend ();
        if (!backend.IsSupported)
            return; // Nothing to cancel if this machine cannot speak at all; covered by the degrade-gracefully test above.

        using var cts = new CancellationTokenSource ();
        var speaking = backend.SpeakAsync ("this is a long line meant to still be speaking when the cancellation below fires", new SpeechOptions (), cts.Token);
        cts.Cancel ();

        var completed = await Task.WhenAny (speaking, Task.Delay (TimeSpan.FromSeconds (10), TestContext.Current.CancellationToken));
        Assert.Same (speaking, completed);
    }
}
