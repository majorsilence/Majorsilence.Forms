using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class SourceConverterTests
{
    [Fact]
    public void Rewrites_WinForms_using_directive ()
    {
        var result = SourceConverter.Convert ("using System.Windows.Forms;\n");
        Assert.Contains ("using Majorsilence.Forms;", result.Text);
        Assert.DoesNotContain ("System.Windows.Forms", result.Text);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Rewrites_fully_qualified_WinForms_reference ()
    {
        var result = SourceConverter.Convert ("System.Windows.Forms.MessageBox.Show(\"hi\");");
        Assert.Contains ("Majorsilence.Forms.MessageBox.Show", result.Text);
    }

    [Theory]
    [InlineData ("Color")]
    [InlineData ("Point")]
    [InlineData ("Size")]
    [InlineData ("Rectangle")]
    [InlineData ("PointF")]
    [InlineData ("SizeF")]
    [InlineData ("RectangleF")]
    public void Keeps_System_Drawing_primitive_value_types (string primitive)
    {
        var src = $"var x = new System.Drawing.{primitive}();";
        var result = SourceConverter.Convert (src);
        Assert.Contains ($"System.Drawing.{primitive}", result.Text);
        Assert.DoesNotContain ($"Majorsilence.Drawing.{primitive}", result.Text);
    }

    [Theory]
    [InlineData ("Bitmap")]
    [InlineData ("Font")]
    [InlineData ("Pen")]
    [InlineData ("SolidBrush")]
    [InlineData ("Icon")]
    [InlineData ("Brushes")]
    [InlineData ("Pens")]
    [InlineData ("TextureBrush")]
    [InlineData ("PathGradientBrush")]
    [InlineData ("SystemIcons")]
    public void Redirects_GDI_plus_types_to_Majorsilence_Drawing (string gdiType)
    {
        var result = SourceConverter.Convert ($"System.Drawing.{gdiType} x;");
        Assert.Contains ($"Majorsilence.Drawing.{gdiType}", result.Text);
    }

    [Fact]
    public void Warns_on_unmapped_GDI_plus_type_and_leaves_it ()
    {
        var result = SourceConverter.Convert ("System.Drawing.Metafile b;");
        Assert.Contains ("System.Drawing.Metafile", result.Text); // left as-is
        Assert.Contains (result.Warnings, w => w.Contains ("System.Drawing.Metafile"));
    }

    [Theory]
    [InlineData ("Graphics")]
    [InlineData ("ContentAlignment")]
    [InlineData ("SystemColors")]
    [InlineData ("SystemFonts")]
    [InlineData ("ColorTranslator")]
    public void Redirects_WinForms_compat_drawing_types_to_Majorsilence_Forms (string type)
    {
        var result = SourceConverter.Convert ($"System.Drawing.{type} x;");
        Assert.Contains ($"Majorsilence.Forms.{type}", result.Text);
        Assert.DoesNotContain (result.Warnings, w => w.Contains (type));
    }

    [Fact]
    public void Rewrites_drawing_sub_namespaces ()
    {
        var result = SourceConverter.Convert ("using System.Drawing.Drawing2D;\nvar m = new System.Drawing.Drawing2D.Matrix();");
        Assert.Contains ("using Majorsilence.Drawing.Drawing2D;", result.Text);
        Assert.Contains ("Majorsilence.Drawing.Drawing2D.Matrix", result.Text);
    }

    [Fact]
    public void Maps_drawing_Printing_to_Majorsilence_Forms_Printing ()
    {
        var result = SourceConverter.Convert ("using System.Drawing.Printing;");
        Assert.Contains ("using Majorsilence.Forms.Printing;", result.Text);
    }

    [Fact]
    public void Removes_unused_bare_Drawing_import ()
    {
        // Nothing from System.Drawing is used here, so the import is dropped entirely.
        var result = SourceConverter.Convert ("using System.Drawing;\nclass C { }\n");
        Assert.DoesNotContain ("using System.Drawing;", result.Text);
        Assert.DoesNotContain ("using Majorsilence.Drawing;", result.Text);
    }

    [Fact]
    public void Keeps_bare_Drawing_import_when_a_primitive_is_used ()
    {
        var result = SourceConverter.Convert ("using System.Drawing;\nColor c;\n");
        Assert.Contains ("using System.Drawing;", result.Text); // Color resolves from System.Drawing
    }

    [Fact]
    public void Replaces_bare_Drawing_import_with_companion_when_only_GDI_plus_is_used ()
    {
        // Bitmap moves to Majorsilence.Drawing and no primitive is used, so the import is swapped.
        var result = SourceConverter.Convert ("using System.Drawing;\nBitmap b;\n");
        Assert.DoesNotContain ("using System.Drawing;", result.Text);
        Assert.Contains ("using Majorsilence.Drawing;", result.Text);
    }

    [Fact]
    public void Keeps_both_imports_when_a_primitive_and_GDI_plus_are_used ()
    {
        var result = SourceConverter.Convert ("using System.Drawing;\nColor c; Bitmap b;\n");
        Assert.Contains ("using System.Drawing;", result.Text);
        Assert.Contains ("using Majorsilence.Drawing;", result.Text);
    }

    [Fact]
    public void Drawing_import_rewrite_is_idempotent ()
    {
        var once = SourceConverter.Convert ("using System.Drawing;\nBitmap b;\n").Text;
        var twice = SourceConverter.Convert (once).Text;
        Assert.Equal (once, twice);
        Assert.Equal (1, CountOccurrences (twice, "using Majorsilence.Drawing;"));
    }

    [Fact]
    public void Comments_out_ApplicationConfiguration_Initialize ()
    {
        var result = SourceConverter.Convert ("ApplicationConfiguration.Initialize();");
        Assert.Contains ("// ApplicationConfiguration.Initialize();", result.Text);
        Assert.Contains (result.Warnings, w => w.Contains ("ApplicationConfiguration.Initialize"));
    }

    [Fact]
    public void Warns_on_unsupported_VisualStyles_namespace ()
    {
        var result = SourceConverter.Convert ("using System.Windows.Forms.VisualStyles;\nusing System.Windows.Forms.Button;");
        Assert.Contains ("System.Windows.Forms.VisualStyles", result.Text); // left as-is
        Assert.Contains (result.Warnings, w => w.Contains ("VisualStyles"));
        Assert.Contains ("Majorsilence.Forms.Button", result.Text); // the generalized guard still rewrites the rest
        Assert.DoesNotContain ("System.Windows.Forms.Button", result.Text);
    }

    [Fact]
    public void Unsupported_subnamespace_warns_once_not_as_a_leaf_type ()
    {
        // System.Drawing.Design is a namespace, not a type — it must not also be reported as a missing
        // "System.Drawing.Design" leaf type by the drawing-type pass.
        var result = SourceConverter.Convert ("System.Drawing.Design.UITypeEditor e;");
        var hits = result.Warnings.Count (w => w.Contains ("System.Drawing.Design"));
        Assert.Equal (1, hits); // exactly one — was two before the dedup fix
        // The lone warning is the namespace one, not the misleading "leaf type" form.
        Assert.DoesNotContain (result.Warnings, w => w.StartsWith ("'System.Drawing.Design' has no", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_rewrite_unrelated_namespace_suffix ()
    {
        var result = SourceConverter.Convert ("using MyApp.System.Drawing.Extensions;");
        Assert.Contains ("MyApp.System.Drawing.Extensions", result.Text);
        Assert.False (result.Changed);
    }

    [Fact]
    public void Warns_on_unqualified_unmapped_GDI_type_under_drawing_import ()
    {
        var result = SourceConverter.Convert ("using System.Drawing;\nImageAttributes a;");
        Assert.Contains (result.Warnings, w => w.Contains ("ImageAttributes"));
    }

    [Fact]
    public void Does_not_warn_on_unmapped_type_without_drawing_import ()
    {
        // No `using System.Drawing;` — `ImageAttributes` is almost certainly an unrelated identifier.
        var result = SourceConverter.Convert ("var ImageAttributes = 1;");
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("ImageAttributes"));
    }

    [Fact]
    public void Unchanged_file_reports_no_change ()
    {
        var result = SourceConverter.Convert ("using System.Text;\nvar x = 1;");
        Assert.False (result.Changed);
        Assert.Empty (result.Warnings);
    }

    [Theory]
    [InlineData ("Telerik.WinControls.UI")]
    [InlineData ("Telerik.WinControls.Enumerations")]
    [InlineData ("Telerik.WinControls")]
    [InlineData ("Telerik.WinControls.UI.Docking")]
    [InlineData ("Telerik.WinControls.UI.Data")]
    [InlineData ("Telerik.WinControls.Data")]
    public void Rewrites_Telerik_using_directive (string ns)
    {
        var result = SourceConverter.Convert ($"using {ns};\n");
        Assert.Contains ("using Majorsilence.Forms.Telerik;", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Rewrites_bare_WinControls_vb_imports ()
    {
        var result = SourceConverter.Convert ("Imports Telerik.WinControls\n", language: SourceLanguage.VisualBasic);
        Assert.Contains ("Imports Majorsilence.Forms.Telerik", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_fully_qualified_docking_type ()
    {
        var result = SourceConverter.Convert ("New Telerik.WinControls.UI.Docking.RadDock()", language: SourceLanguage.VisualBasic);
        Assert.Contains ("New Majorsilence.Forms.Telerik.RadDock()", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Collapses_bare_and_UI_Telerik_imports_to_one ()
    {
        var src = "using Telerik.WinControls;\nusing Telerik.WinControls.UI;\n";
        var result = SourceConverter.Convert (src);
        Assert.Equal (1, CountOccurrences (result.Text, "using Majorsilence.Forms.Telerik;"));
    }

    [Fact]
    public void Leaves_Themes_namespace_and_warns ()
    {
        var result = SourceConverter.Convert ("Telerik.WinControls.Themes.Office2007BlackTheme t;");
        Assert.Contains ("Telerik.WinControls.Themes.Office2007BlackTheme", result.Text);
        Assert.Contains (result.Warnings, w => w.Contains ("Telerik.WinControls.Themes"));
    }

    // --- Phase 7: grid export suite + Export-namespace migrator fix --------------------------------------
    //
    // Telerik.WinControls.UI.Export and the sibling Telerik.WinControls.Export now have a compat
    // implementation (RadGridExport.cs) and are mapped to the flat Majorsilence.Forms.Telerik namespace —
    // they are no longer warn-and-left. Previously the bare `Telerik.WinControls` prefix rule (with no
    // guard for this sub-namespace) fired first and mis-rewrote references into a nonexistent
    // `Majorsilence.Forms.Telerik.Export.*` namespace; the tests below both prove the correct rewrite and
    // regression-guard against that mis-rewrite reappearing.

    [Fact]
    public void Rewrites_UI_Export_namespace_to_flat_Telerik_compat ()
    {
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.Export.DisplayFormatType f;");
        Assert.Contains ("Majorsilence.Forms.Telerik.DisplayFormatType", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        // Regression guard: must not produce the broken nested-namespace artifact the bare-prefix rule
        // used to clip this into.
        Assert.DoesNotContain ("Majorsilence.Forms.Telerik.Export.", result.Text);
        Assert.DoesNotContain ("Majorsilence.Forms.Telerik.UI", result.Text); // the older clipping regression
    }

    [Fact]
    public void Rewrites_bare_WinControls_Export_namespace_to_flat_Telerik_compat ()
    {
        // Mirrors Code/libUtilities/Forms/frmRadGridExport.vb's `Imports Telerik.WinControls.Export` +
        // `New ExportToCSV(...)`.
        var result = SourceConverter.Convert ("Telerik.WinControls.Export.ExportToCSV exporter;");
        Assert.Contains ("Majorsilence.Forms.Telerik.ExportToCSV", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        // Regression guard: this is the exact mis-rewrite the latent bug produced (bare Telerik.WinControls
        // rule clipping the ".Export" segment into the target namespace instead of the shared flat one).
        Assert.DoesNotContain ("Majorsilence.Forms.Telerik.Export.", result.Text);
    }

    [Fact]
    public void Rewrites_grid_export_usage_contract ()
    {
        // Mirrors a representative, fully-qualified slice of Code/libUtilities/Forms/frmRadGridExport.vb
        // (imports collapse the un-qualified call sites, so this exercises the qualified form directly).
        var src = "Dim spreadExporter As Telerik.WinControls.UI.Export.GridViewSpreadExport = " +
                  "New Telerik.WinControls.UI.Export.GridViewSpreadExport(GridController)\n" +
                  "Dim exportRenderer As New Telerik.WinControls.Export.SpreadExportRenderer()\n" +
                  "spreadExporter.SummariesExportOption = Telerik.WinControls.Export.SummariesOption.ExportAll\n" +
                  "spreadExporter.HiddenColumnOption = Telerik.WinControls.Export.HiddenOption.DoNotExport\n" +
                  "spreadExporter.PagingExportOption = Telerik.WinControls.Export.PagingExportOption.AllPages\n" +
                  "spreadExporter.RunExport(fileName, exportRenderer)\n" +
                  "Dim pdfExporter As New Telerik.WinControls.UI.Export.GridViewPdfExport(GridController)\n" +
                  "pdfExporter.RunExport(fileName, New Telerik.WinControls.Export.PdfExportRenderer())\n" +
                  "Telerik.WinControls.UI.RadMessageBox.SetThemeName(GridController.ThemeName)\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains ("Majorsilence.Forms.Telerik.GridViewSpreadExport", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.SpreadExportRenderer", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.SummariesOption.ExportAll", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.HiddenOption.DoNotExport", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.PagingExportOption.AllPages", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.GridViewPdfExport", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.PdfExportRenderer", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadMessageBox", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        Assert.DoesNotContain ("Majorsilence.Forms.Telerik.Export.", result.Text);
    }

    [Fact]
    public void UnmappedTelerikTypes_is_now_empty ()
    {
        // Phase 5 (scheduler data layer + printing) implemented the last of the deliberately-unmapped
        // heavyweight Telerik types (RadPrintDocument, SchedulerBindingDataSource, AppointmentMappingInfo,
        // ResourceMappingInfo, RadPrintWatermark, SchedulerDailyPrintStyle) — see RadSchedulerData.cs /
        // RadScheduler.cs / RadSchedulerPrinting.cs. The set is kept (not deleted) as the designated home
        // for any future heavyweight Telerik type discovered to have no compat implementation yet.
        Assert.Empty (NamespaceMap.UnmappedTelerikTypes);
    }

    // --- Phase 2: RadPdfViewer -------------------------------------------------------------------------

    [Theory]
    [InlineData ("RadPdfViewer")]
    [InlineData ("RadPdfViewerNavigator")]
    [InlineData ("FixedDocumentViewerMode")]
    [InlineData ("ReadingMode")]
    public void Rewrites_RadPdfViewer_types_now_that_a_compat_implementation_exists (string typeName)
    {
        // RadPdfViewer/RadPdfViewerNavigator/FixedDocumentViewerMode/ReadingMode now have Majorsilence.Forms.Telerik
        // compat implementations (Phase 2), so they must no longer be treated as unmapped leaf types left
        // unrewritten under Telerik.WinControls.UI.
        var result = SourceConverter.Convert ($"Telerik.WinControls.UI.{typeName} x;");
        Assert.Contains ($"Majorsilence.Forms.Telerik.{typeName}", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_PdfViewer_usage_contract ()
    {
        // Mirrors the real usage in Code/TownSuite.Winform/Main Forms/PdfViewer.vb.
        var src = "Private radPdf As New Telerik.WinControls.UI.RadPdfViewer\n" +
                  "Private radPdfNavigator As New Telerik.WinControls.UI.RadPdfViewerNavigator\n" +
                  "radPdf.ReadingMode = Telerik.WinControls.UI.ReadingMode.OnDemand\n" +
                  "radPdf.ViewerMode = Telerik.WinControls.UI.FixedDocumentViewerMode.TextSelection\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadPdfViewer", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadPdfViewerNavigator", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.ReadingMode.OnDemand", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.FixedDocumentViewerMode.TextSelection", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    // --- Phase 3: RadRichTextEditor + document model ---------------------------------------------------

    [Theory]
    [InlineData ("RadRichTextEditor")]
    [InlineData ("RichTextEditorRibbonBar")]
    [InlineData ("RadRibbonBar")]
    public void Rewrites_RichTextEditor_types_now_that_a_compat_implementation_exists (string typeName)
    {
        var result = SourceConverter.Convert ($"Telerik.WinControls.UI.{typeName} x;");
        Assert.Contains ($"Majorsilence.Forms.Telerik.{typeName}", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Theory]
    [InlineData ("Telerik.WinForms.Documents.Model.RadDocument")]
    [InlineData ("Telerik.WinForms.Documents.FormatProviders.Html.HtmlFormatProvider")]
    [InlineData ("Telerik.WinForms.Documents.Proofing.DocumentSpellChecker")]
    public void Rewrites_document_model_namespaces_to_flat_Telerik_compat (string qualifiedType)
    {
        var result = SourceConverter.Convert ($"{qualifiedType} x;");
        var expectedLeaf = qualifiedType[(qualifiedType.LastIndexOf ('.') + 1)..];
        Assert.Contains ($"Majorsilence.Forms.Telerik.{expectedLeaf}", result.Text);
        Assert.DoesNotContain ("Telerik.WinForms", result.Text);
    }

    [Fact]
    public void Rewrites_bare_Telerik_WinForms_Documents_namespace_last ()
    {
        var result = SourceConverter.Convert ("Telerik.WinForms.Documents.SomeOtherType x;");
        Assert.Contains ("Majorsilence.Forms.Telerik.SomeOtherType", result.Text);
        Assert.DoesNotContain ("Telerik.WinForms", result.Text);
    }

    [Fact]
    public void Rewrites_RichTextEditorRibbonUI_qualified_tab_type ()
    {
        // Mirrors Code/libSettings/Forms/frmEmailTemplates.Designer.vb's GetChildAt(...) cast chain.
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.RichTextEditorRibbonUI.RichTextEditorRibbonTab t;");
        Assert.Contains ("Majorsilence.Forms.Telerik.RichTextEditorRibbonTab", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        Assert.DoesNotContain ("RichTextEditorRibbonUI", result.Text);
    }

    [Fact]
    public void Guard_does_not_clip_longer_leaf_type_name ()
    {
        // RadPdfViewer/RadPdfViewerNavigator (the original pairing this test used) both gained compat
        // implementations in Phase 2 and are no longer guarded — see Rewrites_PdfViewer_usage_contract,
        // which now covers that they rewrite correctly side by side. RadPrintDocument/SchedulerBindingDataSource
        // (the pairing used after that) gained compat implementations in Phase 5 and are no longer guarded
        // either — see Rewrites_RadPrintDocument_and_SchedulerBindingDataSource_now_that_compat_implementations_exist.
        // UnmappedTelerikTypes is now empty (see UnmappedTelerikTypes_is_now_empty), so this test exercises
        // the word-boundary guard mechanism itself with a name that is (and always will be) synthetic: a
        // longer sibling type sharing a hypothetical guarded name as a literal prefix must still be
        // rewritten normally rather than being swept up by a left-unrewritten guard for the shorter name.
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.RadSomeHypotheticallyGuardedTypeFoo n;");
        Assert.Contains ("Majorsilence.Forms.Telerik.RadSomeHypotheticallyGuardedTypeFoo", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Still_rewrites_mapped_UI_type_alongside_guard ()
    {
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.TableViewDefinition d;");
        Assert.Contains ("Majorsilence.Forms.Telerik.TableViewDefinition", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_fully_qualified_bare_WinControls_member ()
    {
        var result = SourceConverter.Convert ("Telerik.WinControls.ElementVisibility.Collapsed", language: SourceLanguage.VisualBasic);
        Assert.Contains ("Majorsilence.Forms.Telerik.ElementVisibility.Collapsed", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Namespace_prefix_order_is_longest_first ()
    {
        // Invariant the guard mechanism depends on: every dotted extension of a prefix must appear
        // *before* its parent, so the parent's rule never fires first and clips the child's text.
        var prefixes = NamespaceMap.NamespacePrefixes;
        for (var i = 0; i < prefixes.Length; i++)
        for (var j = 0; j < prefixes.Length; j++)
        {
            if (i == j)
                continue;
            var earlier = prefixes[i].From;
            var later = prefixes[j].From;
            if (later.StartsWith (earlier + ".", System.StringComparison.Ordinal))
                Assert.True (j < i, $"'{later}' extends '{earlier}' but does not appear before it");
        }
    }

    [Fact]
    public void Rewrites_fully_qualified_Telerik_reference ()
    {
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.RadGridView grid;");
        Assert.Contains ("Majorsilence.Forms.Telerik.RadGridView", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_fully_qualified_RadSpellChecker_reference ()
    {
        // RadSpellChecker now has a Majorsilence.Forms.Telerik compat implementation (Phase 4), so it must
        // no longer be treated as an unmapped leaf type left unrewritten under Telerik.WinControls.UI.
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.RadSpellChecker rsc;");
        Assert.Contains ("Majorsilence.Forms.Telerik.RadSpellChecker", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    // --- Phase 6: RadDesktopAlert ------------------------------------------------------------------------

    [Fact]
    public void Rewrites_fully_qualified_RadDesktopAlert_reference ()
    {
        // RadDesktopAlert now has a Majorsilence.Forms.Telerik compat implementation (Phase 6), so it must
        // no longer be treated as an unmapped leaf type left unrewritten under Telerik.WinControls.UI.
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.RadDesktopAlert da;");
        Assert.Contains ("Majorsilence.Forms.Telerik.RadDesktopAlert", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_RadDesktopAlert_usage_contract ()
    {
        // Mirrors the real usage in Code/libUtilities/Classes/UtilityProgressBar.vb.
        var src = "Private desktopAlerts As New Telerik.WinControls.UI.RadDesktopAlert()\n" +
                  "desktopAlerts.Popup.AlertElement.CaptionElement.TextAndButtonsElement.TextElement.ForeColor = Color.Red\n" +
                  "desktopAlerts.Popup.AlertElement.CaptionElement.CaptionGrip.BackColor = Color.Red\n" +
                  "desktopAlerts.Popup.AlertElement.CaptionElement.CaptionGrip.GradientStyle = Telerik.WinControls.GradientStyles.Solid\n" +
                  "desktopAlerts.Popup.AlertElement.BackColor = Color.White\n" +
                  "desktopAlerts.Popup.AlertElement.BorderColor = Color.Red\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadDesktopAlert", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.GradientStyles.Solid", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    // --- Phase 5: Scheduler data layer + printing -------------------------------------------------------

    [Theory]
    [InlineData ("SchedulerBindingDataSource")]
    [InlineData ("AppointmentMappingInfo")]
    [InlineData ("ResourceMappingInfo")]
    [InlineData ("RadPrintDocument")]
    [InlineData ("RadPrintWatermark")]
    [InlineData ("SchedulerDailyPrintStyle")]
    [InlineData ("SchedulerWeeklyPrintStyle")]
    [InlineData ("SchedulerMonthlyPrintStyle")]
    [InlineData ("SchedulerDetailsPrintStyle")]
    [InlineData ("SchedulerPrintSettingsDialog")]
    [InlineData ("SchedulerMapping")]
    [InlineData ("ConvertCallback")]
    [InlineData ("RadScheduler")]
    [InlineData ("RadSchedulerNavigator")]
    public void Rewrites_scheduler_types_now_that_a_compat_implementation_exists (string typeName)
    {
        // These all gained Majorsilence.Forms.Telerik compat implementations in Phase 5, so they must no
        // longer be treated as unmapped leaf types left unrewritten under Telerik.WinControls.UI.
        var result = SourceConverter.Convert ($"Telerik.WinControls.UI.{typeName} x;");
        Assert.Contains ($"Majorsilence.Forms.Telerik.{typeName}", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_RadPrintDocument_and_SchedulerBindingDataSource_now_that_compat_implementations_exist ()
    {
        var src = "New Telerik.WinControls.UI.RadPrintDocument()\n" +
                  "New Telerik.WinControls.UI.SchedulerBindingDataSource()\n";
        var result = SourceConverter.Convert (src);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadPrintDocument", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.SchedulerBindingDataSource", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("RadPrintDocument") || w.Contains ("SchedulerBindingDataSource"));
    }

    [Fact]
    public void Rewrites_Reminders_usage_contract ()
    {
        // Mirrors the real usage in Code/TownSuite.Winform/Main Forms/Reminders.vb, whose bare type
        // references (AppointmentMappingInfo, SchedulerMapping, ConvertCallback, ...) resolve via its
        // `Imports Telerik.WinControls.UI` — included here so the namespace-prefix rewrite's Imports rule
        // actually fires (a bare, unqualified name has nothing for the textual rewrite to anchor on).
        var src = "Imports Telerik.WinControls.UI\n" +
                  "Dim appointmentMappingInfo As New AppointmentMappingInfo()\n" +
                  "appointmentMappingInfo.Start = \"Start\"\n" +
                  "Dim idMapping As SchedulerMapping = appointmentMappingInfo.FindByDataSourceProperty(\"Reminder\")\n" +
                  "idMapping.ConvertToDataSource = New ConvertCallback(AddressOf Me.ConvertIdToDataSource)\n" +
                  "SchedulerBindingDataSource1.EventProvider.Mapping = appointmentMappingInfo\n" +
                  "SchedulerBindingDataSource1.EventProvider.DataSource = dt\n" +
                  "RadScheduler1.DataSource = SchedulerBindingDataSource1\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains ("Imports Majorsilence.Forms.Telerik", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_frmPrint_usage_contract ()
    {
        // Mirrors the real usage in Code/LibCM/Forms/frmPrint.vb, whose bare type references resolve via
        // its `Imports Telerik.WinControls.UI` (see Rewrites_Reminders_usage_contract remarks).
        var src = "Imports Telerik.WinControls.UI\n" +
                  "schedular.Print(True, SchPrintDocument)\n" +
                  "schedular.PrintPreview(SchPrintDocument)\n" +
                  "schedular.PrintStyle = New SchedulerDailyPrintStyle(schedular.ActiveView.StartDate, schedular.ActiveView.EndDate)\n" +
                  "schedular.PrintStyle = New SchedulerWeeklyPrintStyle(startDate, endDate)\n" +
                  "schedular.PrintStyle = New SchedulerMonthlyPrintStyle(schedular.ActiveView.StartDate, schedular.ActiveView.EndDate)\n" +
                  "schedular.PrintStyle = New SchedulerDetailsPrintStyle(schedular.ActiveView.StartDate, schedular.ActiveView.EndDate)\n" +
                  "schedular.PrintStyle.DrawPageTitleCalendar = False\n" +
                  "Dim dialog As New SchedulerPrintSettingsDialog()\n" +
                  "dialog.PrintDocument = SchPrintDocument\n" +
                  "dialog.ThemeName = schedular.ThemeName\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains ("Imports Majorsilence.Forms.Telerik", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Rewrites_frmPrint_Designer_watermark_declaration ()
    {
        // Mirrors Code/LibCM/Forms/frmPrint.Designer.vb's InitializeComponent().
        var src = "Dim RadPrintWatermark1 As Telerik.WinControls.UI.RadPrintWatermark = New Telerik.WinControls.UI.RadPrintWatermark()\n" +
                  "SchPrintDocument = New Telerik.WinControls.UI.RadPrintDocument()\n" +
                  "SchPrintDocument.HeaderFont = New Font(\"Microsoft Sans Serif\", 8.25F)\n" +
                  "SchPrintDocument.Watermark = RadPrintWatermark1\n" +
                  "Friend WithEvents SchPrintDocument As Telerik.WinControls.UI.RadPrintDocument\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadPrintWatermark", result.Text);
        Assert.Contains ("Majorsilence.Forms.Telerik.RadPrintDocument", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Collapses_duplicate_Telerik_usings_to_one ()
    {
        // Both Telerik namespaces map to Majorsilence.Forms.Telerik; the result must not contain the
        // duplicate `using` that would otherwise trigger CS0105.
        var src = "using Telerik.WinControls.UI;\nusing Telerik.WinControls.Enumerations;\n";
        var result = SourceConverter.Convert (src);
        Assert.Equal (1, CountOccurrences (result.Text, "using Majorsilence.Forms.Telerik;"));
    }

    [Fact]
    public void Dedup_keeps_distinct_imports ()
    {
        var src = "using System.Text;\nusing System.Linq;\n";
        var result = SourceConverter.Convert (src);
        Assert.Contains ("using System.Text;", result.Text);
        Assert.Contains ("using System.Linq;", result.Text);
    }

    private static int CountOccurrences (string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf (needle, i, System.StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
