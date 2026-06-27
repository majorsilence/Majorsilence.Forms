using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Tests for the window chrome / title bar defaults. Windows/Linux use fully custom chrome (our
    // FormTitleBar with caption buttons). macOS uses the NATIVE title bar (traffic lights, rounded
    // corners, shadow); a plain form leaves the OS in charge of the whole title bar, while a form that
    // wants to paint into it opts in with ExtendsContentIntoTitleBar (Avalonia 12 full-size content
    // view) — the FormTitleBar then runs in "native overlay" mode merged into the native bar.
    public class FormChromeTests
    {
        [Fact]
        public void Form_ChromeDefaults_MatchPlatform ()
        {
            using var form = new Form ();

            if (OperatingSystem.IsMacOS ()) {
                // Plain macOS form: native title bar, no extension (clean client area).
                Assert.True (form.UseSystemDecorations);
                Assert.False (form.ExtendsContentIntoTitleBar);
                Assert.False (form.TitleBar.Visible);
            } else {
                // Windows/Linux: fully custom chrome with a visible title bar.
                Assert.False (form.UseSystemDecorations);
                Assert.False (form.ExtendsContentIntoTitleBar);
                Assert.True (form.TitleBar.Visible);
            }
        }

        [Fact]
        public void FullyCustomChrome_ShowsOwnButtons ()
        {
            using var form = new Form { UseSystemDecorations = false };

            Assert.False (form.UseSystemDecorations);
            Assert.True (form.TitleBar.Visible);
            Assert.False (form.TitleBar.NativeOverlay);
            // Our own caption buttons contribute width on the right.
            Assert.True (form.TitleBar.CaptionButtonsWidth > 0);
        }

        [Fact]
        public void PlainNativeChrome_HidesCustomTitleBar ()
        {
            using var form = new Form { UseSystemDecorations = true };

            // Native chrome with no extension = the OS owns the whole title bar, ours is hidden.
            Assert.True (form.UseSystemDecorations);
            Assert.False (form.ExtendsContentIntoTitleBar);
            Assert.False (form.TitleBar.Visible);
        }

        [Fact]
        public void Merged_TitleBar_RunsInOverlayMode ()
        {
            using var form = new Form { UseSystemDecorations = true, ExtendsContentIntoTitleBar = true };

            // Merged into the native title bar: our strip is visible, in overlay mode (OS draws the
            // traffic lights, so we reserve the left inset and draw no caption buttons of our own).
            Assert.True (form.TitleBar.Visible);
            Assert.True (form.TitleBar.NativeOverlay);
            Assert.True (form.TitleBar.CaptionButtonsOnLeft);
            Assert.True (form.TitleBar.CaptionButtonsWidth > 0);
        }

        [Fact]
        public void ExtendsContentIntoTitleBar_RequiresSystemDecorations_ToShowOverlay ()
        {
            // Without native decorations there is no OS title bar to merge into, so the overlay stays off
            // even if extension is requested (the value is remembered but inert).
            using var form = new Form { UseSystemDecorations = false, ExtendsContentIntoTitleBar = true };

            Assert.True (form.ExtendsContentIntoTitleBar);
            Assert.False (form.TitleBar.NativeOverlay);
            Assert.True (form.TitleBar.Visible);
        }

        [Fact]
        public void ChromeProperties_ThrowAfterShown ()
        {
            using var form = new Form ();
            form.Show ();

            Assert.Throws<InvalidOperationException> (() => form.UseSystemDecorations = !form.UseSystemDecorations);
            Assert.Throws<InvalidOperationException> (() => form.ExtendsContentIntoTitleBar = !form.ExtendsContentIntoTitleBar);

            form.Close ();
        }
    }
}
