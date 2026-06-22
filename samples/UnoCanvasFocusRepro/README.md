# Uno macOS Skia head — keyboard focus / text input into a nested `SKXamlCanvas`

A minimal, **self-contained** repro (no third-party UI framework — just Uno + SkiaSharp) of a problem
embedding a custom-drawn `SKXamlCanvas` inside an Uno app on the **macOS Skia head**: the canvas can't be
made the keyboard focus target, so typing produces the macOS "unhandled key" **beep** per keystroke and no
`CharacterReceived` reaches the canvas.

## Run

```
dotnet run --project samples/UnoCanvasFocusRepro
```

(Desktop session required. On macOS it uses `Uno.UI.Runtime.Skia.MacOS`.)

## What you'll see

The window has a native WinUI `TextBox` (top) and a custom `SKXamlCanvas` (dark area), plus a live log.

1. Click the **native TextBox** and type → works (focus + text), as expected.
2. Click the **SKXamlCanvas** and type:
   - **Windows / X11 heads:** the log shows `canvas routed KeyDown …` / `canvas CharacterReceived '…'`.
   - **macOS Skia head:** macOS **beeps** on every key, **no** `canvas CharacterReceived` appears, and the log
     shows `Focus() returned True` but `FocusManager.GetFocusedElement(...) = <null>` — focus never settles on
     the canvas. `FocusManager.GotFocus → <null>` (or a top-level native control) appears instead.

## Expected vs actual (macOS Skia head)

| | Expected | Actual |
|---|---|---|
| `SKXamlCanvas.Focus()` returns true | focus settles on canvas | returns **true** but `FocusManager.GetFocusedElement` is **`<null>`** |
| Type over the focused canvas | `CharacterReceived` / `KeyDown` on the canvas | **no** `CharacterReceived`; macOS **NSBeep** per key |
| Pointer-down on the canvas | keeps/sets focus | focus resets to **`<null>`** |

Notably, even a **nested native `TextBox`** (not just the `SKXamlCanvas`) fails to retain focus when placed
inside a child container — only the window's top-level native controls hold focus.

## Questions for the Uno team

1. What's the supported way to make a custom/nested element the keyboard focus target on the macOS Skia head
   so it receives `CharacterReceived` (and macOS stops beeping)? Is there an `NSTextInputClient` /
   first-responder requirement for nested / non-`Control` content?
2. Why does `Focus()` return `true` while `FocusManager.GetFocusedElement` reports `<null>` for nested
   elements (including a nested native `TextBox`), and why does a pointer-down on the canvas drop focus to
   `<null>`? Bug, or expected?
3. Is there a supported way to suppress the unhandled-key `NSBeep` / mark a key handled at the native level?
   Setting `Handled` on the routed `KeyDownEventArgs` (when it fires) does not stop it.

## Environment

- Uno.WinUI **6.5.237**, runtime Skia heads (X11 / Win32 / MacOS) 6.5.237
- SkiaSharp.Views.Uno.WinUI 3.119.4
- .NET 10, macOS (Apple Silicon), built via `dotnet run`
