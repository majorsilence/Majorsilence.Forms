using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.WebDriver;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Drives the WebDriver (Selenium-compatible) HTTP server end-to-end: a real HTTP client performs a
    // WebDriver session → find → click / send-keys / get-text flow against a headless window. The server
    // marshals element actions onto the UI thread, so the test pumps the backend queue while the HTTP
    // calls run on a worker thread (mirroring a real app whose UI thread is pumping).
    public class WebDriverServerTests
    {
        private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

        private static int FreePort ()
        {
            var l = new TcpListener (System.Net.IPAddress.Loopback, 0);
            l.Start ();
            var port = ((System.Net.IPEndPoint) l.LocalEndpoint).Port;
            l.Stop ();
            return port;
        }

        // Runs the HTTP interaction on a worker thread while pumping the UI queue on this thread until done.
        private static T RunPumped<T> (Func<Task<T>> work)
        {
            var task = Task.Run (work);
            while (!task.IsCompleted) {
                Platform.Backend.DoEvents ();
                Thread.Sleep (5);
            }
            return task.GetAwaiter ().GetResult ();
        }

        [Fact]
        public void WebDriverFlow_FindClickSendKeysText ()
        {
            var clicks = 0;
            using var form = new Form { UseSystemDecorations = true };
            var button = new Button { Name = "okButton", Text = "OK", Left = 10, Top = 10, Width = 100, Height = 30 };
            button.Click += (_, _) => clicks++;
            var textbox = new TextBox { Name = "nameBox", Left = 10, Top = 50, Width = 200, Height = 30 };
            form.Controls.Add (button);
            form.Controls.Add (textbox);
            HeadlessRenderer.CapturePng (form, 300, 200);  // force a layout pass

            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();

            var baseUrl = server.Url.ToString ();

            var typed = RunPumped (async () => {
                using var http = new HttpClient { BaseAddress = new Uri (baseUrl) };

                // New session
                var session = await PostJson (http, "session", "{}");
                var sid = session.RootElement.GetProperty ("value").GetProperty ("sessionId").GetString ();
                Assert.False (string.IsNullOrEmpty (sid));

                // Find the button by css id selector and click it
                var findBtn = await PostJson (http, $"session/{sid}/element", "{\"using\":\"css selector\",\"value\":\"#okButton\"}");
                var btnId = findBtn.RootElement.GetProperty ("value").GetProperty (ElementKey).GetString ();
                await PostJson (http, $"session/{sid}/element/{btnId}/click", "{}");

                // Find the textbox by name and type into it
                var findBox = await PostJson (http, $"session/{sid}/element", "{\"using\":\"name\",\"value\":\"nameBox\"}");
                var boxId = findBox.RootElement.GetProperty ("value").GetProperty (ElementKey).GetString ();
                await PostJson (http, $"session/{sid}/element/{boxId}/value", "{\"text\":\"Hello WebDriver\"}");

                // Read the text back
                var textResp = await GetJson (http, $"session/{sid}/element/{boxId}/text");
                return textResp.RootElement.GetProperty ("value").GetString ();
            });

            Assert.Equal (1, clicks);
            Assert.Equal ("Hello WebDriver", textbox.Text);
            Assert.Equal ("Hello WebDriver", typed);

            server.Stop ();
        }

        [Fact]
        public void Status_ReportsReady ()
        {
            using var form = new Form { UseSystemDecorations = true };
            HeadlessRenderer.CapturePng (form, 100, 100);

            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();
            var baseUrl = server.Url.ToString ();

            var ready = RunPumped (async () => {
                using var http = new HttpClient { BaseAddress = new Uri (baseUrl) };
                var resp = await GetJson (http, "status");
                return resp.RootElement.GetProperty ("value").GetProperty ("ready").GetBoolean ();
            });

            Assert.True (ready);
            server.Stop ();
        }

        private static async Task<JsonDocument> PostJson (HttpClient http, string path, string body)
        {
            var resp = await http.PostAsync (path, new StringContent (body, Encoding.UTF8, "application/json"));
            return JsonDocument.Parse (await resp.Content.ReadAsStringAsync ());
        }

        private static async Task<JsonDocument> GetJson (HttpClient http, string path)
        {
            var resp = await http.GetAsync (path);
            return JsonDocument.Parse (await resp.Content.ReadAsStringAsync ());
        }
    }
}
