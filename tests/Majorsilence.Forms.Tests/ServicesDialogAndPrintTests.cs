using System.Drawing;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Printing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// #348, the services P2s:
// SVC-26  FolderBrowserDialog.Description was never shown and SelectedPath was never the start folder.
// SVC-32  PrintDocument.BeginPrint/EndPrint were EventHandler, not PrintEventHandler, and did not follow
//         upstream's begin/cancel order.
// SVC-35  FontDialog: Apply never raised, MinSize/MaxSize/FontMustExist/FixedPitchOnly not applied,
//         Underline/Strikeout lost on OK, no colour choice.
// SVC-36  ColorDialog: CustomColors/FullOpen/AllowFullOpen stored only, no way to enter an arbitrary colour,
//         and a swatch click closed the dialog.
// (SVC-18, the per-format clipboard text, is in ClipboardTests.)
[Collection ("Headless")]
public sealed class ServicesDialogAndPrintTests : IDisposable
{
    public ServicesDialogAndPrintTests () => HeadlessRenderer.Use ();

    public void Dispose () => HeadlessRenderer.OpenFolderResponse = null;

    // ── SVC-26: FolderBrowserDialog ──────────────────────────────────────────────────────────────────

    private static async Task<FolderDialogRequest?> RequestFor (FolderBrowserDialog dialog)
    {
        using var form = new Form ();
        form.Show ();

        FolderDialogRequest? seen = null;
        HeadlessRenderer.OpenFolderResponse = request => { seen = request; return null; };

        Assert.Equal (DialogResult.Cancel, await dialog.ShowDialogAsync (form));
        return seen;
    }

    [Fact]
    public async Task The_description_is_the_picker_title_and_SelectedPath_is_where_it_opens ()
    {
        var folder = Directory.CreateTempSubdirectory ("svc26-").FullName;

        try {
            var dialog = new FolderBrowserDialog { Description = "Choose the export folder", SelectedPath = folder };
            var request = await RequestFor (dialog);

            Assert.NotNull (request);
            Assert.Equal ("Choose the export folder", request!.Title);
            Assert.Equal (Path.GetFullPath (folder), request.InitialDirectory);
        } finally {
            Directory.Delete (folder);
        }
    }

    [Fact]
    public async Task An_explicit_Title_and_InitialDirectory_win_unless_UseDescriptionForTitle ()
    {
        var start = Directory.CreateTempSubdirectory ("svc26-start-").FullName;
        var selected = Directory.CreateTempSubdirectory ("svc26-selected-").FullName;

        try {
            var dialog = new FolderBrowserDialog {
                Description = "Prompt", Title = "Own title", InitialDirectory = start, SelectedPath = selected
            };

            var request = await RequestFor (dialog);
            Assert.Equal ("Own title", request!.Title);
            Assert.Equal (Path.GetFullPath (start), request.InitialDirectory);

            dialog.UseDescriptionForTitle = true;
            Assert.Equal ("Prompt", (await RequestFor (dialog))!.Title);
        } finally {
            Directory.Delete (start);
            Directory.Delete (selected);
        }
    }

    // ── SVC-32: PrintDocument.BeginPrint / EndPrint ───────────────────────────────────────────────────

    private sealed class CountingController : PrintController
    {
        internal int Starts;
        internal int Ends;

        public override void OnStartPrint (PrintDocument document, PrintEventArgs e) => Starts++;

        public override void OnEndPrint (PrintDocument document, PrintEventArgs e) => Ends++;
    }

    // A method with the upstream handler shape, subscribed the way a ported designer file does. That this
    // compiles at all is half the finding: the events were EventHandler.
    private static void CancelWhenNothingToPrint (object sender, PrintEventArgs e) => e.Cancel = true;

    [Fact]
    public void Cancelling_BeginPrint_stops_the_job_before_any_page_and_nothing_reaches_the_printer ()
    {
        using var document = new PrintDocument ();
        var controller = new CountingController ();
        document.PrintController = controller;
        document.BeginPrint += new PrintEventHandler (CancelWhenNothingToPrint);

        PrintEventArgs? ended = null;
        document.EndPrint += (_, e) => ended = e;
        var pages_printed = 0;
        document.PrintPage += (_, _) => pages_printed++;

        var submitted = false;
        NativePrinting.LauncherOverride = _ => submitted = true;

        try {
            document.Print ();
        } finally {
            NativePrinting.LauncherOverride = null;
        }

        Assert.Equal (0, pages_printed);
        Assert.False (submitted);

        // Upstream's PrintController.Print: the document's EndPrint still runs, with the same cancelled
        // args, and the controller -- never started -- is never ended either.
        Assert.NotNull (ended);
        Assert.True (ended!.Cancel);
        Assert.Equal (0, controller.Starts);
        Assert.Equal (0, controller.Ends);
    }

    [Fact]
    public void BeginPrint_and_EndPrint_share_one_args_object_whose_action_names_the_destination ()
    {
        using var document = new PrintDocument ();
        PrintEventArgs? begun = null, ended = null;
        document.BeginPrint += (_, e) => begun = e;
        document.EndPrint += (_, e) => ended = e;
        document.PrintPage += (_, e) => e.HasMorePages = false;

        document.Print ();   // the assembly's launcher stub keeps it off any real printer (NoRealPrinting)

        Assert.NotNull (begun);
        Assert.Same (begun, ended);
        Assert.Equal (PrintAction.PrintToPrinter, begun!.PrintAction);
    }

    // ── SVC-35: FontDialog ───────────────────────────────────────────────────────────────────────────

    private static Button ButtonOf (Form dialog, string text)
        => dialog.Controls.OfType<Button> ().Single (b => b.Text == text);

    [Fact]
    public void Underline_and_strikeout_survive_OK ()
    {
        using var dialog = new FontDialog ();
        dialog.Show ();
        dialog.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 10, FontStyle.Underline | FontStyle.Strikeout);

        ButtonOf (dialog, "OK").PerformClick ();

        Assert.True (dialog.Font.Underline);
        Assert.True (dialog.Font.Strikeout);
    }

    [Fact]
    public void MinSize_and_MaxSize_bound_the_size_the_user_can_pick ()
    {
        using var dialog = new FontDialog { MinSize = 12, MaxSize = 20 };
        dialog.Show ();
        var size = dialog.Controls.OfType<NumericUpDown> ().Single ();

        Assert.Equal (12m, size.Minimum);
        Assert.Equal (20m, size.Maximum);

        // A font outside the limits comes back inside them.
        dialog.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 72);
        ButtonOf (dialog, "OK").PerformClick ();
        Assert.Equal (20f, dialog.Font.Size);

        dialog.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 6);
        ButtonOf (dialog, "OK").PerformClick ();
        Assert.Equal (12f, dialog.Font.Size);
    }

    [Fact]
    public void Apply_raises_with_the_dialogs_current_choice_and_only_when_shown ()
    {
        using var dialog = new FontDialog ();
        dialog.Show ();
        var apply = ButtonOf (dialog, "Apply");
        Assert.False (apply.Visible);   // ShowApply is off by default, as upstream

        dialog.ShowApply = true;
        Assert.True (apply.Visible);

        var bold = dialog.Controls.OfType<CheckBox> ().Single (c => c.Text == "Bold");
        bold.Checked = true;

        bool? bold_when_applied = null;
        dialog.Apply += (_, _) => bold_when_applied = dialog.Font.Bold;
        apply.PerformClick ();

        Assert.True (bold_when_applied);
    }

    [Fact]
    public void FontMustExist_refuses_a_family_that_is_not_installed ()
    {
        Assert.SkipWhen (!SkiaSharp.SKFontManager.Default.FontFamilies.Any (), "no font manager to list families");

        using var dialog = new FontDialog { FontMustExist = true };
        dialog.Show ();
        var family = dialog.Controls.OfType<ComboBox> ().First ();
        var installed = family.Items[0]!.ToString ()!;

        family.Text = "No Such Family 4711";
        ButtonOf (dialog, "OK").PerformClick ();
        Assert.NotEqual (DialogResult.OK, dialog.DialogResult);

        family.Text = installed;
        ButtonOf (dialog, "OK").PerformClick ();
        Assert.Equal (DialogResult.OK, dialog.DialogResult);
        Assert.Equal (installed, dialog.Font.Name, ignoreCase: true);
    }

    [Fact]
    public void The_colour_list_is_offered_with_ShowColor_and_its_choice_is_the_Color_after_OK ()
    {
        using var dialog = new FontDialog ();
        dialog.Show ();
        var colours = dialog.Controls.OfType<ComboBox> ().Single (c => c.DropDownStyle == ComboBoxStyle.DropDownList);
        Assert.False (colours.Visible);

        dialog.ShowColor = true;
        Assert.True (colours.Visible);

        colours.SelectedIndex = colours.FindStringExact ("Navy");
        Assert.Equal (Color.Black, dialog.Color);   // not until OK

        ButtonOf (dialog, "OK").PerformClick ();
        Assert.Equal (Color.Navy.ToArgb (), dialog.Color.ToArgb ());
    }

    // ── SVC-36: ColorDialog ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_RGB_colour_added_to_the_custom_colours_comes_back_as_a_COLORREF_after_OK ()
    {
        using var dialog = new ColorDialog { FullOpen = true };
        dialog.Show ();
        Assert.True (dialog.IsFullOpen);

        dialog.RedBox.Value = 1;
        dialog.GreenBox.Value = 2;
        dialog.BlueBox.Value = 3;
        dialog.AddCustomButton.PerformClick ();

        Assert.Equal (0x00FFFFFF, dialog.CustomColors[0]);   // not committed until OK

        dialog.OkButton.PerformClick ();

        Assert.Equal (0x030201, dialog.CustomColors[0]);
        Assert.Equal (Color.FromArgb (1, 2, 3).ToArgb (), dialog.Color.ToArgb ());
    }

    [Fact]
    public void Cancel_discards_the_colour_and_the_custom_colours ()
    {
        using var dialog = new ColorDialog { Color = Color.Red };
        dialog.Show ();

        dialog.ChooseSwatch (Color.Blue);
        dialog.AddCustomColor ();
        dialog.CancelButtonControl.PerformClick ();

        Assert.Equal (Color.Red, dialog.Color);
        Assert.Equal (0x00FFFFFF, dialog.CustomColors[0]);
    }

    [Fact]
    public void A_swatch_selects_without_closing ()
    {
        using var dialog = new ColorDialog ();
        dialog.Show ();

        dialog.ChooseSwatch (Color.Teal);

        Assert.Equal (DialogResult.None, dialog.DialogResult);
        Assert.Equal (Color.Teal.R, (int) dialog.RedBox.Value);   // the selection fills the RGB boxes
        Assert.Equal (Color.Teal.B, (int) dialog.BlueBox.Value);
    }

    [Fact]
    public void AllowFullOpen_off_keeps_the_custom_pane_shut_even_with_FullOpen ()
    {
        using var dialog = new ColorDialog { FullOpen = true, AllowFullOpen = false };
        dialog.Show ();

        Assert.False (dialog.IsFullOpen);
        Assert.False (dialog.DefineButton.Enabled);
        Assert.False (dialog.RedBox.Visible);
    }

    [Fact]
    public void Custom_colours_set_by_the_application_are_shown_and_pickable ()
    {
        using var dialog = new ColorDialog { CustomColors = new[] { 0x00336699 } };
        dialog.Show ();

        dialog.ChooseCustomSlot (0);
        dialog.OkButton.PerformClick ();

        Assert.Equal (Color.FromArgb (0x99, 0x66, 0x33).ToArgb (), dialog.Color.ToArgb ());
    }
}
