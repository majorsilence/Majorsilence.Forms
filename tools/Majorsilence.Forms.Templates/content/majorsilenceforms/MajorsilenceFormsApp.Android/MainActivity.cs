using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Majorsilence.Forms.Backends;

namespace MajorsilenceFormsApp.Android
{
    // Nearly all the wiring lives in App.cs -- by the time this Activity is created, AvaloniaMainActivity's
    // base OnCreate already asks the Application for its MainViewFactory and uses the control it
    // returns as this Activity's content.
    //
    // Theme MUST descend from Theme.AppCompat: AvaloniaMainActivity is an AppCompatActivity, and its
    // setContentView throws IllegalStateException under any other theme (an app with a plain
    // "@android:style/Theme.NoTitleBar" crashes on its first frame). See Resources/values/styles.xml.
    [Activity (
        Label = "MajorsilenceFormsApp",
        Theme = "@style/MajorsilenceFormsAppTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
        protected override void OnCreate (Bundle? savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // The framework cannot discover the current Activity, so the app forwards the Android back button and
            // gesture itself, in one line. It goes to the open sheet or dialog first, then the main form's
            // Form.BackRequested, where a handler can step back within the app (close a panel, pop a screen).
            // Handled = true keeps the app open; leaving it false lets Android do its normal back (leave the app).
            BackRequested += (_, e) => e.Handled = AvaloniaPlatformBackend.RaiseBackRequested ();
        }
    }
}
