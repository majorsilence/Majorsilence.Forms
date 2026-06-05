using System;

namespace Modern.Forms
{
    /// <summary>
    /// Specifies control style and behavior flags. Provided for WinForms source compatibility;
    /// Modern.Forms renders every control into its own double-buffered SkiaSharp surface, so most
    /// of these flags are informational and do not change rendering behavior.
    /// </summary>
    [Flags]
    public enum ControlStyles
    {
        /// <summary>The control is a container-like control.</summary>
        ContainerControl = 0x00000001,
        /// <summary>The control paints itself rather than the OS doing so.</summary>
        UserPaint = 0x00000002,
        /// <summary>The control is drawn opaque and the background is not painted.</summary>
        Opaque = 0x00000004,
        /// <summary>The control is redrawn when it is resized.</summary>
        ResizeRedraw = 0x00000010,
        /// <summary>The control has a fixed width.</summary>
        FixedWidth = 0x00000020,
        /// <summary>The control has a fixed height.</summary>
        FixedHeight = 0x00000040,
        /// <summary>The control implements standard <see cref="Control.Click"/> behavior.</summary>
        StandardClick = 0x00000100,
        /// <summary>The control can receive focus.</summary>
        Selectable = 0x00000200,
        /// <summary>The control notifies for mouse-over events.</summary>
        UserMouse = 0x00000400,
        /// <summary>The control's background is filled with the supports-transparent-backcolor color.</summary>
        SupportsTransparentBackColor = 0x00000800,
        /// <summary>The control implements standard double-click behavior.</summary>
        StandardDoubleClick = 0x00001000,
        /// <summary>The control ignores the window message to avoid flicker.</summary>
        AllPaintingInWmPaint = 0x00002000,
        /// <summary>The control caches the image for double buffering.</summary>
        CacheText = 0x00004000,
        /// <summary>The control is double-buffered.</summary>
        DoubleBuffer = 0x00010000,
        /// <summary>The control uses optimized double buffering.</summary>
        OptimizedDoubleBuffer = 0x00020000,
        /// <summary>Padding is included in the GetPreferredSize result.</summary>
        UseTextForAccessibility = 0x00040000
    }
}
