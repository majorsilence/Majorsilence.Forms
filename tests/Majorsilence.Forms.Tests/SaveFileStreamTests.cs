using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // SaveFileDialog returned a path only, and Android, iOS and the browser hand back a document with no path:
    // nothing could be written there. A saved file is now written through the platform.
    [Collection ("Headless")]
    public sealed class SaveFileStreamTests : IDisposable
    {
        private static System.Threading.CancellationToken Ct => TestContext.Current.CancellationToken;

        public SaveFileStreamTests () => HeadlessRenderer.Use ();

        public void Dispose ()
        {
            HeadlessRenderer.SaveFilePickResponse = null;
            HeadlessRenderer.SaveFileResponse = null;
        }

        // Keeps what was written after the writer disposes the stream.
        private sealed class Kept : MemoryStream
        {
            public byte[] Written { get; private set; } = [];

            protected override void Dispose (bool disposing)
            {
                Written = ToArray ();
                base.Dispose (disposing);
            }
        }

        [Fact]
        public async Task A_file_with_no_path_is_written_through_the_platform ()
        {
            var document = new Kept ();
            HeadlessRenderer.SaveFilePickResponse = _ => new SavedFile ("notes.md", null, _ => Task.FromResult<Stream> (document));
            using var owner = new Form ();
            owner.Show ();
            using var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", FileName = "notes.md" };

            Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (owner));
            Assert.Equal ("notes.md", dialog.FileName);
            await using (var stream = await dialog.OpenFileAsync (Ct))
                await stream.WriteAsync (Encoding.UTF8.GetBytes ("# Notes"), Ct);
            Assert.Equal ("# Notes", Encoding.UTF8.GetString (document.Written));

            // Completed at once here, so the WinForms call works too.
            using (var again = dialog.OpenFile ())
                Assert.Same (document, again);
        }

        [Fact]
        public async Task A_file_written_only_asynchronously_says_to_use_OpenFileAsync ()
        {
            HeadlessRenderer.SaveFilePickResponse = _ => new SavedFile ("notes.md", null, async token => {
                await Task.Delay (10, token);
                return new MemoryStream ();
            });
            using var owner = new Form ();
            owner.Show ();
            using var dialog = new SaveFileDialog ();

            Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (owner));
            Assert.Throws<PlatformNotSupportedException> (() => dialog.OpenFile ());
            await using var stream = await dialog.OpenFileAsync (Ct);
            Assert.True (stream.CanWrite);
        }

        [Fact]
        public async Task A_path_is_still_created_on_disk ()
        {
            var path = Path.Combine (Path.GetTempPath (), $"forms-save-{Guid.NewGuid ():N}.md");
            HeadlessRenderer.SaveFileResponse = _ => path;
            try {
                using var owner = new Form ();
                owner.Show ();
                using var dialog = new SaveFileDialog ();

                Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (owner));
                Assert.Equal (path, dialog.FileName);
                await using (var stream = dialog.OpenFile ())
                    await stream.WriteAsync (Encoding.UTF8.GetBytes ("on disk"), Ct);
                Assert.Equal ("on disk", await File.ReadAllTextAsync (path, Ct));
            } finally {
                File.Delete (path);
            }
        }
    }
}
