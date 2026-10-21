using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    // The font a tool strip, menu bar or drop-down item's caption is measured and drawn with, resolved the way
    // upstream resolves ToolStripItem.Font: the item's own Font; else its strip's own Font; for a drop-down,
    // else the Font of the item that opened it (and so on up the chain to the menu bar or tool strip); else
    // the default. Every strip renderer used the theme font outright, so a designer's
    // `MenuStrip.Font = Microsoft Sans Serif 12pt` reached the menu bar but not its drop-downs, and
    // `ToolStripButton.Font = ...` reached nothing.
    internal static class StripItemFont
    {
        internal static (SKTypeface Typeface, int Size) Resolve (MenuItem item, Control? strip)
            => ResolveFont (item, strip) is { } font ? From (font) : Default;

        // The font itself, for the metrics that come from it rather than from its typeface (GDI's line height and
        // padding); null where the item falls back to the theme's font.
        internal static Majorsilence.Forms.Drawing.Font? ResolveFont (MenuItem item, Control? strip)
        {
            if ((item as ToolStripItem)?.Font is { } own)
                return own;

            var control = strip;

            while (control is not null) {
                // A menu bar already knows its upstream font (own font, or the app's default menu font).
                if (control is Menu menu)
                    return menu.UpstreamFont;

                if (control.HasOwnFont)
                    return control.Font;

                if (control is MenuDropDown drop_down) {
                    var opener = drop_down.DropDownOwnerItem;

                    if ((opener as ToolStripItem)?.Font is { } opener_font)
                        return opener_font;

                    control = opener?.OwnerControl;
                    continue;
                }

                break;
            }

            return null;
        }

        private static (SKTypeface, int) Default => (Theme.UIFont, Theme.FontSize);

        private static (SKTypeface, int) From (Majorsilence.Forms.Drawing.Font font)
            => (TypefaceCache.Resolve (font), (int) System.Math.Round (font.PixelSize));
    }
}
