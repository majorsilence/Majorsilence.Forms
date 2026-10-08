using System.ComponentModel;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Issue #406 follow-up: every way of showing a file dialog -- the blocking ShowDialog overloads and the
// awaitable ShowDialogAsync ones -- raises FileOk once per accepted pick, and a FileOk handler that
// cancels shows the picker again, as upstream keeps its dialog open on a vetoed FileOk.
// The blocking path used to raise FileOk a second time after the pick had already raised it, so a
// handler ran twice for one choice; a dialog whose own ShowDialogAsync (Form) does not raise FileOk got
// it from the blocking path only.
[Collection ("Headless")]
public sealed class FileDialogFileOkTests : IDisposable
{
    public FileDialogFileOkTests () => HeadlessRenderer.Use ();

    public void Dispose ()
    {
        HeadlessRenderer.OpenFileResponse = null;
        HeadlessRenderer.SaveFileResponse = null;
    }

    // Each public way of showing the dialog against a shown form.
    private static readonly Dictionary<string, Func<FileDialog, Form, Task<DialogResult>>> entry_points = new () {
        ["ShowDialog ()"] = (d, _) => Task.FromResult (d.ShowDialog ()),
        ["ShowDialog (IWin32Window)"] = (d, f) => Task.FromResult (d.ShowDialog ((IWin32Window) f)),
        ["ShowDialog (Form)"] = (d, f) => Task.FromResult (d.ShowDialog (f)),
        ["ShowDialogSync (Form)"] = (d, f) => Task.FromResult (d.ShowDialogSync (f)),
        ["ShowDialogAsync ()"] = (d, _) => d.ShowDialogAsync (),
        ["ShowDialogAsync (IWin32Window)"] = (d, f) => d.ShowDialogAsync ((IWin32Window) f),
        ["ShowDialogAsync (IWin32Window: a control)"] = (d, f) => d.ShowDialogAsync ((IWin32Window) f.Controls[0]),
        ["ShowDialogAsync (Form)"] = (d, f) => d.ShowDialogAsync (f),
    };

    public static TheoryData<string> EntryPoints => new (entry_points.Keys);

    private static Form ShownForm ()
    {
        var form = new Form ();
        form.Controls.Add (new Button ());
        form.Show ();
        return form;
    }

    [Theory]
    [MemberData (nameof (EntryPoints))]
    public async Task An_accepted_open_pick_raises_FileOk_once (string entry)
    {
        using var form = ShownForm ();
        HeadlessRenderer.OpenFileResponse = _ => new[] { "/tmp/chosen.png" };

        using var dialog = new OpenFileDialog ();
        var seen = new List<string> ();
        dialog.FileOk += (_, _) => seen.Add (dialog.FileName);

        Assert.Equal (DialogResult.OK, await entry_points[entry] (dialog, form));
        Assert.Equal (new[] { "/tmp/chosen.png" }, seen);
    }

    [Theory]
    [MemberData (nameof (EntryPoints))]
    public async Task An_accepted_save_pick_raises_FileOk_once (string entry)
    {
        using var form = ShownForm ();
        HeadlessRenderer.SaveFileResponse = _ => "/tmp/report.txt";

        using var dialog = new SaveFileDialog ();
        var fired = 0;
        dialog.FileOk += (_, _) => fired++;

        Assert.Equal (DialogResult.OK, await entry_points[entry] (dialog, form));
        Assert.Equal (1, fired);
        Assert.Equal ("/tmp/report.txt", dialog.FileName);
    }

    [Theory]
    [MemberData (nameof (EntryPoints))]
    public async Task A_FileOk_veto_shows_the_picker_again (string entry)
    {
        using var form = ShownForm ();
        var picks = new Queue<string> (new[] { "/tmp/first.png", "/tmp/second.png" });
        var shown = 0;
        HeadlessRenderer.OpenFileResponse = _ => {
            shown++;
            return picks.Count > 0 ? new[] { picks.Dequeue () } : System.Array.Empty<string> ();
        };

        using var dialog = new OpenFileDialog ();
        var seen = new List<string> ();
        dialog.FileOk += (_, e) => {
            seen.Add (dialog.FileName);
            e.Cancel = dialog.FileName.EndsWith ("first.png", StringComparison.Ordinal);
        };

        Assert.Equal (DialogResult.OK, await entry_points[entry] (dialog, form));
        Assert.Equal (2, shown);
        Assert.Equal (new[] { "/tmp/first.png", "/tmp/second.png" }, seen);
        Assert.Equal ("/tmp/second.png", dialog.FileName);
    }

    [Theory]
    [MemberData (nameof (EntryPoints))]
    public async Task Vetoing_every_pick_until_the_user_cancels_answers_Cancel (string entry)
    {
        using var form = ShownForm ();
        var picks = new Queue<string> (new[] { "/tmp/a.png", "/tmp/b.png" });
        HeadlessRenderer.OpenFileResponse = _ => picks.Count > 0 ? new[] { picks.Dequeue () } : System.Array.Empty<string> ();

        using var dialog = new OpenFileDialog ();
        var fired = 0;
        dialog.FileOk += (_, e) => { fired++; e.Cancel = true; };

        Assert.Equal (DialogResult.Cancel, await entry_points[entry] (dialog, form));
        Assert.Equal (2, fired);            // once per pick; the third showing was dismissed
        Assert.Equal (string.Empty, dialog.FileName);
    }

    [Theory]
    [MemberData (nameof (EntryPoints))]
    public async Task A_dismissed_picker_does_not_raise_FileOk (string entry)
    {
        using var form = ShownForm ();

        using var dialog = new OpenFileDialog ();
        var fired = 0;
        dialog.FileOk += (_, _) => fired++;

        Assert.Equal (DialogResult.Cancel, await entry_points[entry] (dialog, form));
        Assert.Equal (0, fired);
    }

    // A FileDialog whose ShowDialogAsync (Form) answers without raising FileOk itself, as a derived
    // dialog outside this library would (AcceptResult is not reachable from one).
    private sealed class AnsweringFileDialog : FileDialog
    {
        public DialogResult Answer = DialogResult.OK;
        public override Task<DialogResult> ShowDialogAsync (Form owner) => Task.FromResult (Answer);
    }

    public static TheoryData<string> EntryPointsThatWrapTheImplementation
        => new (entry_points.Keys.Where (k => k != "ShowDialogAsync (Form)"));

    [Theory]
    [MemberData (nameof (EntryPointsThatWrapTheImplementation))]
    public async Task A_dialog_that_does_not_raise_FileOk_gets_it_raised_and_a_veto_answers_Cancel (string entry)
    {
        using var form = ShownForm ();
        using var dialog = new AnsweringFileDialog ();
        var fired = 0;
        var cancel = false;
        dialog.FileOk += (_, e) => { fired++; e.Cancel = cancel; };

        Assert.Equal (DialogResult.OK, await entry_points[entry] (dialog, form));
        cancel = true;
        Assert.Equal (DialogResult.Cancel, await entry_points[entry] (dialog, form));
        dialog.Answer = DialogResult.Cancel;
        Assert.Equal (DialogResult.Cancel, await entry_points[entry] (dialog, form));

        Assert.Equal (2, fired);   // not asked when the dialog itself answered Cancel
    }
}
