#if ANDROID
using System;
using Android.App;
using Android.Content;
using AndroidX.Core.App;

// Android.App.NotificationChannel/NotificationImportance collide by name with this framework's own
// Notifications.NotificationChannel/NotificationImportance -- fully qualified below rather than imported,
// the same way AndroidAudioBackend references Media.IPlayingSound without importing Majorsilence.Forms.Media.

// android.permission.POST_NOTIFICATIONS is a dangerous permission (API 33+, requested at runtime through
// RequestPermission below); declaring it here, the same assembly-level way F13's VIBRATE permission is,
// merges it into any consuming app's manifest automatically.
[assembly: Android.App.UsesPermission (Android.Manifest.Permission.PostNotifications)]

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Android's local-notifications path: <see cref="NotificationManagerCompat"/> and
    /// <see cref="NotificationChannelCompat"/> (AndroidX Core -- already a transitive dependency of
    /// Avalonia.Android's own AppCompat theme requirement, #288, so no new package reference was needed),
    /// which handle the pre/post-API-26 channel split internally rather than this code needing two paths
    /// the way <see cref="AndroidHapticsBackend"/> does for its own, un-compat-wrapped <c>VibrationEffect</c>.
    /// Builder members are called as separate statements rather than fluently chained: AndroidX's Java
    /// binding declares every <c>Set*</c>/<c>Build</c> return as nullable (Java has no non-null annotation
    /// the binding can trust), even though the real contract always returns <c>this</c> -- calling each
    /// step on the one, never-reassigned builder local avoids ten-plus spurious nullable warnings without
    /// ten-plus null-forgiving operators scattered through the method bodies.
    /// </summary>
    /// <remarks>
    /// <see cref="RequestPermission"/> needs a live <see cref="Activity"/> to call
    /// <see cref="ActivityCompat.RequestPermissions"/> on -- unlike every other backend member on this page,
    /// which only ever need <see cref="Android.App.Application.Context"/>. Nothing in this assembly can
    /// discover "the current Activity" generically (the same gap <see cref="AvaloniaPlatformBackend.RaiseBackRequested"/>'s
    /// own remarks document), so the host app's <c>MainActivity</c> registers itself once, in <c>OnCreate</c>,
    /// through <see cref="AvaloniaPlatformBackend.RegisterAndroidActivity"/> -- one more line of the same
    /// shape <c>BackRequested</c> forwarding already requires. Tapping a notification is reported the same
    /// way: <c>MainActivity</c> forwards its own <see cref="Intent"/> from both <c>OnCreate</c> and
    /// <c>OnNewIntent</c> to <see cref="AvaloniaPlatformBackend.ReportAndroidIntent"/>, which recognises this
    /// backend's own extra key and raises <see cref="Notifications.LocalNotifications.Tapped"/> -- the host never needs to
    /// know that key itself.
    /// </remarks>
    internal sealed class AndroidNotificationBackend : INotificationBackend
    {
        // Also embedded, as a literal, in .github/scripts/android-smoke-test.sh's tap-replay check --
        // changing this value must update that script too, or its tap check silently stops proving anything.
        internal const string TappedExtraKey = "majorsilence_forms_notification_tapped_id";
        private const int PermissionRequestCode = 0x4D46;   // 'MF', arbitrary but stable and unlikely to collide with a host app's own request codes

        public bool IsPermissionGranted {
            get {
                var context = global::Android.App.Application.Context;
                var manager = context is null ? null : NotificationManagerCompat.From (context);
                return manager is not null && manager.AreNotificationsEnabled ();
            }
        }

        public void RequestPermission ()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast (33))
                return;   // no runtime permission exists below API 33 -- IsPermissionGranted already answers true there

            if (IsPermissionGranted)
                return;

            var activity = AvaloniaPlatformBackend.CurrentAndroidActivity;
            if (activity is null)
                return;   // never worth a crash: the host has simply not registered one (see the class remarks)

            try {
                ActivityCompat.RequestPermissions (activity, new[] { global::Android.Manifest.Permission.PostNotifications }, PermissionRequestCode);
            } catch {
            }
        }

        public void RegisterChannel (string id, string name, string? description, Notifications.NotificationImportance importance, bool sound)
        {
            var context = global::Android.App.Application.Context;
            if (context is null)
                return;

            try {
                var builder = new NotificationChannelCompat.Builder (id, ImportanceFor (importance));
                builder.SetName (name);

                if (description is { Length: > 0 })
                    builder.SetDescription (description);

                if (!sound)
                    builder.SetSound (null, null);

                var built = builder.Build ();
                if (built is not null)
                    NotificationManagerCompat.From (context)?.CreateNotificationChannel (built);
            } catch {
            }
        }

        public void Show (int id, string channelId, string title, string text, bool ongoing, bool fullScreen)
        {
            var context = global::Android.App.Application.Context;
            if (context is null)
                return;

            try {
                var builder = new NotificationCompat.Builder (context, channelId);
                builder.SetContentTitle (title);
                builder.SetContentText (text);
                builder.SetOngoing (ongoing);
                builder.SetAutoCancel (!ongoing);
                builder.SetSmallIcon (context.ApplicationInfo?.Icon ?? 0);

                var contentIntent = TapPendingIntent (context, id);
                if (contentIntent is not null) {
                    builder.SetContentIntent (contentIntent);

                    if (fullScreen)
                        builder.SetFullScreenIntent (contentIntent, true);
                }

                var built = builder.Build ();
                if (built is not null)
                    NotificationManagerCompat.From (context)?.Notify (id, built);
            } catch {
                // a missing channel, a revoked permission mid-call, or a platform failure: never worth a crash
            }
        }

        public void Cancel (int id)
        {
            var context = global::Android.App.Application.Context;
            if (context is null)
                return;

            try {
                NotificationManagerCompat.From (context)?.Cancel (id);
            } catch {
            }
        }

        // The app's own launcher activity, found generically the same well-known way an Android "reopen my
        // app" shortcut always does -- PackageManager.GetLaunchIntentForPackage -- so no per-app
        // registration is needed just to build this, unlike RequestPermission's Activity requirement above.
        private static PendingIntent? TapPendingIntent (Context context, int id)
        {
            var launch = context.PackageManager?.GetLaunchIntentForPackage (context.PackageName ?? string.Empty);
            if (launch is null)
                return null;

            launch.SetFlags (ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            launch.PutExtra (TappedExtraKey, id);

            var flags = PendingIntentFlags.UpdateCurrent;
            // Immutable needs API 23+; every PendingIntent was implicitly mutable before that flag existed,
            // and this one is never updated with new extras later, so leaving it unset below API 23 is
            // correct, not just a version-guard formality.
            if (OperatingSystem.IsAndroidVersionAtLeast (23))
                flags |= PendingIntentFlags.Immutable;

            return PendingIntent.GetActivity (context, id, launch, flags);
        }

        private static int ImportanceFor (Notifications.NotificationImportance importance) => importance switch {
            Notifications.NotificationImportance.Low => NotificationManagerCompat.ImportanceLow,
            Notifications.NotificationImportance.High => NotificationManagerCompat.ImportanceHigh,
            _ => NotificationManagerCompat.ImportanceDefault,
        };
    }
}
#endif
