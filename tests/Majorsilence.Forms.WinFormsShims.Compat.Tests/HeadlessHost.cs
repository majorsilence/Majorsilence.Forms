using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;

namespace Majorsilence.Forms.WinFormsShims.Compat.Tests
{
    /// <summary>The application the tests run inside: headless Avalonia, so no display or native platform is needed.</summary>
    public sealed class HeadlessCompatApp : Application
    {
        /// <summary>The builder <see cref="HeadlessUnitTestSession"/> uses to start the application.</summary>
        public static AppBuilder BuildAvaloniaApp ()
            => AppBuilder.Configure<HeadlessCompatApp> ().UseHeadless (new AvaloniaHeadlessPlatformOptions ());
    }

    // One headless session for the assembly; Avalonia's Application is process-wide, so a second one would throw.
    internal static class HeadlessHost
    {
        private static readonly Lazy<HeadlessUnitTestSession> session = new (() => HeadlessUnitTestSession.StartNew (typeof (HeadlessCompatApp)));

        public static Task Run (Action action) => session.Value.Dispatch (action, CancellationToken.None);
    }
}
