using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Majorsilence.Forms.Essentials;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Launcher: one call that hands a URI to another app on Android, iOS and desktop. The public surface -- scheme allow-list, the
// "never throws" contract -- is exercised against a fake backend; the framework's suite has no browser to launch.
public class LauncherTests
{
    private sealed class FakeLauncherBackend : ILauncherBackend
    {
        public bool IsSupported { get; set; } = true;
        public bool Accept { get; set; } = true;
        public bool Throw { get; set; }
        public List<Uri> Opened { get; } = new ();

        public Task<bool> OpenAsync (Uri uri)
        {
            if (Throw)
                throw new InvalidOperationException ("no browser");
            Opened.Add (uri);
            return Task.FromResult (Accept);
        }
    }

    private static async Task<T> WithBackend<T> (FakeLauncherBackend fake, Func<Task<T>> body)
    {
        var previous = Launcher.Backend;
        Launcher.Backend = fake;
        try {
            return await body ();
        } finally {
            Launcher.Backend = previous;
        }
    }

    [Theory]
    [InlineData ("http://example.com/a")]
    [InlineData ("https://example.com/a?b=c")]
    [InlineData ("HTTPS://EXAMPLE.COM")]
    [InlineData ("mailto:someone@example.com")]
    [InlineData ("tel:+15551234567")]
    [InlineData ("sms:+15551234567")]
    public void CanOpen_accepts_the_web_mail_and_phone_schemes (string uri)
        => Assert.True (Launcher.CanOpen (new Uri (uri)));

    [Theory]
    [InlineData ("file:///etc/passwd")]
    [InlineData ("javascript:alert(1)")]
    [InlineData ("ms-settings:network")]
    [InlineData ("myapp://do/something")]
    public void CanOpen_refuses_other_schemes (string uri)
        => Assert.False (Launcher.CanOpen (new Uri (uri)));

    [Fact]
    public void CanOpen_refuses_null_and_relative_uris ()
    {
        Assert.False (Launcher.CanOpen (null));
        Assert.False (Launcher.CanOpen (new Uri ("/relative/path", UriKind.Relative)));
    }

    [Fact]
    public async Task OpenAsync_forwards_an_allowed_uri_to_the_backend ()
    {
        var fake = new FakeLauncherBackend ();
        var result = await WithBackend (fake, () => Launcher.OpenAsync (new Uri ("https://example.com/post")));

        Assert.True (result);
        Assert.Equal (new Uri ("https://example.com/post"), Assert.Single (fake.Opened));
    }

    [Fact]
    public async Task OpenAsync_never_reaches_the_backend_for_a_refused_scheme ()
    {
        var fake = new FakeLauncherBackend ();
        var result = await WithBackend (fake, () => Launcher.OpenAsync (new Uri ("file:///etc/passwd")));

        Assert.False (result);
        Assert.Empty (fake.Opened);
    }

    [Fact]
    public async Task OpenAsync_returns_false_when_the_platform_is_unsupported ()
    {
        var fake = new FakeLauncherBackend { IsSupported = false };
        var result = await WithBackend (fake, () => Launcher.OpenAsync (new Uri ("https://example.com")));

        Assert.False (result);
        Assert.Empty (fake.Opened);
    }

    [Fact]
    public async Task OpenAsync_returns_false_when_no_app_handles_the_uri ()
    {
        var fake = new FakeLauncherBackend { Accept = false };
        Assert.False (await WithBackend (fake, () => Launcher.OpenAsync (new Uri ("https://example.com"))));
    }

    [Fact]
    public async Task OpenAsync_swallows_a_backend_exception ()
    {
        var fake = new FakeLauncherBackend { Throw = true };
        Assert.False (await WithBackend (fake, () => Launcher.OpenAsync (new Uri ("https://example.com"))));
    }

    [Fact]
    public async Task OpenAsync_text_overload_parses_and_returns_false_for_garbage ()
    {
        var fake = new FakeLauncherBackend ();
        Assert.True (await WithBackend (fake, () => Launcher.OpenAsync ("https://example.com")));
        Assert.False (await WithBackend (fake, () => Launcher.OpenAsync ("not a uri")));
        Assert.Single (fake.Opened);
    }
}
