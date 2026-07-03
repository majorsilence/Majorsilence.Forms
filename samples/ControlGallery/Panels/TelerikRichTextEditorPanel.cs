using Majorsilence.Forms;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadRichTextEditor + RichTextEditorRibbonBar (Majorsilence.Forms.Telerik). The editor is
    // pre-loaded with a sample HTML Document; the ribbon's buttons drive the editor's execCommand-backed
    // formatting live (bold/italic/underline/lists/alignment/undo/redo). DocumentChanged reports the raw
    // HTML back into the status label so the effect of each toolbar click is visible.
    public class TelerikRichTextEditorPanel : BasePanel
    {
        private readonly RadRichTextEditor editor;
        private readonly Label status;

        private const string SampleHtml =
            "<p>Welcome to <b>RadRichTextEditor</b>.</p>" +
            "<p>Select some text and use the ribbon above to <i>format</i> it, or try the list/alignment buttons.</p>" +
            "<ul><li>First item</li><li>Second item</li></ul>";

        public TelerikRichTextEditorPanel ()
        {
            Controls.Add (new Label {
                Text = "RadRichTextEditor + RichTextEditorRibbonBar — the ribbon's buttons call ExecCommand on the editor; DocumentChanged reports the live HTML below.",
                Left = 10, Top = 10, Width = 780, Height = 20
            });

            var ribbon = new RichTextEditorRibbonBar { Left = 10, Top = 34, Width = 780 };
            Controls.Add (ribbon);

            editor = new RadRichTextEditor {
                Left = 10, Top = 34 + ribbon.Height, Width = 780, Height = 320,
                Document = new RadDocument { Html = SampleHtml },
            };
            ribbon.AssociatedRichTextEditor = editor;
            editor.DocumentChanged += (_, _) => Report ("DocumentChanged fired");
            Controls.Add (editor);

            status = new Label { Text = "Last action: (none)", Left = 10, Top = 34 + ribbon.Height + 326, Width = 780 };
            Controls.Add (status);

            var readOnlyToggle = new CheckBox { Text = "Read only", Left = 10, Top = 34 + ribbon.Height + 350, Width = 120 };
            readOnlyToggle.CheckedChanged += (_, _) => {
                editor.IsReadOnly = readOnlyToggle.Checked;
                Report ($"IsReadOnly = {editor.IsReadOnly}");
            };
            Controls.Add (readOnlyToggle);

            var resetButton = new Button { Text = "Reset Sample HTML", Left = 140, Top = 34 + ribbon.Height + 346, Width = 170, Height = 28 };
            resetButton.Click += (_, _) => {
                editor.Document = new RadDocument { Html = SampleHtml };
                Report ("Reset to sample HTML");
            };
            Controls.Add (resetButton);

            var showHtmlButton = new Button { Text = "Show Exported HTML", Left = 320, Top = 34 + ribbon.Height + 346, Width = 180, Height = 28 };
            showHtmlButton.Click += (_, _) => {
                var provider = new HtmlFormatProvider ();
                var html = provider.Export (editor.Document);
                MessageBox.Show (html, "HtmlFormatProvider.Export");
            };
            Controls.Add (showHtmlButton);

            Controls.Add (new Label {
                Text = $"IsWebViewFunctional: {editor.IsWebViewFunctional}",
                Left = 510, Top = 34 + ribbon.Height + 351, Width = 280
            });
        }

        private void Report (string action) => status.Text = $"Last action: {action}";
    }
}
