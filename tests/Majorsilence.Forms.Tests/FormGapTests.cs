using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Form / Application behaviour-gap findings (docs/behaviour-gap/form.md, issue #345). Each test names
// the finding it pins; the upstream source each behaviour was taken from is cited in the fix.
[Collection ("Headless")]
public sealed class FormGapTests : IDisposable
{
    private readonly Point original_mouse = Cursor.Position;

    public FormGapTests () => HeadlessRenderer.Use ();

    // The cursor position and the headless display list are process-wide.
    public void Dispose ()
    {
        Cursor.Position = original_mouse;
        Headless.Screens = null;
    }

    private static HeadlessPlatformBackend Headless => (HeadlessPlatformBackend) Platform.Backend;

    private static HeadlessWindowHost Host (WindowBase window) => (HeadlessWindowHost) window.Backend;

    // A second monitor to the right of the headless default one, with a taskbar along its bottom.
    private static readonly ScreenInfo primary_screen = new ("Primary", new Rectangle (0, 0, 1920, 1080), new Rectangle (0, 0, 1920, 1040), isPrimary: true);
    private static readonly ScreenInfo second_screen = new ("Second", new Rectangle (1920, 0, 1280, 1024), new Rectangle (1920, 0, 1280, 984), isPrimary: false);

    private static void UseTwoScreens () => Headless.Screens = new[] { primary_screen, second_screen };

    // ── FRM-11: ActiveForm named the newest OPENED form, not the active one ────────────────────────

    [Fact]
    public void FRM11_ActiveForm_follows_activation_back_to_an_older_form ()
    {
        using var main = new Form ();
        using var tool = new Form ();
        main.Show ();
        tool.Show ();
        Assert.Same (tool, Form.ActiveForm);

        // The user clicks back to the main window.
        Host (main).Activate ();

        Assert.Same (main, Form.ActiveForm);
    }

    [Fact]
    public void FRM11_ActiveForm_is_null_once_the_active_form_deactivates_or_closes ()
    {
        using var main = new Form ();
        main.Show ();
        Assert.Same (main, Form.ActiveForm);

        // Another application took the foreground.
        main.OnBackendDeactivated ();
        Assert.Null (Form.ActiveForm);

        Host (main).Activate ();
        Assert.Same (main, Form.ActiveForm);

        main.Close ();
        Assert.Null (Form.ActiveForm);
    }

    // ── FRM-18: MaximizeBox decided whether the window could be resized ────────────────────────────

    [Fact]
    public void FRM18_MaximizeBox_off_leaves_a_sizable_form_resizable ()
    {
        using var form = new Form { FormBorderStyle = FormBorderStyle.Sizable, MinimizeBox = true };

        form.MaximizeBox = false;

        Assert.True (form.Backend.CanResize);
        Assert.False (form.TitleBar.MaximizeButtonControl.Enabled);
    }

    [Fact]
    public void FRM18_A_fixed_border_style_makes_the_window_not_resizable ()
    {
        using var form = new Form ();

        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        Assert.False (form.Backend.CanResize);

        // MaximizeBox has no say in it either way.
        form.MaximizeBox = true;
        Assert.False (form.Backend.CanResize);

        form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
        Assert.True (form.Backend.CanResize);
    }

    // ── FRM-20: CenterToScreen / CenterToParent did not move the window ────────────────────────────

    // The window's size on the desktop: Size is logical, the screen is in desktop pixels.
    private static Size DesktopSize (Form form) => form.ScreenBounds.Size;

    [Fact]
    public void FRM20_CenterToScreen_moves_a_shown_Manual_form_to_the_working_area_centre ()
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point (5, 7), Width = 400, Height = 300 };
        form.Show ();

        form.CenterToScreen ();

        var size = DesktopSize (form);
        var area = Screen.PrimaryScreen!.WorkingArea;
        Assert.Equal (new Point (area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2), form.Location);
        Assert.Equal (FormStartPosition.Manual, form.StartPosition);
    }

    [Fact]
    public void FRM20_CenterToScreen_uses_the_screen_under_the_mouse ()
    {
        UseTwoScreens ();
        Cursor.Position = new Point (2500, 500);
        using var form = new Form { StartPosition = FormStartPosition.Manual, Width = 400, Height = 300 };

        form.CenterToScreen ();

        var size = DesktopSize (form);
        var area = second_screen.WorkingArea;
        Assert.Equal (new Point (area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2), form.Location);
    }

    [Fact]
    public void FRM20_CenterParent_with_no_owner_centres_on_screen_at_show ()
    {
        using var form = new Form { StartPosition = FormStartPosition.CenterParent, Location = new Point (5, 7), Width = 400, Height = 300 };

        form.Show ();

        var size = DesktopSize (form);
        var area = Screen.PrimaryScreen!.WorkingArea;
        Assert.Equal (new Point (area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2), form.Location);
    }

    [Fact]
    public void FRM20_CenterToParent_centres_over_the_owner_and_stays_on_its_screen ()
    {
        using var owner = new Form { StartPosition = FormStartPosition.Manual, Location = new Point (100, 100), Width = 600, Height = 500 };
        owner.Show ();
        using var tool = new Form { StartPosition = FormStartPosition.Manual, Width = 200, Height = 100, Owner = owner };
        tool.Show ();

        tool.CenterToParent ();

        var parent = owner.ScreenBounds;
        var size = DesktopSize (tool);
        Assert.Equal (new Point ((parent.Left + parent.Right - size.Width) / 2, (parent.Top + parent.Bottom - size.Height) / 2), tool.Location);

        // An owner hard against the screen's corner: the centred window would hang off it, so it is
        // pulled back inside the working area.
        owner.Location = new Point (-500, -450);
        tool.CenterToParent ();

        Assert.Equal (new Point (0, 0), tool.Location);
    }

    // ── FRM-33: Screen.FromControl answered the primary screen whatever the control ────────────────

    [Fact]
    public void FRM33_FromControl_finds_the_screen_a_window_is_on ()
    {
        UseTwoScreens ();
        var button = new Button { Bounds = new Rectangle (10, 10, 80, 30) };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point (2000, 100), Width = 400, Height = 300 };
        form.Controls.Add (button);
        form.Show ();

        Assert.Equal ("Second", Screen.FromControl (form)!.DeviceName);
        Assert.Equal ("Second", Screen.FromControl (button)!.DeviceName);

        form.Location = new Point (100, 100);
        Assert.Equal ("Primary", Screen.FromControl (form)!.DeviceName);
        Assert.Equal ("Primary", Screen.FromControl (button)!.DeviceName);
    }

    [Fact]
    public void FRM33_FromRectangle_picks_the_screen_with_the_larger_share ()
    {
        UseTwoScreens ();

        // Its centre is a pixel onto the second screen, but the second is shorter, so more of the
        // rectangle is on the primary one.
        Assert.Equal ("Primary", Screen.FromRectangle (new Rectangle (1821, 0, 200, 1080))!.DeviceName);
        Assert.Equal ("Second", Screen.FromRectangle (new Rectangle (1821, 0, 260, 1000))!.DeviceName);
    }

    // ── FRM-27: a Control owner was not resolved to its own form ────────────────────────────────────

    [Fact]
    public void FRM27_MessageBox_owned_by_a_control_is_owned_by_that_controls_form ()
    {
        using var main = new Form ();
        main.Show ();
        var panel = new Panel ();
        using var tool = new Form ();
        tool.Controls.Add (panel);
        tool.Show ();

        IWindowBackend? owner = null;

        Platform.Backend.Post (() => {
            var box = Application.OpenForms.Cast<Form> ().LastOrDefault (f => f is MessageBoxForm);
            owner = box is null ? null : Host (box).LastDialogOwner;
            box?.Close ();
        });

        MessageBox.Show (panel, "text", "caption", MessageBoxButtons.OK, MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign);

        Assert.Same (tool.Backend, owner);
    }

    // ── FRM-29: Dispose raised FormClosed and Closed for a close that never happened ───────────────

    [Fact]
    public void FRM29_Disposing_a_shown_form_raises_neither_FormClosed_nor_Closed ()
    {
        var form = new Form ();
        form.Show ();
        var events = "";
        form.FormClosed += (_, _) => events += "FormClosed ";
        form.Closed += (_, _) => events += "Closed ";
        form.HandleDestroyed += (_, _) => events += "HandleDestroyed ";

        form.Dispose ();

        // The handle still goes, as upstream's does.
        Assert.Equal ("HandleDestroyed ", events);
        Assert.DoesNotContain (form, Application.OpenForms.Cast<Form> ());
    }

    [Fact]
    public void FRM29_Disposing_a_closed_dialog_does_not_raise_FormClosed_a_second_time ()
    {
        using var owner = new Form ();
        owner.Show ();
        var dialog = new Form ();
        var closed = 0;
        dialog.FormClosed += (_, _) => closed++;

        Platform.Backend.Post (() => dialog.Close ());
        dialog.ShowDialog (owner);
        Assert.Equal (1, closed);

        dialog.Dispose ();

        Assert.Equal (1, closed);
    }

    [Fact]
    public void FRM29_Disposing_the_main_form_still_ends_its_ApplicationContext ()
    {
        var form = new Form ();
        form.Show ();
        using var context = new ApplicationContext (form);
        var exited = false;
        context.ThreadExit += (_, _) => exited = true;

        form.Dispose ();

        Assert.True (exited);
    }

    // ── FRM-32: a form's default size was 1080x720 ──────────────────────────────────────────────────

    [Fact]
    public void FRM32_A_form_with_no_size_set_is_upstreams_300_by_300 ()
    {
        using var form = new Form ();

        Assert.Equal (new Size (300, 300), form.Size);
    }

    // ── FRM-34: BackgroundWorker cleared IsBusy early and its Result never threw ───────────────────

    // Pumps the UI queue until the condition holds, so a regression fails instead of hanging.
    private static void PumpUntil (Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds (10);

        while (!condition () && DateTime.UtcNow < deadline) {
            Application.DoEvents ();
            System.Threading.Thread.Sleep (5);
        }

        Assert.True (condition (), "timed out");
    }

    [Fact]
    public void FRM34_IsBusy_stays_set_until_the_completion_runs_on_the_UI_thread ()
    {
        using var worker = new BackgroundWorker ();
        using var finished = new System.Threading.ManualResetEventSlim ();
        bool? busy_in_completed = null;
        worker.DoWork += (_, e) => { e.Result = 42; finished.Set (); };
        worker.RunWorkerCompleted += (_, e) => busy_in_completed = worker.IsBusy;

        worker.RunWorkerAsync ();
        Assert.True (finished.Wait (10_000, TestContext.Current.CancellationToken));

        // DoWork has returned, but its completion is still queued for the UI thread: a second
        // RunWorkerAsync now would interleave with it.
        System.Threading.Thread.Sleep (200);
        Assert.True (worker.IsBusy);

        PumpUntil (() => busy_in_completed.HasValue);
        Assert.False (busy_in_completed);
    }

    [Fact]
    public void FRM34_Result_throws_when_DoWork_failed_or_was_cancelled ()
    {
        using var worker = new BackgroundWorker ();
        var failure = new InvalidTimeZoneException ("boom");
        RunWorkerCompletedEventArgs? args = null;
        worker.DoWork += (_, _) => throw failure;
        worker.RunWorkerCompleted += (_, e) => args = e;

        worker.RunWorkerAsync ();
        PumpUntil (() => args is not null);

        var thrown = Assert.Throws<System.Reflection.TargetInvocationException> (() => args!.Result);
        Assert.Same (failure, thrown.InnerException);

        var cancelled = new RunWorkerCompletedEventArgs (7, null, cancelled: true);
        Assert.Throws<InvalidOperationException> (() => cancelled.Result);
        Assert.Equal (7, new RunWorkerCompletedEventArgs (7, null, cancelled: false).Result);
    }

    // ── FRM-35: product/company had no fallback and the data paths no version segment ─────────────

    // An assembly with none of the attributes an SDK project generates, and an entry-point type in a
    // two-part namespace, as a hand-built AssemblyInfo or an older project leaves them.
    private static (System.Reflection.Assembly Assembly, Type Main) BareAssembly ()
    {
        var name = new System.Reflection.AssemblyName ("Bare") { Version = new Version (2, 3, 4, 5) };
        var builder = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly (name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var main = builder.DefineDynamicModule ("Bare").DefineType ("Contoso.Tools.Viewer.Program").CreateType ();
        return (builder, main);
    }

    [Fact]
    public void FRM35_Product_and_company_fall_back_to_the_entry_types_namespace ()
    {
        var (assembly, main) = BareAssembly ();

        Assert.Equal ("Viewer", Application.ProductNameOf (assembly, main));
        Assert.Equal ("Contoso", Application.CompanyNameOf (assembly, main));
        Assert.Equal ("2.3.4.5", Application.ProductVersionOf (assembly));

        // The attributes still come first: this assembly has all three.
        var real = typeof (FormGapTests).Assembly;
        Assert.Equal (real.GetCustomAttributes (typeof (System.Reflection.AssemblyProductAttribute), false).Cast<System.Reflection.AssemblyProductAttribute> ().Single ().Product,
            Application.ProductNameOf (real, main));
    }

    [Fact]
    public void FRM35_Data_paths_end_in_company_product_version_and_exist ()
    {
        var (assembly, main) = BareAssembly ();
        var root = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "frm35-" + Guid.NewGuid ().ToString ("N"));

        try {
            var path = Application.DataPathOf (root, assembly, main);

            Assert.Equal (System.IO.Path.Combine (root, "Contoso", "Viewer", "2.3.4.5"), path);
            Assert.True (System.IO.Directory.Exists (path));
        } finally {
            if (System.IO.Directory.Exists (root))
                System.IO.Directory.Delete (root, recursive: true);
        }
    }

    // ── FRM-36: RestoreBounds was the current bounds even when maximized ───────────────────────────

    [Fact]
    public void FRM36_RestoreBounds_keeps_the_normal_bounds_while_maximized ()
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point (10, 20), Width = 400, Height = 300 };
        form.Show ();
        var normal = form.Bounds;

        form.WindowState = FormWindowState.Maximized;

        // The platform then grows and moves the window to fill the screen.
        form.Backend.Size = new Size (1900, 1000);
        form.Backend.Location = Point.Empty;
        form.SyncAdapterBounds ();
        form.OnBackendMoved ();

        Assert.NotEqual (normal, form.Bounds);
        Assert.Equal (normal, form.RestoreBounds);

        form.WindowState = FormWindowState.Normal;
        Assert.Equal (form.Bounds, form.RestoreBounds);
    }

    // ── FRM-37: setting Size raised no synchronous Resize or layout ─────────────────────────────────

    [Fact]
    public void FRM37_Setting_Size_lays_out_docked_children_before_returning ()
    {
        var fill = new Panel { Dock = DockStyle.Fill };
        using var form = new Form { Width = 400, Height = 300 };
        form.Controls.Add (fill);
        form.Show ();
        var resizes = 0;
        form.Resize += (_, _) => resizes++;
        var before = fill.Width;

        form.Width = 600;

        Assert.Equal (1, resizes);
        Assert.Equal (before + 200, fill.Width);
    }
}
