using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Forms;

public class ReceiptForm : Form
{
    public ReceiptForm(SaleReceiptDto receipt)
    {
        Text = $"Receipt — {receipt.SaleNumber}";
        ClientSize = new System.Drawing.Size(560, 730);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40 (see the note in TenderDialog).
        var title = Controls.Add(new Label { Text = "Sale Complete", Left = 20, Top = 46, Width = 400 });
        PosStyle.Heading(title, fontSize: 26);

        Controls.Add(new Label { Text = $"Sale: {receipt.SaleNumber}    Cashier: {receipt.CashierDisplayName}", Left = 20, Top = 96, Width = 520 });

        var grid = Controls.Add(new DataGridView
        {
            Left = 20,
            Top = 136,
            Width = 520,
            Height = 340,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
        });
        GridColumns.AddBound(grid, nameof(SaleLineItemDto.ProductNameSnapshot), "Item", 220);
        GridColumns.AddBound(grid, nameof(SaleLineItemDto.Quantity), "Qty", 90);
        GridColumns.AddBound(grid, nameof(SaleLineItemDto.UnitPriceSnapshot), "Price", 100);
        GridColumns.AddBound(grid, nameof(SaleLineItemDto.LineTotal), "Total", 100);
        grid.DataSource = receipt.LineItems.ToList();

        Controls.Add(new Label { Text = $"Subtotal: {receipt.Subtotal:C}", Left = 20, Top = 494, Width = 260 });
        Controls.Add(new Label { Text = $"Discount: {receipt.DiscountTotal:C}", Left = 20, Top = 524, Width = 260 });
        Controls.Add(new Label { Text = $"Tax: {receipt.TaxTotal:C}", Left = 20, Top = 554, Width = 260 });
        Controls.Add(new Label
        {
            Text = receipt.TenderType == TenderType.Cash
                ? $"Cash Tendered: {receipt.AmountTendered:C}    Change: {receipt.ChangeDue:C}"
                : "Card: Approved",
            Left = 280,
            Top = 524,
            Width = 260,
        });

        var totalLabel = Controls.Add(new Label { Text = $"Total: {receipt.Total:C}", Left = 20, Top = 590, Width = 260, Height = 52 });
        PosStyle.Display(totalLabel, fontSize: 26);

        var closeButton = Controls.Add(new Button { Text = "New Sale", Left = 20, Top = 660, Width = 300, Height = 54 });
        PosStyle.PrimaryButton(closeButton);
        closeButton.Click += (_, _) => DialogResult = DialogResult.OK;
    }
}
