using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using Xunit;

namespace Majorsilence.Forms.Mcp.Tests
{
    // The tool names and their descriptions are the tool's public contract twice over: users name them in
    // MCP client configuration, and the descriptions are the only documentation the model ever reads.
    // Renaming one silently breaks somebody's config, so the surface is pinned here.
    public class ToolSurfaceTests
    {
        private static (MethodInfo Method, McpServerToolAttribute Tool) [] Tools () =>
            typeof (UiTools).GetMethods (BindingFlags.Public | BindingFlags.Instance)
                .Select (method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute> ()))
                .Where (pair => pair.Tool is not null)
                .Select (pair => (pair.Method, Tool: pair.Tool!))
                .ToArray ();

        [Fact]
        public void Exposes_exactly_the_documented_tools ()
        {
            var names = Tools ().Select (pair => pair.Tool.Name!).OrderBy (name => name, StringComparer.Ordinal);

            Assert.Equal (
                new [] { "ui_click", "ui_find", "ui_read", "ui_screenshot", "ui_snapshot", "ui_type", "ui_wait_for" },
                names);
        }

        [Fact]
        public void Every_tool_and_every_argument_is_described ()
        {
            foreach (var (method, tool) in Tools ()) {
                Assert.False (
                    string.IsNullOrWhiteSpace (method.GetCustomAttribute<DescriptionAttribute> ()?.Description),
                    $"{tool.Name} has no description for the model to read");

                foreach (var parameter in method.GetParameters ()) {
                    // The cancellation token is plumbing the MCP host supplies, not an argument a caller
                    // ever sees in the generated schema.
                    if (parameter.ParameterType == typeof (System.Threading.CancellationToken))
                        continue;

                    Assert.False (
                        string.IsNullOrWhiteSpace (
                            parameter.GetCustomAttribute<DescriptionAttribute> ()?.Description),
                        $"{tool.Name}'s '{parameter.Name}' argument has no description");
                }
            }
        }

        [Fact]
        public void Only_the_state_changing_tools_are_marked_destructive ()
        {
            // Hosts surface these hints when deciding what a user has to approve, so a read-only tool
            // marked destructive (or worse, the reverse) misleads the approval prompt.
            //
            // Asserted against the generated protocol tool rather than the attribute: the attribute's
            // Destructive/ReadOnly properties read back as their own defaults whether or not they were
            // set, so only what the SDK emits shows what a client is actually told.
            using var client = new WebDriverClient (new Uri ("http://127.0.0.1:1/"));
            var target = new UiTools (client);
            var tools = Tools ().Select (pair => McpServerTool.Create (pair.Method, target).ProtocolTool).ToArray ();

            var destructive = tools
                .Where (tool => tool.Annotations?.DestructiveHint == true)
                .Select (tool => tool.Name)
                .OrderBy (name => name, StringComparer.Ordinal);

            Assert.Equal (new [] { "ui_click", "ui_type" }, destructive);

            var readOnly = tools
                .Where (tool => tool.Annotations?.ReadOnlyHint == true)
                .Select (tool => tool.Name)
                .OrderBy (name => name, StringComparer.Ordinal);

            Assert.Equal (
                new [] { "ui_find", "ui_read", "ui_screenshot", "ui_snapshot", "ui_wait_for" }, readOnly);
        }
    }
}
