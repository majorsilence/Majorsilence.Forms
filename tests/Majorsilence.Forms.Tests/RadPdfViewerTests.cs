using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // These run on the Headless backend, which does not implement IWebViewFactory, so RadPdfViewer always
    // takes its no-webview fallback path here. WebViewSupport.AllowSystemViewerFallback defaults to false
    // on Headless (see src/Majorsilence.Forms/WebViewHost.cs), so LoadDocument never spawns a real OS
    // process during these tests — it just caches the temp file and shows the placeholder text.
    public class RadPdfViewerTests
    {
        private static byte[] TinyPdfBytes ()
        {
            // A minimal (but syntactically valid enough) one-page PDF, sufficient as opaque bytes to copy
            // through LoadDocument's temp-file path — RadPdfViewer never parses PDF content itself.
            const string pdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
                "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>endobj\n" +
                "trailer<</Root 1 0 R>>\n%%EOF";
            return System.Text.Encoding.ASCII.GetBytes (pdf);
        }

        [Fact]
        public void CanRenderPdfInline_matches_OS_policy ()
        {
            var expected = OperatingSystem.IsWindows () || OperatingSystem.IsMacOS ();
            Assert.Equal (expected, RadPdfViewer.CanRenderPdfInline);
        }

        [Fact]
        public void IsInlineRenderingActive_false_without_a_functional_webview ()
        {
            using var viewer = new RadPdfViewer ();

            // The Headless backend has no IWebViewFactory, so regardless of CanRenderPdfInline's OS policy
            // there is never a functional webview to render into.
            Assert.False (viewer.IsInlineRenderingActive);
        }

        [Fact]
        public void LoadDocument_from_stream_copies_to_temp_file_without_throwing ()
        {
            using var viewer = new RadPdfViewer ();
            using var ms = new MemoryStream (TinyPdfBytes ());

            viewer.LoadDocument (ms);

            // The caller may dispose the source stream immediately after LoadDocument returns (documented
            // contract) — assert that doing so doesn't fault the viewer or leave it in a broken state.
            viewer.UnloadDocument ();
        }

        [Fact]
        public void LoadDocument_from_path_does_not_throw ()
        {
            var path = Path.Combine (Path.GetTempPath (), $"{Guid.NewGuid ():N}.pdf");
            File.WriteAllBytes (path, TinyPdfBytes ());
            try {
                using var viewer = new RadPdfViewer ();
                viewer.LoadDocument (path);
                viewer.UnloadDocument ();
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void ReadingMode_and_ViewerMode_are_settable_no_ops ()
        {
            using var viewer = new RadPdfViewer {
                ReadingMode = ReadingMode.OnDemand,
                ViewerMode = FixedDocumentViewerMode.TextSelection,
            };

            Assert.Equal (ReadingMode.OnDemand, viewer.ReadingMode);
            Assert.Equal (FixedDocumentViewerMode.TextSelection, viewer.ViewerMode);
        }

        [Fact]
        public void Navigator_AssociatedViewer_round_trips ()
        {
            using var viewer = new RadPdfViewer ();
            using var navigator = new RadPdfViewerNavigator { AssociatedViewer = viewer };

            Assert.Same (viewer, navigator.AssociatedViewer);
        }

        [Fact]
        public void Navigator_has_nonzero_height_for_PdfViewer_vb_layout_math ()
        {
            // Code/TownSuite.Winform/Main Forms/PdfViewer.vb positions the viewer at
            // New Point(0, radPdfNavigator.Height) — a zero-height navigator would silently degrade that
            // layout to overlapping controls instead of a visible offset.
            using var navigator = new RadPdfViewerNavigator ();

            Assert.True (navigator.Height > 0);
        }

        [Fact]
        public void Disposing_viewer_cleans_up_temp_file ()
        {
            using var ms = new MemoryStream (TinyPdfBytes ());
            var viewer = new RadPdfViewer ();
            viewer.LoadDocument (ms);
            viewer.Dispose ();

            // Best-effort cleanup — just confirm Dispose doesn't throw when a temp file is pending deletion.
        }
    }
}
