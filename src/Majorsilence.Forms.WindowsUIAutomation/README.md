# Majorsilence.Forms.WindowsUIAutomation

**Screen-reader accessibility for [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms)
apps on Windows.**

Majorsilence.Forms draws every control itself, so there are no native child windows for the platform
to expose. This package projects the framework's backend-neutral automation tree onto the **Windows
UI Automation** provider API, so Narrator, NVDA, JAWS and focus-following magnifiers can read and
drive a Majorsilence.Forms window like any other Windows application. Inspect tools such as
`inspect.exe` and Accessibility Insights see it too.

Windows-only. Off Windows it builds as an empty placeholder assembly, so a cross-platform solution
still compiles everywhere.

## Install

```bash
dotnet add package Majorsilence.Forms.WindowsUIAutomation
```

## Use it

One call per window, after it exists:

```csharp
using Majorsilence.Forms.WindowsUIAutomation;

WindowsUIAutomation.Enable (form);   // detaches automatically when the window closes
```

```vb
Imports Majorsilence.Forms.WindowsUIAutomation

WindowsUIAutomation.Enable(form)     ' detaches automatically when the window closes
```

Control roles map to UI Automation control types, and buttons, text boxes, combo boxes, check boxes
and radio buttons expose the Invoke, Value and Toggle patterns.

## The same tree, three consumers

Accessibility, tests and remote automation all read one tree — anything a screen reader can find,
a test can find:

- In-process UI tests, and headless rendering in CI
- [`Majorsilence.Forms.WebDriver`](https://www.nuget.org/packages/Majorsilence.Forms.WebDriver) —
  a W3C WebDriver server for Selenium and friends
- This package — Windows screen readers and magnifiers

## Links

- [Automation & UI testing guide](https://forms.majorsilence.com/automation/)
- [Documentation](https://forms.majorsilence.com)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)

Licensed under the MIT License.
