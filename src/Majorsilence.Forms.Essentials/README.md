# Majorsilence.Forms.Essentials

Platform capabilities that carry per-platform dependencies the Majorsilence.Forms core deliberately avoids. Today that is secure
storage; text to speech is planned next.

```csharp
using Majorsilence.Forms.Essentials;

if (SecureStorage.IsSupported)
{
    await SecureStorage.SetAsync ("ntfy.password", password);
    var stored = await SecureStorage.GetAsync ("ntfy.password");
    SecureStorage.Remove ("ntfy.password");
}
```

- **`SecureStorage`** stores a string under a key in the platform's own secure store, never a plain file: the AndroidKeyStore
  (an AES-256/GCM key generated inside the keystore encrypts each value before it reaches `SharedPreferences`), the iOS
  Keychain, Windows Credential Manager, macOS Keychain Services, or the Linux Secret Service (via `secret-tool`; a missing
  keyring daemon or `libsecret-tools` package means `IsSupported` is false rather than falling back to an unencrypted file).
- Every member degrades to doing nothing (`Get` returns null, `Set`/`Remove` do nothing) rather than throwing when the platform
  cannot help — the same contract `Haptics` and `Media.AudioPlayer` already use for their own capability gaps. Check
  `IsSupported` before depending on a value having actually persisted.
- Not routed through the `Backends.Platform` seam every UI-facing capability (`Haptics`, `LocalNotifications`,
  `Application.KeepScreenAwake`) uses: which OS credential store exists has nothing to do with which UI backend (Avalonia,
  WinForms, Uno) is active.
