using Majorsilence.Forms;
using PointOfSale.Client.Panels;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client;

public partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly ApiClient _api;
    private readonly Dictionary<string, Panel> _panelCache = new();
    private Panel? _currentPanel;
    private SessionState? _session;
    private bool _isFullScreen;
    private FormBorderStyle _restoreBorderStyle;

    public MainForm(AppSettings settings, ApiClient api)
    {
        _settings = settings;
        _api = api;

        InitializeComponent();

        // A POS terminal should fill whatever screen it's running on rather than open at a
        // fixed design-time size — Maximized on startup, with F11 for a true borderless
        // fullscreen kiosk mode on top of that (KeyPreview so the Form sees F11 even when a
        // child control — a TextBox, a keypad Button — currently has focus).
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11)
                ToggleFullScreen();
        };

        if (settings.Mode == TerminalMode.Kiosk)
            Shown += MainForm_KioskAutoLogin;
        else
            ShowLoginPanel(SaleChannel.MannedCheckout);
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            FormBorderStyle = _restoreBorderStyle;
        }
        else
        {
            _restoreBorderStyle = FormBorderStyle;
            FormBorderStyle = FormBorderStyle.None;
        }

        WindowState = FormWindowState.Maximized;
        _isFullScreen = !_isFullScreen;
    }

    private async void MainForm_KioskAutoLogin(object? sender, EventArgs e)
    {
        statusConnectionPanel.Text = "Connecting...";

        try
        {
            var response = await _api.LoginAsync(_settings.KioskPin);
            _api.SetToken(response.Token);

            _session = new SessionState
            {
                UserId = response.UserId,
                DisplayName = response.DisplayName,
                Role = response.Role,
                Token = response.Token,
                Channel = SaleChannel.SelfCheckout,
            };

            statusConnectionPanel.Text = string.Empty;
            EnterMainScreen();
        }
        catch (Exception ex)
        {
            statusConnectionPanel.Text = "Offline";
            new MessageBoxForm("Self-Checkout Unavailable", $"Could not connect to the store server: {ex.Message}").ShowDialog(this);
        }
    }

    private void ShowLoginPanel(SaleChannel channel)
    {
        nav.Visible = false;
        statusUserPanel.Text = "Not signed in";

        var login = new LoginPanel(_api, channel) { Dock = DockStyle.Fill };
        login.LoggedIn += session =>
        {
            _session = session;
            Controls.Remove(login);
            EnterMainScreen();
        };

        SwapContent(login);
    }

    private void EnterMainScreen()
    {
        if (_session is null)
            return;

        statusUserPanel.Text = $"{_session.DisplayName} ({_session.Role})";

        BuildNavItemsForRole();

        if (_settings.Mode == TerminalMode.Kiosk)
            nav.Visible = false; // self-checkout never exposes back-office navigation
        else
            nav.Visible = true;

        SelectPanel("Checkout");
    }

    private void BuildNavItemsForRole()
    {
        nav.Items.Clear();
        _panelCache.Clear();

        AddNavItem("Checkout");

        if (_session!.IsManager)
        {
            AddNavItem("Products");
            AddNavItem("Categories");
            AddNavItem("Inventory");
            AddNavItem("Reports");
            AddNavItem("Users");
        }
    }

    // NavigationPaneRenderer draws an item's icon and text both centered on the same bounds (see
    // ControlGallery's NavigationPanePanel, which only ever uses icon-only items) — any real icon
    // would sit on top of the label instead of beside it. A fully transparent 1x1 bitmap gives us
    // clean text-only nav items instead.
    private static readonly SkiaSharp.SKBitmap TransparentPlaceholder = new(1, 1);

    private void AddNavItem(string key)
    {
        var item = nav.Items.Add(new NavigationPaneItem(TransparentPlaceholder, key));
        item.Tag = key;
    }

    private void Nav_SelectedItemChanged(object? sender, EventArgs e)
    {
        var key = nav.SelectedItem?.Tag as string;
        if (key is not null)
            SelectPanel(key);
    }

    private void SelectPanel(string key)
    {
        if (!_panelCache.TryGetValue(key, out var panel))
        {
            try
            {
                panel = CreatePanel(key);
            }
            catch (Exception ex)
            {
                new MessageBoxForm("Screen failed to load", $"{key}: {ex}").ShowDialog(this);
                return;
            }

            if (panel is null)
                return;

            _panelCache[key] = panel;
        }

        SwapContent(panel);
    }

    private void SwapContent(Panel panel)
    {
        if (_currentPanel is not null)
        {
            Controls.Remove(_currentPanel);
            if (_currentPanel is BasePanel bp)
                bp.UnloadPanel();
        }

        _currentPanel = panel;
        panel.Dock = DockStyle.Fill;
        Controls.Insert(0, panel);
        // Freshly-constructed panels don't paint on their own until invalidated (see ControlGallery/MainForm.cs).
        panel.Invalidate();

        if (panel is BasePanel bp2)
            bp2.LoadPanel();
    }

    private Panel? CreatePanel(string key)
    {
        if (_session is null)
            return null;

        return key switch
        {
            "Checkout" => new CheckoutPanel(_api, _session),
            "Products" => new ProductsPanel(_api, _session),
            "Categories" => new CategoriesPanel(_api, _session),
            "Inventory" => new InventoryPanel(_api, _session),
            "Reports" => new ReportsPanel(_api, _session),
            "Users" => new UsersPanel(_api, _session),
            _ => null,
        };
    }
}
