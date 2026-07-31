using System;
using Android.App;
using Android.Runtime;
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
                    MSForms.Application.RunAndroid (() => new MainForm ());
                    return ((ISingleViewApplicationLifetime) ApplicationLifetime!).MainView!;
                };
            }

            base.OnFrameworkInitializationCompleted ();
        }
    }
}
