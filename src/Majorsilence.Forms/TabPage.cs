namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TabPage control.
    /// </summary>
    public partial class TabPage : Panel
    {
        private int image_index = -1;
        private string image_key = string.Empty;

        /// <summary>
        /// Initializes a new instance of the TabPage class.
        /// </summary>
        public TabPage ()
        {
            Dock = DockStyle.Fill;
            TabStripItem = new TabStripItem ();
        }

        /// <summary>
        /// Initializes a new instance of the TabPage class with the specified text.
        /// </summary>
        public TabPage (string text) : this ()
        {
            TabStripItem.Text = text;
        }

        // The TabStripItem that accompanies this TabPage.
        internal TabStripItem TabStripItem { get; }

        /// <inheritdoc/>
        public override string Text {
            get => TabStripItem.Text;
            set => TabStripItem.Text = value ?? string.Empty;
        }

        /// <summary>Gets or sets the ToolTip text for this tab page.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the index into the ImageList of the image to display on this tab.</summary>
        public int ImageIndex {
            get => image_index;
            set {
                if (image_index == value)
                    return;

                image_index = value;

                // Re-resolve through the owning control's ImageList. Both of these were bare stores
                // nothing read, so a page built the WinForms way -- an ImageList on the TabControl plus
                // an index per page -- showed no icon at all and its tab measured text only (LAY-14).
                (Parent as TabControl)?.UpdateTabImage (this);
            }
        }

        /// <summary>Gets or sets the key into the ImageList of the image to display on this tab.</summary>
        public string ImageKey {
            get => image_key;
            set {
                var resolved = value ?? string.Empty;

                if (image_key == resolved)
                    return;

                image_key = resolved;

                (Parent as TabControl)?.UpdateTabImage (this);
            }
        }

        /// <summary>Gets or sets whether the tab page is enabled.</summary>
        /// <remarks>
        /// The <c>new</c> is upstream's, and upstream uses it only to re-declare the designer
        /// attributes: the behaviour is <see cref="Control.Enabled"/>. Ours used to be a <c>new</c>
        /// auto-property with its own backing field, so <c>page.Enabled = false</c> stored a value
        /// nothing read -- the page's children stayed interactive, and the same state read through a
        /// <see cref="Control"/> reference gave the opposite answer (LAY-12).
        /// </remarks>
        public new bool Enabled {
            get => base.Enabled;
            set => base.Enabled = value;
        }

        /// <summary>Gets or sets whether the tab page uses the visual style of the tab control.</summary>
        /// <remarks>Under the upstream look (an app that chose its font) a page with it set paints the Windows 11 tab
        /// body (#F9F9F9) over its BackColor, as upstream's TabPage.OnPaintBackground does for a Normal tab control;
        /// its children take a BackColor of its own as their ambient colour, and the body otherwise (see
        /// <see cref="BackColor"/>). It was stored and ignored, so a page whose designer BackColor was navy painted
        /// navy all over.</remarks>
        public new bool UseVisualStyleBackColor {
            get => use_visual_style_back_color;
            set {
                use_visual_style_back_color = value;
                Invalidate ();
            }
        }

        private bool use_visual_style_back_color;

        // The Windows 11 theme's tab body, as measured from upstream.
        internal static readonly SkiaSharp.SKColor ThemedBodyColor = new SkiaSharp.SKColor (0xF9, 0xF9, 0xF9);

        private bool PaintsThemedBody
            => use_visual_style_back_color && ControlPaint.UsesUpstreamGlyphs && BackgroundImage is null
               && Parent is TabControl { Appearance: TabAppearance.Normal };

        // Upstream's TabPage.BackColor is Color.Transparent while it paints the themed body and its own colour is the
        // default (unset, or SystemColors.Control), so the labels, check boxes and group boxes that inherit it show the
        // body through. Here they inherited Control and stood on the body in grey boxes.
        private bool BodyIsAmbient
            => PaintsThemedBody && (Style.BackgroundColor is not { } own || own == SystemColors.Control.ToSKColor ());

        /// <inheritdoc/>
        /// <remarks>Color.Transparent while the page paints the themed tab body over a default BackColor, as upstream's;
        /// its children then inherit the body.</remarks>
        public override System.Drawing.Color BackColor {
            get => BodyIsAmbient ? System.Drawing.Color.Transparent : base.BackColor;
            set => base.BackColor = value;
        }

        internal override SkiaSharp.SKColor? UpstreamAmbientBackColor => BodyIsAmbient ? ThemedBodyColor : null;

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            if (!PaintsThemedBody) {
                base.OnPaintBackground (e);
                return;
            }

            using var device = e.DeviceSpace ();
            e.Canvas.Clear (ThemedBodyColor);
        }
    }
}
