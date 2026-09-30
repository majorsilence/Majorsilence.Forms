using System;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Android is the AndroidKeyStore-backed AES key below.</summary>
    internal static class PlatformSecureStorage
    {
        public static ISecureStorageBackend Create () => new AndroidSecureStorageBackend ();
    }

    /// <summary>
    /// An AES-256/GCM key generated inside the AndroidKeyStore (the key material itself never leaves the hardware-backed
    /// keystore) encrypts each value; the ciphertext and its IV sit in a private-mode <c>SharedPreferences</c> file, so what is on
    /// disk is unreadable without the device's own keystore, satisfying the "not readable from plain files" criterion.
    /// </summary>
    /// <remarks>
    /// <see cref="KeyGenParameterSpec"/> (the API this key generation needs) is API 23+; this project's own library floor is API
    /// 21 (see the Haptics/notifications entries in COMPATIBILITY_MATRIX.md for the same floor on other capabilities), so
    /// <see cref="IsSupported"/> is a real version check, not an unconditional true -- a device below 23 answers honestly rather
    /// than the app crashing with a platform-compat (CA1416) violation at that gap.
    /// </remarks>
    internal sealed class AndroidSecureStorageBackend : ISecureStorageBackend
    {
        private const string KeyAlias = "majorsilence.forms.securestorage";
        private const string PreferencesName = "majorsilence.forms.securestorage";
        private const string Transformation = "AES/GCM/NoPadding";
        private const int GcmTagBits = 128;
        private const int MinimumApiLevel = 23;

        /// <inheritdoc />
        public bool IsSupported => OperatingSystem.IsAndroidVersionAtLeast (MinimumApiLevel);

        /// <inheritdoc />
        public Task<string?> GetAsync (string key)
        {
            if (!IsSupported)
                return Task.FromResult<string?> (null);

            try {
                var prefs = Application.Context.GetSharedPreferences (PreferencesName, FileCreationMode.Private);
                var stored = prefs?.GetString (key, null);
                if (string.IsNullOrEmpty (stored))
                    return Task.FromResult<string?> (null);

                var separator = stored.IndexOf ('.');
                if (separator < 0)
                    return Task.FromResult<string?> (null);

                var iv = Convert.FromBase64String (stored[..separator]);
                var cipherText = Convert.FromBase64String (stored[(separator + 1)..]);

                var cipher = Cipher.GetInstance (Transformation);
                cipher!.Init (CipherMode.DecryptMode, GetOrCreateKey (), new GCMParameterSpec (GcmTagBits, iv));
                var plain = cipher.DoFinal (cipherText);
                return Task.FromResult<string?> (plain is null ? null : Encoding.UTF8.GetString (plain));
            } catch {
                // Never worth a crash: a corrupt entry, a keystore that rejects the key, or any other failure reads as "not there".
                return Task.FromResult<string?> (null);
            }
        }

        /// <inheritdoc />
        public Task SetAsync (string key, string value)
        {
            if (!IsSupported)
                return Task.CompletedTask;

            try {
                var cipher = Cipher.GetInstance (Transformation);
                cipher!.Init (CipherMode.EncryptMode, GetOrCreateKey ());
                var iv = cipher.GetIV ()!;
                var cipherText = cipher.DoFinal (Encoding.UTF8.GetBytes (value))!;

                var stored = Convert.ToBase64String (iv) + "." + Convert.ToBase64String (cipherText);
                using var editor = Application.Context.GetSharedPreferences (PreferencesName, FileCreationMode.Private)?.Edit ();
                editor?.PutString (key, stored)?.Apply ();
            } catch {
                // Never worth a crash, per the type summary.
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Remove (string key)
        {
            try {
                using var editor = Application.Context.GetSharedPreferences (PreferencesName, FileCreationMode.Private)?.Edit ();
                editor?.Remove (key)?.Apply ();
            } catch {
                // Never worth a crash, per the type summary.
            }
        }

        // The guard is checked here too, not only by IsSupported in GetAsync/SetAsync above: CA1416 (the platform-compat
        // analyzer) needs a literal-value if (OperatingSystem.IsAndroidVersionAtLeast (23)) positively wrapping the call
        // site itself -- a negated early-return guard, and a guard through the MinimumApiLevel constant rather than the
        // literal, both still tripped it -- the same "the guard has to sit exactly at the gap" fix F13's own CA1416
        // finding documents (moving its guard to wrap the field access directly, one call frame closer than it started).
        private static ISecretKey GetOrCreateKey ()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast (23)) {
                var keyStore = KeyStore.GetInstance ("AndroidKeyStore")!;
                keyStore.Load (null);

                // A real CI finding, not guessed: KeyStore.GetKey returns the binding's IKey, and a plain C# (ISecretKey)
                // cast on that managed peer throws InvalidCastException at runtime (confirmed by F16_SECURESTORAGE_DEBUG's
                // own logged stack trace) -- .NET-for-Android's JNI interop needs JavaCast<T> to re-wrap the same
                // underlying Java object as a different bound interface, not a CLR cast.
                if (keyStore.IsKeyEntry (KeyAlias))
                    return keyStore.GetKey (KeyAlias, null)!.JavaCast<ISecretKey> ();

                var generator = KeyGenerator.GetInstance (KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
                var spec = new KeyGenParameterSpec.Builder (KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                    .SetBlockModes (KeyProperties.BlockModeGcm)
                    .SetEncryptionPaddings (KeyProperties.EncryptionPaddingNone)
                    .SetKeySize (256)
                    .Build ();
                generator.Init (spec);
                return generator.GenerateKey ()!;
            }

            throw new PlatformNotSupportedException ($"SecureStorage needs Android API {MinimumApiLevel}+.");
        }
    }
}
