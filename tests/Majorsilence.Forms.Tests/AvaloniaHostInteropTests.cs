using System;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Guards the WindowBase.Show()/ShowDialog() → EnsureShownBookkeeping refactor (see WindowBase.cs) and
// the Avalonia host-interop guard rail (AvaloniaHostInterop.ToAvaloniaWindow), both introduced to let a
// host Avalonia/Uno app use a Form as a native window (see docs/backends.md, "Embedding in a host
// app"). The Uno side (UnoHostInterop.ToUnoWindow) needs a running Uno Application/DispatcherQueue and
// isn't referenced by this project, so it's verified manually via samples/EmbeddingUno instead.
public class AvaloniaHostInteropTests
{
    [Fact]
    public void ToAvaloniaWindow_Throws_WhenNotAvaloniaBackend ()
    {
        // The test assembly's ModuleInitializer selects the Headless backend, so this Form's Backend
        // is a HeadlessWindowHost, not a real Avalonia.Controls.Window.
        var form = new Form ();

        var ex = Assert.Throws<InvalidOperationException> (() => form.ToAvaloniaWindow ());
        Assert.Contains ("Avalonia", ex.Message);

        form.Close ();
    }

    [Fact]
    public void Show_CalledTwice_OnlyRaisesShownOnce ()
    {
        var form = new Form ();
        var shownCount = 0;
        form.Shown += (_, _) => shownCount++;

        form.Show ();
        form.Show ();

        Assert.Equal (1, shownCount);

        form.Close ();
    }
}
