using WF = System.Windows.Forms;
using CF = Majorsilence.Forms;
using Majorsilence.Forms.WinForms;

namespace EmbeddingWinForms;

// A native WinForms Form whose content is a mix of native WinForms controls and an embedded
// Majorsilence.Forms scene (hosted by MajorsilenceFormsPresenter). Mirrors samples/EmbeddingAvalonia
// and samples/EmbeddingUno.
public sealed class MainForm : WF.Form
{
    public MainForm ()
    {
        Text = "Majorsilence.Forms embedded in WinForms";
        ClientSize = new System.Drawing.Size (820, 560);

        var heading = new WF.Label {
            Text = "Native WinForms controls",
            Font = new System.Drawing.Font (Font, System.Drawing.FontStyle.Bold),
            Left = 12, Top = 12, Width = 400, Height = 20,
        };

        var nativeBox = new WF.TextBox {
            PlaceholderText = "A native WinForms TextBox",
            Left = 12, Top = 40, Width = 260,
        };

        // WinFormsHostInterop.ToWinFormsForm(): a Majorsilence.Forms Form's backend window is a real
        // System.Windows.Forms.Form under the hood (when the WinForms backend is active), created
        // eagerly in the Form's own constructor. This hands that form straight back so the host can
        // use it exactly like any other WinForms form — here, ShowDialog(owner) for a real
        // native-modal relationship.
        var dialogButton = new WF.Button {
            Text = "Open as WinForms dialog",
            Left = 284, Top = 38, Width = 180, Height = 28,
        };
        dialogButton.Click += (_, _) => {
            var form = BuildDialogForm ();
            using var native = form.ToWinFormsForm ();
            native.ShowDialog (this);
        };

        var divider = new WF.Label {
            BorderStyle = WF.BorderStyle.Fixed3D,
            Left = 12, Top = 78, Width = 796, Height = 2,
            Anchor = WF.AnchorStyles.Top | WF.AnchorStyles.Left | WF.AnchorStyles.Right,
        };

        var subheading = new WF.Label {
            Text = "Embedded Majorsilence.Forms (MajorsilenceFormsPresenter)",
            Font = new System.Drawing.Font (Font, System.Drawing.FontStyle.Bold),
            Left = 12, Top = 90, Width = 500, Height = 20,
        };

        // WinFormsHostInterop.ToWinFormsControl(): equivalent to
        // `new MajorsilenceFormsPresenter { Content = BuildMajorsilenceScene () }`, just via the
        // public extension method.
        var presenter = BuildMajorsilenceScene ().ToWinFormsControl ();
        presenter.SetBounds (12, 116, 796, 432);
        presenter.Anchor = WF.AnchorStyles.Top | WF.AnchorStyles.Bottom | WF.AnchorStyles.Left | WF.AnchorStyles.Right;

        Controls.Add (heading);
        Controls.Add (nativeBox);
        Controls.Add (dialogButton);
        Controls.Add (divider);
        Controls.Add (subheading);
        Controls.Add (presenter);
    }

    // A small Majorsilence.Forms Form shown as a real WinForms dialog via ToWinFormsForm() (see the
    // "Open as WinForms dialog" button above) rather than through Form.Show()/ShowDialog().
    private static CF.Form BuildDialogForm ()
    {
        var form = new CF.Form {
            Text = "Majorsilence.Forms dialog",
            ClientSize = new System.Drawing.Size (320, 170),
        };

        // Top values clear the form's self-drawn title bar (FormTitleBar is a top-docked control
        // inside the form's surface, ~32 logical px, so content at Top < 32 would sit under it).
        var label = new CF.Label {
            Text = "This Form is hosted by a real WinForms dialog.",
            Left = 12, Top = 44, Width = 280, Height = 24,
        };

        var closeButton = new CF.Button {
            Text = "Close",
            Left = 12, Top = 80, Width = 100, Height = 32,
        };
        closeButton.Click += (_, _) => form.Close ();

        form.Controls.Add (label);
        form.Controls.Add (closeButton);

        return form;
    }

    // Builds a small Majorsilence.Forms control tree exercising render + input + popups.
    private static CF.Panel BuildMajorsilenceScene ()
    {
        var panel = new CF.Panel ();

        var label = new CF.Label {
            Text = "Majorsilence.Forms controls:",
            Left = 12, Top = 12, Width = 420, Height = 24,
        };

        var textbox = new CF.TextBox {
            Text = "Edit me",
            Left = 12, Top = 44, Width = 240, Height = 30,
        };

        var combo = new CF.ComboBox {
            Left = 12, Top = 84, Width = 240, Height = 30,
        };
        combo.Items.Add ("First");
        combo.Items.Add ("Second");
        combo.Items.Add ("Third");

        var button = new CF.Button {
            Text = "Majorsilence Button",
            Left = 12, Top = 124, Width = 160, Height = 34,
        };

        var status = new CF.Label {
            Text = "Clicks: 0",
            Left = 12, Top = 168, Width = 300, Height = 24,
        };

        var clicks = 0;
        button.Click += (_, _) => { clicks++; status.Text = $"Clicks: {clicks} — text: \"{textbox.Text}\""; };

        // A real native WinForms control hosted *inside* the Majorsilence scene (airspace overlay).
        var nativeButton = new WF.Button { Text = "Native WinForms button" };
        var nativeHost = new CF.NativeControlHost {
            Left = 280, Top = 44, Width = 240, Height = 40,
            NativeControl = nativeButton,
        };
        var nativeCheck = new WF.CheckBox { Text = "Native WinForms checkbox" };
        var nativeCheckHost = new CF.NativeControlHost {
            Left = 280, Top = 92, Width = 240, Height = 32,
            NativeControl = nativeCheck,
        };
        nativeButton.Click += (_, _) => status.Text = "Native WinForms button clicked!";

        panel.Controls.Add (label);
        panel.Controls.Add (textbox);
        panel.Controls.Add (combo);
        panel.Controls.Add (button);
        panel.Controls.Add (status);
        panel.Controls.Add (nativeHost);
        panel.Controls.Add (nativeCheckHost);

        return panel;
    }
}
