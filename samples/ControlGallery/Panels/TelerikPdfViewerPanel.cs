using System;
using System.IO;
using Majorsilence.Forms;
using Majorsilence.Forms.Printing;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadPdfViewer + RadPdfViewerNavigator (Majorsilence.Forms.Telerik). A small demo PDF is
    // generated at runtime through Majorsilence.Forms.Printing.PrintDocument (rather than shipping a binary
    // PDF asset) and loaded into the viewer via LoadDocument(Stream). On platforms whose native webview
    // renders PDF inline (Windows/macOS — see RadPdfViewer.CanRenderPdfInline) the document displays directly
    // in the viewer with the browser engine's own PDF toolbar; otherwise it falls back to the system's
    // default PDF viewer and a one-line placeholder, per RadPdfViewer's documented policy.
    public class TelerikPdfViewerPanel : BasePanel
    {
        private readonly RadPdfViewer viewer;
        private readonly Label status;

        public TelerikPdfViewerPanel ()
        {
            Controls.Add (new Label {
                Text = "RadPdfViewer + RadPdfViewerNavigator — loads a PDF generated on the fly via Majorsilence.Forms.Printing.PrintDocument.",
                Left = 10, Top = 10, Width = 780, Height = 20
            });

            status = new Label {
                Text = "Last action: (none)",
                Left = 10, Top = 32, Width = 780
            };
            Controls.Add (status);

            var navigator = new RadPdfViewerNavigator { Left = 10, Top = 58, Width = 780 };
            Controls.Add (navigator);

            viewer = new RadPdfViewer {
                Left = 10, Top = 58 + navigator.Height, Width = 780, Height = 460
            };
            navigator.AssociatedViewer = viewer;
            Controls.Add (viewer);

            var generateButton = new Button { Text = "Generate && Load Demo PDF", Left = 10, Top = 526, Width = 220, Height = 30 };
            generateButton.Click += (_, _) => GenerateAndLoad ();
            Controls.Add (generateButton);

            var unloadButton = new Button { Text = "Unload Document", Left = 240, Top = 526, Width = 160, Height = 30 };
            unloadButton.Click += (_, _) => {
                viewer.UnloadDocument ();
                Report ("Document unloaded");
            };
            Controls.Add (unloadButton);

            Controls.Add (new Label {
                Text = $"IsInlineRenderingActive: {viewer.IsInlineRenderingActive} (CanRenderPdfInline: {RadPdfViewer.CanRenderPdfInline})",
                Left = 420, Top = 531, Width = 370
            });
        }

        private void GenerateAndLoad ()
        {
            using var ms = new MemoryStream ();
            BuildDemoPdf ().PrintToPdf (ms);
            ms.Position = 0;

            viewer.LoadDocument (ms);
            Report ("Generated a demo PDF and loaded it into RadPdfViewer");
        }

        private static PrintDocument BuildDemoPdf ()
        {
            var titleFont = new Majorsilence.Drawing.Font ("Arial", 16, bold: true);
            var bodyFont = new Majorsilence.Drawing.Font ("Arial", 10);

            var doc = new PrintDocument { DocumentName = "telerik-pdf-viewer-demo" };
            doc.PrintPage += (_, e) => {
                var g = e.Graphics;
                var rect = e.MarginBounds;
                g.DrawString ("RadPdfViewer demo document", titleFont, Majorsilence.Drawing.Brushes.Black,
                    new System.Drawing.RectangleF (rect.Left, rect.Top, rect.Width, 30), ContentAlignment.TopLeft);
                g.DrawString ($"Generated {DateTime.Now}", bodyFont, Majorsilence.Drawing.Brushes.Gray,
                    new System.Drawing.RectangleF (rect.Left, rect.Top + 34, rect.Width, 24), ContentAlignment.TopLeft);
                g.DrawString ("This PDF was generated at runtime via Majorsilence.Forms.Printing.PrintDocument and loaded into a RadPdfViewer through LoadDocument(Stream).",
                    bodyFont, Majorsilence.Drawing.Brushes.Black,
                    new System.Drawing.RectangleF (rect.Left, rect.Top + 70, rect.Width, 80), ContentAlignment.TopLeft);
                e.HasMorePages = false;
            };
            return doc;
        }

        private void Report (string action) => status.Text = $"Last action: {action}";

        public override void UnloadPanel ()
        {
            viewer.UnloadDocument ();
        }
    }
}
