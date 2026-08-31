using Android.App;
using Android.Content.PM;
using Avalonia.Android;

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
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
    }
}
