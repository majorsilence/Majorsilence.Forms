using System;
using System.ComponentModel;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// WindowBase.BackRequested (register item F11): the platform back button/gesture, real on Android and iOS
// (no desktop equivalent to raise it from). RaiseBackRequested is exactly what a real backend calls --
// AvaloniaPlatformBackend.RaiseBackRequested on Android/iOS -- so calling it directly here is "a test
// raises a backend back request" (the acceptance criterion), the same shape F10's Suspended/Resumed tests
// use for the same reason: nothing in the Headless backend has an OS back button to press for real.
[Collection ("Headless")]
public class BackRequestedTests
{
    [Fact]
    public void Unhandled_ReturnsFalse_SoThePlatformProceedsNormally ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form ();

        Assert.False (form.RaiseBackRequested ());
    }

    [Fact]
    public void Cancelling_KeepsTheAppOpen ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form ();

        form.BackRequested += (_, e) => e.Cancel = true;

        Assert.True (form.RaiseBackRequested ());
    }

    [Fact]
    public void NotCancelling_StillReturnsFalse ()
    {
        // A handler that runs but does not ask to stay must behave exactly like no handler at all --
        // e.g. a screen that only logs the back press, expecting normal navigation to proceed.
        HeadlessRenderer.Use ();
        using var form = new Form ();

        var raised = 0;
        form.BackRequested += (_, e) => raised++;

        Assert.False (form.RaiseBackRequested ());
        Assert.Equal (1, raised);
    }

    [Fact]
    public void EachWindowsBackRequestedIsIndependent ()
    {
        HeadlessRenderer.Use ();
        using var a = new Form ();
        using var b = new Form ();

        a.BackRequested += (_, e) => e.Cancel = true;
        // b has no handler at all.

        Assert.True (a.RaiseBackRequested ());
        Assert.False (b.RaiseBackRequested ());
    }

    [Fact]
    public void PopupWindowAlsoHasBackRequested ()
    {
        // The acceptance criterion is specifically "closes a sheet without leaving the app" -- a
        // PopupWindow, not just a top-level Form, so this lives on WindowBase, shared by both.
        HeadlessRenderer.Use ();
        using var owner = new Form ();
        owner.Show ();
        using var popup = new PopupWindow (owner);

        popup.BackRequested += (_, e) => e.Cancel = true;

        Assert.True (popup.RaiseBackRequested ());
    }
}
