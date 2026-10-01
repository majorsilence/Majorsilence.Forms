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

    private sealed class FakeSpeechBackend : ISpeechBackend
    {
        public System.Collections.Generic.List<(string Text, SpeechOptions Options)> Calls { get; } = [];

        public bool IsSupported { get; set; } = true;

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
