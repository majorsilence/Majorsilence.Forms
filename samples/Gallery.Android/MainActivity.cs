using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;

using MSForms = Majorsilence.Forms;
using MSFormsBackends = Majorsilence.Forms.Backends;

namespace Gallery.Android
{
    // All the Avalonia/Majorsilence.Forms wiring lives in App.cs (GalleryApplication/GalleryAvaloniaApp) --
    // by the time this Activity is created, AvaloniaMainActivity's base OnCreate already asks
    // GalleryApplication (this process's Android Application, an IAndroidApplication) for its
    // ApplicationLifetime.MainViewFactory and uses whatever Control it returns as this Activity's content,
    // so there is nothing left to override here.
    //
    // Theme MUST descend from Theme.AppCompat: AvaloniaMainActivity is an AppCompatActivity, and its
    // setContentView throws IllegalStateException under any other theme (this crashed the app on the first
    // frame when it was "@android:style/Theme.NoTitleBar"). See Resources/values/styles.xml.
    [Activity (
        Label = "Gallery.Android",
        Theme = "@style/GalleryTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
        // Register item F11: AvaloniaActivity.BackRequested is declared directly on the Activity -- there
        // is no generic "current Activity" accessor in the public API for AvaloniaPlatformBackend itself
        // to subscribe through (see its RaiseBackRequested doc comment) -- so the host app's own Activity
        // forwards it, one line, the same shape RunAndroid already requires of MainActivity/GalleryApplication.
        //
        // Register item F14: the same "no current Activity accessor" gap applies to requesting the
        // notification permission, so RegisterAndroidActivity gives AvaloniaPlatformBackend a live Activity
        // to call ActivityCompat.RequestPermissions on. ReportAndroidIntent forwards this Activity's own
        // Intent so a notification tap (LaunchMode.SingleTop routes a running instance's tap through
        // OnNewIntent below, not a fresh OnCreate) reaches LocalNotifications.Tapped either way.
        protected override void OnCreate (Bundle? savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            BackRequested += (_, e) => e.Handled = MSFormsBackends.AvaloniaPlatformBackend.RaiseBackRequested ();
            MSFormsBackends.AvaloniaPlatformBackend.RegisterAndroidActivity (this);
            MSFormsBackends.AvaloniaPlatformBackend.ReportAndroidIntent (Intent);
            RunKeepScreenAwakeSmokeTest ();
        }

        protected override void OnNewIntent (Intent? intent)
        {
            base.OnNewIntent (intent);
            MSFormsBackends.AvaloniaPlatformBackend.ReportAndroidIntent (intent);
        }

        public override void OnRequestPermissionsResult (int requestCode, string[] permissions, [global::Android.Runtime.GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult (requestCode, permissions, grantResults);
            MSFormsBackends.AvaloniaPlatformBackend.ReportNotificationPermissionResult ();
        }

        // Register item F12: Window.AddFlags/ClearFlags (AndroidKeepAwakeBackend) need a live Activity,
        // only available once RegisterAndroidActivity above has run -- unlike F8/F9/F13/F14's own smoke
        // tests (queued on a background thread from GalleryApplication.OnCreate, before any Activity
        // exists yet), this one runs here, synchronously, on the UI thread, right after the Activity
        // actually becomes available. android-smoke-test.sh greps for its PASS line the same way.
        private void RunKeepScreenAwakeSmokeTest ()
        {
            const string Tag = "F12_KEEPAWAKE_SMOKE";
            try {
                MSForms.Application.KeepScreenAwake = true;
                if (!MSForms.Application.KeepScreenAwake) {
                    global::Android.Util.Log.Error (Tag, "FAIL: KeepScreenAwake did not read back true after being set");
                    return;
                }

                MSForms.Application.KeepScreenAwake = false;
                if (MSForms.Application.KeepScreenAwake) {
                    global::Android.Util.Log.Error (Tag, "FAIL: KeepScreenAwake did not read back false after being cleared");
                    return;
                }

                global::Android.Util.Log.Info (Tag, "PASS: KeepScreenAwake set true then false, both read back correctly, no exception");
            } catch (Exception ex) {
                global::Android.Util.Log.Error (Tag, $"FAIL: {ex}");
            }
        }
    }
}
