using System.Text;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// On Android (a content:// URI), iOS (a security-scoped URL) and in the browser (no file system) a picked
// file has no path that File.OpenRead can open. The backends returned only paths -- the Avalonia one
// mapped each storage item through Uri.LocalPath, a path that names nothing on Android and is null in the
// browser -- and OpenFile () was File.OpenRead (FileName), so an app could not read a file the user had
// just picked. Found by Recital's library import (FRAMEWORK_GAPS GAP-005).
[Collection ("Headless")]
public sealed class OpenFileDialogPickedFileTests : IDisposable
{
    public OpenFileDialogPickedFileTests () => HeadlessRenderer.Use ();

    public void Dispose ()
    {
        HeadlessRenderer.OpenFileResponse = null;
        HeadlessRenderer.OpenFilePickResponse = null;
    }

    private static PickedFile InMemory (string name, string text, bool completesAtOnce = true)
        => new (name, null, async ct => {
            if (!completesAtOnce)
                await Task.Yield ();
            return new MemoryStream (Encoding.UTF8.GetBytes (text));
        });

    private static Form ShownForm ()
    {
        var form = new Form ();
        form.Show ();
        return form;
    }

    private static async Task<string> ReadAll (Stream stream)
    {
        using (stream)
        using (var reader = new StreamReader (stream))
            return await reader.ReadToEndAsync (TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_file_with_no_path_is_named_and_readable ()
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFilePickResponse = _ => new[] { InMemory ("Alice.epub", "contents") };

        using var dialog = new OpenFileDialog { Filter = "Books|*.epub" };

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal ("Alice.epub", dialog.FileName);
        Assert.Equal ("Alice.epub", dialog.SafeFileName);
        Assert.Null (Assert.Single (dialog.SelectedFiles).Path);
        Assert.Equal ("contents", await ReadAll (await dialog.OpenFileAsync (TestContext.Current.CancellationToken)));
        Assert.Equal ("contents", await ReadAll (dialog.OpenFile ()));
    }

    [Fact]
    public async Task Every_file_of_a_multiple_pick_is_readable ()
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFilePickResponse = _ => new[] { InMemory ("a.epub", "A"), InMemory ("b.epub", "B") };

        using var dialog = new OpenFileDialog { Multiselect = true };

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal (new[] { "a.epub", "b.epub" }, dialog.FileNames);
        var texts = new List<string> ();
        foreach (var file in dialog.SelectedFiles)
            texts.Add (await ReadAll (await file.OpenReadAsync (TestContext.Current.CancellationToken)));
        Assert.Equal (new[] { "A", "B" }, texts);
    }

    [Fact]
    public async Task A_FileOk_handler_can_read_the_pick ()
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFilePickResponse = _ => new[] { InMemory ("a.epub", "A") };

        using var dialog = new OpenFileDialog ();
        string? read = null;
        dialog.FileOk += (_, _) => read = new StreamReader (dialog.OpenFile ()).ReadToEnd ();

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal ("A", read);
    }

    [Fact]
    public async Task A_name_without_an_extension_is_not_given_one ()
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFilePickResponse = _ => new[] { InMemory ("notes", "N") };

        // A path-based pick of "notes" would become "notes.txt" if that file existed beside it; a
        // path-less one has no directory to look in, and its handle is found by the name as picked.
        using var dialog = new OpenFileDialog { Filter = "Text|*.txt", DefaultExt = "txt" };

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal ("notes", dialog.FileName);
        Assert.Equal ("N", await ReadAll (await dialog.OpenFileAsync (TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task OpenFile_refuses_a_read_that_only_completes_later ()
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFilePickResponse = _ => new[] { InMemory ("a.epub", "A", completesAtOnce: false) };

        using var dialog = new OpenFileDialog ();

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Throws<PlatformNotSupportedException> (() => dialog.OpenFile ());
        Assert.Equal ("A", await ReadAll (await dialog.OpenFileAsync (TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task A_path_pick_is_still_a_path_and_reads_from_disk ()
    {
        var path = Path.Combine (Path.GetTempPath (), $"picked-{Guid.NewGuid ():N}.txt");
        await File.WriteAllTextAsync (path, "on disk", TestContext.Current.CancellationToken);
        try {
            using var form = ShownForm ();
            HeadlessRenderer.OpenFileResponse = _ => new[] { path };

            using var dialog = new OpenFileDialog ();

            Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
            Assert.Equal (path, dialog.FileName);
            Assert.Equal (path, Assert.Single (dialog.SelectedFiles).Path);
            Assert.Equal ("on disk", await ReadAll (await dialog.OpenFileAsync (TestContext.Current.CancellationToken)));
        } finally {
            File.Delete (path);
        }
    }

    [Fact]
    public async Task A_FileName_set_after_a_pick_is_read_from_disk ()
    {
        var path = Path.Combine (Path.GetTempPath (), $"picked-{Guid.NewGuid ():N}.txt");
        await File.WriteAllTextAsync (path, "on disk", TestContext.Current.CancellationToken);
        try {
            using var form = ShownForm ();
            HeadlessRenderer.OpenFilePickResponse = _ => new[] { InMemory ("a.epub", "A") };

            using var dialog = new OpenFileDialog ();
            Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));

            dialog.FileName = path;
            Assert.Equal ("on disk", await ReadAll (await dialog.OpenFileAsync (TestContext.Current.CancellationToken)));
        } finally {
            File.Delete (path);
        }
    }

    [Fact]
    public async Task A_cancelled_pick_leaves_nothing_selected ()
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFilePickResponse = _ => Array.Empty<PickedFile> ();

        using var dialog = new OpenFileDialog ();

        Assert.Equal (DialogResult.Cancel, await dialog.ShowDialogAsync (form));
        Assert.Empty (dialog.SelectedFiles);
        await Assert.ThrowsAsync<InvalidOperationException> (() => dialog.OpenFileAsync (TestContext.Current.CancellationToken));
    }
}
