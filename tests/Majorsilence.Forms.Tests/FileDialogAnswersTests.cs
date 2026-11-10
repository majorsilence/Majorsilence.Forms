using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.WebDriver;
using Xunit;

namespace Majorsilence.Forms.Tests;

// A desktop backend shows the operating system's own file picker, outside the automation tree, so a script
// driving the app had no way to choose a file. An answer queued in FileDialogAnswers stands in for the
// picker for exactly one dialog; the picker itself must not be asked.
[Collection ("Headless")]
public sealed class FileDialogAnswersTests : IDisposable
{
    private readonly string file = Path.Combine (Path.GetTempPath (), $"mf-answer-{Guid.NewGuid ():N}.txt");

    public FileDialogAnswersTests ()
    {
        HeadlessRenderer.Use ();
        File.WriteAllText (file, "x");
        HeadlessRenderer.OpenFileResponse = _ => throw new InvalidOperationException ("the picker was shown");
        HeadlessRenderer.SaveFileResponse = _ => throw new InvalidOperationException ("the picker was shown");
    }

    public void Dispose ()
    {
        FileDialogAnswers.Clear ();
        HeadlessRenderer.OpenFileResponse = null;
        HeadlessRenderer.SaveFileResponse = null;
        File.Delete (file);
    }

    [Fact]
    public async Task A_queued_open_answer_is_returned_once_instead_of_the_picker ()
    {
        using var form = new Form ();
        form.Show ();
        using var dialog = new OpenFileDialog ();
        var fileOk = 0;
        dialog.FileOk += (_, _) => fileOk++;

        FileDialogAnswers.EnqueueOpen (file);
        Assert.Equal (1, FileDialogAnswers.PendingOpen);

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal (file, dialog.FileName);
        Assert.Equal (1, fileOk);
        Assert.Equal (0, FileDialogAnswers.PendingOpen);

        // Taken: the next dialog goes to the picker again.
        await Assert.ThrowsAsync<InvalidOperationException> (() => dialog.ShowDialogAsync (form));
    }

    [Fact]
    public async Task An_empty_open_answer_and_a_null_save_answer_cancel ()
    {
        using var form = new Form ();
        form.Show ();
        using var open = new OpenFileDialog ();
        using var save = new SaveFileDialog ();

        FileDialogAnswers.EnqueueOpen ();
        FileDialogAnswers.EnqueueSave (null);

        Assert.Equal (DialogResult.Cancel, await open.ShowDialogAsync (form));
        Assert.Equal (DialogResult.Cancel, await save.ShowDialogAsync (form));
    }

    [Fact]
    public async Task A_queued_save_answer_gets_the_default_extension ()
    {
        using var form = new Form ();
        form.Show ();
        using var save = new SaveFileDialog { DefaultExt = "csv", AddExtension = true };
        var target = Path.Combine (Path.GetTempPath (), "mf-answer-report");

        FileDialogAnswers.EnqueueSave (target);

        Assert.Equal (DialogResult.OK, await save.ShowDialogAsync (form));
        Assert.Equal (target + ".csv", save.FileName);
    }

    [Fact]
    public void A_file_dialog_opened_by_a_click_takes_the_answer_queued_over_WebDriver ()
    {
        {
            using var form = new Form { UseSystemDecorations = true };
            var picked = "";
            var button = new Button { Name = "attach", Text = "Attach", Left = 10, Top = 10, Width = 100, Height = 30 };
            button.Click += (_, _) => {
                using var dialog = new OpenFileDialog ();
                if (dialog.ShowDialog (form) == DialogResult.OK)
                    picked = dialog.FileName;
            };
            form.Controls.Add (button);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);

            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();
            var baseUrl = server.Url.ToString ();

            var pending = RunPumped (async () => {
                using var http = new HttpClient { BaseAddress = new Uri (baseUrl) };
                var session = await PostJson (http, "session", "{}");
                var sid = session.RootElement.GetProperty ("value").GetProperty ("sessionId").GetString ();

                var queued = await PostJson (http, $"session/{sid}/majorsilence/file-dialog",
                    JsonSerializer.Serialize (new { open = new[] { file } }));
                var find = await PostJson (http, $"session/{sid}/element", "{\"using\":\"id\",\"value\":\"attach\"}");
                var eid = find.RootElement.GetProperty ("value").GetProperty (ElementKey).GetString ();
                await PostJson (http, $"session/{sid}/element/{eid}/click", "{}");
                return queued.RootElement.GetProperty ("value").GetProperty ("open").GetInt32 ();
            });

            Assert.Equal (1, pending);
            Assert.Equal (file, picked);
            Assert.Equal (0, FileDialogAnswers.PendingOpen);
            server.Stop ();
        }
    }

    private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

    private static int FreePort ()
    {
        var l = new System.Net.Sockets.TcpListener (System.Net.IPAddress.Loopback, 0);
        l.Start ();
        var port = ((System.Net.IPEndPoint) l.LocalEndpoint).Port;
        l.Stop ();
        return port;
    }

    private static T RunPumped<T> (Func<Task<T>> work)
    {
        var task = Task.Run (work, TestContext.Current.CancellationToken);
        while (!task.IsCompleted) {
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();
            System.Threading.Thread.Sleep (5);
        }
        return task.GetAwaiter ().GetResult ();
    }

    private static async Task<JsonDocument> PostJson (HttpClient http, string path, string body)
    {
        var resp = await http.PostAsync (path, new StringContent (body, Encoding.UTF8, "application/json"));
        return JsonDocument.Parse (await resp.Content.ReadAsStringAsync ());
    }
}
