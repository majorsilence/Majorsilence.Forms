using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

public class ReportsPanel : BasePanel
{
    private readonly TextBox _dateBox;
    private readonly Label _summaryLabel;
    private readonly DataGridView _topProductsGrid;
    private readonly DataGridView _salesGrid;
    private readonly Label _statusLabel;
    private List<SaleSummaryDto> _sales = [];

    public ReportsPanel(ApiClient api, SessionState session) : base(api, session)
    {
        AutoScroll = true;

        var heading = Controls.Add(new Label { Text = "Reports", Left = 10, Top = 10, Width = 400 });
        PosStyle.Heading(heading);

        Controls.Add(new Label { Text = "Daily Summary — Date (yyyy-MM-dd):", Left = 10, Top = 62, Width = 340 });
        _dateBox = Controls.Add(new TextBox { Left = 360, Top = 58, Width = 150, Height = 40, Text = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        var loadButton = Controls.Add(new Button { Text = "Load", Left = 520, Top = 58, Width = 110, Height = 40 });
        PosStyle.SecondaryButton(loadButton, fontSize: 20);
        loadButton.Click += async (_, _) => await LoadDailySummaryAsync();

        _summaryLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 112, Width = 1030, Height = 60 });

        var topProductsHeading = Controls.Add(new Label { Text = "Top Products", Left = 10, Top = 190, Width = 300 });
        PosStyle.Heading(topProductsHeading, fontSize: 22);
        _topProductsGrid = Controls.Add(new DataGridView
        {
            Left = 10,
            Top = 234,
            Width = 460,
            Height = 260,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
        });
        GridColumns.AddBound(_topProductsGrid, nameof(TopProductDto.Name), "Product", 210);
        GridColumns.AddBound(_topProductsGrid, nameof(TopProductDto.QuantitySold), "Qty Sold", 120);
        GridColumns.AddBound(_topProductsGrid, nameof(TopProductDto.Revenue), "Revenue", 130);

        var recentSalesHeading = Controls.Add(new Label { Text = "Recent Sales", Left = 490, Top = 190, Width = 300 });
        PosStyle.Heading(recentSalesHeading, fontSize: 22);
        _salesGrid = Controls.Add(new DataGridView
        {
            Left = 490,
            Top = 234,
            Width = 550,
            Height = 260,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        });
        GridColumns.AddBound(_salesGrid, nameof(SaleSummaryDto.SaleNumber), "Sale #", 170);
        GridColumns.AddBound(_salesGrid, nameof(SaleSummaryDto.CashierDisplayName), "Cashier", 150);
        GridColumns.AddBound(_salesGrid, nameof(SaleSummaryDto.Total), "Total", 100);
        GridColumns.AddBound(_salesGrid, nameof(SaleSummaryDto.Status), "Status", 110);

        var voidButton = Controls.Add(new Button { Text = "Void Selected Sale", Left = 490, Top = 508, Width = 230, Height = 48 });
        PosStyle.CautionButton(voidButton);
        voidButton.Click += async (_, _) => await VoidSelectedSaleAsync();

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 570, Width = 1030 });
    }

    public override void LoadPanel()
    {
        _ = LoadDailySummaryAsync();
        _ = LoadTopProductsAsync();
        _ = LoadSalesAsync();
    }

    private async Task LoadDailySummaryAsync()
    {
        if (!DateOnly.TryParse(_dateBox.Text, out var date))
        {
            _summaryLabel.Text = "Enter a valid date.";
            return;
        }

        try
        {
            var summary = await Api.GetDailySummaryAsync(date);
            _summaryLabel.Text =
                $"{summary.Date:yyyy-MM-dd} — Sales: {summary.SaleCount}   Gross: {summary.GrossTotal:C}   " +
                $"Tax: {summary.TaxTotal:C}   Discounts: {summary.DiscountTotal:C}   Net: {summary.NetTotal:C}   " +
                $"(Cash {summary.ByTenderType.Cash:C} / Card {summary.ByTenderType.Card:C})";
        }
        catch (Exception ex)
        {
            _summaryLabel.Text = $"Could not load daily summary: {ex.Message}";
        }
    }

    private async Task LoadTopProductsAsync()
    {
        try
        {
            var top = await Api.GetTopProductsAsync(take: 10);
            GridColumns.Rebind(_topProductsGrid, top);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load top products: {ex.Message}";
        }
    }

    private async Task LoadSalesAsync()
    {
        try
        {
            _sales = await Api.GetSalesAsync();
            GridColumns.Rebind(_salesGrid, _sales);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load sales: {ex.Message}";
        }
    }

    private async Task VoidSelectedSaleAsync()
    {
        var index = _salesGrid.SelectedRowIndex;
        if (index < 0 || index >= _sales.Count)
        {
            _statusLabel.Text = "Select a sale to void first.";
            return;
        }

        var sale = _sales[index];
        if (sale.Status == SaleStatus.Voided)
        {
            _statusLabel.Text = "That sale is already voided.";
            return;
        }

        try
        {
            await Api.VoidSaleAsync(sale.Id);
            _statusLabel.Text = $"Sale {sale.SaleNumber} voided; stock restored.";
            await LoadSalesAsync();
        }
        catch (ApiException ex)
        {
            _statusLabel.Text = $"Could not void sale: {ex.Body}";
        }
    }
}
