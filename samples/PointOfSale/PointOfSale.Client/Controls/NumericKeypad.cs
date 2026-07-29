using Majorsilence.Forms;

namespace PointOfSale.Client.Controls;

/// <summary>
/// A large-button on-screen numeric keypad for touchscreen POS terminals. The framework has no
/// built-in touch keypad, so this is example-local code; it only reports key presses — callers own
/// whatever they're feeding (a masked PIN buffer, a quantity field, a cash-tendered amount, etc.).
/// </summary>
public class NumericKeypad : UserControl
{
    public event Action<char>? DigitPressed;
    public event Action? BackspacePressed;
    public event Action? ClearPressed;

    private const int ButtonSize = 84;
    private const int Gap = 10;
    private const int DigitFontSize = 32;

    public NumericKeypad()
    {
        Width = 3 * ButtonSize + 2 * Gap;
        Height = 4 * ButtonSize + 3 * Gap;

        AddDigitButton("7", 0, 0);
        AddDigitButton("8", 1, 0);
        AddDigitButton("9", 2, 0);
        AddDigitButton("4", 0, 1);
        AddDigitButton("5", 1, 1);
        AddDigitButton("6", 2, 1);
        AddDigitButton("1", 0, 2);
        AddDigitButton("2", 1, 2);
        AddDigitButton("3", 2, 2);

        AddActionButton("C", 0, 3, () => ClearPressed?.Invoke());
        AddDigitButton("0", 1, 3);
        AddActionButton("⌫", 2, 3, () => BackspacePressed?.Invoke());
    }

    private void AddDigitButton(string digit, int col, int row)
    {
        var button = new Button
        {
            Text = digit,
            Left = col * (ButtonSize + Gap),
            Top = row * (ButtonSize + Gap),
            Width = ButtonSize,
            Height = ButtonSize,
        };
        PosStyle.Tile(button, Theme.ControlMidHighColor, DigitFontSize);
        button.Click += (_, _) => DigitPressed?.Invoke(digit[0]);
        Controls.Add(button);
    }

    private void AddActionButton(string text, int col, int row, Action action)
    {
        var button = new Button
        {
            Text = text,
            Left = col * (ButtonSize + Gap),
            Top = row * (ButtonSize + Gap),
            Width = ButtonSize,
            Height = ButtonSize,
        };
        PosStyle.Tile(button, Theme.ControlHighColor, DigitFontSize);
        button.Click += (_, _) => action();
        Controls.Add(button);
    }
}
