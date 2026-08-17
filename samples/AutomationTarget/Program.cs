using System;
using Majorsilence.Forms;
using Majorsilence.Forms.WebDriver;

namespace AutomationTarget
{
    /// <summary>
    /// Runs the sample form and, unless told not to, exposes it for automation on loopback. This is the
    /// app-side half of every automation story in <c>docs/automation.md</c>: the MCP server, a Selenium
    /// client, and plain <c>curl</c> are all clients of the endpoint started here.
    /// </summary>
    internal sealed class Program
    {
        private const int DefaultPort = 4444;

        [STAThread]
        public static int Main (string [] args)
        {
            int port;
            try {
                port = ParsePort (args);
            } catch (ArgumentException ex) {
                Console.Error.WriteLine ($"AutomationTarget: {ex.Message}");
                Console.Error.WriteLine ("Usage: AutomationTarget [--webdriver <port>] [--no-webdriver]");
                return 2;
            }

            var form = new MainForm ();
            WebDriverServer? server = null;

            if (port > 0)
                // Started from Shown rather than before Run: a layout pass has to have happened or every
                // control in the tree reports zero bounds, which is the first thing that confuses a client.
                form.Shown += (sender, e) => {
                    server = new WebDriverServer (form, port);
                    server.Start ();
                    Announce (server);
                };

            try {
                Application.Run (form);
            } finally {
                server?.Dispose ();
            }

            return 0;
        }

        private static int ParsePort (string [] args)
        {
            var port = DefaultPort;

            for (var i = 0; i < args.Length; i++) {
                switch (args [i]) {
                    case "--no-webdriver":
                        port = 0;
                        break;
                    case "--webdriver":
                        if (++i >= args.Length)
                            throw new ArgumentException ("--webdriver needs a port.");
                        if (!int.TryParse (args [i], out port) || port is < 1 or > 65535)
                            throw new ArgumentException (
                                $"--webdriver takes a TCP port between 1 and 65535, not '{args [i]}'.");
                        break;
                    default:
                        throw new ArgumentException ($"unknown argument '{args [i]}'.");
                }
            }

            return port;
        }

        private static void Announce (WebDriverServer server)
        {
            Console.WriteLine ($"Automation endpoint: {server.Url}");
            Console.WriteLine ($"  status:    curl -s {server.Url}status");
            Console.WriteLine ($"  MCP:       claude mcp add majorsilence-ui -- majorsilence-mcp --port {server.Port}");
            Console.WriteLine ($"  Selenium:  new RemoteWebDriver (new Uri (\"{server.Url}\"), options.ToCapabilities ())");
        }
    }
}
