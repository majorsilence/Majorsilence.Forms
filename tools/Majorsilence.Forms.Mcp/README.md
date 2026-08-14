# Majorsilence.Forms.Mcp

An [MCP](https://modelcontextprotocol.io) server that lets an AI assistant drive a running
[Majorsilence.Forms](https://github.com/majorsilence/Majorsilence.Forms) application: read its control
tree, click, type, wait, and take screenshots.

It is a `dotnet` global tool that speaks MCP over stdin/stdout and talks to the app under test over the
app's own W3C WebDriver endpoint — so it works against any Majorsilence.Forms app, on any backend, on
Windows, macOS, and Linux, without referencing the framework itself.

```
assistant  ──MCP/stdio──▶  majorsilence-mcp  ──HTTP/loopback──▶  your app (WebDriverServer)
```

## Install

```
dotnet tool install -g Majorsilence.Forms.Mcp
```

## Expose the app under test

The server drives an app through `Majorsilence.Forms.WebDriver`, which the app starts itself. Two lines,
usually behind a debug flag or a command-line switch:

```csharp
using var server = new WebDriverServer (form, 4444);
server.Start ();
```

Two prerequisites carry over from ordinary UI automation:

- **Name the controls you want to target.** `Control.Name` becomes the `id` every tool uses. An unnamed
  control can only be reached by XPath or position, which is exactly the brittleness worth avoiding.
- **A layout pass has to have run**, or every control's bounds are zero. Showing the form does this; in a
  headless process, `HeadlessRenderer.CapturePng (form, width, height)` does it explicitly.

## Point a client at it

Claude Code:

```
claude mcp add majorsilence-ui -- majorsilence-mcp --port 4444
```

Any client that launches MCP servers itself (Claude Desktop, editors, agent frameworks) takes the same
command in its own configuration format:

```json
{
  "mcpServers": {
    "majorsilence-ui": {
      "command": "majorsilence-mcp",
      "args": ["--port", "4444"]
    }
  }
}
```

Options: `--port <port>` (loopback, default 4444), `--url <url>` for a full base URL, or the
`MAJORSILENCE_MCP_URL` environment variable. `--help` prints the same summary.

## Tools

| Tool | Arguments | Returns |
|---|---|---|
| `ui_snapshot` | — | the whole control tree as XML |
| `ui_find` | `target`, `strategy` | id, name, role, type, value, text, enabled/visible, bounds |
| `ui_read` | `target`, `strategy` | what the control currently displays |
| `ui_click` | `target`, `strategy` | confirmation, or why it was refused |
| `ui_type` | `target`, `text`, `strategy`, `clear` | what the control reads afterwards |
| `ui_wait_for` | `target`, `strategy`, `timeoutMs`, `requireEnabled` | readiness, or why the wait timed out |
| `ui_screenshot` | — | a PNG of the window |

`strategy` defaults to `id` — the control's `Name`, and the only locator that stays stable as the UI
changes. The others are `name`, `xpath`, `role`, `type`, `text`, `css selector`, and `tag name`.

Every tool takes a locator rather than an element handle, so each call re-resolves what it acts on:
there is no handle to go stale between a find and an action.

`ui_click` and `ui_type` are marked destructive; the rest are marked read-only. Hosts use those hints
when deciding what a user has to approve.

## What it is for

- **Writing tests.** Let the assistant explore the real UI, then have it write xUnit/NUnit tests against
  the same automation tree. See [docs/automation.md](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/automation.md).
- **Reproducing a bug report.** Walk the steps, screenshot the result.
- **Checking a change.** Ask what the window looks like now, rather than describing it.

## Scope and safety

The automation surface is unauthenticated and can drive the application arbitrarily, so
`WebDriverServer` binds loopback only and `--port` follows it. Expose it in development and test builds,
not in anything shipped to users.

The server drives one window at a time — the one the app handed to `WebDriverServer`. Modal dialogs the
app opens on its UI thread block automation exactly as they block the UI.

MIT licensed, like the rest of Majorsilence.Forms.
