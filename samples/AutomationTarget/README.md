# AutomationTarget

A small app that exposes itself for automation, so you have something real to drive while learning the
tooling. It is the app-side half of [docs/automation.md](../../docs/automation.md): the MCP server, a
Selenium client, and plain `curl` are all clients of the endpoint it starts.

```
dotnet run --project samples/AutomationTarget -- --webdriver 4444
```

It prints the endpoint and the commands to drive it:

```
Automation endpoint: http://127.0.0.1:4444/
  status:    curl -s http://127.0.0.1:4444/status
  MCP:       claude mcp add majorsilence-ui -- majorsilence-mcp --port 4444
  Selenium:  new RemoteWebDriver (new Uri ("http://127.0.0.1:4444/"), options.ToCapabilities ())
```

`--webdriver <port>` picks the port (default 4444); `--no-webdriver` runs it as an ordinary app.

## What each control is for

| Control | `id` | Why it's here |
|---|---|---|
| Name text box | `nameBox` | Something to write to and read back |
| Greet / Clear | `greetButton`, `clearButton` | A click whose handler changes `greetingLabel` — proof the real event ran |
| Greeting label | `greetingLabel` | The assertion target for those clicks |
| Locked button | `lockedButton` | Never enabled, so a client can show you a refusal instead of a false success |
| Agree checkbox | `agreeCheck` | Toggling it enables Submit |
| Submit | `submitButton` | Starts disabled — this is what a wait-for-enabled is actually for |
| Last action label | `lastActionLabel` | The most recent action, in a form a client can read |
| Action log | `logList` | The same history, on screen |
| Instructions label | *(none)* | Deliberately unnamed: it appears in the tree with an empty id, which is what "name your controls" means in practice |

Every action is also written to stdout, so you can compare what a client *claims* it did against what the
app actually saw.

## Driving it from an assistant

With the [MCP server](../../tools/Majorsilence.Forms.Mcp) built or installed:

```
claude mcp add majorsilence-ui -- majorsilence-mcp --port 4444
```

Then ask for something end-to-end — *"type 'Grace Hopper' into nameBox, click greetButton, and read
greetingLabel"* — and you should get `Hello, Grace Hopper!` back. A worthwhile second exercise is
Submit: it can't be clicked until `agreeCheck` is ticked, so the assistant has to notice that and wait.

## Driving it with no client at all

```bash
curl -s http://127.0.0.1:4444/status
SID=$(curl -s -X POST http://127.0.0.1:4444/session -d '{}' | sed 's/.*"sessionId":"\([^"]*\)".*/\1/')
curl -s "http://127.0.0.1:4444/session/$SID/source"
```

## Two boundaries this sample runs into

- **Screenshots need the Headless backend.** This sample runs on Avalonia, so
  `GET /session/{id}/screenshot` (and the MCP server's `ui_screenshot`) will tell you screenshots are
  unavailable. That is the endpoint's boundary, not a bug: capture images from a headless test run.
- **A `ListBox`'s items are not in the automation tree.** Reading `logList` gets you the control, not its
  contents, which is why `lastActionLabel` exists. Mirroring state into a readable control is the general
  workaround.
