# Majorsilence.Forms.WebDriver

A **W3C WebDriver server** for [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms)
applications — so you can drive a cross-platform WinForms app from Selenium, or from any
WebDriver-speaking client, on Windows, macOS or Linux.

It maps WebDriver commands onto the framework's backend-neutral automation tree: element location by
id, name, role and text; neutral input injection; and offscreen screenshots. No display server and
no browser are involved.

## Install

```bash
dotnet add package Majorsilence.Forms.WebDriver
```

## Use it

Start the server against the window under test, then point any WebDriver client at it:

```csharp
using Majorsilence.Forms.WebDriver;

using var server = new WebDriverServer (form, port: 4444);
server.Start ();

Console.WriteLine (server.Url);      // http://127.0.0.1:4444/  (loopback only)

// … drive it from Selenium, or any WebDriver client in any language …

server.Stop ();
```

Supported commands: new/delete session, find element(s), click, send keys, clear, get text, get name
(role), get attribute, get rect, get enabled, page source and screenshot. It is a practical subset,
not a full conformance implementation.

A click, send keys or clear that opens a modal dialog answers as soon as the dialog is up, so the
client can go on to drive the dialog. The tree includes the contents of MDI child windows, and a click
on a menu item inside a closed menu runs that item's command, as UI Automation's Invoke does.

File dialogs are the operating system's picker on a desktop backend, outside the automation tree. Queue
the answer before the click that opens one, with the vendor command
`POST /session/{id}/majorsilence/file-dialog`: `{"open": ["/path/a.txt"]}` (an empty list cancels),
`{"save": "/path/out.csv"}` (`null` cancels) or `{"clear": true}`. The next dialog takes the answer instead
of showing the picker. In-process tests do the same through `Majorsilence.Forms.Automation.FileDialogAnswers`.

The same automation tree also backs in-process UI tests, Windows screen readers via
[`Majorsilence.Forms.WindowsUIAutomation`](https://www.nuget.org/packages/Majorsilence.Forms.WindowsUIAutomation),
and AI agents through the repository's MCP server.

## Links

- [Automation & UI testing guide](https://forms.majorsilence.com/automation/) — page objects, waits,
  visual regression, CI recipes
- [Documentation](https://forms.majorsilence.com)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)

Licensed under the MIT License.
