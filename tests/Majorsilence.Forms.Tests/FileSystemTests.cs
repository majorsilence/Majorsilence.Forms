using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Majorsilence.Forms.Essentials;
using Xunit;

namespace Majorsilence.Forms.Tests;

// FileSystem: reading files shipped inside the app. Name validation and the copy-to-real-path helper are exercised against a fake
// package; the desktop backend (the files beside the executable) is exercised for real on whichever OS the suite runs on.
public class FileSystemTests
{
    private sealed class FakePackage : IFileSystemBackend
    {
        public Dictionary<string, byte[]> Files { get; } = new ();
        public string? LastRequested { get; private set; }

        public Task<Stream?> OpenAppPackageFileAsync (string name)
        {
            LastRequested = name;
            return Task.FromResult<Stream?> (Files.TryGetValue (name, out var bytes) ? new MemoryStream (bytes) : null);
        }
    }

    private static async Task<T> WithBackend<T> (IFileSystemBackend fake, Func<Task<T>> body)
    {
        var previous = FileSystem.Backend;
        FileSystem.Backend = fake;
        try {
            return await body ();
        } finally {
            FileSystem.Backend = previous;
        }
    }

    [Theory]
    [InlineData ("blog.sqlite", "blog.sqlite")]
    [InlineData ("data/seed.json", "data/seed.json")]
    [InlineData ("data\\seed.json", "data/seed.json")]
    [InlineData ("data//seed.json", "data/seed.json")]
    public void Normalize_returns_a_relative_path_with_forward_slashes (string name, string expected)
        => Assert.Equal (expected, FileSystem.Normalize (name));

    [Theory]
    [InlineData ("")]
    [InlineData ("   ")]
    [InlineData ("/etc/passwd")]
    [InlineData ("\\windows\\system32")]
    [InlineData ("C:\\windows\\win.ini")]
    [InlineData ("../secret.txt")]
    [InlineData ("data/../../secret.txt")]
    [InlineData ("..")]
    public void Normalize_rejects_empty_absolute_and_climbing_names (string name)
        => Assert.ThrowsAny<ArgumentException> (() => FileSystem.Normalize (name));

    [Fact]
    public async Task OpenAppPackageFileAsync_returns_the_packaged_bytes ()
    {
        var fake = new FakePackage ();
        fake.Files["data/seed.json"] = Encoding.UTF8.GetBytes ("{}");

        var text = await WithBackend (fake, async () => {
            await using var stream = await FileSystem.OpenAppPackageFileAsync ("data\\seed.json");
            return await new StreamReader (stream).ReadToEndAsync (TestContext.Current.CancellationToken);
        });

        Assert.Equal ("{}", text);
        Assert.Equal ("data/seed.json", fake.LastRequested);
    }

    [Fact]
    public async Task OpenAppPackageFileAsync_throws_FileNotFound_for_a_missing_file ()
    {
        var fake = new FakePackage ();
        await Assert.ThrowsAsync<FileNotFoundException> (() => WithBackend (fake, () => FileSystem.OpenAppPackageFileAsync ("nope.bin")));
    }

    [Fact]
    public async Task AppPackageFileExistsAsync_reports_presence_and_treats_a_bad_name_as_absent ()
    {
        var fake = new FakePackage ();
        fake.Files["here.txt"] = new byte[] { 1 };

        Assert.True (await WithBackend (fake, () => FileSystem.AppPackageFileExistsAsync ("here.txt")));
        Assert.False (await WithBackend (fake, () => FileSystem.AppPackageFileExistsAsync ("gone.txt")));
        Assert.False (await WithBackend (fake, () => FileSystem.AppPackageFileExistsAsync ("../here.txt")));
    }

    [Fact]
    public async Task CopyAppPackageFileAsync_writes_the_file_and_replaces_a_stale_copy ()
    {
        var directory = Path.Combine (Path.GetTempPath (), "mf-filesystem-" + Guid.NewGuid ().ToString ("N"));
        try {
            var fake = new FakePackage ();
            fake.Files["blog.sqlite"] = new byte[] { 1, 2, 3 };

            var path = await WithBackend (fake, () => FileSystem.CopyAppPackageFileAsync ("blog.sqlite", directory));
            Assert.Equal (Path.Combine (directory, "blog.sqlite"), path);
            Assert.Equal (new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync (path, TestContext.Current.CancellationToken));

            fake.Files["blog.sqlite"] = new byte[] { 9, 9, 9, 9 };   // a new build ships a different file
            await WithBackend (fake, () => FileSystem.CopyAppPackageFileAsync ("blog.sqlite", directory));
            Assert.Equal (new byte[] { 9, 9, 9, 9 }, await File.ReadAllBytesAsync (path, TestContext.Current.CancellationToken));
        } finally {
            if (Directory.Exists (directory))
                Directory.Delete (directory, recursive: true);
        }
    }

    [Fact]
    public void AppDataDirectory_exists_after_it_is_read ()
        => Assert.True (Directory.Exists (FileSystem.AppDataDirectory));

    [Fact]
    public async Task The_desktop_backend_reads_a_file_beside_the_executable ()
    {
        var name = "mf-package-" + Guid.NewGuid ().ToString ("N") + ".txt";
        var path = Path.Combine (AppContext.BaseDirectory, name);
        await File.WriteAllTextAsync (path, "packaged", TestContext.Current.CancellationToken);
        try {
            var backend = new DesktopFileSystemBackend ();
            await using var stream = await backend.OpenAppPackageFileAsync (name) ?? throw new InvalidOperationException ("not found");
            Assert.Equal ("packaged", await new StreamReader (stream).ReadToEndAsync (TestContext.Current.CancellationToken));
            Assert.Null (await backend.OpenAppPackageFileAsync ("definitely-not-here-" + name));
        } finally {
            File.Delete (path);
        }
    }
}
