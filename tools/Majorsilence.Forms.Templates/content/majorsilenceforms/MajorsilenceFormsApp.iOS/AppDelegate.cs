using Avalonia;
using Avalonia.iOS;
using Avalonia.Controls.ApplicationLifetimes;
using Foundation;

using MSForms = Majorsilence.Forms;

namespace MajorsilenceFormsApp.iOS
{
    [Register ("AppDelegate")]
    public class AppDelegate : AvaloniaAppDelegate<AvaloniaApp>
    {
    }

    public sealed class AvaloniaApp : Avalonia.Application
    {
        public override void OnFrameworkInitializationCompleted ()
        {
            // MainForm.Show() constructs the single-view host, which registers itself as
            // ISingleViewApplicationLifetime.MainView as a side effect.
            if (ApplicationLifetime is ISingleViewApplicationLifetime)
                MSForms.Application.RunIOS (() => new MainForm ());

            base.OnFrameworkInitializationCompleted ();
        }
    }
}
