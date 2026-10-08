using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Printing;
using Microsoft.VisualBasic;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Issue #406: on the browser target a blocking modal call cannot work -- .NET runs on the page's only
// thread, and Avalonia.Browser's dispatcher has no nested frame (measured with samples/Gallery.Wasm's
// modal check: PushFrame throws a message-less PlatformNotSupportedException). The blocking APIs now
// refuse up front with a message naming the async alternative, and every modal API has an awaitable
// form that works without a nested loop.
//
// Android and iOS (Avalonia backend) were measured the same way with the check linked into their sample
// heads: PushFrame throws there too, after the dialog is shown, so they report CanRunModalLoop = false
// as well.
//
// The headless backend stands in for all three by reporting CanRunModalLoop = false; the refusal is
// decided above the seam, so this is the same code path they take.
public sealed class BrowserBlockingModalTests : IDisposable
{
    // A regression that stops a dialog's task completing fails the test instead of hanging the run.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds (5);

    private static HeadlessPlatformBackend Headless => (HeadlessPlatformBackend) Platform.Backend;

    // Forms another test left open; the suite shares Application.OpenForms.
    private readonly Form[] already_open = Application.OpenForms.Cast<Form> ().ToArray ();

    public BrowserBlockingModalTests () => Headless.CanRunModalLoop = false;

    public void Dispose ()
    {
        Headless.CanRunModalLoop = true;

        foreach (var form in Application.OpenForms.Cast<Form> ().Except (already_open).ToArray ())
            form.Close ();
    }

    private void AssertNothingLeftOpen (Form owner)
    {
        // The point of refusing before the dialog is shown: a refusal from inside the loop would leave the
        // dialog on screen, on the modal stack, with its owner disabled -- a UI nobody can get out of.
        Assert.Equal (new Form[] { owner }, Application.OpenForms.Cast<Form> ().Except (already_open).ToArray ());
        Assert.Null (Application.ActiveModalForm);
        Assert.True (owner.Backend.Enabled);
    }

    private static PlatformNotSupportedException AssertRefused (Action call, string alternative)
    {
        var ex = Assert.Throws<PlatformNotSupportedException> (call);
        Assert.Contains (alternative, ex.Message);
        Assert.Contains ("docs/backends.md", ex.Message);
        return ex;
    }

    // ── The blocking calls refuse, naming the call to make instead, and leave nothing behind ──

    [Fact]
    public void Form_ShowDialog_with_an_owner_is_refused_before_the_dialog_is_shown ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var dialog = new Form ();
        var shown = false;
        dialog.Load += (_, _) => shown = true;

        AssertRefused (() => dialog.ShowDialog (owner), "Form.ShowDialogAsync");

        Assert.False (shown);
        Assert.False (dialog.Modal);
        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public void Form_ShowDialog_without_an_owner_is_refused_too ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var dialog = new Form ();

        AssertRefused (() => dialog.ShowDialog (), "Form.ShowDialogAsync");
        AssertRefused (() => dialog.ShowDialog ((IWin32Window) owner), "Form.ShowDialogAsync");

        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public void MessageBox_Show_is_refused_naming_ShowAsync_and_no_box_stays_open ()
    {
        using var owner = new Form ();
        owner.Show ();

        AssertRefused (() => MessageBox.Show ("text"), "MessageBox.ShowAsync");
        AssertRefused (() => MessageBox.Show (owner, "text", "caption", MessageBoxButtons.YesNo), "MessageBox.ShowAsync");

        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public void File_and_folder_pickers_are_refused_before_the_picker_is_asked_for ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var open = new OpenFileDialog ();
        using var folder = new FolderBrowserDialog ();

        AssertRefused (() => open.ShowDialog (owner), "FileDialog.ShowDialogAsync");
        AssertRefused (() => open.ShowDialog (), "FileDialog.ShowDialogAsync");
        AssertRefused (() => open.ShowDialog ((IWin32Window) owner), "FileDialog.ShowDialogAsync");
        AssertRefused (() => folder.ShowDialog (owner), "FolderBrowserDialog.ShowDialogAsync");
        AssertRefused (() => folder.ShowDialog (), "FolderBrowserDialog.ShowDialogAsync");

        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public void TaskDialog_ShowDialog_is_refused_before_the_page_is_created ()
    {
        using var owner = new Form ();
        owner.Show ();
        var page = new TaskDialogPage { Text = "text" };
        var created = false;
        page.Created += (_, _) => created = true;

        AssertRefused (() => TaskDialog.ShowDialog (owner, page), "TaskDialog.ShowDialogAsync");

        Assert.False (created);
        Assert.Null (page.BoundDialog);
        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public void The_VB_helpers_are_refused_like_the_calls_they_wrap ()
    {
        using var owner = new Form ();
        owner.Show ();

        AssertRefused (() => VbInteraction.MsgBox ("text"), "MessageBox.ShowAsync");
        AssertRefused (() => VbInteraction.InputBox ("prompt"), "Form.ShowDialogAsync");

        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public void The_refusal_explains_the_browser_and_the_mobile_reason_differently ()
    {
        var browser = BlockingModal.Message ("Form.ShowDialog", "Form.ShowDialogAsync", browser: true);
        var mobile = BlockingModal.Message ("Form.ShowDialog", "Form.ShowDialogAsync", browser: false);

        // Each names the call to make instead and the docs section that explains its own reason.
        Assert.Contains ("Form.ShowDialogAsync", browser);
        Assert.Contains ("page's only thread", browser);
        Assert.Contains ("\"Browser threading\" in docs/backends.md", browser);

        Assert.Contains ("Form.ShowDialogAsync", mobile);
        Assert.Contains ("nested message loop", mobile);
        Assert.DoesNotContain ("browser", mobile);
        Assert.Contains ("\"Blocking modal calls on Android and iOS\" in docs/backends.md", mobile);
    }

    [Fact]
    public void Off_the_browser_a_refusal_gives_the_mobile_reason ()
    {
        // The test host is not a browser, so this is the text an Android or iOS app sees.
        using var dialog = new Form ();

        var ex = AssertRefused (() => dialog.ShowDialog (), "Form.ShowDialogAsync");

        Assert.Contains ("Android and iOS", ex.Message);
    }

    [Fact]
    public void The_backend_loop_itself_refuses_a_direct_caller ()
    {
        var ex = Assert.Throws<PlatformNotSupportedException> (() => Headless.RunModalLoop (Task.CompletedTask));

        Assert.Contains ("async", ex.Message);
    }

    [Fact]
    public void With_a_loop_available_the_blocking_call_still_runs ()
    {
        // The refusal is only for a backend that says it cannot block: every desktop backend can.
        Headless.CanRunModalLoop = true;
        using var owner = new Form ();
        owner.Show ();
        using var dialog = new Form ();

        Platform.Backend.Post (() => dialog.DialogResult = DialogResult.OK);

        Assert.Equal (DialogResult.OK, dialog.ShowDialog (owner));
    }

    // ── The awaitable forms work where blocking cannot ──

    [Fact]
    public async Task Form_ShowDialogAsync_is_modal_and_completes_when_the_dialog_closes ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var dialog = new Form ();

        var task = dialog.ShowDialogAsync (owner);

        // Returned without waiting, yet modal: the owner is disabled while it is up.
        Assert.False (task.IsCompleted);
        Assert.Same (dialog, Application.ActiveModalForm);
        Assert.False (owner.Backend.Enabled);

        dialog.DialogResult = DialogResult.Yes;

        Assert.Equal (DialogResult.Yes, await task.WaitAsync (Timeout, TestContext.Current.CancellationToken));
        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public async Task Form_ShowDialogAsync_with_a_control_owner_is_owned_by_the_controls_form ()
    {
        using var main = new Form ();
        main.Show ();
        using var tool = new Form ();
        var panel = new Panel ();
        tool.Controls.Add (panel);
        tool.Show ();
        using var dialog = new Form ();

        var task = dialog.ShowDialogAsync ((IWin32Window) panel);

        // It used to take the most recently opened form whatever was passed unless it was a Form.
        Assert.Same (tool, dialog.Owner);

        dialog.Close ();
        await task.WaitAsync (Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MessageBox_ShowAsync_shows_the_same_box_and_completes_with_the_button_chosen ()
    {
        using var owner = new Form ();
        owner.Show ();

        var task = MessageBox.ShowAsync (owner, "Save changes?", "Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        Assert.False (task.IsCompleted);
        var box = Assert.IsType<MessageBoxForm> (Application.ActiveModalForm);
        Assert.Equal ("Editor", box.Text);
        Assert.Same (owner, box.Owner);
        Assert.False (owner.Backend.Enabled);

        box.DialogResult = DialogResult.No;

        Assert.Equal (DialogResult.No, await task.WaitAsync (Timeout, TestContext.Current.CancellationToken));
        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public async Task MessageBox_ShowAsync_owned_by_a_control_is_owned_by_that_controls_form ()
    {
        using var main = new Form ();
        main.Show ();
        var panel = new Panel ();
        using var tool = new Form ();
        tool.Controls.Add (panel);
        tool.Show ();

        var task = MessageBox.ShowAsync (panel, "text");

        Assert.Same (tool, Application.ActiveModalForm!.Owner);

        Application.ActiveModalForm.Close ();
        await task.WaitAsync (Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Every_MessageBox_Show_overload_has_a_ShowAsync_twin ()
    {
        AssertAsyncTwins (typeof (MessageBox), "Show", "ShowAsync");
        AssertAsyncTwins (typeof (Majorsilence.Forms.Telerik.RadMessageBox), "Show", "ShowAsync");
    }

    private static void AssertAsyncTwins (Type type, string blocking, string awaitable)
    {
        static string Signature (MethodInfo m) => string.Join (", ", m.GetParameters ().Select (p => p.ParameterType.FullName));

        var methods = type.GetMethods (BindingFlags.Public | BindingFlags.Static);
        var shows = methods.Where (m => m.Name == blocking).ToArray ();
        var asyncs = methods.Where (m => m.Name == awaitable).ToDictionary (Signature);

        Assert.NotEmpty (shows);

        foreach (var show in shows) {
            Assert.True (asyncs.TryGetValue (Signature (show), out var twin), $"{type.Name}.{awaitable} ({Signature (show)}) is missing");
            Assert.Equal (typeof (Task<>).MakeGenericType (show.ReturnType), twin!.ReturnType);
        }

        Assert.Equal (shows.Length, asyncs.Count);
    }

    [Fact]
    public async Task TaskDialog_ShowDialogAsync_no_longer_blocks_and_returns_the_button_clicked ()
    {
        using var owner = new Form ();
        owner.Show ();
        var no = TaskDialogButton.No;
        var page = new TaskDialogPage { Text = "Overwrite?", Buttons = { TaskDialogButton.Yes, no } };
        var events = "";
        page.Created += (_, _) => events += "Created ";
        page.Destroyed += (_, _) => events += "Destroyed ";

        // It used to wrap the blocking ShowDialog in Task.FromResult, so it blocked -- and on the
        // browser failed -- exactly as that does.
        var task = TaskDialog.ShowDialogAsync (owner, page);

        Assert.False (task.IsCompleted);
        Assert.Equal ("Created ", events);
        Assert.NotNull (page.BoundDialog);

        var dialog = Application.ActiveModalForm!;
        dialog.Controls.GetAllControls ().OfType<Button> ().Single (b => b.Text == "No").PerformClick ();

        Assert.Same (no, await task.WaitAsync (Timeout, TestContext.Current.CancellationToken));
        Assert.Equal ("Created Destroyed ", events);
        Assert.Null (page.BoundDialog);
    }

    [Fact]
    public async Task VbInteraction_async_helpers_answer_like_the_blocking_ones ()
    {
        using var owner = new Form ();
        owner.Show ();

        var msg = VbInteraction.MsgBoxAsync ("Continue?", MsgBoxStyle.YesNo, "Title");
        Assert.False (msg.IsCompleted);
        Application.ActiveModalForm!.DialogResult = DialogResult.Yes;
        Assert.Equal (MsgBoxResult.Yes, await msg.WaitAsync (Timeout, TestContext.Current.CancellationToken));

        var input = VbInteraction.InputBoxAsync ("Name?", "Title", "Ada");
        Assert.False (input.IsCompleted);
        var box = Application.ActiveModalForm!;
        box.Controls.GetAllControls ().OfType<TextBox> ().Single ().Text = "Grace";
        box.DialogResult = DialogResult.OK;
        Assert.Equal ("Grace", await input.WaitAsync (Timeout, TestContext.Current.CancellationToken));

        var cancelled = VbInteraction.InputBoxAsync ("Name?", "Title", "Ada");
        Application.ActiveModalForm!.DialogResult = DialogResult.Cancel;
        Assert.Equal (string.Empty, await cancelled.WaitAsync (Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Print_and_page_setup_stubs_answer_OK_when_awaited_without_showing_an_empty_form ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var print = new PrintDialog ();
        using var setup = new PageSetupDialog ();

        // The inherited Form.ShowDialogAsync would have shown these UI-less stubs as blank windows and
        // waited for someone to close them; the blocking ShowDialog answers OK without showing anything.
        Assert.Equal (DialogResult.OK, await print.ShowDialogAsync ());
        Assert.Equal (DialogResult.OK, await print.ShowDialogAsync (owner));
        Assert.Equal (DialogResult.OK, await setup.ShowDialogAsync ((IWin32Window) owner));

        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public async Task PrintPreviewDialog_ShowDialogAsync_captures_the_pages_like_ShowDialog ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var document = new PrintDocument ();
        document.PrintPage += (_, e) => e.HasMorePages = false;
        using var preview = new PrintPreviewDialog { Document = document };

        var task = preview.ShowDialogAsync (owner);

        Assert.Same (preview, preview.PrintPreviewControl.FindForm ());
        Assert.Same (preview, Application.ActiveModalForm);

        preview.Close ();
        await task.WaitAsync (Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void PrintPreviewDialog_ShowDialog_is_refused_before_the_page_walk ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var document = new PrintDocument ();
        var printed = 0;
        document.PrintPage += (_, e) => printed++;
        using var preview = new PrintPreviewDialog { Document = document };
        printed = 0;

        AssertRefused (() => preview.ShowDialog (), "PrintPreviewDialog.ShowDialogAsync");

        Assert.Equal (0, printed);
        AssertNothingLeftOpen (owner);
    }

    [Fact]
    public async Task Color_and_font_dialogs_can_be_awaited ()
    {
        using var owner = new Form ();
        owner.Show ();
        using var color = new ColorDialog ();
        using var font = new FontDialog ();

        var picked = color.ShowDialogAsync (owner);
        Assert.Same (color, Application.ActiveModalForm);
        color.DialogResult = DialogResult.OK;
        Assert.Equal (DialogResult.OK, await picked.WaitAsync (Timeout, TestContext.Current.CancellationToken));

        var chosen = font.ShowDialogAsync ((IWin32Window) owner);
        Assert.Same (font, Application.ActiveModalForm);
        font.DialogResult = DialogResult.Cancel;
        Assert.Equal (DialogResult.Cancel, await chosen.WaitAsync (Timeout, TestContext.Current.CancellationToken));
    }

    private sealed class FileDialogStub : FileDialog
    {
        public Form? Owner;

        public override Task<DialogResult> ShowDialogAsync (Form owner)
        {
            Owner = owner;
            return Task.FromResult (DialogResult.OK);
        }
    }

    [Fact]
    public async Task FileDialog_ShowDialogAsync_resolves_the_owner_as_ShowDialog_does ()
    {
        using var stub = new FileDialogStub ();

        // No open form: nothing to show against, the same Cancel the blocking overload answers.
        foreach (var form in Application.OpenForms.Cast<Form> ().ToArray ())
            form.Close ();
        Assert.Equal (DialogResult.Cancel, await stub.ShowDialogAsync ());

        using var main = new Form ();
        main.Show ();
        using var tool = new Form ();
        var panel = new Panel ();
        tool.Controls.Add (panel);
        tool.Show ();

        Assert.Equal (DialogResult.OK, await stub.ShowDialogAsync ());
        Assert.Same (main, stub.Owner);

        Assert.Equal (DialogResult.OK, await stub.ShowDialogAsync ((IWin32Window) panel));
        Assert.Same (tool, stub.Owner);
    }

    private sealed class AwaitingCommonDialog : CommonDialog
    {
        public readonly TaskCompletionSource<bool> Answer = new ();

        public override void Reset () { }

        protected override bool RunDialog (IntPtr hwndOwner) => throw new InvalidOperationException ("the blocking path must not run");

        protected override Task<bool> RunDialogAsync (IWin32Window? hwndOwner) => Answer.Task;
    }

    private sealed class SynchronousCommonDialog : CommonDialog
    {
        public override void Reset () { }

        protected override bool RunDialog (IntPtr hwndOwner) => true;
    }

    [Fact]
    public async Task CommonDialog_ShowDialogAsync_runs_the_async_hook_and_maps_its_answer ()
    {
        using var awaiting = new AwaitingCommonDialog ();

        var task = awaiting.ShowDialogAsync ();
        Assert.False (task.IsCompleted);
        awaiting.Answer.SetResult (false);
        Assert.Equal (DialogResult.Cancel, await task.WaitAsync (Timeout, TestContext.Current.CancellationToken));

        // A dialog with nothing to wait for keeps working unchanged through the default hook.
        using var synchronous = new SynchronousCommonDialog ();
        Assert.Equal (DialogResult.OK, await synchronous.ShowDialogAsync (null));
    }
}
