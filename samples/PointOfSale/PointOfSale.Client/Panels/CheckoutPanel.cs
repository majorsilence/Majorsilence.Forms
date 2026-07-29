using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Forms;
using PointOfSale.Client.Models;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

public class CheckoutPanel : BasePanel
{
    private readonly List<CartLine> _cart = [];
    private List<ProductDto> _searchResults = [];
    private string _qtyText = "1";

    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private readonly Label _qtyDisplay;
    private readonly DataGridView _cartGrid;
    private readonly Label _subtotalLabel;
    private readonly Label _statusLabel;

    public CheckoutPanel(ApiClient api, SessionState session) : base(api, session)
    {
        // AutoScroll is a safety net: on a smaller/laptop-sized maximized window this layout
        // may still run taller than the viewport, and a scrollbar beats content silently
        // clipping off the bottom.
        AutoScroll = true;

        // Every element below is positioned from the actual bottom edge of the one above it
        // (Top + Height + ~14-20px gap) — NumericKeypad is 272x366 (3x4 84px tiles + 10px gaps),
        // so eyeballing offsets here is what caused earlier rounds of overlap bugs.
        var heading = Controls.Add(new Label { Text = "Checkout", Left = 10, Top = 10, Width = 400 });
        PosStyle.Heading(heading); // Height = 54, bottom = 64

        // Search row (left) and Qty-to-add row (right of it, same top) share the same two-row
        // rhythm: a label at Top=80, then the input/box row at Top=126.
        Controls.Add(new Label { Text = "Find product (SKU or name):", Left = 10, Top = 80, Width = 320, Height = 36 });
        _searchBox = Controls.Add(new TextBox { Left = 10, Top = 126, Width = 300, Height = 48 });
        var searchButton = Controls.Add(new Button { Text = "Search", Left = 320, Top = 124, Width = 160, Height = 48 }); // right edge = 480, bottom = 172
        PosStyle.SecondaryButton(searchButton);
        searchButton.Click += async (_, _) => await SearchAsync();

        Controls.Add(new Label { Text = "Qty to add:", Left = 510, Top = 80, Width = 220, Height = 36 });
        _qtyDisplay = Controls.Add(new Label
        {
            Text = _qtyText,
            Left = 510,
            Top = 126,
            Width = 272,
            Height = 48,
        }); // bottom = 174
        PosStyle.Display(_qtyDisplay);
        var qtyKeypad = Controls.Add(new NumericKeypad { Left = 510, Top = 188 }); // bottom = 188 + 366 = 554
        qtyKeypad.DigitPressed += digit =>
        {
            _qtyText = _qtyText == "0" ? digit.ToString() : _qtyText + digit;
            if (_qtyText.Length > 3)
                _qtyText = _qtyText[..3];
            _qtyDisplay.Text = _qtyText;
        };
        qtyKeypad.BackspacePressed += () =>
        {
            _qtyText = _qtyText.Length > 1 ? _qtyText[..^1] : "1";
            _qtyDisplay.Text = _qtyText;
        };
        qtyKeypad.ClearPressed += () => { _qtyText = "1"; _qtyDisplay.Text = _qtyText; };

        var addButton = Controls.Add(new Button { Text = "Add to Cart", Left = 510, Top = 568, Width = 272, Height = 54 }); // bottom = 622
        PosStyle.PrimaryButton(addButton);
        addButton.Click += (_, _) => AddSelectedToCart();

        // Results list's right edge lines up with the Search button's right edge (480).
        _resultsList = Controls.Add(new ListBox { Left = 10, Top = 186, Width = 470, Height = 340 }); // bottom = 526
        PosStyle.LargeText(_resultsList);

        var tenderButton = Controls.Add(new Button { Text = "Tender / Pay", Left = 812, Top = 80, Width = 280, Height = 120 });
        PosStyle.PrimaryButton(tenderButton, fontSize: 28);
        tenderButton.Click += async (_, _) => await TenderAsync();

        // Cart grid starts below the taller of the two columns above it (resultsList bottom
        // 526, qty/keypad column bottom 622).
        _cartGrid = Controls.Add(new DataGridView
        {
            Left = 10,
            Top = 642,
            Width = 1080,
            Height = 220,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        }); // bottom = 862
        GridColumns.AddBound(_cartGrid, nameof(CartLine.Name), "Item", 320);
        GridColumns.AddBound(_cartGrid, nameof(CartLine.UnitPrice), "Price", 130);
        GridColumns.AddBound(_cartGrid, nameof(CartLine.Quantity), "Qty", 100);
        GridColumns.AddBound(_cartGrid, nameof(CartLine.DiscountAmount), "Discount", 140);
        GridColumns.AddBound(_cartGrid, nameof(CartLine.LineTotal), "Total", 140);

        var removeButton = Controls.Add(new Button { Text = "Remove Line", Left = 10, Top = 882, Width = 220, Height = 54 }); // bottom = 936
        PosStyle.CautionButton(removeButton);
        removeButton.Click += async (_, _) => await RemoveSelectedLineAsync();

        Controls.Add(new Label { Text = "Discount $:", Left = 250, Top = 898, Width = 120, Height = 36 });
        var discountBox = Controls.Add(new TextBox { Left = 380, Top = 890, Width = 120, Height = 44 });
        var applyDiscountButton = Controls.Add(new Button { Text = "Apply Discount", Left = 520, Top = 882, Width = 220, Height = 54 });
        PosStyle.CautionButton(applyDiscountButton);
        applyDiscountButton.Click += async (_, _) => await ApplyDiscountAsync(discountBox.Text);

        _subtotalLabel = Controls.Add(new Label { Text = "Subtotal: $0.00", Left = 760, Top = 882, Width = 330, Height = 54 });
        PosStyle.Display(_subtotalLabel, fontSize: 26);

        RebindCart();

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 950, Width = 1080, Height = 36 });
    }

    private async Task SearchAsync()
    {
        try
        {
            _searchResults = await Api.GetProductsAsync(_searchBox.Text, activeOnly: true);
            _resultsList.Items.Clear();
            foreach (var p in _searchResults)
                _resultsList.Items.Add($"{p.Sku}  {p.Name}  {p.Price:C}  (on hand: {p.QuantityOnHand})");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Search failed: {ex.Message}";
        }
    }

    private void AddSelectedToCart()
    {
        var index = _resultsList.SelectedIndex;
        if (index < 0 || index >= _searchResults.Count)
        {
            _statusLabel.Text = "Select a product from the search results first.";
            return;
        }

        if (!int.TryParse(_qtyText, out var qty) || qty <= 0)
            qty = 1;

        var product = _searchResults[index];
        var existing = _cart.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing is not null)
            existing.Quantity += qty;
        else
            _cart.Add(new CartLine { ProductId = product.Id, Name = product.Name, UnitPrice = product.Price, Quantity = qty });

        _statusLabel.Text = string.Empty;
        RebindCart();
    }

    private async Task RemoveSelectedLineAsync()
    {
        var index = _cartGrid.SelectedRowIndex;
        if (index < 0 || index >= _cart.Count)
            return;

        if (!Session.IsManager && !await ConfirmManagerOverrideAsync())
            return;

        _cart.RemoveAt(index);
        RebindCart();
    }

    private async Task ApplyDiscountAsync(string amountText)
    {
        var index = _cartGrid.SelectedRowIndex;
        if (index < 0 || index >= _cart.Count)
        {
            _statusLabel.Text = "Select a cart line to discount first.";
            return;
        }

        if (!decimal.TryParse(amountText, out var amount) || amount < 0)
        {
            _statusLabel.Text = "Enter a valid discount amount.";
            return;
        }

        if (!Session.IsManager && !await ConfirmManagerOverrideAsync())
            return;

        _cart[index].DiscountAmount = amount;
        _statusLabel.Text = string.Empty;
        RebindCart();
    }

    private async Task<bool> ConfirmManagerOverrideAsync()
    {
        using var dialog = new ManagerOverrideDialog(Api);
        var result = await dialog.ShowDialogAsync(FindForm()!);
        return result == DialogResult.OK;
    }

    private async Task TenderAsync()
    {
        if (_cart.Count == 0)
        {
            _statusLabel.Text = "Cart is empty.";
            return;
        }

        var subtotal = _cart.Sum(c => c.LineTotal);
        using var tenderDialog = new TenderDialog(subtotal);
        if (await tenderDialog.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            var request = new SaleCreateDto(
                Session.Channel,
                tenderDialog.SelectedTenderType,
                tenderDialog.AmountTendered,
                0,
                _cart.Select(c => new SaleLineItemRequest(c.ProductId, c.Quantity, c.DiscountAmount)).ToList());

            var receipt = await Api.CreateSaleAsync(request);

            _cart.Clear();
            RebindCart();
            _statusLabel.Text = string.Empty;

            using var receiptForm = new ReceiptForm(receipt);
            await receiptForm.ShowDialogAsync(FindForm()!);
        }
        catch (ApiException ex)
        {
            _statusLabel.Text = $"Sale failed: {ex.Body}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Sale failed: {ex.Message}";
        }
    }

    private void RebindCart()
    {
        GridColumns.Rebind(_cartGrid, _cart);
        _subtotalLabel.Text = $"Subtotal: {_cart.Sum(c => c.LineTotal):C}";
    }
}
