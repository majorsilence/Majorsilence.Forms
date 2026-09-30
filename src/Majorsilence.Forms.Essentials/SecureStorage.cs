using System;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>
    /// What a platform actually does the storing: Android Keystore-backed preferences, the iOS Keychain, Windows Credential Manager,
    /// macOS Keychain Services, or the Linux Secret Service (register item F16). Internal so <see cref="SecureStorage"/> is the only
    /// public surface; a test injects its own via <see cref="SecureStorage.Backend"/>.
    /// </summary>
    internal interface ISecureStorageBackend
    {
        /// <summary>Whether this platform can actually store anything securely right now.</summary>
        bool IsSupported { get; }

        /// <summary>The value stored under <paramref name="key"/>, or null if there is none.</summary>
        Task<string?> GetAsync (string key);

        /// <summary>Stores a value, replacing what was there.</summary>
        Task SetAsync (string key, string value);

        /// <summary>Removes a value. Removing one that is not there is not an error.</summary>
        void Remove (string key);
    }

    /// <summary>
    /// A password or token held in the platform's own secure store, never in a plain file (register item F16). Every member
    /// degrades to doing nothing rather than throwing when the platform cannot help -- the same "never worth a crash" contract
    /// <c>Haptics</c> and <c>Media.AudioPlayer</c> already use for their own capability gaps. Check
    /// <see cref="IsSupported"/> before depending on a value actually having persisted.
    /// </summary>
    public static class SecureStorage
    {
        // Internal, not the Backends.Platform seam every UI-facing capability (Haptics, LocalNotifications, KeepScreenAwake)
        // routes through: which OS credential store exists has nothing to do with which UI backend (Avalonia, WinForms, Uno) is
        // active, so this picks its own implementation per target framework instead of asking AvaloniaPlatformBackend for one.
        internal static ISecureStorageBackend Backend { get; set; } = PlatformSecureStorage.Create ();

        /// <summary>Gets whether this platform can actually store anything securely right now.</summary>
        public static bool IsSupported => Backend.IsSupported;

        /// <summary>Gets the value stored under <paramref name="key"/>, or null if there is none or it cannot be read.</summary>
        public static Task<string?> GetAsync (string key)
        {
            ArgumentException.ThrowIfNullOrEmpty (key);
            return Backend.GetAsync (key);
        }

        /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, replacing what was there. Does nothing if <see cref="IsSupported"/> is false.</summary>
        public static Task SetAsync (string key, string value)
        {
            ArgumentException.ThrowIfNullOrEmpty (key);
            ArgumentNullException.ThrowIfNull (value);
            return Backend.SetAsync (key, value);
        }

        /// <summary>Removes the value stored under <paramref name="key"/>. Removing one that is not there, or that never persisted, is not an error.</summary>
        public static void Remove (string key)
        {
            ArgumentException.ThrowIfNullOrEmpty (key);
            Backend.Remove (key);
        }
    }
}
