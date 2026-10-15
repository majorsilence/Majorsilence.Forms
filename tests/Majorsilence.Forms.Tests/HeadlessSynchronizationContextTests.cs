using System.Threading;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Nothing installed a UI SynchronizationContext on the headless backend -- WindowsFormsSynchronizationContext
// .AutoInstall was stored and read by nothing -- so the continuation after an await in an event handler ran
// on the thread pool and touched controls there, where on Avalonia and under WinForms it resumes on the UI
// thread. Found by Recital's UI tests (FRAMEWORK_GAPS GAP-007).
[Collection ("Headless")]
public sealed class HeadlessSynchronizationContextTests
{
    [Fact]
    public void An_await_in_a_handler_resumes_on_the_ui_thread ()
    {
        HeadlessRenderer.Use ();
        using var context = HeadlessRenderer.UseSynchronizationContext ();

        var button = new Button ();
        int? resumedOn = null;
        button.Click += async (_, _) => {
            // Completes on a timer thread: without the context the rest of the handler runs there.
            await Task.Delay (20, TestContext.Current.CancellationToken);
            resumedOn = Environment.CurrentManagedThreadId;
        };

        button.PerformClick ();
        HeadlessRenderer.PumpUntil (() => resumedOn is not null);

        Assert.Equal (Environment.CurrentManagedThreadId, resumedOn);
    }

    [Fact]
    public void Disposing_restores_the_previous_context ()
    {
        HeadlessRenderer.Use ();
        var before = SynchronizationContext.Current;

        using (HeadlessRenderer.UseSynchronizationContext ())
            Assert.IsType<WindowsFormsSynchronizationContext> (SynchronizationContext.Current);

        Assert.Same (before, SynchronizationContext.Current);
    }

    private static SynchronizationContext? ContextInsideTheMainLoop ()
    {
        HeadlessRenderer.Use ();
        var backend = Platform.Backend;
        SynchronizationContext? seen = null;
        backend.Post (() => {
            seen = SynchronizationContext.Current;
            backend.Stop ();
        });
        backend.RunMainLoop (TestContext.Current.CancellationToken);
        return seen;
    }

    [Fact]
    public void The_main_loop_installs_the_context_and_removes_it_afterwards ()
    {
        var before = SynchronizationContext.Current;

        Assert.IsType<WindowsFormsSynchronizationContext> (ContextInsideTheMainLoop ());
        Assert.Same (before, SynchronizationContext.Current);
    }

    [Fact]
    public void AutoInstall_off_leaves_the_main_loop_without_one ()
    {
        WindowsFormsSynchronizationContext.AutoInstall = false;
        try {
            Assert.IsNotType<WindowsFormsSynchronizationContext> (ContextInsideTheMainLoop ());
        } finally {
            WindowsFormsSynchronizationContext.AutoInstall = true;
        }
    }

    [Fact]
    public void PumpUntil_gives_up_after_its_timeout ()
    {
        HeadlessRenderer.Use ();
        Assert.Throws<TimeoutException> (() => HeadlessRenderer.PumpUntil (() => false, TimeSpan.FromMilliseconds (50)));
    }
}
