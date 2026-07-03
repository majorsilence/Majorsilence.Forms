using System.Globalization;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // These run on the Headless backend, which does not implement IWebViewFactory, so RadRichTextEditor
    // always composes its RichTextBox fallback here (see src/Majorsilence.Forms/Telerik/RadRichTextEditor.cs)
    // rather than a real webview page. That still exercises the full Document/RadDocument/HtmlFormatProvider
    // round-trip contract end to end, just via the plain-text-source path instead of the JS bridge.
    public class RadRichTextEditorTests
    {
        [Fact]
        public void IsWebViewFunctional_false_on_Headless ()
        {
            using var editor = new RadRichTextEditor ();
            Assert.False (editor.IsWebViewFunctional);
        }

        [Fact]
        public void Document_round_trips_through_fallback_text_box ()
        {
            using var editor = new RadRichTextEditor ();

            editor.Document = new RadDocument { Html = "<p>Hello <b>world</b></p>" };

            Assert.Equal ("<p>Hello <b>world</b></p>", editor.Document.Html);
        }

        [Fact]
        public void Setting_Document_raises_DocumentChanged_via_fallback_text_change ()
        {
            using var editor = new RadRichTextEditor ();
            var raised = false;
            editor.DocumentChanged += (_, _) => raised = true;

            editor.Document = new RadDocument { Html = "<p>content</p>" };

            Assert.True (raised);
        }

        [Fact]
        public void IsReadOnly_round_trips ()
        {
            using var editor = new RadRichTextEditor ();

            editor.IsReadOnly = true;

            Assert.True (editor.IsReadOnly);
        }

        [Fact]
        public void SpellChecker_defaults_to_a_DocumentSpellChecker ()
        {
            using var editor = new RadRichTextEditor ();

            Assert.IsType<DocumentSpellChecker> (editor.SpellChecker);
        }

        [Fact]
        public void DocumentSpellChecker_AddDictionary_does_not_throw ()
        {
            var checker = new DocumentSpellChecker ();

            checker.AddDictionary (new RadEn_USDictionary (), CultureInfo.GetCultureInfo ("en-US"));
        }

        [Fact]
        public void HtmlFormatProvider_import_export_round_trips_html ()
        {
            var provider = new HtmlFormatProvider ();
            const string html = "<p>Some <i>formatted</i> text</p>";

            var document = provider.Import (html);
            var exported = provider.Export (document);

            Assert.Equal (html, exported);
        }

        [Fact]
        public void HtmlFormatProvider_export_with_settings_matches_plain_export ()
        {
            var provider = new HtmlFormatProvider ();
            var document = new RadDocument { Html = "<p>x</p>" };
            var settings = new HtmlExportSettings {
                DocumentExportLevel = DocumentExportLevel.Fragment,
                StylesExportMode = StylesExportMode.Inline,
            };

            Assert.Equal (provider.Export (document), provider.Export (document, settings));
        }

        [Fact]
        public void RadDocument_defaults_to_empty_html ()
        {
            var document = new RadDocument ();
            Assert.Equal (string.Empty, document.Html);
        }
    }
}
