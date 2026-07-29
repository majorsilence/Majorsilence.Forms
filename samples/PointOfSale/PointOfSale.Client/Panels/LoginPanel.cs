using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

/// <summary>Shown before any session exists, so it takes only the ApiClient — never a SessionState.</summary>
public class LoginPanel : Panel
{
    private readonly ApiClient _api;
    private readonly Label _pinDisplay;
    private readonly Label _statusLabel;
    private readonly Button _loginButton;
    private readonly SaleChannel _channel;
    private string _pin = string.Empty;

    public event Action<SessionState>? LoggedIn;

    public LoginPanel(ApiClient api, SaleChannel channel)
    {
        _api = api;
        _channel = channel;

        AutoScroll = true;

        // Every element below is positioned from the actual bottom edge of the one above it
        // (Top + Height + ~14px gap) — NumericKeypad is 272x366 (3x4 84px tiles + 10px gaps),
        // so eyeballing offsets here is what caused earlier rounds of overlap bugs.
        var title = Controls.Add(new Label
        {
            Text = "Point of Sale — Sign In",
            Left = 50,
            Top = 50,
            Width = 600,
        });
        PosStyle.Heading(title); // Height = 54, bottom = 104

        Controls.Add(new Label { Text = "Enter your PIN:", Left = 50, Top = 120, Width = 280, Height = 36 }); // bottom = 156

        _pinDisplay = Controls.Add(new Label
        {
            Text = string.Empty,
            Left = 50,
            Top = 170,
            Width = 272,
            Height = 48,
        }); // bottom = 218
        PosStyle.Display(_pinDisplay);

        var keypad = Controls.Add(new NumericKeypad { Left = 50, Top = 232 }); // bottom = 232 + 366 = 598
        keypad.DigitPressed += digit =>
        {
            if (_pin.Length < 8)
                _pin += digit;
            RefreshPinDisplay();
        };
        keypad.BackspacePressed += () =>
        {
            if (_pin.Length > 0)
                _pin = _pin[..^1];
            RefreshPinDisplay();
        };
        keypad.ClearPressed += () =>
        {
            _pin = string.Empty;
            RefreshPinDisplay();
        };

        _loginButton = Controls.Add(new Button { Text = "Log In", Left = 50, Top = 612, Width = 272, Height = 54 }); // bottom = 666
        PosStyle.PrimaryButton(_loginButton);
        _loginButton.Click += async (_, _) => await AttemptLoginAsync();

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 50, Top = 680, Width = 420, Height = 36 });
    }

    private void RefreshPinDisplay() => _pinDisplay.Text = new string('•', _pin.Length);

    private async Task AttemptLoginAsync()
    {
        if (_pin.Length == 0)
            return;

        _loginButton.Enabled = false;
        _statusLabel.Text = "Signing in...";

        try
        {
            var response = await _api.LoginAsync(_pin);
            _api.SetToken(response.Token);

            var session = new SessionState
            {
                UserId = response.UserId,
                DisplayName = response.DisplayName,
                Role = response.Role,
                Token = response.Token,
                Channel = _channel,
            };

            _pin = string.Empty;
            RefreshPinDisplay();
            _statusLabel.Text = string.Empty;
            LoggedIn?.Invoke(session);
        }
        catch (ApiException)
        {
            _statusLabel.Text = "Incorrect PIN. Please try again.";
            _pin = string.Empty;
            RefreshPinDisplay();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not reach the server: {ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
        }
    }
}
