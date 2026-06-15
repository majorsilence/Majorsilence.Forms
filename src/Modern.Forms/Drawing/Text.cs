namespace Modern.Drawing.Text
{
    /// <summary>
    /// Specifies the quality of text rendering. Cross-platform replacement for
    /// <c>System.Drawing.Text.TextRenderingHint</c>.
    /// </summary>
    public enum TextRenderingHint
    {
        /// <summary>Each character is drawn using its glyph bitmap and the system default rendering hint.</summary>
        SystemDefault = 0,
        /// <summary>Each character is drawn using its glyph bitmap. Hinting is used to improve glyph appearance.</summary>
        SingleBitPerPixelGridFit = 1,
        /// <summary>Each character is drawn using its glyph bitmap. Hinting is not used.</summary>
        SingleBitPerPixel = 2,
        /// <summary>Each character is drawn using its antialiased glyph bitmap with hinting.</summary>
        AntiAliasGridFit = 3,
        /// <summary>Each character is drawn using its antialiased glyph bitmap without hinting.</summary>
        AntiAlias = 4,
        /// <summary>Each character is drawn using its ClearType glyph bitmap with hinting.</summary>
        ClearTypeGridFit = 5
    }

    /// <summary>Specifies how the hotkey prefix is displayed. Replacement for System.Drawing.Text.HotkeyPrefix.</summary>
    public enum HotkeyPrefix
    {
        /// <summary>No hotkey prefix processing.</summary>
        None = 0,
        /// <summary>Display the hotkey prefix.</summary>
        Show = 1,
        /// <summary>Hide the hotkey prefix.</summary>
        Hide = 2
    }
}
