using System;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    // Inside the namespace, so Avalonia types win over the Majorsilence.Forms ones of the same name.
    using global::Avalonia;
    using global::Avalonia.Headless;
    using global::Avalonia.Markup.Xaml.Styling;
    using global::Avalonia.Themes.Fluent;

    // The application the tests run in: Fluent plus the DataGrid's Fluent theme, the combination the
    // support matrix targets.
    public sealed class HeadlessApp : Application
    {
        public override void Initialize ()
        {
            Styles.Add (new FluentTheme ());
            Styles.Add (new StyleInclude (new Uri ("avares://Majorsilence.Forms.Theming.Avalonia.Tests/")) {
                Source = new Uri ("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
            });
        }

        public static AppBuilder BuildAvaloniaApp ()
            => AppBuilder.Configure<HeadlessApp> ().UseHeadless (new AvaloniaHeadlessPlatformOptions ());
    }

    // One headless session for the whole assembly; every test body runs on its UI thread. Avalonia's
    // Application is process-wide, as is Majorsilence.Forms' Theme, so the tests are not parallelised
    // (see xunit.runner.json) and each resets what it applied.
    internal static class Headless
    {
        private static readonly Lazy<HeadlessUnitTestSession> session = new (() => HeadlessUnitTestSession.StartNew (typeof (HeadlessApp)));

        public static Task Run (Action action) => session.Value.Dispatch (action, CancellationToken.None);
    }
}
