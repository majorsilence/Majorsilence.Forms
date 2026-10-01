# Majorsilence.Forms.Essentials

Platform capabilities that carry per-platform dependencies the Majorsilence.Forms core deliberately avoids: secure storage
and text to speech today.

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
