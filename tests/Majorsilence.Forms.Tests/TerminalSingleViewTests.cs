using System;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Terminal;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The terminal is the window, so a Form on the terminal backend is a single-view host like a phone: no
    // title bar of its own, filling the whole screen. The core decides that from IWindowBackend.IsSingleView,
    // so these tests make the terminal backend the platform for the length of one test and compare with the
    // Headless backend (a real window with a caption) as the control.
    [Collection ("Headless")]
    public class TerminalSingleViewTests : IDisposable
    {
        public TerminalSingleViewTests () => HeadlessRenderer.Use ();

        // Platform.Backend is process-wide: put Headless back so no other test sees the terminal backend.
        public void Dispose ()
        {
            HeadlessRenderer.Use ();
            GC.SuppressFinalize (this);
        }

        private static TerminalPlatformBackend Terminal ()
            => new (new TerminalOptions { GraphicsMode = TerminalGraphicsMode.HalfBlock }, _ => null);

        [Fact]
        public void EveryTerminalWindowIsASingleView ()
        {
            using var backend = Terminal ();

            Assert.True (backend.CreateWindow (null!, isPopup: false).IsSingleView);
            Assert.True (backend.CreateWindow (null!, isPopup: true).IsSingleView);
        }

        [Fact]
        public void AFormOnAWindowedBackendHasATitleBar ()
        {
            // The control: without this the test below could pass because nothing ever shows a title bar.
            using var form = new Form ();
            form.Show ();

            Assert.True (form.TitleBar.Visible);
        }

        [Fact]
        public void AFormOnTheTerminalBackendHasNoTitleBar ()
        {
            using var backend = Terminal ();
            Platform.Backend = backend;

            using var form = new Form ();
            form.Show ();

            Assert.False (form.TitleBar.Visible);
        }

        [Fact]
        public void TheTitleBarStaysHiddenAfterTheFormIsReconfigured ()
        {
            // The chrome is recomputed when decorations or borders change; it must not bring the bar back.
            using var backend = Terminal ();
            Platform.Backend = backend;

            using var form = new Form { FormBorderStyle = FormBorderStyle.Sizable };
            form.Show ();
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.FormBorderStyle = FormBorderStyle.Sizable;

            Assert.False (form.TitleBar.Visible);
        }
    }
}
