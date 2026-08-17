using Majorsilence.Forms.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return 0;
}

Uri endpoint;
try
{
    endpoint = ParseEndpoint(args);
}
catch (ArgumentException ex)
{
    // stderr, not stdout: stdout belongs to the protocol (see the logging note below).
    Console.Error.WriteLine($"majorsilence-mcp: {ex.Message}");
    Console.Error.WriteLine("Run with --help for usage.");
    return 2;
}

// The host builder is given no args deliberately: its command-line configuration provider rejects
// switch-style flags like the ones parsed above, and there is no configuration to read from them.
var builder = Host.CreateApplicationBuilder();

// stdout carries the JSON-RPC stream for the stdio transport, so every log line has to go to stderr.
// One stray write to stdout corrupts the protocol and the client drops the connection.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

using var client = new WebDriverClient(endpoint);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools(new UiTools(client));

await builder.Build().RunAsync();
return 0;

static Uri ParseEndpoint(string[] args)
{
    var url = Environment.GetEnvironmentVariable("MAJORSILENCE_MCP_URL");
    string? port = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--url":
                url = ValueAfter(args, ref i, "--url");
                break;
            case "--port":
                port = ValueAfter(args, ref i, "--port");
                break;
            default:
                throw new ArgumentException($"unknown argument '{args[i]}'.");
        }
    }

    if (port is not null)
    {
        if (!int.TryParse(port, out var number) || number is < 1 or > 65535)
            throw new ArgumentException($"--port takes a TCP port between 1 and 65535, not '{port}'.");

        // Loopback only, to match WebDriverServer: the automation surface is unauthenticated and can
        // drive the app arbitrarily, so it has no business being reachable from off the machine.
        url = $"http://127.0.0.1:{number}/";
    }

    url ??= "http://127.0.0.1:4444/";

    if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        throw new ArgumentException($"'{url}' is not an absolute URL.");

    // Relative request paths resolve against the base address, which discards its last segment unless it
    // ends in a slash — so a --url of "http://host/wd/hub" would quietly request "http://host/wd/session".
    return parsed.AbsolutePath.EndsWith('/') ? parsed : new Uri(parsed.AbsoluteUri + "/");
}

static string ValueAfter(string[] args, ref int i, string flag) =>
    ++i < args.Length ? args[i] : throw new ArgumentException($"{flag} needs a value.");

static void PrintUsage()
{
    Console.WriteLine(
        """
        majorsilence-mcp — an MCP server for driving a running Majorsilence.Forms app.

        Usage:
          majorsilence-mcp [--port <port>] [--url <url>]

        Options:
          --port <port>   Port of the app's WebDriver server on loopback. Default: 4444.
          --url <url>     Full base URL instead of a port, e.g. http://127.0.0.1:5555/.
          -h, --help      Show this help.

        Environment:
          MAJORSILENCE_MCP_URL   Used when neither --port nor --url is given.

        The app under test has to expose the automation surface itself, by referencing
        Majorsilence.Forms.WebDriver and starting a server on the same port:

            using var server = new WebDriverServer (form, 4444);
            server.Start ();

        This tool speaks MCP over stdin/stdout, so it is launched by an MCP client rather than run
        directly. With Claude Code:

            claude mcp add majorsilence-ui -- majorsilence-mcp --port 4444

        Tools: ui_snapshot, ui_find, ui_read, ui_click, ui_type, ui_wait_for, ui_screenshot.
        """);
}
