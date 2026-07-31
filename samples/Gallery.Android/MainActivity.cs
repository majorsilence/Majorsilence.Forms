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
    [Activity (
        Label = "Gallery.Android",
        Theme = "@android:style/Theme.NoTitleBar",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
    }
}
