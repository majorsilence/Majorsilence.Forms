using Modern.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Modern.Forms.Tests;

// Exercises the backend seam on the Headless backend: offscreen rendering, neutral input
// injection, and the modal-dialog flow. Guards against regressions in IPlatformBackend /
// IWindowBackend and the WindowBase render/input/lifecycle plumbing.
public class HeadlessBackendTests
{
    [Fact]
    public void Backend_IsHeadless ()
    {
        // The test assembly's ModuleInitializer selects the Headless backend.
        Assert.Equal ("Headless", Modern.Forms.Backends.Platform.Backend.Name);
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
        var form = new Form ();
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
        var form = new Form ();
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

    [Fact]
    public void ShowDialog_CompletesWithoutRecursion ()
    {
        // Regression: Form.ShowDialog(Form) must call the base window helper, not recurse into itself.
        var parent = new Form ();
        parent.Show ();

        var dialog = new Form ();
        var task = dialog.ShowDialog (parent);
        Assert.False (task.IsCompleted);

        dialog.DialogResult = DialogResult.OK;   // triggers Close → completes the dialog task

        Assert.True (task.IsCompleted);
        Assert.Equal (DialogResult.OK, task.Result);

        parent.Close ();
        Assert.Equal (0, Application.OpenForms.Count);
    }
}
