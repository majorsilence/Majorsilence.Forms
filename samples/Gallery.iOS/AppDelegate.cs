using Avalonia;
using Avalonia.iOS;
using Avalonia.Controls.ApplicationLifetimes;
using ControlGallery;
using Foundation;
using UIKit;

using MSForms = Majorsilence.Forms;

namespace Gallery.iOS
{
    // The iOS UIApplicationDelegate that owns Avalonia's bootstrap: Avalonia.iOS's own
    // AvaloniaAppDelegate<TApp>.FinishedLaunching runs AppBuilder.Configure<TApp>().UseiOS(this)
    // ....SetupWithLifetime(...) for us -- this override only needs to point the gallery's image loading
    // at the app bundle's Resources before that happens.
    [Register ("AppDelegate")]
    public class GalleryAppDelegate : AvaloniaAppDelegate<GalleryAvaloniaApp>
    {
        public override bool FinishedLaunching (UIApplication application, NSDictionary? launchOptions)
        {
            PointImageLoaderAtBundleResources ();
            return base.FinishedLaunching (application, launchOptions);
        }

        // ImageLoader.cs (backend-agnostic, plain File.Open under a relative "Images" folder) has no
        // concept of an app bundle -- unlike Android's APK assets (which need extracting to app-private
        // storage at runtime, see Gallery.Android's App.cs), the BundleResource images referenced in
        // Gallery.iOS.csproj are already directly on the filesystem inside the app bundle's Resources
        // folder once installed, so pointing the process's current directory there is all that's needed,
        // with no copy/extract step.
        private static void PointImageLoaderAtBundleResources ()
        {
            var resourcePath = NSBundle.MainBundle.ResourcePath;
            if (resourcePath != null)
                System.IO.Directory.SetCurrentDirectory (resourcePath);
        }
    }

    // The Avalonia.Application TApp -- Avalonia's own bootstrap (in the base AvaloniaAppDelegate<TApp>)
    // creates this, calls AppBuilder.SetupWithLifetime, and then invokes OnFrameworkInitializationCompleted
    // below with ApplicationLifetime already assigned, matching the same override point Gallery.Android's
    // App.cs uses for IActivityApplicationLifetime.
    public sealed class GalleryAvaloniaApp : Avalonia.Application
    {
        public override void OnFrameworkInitializationCompleted ()
        {
            // Unlike Android's IActivityApplicationLifetime (whose MainViewFactory is invoked lazily,
            // only once the Activity itself is created), iOS's SingleViewLifetime is already assigned
            // here and its MainView can be set immediately: AvaloniaAppDelegate<TApp>.FinishedLaunching
            // only creates the actual UIWindow/view hierarchy in an AfterApplicationSetup callback that
            // runs after SetupWithLifetime (and so after this method) returns, so MainView is already
            // set by the time that callback reads it.
            //
            // MainForm.Show() constructs MajorsilenceFormsSingleViewHost, whose constructor registers
            // itself as ISingleViewApplicationLifetime.MainView as a side effect -- there is nothing
            // further to read/return here, unlike Android's MainViewFactory.
            if (ApplicationLifetime is ISingleViewApplicationLifetime)
                MSForms.Application.RunIOS (() => new MainForm ());

            base.OnFrameworkInitializationCompleted ();
        }
    }
}
