namespace Majorsilence.Forms
{
    /// <summary>
    /// Specifies how an image is positioned within a control.
    /// </summary>
    public enum ImageLayout
    {
        /// <summary>The image is left-aligned at the top of the control.</summary>
        None,
        /// <summary>The image is tiled across the control's surface.</summary>
        Tile,
        /// <summary>The image is centered within the control.</summary>
        Center,
        /// <summary>The image is stretched to fill the control.</summary>
        Stretch,
        /// <summary>The image is enlarged within the control, preserving aspect ratio.</summary>
        Zoom
    }
}
