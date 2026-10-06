using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a Menu control.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="ToolStrip"/> so that <see cref="MenuStrip"/> exposes the ToolStrip
    /// member surface real WinForms gives it (MenuStrip : ToolStrip upstream). Everything that makes a
    /// Menu a top-docked menu bar -- the horizontal expand layout, hover-opens-the-next-drop-down, and
    /// the MenuRenderer registration -- is unchanged and still lives here.
    /// </remarks>
    public partial class Menu : ToolStrip
    {
        /// <summary>
        /// Initializes a new instance of the Menu class.
        /// </summary>
        public Menu ()
        {
            Dock = DockStyle.Top;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 28);

        /// <summary>
        /// Gets the collection of menu items contained by this Menu. Re-exposed past
        /// <see cref="ToolStrip"/>'s ToolStripItemCollection facade: MenuRenderer, LayoutItems and
        /// MenuBase's hit-testing all consume this collection, so it must stay the visible one.
        /// </summary>
        public new global::Majorsilence.Forms.MenuItemCollection Items => RootItems;

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        // Part styles for the items (CSS `Menu::item` and `Menu::item:hover`). The hover style
        // layers on the item style, so an item text colour carries into the hovered state.

        /// <summary>The default style of an item: its text colour (the background is the strip itself unless set). CSS: <c>Menu::item</c>.</summary>
        public new static readonly ControlStyle DefaultItemStyle = new ControlStyle (null,
            (style) => { style.ForegroundColor = Theme.ForegroundColor; });

        /// <summary>The default style of a hovered (or open) item. CSS: <c>Menu::item:hover</c>.</summary>
        public new static readonly ControlStyle DefaultItemHoverStyle = new ControlStyle (DefaultItemStyle,
            (style) => style.BackgroundColor = Theme.ControlHighlightLowColor);

        /// <inheritdoc/>
        protected override bool IsTopLevelMenu => true;

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            var visible = Items.Where (i => i.Visible).ToList ();
            var area = LogicalClientRectangle;

            if (UpstreamFont is { } font) {
                // Upstream's MenuStrip is as tall as its tallest item plus its padding (its AutoSize, which
                // defaults on there; this strip's AutoSize defaults off, so it is not consulted), and lays
                // its items out inside that padding.
                var row = visible.Count == 0 ? font.Height + 4 : visible.Max (i => UpstreamItemHeight (i, font));
                var height = row + Padding.Vertical;

                if (Height != height && Dock is DockStyle.Top or DockStyle.Bottom or DockStyle.None)
                    Height = height;

                // The row is laid out at the height just set: the client rectangle read above predates it.
                area = new Rectangle (area.X + Padding.Left, area.Y + Padding.Top,
                    Math.Max (0, area.Width - Padding.Horizontal), row);
            }

            StackLayoutEngine.HorizontalExpand.Layout (area, visible.Cast<ILayoutable> ());

            // ToolStripItemAlignment.Right: a merged MDI child's caption buttons, or a Help menu pinned
            // to the far edge, as upstream lays a MenuStrip out.
            PinTrailing (visible, area, vertical: false);
        }

        // ── Upstream metrics ───────────────────────────────────────────────────────
        //
        // A menu bar draws with the theme's font and roomy padding. Once the app has chosen a font --
        // on this strip, or app-wide with Application.SetDefaultFont, as a ported WinForms app does --
        // it takes upstream's metrics instead: SystemFonts.MenuFont (a ToolStrip does not inherit its
        // form's font), 4px item padding, a 2px item border, GDI's text padding, and a MenuStrip's
        // own (6, 2, 0, 2) padding. Under the theme metrics ReportDesigner's "File" was 49px against
        // WinForms' 37, and its menu bar 4px short.

        /// <summary>The font menu items are laid out with under upstream's metrics, or null for the theme's.</summary>
        internal Majorsilence.Forms.Drawing.Font? UpstreamFont
            => HasOwnFont ? Font
             : SystemFonts.HasDefaultFontOverride ? SystemFonts.MenuFont
             : null;

        /// <summary>The typeface items draw with.</summary>
        internal SkiaSharp.SKTypeface ItemTypeface => UpstreamFont is { } font ? TypefaceCache.Resolve (font) : Theme.UIFont;

        /// <summary>The logical pixel size items draw at.</summary>
        internal int ItemFontSize => UpstreamFont is { } font ? (int) Math.Round (font.PixelSize) : Theme.FontSize;

        // An item's height under upstream's metrics: a line of text, or a merged MDI child's 20px icon,
        // inside its padding and the 2px item border.
        private static int UpstreamItemHeight (MenuItem item, Majorsilence.Forms.Drawing.Font font)
            => item is MdiControlItem ? 24 : font.Height + item.Padding.Vertical + 4;

        /// <inheritdoc/>
        protected override Padding DefaultPadding => UpstreamFont is not null ? new Padding (6, 2, 0, 2) : base.DefaultPadding;

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            Deactivate ();
        }

        /// <inheritdoc/>
        protected override void OnHoverChanged (MenuItem? oldItem, MenuItem? newItem)
        {
            if (IsActivated && newItem != null)
                SelectedItem = newItem;
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
