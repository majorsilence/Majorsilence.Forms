using System;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Regression: the synchronous ShowDialog overloads waited on their async body with a blocking
// sync-over-async helper (Task.Factory.StartNew(...).GetAwaiter().GetResult()). A backend's file
// picker has to be driven from the UI thread and completes on it, so blocking that thread meant the
// picker never appeared and the application hung -- reported against a migrated media player whose
// "Load file" button froze it outright. They now pump a nested message loop while waiting, the same
// way Form.ShowDialog(Form) always has.
public class FileDialogModalPumpTests
{
    // Stands in for a real backend's picker: the result is delivered through work posted to the UI
    // queue, which only drains while the thread is pumping. A blocking wait can never observe it.
    private sealed class DispatcherCompletedFileDialog : FileDialog
    {
        public override Task<DialogResult> ShowDialogAsync(Form owner)
        {
            var completion = new TaskCompletionSource<DialogResult>();
            Platform.Backend.Post(() => completion.SetResult(DialogResult.OK));
            return completion.Task;
        }
    }

    private sealed class DispatcherCompletedFolderDialog : FolderBrowserDialog
    {
        public new Task<DialogResult> ShowDialogAsync(Form owner)
        {
            var completion = new TaskCompletionSource<DialogResult>();
            Platform.Backend.Post(() => completion.SetResult(DialogResult.OK));
            return completion.Task;
        }
    }

    private static DialogResult RunOnPumpingThread(Func<DialogResult> show)
    {
        // On a background thread with a join timeout so a regression fails the test instead of
        // hanging the whole suite.
        var result = DialogResult.None;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try { result = show(); }
            catch (Exception ex) { failure = ex; }
        })
        { IsBackground = true };

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)),
            "the synchronous ShowDialog deadlocked: it blocked the thread that had to deliver its result");

        if (failure is not null)
            throw failure;

        return result;
    }

    [Fact]
    public void FileDialog_ShowDialogSync_completes_when_the_result_arrives_on_the_dispatcher()
    {
        using var form = new Form();
        form.Show();
        var dialog = new DispatcherCompletedFileDialog();

        var result = RunOnPumpingThread(() => dialog.ShowDialogSync(form));

        Assert.Equal(DialogResult.OK, result);
    }

    [Fact]
    public void FileDialog_ShowDialog_with_owner_completes_too()
    {
        using var form = new Form();
        form.Show();
        var dialog = new DispatcherCompletedFileDialog();

        var result = RunOnPumpingThread(() => dialog.ShowDialog(form));

        Assert.Equal(DialogResult.OK, result);
    }

    [Fact]
    public void FolderBrowserDialog_ShowDialog_without_an_open_form_returns_Cancel_rather_than_throwing()
    {
        // The no-argument overload used to dereference Application.OpenForms.LastOrDefault() with a
        // null-forgiving operator, so it threw when nothing was open; FileDialog's equivalent
        // already returned Cancel.
        var openForms = Application.OpenForms.Count;
        if (openForms != 0)
            return;

        Assert.Equal(DialogResult.Cancel, new FolderBrowserDialog().ShowDialog());
    }
}
