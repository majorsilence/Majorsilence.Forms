using System;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.WebDriver;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // An application driven through automation is usually an MDI application: its screens are child forms of a
    // module window, opened from that window's menus, and they open modal dialogs. The tree stopped at each child
    // window's frame, a menu item in a closed menu could not be clicked, and a click that opened a modal dialog did
    // not answer until the dialog closed.
    [Collection ("Headless")]
    public class AutomationHostedFormTests
    {
        private static (Form Parent, Form Child, Button Ok) MdiPair ()
        {
            HeadlessRenderer.Use ();
            var parent = new Form { Size = new Size (800, 600), IsMdiContainer = true, UseSystemDecorations = true };
            parent.Show ();

            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200), Text = "Child" };
            var ok = new Button { Name = "childOk", Text = "OK", Left = 20, Top = 30, Width = 100, Height = 30 };
            child.Controls.Add (ok);
            child.Controls.Add (new TextBox { Name = "childBox", Left = 20, Top = 80, Width = 150, Height = 26 });
            child.Show ();
            HeadlessRenderer.CapturePng (parent, 800, 600);
            return (parent, child, ok);
        }

        [Fact]
        public void An_MDI_child_forms_controls_are_in_the_parents_tree ()
        {
            var (parent, child, _) = MdiPair ();
            using var _p = parent;

            var all = AutomationProvider.BuildTree (parent).Self ().ToList ();

            var host = Assert.Single (all, e => e.ControlType == "MdiChildWindow");
            Assert.Equal ("Child", host.Name);
            Assert.Contains (host.Self (), e => e.AutomationId == "childOk");
            Assert.Contains (host.Self (), e => e.AutomationId == "childBox");
        }

        [Fact]
        public void A_button_on_an_MDI_child_is_clicked_where_it_is_drawn ()
        {
            var (parent, _, ok) = MdiPair ();
            using var _p = parent;
            var clicks = 0;
            ok.Click += (_, _) => clicks++;

            var session = new AutomationSession (parent);
            session.Click (session.FindOrThrow (By.Id ("childOk")));

            Assert.Equal (1, clicks);
        }

        [Fact]
        public void A_button_on_a_maximized_MDI_child_is_clicked_where_it_is_drawn ()
        {
            var (parent, child, ok) = MdiPair ();
            using var _p = parent;
            parent.Controls.Add (new MenuStrip ());
            parent.MainMenuStrip = (MenuStrip) parent.Controls[parent.Controls.Count - 1];
            child.WindowState = FormWindowState.Maximized;
            HeadlessRenderer.CapturePng (parent, 800, 600);
            var clicks = 0;
            ok.Click += (_, _) => clicks++;

            var session = new AutomationSession (parent);
            session.Click (session.FindOrThrow (By.Id ("childOk")));

            Assert.Equal (1, clicks);
        }

        [Fact]
        public void An_MDI_childs_button_is_found_and_clicked_over_WebDriver ()
        {
            var (parent, _, ok) = MdiPair ();
            using var _p = parent;
            var clicks = 0;
            ok.Click += (_, _) => clicks++;

            using var server = new WebDriverServer (parent, FreePort ());
            server.Start ();
            var baseUrl = server.Url.ToString ();

            var task = Task.Run (async () => {
                using var http = new HttpClient { BaseAddress = new Uri (baseUrl), Timeout = TimeSpan.FromSeconds (5) };
                var session = await PostJson (http, "session", "{}");
                var sid = session.RootElement.GetProperty ("value").GetProperty ("sessionId").GetString ();
                var find = await PostJson (http, $"session/{sid}/element", "{\"using\":\"id\",\"value\":\"childOk\"}");
                var id = find.RootElement.GetProperty ("value").GetProperty ("element-6066-11e4-a52e-4f735466cecf").GetString ();
                await PostJson (http, $"session/{sid}/element/{id}/click", "{}");
            }, TestContext.Current.CancellationToken);

            var deadline = DateTime.UtcNow.AddSeconds (10);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) {
                Platform.Backend.DoEvents ();
                Thread.Sleep (5);
            }

            Assert.True (task.IsCompletedSuccessfully, task.Exception?.ToString () ?? "timed out");
            Assert.Equal (1, clicks);
        }

        [Fact]
        public void A_button_on_a_form_hosted_in_a_panel_is_clicked_where_it_is_drawn ()
        {
            HeadlessRenderer.Use ();
            using var window = new Form { Size = new Size (600, 400), UseSystemDecorations = true };
            var panel = new Panel { Left = 40, Top = 30, Width = 400, Height = 300 };
            window.Controls.Add (panel);
            var page = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
            var ok = new Button { Name = "pageOk", Text = "OK", Left = 50, Top = 60, Width = 100, Height = 30 };
            page.Controls.Add (ok);
            panel.Controls.Add (page);
            page.Show ();
            window.Show ();
            HeadlessRenderer.CapturePng (window, 600, 400);
            var clicks = 0;
            ok.Click += (_, _) => clicks++;

            var session = new AutomationSession (window);
            session.Click (session.FindOrThrow (By.Id ("pageOk")));

            Assert.Equal (1, clicks);
        }

        [Fact]
        public void A_menu_item_in_a_closed_menu_runs_its_command ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Size = new Size (600, 400), UseSystemDecorations = true };
            var menu = new MenuStrip ();
            var file = new ToolStripMenuItem ("File");
            var open = new ToolStripMenuItem ("Open") { Name = "openItem" };
            var opened = 0;
            open.Click += (_, _) => opened++;
            file.DropDownItems.Add (open);
            menu.Items.Add (file);
            form.Controls.Add (menu);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 600, 400);

            var session = new AutomationSession (form);
            session.Click (session.FindOrThrow (By.Id ("openItem")));

            Assert.Equal (1, opened);
        }

        [Fact]
        public void A_click_that_opens_a_modal_dialog_answers_while_the_dialog_is_open ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Size = new Size (400, 300), UseSystemDecorations = true };
            var button = new Button { Name = "openDialog", Text = "Open", Left = 10, Top = 10, Width = 100, Height = 30 };
            Form? dialog = null;
            var after = false;
            button.Click += (_, _) => {
                dialog = new Form { Text = "Dialog" };
                dialog.ShowDialog (form);
                after = true;
            };
            form.Controls.Add (button);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 300);

            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();
            var baseUrl = server.Url.ToString ();

            // The test's own loop below sits under the dialog's modal loop once the click opens it, so the
            // dialog has to be closed from work that loop runs: the client posts the close itself, whether
            // the click answered or timed out, and first records on the UI thread what it saw.
            var answered = false;
            var openWhenAnswered = false;
            var task = Task.Run (async () => {
                try {
                    using var http = new HttpClient { BaseAddress = new Uri (baseUrl), Timeout = TimeSpan.FromSeconds (5) };
                    var session = await PostJson (http, "session", "{}");
                    var sid = session.RootElement.GetProperty ("value").GetProperty ("sessionId").GetString ();
                    var find = await PostJson (http, $"session/{sid}/element", "{\"using\":\"id\",\"value\":\"openDialog\"}");
                    var id = find.RootElement.GetProperty ("value").GetProperty ("element-6066-11e4-a52e-4f735466cecf").GetString ();
                    await PostJson (http, $"session/{sid}/element/{id}/click", "{}");
                    answered = true;
                } finally {
                    Platform.Backend.Post (() => {
                        openWhenAnswered = answered && dialog is { Visible: true } && !after;
                        if (dialog is { Visible: true })
                            dialog.DialogResult = DialogResult.Cancel;
                    });
                }
            }, TestContext.Current.CancellationToken);

            // Pump the UI thread, as a running application does; the click's dialog pumps it in turn.
            var deadline = DateTime.UtcNow.AddSeconds (15);
            while (!(task.IsCompleted && after) && DateTime.UtcNow < deadline) {
                Platform.Backend.DoEvents ();
                Thread.Sleep (5);
            }

            Assert.True (answered, "the click did not answer while the dialog was open");
            Assert.True (openWhenAnswered, "the dialog was not open when the click answered");
            Assert.True (after, "the dialog did not close");
        }

        private static int FreePort ()
        {
            var l = new TcpListener (System.Net.IPAddress.Loopback, 0);
            l.Start ();
            var port = ((System.Net.IPEndPoint) l.LocalEndpoint).Port;
            l.Stop ();
            return port;
        }

        private static async Task<JsonDocument> PostJson (HttpClient http, string url, string json)
        {
            var response = await http.PostAsync (url, new StringContent (json, Encoding.UTF8, "application/json"));
            return JsonDocument.Parse (await response.Content.ReadAsStringAsync ());
        }
    }
}
