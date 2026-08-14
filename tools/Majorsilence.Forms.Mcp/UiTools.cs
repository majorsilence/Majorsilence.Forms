using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Majorsilence.Forms.Mcp;

/// <summary>
/// The MCP tool surface over a running Majorsilence.Forms window. Tools take locators rather than
/// element handles, so every call re-resolves what it acts on: there is no handle for a caller to hold
/// on to and no stale snapshot to act against.
///
/// Failures that a caller can do something about — nothing matched, the control is disabled, nothing is
/// listening — come back as plain text rather than exceptions, because a sentence is recoverable and a
/// stack trace across a tool boundary usually is not.
/// </summary>
[McpServerToolType]
public sealed class UiTools
{
    private static readonly JsonSerializerOptions Readable = new() { WriteIndented = true };

    private readonly WebDriverClient client;

    /// <summary>Creates the tool surface over a WebDriver client.</summary>
    /// <param name="client">The client pointed at the app under test.</param>
    public UiTools(WebDriverClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>Returns the app's whole control tree as XML.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    [McpServerTool(Name = "ui_snapshot", ReadOnly = true, Idempotent = true)]
    [Description("Read the running app's entire control tree as XML. Every node carries the control's " +
                 "id (its WinForms Name), accessible name, role, type, value, enabled/visible state and " +
                 "bounds. Call this first to see what is on screen: the id attribute is what the other " +
                 "tools expect as their target.")]
    public Task<string> SnapshotAsync(CancellationToken cancellationToken) =>
        GuardAsync(() => client.SnapshotAsync(cancellationToken));

    /// <summary>Locates one control and reports its current state.</summary>
    /// <param name="target">The locator value.</param>
    /// <param name="strategy">How to interpret <paramref name="target"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    [McpServerTool(Name = "ui_find", ReadOnly = true, Idempotent = true)]
    [Description("Locate one control and report its id, name, role, type, value, text, enabled/visible " +
                 "state and bounds. Returns a message instead when nothing matches.")]
    public Task<string> FindAsync(
        [Description("The locator value — with the default strategy, the control's Name, e.g. 'okButton'.")]
        string target,
        [Description("How to read 'target': id (the control's Name — the default, and the only locator " +
                     "that is stable as the UI changes), name (accessible name; for a text box this is " +
                     "its typed text), xpath, role, type, text, css selector, or tag name.")]
        string strategy = "id",
        CancellationToken cancellationToken = default) =>
        GuardAsync(async () =>
        {
            if (WebDriverClient.ValidateStrategy(strategy) is { } problem)
                return problem;

            var element = await client.FindAsync(strategy, target, cancellationToken).ConfigureAwait(false);
            return element is null
                ? $"No control matched {strategy}='{target}'. Call ui_snapshot to see what is on screen."
                : JsonSerializer.Serialize(element, Readable);
        });

    /// <summary>Reads a control's text and value.</summary>
    /// <param name="target">The locator value.</param>
    /// <param name="strategy">How to interpret <paramref name="target"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    [McpServerTool(Name = "ui_read", ReadOnly = true, Idempotent = true)]
    [Description("Read what a control currently displays — its text and its value. Use this to check " +
                 "the result of an action rather than assuming it worked.")]
    public Task<string> ReadAsync(
        [Description("The locator value — with the default strategy, the control's Name, e.g. 'nameBox'.")]
        string target,
        [Description("How to read 'target': id (the control's Name — the default), name, xpath, role, " +
                     "type, text, css selector, or tag name.")]
        string strategy = "id",
        CancellationToken cancellationToken = default) =>
        GuardAsync(async () =>
        {
            if (WebDriverClient.ValidateStrategy(strategy) is { } problem)
                return problem;

            var element = await client.FindAsync(strategy, target, cancellationToken).ConfigureAwait(false);
            return element is null
                ? $"No control matched {strategy}='{target}'. Call ui_snapshot to see what is on screen."
                : JsonSerializer.Serialize(
                    new { element.Id, element.Text, element.Value, element.Enabled, element.Visible }, Readable);
        });

    /// <summary>Clicks a control.</summary>
    /// <param name="target">The locator value.</param>
    /// <param name="strategy">How to interpret <paramref name="target"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    [McpServerTool(Name = "ui_click", Destructive = true)]
    [Description("Click a control, as a user would: the click goes through the same input pipeline, so " +
                 "it fires the app's real event handlers. Refuses to click a disabled control and says " +
                 "so. This changes application state.")]
    public Task<string> ClickAsync(
        [Description("The locator value — with the default strategy, the control's Name, e.g. 'okButton'.")]
        string target,
        [Description("How to read 'target': id (the control's Name — the default), name, xpath, role, " +
                     "type, text, css selector, or tag name.")]
        string strategy = "id",
        CancellationToken cancellationToken = default) =>
        GuardAsync(async () =>
        {
            if (WebDriverClient.ValidateStrategy(strategy) is { } problem)
                return problem;

            var outcome = await client.ClickAsync(strategy, target, cancellationToken).ConfigureAwait(false);
            return outcome.Message;
        });

    /// <summary>Types into a control and reports what it holds afterwards.</summary>
    /// <param name="target">The locator value.</param>
    /// <param name="text">The text to type.</param>
    /// <param name="strategy">How to interpret <paramref name="target"/>.</param>
    /// <param name="clear">Whether to clear the control first.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    [McpServerTool(Name = "ui_type", Destructive = true)]
    [Description("Type text into a control and report what it reads afterwards — which is not always " +
                 "what was sent (masking, max length, input filtering). Clears the control first unless " +
                 "told otherwise. This changes application state.")]
    public Task<string> TypeAsync(
        [Description("The locator value — with the default strategy, the control's Name, e.g. 'nameBox'.")]
        string target,
        [Description("The text to type.")]
        string text,
        [Description("How to read 'target': id (the control's Name — the default), name, xpath, role, " +
                     "type, text, css selector, or tag name.")]
        string strategy = "id",
        [Description("Clear the control before typing. Set false to append.")]
        bool clear = true,
        CancellationToken cancellationToken = default) =>
        GuardAsync(async () =>
        {
            if (WebDriverClient.ValidateStrategy(strategy) is { } problem)
                return problem;

            var outcome = await client.TypeAsync(strategy, target, text, clear, cancellationToken)
                .ConfigureAwait(false);
            return outcome.Message;
        });

    /// <summary>Waits for a control to appear, and optionally to become enabled.</summary>
    /// <param name="target">The locator value.</param>
    /// <param name="strategy">How to interpret <paramref name="target"/>.</param>
    /// <param name="timeoutMs">How long to wait before giving up.</param>
    /// <param name="requireEnabled">Whether the control must also be enabled.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    [McpServerTool(Name = "ui_wait_for", ReadOnly = true)]
    [Description("Wait until a control exists (optionally, until it is also enabled), polling until the " +
                 "timeout. Use this after an action that takes time instead of retrying ui_find in a " +
                 "loop or guessing at a delay.")]
    public Task<string> WaitForAsync(
        [Description("The locator value — with the default strategy, the control's Name.")]
        string target,
        [Description("How to read 'target': id (the control's Name — the default), name, xpath, role, " +
                     "type, text, css selector, or tag name.")]
        string strategy = "id",
        [Description("How long to wait, in milliseconds (1-60000).")]
        int timeoutMs = 5000,
        [Description("Also require the control to be enabled, not merely present.")]
        bool requireEnabled = false,
        CancellationToken cancellationToken = default) =>
        GuardAsync(async () =>
        {
            if (WebDriverClient.ValidateStrategy(strategy) is { } problem)
                return problem;
            if (timeoutMs is < 1 or > 60_000)
                return $"timeoutMs must be between 1 and 60000, not {timeoutMs}.";

            var outcome = await client.WaitForAsync(
                strategy, target, requireEnabled, TimeSpan.FromMilliseconds(timeoutMs), cancellationToken)
                .ConfigureAwait(false);
            return outcome.Message;
        });

    /// <summary>Renders the window offscreen and returns it as a PNG image.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    [McpServerTool(Name = "ui_screenshot", ReadOnly = true)]
    [Description("Render the app's window offscreen and return it as a PNG image. Use this to see what " +
                 "the UI actually looks like — layout, overlap, clipping — which the control tree cannot " +
                 "tell you.")]
    public async Task<CallToolResult> ScreenshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var png = await client.ScreenshotAsync(cancellationToken).ConfigureAwait(false);
            return new CallToolResult
            {
                Content = [ImageContentBlock.FromBytes(png, "image/png")]
            };
        }
        catch (WebDriverProtocolException ex)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"{ex.Code}: {ex.Message}" }]
            };
        }
    }

    // Turns the protocol's failures into the sentence a caller needs, and leaves everything else to the
    // MCP host: an unexpected exception is a bug here, not a state the model should try to work around.
    private static async Task<string> GuardAsync(Func<Task<string>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (WebDriverProtocolException ex)
        {
            return $"{ex.Code}: {ex.Message}";
        }
    }
}
