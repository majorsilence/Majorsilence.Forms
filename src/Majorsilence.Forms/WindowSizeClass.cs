namespace Majorsilence.Forms
{
    /// <summary>
    /// A coarse bucket for a window's width, so a layout can switch between a phone-style single column
    /// and a wider multi-pane arrangement without hard-coding pixel breakpoints. The cut-offs follow the
    /// Material window-size classes and are in logical pixels.
    /// </summary>
    public enum WindowSizeClass
    {
        /// <summary>Narrower than 600 logical pixels: a phone in portrait. Show one screen at a time.</summary>
        Compact,

        /// <summary>600 up to (not including) 840 logical pixels: a tablet in portrait or a phone in landscape.</summary>
        Medium,

        /// <summary>840 logical pixels or wider: a tablet in landscape or a desktop window. Room for list and detail side by side.</summary>
        Expanded,
    }
}
