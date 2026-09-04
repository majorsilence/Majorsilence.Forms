using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TabStripItem.
    /// </summary>
    public class TabStripItem : ILayoutable
    {
        // Gap between a tab's image and its text, in logical units. Upstream's native tab control
        // uses 2px of padding between the TCITEM image and the label; the strip's own Padding
        // supplies the outer inset.
        internal const int IMAGE_TEXT_GAP = 4;

        private bool enabled = true;
        private SKBitmap? image;
        private string text;

        /// <summary>
        /// Initializes a new instance of the TabStripItem class.
        /// </summary>
        public TabStripItem (string? text = null)
        {
            this.text = text ?? string.Empty;
        }

        /// <summary>
        /// Gets the current bounding box of the tab.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tab is enabled.
        /// </summary>
        public bool Enabled {
            get => enabled && Parent?.Enabled == true;
            set {
                if (enabled != value) {
                    enabled = value;
                    Parent?.Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the preferred size of the tab.
        /// </summary>
        public Size GetPreferredSize (Size proposedSize)
        {
            // Measure with the strip's ambient effective font (the same resolution the renderer
            // uses), not the theme chrome font -- a strip with a designer-set font must size its
            // tabs to that font.
            var padding = Parent?.LogicalToDeviceUnits (Padding.Horizontal) ?? Padding.Horizontal;
            var font = Parent?.GetEffectiveFont () ?? Theme.UIFont;
            var font_size = Parent is { } strip ? strip.LogicalToDeviceUnits (strip.GetEffectiveFontSize ()) : Theme.FontSize;
            var text_size = (int)Math.Round (TextMeasurer.MeasureText (Text, font, font_size).Width);

            // An imaged tab has to be wider than a text-only one or the icon crowds the label out --
            // upstream widens the tab by the image extent, and ours measured text only (LAY-14).
            var image_extent = ImageExtent ();

            // TabControl.Padding insets every tab on top of the strip's own Padding; it was stored
            // and never read (LAY-15).
            var owner_padding = Parent?.OwnerTabControl?.Padding.X ?? 0;

            return new Size (text_size + padding + image_extent + (2 * ScaleToDevice (owner_padding)), Bounds.Height);
        }

        // The horizontal room the image needs, gap included, in the same (device) units
        // GetPreferredSize measures text and padding in. Zero when there is no image.
        internal int ImageExtent () => image is null ? 0 : ScaleToDevice (ImageSize.Width + IMAGE_TEXT_GAP);

        // The image's logical size. ImageList has already resized every frame to its ImageSize, so the
        // bitmap's own dimensions are the logical extent the tab has to reserve.
        internal Size ImageSize => image is null ? Size.Empty : new Size (image.Width, image.Height);

        private int ScaleToDevice (int value) => Parent?.LogicalToDeviceUnits (value) ?? value;

        /// <summary>
        /// Gets or sets the image drawn at the leading edge of the tab.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="TabControl"/> from its <c>ImageList</c> and the page's
        /// <c>ImageIndex</c>/<c>ImageKey</c>; the bitmap is owned by the image list, not by the tab.
        /// </remarks>
        public SKBitmap? Image {
            get => image;
            set {
                if (image == value)
                    return;

                image = value;

                // The tab's preferred width just changed, so the strip has to re-wrap, not merely repaint.
                Parent?.PerformLayout ();
                Parent?.Invalidate ();
            }
        }

        /// <summary>
        /// Gets a value indicating if the tab currently has the mouse hovered over it.
        /// </summary>
        public bool Hovered => Parent?.Tabs.HoveredIndex == Index;

        // Gets the current index in the parent TabStrip, if parented to a TabStrip.
        private int Index => Parent?.Tabs.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets or sets the amount of space to leave between this tab and other elements.
        /// </summary>
        public Padding Margin { get; set; } = Padding.Empty;

        /// <summary>
        /// Gets or sets the amount of space to leave between the text and the border of the tab.
        /// </summary>
        public Padding Padding { get; set; } = new Padding (14, 0, 14, 0);

        /// <summary>
        /// Gets the TabStrip this tab is currently a part of.
        /// </summary>
        public TabStrip? Parent { get; internal set; }

        /// <summary>
        /// Gets a value indicating if the tab is currently the selected tab.
        /// </summary>
        public bool Selected => Parent?.SelectedTab == this;

        /// <summary>
        /// Sets the bounding box of the tab. This is internal API and should not be called.
        /// </summary>
        public void SetBounds (int x, int y, int width, int height, BoundsSpecified specified = BoundsSpecified.All)
        {
            Bounds = new Rectangle (x, y, width, height);
        }

        /// <summary>
        /// Gets or sets an object with additional user data about this tab.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the text displayed on the tab.
        /// </summary>
        public string Text {
            get => text;
            set {
                if (text != value) {
                    text = value;
                    Parent?.Invalidate ();
                }
            }
        }
    }
}
