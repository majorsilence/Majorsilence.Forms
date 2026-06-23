using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class WinFormsPackagesTests
{
    [Theory]
    [InlineData ("Telerik.UI.for.WinForms", true)]
    [InlineData ("Telerik.UI.for.WinForms.AllControls", true)]
    [InlineData ("telerik.ui.for.winforms.themes", true)]   // case-insensitive
    [InlineData ("DevExpress.Win.Grid", true)]
    [InlineData ("Infragistics.Win.UltraWinGrid", true)]
    [InlineData ("C1.Win.FlexGrid", true)]
    [InlineData ("Syncfusion.SfDataGrid.WinForms", true)]
    [InlineData ("Newtonsoft.Json", false)]
    [InlineData ("Telerik.Documents.Core", false)]          // not a WinForms UI package
    [InlineData ("Majorsilence.Forms", false)]
    public void IsMatch_against_the_default_patterns (string id, bool expected)
    {
        Assert.Equal (expected, WinFormsPackages.IsMatch (id, WinFormsPackages.DefaultPatterns));
    }

    [Fact]
    public void Wildcard_matches_a_custom_pattern ()
    {
        Assert.True (WinFormsPackages.IsMatch ("Acme.WinForms.Grid", new[] { "Acme.WinForms.*" }));
        Assert.False (WinFormsPackages.IsMatch ("Acme.Core", new[] { "Acme.WinForms.*" }));
    }
}
