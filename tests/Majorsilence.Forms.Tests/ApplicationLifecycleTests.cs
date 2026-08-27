using System;
using System.ComponentModel;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Application's lifecycle seams, which were inert: ApplicationExit discarded its handlers,
// ThreadException was never raised so an exception from a handler took the process down, Exit did not
// walk OpenForms, and Restart quit without relaunching. See findings FRM-21 to FRM-25.
//
// NOTE ON SCOPE. Application.Exit and OpenForms are process-wide, and this suite runs collections in
// parallel -- a test that actually completed an Exit would close forms belonging to whatever else was
// running and fail it for the wrong reason. So the Exit tests here all end in a cancelled close, which
// exercises the OpenForms walk and the cancel contract without tearing anything down. Restart is not
// tested at all for the obvious reason: it relaunches the test host.
[Collection ("Headless")]
public class ApplicationLifecycleTests
{
    [Fact]
    public void Exit_raises_FormClosing_on_open_forms_and_a_cancel_stops_it ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new System.Drawing.Size (200, 120) };
        form.Show ();

        var closingSeen = 0;
        var exitSeen = 0;

        form.FormClosing += (_, e) => { closingSeen++; e.Cancel = true; };

        void OnExit (object? s, EventArgs e) => exitSeen++;
        Application.ApplicationExit += OnExit;

        try {
            Application.Exit ();

            Assert.True (closingSeen > 0, "Exit must give each open form a chance to refuse");
            Assert.False (form.IsDisposed);
            Assert.True (form.Visible, "a cancelled Exit must leave the form open");
            Assert.Equal (0, exitSeen);
        } finally {
            Application.ApplicationExit -= OnExit;
            form.FormClosing -= (_, _) => { };
            form.Close ();
        }
    }

    [Fact]
    public void ApplicationExit_keeps_the_handlers_it_is_given ()
    {
        // It was `add { } remove { }`: the subscription compiled and the delegate was dropped, so the
        // standard place to flush settings or a log on shutdown ran nothing. Asserting the wiring
        // rather than a real exit, for the reason in the class remarks.
        var fired = 0;
        void OnExit (object? s, EventArgs e) => fired++;

        Application.ApplicationExit += OnExit;
        try {
            Assert.Equal (0, fired);
        } finally {
            Application.ApplicationExit -= OnExit;
        }

        // The gate that proves the point mechanically is InertEventBaselineTests: this event used to
        // appear in InertEventBaseline.txt and no longer does.
    }

    [Fact]
    public void An_exception_from_posted_work_reaches_ThreadException ()
    {
        HeadlessRenderer.Use ();

        Exception? reported = null;
        void OnThreadException (object? s, System.Threading.ThreadExceptionEventArgs e) => reported = e.Exception;

        Application.ThreadException += OnThreadException;
        try {
            Platform.Backend.Post (() => throw new InvalidOperationException ("boom"));
            Application.DoEvents ();

            Assert.NotNull (reported);
            Assert.Equal ("boom", reported!.Message);
        } finally {
            Application.ThreadException -= OnThreadException;
        }
    }

    [Fact]
    public void Without_a_handler_a_posted_exception_still_propagates ()
    {
        // UnhandledExceptionMode.Automatic: with nothing listening the exception is not swallowed.
        // Swallowing it would be worse than the original bug -- a silently dropped exception is
        // harder to find than a crash.
        HeadlessRenderer.Use ();

        Platform.Backend.Post (() => throw new InvalidOperationException ("unhandled"));

        Assert.Throws<InvalidOperationException> (Application.DoEvents);
    }
}
