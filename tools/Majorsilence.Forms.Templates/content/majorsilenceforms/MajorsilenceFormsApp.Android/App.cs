using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;

using MSForms = Majorsilence.Forms;

namespace MajorsilenceFormsApp.Android
{
    // The Android Application that owns Avalonia's bootstrap. Avalonia's Android integration runs
    // AppBuilder.Configure<TApp>().UseAndroid()...SetupWithLifetime(...) from the base class before
    // MainActivity exists.
    [Application]
    public class MainApplication : AvaloniaAndroidApplication<AvaloniaApp>
    {
        public MainApplication (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
        {
        }
    }

    public sealed class AvaloniaApp : Avalonia.Application
    {
        public override void OnFrameworkInitializationCompleted ()
        {
            if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime) {
                activityLifetime.MainViewFactory = () => {
                    // MainForm.Show() constructs the single-view host, which registers itself as
                    // ISingleViewApplicationLifetime.MainView -- read it back rather than reaching into
                    // the internal host type.
                    MSForms.Application.RunAndroid (() => new MainForm ());
                    return ((ISingleViewApplicationLifetime) ApplicationLifetime!).MainView!;
                };
            }

            base.OnFrameworkInitializationCompleted ();
        }
    }
}
