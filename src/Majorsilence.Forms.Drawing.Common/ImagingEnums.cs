// Data-only enums completed from upstream System.Drawing.Common as Phase 2 of docs/gdi-gap-plan.md.
//
// These carry no behavior: they exist so migrated code that references them compiles, and so the
// numeric values round-trip. The values are generated from the real assembly rather than transcribed,
// because designer-serialized and .resx code persists them as raw integers -- a wrong number here is a
// silent data-corruption bug, not a compile error.
//
// Members whose behavior is not yet honored by the rendering path are still declared: per the stub
// policy in COMPATIBILITY_MATRIX.md, migrated code should compile and run, and an enum value that is
// merely stored is exactly that policy applied to data.

using System;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>Specifies the two color modes available for color adjustment (32-bit or 64-bit ARGB). Matches <c>System.Drawing.Imaging.ColorMode</c>, including its numeric values.</summary>
    public enum ColorMode
    {
        /// <summary>Argb 32 mode.</summary>
        Argb32Mode = 0,
        /// <summary>Argb 64 mode.</summary>
        Argb64Mode = 1,
    }

    /// <summary>Specifies which CMYK color channel to isolate. Matches <c>System.Drawing.Imaging.ColorChannelFlag</c>, including its numeric values.</summary>
    public enum ColorChannelFlag
    {
        /// <summary>Color channel c.</summary>
        ColorChannelC = 0,
        /// <summary>Color channel m.</summary>
        ColorChannelM = 1,
        /// <summary>Color channel y.</summary>
        ColorChannelY = 2,
        /// <summary>Color channel k.</summary>
        ColorChannelK = 3,
        /// <summary>Color channel last.</summary>
        ColorChannelLast = 4,
    }

    /// <summary>Specifies the type of a color map. Matches <c>System.Drawing.Imaging.ColorMapType</c>, including its numeric values.</summary>
    public enum ColorMapType
    {
        /// <summary>Default.</summary>
        Default = 0,
        /// <summary>Brush.</summary>
        Brush = 1,
    }

    /// <summary>Specifies the kind of color data held in a color palette. Matches <c>System.Drawing.Imaging.PaletteFlags</c>, including its numeric values.</summary>
    [Flags]
    public enum PaletteFlags
    {
        /// <summary>Has alpha.</summary>
        HasAlpha = 1,
        /// <summary>Gray scale.</summary>
        GrayScale = 2,
        /// <summary>Halftone.</summary>
        Halftone = 4,
    }

    /// <summary>Specifies the attributes of the pixel data held by an <see cref="Image"/>. Matches <c>System.Drawing.Imaging.ImageFlags</c>, including its numeric values.</summary>
    [Flags]
    public enum ImageFlags
    {
        /// <summary>None.</summary>
        None = 0,
        /// <summary>Scalable.</summary>
        Scalable = 1,
        /// <summary>Has alpha.</summary>
        HasAlpha = 2,
        /// <summary>Has translucent.</summary>
        HasTranslucent = 4,
        /// <summary>Partially scalable.</summary>
        PartiallyScalable = 8,
        /// <summary>Color space rgb.</summary>
        ColorSpaceRgb = 0x10,
        /// <summary>Color space cmyk.</summary>
        ColorSpaceCmyk = 0x20,
        /// <summary>Color space gray.</summary>
        ColorSpaceGray = 0x40,
        /// <summary>Color space ycbcr.</summary>
        ColorSpaceYcbcr = 0x80,
        /// <summary>Color space ycck.</summary>
        ColorSpaceYcck = 0x100,
        /// <summary>Has real dpi.</summary>
        HasRealDpi = 0x1000,
        /// <summary>Has real pixel size.</summary>
        HasRealPixelSize = 0x2000,
        /// <summary>Read only.</summary>
        ReadOnly = 0x10000,
        /// <summary>Caching.</summary>
        Caching = 0x20000,
    }

    /// <summary>Specifies the capabilities and origin of an image codec. Matches <c>System.Drawing.Imaging.ImageCodecFlags</c>, including its numeric values.</summary>
    [Flags]
    public enum ImageCodecFlags
    {
        /// <summary>Encoder.</summary>
        Encoder = 1,
        /// <summary>Decoder.</summary>
        Decoder = 2,
        /// <summary>Support bitmap.</summary>
        SupportBitmap = 4,
        /// <summary>Support vector.</summary>
        SupportVector = 8,
        /// <summary>Seekable encode.</summary>
        SeekableEncode = 0x10,
        /// <summary>Blocking decode.</summary>
        BlockingDecode = 0x20,
        /// <summary>Builtin.</summary>
        Builtin = 0x10000,
        /// <summary>System.</summary>
        System = 0x20000,
        /// <summary>User.</summary>
        User = 0x40000,
    }

    /// <summary>Specifies a value that can be passed to an image encoder parameter. Matches <c>System.Drawing.Imaging.EncoderValue</c>, including its numeric values.</summary>
    public enum EncoderValue
    {
        /// <summary>Color type cmyk.</summary>
        ColorTypeCMYK = 0,
        /// <summary>Color type ycck.</summary>
        ColorTypeYCCK = 1,
        /// <summary>Compression lzw.</summary>
        CompressionLZW = 2,
        /// <summary>Compression ccit t 3.</summary>
        CompressionCCITT3 = 3,
        /// <summary>Compression ccit t 4.</summary>
        CompressionCCITT4 = 4,
        /// <summary>Compression rle.</summary>
        CompressionRle = 5,
        /// <summary>Compression none.</summary>
        CompressionNone = 6,
        /// <summary>Scan method interlaced.</summary>
        ScanMethodInterlaced = 7,
        /// <summary>Scan method non interlaced.</summary>
        ScanMethodNonInterlaced = 8,
        /// <summary>Version gif 87.</summary>
        VersionGif87 = 9,
        /// <summary>Version gif 89.</summary>
        VersionGif89 = 10,
        /// <summary>Render progressive.</summary>
        RenderProgressive = 11,
        /// <summary>Render non progressive.</summary>
        RenderNonProgressive = 12,
        /// <summary>Transform rotate 90.</summary>
        TransformRotate90 = 13,
        /// <summary>Transform rotate 180.</summary>
        TransformRotate180 = 14,
        /// <summary>Transform rotate 270.</summary>
        TransformRotate270 = 15,
        /// <summary>Transform flip horizontal.</summary>
        TransformFlipHorizontal = 16,
        /// <summary>Transform flip vertical.</summary>
        TransformFlipVertical = 17,
        /// <summary>Multi frame.</summary>
        MultiFrame = 18,
        /// <summary>Last frame.</summary>
        LastFrame = 19,
        /// <summary>Flush.</summary>
        Flush = 20,
        /// <summary>Frame dimension time.</summary>
        FrameDimensionTime = 21,
        /// <summary>Frame dimension resolution.</summary>
        FrameDimensionResolution = 22,
        /// <summary>Frame dimension page.</summary>
        FrameDimensionPage = 23,
    }

    /// <summary>Specifies the data type of an encoder parameter's value. Matches <c>System.Drawing.Imaging.EncoderParameterValueType</c>, including its numeric values.</summary>
    public enum EncoderParameterValueType
    {
        /// <summary>Value type byte.</summary>
        ValueTypeByte = 1,
        /// <summary>Value type ascii.</summary>
        ValueTypeAscii = 2,
        /// <summary>Value type short.</summary>
        ValueTypeShort = 3,
        /// <summary>Value type long.</summary>
        ValueTypeLong = 4,
        /// <summary>Value type rational.</summary>
        ValueTypeRational = 5,
        /// <summary>Value type long range.</summary>
        ValueTypeLongRange = 6,
        /// <summary>Value type undefined.</summary>
        ValueTypeUndefined = 7,
        /// <summary>Value type rational range.</summary>
        ValueTypeRationalRange = 8,
        /// <summary>Value type pointer.</summary>
        ValueTypePointer = 9,
    }
}
