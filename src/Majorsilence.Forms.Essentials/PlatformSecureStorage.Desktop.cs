using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Desktop dispatches to Windows, macOS or Linux at run time.</summary>
    internal static class PlatformSecureStorage
    {
        public static ISecureStorageBackend Create () => new DesktopSecureStorageBackend ();
    }

    /// <summary>
    /// Windows Credential Manager, macOS Keychain Services, or the Linux Secret Service (via <c>secret-tool</c>), told apart at run
    /// time -- the same shape <c>Backends.DesktopKeepAwake</c> already uses for one desktop build to run on all three.
    /// </summary>
    internal sealed class DesktopSecureStorageBackend : ISecureStorageBackend
    {
        // "MajorsilenceForms" rather than the consuming app's own name: nothing here knows what app it is running in, the same
        // reasoning DesktopKeepAwake's Linux inhibitor gives itself a fixed --who.
        private const string Prefix = "MajorsilenceForms:";
        private const string LinuxSchema = "com.majorsilence.forms.securestorage";

        private bool? linux_secret_tool_available;

        /// <inheritdoc />
        public bool IsSupported => Dispatch (
            OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
            () => true, () => true, LinuxSecretToolAvailable);

        /// <inheritdoc />
        public Task<string?> GetAsync (string key) => Task.FromResult (Dispatch (
            OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
            () => GetWindows (key), () => GetMacOS (key), () => GetLinux (key)));

        /// <inheritdoc />
        public Task SetAsync (string key, string value)
        {
            Dispatch (
                OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
                () => SetWindows (key, value), () => SetMacOS (key, value), () => SetLinux (key, value));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Remove (string key) => Dispatch (
            OperatingSystem.IsWindows, OperatingSystem.IsMacOS, OperatingSystem.IsLinux,
            () => RemoveWindows (key), () => RemoveMacOS (key), () => RemoveLinux (key));

        // Every predicate and action injected, the same shape DesktopKeepAwake.Dispatch uses, so a test can drive all three
        // branches without depending on which OS the test itself happens to run on.
        internal static T Dispatch<T> (Func<bool> isWindows, Func<bool> isMacOS, Func<bool> isLinux, Func<T> onWindows, Func<T> onMacOS, Func<T> onLinux)
        {
            try {
                if (isWindows ())
                    return onWindows ();
                if (isMacOS ())
                    return onMacOS ();
                if (isLinux ())
                    return onLinux ();
            } catch {
                // See the type summary: any platform failure degrades to "not there"/"not supported", never a crash.
            }

            return default!;
        }

        internal static void Dispatch (Func<bool> isWindows, Func<bool> isMacOS, Func<bool> isLinux, Action onWindows, Action onMacOS, Action onLinux)
            => Dispatch (isWindows, isMacOS, isLinux, () => { onWindows (); return true; }, () => { onMacOS (); return true; }, () => { onLinux (); return true; });

        // ---- Windows: CredWrite / CredRead / CredDelete (wincred.h) --------------------------------
        // Written from the documented Win32 API; not run on Windows -- this machine has none, same as DesktopKeepAwake's own
        // Windows section. CRED_PERSIST_LOCAL_MACHINE survives a reboot, matching the "persists across an app restart" criterion.

        private const uint CRED_TYPE_GENERIC = 1;
        private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        [StructLayout (LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport ("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWriteW (ref CREDENTIAL credential, uint flags);

        [DllImport ("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredReadW (string targetName, uint type, uint flags, out IntPtr credentialPtr);

        [DllImport ("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDeleteW (string targetName, uint type, uint flags);

        [DllImport ("advapi32.dll")]
        private static extern void CredFree (IntPtr credentialPtr);

        private static string? GetWindows (string key)
        {
            if (!CredReadW (Prefix + key, CRED_TYPE_GENERIC, 0, out var ptr))
                return null;

            try {
                var credential = Marshal.PtrToStructure<CREDENTIAL> (ptr);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                    return null;

                var bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy (credential.CredentialBlob, bytes, 0, bytes.Length);
                return Encoding.Unicode.GetString (bytes);
            } finally {
                CredFree (ptr);
            }
        }

        private static void SetWindows (string key, string value)
        {
            var target = Prefix + key;
            var blob = Encoding.Unicode.GetBytes (value);
            var blobPtr = Marshal.AllocHGlobal (blob.Length);
            var targetPtr = Marshal.StringToHGlobalUni (target);

            try {
                Marshal.Copy (blob, 0, blobPtr, blob.Length);

                var credential = new CREDENTIAL {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = targetPtr,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = blobPtr,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                };

                _ = CredWriteW (ref credential, 0);   // failure degrades to "not stored", per the type summary
            } finally {
                Marshal.FreeHGlobal (blobPtr);
                Marshal.FreeHGlobal (targetPtr);
            }
        }

        private static void RemoveWindows (string key) => CredDeleteW (Prefix + key, CRED_TYPE_GENERIC, 0);

        // ---- macOS: SecKeychainAddGenericPassword / Find / ItemDelete (Security.framework) --------
        // Written from the documented Keychain Services API via raw P/Invoke (no Xamarin.Mac/.NET-for-macOS binding referenced
        // here, the same reasoning DesktopKeepAwake's macOS section gives for going straight to CoreFoundation/IOKit); not run on
        // macOS -- this machine has none. The generic-password calls take plain byte buffers directly, unlike the newer
        // SecItem* API's CFDictionary-based parameters, which is why this uses the older surface.

        private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
        private const string ServiceName = "com.majorsilence.forms.securestorage";

        [DllImport (SecurityLibrary)]
        private static extern int SecKeychainAddGenericPassword (
            IntPtr keychain, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName,
            uint passwordLength, byte[] passwordData, out IntPtr itemRef);

        [DllImport (SecurityLibrary)]
        private static extern int SecKeychainFindGenericPassword (
            IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName,
            out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

        [DllImport (SecurityLibrary)]
        private static extern int SecKeychainItemModifyContent (IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

        [DllImport (SecurityLibrary)]
        private static extern int SecKeychainItemDelete (IntPtr itemRef);

        [DllImport (SecurityLibrary)]
        private static extern int SecKeychainItemFreeContent (IntPtr attrList, IntPtr data);

        private static string? GetMacOS (string key)
        {
            var service = Encoding.UTF8.GetBytes (ServiceName);
            var account = Encoding.UTF8.GetBytes (key);

            var status = SecKeychainFindGenericPassword (IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                out var length, out var data, out var itemRef);

            if (status != 0 || length == 0)
                return null;

            try {
                var bytes = new byte[length];
                Marshal.Copy (data, bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString (bytes);
            } finally {
                _ = SecKeychainItemFreeContent (IntPtr.Zero, data);
                if (itemRef != IntPtr.Zero)
                    CFRelease (itemRef);
            }
        }

        private static void SetMacOS (string key, string value)
        {
            var service = Encoding.UTF8.GetBytes (ServiceName);
            var account = Encoding.UTF8.GetBytes (key);
            var password = Encoding.UTF8.GetBytes (value);

            var status = SecKeychainFindGenericPassword (IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                out _, out _, out var existing);

            if (status == 0 && existing != IntPtr.Zero) {
                try {
                    _ = SecKeychainItemModifyContent (existing, IntPtr.Zero, (uint)password.Length, password);
                } finally {
                    CFRelease (existing);
                }
                return;
            }

            _ = SecKeychainAddGenericPassword (IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                (uint)password.Length, password, out var created);   // failure degrades to "not stored", per the type summary
            if (created != IntPtr.Zero)
                CFRelease (created);
        }

        private static void RemoveMacOS (string key)
        {
            var service = Encoding.UTF8.GetBytes (ServiceName);
            var account = Encoding.UTF8.GetBytes (key);

            var status = SecKeychainFindGenericPassword (IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                out _, out _, out var itemRef);

            if (status != 0 || itemRef == IntPtr.Zero)
                return;

            try {
                _ = SecKeychainItemDelete (itemRef);
            } finally {
                CFRelease (itemRef);
            }
        }

        [DllImport ("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease (IntPtr cf);

        // ---- Linux: secret-tool (libsecret), the Secret Service over D-Bus --------------------------
        // Shells out rather than a hand-rolled D-Bus client, the same choice DesktopKeepAwake's Linux section makes for
        // systemd-inhibit. Unlike KeepAwake, a missing secret-tool (no libsecret-tools package, or no keyring daemon running --
        // common on a headless box) means secrets genuinely cannot be stored securely here, so IsSupported reports false rather
        // than falling back to a plain file, per the "not readable from plain files" acceptance criterion.

        private bool LinuxSecretToolAvailable ()
        {
            if (linux_secret_tool_available is { } cached)
                return cached;

            try {
                using var probe = Process.Start (new ProcessStartInfo ("secret-tool", "--version") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                });
                probe?.WaitForExit (2000);
                linux_secret_tool_available = probe is { HasExited: true, ExitCode: 0 };
            } catch {
                linux_secret_tool_available = false;
            }

            return linux_secret_tool_available.Value;
        }

        private string? GetLinux (string key)
        {
            if (!LinuxSecretToolAvailable ())
                return null;

            var (output, exitCode) = RunSecretTool (null, "lookup", "schema", LinuxSchema, "account", key);
            return exitCode == 0 && output.Length > 0 ? output.TrimEnd ('\n') : null;
        }

        private void SetLinux (string key, string value)
        {
            if (!LinuxSecretToolAvailable ())
                return;

            RunSecretTool (value, "store", "--label", $"Majorsilence.Forms: {key}", "schema", LinuxSchema, "account", key);
        }

        private void RemoveLinux (string key)
        {
            if (!LinuxSecretToolAvailable ())
                return;

            RunSecretTool (null, "clear", "schema", LinuxSchema, "account", key);
        }

        // Every argument here is either a fixed literal or a caller-supplied key/value passed through ArgumentList, never
        // interpolated into a shell string, so nothing needs escaping. `stdin` carries the secret for `store`; every other
        // command ignores it.
        private static (string Output, int ExitCode) RunSecretTool (string? stdin, params string[] arguments)
        {
            var start = new ProcessStartInfo ("secret-tool") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin is not null,
                UseShellExecute = false,
            };
            foreach (var arg in arguments)
                start.ArgumentList.Add (arg);

            using var process = Process.Start (start);
            if (process is null)
                return ("", -1);

            if (stdin is not null) {
                process.StandardInput.Write (stdin);
                process.StandardInput.Close ();
            }

            var output = process.StandardOutput.ReadToEnd ();
            process.WaitForExit (5000);
            return (output, process.HasExited ? process.ExitCode : -1);
        }
    }
}
