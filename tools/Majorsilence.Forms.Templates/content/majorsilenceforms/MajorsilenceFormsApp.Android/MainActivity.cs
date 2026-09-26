using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace MajorsilenceFormsApp.Android
{
    // All the wiring lives in App.cs -- by the time this Activity is created, AvaloniaMainActivity's
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
    }
}
