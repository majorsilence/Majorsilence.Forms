using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises the backend seam on the Headless backend: offscreen rendering, neutral input
// injection, and the modal-dialog flow. Guards against regressions in IPlatformBackend /
// IWindowBackend and the WindowBase render/input/lifecycle plumbing.
public class HeadlessBackendTests
{
    [Fact]
    public void Backend_IsHeadless ()
    {
        // The test assembly's ModuleInitializer selects the Headless backend.
        Assert.Equal ("Headless", Majorsilence.Forms.Backends.Platform.Backend.Name);
    }

    [Fact]
    public void RendersFormToPng_AtRequestedSize ()
    {
        var form = new Form ();
        form.Controls.Add (new Button { Text = "Hello", Left = 10, Top = 10, Width = 100, Height = 30 });

        var png = HeadlessRenderer.CapturePng (form, 200, 120);

        Assert.NotNull (png);
        Assert.True (png.Length > 0);

        using var bmp = SKBitmap.Decode (png);
        Assert.Equal (200, bmp.Width);
        Assert.Equal (120, bmp.Height);
    }

    [Fact]
    public void RenderedContent_IsNotBlank ()
    {
        // A form with a button must produce more than a single flat colour.
        var form = new Form ();
        form.Controls.Add (new Button { Text = "Click me", Left = 20, Top = 20, Width = 140, Height = 40 });

        var png = HeadlessRenderer.CapturePng (form, 220, 120);

        using var bmp = SKBitmap.Decode (png);
        var first = bmp.GetPixel (0, 0);
        var distinct = false;
        for (var y = 0; y < bmp.Height && !distinct; y += 4)
            for (var x = 0; x < bmp.Width; x += 4)
                if (bmp.GetPixel (x, y) != first) { distinct = true; break; }

        Assert.True (distinct, "Rendered frame was a single flat colour — nothing was drawn.");
    }

    [Fact]
    public void Label_WithDesignerTypicalShortHeight_StillRendersText ()
    {
        // Regression: found via a real migrated WinForms designer app (ReportDesigner.Forms)
        // whose labels all rendered as nothing but blank space. 13px is the height the WinForms
        // designer emits for an AutoSize label at the default (8.25pt) font -- a couple pixels
        // short of that font's own line height. SkiaTextExtensions.DrawText used to pass the
        // control's own (short) height straight through as RichTextKit's TextBlock.MaxHeight, a
        // hard layout budget: since not even one line fit inside 13px, RichTextKit laid out zero
        // lines instead of the first line plus overflow (which is what real WinForms/GDI+ does,
        // relying on the paint clip to hide anything past the control's edge, not the text layout
        // engine to refuse drawing). Fixed by passing an unconstrained height into text layout and
        // letting canvas.Clip(bounds) do the only actual vertical clipping, as before.
        var form = new Form ();
        var label = new Label { Text = "Fore Color:", AutoSize = false, Left = 4, Top = 4, Width = 100, Height = 13 };
        form.Controls.Add (label);

        var png = HeadlessRenderer.CapturePng (form, 200, 60);

        using var bmp = SKBitmap.Decode (png);
        var background = bmp.GetPixel (150, 40);   // far from the label -- the form's own background
        var textPixelFound = false;
        for (var y = label.Top; y < label.Top + label.Height && !textPixelFound; y++)
            for (var x = label.Left; x < label.Left + label.Width; x++)
                if (bmp.GetPixel (x, y) != background) { textPixelFound = true; break; }

        Assert.True (textPixelFound, "Label text did not render within its own (short) bounds.");
    }

    [Fact]
    public void PopupWindow_Show_SuppressesParentDeactivationQueuedAfterShowReturns ()
    {
        // Regression: found via a real WinForms-migrated app (ReportDesigner.Forms) whose menus
        // opened and then immediately closed again -- every dropdown menu (a MenuDropDown, backed
        // by a PopupWindow) goes through PopupWindow.Show(int,int).
        //
        // Showing a popup steals activation from its parent, whose own deactivation handler would
        // otherwise dismiss the very popup just opened -- Show() sets
        // Application.SuppressPopupDismiss for the duration to prevent exactly that. The bug: on a
        // real backend (Avalonia), the parent's deactivation event is often not delivered
        // synchronously inside Show() -- it is queued and only dispatched on a later UI-thread
        // tick. Resetting the suppress flag synchronously, right after Show() returns, cleared it
        // before that queued deactivation had a chance to arrive, so it went through unsuppressed
        // and closed the popup.
        //
        // The Headless backend's own Post(...) (what BeginInvoke uses) enqueues rather than running
        // inline, so calling OnBackendDeactivated() here -- before anything drains that queue --
        // reproduces exactly this "deactivation arrives after Show() returns" ordering.
        using var parent = new Form ();
        parent.Show ();

        var popup = new PopupWindow (parent);
        popup.Show (10, 10);

        parent.OnBackendDeactivated ();

        Assert.True (popup.Visible, "the popup was dismissed by its own parent's deactivation, which Show() should have suppressed");
    }

    [Fact]
    public void InjectedClick_RaisesButtonClick ()
    {
        var form = new Form ();
        var clicks = 0;
        var button = new Button { Text = "Click", Left = 20, Top = 20, Width = 120, Height = 40 };
        button.Click += (_, _) => clicks++;
        form.Controls.Add (button);

        HeadlessRenderer.CapturePng (form, 300, 200);   // force a layout pass
        HeadlessRenderer.Click (form, 80, 40);           // centre of the button

        Assert.Equal (1, clicks);
    }

    [Fact]
    public void Clipboard_RoundTripsThroughBackend ()
    {
        Clipboard.SetText ("round-trip-value");
        Assert.Equal ("round-trip-value", Clipboard.GetText ());

        Clipboard.Clear ();
        Assert.Equal (string.Empty, Clipboard.GetText ());
    }

    [Fact]
    public void Screens_ComeFromBackend ()
    {
        var screens = Screen.AllScreens;

        Assert.NotEmpty (screens);
        Assert.NotNull (Screen.PrimaryScreen);
        Assert.Equal (1920, Screen.PrimaryScreen!.Bounds.Width);
        Assert.Equal (1080, Screen.PrimaryScreen!.Bounds.Height);
    }

    [Fact]
    public void TextInput_ReachesFocusedTextBox ()
    {
        // UseSystemDecorations gives a clean client area on every platform: without it the custom
        // FormTitleBar (drawn in-client on Windows/Linux) overlaps a control at the top and would
        // intercept the focusing click. macOS defers to native chrome so the title bar is already hidden.
        var form = new Form { UseSystemDecorations = true };
        var textbox = new TextBox { Left = 10, Top = 10, Width = 200, Height = 30 };
        form.Controls.Add (textbox);

        HeadlessRenderer.CapturePng (form, 240, 60);   // force a layout pass
        HeadlessRenderer.Click (form, 100, 25);         // click to focus the textbox
        HeadlessRenderer.TextInput (form, "Hello");

        Assert.Equal ("Hello", textbox.Text);
    }

    [Fact]
    public void MenuPopup_StaysOpenOnShow_AndDismissesOnRealDeactivation ()
    {
        // Regression: showing a popup deactivates its parent; that deactivation (while suppressed)
        // must NOT dismiss the popup we just opened — otherwise menus render as an empty box.
        var form = new Form ();
        var panel = new Panel { Left = 0, Top = 0, Width = 100, Height = 20 };
        form.Controls.Add (panel);
        form.Show ();

        var menu = new MenuDropDown ();
        menu.Items.Add ("Open");
        menu.Items.Add ("Save");
        menu.Show (panel, new System.Drawing.Point (0, 20));

        Assert.True (menu.Visible);   // popup is shown, not dismissed by its own show

        // A parent deactivation caused by the popup opening (suppressed) must not dismiss it.
        Application.SuppressPopupDismiss = true;
        form.OnBackendDeactivated ();
        Assert.True (menu.Visible);

        // A genuine deactivation (user clicks away) dismisses it.
        Application.SuppressPopupDismiss = false;
        form.OnBackendDeactivated ();
        Assert.False (menu.Visible);

        form.Close ();
    }

    [Fact]
    public void RightClick_OpensContextMenu ()
    {
        // Right-click must reach OnClick with MouseButtons.Right and open the control's context menu.
        // Clean client area (no in-client title bar) so the right-click lands on the panel, not the
        // FormTitleBar — see TextInput_ReachesFocusedTextBox.
        var form = new Form { UseSystemDecorations = true };
        var panel = new Panel { Left = 0, Top = 0, Width = 200, Height = 100 };
        var menu = new ContextMenu ();
        menu.Items.Add ("Copy");
        menu.Items.Add ("Paste");
        panel.ContextMenu = menu;
        form.Controls.Add (panel);
        form.Show ();

        HeadlessRenderer.CapturePng (form, 200, 100);              // force a layout pass
        HeadlessRenderer.Click (form, 50, 30, MouseButtons.Right); // right-click the panel

        Assert.True (menu.Visible);   // the context-menu popup opened

        Application.ClosePopups ();
        form.Close ();
    }

    [Theory]
    [InlineData ("File", "File", -1)]            // no prefix: unchanged, no mnemonic
    [InlineData ("&File", "File", 0)]            // leading prefix: 'F' is the mnemonic
    [InlineData ("E&xit", "Exit", 1)]            // mid-string prefix: 'x' is the mnemonic
    [InlineData ("Save && Close", "Save & Close", -1)]  // doubled '&' is a literal ampersand
    [InlineData ("R&&D", "R&D", -1)]             // doubled '&' between letters
    [InlineData ("Trailing&", "Trailing", -1)]   // trailing '&' is dropped
    [InlineData ("", "", -1)]                     // empty
    public void Mnemonics_Parse_StripsPrefixAndLocatesAccessKey (string input, string expectedDisplay, int expectedIndex)
    {
        var display = Mnemonics.Parse (input, out var index);

        Assert.Equal (expectedDisplay, display);
        Assert.Equal (expectedIndex, index);
    }

    [Fact]
    public async System.Threading.Tasks.Task ShowDialog_CompletesWithoutRecursion ()
    {
        // Regression: Form.ShowDialog(Form) must call the base window helper, not recurse into itself.
        var parent = new Form ();
        parent.Show ();

        var dialog = new Form ();
        var task = dialog.ShowDialogAsync (parent);
        Assert.False (task.IsCompleted);

        dialog.DialogResult = DialogResult.OK;   // triggers Close → completes the dialog task

        Assert.True (task.IsCompleted);
        Assert.Equal (DialogResult.OK, await task);

        parent.Close ();
        Assert.Equal (0, Application.OpenForms.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task NestedShowDialog_ActiveModalForm_TracksInnermostDialog ()
    {
        // Regression: found via a real migrated app (ReportDesigner.Forms) -- a MessageBox raised
        // from code running inside an already-modal dialog (a "New Report from Database" wizard)
        // parented itself to Application.OpenForms.FirstOrDefault(), which is always the very first
        // window the app ever opened (the main designer window sitting behind the wizard), not the
        // wizard itself. The box rendered relative to the wrong (backgrounded, input-blocked)
        // window -- indistinguishable from the whole app silently hanging. Form.ShowDialog(),
        // FileDialog.ShowDialog(), and both no-explicit-owner MessageBox.Show overloads all now
        // resolve their parent through Application.ActiveModalForm first; this pins the underlying
        // push/pop mechanism (Form.ShowDialogAsync / Form.Close) all four share.
        var main = new Form ();
        main.Show ();
        Assert.Null (Application.ActiveModalForm);   // nothing modal yet

        var wizard = new Form ();
        var wizardTask = wizard.ShowDialogAsync (main);
        Assert.False (wizardTask.IsCompleted);
        Assert.Same (wizard, Application.ActiveModalForm);   // not main, despite main opening first

        var errorBox = new Form ();
        var errorTask = errorBox.ShowDialogAsync (Application.ActiveModalForm!);
        Assert.False (errorTask.IsCompleted);
        Assert.Same (errorBox, Application.ActiveModalForm);   // now nested one level deeper

        errorBox.DialogResult = DialogResult.OK;   // close the innermost dialog first
        Assert.True (errorTask.IsCompleted);
        Assert.Same (wizard, Application.ActiveModalForm);   // reverts to the wizard, not main

        wizard.DialogResult = DialogResult.OK;
        Assert.True (wizardTask.IsCompleted);
        Assert.Null (Application.ActiveModalForm);   // all modals closed, stack empty

        main.Close ();
    }
}
