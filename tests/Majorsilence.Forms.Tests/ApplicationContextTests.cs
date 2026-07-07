using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises ApplicationContext's dispose/ThreadExit contract in isolation from Application.Run —
// Run(ApplicationContext) can only execute once per process (Application.Run throws on a second call
// and there is no reset hook), so it is not exercised here to avoid poisoning every other test in this
// assembly. These tests instead verify the same override points real WinForms exposes
// (ExitThreadCore/OnMainFormClosed/Dispose(bool)), matching the documented contract: a derived context
// can drive its own shutdown (ExitThread) and its Dispose(bool) override is honored.
public class ApplicationContextTests
{
    [Fact]
    public void MainForm_defaults_to_null ()
    {
        using var context = new ApplicationContext ();

        Assert.Null (context.MainForm);
    }

    [Fact]
    public void Constructor_with_mainForm_sets_MainForm ()
    {
        var form = new Form ();
        using var context = new ApplicationContext (form);

        Assert.Same (form, context.MainForm);
    }

    [Fact]
    public void ExitThread_raises_ThreadExit ()
    {
        using var context = new ApplicationContext ();
        var raised = false;
        context.ThreadExit += (s, e) => raised = true;

        context.ExitThread ();

        Assert.True (raised);
    }

    [Fact]
    public void Closing_MainForm_raises_ThreadExit ()
    {
        var form = new Form ();
        using var context = new ApplicationContext (form);
        var raised = false;
        context.ThreadExit += (s, e) => raised = true;

        form.Close ();

        Assert.True (raised);
    }

    [Fact]
    public void Replacing_MainForm_unwires_the_previous_form ()
    {
        var firstForm = new Form ();
        using var context = new ApplicationContext (firstForm);
        context.MainForm = new Form ();

        var raised = false;
        context.ThreadExit += (s, e) => raised = true;

        // Closing the form that MainForm no longer points to must not still drive this context.
        firstForm.Close ();

        Assert.False (raised);
    }

    [Fact]
    public void Dispose_disposes_MainForm ()
    {
        var form = new Form ();
        var context = new ApplicationContext (form);

        context.Dispose ();

        Assert.True (form.IsDisposed);
    }

    [Fact]
    public void Dispose_is_idempotent ()
    {
        var form = new Form ();
        var context = new ApplicationContext (form);

        context.Dispose ();
        context.Dispose (); // must not throw (e.g. double-disposing MainForm)
    }

    [Fact]
    public void Dispose_without_a_MainForm_does_not_throw ()
    {
        var context = new ApplicationContext ();

        context.Dispose ();
    }

    // Matches real WinForms: a derived context overrides ExitThreadCore to redefine what ends the
    // thread's message loop, and Dispose(bool) to release its own resources alongside the base's.
    private sealed class RecordingApplicationContext : ApplicationContext
    {
        public int ExitThreadCoreCalls;
        public int DisposeCalls;
        public bool LastDisposing;

        public void CallExitThread () => ExitThread ();

        protected override void ExitThreadCore ()
        {
            ExitThreadCoreCalls++;
            base.ExitThreadCore ();
        }

        protected override void Dispose (bool disposing)
        {
            DisposeCalls++;
            LastDisposing = disposing;
            base.Dispose (disposing);
        }
    }

    [Fact]
    public void ExitThreadCore_is_overridable ()
    {
        using var context = new RecordingApplicationContext ();

        context.CallExitThread ();

        Assert.Equal (1, context.ExitThreadCoreCalls);
    }

    [Fact]
    public void Dispose_bool_is_overridable_and_receives_true_from_public_Dispose ()
    {
        var context = new RecordingApplicationContext ();

        context.Dispose ();

        Assert.Equal (1, context.DisposeCalls);
        Assert.True (context.LastDisposing);
    }

    [Fact]
    public void MainForm_close_still_invokes_an_overridden_ExitThreadCore ()
    {
        var form = new Form ();
        using var context = new RecordingApplicationContext { MainForm = form };

        form.Close ();

        Assert.Equal (1, context.ExitThreadCoreCalls);
    }
}
