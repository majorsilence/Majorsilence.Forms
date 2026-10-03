# Majorsilence.Forms.Essentials

Platform capabilities that carry per-platform dependencies the Majorsilence.Forms core deliberately avoids: secure storage,
text to speech, opening links in another app, and reading files shipped inside the app.

```csharp
using Majorsilence.Forms.Essentials;

if (SecureStorage.IsSupported)
{
    await SecureStorage.SetAsync ("ntfy.password", password);
    var stored = await SecureStorage.GetAsync ("ntfy.password");
    SecureStorage.Remove ("ntfy.password");
}

if (Speech.IsSupported)
    await Speech.SpeakAsync ("The workshop is getting warm.", new SpeechOptions { Rate = 0.9f });
```

- **`SecureStorage`** stores a string under a key in the platform's own secure store, never a plain file: the AndroidKeyStore
  (an AES-256/GCM key generated inside the keystore encrypts each value before it reaches `SharedPreferences`), the iOS
  Keychain, Windows Credential Manager, macOS Keychain Services, or the Linux Secret Service (via `secret-tool`; a missing
  keyring daemon or `libsecret-tools` package means `IsSupported` is false rather than falling back to an unencrypted file).
- **`Speech`** reads a line aloud with the platform's own voice: Android `TextToSpeech`, iOS `AVSpeechSynthesizer`, macOS's
  `say`, Linux's `espeak`/`espeak-ng`, or Windows via a short PowerShell script over `System.Speech.Synthesis`. `SpeakAsync`
  completes when the line finishes, is cancelled through its `CancellationToken`, or the platform reports a failure; a
  missing `espeak`/`espeak-ng` on Linux means `IsSupported` is false there too, the same honesty `SecureStorage`'s own
  Linux row has.
- Every member degrades to doing nothing (`SecureStorage.Get` returns null, `Set`/`Remove` and `Speech.SpeakAsync` do
  nothing) rather than throwing when the platform cannot help — the same contract `Haptics` and `Media.AudioPlayer` already
  use for their own capability gaps. Check `IsSupported` before depending on a value having actually persisted or a line
  actually having been read.
- Neither is routed through the `Backends.Platform` seam every UI-facing capability (`Haptics`, `LocalNotifications`,
  `Application.KeepScreenAwake`) uses: which OS credential store or speech engine exists has nothing to do with which UI
  backend (Avalonia, WinForms, Uno) is active.

## Launcher

Hands a URI to another app from one call on Android, iOS and desktop. `Process.Start` does not work on mobile or in the browser.

```csharp
using Majorsilence.Forms.Essentials;

if (!await Launcher.OpenAsync ("https://example.com/post"))
    ShowMessage ("No app can open that link.");
```

- Only `http`, `https`, `mailto`, `tel` and `sms` URIs are forwarded (`Launcher.CanOpen`). `file:`, `javascript:` and custom schemes
  return false, because the URI is often content from a document or a feed.
- Returns false, never throws, when the URI is refused, the platform cannot launch it, or no app handles it.
- Android starts an `ACTION_VIEW` intent, iOS calls `UIApplication.OpenUrl`, and desktop uses the shell (`open`, `xdg-open`, or
  `UseShellExecute`). The browser (WebAssembly) reports `IsSupported` false.

## FileSystem

Reads files shipped inside the app the same way everywhere: Android assets, the iOS app bundle, or beside the executable on desktop.

```csharp
await using var stream = await FileSystem.OpenAppPackageFileAsync ("data/seed.json");

// Code that needs a real path (SQLite, say) gets a copy in the app's data folder:
var path = await FileSystem.CopyAppPackageFileAsync ("blog.sqlite");
```

- Names are relative, with `/` or `\` separators. An absolute path or a `..` segment throws `ArgumentException`.
- `AppPackageFileExistsAsync` checks for a file without throwing; `AppDataDirectory` is a writable folder that survives updates.
- Mark the file as an `AndroidAsset`, a `BundleResource` (iOS) or a `Content` item with `CopyToOutputDirectory` (desktop).
- The Android stream is buffered in memory (asset streams are not seekable). On the browser, desktop-style lookup is best effort.
- The iOS backends are not compiled or run by this repository's CI on Linux; treat them as unverified until built on a Mac.
