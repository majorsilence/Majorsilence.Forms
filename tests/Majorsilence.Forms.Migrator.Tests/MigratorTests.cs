using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class MigratorTests : IDisposable
{
    private readonly string _dir;

    public MigratorTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-mig-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose () => Directory.Delete (_dir, recursive: true);

    private string Write (string name, string content)
    {
        var path = Path.Combine (_dir, name);
        Directory.CreateDirectory (Path.GetDirectoryName (path)!);
        File.WriteAllText (path, content);
        return path;
    }

    [Fact]
    public void Converts_single_source_file_to_output ()
    {
        var input = Write ("One.cs", "using System.Windows.Forms;\nclass F : Form { }\n");
        var outDir = Path.Combine (_dir, "out");

        var exit = new Migrator (new MigrationOptions { Input = input, Output = outDir, NoReport = true }).Run ();

        Assert.Equal (0, exit);
        var converted = File.ReadAllText (Path.Combine (outDir, "One.cs"));
        Assert.Contains ("using Majorsilence.Forms;", converted);
    }

    [Fact]
    public void Dry_run_writes_nothing ()
    {
        var input = Write ("Two.cs", "using System.Windows.Forms;\n");
        var original = File.ReadAllText (input);

        new Migrator (new MigrationOptions { Input = _dir, DryRun = true, NoReport = true }).Run ();

        Assert.Equal (original, File.ReadAllText (input)); // untouched
        Assert.False (File.Exists (input + ".bak"));
    }

    [Fact]
    public void In_place_conversion_leaves_a_backup ()
    {
        var input = Write ("Three.cs", "using System.Windows.Forms;\n");

        new Migrator (new MigrationOptions { Input = _dir, NoReport = true }).Run ();

        Assert.Contains ("using Majorsilence.Forms;", File.ReadAllText (input));
        Assert.True (File.Exists (input + ".bak"));
        Assert.Contains ("using System.Windows.Forms;", File.ReadAllText (input + ".bak"));
    }

    [Fact]
    public void Strict_mode_returns_nonzero_on_warnings ()
    {
        // Metafile has no Majorsilence equivalent → a manual-review warning.
        Write ("Four.cs", "class C { System.Drawing.Metafile b; }");

        var exit = new Migrator (new MigrationOptions { Input = _dir, DryRun = true, NoReport = true, Strict = true }).Run ();

        Assert.NotEqual (0, exit);
    }

    [Fact]
    public void Writes_report_by_default ()
    {
        Write ("Five.cs", "using System.Windows.Forms;\n");
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir }).Run ();

        Assert.True (File.Exists (Path.Combine (outDir, "migration-report.md")));
    }
}
