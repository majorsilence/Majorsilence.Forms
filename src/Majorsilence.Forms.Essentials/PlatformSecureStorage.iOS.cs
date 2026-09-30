using System.Threading.Tasks;
using Foundation;
using Security;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. iOS is the Keychain below.</summary>
    internal static class PlatformSecureStorage
    {
        public static ISecureStorageBackend Create () => new IosSecureStorageBackend ();
    }

    /// <summary>The iOS Keychain (<see cref="SecKeyChain"/>), scoped to this app by a fixed service name.</summary>
    internal sealed class IosSecureStorageBackend : ISecureStorageBackend
    {
        private const string ServiceName = "com.majorsilence.forms.securestorage";

        /// <inheritdoc />
        public bool IsSupported => true;

        /// <inheritdoc />
        public Task<string?> GetAsync (string key)
        {
            try {
                var record = new SecRecord (SecKind.GenericPassword) { Service = ServiceName, Account = key };
                var match = SecKeyChain.QueryAsRecord (record, out var status);
                if (status != SecStatusCode.Success || match?.ValueData is null)
                    return Task.FromResult<string?> (null);

                return Task.FromResult<string?> (NSString.FromData (match.ValueData, NSStringEncoding.UTF8)?.ToString ());
            } catch {
                // Never worth a crash: see SecureStorage's type summary.
                return Task.FromResult<string?> (null);
            }
        }

        /// <inheritdoc />
        public Task SetAsync (string key, string value)
        {
            try {
                var record = new SecRecord (SecKind.GenericPassword) { Service = ServiceName, Account = key };
                SecKeyChain.Remove (record);   // Add fails on a duplicate; remove-then-add is the documented update idiom.

                record.ValueData = NSData.FromString (value, NSStringEncoding.UTF8);
                SecKeyChain.Add (record);
            } catch {
                // Never worth a crash, per the type summary.
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Remove (string key)
        {
            try {
                var record = new SecRecord (SecKind.GenericPassword) { Service = ServiceName, Account = key };
                SecKeyChain.Remove (record);
            } catch {
                // Never worth a crash, per the type summary.
            }
        }
    }
}
