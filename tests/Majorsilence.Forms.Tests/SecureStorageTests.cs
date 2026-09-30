using System;
using System.Threading.Tasks;
using Majorsilence.Forms.Essentials;
using Xunit;

namespace Majorsilence.Forms.Tests;

// SecureStorage (register item F16): a password or token in the platform's own secure store, never a plain file. The public
// surface is exercised here against a fake backend (the framework's own test suite has no OS keystore to depend on for logic
// that has nothing to do with any real one); DesktopSecureStorageBackend's OS dispatch and its real behavior on whichever OS
// this suite actually runs on (Linux in this repository's own CI) get their own section below, the same split
// KeepScreenAwakeTests uses for DesktopKeepAwake.
public class SecureStorageTests
{
    [Fact]
    public async Task GetAsync_returns_what_SetAsync_stored ()
    {
        var fake = new FakeSecureStorageBackend ();
        var previous = SecureStorage.Backend;
        SecureStorage.Backend = fake;
        try {
            await SecureStorage.SetAsync ("ntfy.password", "hunter2");
            Assert.Equal ("hunter2", await SecureStorage.GetAsync ("ntfy.password"));
        } finally {
            SecureStorage.Backend = previous;
        }
    }

    [Fact]
    public async Task GetAsync_returns_null_for_a_key_never_set ()
    {
        var fake = new FakeSecureStorageBackend ();
        var previous = SecureStorage.Backend;
        SecureStorage.Backend = fake;
        try {
            Assert.Null (await SecureStorage.GetAsync ("never-set"));
        } finally {
            SecureStorage.Backend = previous;
        }
    }

    [Fact]
    public async Task Remove_deletes_the_value_and_removing_an_absent_key_is_not_an_error ()
    {
        var fake = new FakeSecureStorageBackend ();
        var previous = SecureStorage.Backend;
        SecureStorage.Backend = fake;
        try {
            await SecureStorage.SetAsync ("ntfy.token", "abc123");
            SecureStorage.Remove ("ntfy.token");
            Assert.Null (await SecureStorage.GetAsync ("ntfy.token"));

            var exception = Record.Exception (() => SecureStorage.Remove ("ntfy.token"));
            Assert.Null (exception);
        } finally {
            SecureStorage.Backend = previous;
        }
    }

    [Fact]
    public void IsSupported_reflects_the_backend ()
    {
        var previous = SecureStorage.Backend;
        SecureStorage.Backend = new FakeSecureStorageBackend { IsSupported = false };
        try {
            Assert.False (SecureStorage.IsSupported);
        } finally {
            SecureStorage.Backend = previous;
        }
    }

    [Theory]
    [InlineData (null)]
    [InlineData ("")]
    public async Task GetAsync_rejects_a_null_or_empty_key (string? key)
    {
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentNullException for null specifically (a subtype), ArgumentException
        // for empty; ThrowsAny accepts either, since both are "rejected", which is all this asserts.
        await Assert.ThrowsAnyAsync<ArgumentException> (() => SecureStorage.GetAsync (key!));
    }

    [Theory]
    [InlineData (null)]
    [InlineData ("")]
    public async Task SetAsync_rejects_a_null_or_empty_key (string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException> (() => SecureStorage.SetAsync (key!, "value"));
    }

    [Fact]
    public async Task SetAsync_rejects_a_null_value ()
    {
        await Assert.ThrowsAsync<ArgumentNullException> (() => SecureStorage.SetAsync ("key", null!));
    }

    [Theory]
    [InlineData (null)]
    [InlineData ("")]
    public void Remove_rejects_a_null_or_empty_key (string? key)
    {
        Assert.ThrowsAny<ArgumentException> (() => SecureStorage.Remove (key!));
    }

    private sealed class FakeSecureStorageBackend : ISecureStorageBackend
    {
        private readonly System.Collections.Generic.Dictionary<string, string> values = new ();

        public bool IsSupported { get; set; } = true;

        public Task<string?> GetAsync (string key) => Task.FromResult (values.GetValueOrDefault (key));

        public Task SetAsync (string key, string value)
        {
            values[key] = value;
            return Task.CompletedTask;
        }

        public void Remove (string key) => values.Remove (key);
    }

    // ---- DesktopSecureStorageBackend.Dispatch: which OS runs, with the OS itself faked ---------------

    [Fact]
    public void Dispatch_asks_only_the_matching_OSs_branch ()
    {
        // linuxPredicateCalled (not an exception from isLinux) is what actually catches a missing short-circuit: a mutant that
        // lets execution fall through to isLinux after macOS already matched, without also calling onLinux, would otherwise
        // still leave linuxCalls at 0 and pass -- proven by mutation-testing this exact case against a throwing isLinux, which
        // let the fall-through's own exception get silently swallowed by Dispatch's catch instead of failing the test.
        var windowsCalls = 0; var macCalls = 0; var linuxCalls = 0; var linuxPredicateCalled = false;

        DesktopSecureStorageBackend.Dispatch<object?> (
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

        DesktopSecureStorageBackend.Dispatch<object?> (
            () => { order.Add ("windows"); return false; },
            () => { order.Add ("macos"); return false; },
            () => { order.Add ("linux"); return true; },
            () => null, () => null, () => null);

        Assert.Equal (new[] { "windows", "macos", "linux" }, order);
    }

    [Fact]
    public void Dispatch_returns_default_when_no_predicate_matches ()
    {
        var result = DesktopSecureStorageBackend.Dispatch (() => false, () => false, () => false,
            () => "windows", () => "macos", () => "linux");

        Assert.Null (result);
    }

    [Fact]
    public void Dispatch_swallows_the_matched_branchs_exception_and_returns_default ()
    {
        var exception = Record.Exception (() => DesktopSecureStorageBackend.Dispatch<object?> (() => false, () => false, () => true,
            () => throw new InvalidOperationException (), () => throw new InvalidOperationException (), () => throw new InvalidOperationException ("boom")));

        Assert.Null (exception);
    }

    // ---- The real desktop backend on whichever OS this suite actually runs on ------------------------

    [Fact]
    public async Task The_real_desktop_backend_round_trips_when_supported_and_degrades_gracefully_when_not ()
    {
        // Exercises whichever OS branch this machine actually is (Linux in this repository's own CI), and proves the other
        // two branches' P/Invoke declarations at least load and bind correctly wherever this assembly runs, the same
        // reasoning KeepScreenAwakeTests.The_real_desktop_set_never_throws_regardless_of_host documents. Linux specifically:
        // IsSupported is false without a running Secret Service (no libsecret-tools, or no keyring daemon -- true of this
        // repository's own CI and of a typical headless dev box), and every member must still degrade rather than throw.
        var backend = new DesktopSecureStorageBackend ();
        var key = $"majorsilence-forms-tests-{Guid.NewGuid ():N}";

        try {
            if (backend.IsSupported) {
                await backend.SetAsync (key, "round-trip-value");
                Assert.Equal ("round-trip-value", await backend.GetAsync (key));

                backend.Remove (key);
                Assert.Null (await backend.GetAsync (key));
            } else {
                var setException = await Record.ExceptionAsync (() => backend.SetAsync (key, "value"));
                Assert.Null (setException);

                Assert.Null (await backend.GetAsync (key));

                var removeException = Record.Exception (() => backend.Remove (key));
                Assert.Null (removeException);
            }
        } finally {
            backend.Remove (key);
        }
    }
}
