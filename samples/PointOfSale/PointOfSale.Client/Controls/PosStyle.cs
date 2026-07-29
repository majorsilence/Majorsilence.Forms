using Majorsilence.Forms;
using SkiaSharp;

namespace PointOfSale.Client.Controls;

/// <summary>
/// Shared per-control style overrides layered on top of BuiltInTheme.PointOfSale (navy canvas,
/// amber accent for selection/focus, green accent2 reserved here for "go" actions). Individual
/// controls each own a mutable Style instance (Control.Style), so these just set properties on
/// whatever's passed in rather than registering a new theme.
///
/// Button.StyleHover is a SEPARATE ControlStyle chained from the static DefaultStyleHover (not
/// from the instance's own Style) — it only overrides BackgroundColor/Border.Color/ForegroundColor,
/// never FontSize. So without also setting StyleHover here, every custom-sized/colored button
/// snaps back to Theme.FontSize and the stock amber hover color the moment the pointer enters it.
/// </summary>
public static class PosStyle
{
    /// <summary>Bold section/screen heading, e.g. "Checkout", "Point of Sale — Sign In".</summary>
    public static void Heading(Control control, int fontSize = 34)
    {
        control.Style.Font = Theme.UIFontBold;
        control.Style.FontSize = fontSize;
        // Label.DefaultSize is a fixed 23px tall, and changing Style.FontSize never triggers a
        // re-layout (only Text/Multiline/etc. do) — without an explicit Height a heading this
        // large just gets clipped to the top few pixels of each glyph.
        control.Height = fontSize + 20;
    }

    /// <summary>
    /// The signature "register display" treatment for PIN/quantity/subtotal readouts: a
    /// blue-tinted LCD-style panel with bold amber digits, evoking real till hardware rather
    /// than a plain bordered label.
    /// </summary>
    public static void Display(Control control, int fontSize = 30)
    {
        control.Style.BackgroundColor = Theme.ControlMidColor;
        control.Style.ForegroundColor = Theme.AccentColor;
        control.Style.Font = Theme.UIFontBold;
        control.Style.FontSize = fontSize;
        control.Style.Border.Width = 2;
        control.Style.Border.Color = Theme.BorderHighColor;
        control.Style.Border.Radius = 6;
    }

    /// <summary>Primary "go" action — Log In, Tender/Pay, Add to Cart. Filled green.</summary>
    public static void PrimaryButton(Control control, int fontSize = 26) =>
        ApplyButtonStyle(control, Theme.AccentColor2, SKColors.White, fontSize);

    /// <summary>Manager-gated / destructive action — Remove Line, Void. Filled amber-orange.</summary>
    public static void CautionButton(Control control, int fontSize = 24) =>
        ApplyButtonStyle(control, Theme.ControlHighlightMidColor, SKColors.White, fontSize);

    /// <summary>
    /// A plain/neutral action — Search, Edit, Cancel. Same bold weight and size as the accent
    /// buttons (a plain Button's default text is regular-weight Theme.FontSize, which reads
    /// noticeably smaller next to a bold PrimaryButton/CautionButton right beside it) but a
    /// neutral fill so it doesn't compete with the real "go"/"caution" actions.
    /// </summary>
    public static void SecondaryButton(Control control, int fontSize = 24) =>
        ApplyButtonStyle(control, Theme.ControlHighColor, Theme.ForegroundColor, fontSize);

    /// <summary>Bumps a text-heavy list/box control's font past the base body size — used for
    /// search results, where the default body size reads small against the bold CTAs around it.</summary>
    public static void LargeText(Control control, int fontSize = 24)
    {
        control.Style.FontSize = fontSize;
    }

    /// <summary>A keypad/tile-style button — same background normal and hovered except brightness.</summary>
    public static void Tile(Control control, SKColor background, int fontSize)
    {
        control.Style.Font = Theme.UIFontBold;
        control.Style.FontSize = fontSize;
        control.Style.BackgroundColor = background;
        control.Style.Border.Radius = 8;

        control.StyleHover.Font = Theme.UIFontBold;
        control.StyleHover.FontSize = fontSize;
        control.StyleHover.BackgroundColor = Lighten(background, 24);
        control.StyleHover.ForegroundColor = Theme.ForegroundColor;
        control.StyleHover.Border.Radius = 8;
    }

    private static void ApplyButtonStyle(Control control, SKColor background, SKColor foreground, int fontSize)
    {
        control.Style.BackgroundColor = background;
        control.Style.ForegroundColor = foreground;
        control.Style.Font = Theme.UIFontBold;
        control.Style.FontSize = fontSize;
        control.Style.Border.Radius = 6;

        control.StyleHover.BackgroundColor = Lighten(background, 24);
        control.StyleHover.ForegroundColor = foreground;
        control.StyleHover.Font = Theme.UIFontBold;
        control.StyleHover.FontSize = fontSize;
        control.StyleHover.Border.Radius = 6;
    }

    private static SKColor Lighten(SKColor color, byte amount) => new(
        (byte)Math.Min(255, color.Red + amount),
        (byte)Math.Min(255, color.Green + amount),
        (byte)Math.Min(255, color.Blue + amount),
        color.Alpha);
}
