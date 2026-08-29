using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace MajorsilenceFormsApp.Android
{
    // All the wiring lives in App.cs -- by the time this Activity is created, AvaloniaMainActivity's
    // base OnCreate already asks the Application for its MainViewFactory and uses the control it
    // returns as this Activity's content.
    [Activity (
        Label = "MajorsilenceFormsApp",
        Theme = "@android:style/Theme.NoTitleBar",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
    }
}
