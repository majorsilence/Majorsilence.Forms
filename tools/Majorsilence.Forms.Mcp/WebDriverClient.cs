using System.Net.Http.Json;
using System.Text.Json;

namespace Majorsilence.Forms.Mcp;

/// <summary>One control, as the automation tree sees it.</summary>
/// <param name="Id">The control's <c>Name</c> — the stable handle tests and agents should target.</param>
/// <param name="Name">The accessible name (for a text box this becomes its typed text, so it is a poor locator).</param>
/// <param name="Role">The accessibility role, e.g. <c>button</c>.</param>
/// <param name="ControlType">The Majorsilence.Forms control type, e.g. <c>TextBox</c>.</param>
/// <param name="Value">The control's value, where it has one.</param>
/// <param name="Text">What the automation tree reports as this control's text.</param>
/// <param name="Enabled">Whether the control can be interacted with.</param>
/// <param name="Visible">Whether the control is currently visible.</param>
/// <param name="X">Left edge, in window coordinates.</param>
/// <param name="Y">Top edge, in window coordinates.</param>
/// <param name="Width">Width in pixels — zero for every control until a layout pass has run.</param>
/// <param name="Height">Height in pixels — zero for every control until a layout pass has run.</param>
public sealed record ElementSnapshot(
    string Id,
    string Name,
    string Role,
    string ControlType,
    string Value,
    string Text,
    bool Enabled,
    bool Visible,
    double X,
    double Y,
    double Width,
    double Height);

/// <summary>The outcome of an action, phrased for a caller that has to decide what to do next.</summary>
/// <param name="Ok">Whether the action was carried out.</param>
/// <param name="Message">What happened, in words.</param>
/// <param name="Element">The control acted on, re-read afterwards where that is useful.</param>
public sealed record ActionOutcome(bool Ok, string Message, ElementSnapshot? Element = null);

/// <summary>
/// A WebDriver-level failure: the <c>error</c>/<c>message</c> pair the server returned, or a locally
/// generated equivalent for transport problems (nothing listening, no response in time).
/// </summary>
public sealed class WebDriverProtocolException : Exception
{
    /// <summary>Creates an exception with the W3C error code and its message.</summary>
    /// <param name="code">The W3C error code, e.g. <c>no such element</c>.</param>
    /// <param name="message">The human-readable failure message.</param>
    public WebDriverProtocolException(string code, string message) : base(message) => Code = code;

    /// <inheritdoc cref="Exception()"/>
    public WebDriverProtocolException() => Code = "unknown error";

    /// <inheritdoc cref="Exception(string)"/>
    public WebDriverProtocolException(string message) : base(message) => Code = "unknown error";

    /// <inheritdoc cref="Exception(string, Exception)"/>
    public WebDriverProtocolException(string message, Exception innerException)
        : base(message, innerException) => Code = "unknown error";

    /// <summary>The W3C error code, e.g. <c>no such element</c> or <c>invalid session id</c>.</summary>
    public string Code { get; }
}

/// <summary>
/// A small client for the W3C WebDriver endpoint that <c>Majorsilence.Forms.WebDriver.WebDriverServer</c>
/// exposes. Scoped to what an assistant needs — read the tree, locate a control, click, type, read back,
/// screenshot — with element handles kept internal, so callers only ever deal in locators.
/// </summary>
public sealed class WebDriverClient : IDisposable
{
    // The W3C element-reference key the server returns from find-element.
    private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

    /// <summary>The locator strategies the server understands.</summary>
    public static readonly IReadOnlyList<string> Strategies =
        ["id", "name", "xpath", "role", "type", "text", "css selector", "tag name"];

    private readonly HttpClient http;

    // The server keeps exactly one session, and element handles only mean anything inside it, so
    // overlapping calls would trample each other's find-then-act sequences. Everything takes this gate;
    // on loopback the serialization costs far less than the class of bug it removes.
    private readonly SemaphoreSlim gate = new(1, 1);

    private string? sessionId;

    /// <summary>Creates a client for the given WebDriver base URL. No connection is made until first use.</summary>
    /// <param name="endpoint">Base URL of the app's WebDriver server, e.g. <c>http://127.0.0.1:4444/</c>.</param>
    public WebDriverClient(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        Endpoint = endpoint;
        http = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>The base URL this client talks to.</summary>
    public Uri Endpoint { get; }

    /// <summary>
    /// Checks a locator strategy before it reaches the server, which falls back to a name lookup for
    /// anything it does not recognise — so an unchecked typo would quietly search the wrong way and
    /// report "not found" instead of "bad strategy".
    /// </summary>
    /// <param name="strategy">The strategy to check.</param>
    /// <returns>An error message, or <c>null</c> when the strategy is valid.</returns>
    public static string? ValidateStrategy(string strategy) =>
        Strategies.Contains(strategy, StringComparer.Ordinal)
            ? null
            : $"unknown locator strategy '{strategy}'. Use one of: {string.Join(", ", Strategies)}.";

    /// <summary>Returns the whole control tree as XML.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<string> SnapshotAsync(CancellationToken cancellationToken) =>
        RunAsync(async sid =>
            (await GetAsync($"session/{sid}/source", cancellationToken).ConfigureAwait(false)).GetString()
            ?? string.Empty, cancellationToken);

    /// <summary>Locates one control and reads its current state, or returns <c>null</c> when nothing matches.</summary>
    /// <param name="strategy">A strategy from <see cref="Strategies"/>.</param>
    /// <param name="target">The locator value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<ElementSnapshot?> FindAsync(string strategy, string target, CancellationToken cancellationToken) =>
        RunAsync(async sid =>
        {
            var handle = await HandleAsync(sid, strategy, target, cancellationToken).ConfigureAwait(false);
            return handle is null
                ? null
                : await DescribeAsync(sid, handle, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Clicks a control, refusing to click one that is disabled.</summary>
    /// <param name="strategy">A strategy from <see cref="Strategies"/>.</param>
    /// <param name="target">The locator value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<ActionOutcome> ClickAsync(string strategy, string target, CancellationToken cancellationToken) =>
        RunAsync(async sid =>
        {
            var handle = await HandleAsync(sid, strategy, target, cancellationToken).ConfigureAwait(false);
            if (handle is null)
                return NotFound(strategy, target);

            var element = await DescribeAsync(sid, handle, cancellationToken).ConfigureAwait(false);
            if (!element.Enabled)
                return new ActionOutcome(false, $"'{target}' is disabled, so it was not clicked.", element);

            await PostAsync($"session/{sid}/element/{handle}/click", null, cancellationToken).ConfigureAwait(false);
            return new ActionOutcome(true, $"Clicked '{target}'.", element);
        }, cancellationToken);

    /// <summary>Types into a control and reads back what it now holds.</summary>
    /// <param name="strategy">A strategy from <see cref="Strategies"/>.</param>
    /// <param name="target">The locator value.</param>
    /// <param name="text">The text to send.</param>
    /// <param name="clear">Whether to clear the control first.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<ActionOutcome> TypeAsync(
        string strategy, string target, string text, bool clear, CancellationToken cancellationToken) =>
        RunAsync(async sid =>
        {
            var handle = await HandleAsync(sid, strategy, target, cancellationToken).ConfigureAwait(false);
            if (handle is null)
                return NotFound(strategy, target);

            var before = await DescribeAsync(sid, handle, cancellationToken).ConfigureAwait(false);
            if (!before.Enabled)
                return new ActionOutcome(false, $"'{target}' is disabled, so nothing was typed.", before);

            if (clear)
                await PostAsync($"session/{sid}/element/{handle}/clear", null, cancellationToken).ConfigureAwait(false);

            await PostAsync($"session/{sid}/element/{handle}/value", new { text }, cancellationToken)
                .ConfigureAwait(false);

            // Re-read rather than trusting the input: the caller wants to know what the control actually
            // holds now, which is not always what was sent (masking, max length, input filtering).
            var after = await DescribeAsync(sid, handle, cancellationToken).ConfigureAwait(false);
            return new ActionOutcome(true, $"Typed into '{target}'. It now reads '{after.Text}'.", after);
        }, cancellationToken);

    /// <summary>
    /// Polls until a control exists (and optionally is enabled), or the timeout elapses. Each poll is a
    /// fresh lookup, so this never returns a stale snapshot.
    /// </summary>
    /// <param name="strategy">A strategy from <see cref="Strategies"/>.</param>
    /// <param name="target">The locator value.</param>
    /// <param name="requireEnabled">Whether the control must also be enabled to satisfy the wait.</param>
    /// <param name="timeout">How long to keep polling.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task<ActionOutcome> WaitForAsync(
        string strategy,
        string target,
        bool requireEnabled,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        ElementSnapshot? last = null;

        while (true)
        {
            last = await FindAsync(strategy, target, cancellationToken).ConfigureAwait(false);
            if (last is not null && (!requireEnabled || last.Enabled))
                return new ActionOutcome(true, $"'{target}' is ready.", last);

            if (DateTime.UtcNow >= deadline)
            {
                var why = last is null
                    ? $"no control matched {strategy}='{target}'"
                    : $"'{target}' was found but stayed disabled";
                return new ActionOutcome(false,
                    $"Timed out after {timeout.TotalSeconds:0.#}s: {why}.", last);
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Renders the window offscreen and returns the PNG bytes.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<byte []> ScreenshotAsync(CancellationToken cancellationToken) =>
        RunAsync(async sid =>
        {
            var base64 = (await GetAsync($"session/{sid}/screenshot", cancellationToken).ConfigureAwait(false))
                .GetString() ?? string.Empty;
            return Convert.FromBase64String(base64);
        }, cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        http.Dispose();
        gate.Dispose();
    }

    private static ActionOutcome NotFound(string strategy, string target) =>
        new(false, $"No control matched {strategy}='{target}'. Call ui_snapshot to see what is on screen.");

    // Runs one operation inside a session, creating the session on first use. A session that has gone
    // away is re-established once and the operation retried, because that is the normal consequence of
    // the app restarting or another client taking the server's single session slot.
    private async Task<T> RunAsync<T>(Func<string, Task<T>> action, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sid = sessionId ??= await NewSessionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await action(sid).ConfigureAwait(false);
            }
            catch (WebDriverProtocolException ex) when (ex.Code == "invalid session id")
            {
                sessionId = null;
                var replacement = sessionId = await NewSessionAsync(cancellationToken).ConfigureAwait(false);
                return await action(replacement).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> NewSessionAsync(CancellationToken cancellationToken)
    {
        var value = await PostAsync("session", new { capabilities = new { } }, cancellationToken)
            .ConfigureAwait(false);

        return value.TryGetProperty("sessionId", out var id) && id.GetString() is { Length: > 0 } sid
            ? sid
            : throw new WebDriverProtocolException("session not created",
                $"The server at {Endpoint} accepted POST /session but returned no session id.");
    }

    private async Task<string?> HandleAsync(
        string sid, string strategy, string target, CancellationToken cancellationToken)
    {
        try
        {
            var value = await PostAsync(
                $"session/{sid}/element",
                new { @using = strategy, value = target },
                cancellationToken).ConfigureAwait(false);

            return value.TryGetProperty(ElementKey, out var handle) ? handle.GetString() : null;
        }
        catch (WebDriverProtocolException ex) when (ex.Code == "no such element")
        {
            // "Nothing matched" is an ordinary answer to a lookup, not a failure to report upwards.
            return null;
        }
    }

    // One request per field: the WebDriver protocol has no batch read, and these are the same endpoints
    // a Selenium client would use. The reads are not a single atomic observation of the control — if the
    // UI is changing underneath, fields can come from marginally different moments.
    private async Task<ElementSnapshot> DescribeAsync(
        string sid, string handle, CancellationToken cancellationToken)
    {
        var prefix = $"session/{sid}/element/{handle}";
        var rect = await GetAsync($"{prefix}/rect", cancellationToken).ConfigureAwait(false);

        return new ElementSnapshot(
            Id: await AttributeAsync(prefix, "id", cancellationToken).ConfigureAwait(false),
            Name: await AttributeAsync(prefix, "name", cancellationToken).ConfigureAwait(false),
            Role: await AttributeAsync(prefix, "role", cancellationToken).ConfigureAwait(false),
            ControlType: await AttributeAsync(prefix, "type", cancellationToken).ConfigureAwait(false),
            Value: await AttributeAsync(prefix, "value", cancellationToken).ConfigureAwait(false),
            Text: (await GetAsync($"{prefix}/text", cancellationToken).ConfigureAwait(false)).GetString()
                  ?? string.Empty,
            Enabled: await FlagAsync(prefix, "enabled", cancellationToken).ConfigureAwait(false),
            Visible: await FlagAsync(prefix, "visible", cancellationToken).ConfigureAwait(false),
            X: Number(rect, "x"),
            Y: Number(rect, "y"),
            Width: Number(rect, "width"),
            Height: Number(rect, "height"));
    }

    private static double Number(JsonElement rect, string name) =>
        rect.ValueKind == JsonValueKind.Object && rect.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : 0;

    private async Task<string> AttributeAsync(string prefix, string name, CancellationToken cancellationToken)
    {
        var value = await GetAsync($"{prefix}/attribute/{name}", cancellationToken).ConfigureAwait(false);

        // The protocol returns null for an absent attribute; report it as empty rather than "null".
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private async Task<bool> FlagAsync(string prefix, string name, CancellationToken cancellationToken) =>
        string.Equals(
            await AttributeAsync(prefix, name, cancellationToken).ConfigureAwait(false),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, path, null, cancellationToken);

    private Task<JsonElement> PostAsync(string path, object? body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body ?? new { }, cancellationToken);

    private async Task<JsonElement> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await SendRawAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        JsonElement value;
        try
        {
            using var document = JsonDocument.Parse(json);
            value = document.RootElement.TryGetProperty("value", out var v) ? v.Clone() : default;
        }
        catch (JsonException ex)
        {
            throw new WebDriverProtocolException("invalid response",
                $"{Endpoint} answered {path} with something that is not WebDriver JSON: {ex.Message}");
        }

        if (response.IsSuccessStatusCode)
            return value;

        // W3C error shape: {"value":{"error":"no such element","message":"..."}}
        var code = StringField(value, "error") ?? $"http {(int) response.StatusCode}";
        var message = StringField(value, "message") ?? json;
        throw new WebDriverProtocolException(code, message);
    }

    private static string? StringField(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field)
            ? field.GetString()
            : null;

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // The common first-run failure: the app is running but was started without a WebDriver
            // server, so nothing is listening. Say that, instead of surfacing "connection refused".
            throw new WebDriverProtocolException("not connected",
                $"Nothing is listening at {Endpoint}. Start the app under test with " +
                $"`new WebDriverServer (form, {Endpoint.Port}).Start ()` and leave it running ({ex.Message}).");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces its own timeout as cancellation; only translate the ones the caller
            // did not ask for.
            throw new WebDriverProtocolException("timeout",
                $"The app at {Endpoint} did not answer within {http.Timeout.TotalSeconds:0}s. Automation " +
                $"runs on its UI thread, so a blocked or unpumped thread — a modal dialog, a busy loop — " +
                $"stalls every command ({ex.Message}).");
        }
    }
}
