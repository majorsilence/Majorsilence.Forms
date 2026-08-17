using System;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.WebDriver;
using Xunit;

namespace Majorsilence.Forms.Mcp.Tests
{
    // Drives the MCP server's client half against a real app: a headless window behind a real
    // WebDriverServer, exercised over loopback HTTP exactly as the MCP tools do. The server marshals
    // element actions onto the UI thread, so these tests pump the backend queue on the test thread while
    // the client runs on a worker (which is what a real app's pumping UI thread provides).
    public class WebDriverClientTests
    {
        private static int FreePort ()
        {
            var listener = new TcpListener (IPAddress.Loopback, 0);
            listener.Start ();
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop ();
            return port;
        }

        private static T RunPumped<T> (Func<Task<T>> work)
        {
            var task = Task.Run (work);
            while (!task.IsCompleted) {
                Platform.Backend.DoEvents ();
                Thread.Sleep (5);
            }
            return task.GetAwaiter ().GetResult ();
        }

        // The app under test: a greeting label the OK button rewrites, plus a control that stays disabled.
        private static Form BuildApp ()
        {
            var form = new Form { Text = "MCP target", ClientSize = new Size (360, 170) };

            var greeting = new Label {
                Name = "greeting", Text = "Who are you?", Left = 12, Top = 12, Width = 336, Height = 20
            };
            var nameBox = new TextBox { Name = "nameBox", Left = 12, Top = 44, Width = 336, Height = 28 };
            var okButton = new Button {
                Name = "okButton", Text = "Greet", Left = 12, Top = 100, Width = 100, Height = 30
            };
            var lockedButton = new Button {
                Name = "lockedButton", Text = "Locked", Left = 124, Top = 100, Width = 100, Height = 30,
                Enabled = false
            };

            okButton.Click += (_, _) => greeting.Text = $"Hello, {nameBox.Text}!";

            form.Controls.Add (greeting);
            form.Controls.Add (nameBox);
            form.Controls.Add (okButton);
            form.Controls.Add (lockedButton);

            HeadlessRenderer.CapturePng (form, 360, 170);   // force a layout pass so bounds are real
            return form;
        }

        [Fact]
        public void Reads_types_clicks_and_screenshots_a_running_app ()
        {
            var cancellation = TestContext.Current.CancellationToken;
            using var form = BuildApp ();
            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();
            using var client = new WebDriverClient (server.Url);

            var result = RunPumped (async () => {
                var found = await client.FindAsync ("id", "nameBox", cancellation);
                var typed = await client.TypeAsync ("id", "nameBox", "Ada Lovelace", clear: true, cancellation);
                var clicked = await client.ClickAsync ("id", "okButton", cancellation);
                var greeting = await client.FindAsync ("id", "greeting", cancellation);
                var locked = await client.ClickAsync ("id", "lockedButton", cancellation);
                var missing = await client.FindAsync ("id", "nope", cancellation);
                var xml = await client.SnapshotAsync (cancellation);
                var png = await client.ScreenshotAsync (cancellation);
                return (found, typed, clicked, greeting, locked, missing, xml, png);
            });

            Assert.NotNull (result.found);
            Assert.Equal ("nameBox", result.found!.Id);
            Assert.Equal ("TextBox", result.found.ControlType);
            Assert.True (result.found.Enabled);
            Assert.True (result.found.Width > 0, "bounds are zero until a layout pass has run");

            Assert.True (result.typed.Ok);
            Assert.Equal ("Ada Lovelace", result.typed.Element!.Text);

            // The click has to run the app's own handler, not merely report success: the greeting is
            // written by the form's Click event, so this is what proves the input pipeline was used.
            Assert.True (result.clicked.Ok);
            Assert.Equal ("Hello, Ada Lovelace!", result.greeting!.Text);

            Assert.False (result.locked.Ok);
            Assert.Contains ("disabled", result.locked.Message, StringComparison.Ordinal);

            Assert.Null (result.missing);

            Assert.Contains ("nameBox", result.xml, StringComparison.Ordinal);
            Assert.Equal (new byte [] { 0x89, (byte) 'P', (byte) 'N', (byte) 'G' }, result.png [..4]);
        }

        [Fact]
        public void Waits_for_a_control_and_reports_why_a_wait_timed_out ()
        {
            var cancellation = TestContext.Current.CancellationToken;
            using var form = BuildApp ();
            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();
            using var client = new WebDriverClient (server.Url);

            var result = RunPumped (async () => {
                var ready = await client.WaitForAsync (
                    "id", "okButton", requireEnabled: true, TimeSpan.FromSeconds (2), cancellation);
                var absent = await client.WaitForAsync (
                    "id", "nope", requireEnabled: false, TimeSpan.FromMilliseconds (300), cancellation);
                var stuck = await client.WaitForAsync (
                    "id", "lockedButton", requireEnabled: true, TimeSpan.FromMilliseconds (300), cancellation);
                return (ready, absent, stuck);
            });

            Assert.True (result.ready.Ok);

            Assert.False (result.absent.Ok);
            Assert.Contains ("no control matched", result.absent.Message, StringComparison.Ordinal);

            // A control that exists but never becomes enabled is a different problem from one that never
            // appears, and the message has to distinguish them for the caller to act.
            Assert.False (result.stuck.Ok);
            Assert.Contains ("stayed disabled", result.stuck.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Recovers_when_another_client_takes_the_servers_only_session ()
        {
            var cancellation = TestContext.Current.CancellationToken;
            using var form = BuildApp ();
            using var server = new WebDriverServer (form, FreePort ());
            server.Start ();
            using var client = new WebDriverClient (server.Url);

            var result = RunPumped (async () => {
                var before = await client.FindAsync ("id", "okButton", cancellation);

                // The server holds one session at a time, so this displaces ours and makes every handle
                // we hold invalid — the same thing that happens when the app under test restarts.
                using var interloper = new HttpClient ();
                using var content = new StringContent ("{\"capabilities\":{}}");
                using var response = await interloper.PostAsync (
                    new Uri (server.Url, "session"), content, cancellation);
                response.EnsureSuccessStatusCode ();

                var after = await client.FindAsync ("id", "okButton", cancellation);
                return (before, after);
            });

            Assert.NotNull (result.before);
            Assert.NotNull (result.after);
            Assert.Equal ("okButton", result.after!.Id);
        }

        [Fact]
        public async Task Reports_how_to_start_a_server_when_nothing_is_listening ()
        {
            using var client = new WebDriverClient (new Uri ($"http://127.0.0.1:{FreePort ()}/"));

            var failure = await Assert.ThrowsAsync<WebDriverProtocolException> (
                () => client.SnapshotAsync (TestContext.Current.CancellationToken));

            Assert.Equal ("not connected", failure.Code);
            Assert.Contains ("WebDriverServer", failure.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData ("id")]
        [InlineData ("name")]
        [InlineData ("xpath")]
        [InlineData ("css selector")]
        public void Accepts_the_strategies_the_server_understands (string strategy) =>
            Assert.Null (WebDriverClient.ValidateStrategy (strategy));

        [Theory]
        [InlineData ("identifier")]
        [InlineData ("ID")]
        [InlineData ("")]
        public void Rejects_a_strategy_the_server_would_silently_reinterpret (string strategy)
        {
            // WebDriverServer maps anything it does not recognise onto a name lookup, so an unvalidated
            // typo would search the wrong way and answer "not found" instead of "bad strategy".
            var problem = WebDriverClient.ValidateStrategy (strategy);

            Assert.NotNull (problem);
            Assert.Contains ("unknown locator strategy", problem, StringComparison.Ordinal);
        }
    }
}
