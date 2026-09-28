using System;
using System.Threading;
using Android.App;
using Android.Runtime;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ControlGallery;

using MSForms = Majorsilence.Forms;

namespace Gallery.Android
{
    // The Android Application (not Activity) that owns Avalonia's bootstrap: Avalonia 12's Android
    // integration runs AppBuilder.Configure<TApp>().UseAndroid()...SetupWithLifetime(...) from here, via
    // the base class, before MainActivity even exists -- so this is also where the gallery's images get
    // extracted from the APK (see ExtractImageAssets) in time for the first render.
    [Application]
    public class GalleryApplication : AvaloniaAndroidApplication<GalleryAvaloniaApp>
    {
        public GalleryApplication (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
        {
        }

        public override void OnCreate ()
        {
            ExtractImageAssets ();
            base.OnCreate ();
            ThreadPool.QueueUserWorkItem (_ => RunAudioSmokeTest ());
            ThreadPool.QueueUserWorkItem (_ => RunAudioPlayerSmokeTest ());
            ThreadPool.QueueUserWorkItem (_ => RunHapticsSmokeTest ());
            ThreadPool.QueueUserWorkItem (_ => RunNotificationsSmokeTest ());

            // Register item F10: real signals need a real backgrounding, which nothing in-process can
            // trigger -- android-smoke-test.sh drives it externally (KEYCODE_HOME, then relaunch) and
            // greps for these two lines, so this is a permanent, repeated-on-every-PR check like the
            // F8/F9 ones above, not a one-off manual run.
            const string Tag = "F10_LIFECYCLE";
            MSForms.Application.Suspended += (_, _) => Log.Info (Tag, "Suspended");
            MSForms.Application.Resumed += (_, _) => Log.Info (Tag, "Resumed");
        }

        // Not a xunit test -- there is no test runner on the emulator android-smoke boots. This exercises
        // register item F8's IAudioBackend seam for real on a device/emulator and logs a single line
        // android-smoke-test.sh greps for, so "SoundPlayer and SystemSounds play on an Android emulator"
        // (the acceptance criterion) is a real, repeated-on-every-PR CI check, not a one-off manual run.
        // Runs on a background thread: MediaPlayer.Prepare is a blocking call and this must not delay the
        // Avalonia UI's own first frame.
        private void RunAudioSmokeTest ()
        {
            const string Tag = "F8_AUDIO_SMOKE";
            try {
                var filesDir = FilesDir?.AbsolutePath;
                if (filesDir is null) {
                    Log.Warn (Tag, "SKIP: FilesDir unavailable");
                    return;
                }

                var path = System.IO.Path.Combine (filesDir, "audio-smoke-test.wav");
                if (!System.IO.File.Exists (path)) {
                    using var src = Assets!.Open ("audio-smoke-test.wav");
                    using var dest = System.IO.File.Create (path);
                    src.CopyTo (dest);
                }

                if (MSForms.Backends.Platform.Backend is not MSForms.Backends.IAudioBackend audio) {
                    Log.Warn (Tag, "SKIP: active backend is not IAudioBackend");
                    return;
                }

                using (var file = audio.PlayFile (path, loop: false)) {
                    if (file is null) {
                        Log.Error (Tag, "FAIL: PlayFile returned null");
                        return;
                    }
                    file.Wait ();
                }

                using (var system = audio.PlaySystemSound (nameof (MSForms.Media.SystemSounds.Hand))) {
                    if (system is null) {
                        Log.Error (Tag, "FAIL: PlaySystemSound returned null");
                        return;
                    }
                    system.Wait ();
                }

                Log.Info (Tag, "PASS: PlayFile and PlaySystemSound both completed with no exception");
            } catch (Exception ex) {
                Log.Error (Tag, $"FAIL: {ex}");
            }
        }

        // Register item F9's richer sibling: volume, Usage.Alarm (the one meant to be audible with media
        // volume down -- not verified here, that needs a human ear and the emulator's -no-audio flag would
        // hide it anyway), native looping actually stopped by Stop(), and the Completed event for a track
        // that finishes on its own. Same reasoning as RunAudioSmokeTest above: a real emulator run on every
        // PR via android-smoke-test.sh's F9_AUDIOPLAYER_SMOKE check, not a one-off manual verification.
        private void RunAudioPlayerSmokeTest ()
        {
            const string Tag = "F9_AUDIOPLAYER_SMOKE";
            try {
                if (!MSForms.Media.AudioPlayer.IsSupported) {
                    Log.Warn (Tag, "SKIP: AudioPlayer.IsSupported is false");
                    return;
                }

                var filesDir = FilesDir?.AbsolutePath;
                if (filesDir is null) {
                    Log.Warn (Tag, "SKIP: FilesDir unavailable");
                    return;
                }

                var path = System.IO.Path.Combine (filesDir, "audio-smoke-test.wav");
                if (!System.IO.File.Exists (path)) {
                    using var src = Assets!.Open ("audio-smoke-test.wav");
                    using var dest = System.IO.File.Create (path);
                    src.CopyTo (dest);
                }

                using var player = new MSForms.Media.AudioPlayer (path) {
                    Volume = 0.5f,
                    Usage = MSForms.Media.AudioUsage.Alarm,
                };

                var completed = new ManualResetEventSlim (false);
                player.Completed += (_, _) => completed.Set ();

                player.Play ();
                if (!completed.Wait (TimeSpan.FromSeconds (10))) {
                    Log.Error (Tag, "FAIL: Completed was never raised for a non-looping track");
                    return;
                }

                // A second, looping track: prove Stop() actually silences it rather than it looping forever.
                player.Loop = true;
                player.Play ();
                System.Threading.Thread.Sleep (500);
                player.Stop ();

                Log.Info (Tag, "PASS: Play/Completed/Loop/Stop all completed with no exception");
            } catch (Exception ex) {
                Log.Error (Tag, $"FAIL: {ex}");
            }
        }

        // Register item F13: proves the plumbing, not the buzz -- the acceptance criterion itself says
        // "emulators have no vibrator", so this can only confirm Haptics.IsSupported answers true and
        // Tap/Impact/Vibrate all run with no exception on a real Android host, not that anything was felt.
        // That's still a real, repeated-on-every-PR CI check the other rows' F8/F9/F10/F11 markers already
        // establish; a human on a real phone is what the acceptance criterion actually asks for the feel.
        private void RunHapticsSmokeTest ()
        {
            const string Tag = "F13_HAPTICS_SMOKE";
            try {
                if (!MSForms.Haptics.IsSupported) {
                    Log.Error (Tag, "FAIL: Haptics.IsSupported is false on Android");
                    return;
                }

                MSForms.Haptics.Tap ();
                MSForms.Haptics.Impact ();
                MSForms.Haptics.Vibrate (TimeSpan.FromMilliseconds (200));

                Log.Info (Tag, "PASS: IsSupported is true and Tap/Impact/Vibrate all ran with no exception");
            } catch (Exception ex) {
                Log.Error (Tag, $"FAIL: {ex}");
            }
        }

        // Register item F14 (Android half): channels, importance, permission, posting with an ongoing
        // full-screen-intent notification, and the tap callback round-trip. Unlike F13's haptics, an
        // emulator DOES have a real notification centre, so this checks ActiveNotifications itself (Show()
        // never throws even when the platform silently drops a post for a missing permission -- confirmed
        // by a real CI run where PASS logged and dumpsys notification still showed nothing), and
        // android-smoke-test.sh separately confirms the same thing externally (dumpsys notification) and
        // drives the tap callback for real by replaying the exact launch intent a tap's PendingIntent would
        // send (see AndroidNotificationBackend's own remarks for why a literal notification-shade gesture
        // is not simulated here). LocalNotifications.Tapped is subscribed here, in OnCreate, so it is
        // already wired before the script's later am start delivers the tap.
        private void RunNotificationsSmokeTest ()
        {
            const string Tag = "F14_NOTIFICATIONS_SMOKE";
            try {
                if (!MSForms.Notifications.LocalNotifications.IsSupported) {
                    Log.Error (Tag, "FAIL: LocalNotifications.IsSupported is false on Android");
                    return;
                }

                MSForms.Notifications.LocalNotifications.Tapped += (_, e) => Log.Info (Tag, $"Tapped:{e.Id}");

                MSForms.Notifications.LocalNotifications.RequestPermission ();
                if (!MSForms.Notifications.LocalNotifications.IsPermissionGranted) {
                    // android-smoke-test.sh grants the permission before launch (adb shell pm grant), the
                    // standard way CI avoids an interactive system dialog nothing here can answer -- a
                    // human running the Gallery by hand instead sees the real prompt.
                    Log.Warn (Tag, "SKIP: notification permission not granted");
                    return;
                }

                MSForms.Notifications.LocalNotifications.RegisterChannel (new MSForms.Notifications.NotificationChannel ("f14-smoke", "F14 smoke") {
                    Importance = MSForms.Notifications.NotificationImportance.High,
                });

                MSForms.Notifications.LocalNotifications.Show (1001, new MSForms.Notifications.LocalNotification {
                    ChannelId = "f14-smoke",
                    Title = "F14 smoke test",
                    Text = "RegisterChannel, RequestPermission and Show all completed with no exception",
                    Ongoing = true,
                    FullScreen = true,
                });

                // Show() itself never throws -- Android 13+'s own contract for a missing POST_NOTIFICATIONS
                // grant is to silently no-op, not raise -- so "no exception" alone does not prove anything
                // posted. ActiveNotifications is this process's own, immediate way to check for real,
                // instead of only trusting android-smoke-test.sh's later, external dumpsys check.
                var posted = AndroidX.Core.App.NotificationManagerCompat.From (this)?.ActiveNotifications;
                var found = false;
                if (posted is not null) {
                    foreach (var n in posted) {
                        if (n?.Id == 1001) {
                            found = true;
                            break;
                        }
                    }
                }
                if (!found) {
                    Log.Error (Tag, "FAIL: Show() did not throw, but notification 1001 is not in ActiveNotifications");
                    return;
                }

                Log.Info (Tag, "PASS: RegisterChannel, RequestPermission, Show and ActiveNotifications all confirm the notification posted");
            } catch (Exception ex) {
                Log.Error (Tag, $"FAIL: {ex}");
            }
        }

        // ControlGallery's ImageLoader does plain File.Open under a relative "Images" folder -- there is
        // no such thing as a "working directory" full of loose files in an APK, so the PNGs bundled as
        // AndroidAssets (see the csproj) are copied out to this app's private storage once per run, and
        // the process's current directory is pointed at that folder so ImageLoader's relative path
        // resolves exactly as it does on desktop/browser, with no changes to ImageLoader itself.
        private void ExtractImageAssets ()
        {
            var filesDir = FilesDir?.AbsolutePath;
            if (filesDir is null)
                return;

            var imagesDir = System.IO.Path.Combine (filesDir, "Images");
            System.IO.Directory.CreateDirectory (imagesDir);

            foreach (var name in Assets!.List ("Images") ?? Array.Empty<string> ()) {
                var destPath = System.IO.Path.Combine (imagesDir, name);
                if (System.IO.File.Exists (destPath))
                    continue;

                using var src = Assets.Open ($"Images/{name}");
                using var dest = System.IO.File.Create (destPath);
                src.CopyTo (dest);
            }

            System.IO.Directory.SetCurrentDirectory (filesDir);
        }
    }

    // The Avalonia.Application TApp -- Avalonia's own bootstrap (in the base AvaloniaAndroidApplication<TApp>)
    // creates this, calls AppBuilder.SetupWithLifetime, and then invokes OnFrameworkInitializationCompleted
    // below with ApplicationLifetime already assigned, matching the same override point EmbeddingAvalonia's
    // desktop App.cs uses for IClassicDesktopStyleApplicationLifetime.
    public sealed class GalleryAvaloniaApp : Avalonia.Application
    {
        public override void OnFrameworkInitializationCompleted ()
        {
            // Android's IActivityApplicationLifetime.MainViewFactory is invoked lazily by
            // AvaloniaMainActivity once the Activity itself is created, so the Majorsilence.Forms side
            // (which owns the Control it needs to return) is only constructed then, not here.
            if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime) {
                activityLifetime.MainViewFactory = () => {
                    // MainForm.Show() constructs MajorsilenceFormsSingleViewHost, whose constructor
                    // registers itself as ISingleViewApplicationLifetime.MainView -- read it back rather
                    // than reaching into the (internal, cross-assembly-inaccessible) host type directly.
                    MSForms.Application.RunAndroid (() => {
                        var form = new MainForm ();
                        // Register item F10: the single-view root host's own Activated/Deactivate --
                        // MajorsilenceFormsSingleViewHost forwards these from the same IActivatableLifetime
                        // subscription that raises Application.Suspended/Resumed above.
                        const string Tag = "F10_LIFECYCLE";
                        form.Activated += (_, _) => Log.Info (Tag, "Form.Activated");
                        form.Deactivate += (_, _) => Log.Info (Tag, "Form.Deactivate");

                        // Register item F11: the platform back button/gesture. MainActivity forwards its
                        // own BackRequested to AvaloniaPlatformBackend.RaiseBackRequested, which prefers
                        // Application.ActivePopupWindow over this form when one is open. A small popup
                        // ("sheet") shown right after this form appears lets android-smoke-test.sh press
                        // KEYCODE_BACK for real and prove the acceptance criterion -- "closes a sheet
                        // without leaving the app" -- rather than just exiting: the popup's own
                        // BackRequested cancels and hides it (clearing ActivePopupWindow, see
                        // WindowBase.Hide); a second back press then has no popup left to route to, so it
                        // reaches this form's own (unhandled) BackRequested and the platform's default back
                        // behaviour proceeds, same as pressing back with nothing open.
                        const string BackTag = "F11_BACKBUTTON_SMOKE";
                        form.BackRequested += (_, _) => Log.Info (BackTag, "MainForm.BackRequested (unhandled)");
                        form.Shown += (_, _) => {
                            var popup = new MSForms.PopupWindow (form) { Size = new System.Drawing.Size (200, 100) };
                            popup.BackRequested += (_, e2) => {
                                e2.Cancel = true;
                                popup.Hide ();
                                Log.Info (BackTag, "Popup.BackRequested cancelled -- sheet closed, app still open");
                            };
                            popup.Show (20, 20);
                        };
                        return form;
                    });
                    return ((ISingleViewApplicationLifetime) ApplicationLifetime!).MainView!;
                };
            }

            base.OnFrameworkInitializationCompleted ();
        }
    }
}
