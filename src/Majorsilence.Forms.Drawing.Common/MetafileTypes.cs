using System;
using System.Drawing;

namespace Majorsilence.Forms.Drawing.Imaging
{
    // The metafile family (docs/gdi-gap-plan.md).
    //
    // EMF and WMF are Windows GDI record-and-replay formats with no Skia equivalent, so recording and
    // playback stay out of scope -- but the *shapes* around them do not need GDI to exist. The
    // headers are plain data, the enums are plain numbers, and code that reads a header, switches on
    // a record type or declares a callback compiles and runs here. Only the recording surface on
    // Metafile itself throws, and it says exactly why.

    /// <summary>The kind of metafile a <see cref="MetafileHeader"/> describes.</summary>
    public enum MetafileType
    {
        /// <summary>Not a recognised metafile.</summary>
        Invalid = 0,
        /// <summary>A Windows metafile.</summary>
        Wmf = 1,
        /// <summary>A placeable Windows metafile.</summary>
        WmfPlaceable = 2,
        /// <summary>An enhanced metafile.</summary>
        Emf = 3,
        /// <summary>An EMF+ metafile holding only EMF+ records.</summary>
        EmfPlusOnly = 4,
        /// <summary>An EMF+ metafile holding both EMF+ and EMF records.</summary>
        EmfPlusDual = 5,
    }

    /// <summary>The kind of records an enhanced metafile is recorded with.</summary>
    public enum EmfType
    {
        /// <summary>EMF records only.</summary>
        EmfOnly = 3,
        /// <summary>EMF+ records only.</summary>
        EmfPlusOnly = 4,
        /// <summary>Both EMF+ and EMF records.</summary>
        EmfPlusDual = 5,
    }

    /// <summary>The unit a metafile's frame rectangle is measured in.</summary>
    public enum MetafileFrameUnit
    {
        /// <summary>Pixels.</summary>
        Pixel = 2,
        /// <summary>Printer's points.</summary>
        Point = 3,
        /// <summary>Inches.</summary>
        Inch = 4,
        /// <summary>Document units, 1/300 inch.</summary>
        Document = 5,
        /// <summary>Millimetres.</summary>
        Millimeter = 6,
        /// <summary>The units GDI itself uses, 0.01 millimetre.</summary>
        GdiCompatible = 7,
    }

    /// <summary>The type of a record inside a metafile.</summary>
    public enum EmfPlusRecordType
    {
        /// <summary>The WmfRecordBase record type.</summary>
        WmfRecordBase = 65536,
        /// <summary>The WmfSetBkColor record type.</summary>
        WmfSetBkColor = 66049,
        /// <summary>The WmfSetBkMode record type.</summary>
        WmfSetBkMode = 65794,
        /// <summary>The WmfSetMapMode record type.</summary>
        WmfSetMapMode = 65795,
        /// <summary>The WmfSetROP2 record type.</summary>
        WmfSetROP2 = 65796,
        /// <summary>The WmfSetRelAbs record type.</summary>
        WmfSetRelAbs = 65797,
        /// <summary>The WmfSetPolyFillMode record type.</summary>
        WmfSetPolyFillMode = 65798,
        /// <summary>The WmfSetStretchBltMode record type.</summary>
        WmfSetStretchBltMode = 65799,
        /// <summary>The WmfSetTextCharExtra record type.</summary>
        WmfSetTextCharExtra = 65800,
        /// <summary>The WmfSetTextColor record type.</summary>
        WmfSetTextColor = 66057,
        /// <summary>The WmfSetTextJustification record type.</summary>
        WmfSetTextJustification = 66058,
        /// <summary>The WmfSetWindowOrg record type.</summary>
        WmfSetWindowOrg = 66059,
        /// <summary>The WmfSetWindowExt record type.</summary>
        WmfSetWindowExt = 66060,
        /// <summary>The WmfSetViewportOrg record type.</summary>
        WmfSetViewportOrg = 66061,
        /// <summary>The WmfSetViewportExt record type.</summary>
        WmfSetViewportExt = 66062,
        /// <summary>The WmfOffsetWindowOrg record type.</summary>
        WmfOffsetWindowOrg = 66063,
        /// <summary>The WmfScaleWindowExt record type.</summary>
        WmfScaleWindowExt = 66576,
        /// <summary>The WmfOffsetViewportOrg record type.</summary>
        WmfOffsetViewportOrg = 66065,
        /// <summary>The WmfScaleViewportExt record type.</summary>
        WmfScaleViewportExt = 66578,
        /// <summary>The WmfLineTo record type.</summary>
        WmfLineTo = 66067,
        /// <summary>The WmfMoveTo record type.</summary>
        WmfMoveTo = 66068,
        /// <summary>The WmfExcludeClipRect record type.</summary>
        WmfExcludeClipRect = 66581,
        /// <summary>The WmfIntersectClipRect record type.</summary>
        WmfIntersectClipRect = 66582,
        /// <summary>The WmfArc record type.</summary>
        WmfArc = 67607,
        /// <summary>The WmfEllipse record type.</summary>
        WmfEllipse = 66584,
        /// <summary>The WmfFloodFill record type.</summary>
        WmfFloodFill = 66585,
        /// <summary>The WmfPie record type.</summary>
        WmfPie = 67610,
        /// <summary>The WmfRectangle record type.</summary>
        WmfRectangle = 66587,
        /// <summary>The WmfRoundRect record type.</summary>
        WmfRoundRect = 67100,
        /// <summary>The WmfPatBlt record type.</summary>
        WmfPatBlt = 67101,
        /// <summary>The WmfSaveDC record type.</summary>
        WmfSaveDC = 65566,
        /// <summary>The WmfSetPixel record type.</summary>
        WmfSetPixel = 66591,
        /// <summary>The WmfOffsetCilpRgn record type.</summary>
        WmfOffsetCilpRgn = 66080,
        /// <summary>The WmfTextOut record type.</summary>
        WmfTextOut = 66849,
        /// <summary>The WmfBitBlt record type.</summary>
        WmfBitBlt = 67874,
        /// <summary>The WmfStretchBlt record type.</summary>
        WmfStretchBlt = 68387,
        /// <summary>The WmfPolygon record type.</summary>
        WmfPolygon = 66340,
        /// <summary>The WmfPolyline record type.</summary>
        WmfPolyline = 66341,
        /// <summary>The WmfEscape record type.</summary>
        WmfEscape = 67110,
        /// <summary>The WmfRestoreDC record type.</summary>
        WmfRestoreDC = 65831,
        /// <summary>The WmfFillRegion record type.</summary>
        WmfFillRegion = 66088,
        /// <summary>The WmfFrameRegion record type.</summary>
        WmfFrameRegion = 66601,
        /// <summary>The WmfInvertRegion record type.</summary>
        WmfInvertRegion = 65834,
        /// <summary>The WmfPaintRegion record type.</summary>
        WmfPaintRegion = 65835,
        /// <summary>The WmfSelectClipRegion record type.</summary>
        WmfSelectClipRegion = 65836,
        /// <summary>The WmfSelectObject record type.</summary>
        WmfSelectObject = 65837,
        /// <summary>The WmfSetTextAlign record type.</summary>
        WmfSetTextAlign = 65838,
        /// <summary>The WmfChord record type.</summary>
        WmfChord = 67632,
        /// <summary>The WmfSetMapperFlags record type.</summary>
        WmfSetMapperFlags = 66097,
        /// <summary>The WmfExtTextOut record type.</summary>
        WmfExtTextOut = 68146,
        /// <summary>The WmfSetDibToDev record type.</summary>
        WmfSetDibToDev = 68915,
        /// <summary>The WmfSelectPalette record type.</summary>
        WmfSelectPalette = 66100,
        /// <summary>The WmfRealizePalette record type.</summary>
        WmfRealizePalette = 65589,
        /// <summary>The WmfAnimatePalette record type.</summary>
        WmfAnimatePalette = 66614,
        /// <summary>The WmfSetPalEntries record type.</summary>
        WmfSetPalEntries = 65591,
        /// <summary>The WmfPolyPolygon record type.</summary>
        WmfPolyPolygon = 66872,
        /// <summary>The WmfResizePalette record type.</summary>
        WmfResizePalette = 65849,
        /// <summary>The WmfDibBitBlt record type.</summary>
        WmfDibBitBlt = 67904,
        /// <summary>The WmfDibStretchBlt record type.</summary>
        WmfDibStretchBlt = 68417,
        /// <summary>The WmfDibCreatePatternBrush record type.</summary>
        WmfDibCreatePatternBrush = 65858,
        /// <summary>The WmfStretchDib record type.</summary>
        WmfStretchDib = 69443,
        /// <summary>The WmfExtFloodFill record type.</summary>
        WmfExtFloodFill = 66888,
        /// <summary>The WmfSetLayout record type.</summary>
        WmfSetLayout = 65865,
        /// <summary>The WmfDeleteObject record type.</summary>
        WmfDeleteObject = 66032,
        /// <summary>The WmfCreatePalette record type.</summary>
        WmfCreatePalette = 65783,
        /// <summary>The WmfCreatePatternBrush record type.</summary>
        WmfCreatePatternBrush = 66041,
        /// <summary>The WmfCreatePenIndirect record type.</summary>
        WmfCreatePenIndirect = 66298,
        /// <summary>The WmfCreateFontIndirect record type.</summary>
        WmfCreateFontIndirect = 66299,
        /// <summary>The WmfCreateBrushIndirect record type.</summary>
        WmfCreateBrushIndirect = 66300,
        /// <summary>The WmfCreateRegion record type.</summary>
        WmfCreateRegion = 67327,
        /// <summary>The EmfHeader record type.</summary>
        EmfHeader = 1,
        /// <summary>The EmfPolyBezier record type.</summary>
        EmfPolyBezier = 2,
        /// <summary>The EmfPolygon record type.</summary>
        EmfPolygon = 3,
        /// <summary>The EmfPolyline record type.</summary>
        EmfPolyline = 4,
        /// <summary>The EmfPolyBezierTo record type.</summary>
        EmfPolyBezierTo = 5,
        /// <summary>The EmfPolyLineTo record type.</summary>
        EmfPolyLineTo = 6,
        /// <summary>The EmfPolyPolyline record type.</summary>
        EmfPolyPolyline = 7,
        /// <summary>The EmfPolyPolygon record type.</summary>
        EmfPolyPolygon = 8,
        /// <summary>The EmfSetWindowExtEx record type.</summary>
        EmfSetWindowExtEx = 9,
        /// <summary>The EmfSetWindowOrgEx record type.</summary>
        EmfSetWindowOrgEx = 10,
        /// <summary>The EmfSetViewportExtEx record type.</summary>
        EmfSetViewportExtEx = 11,
        /// <summary>The EmfSetViewportOrgEx record type.</summary>
        EmfSetViewportOrgEx = 12,
        /// <summary>The EmfSetBrushOrgEx record type.</summary>
        EmfSetBrushOrgEx = 13,
        /// <summary>The EmfEof record type.</summary>
        EmfEof = 14,
        /// <summary>The EmfSetPixelV record type.</summary>
        EmfSetPixelV = 15,
        /// <summary>The EmfSetMapperFlags record type.</summary>
        EmfSetMapperFlags = 16,
        /// <summary>The EmfSetMapMode record type.</summary>
        EmfSetMapMode = 17,
        /// <summary>The EmfSetBkMode record type.</summary>
        EmfSetBkMode = 18,
        /// <summary>The EmfSetPolyFillMode record type.</summary>
        EmfSetPolyFillMode = 19,
        /// <summary>The EmfSetROP2 record type.</summary>
        EmfSetROP2 = 20,
        /// <summary>The EmfSetStretchBltMode record type.</summary>
        EmfSetStretchBltMode = 21,
        /// <summary>The EmfSetTextAlign record type.</summary>
        EmfSetTextAlign = 22,
        /// <summary>The EmfSetColorAdjustment record type.</summary>
        EmfSetColorAdjustment = 23,
        /// <summary>The EmfSetTextColor record type.</summary>
        EmfSetTextColor = 24,
        /// <summary>The EmfSetBkColor record type.</summary>
        EmfSetBkColor = 25,
        /// <summary>The EmfOffsetClipRgn record type.</summary>
        EmfOffsetClipRgn = 26,
        /// <summary>The EmfMoveToEx record type.</summary>
        EmfMoveToEx = 27,
        /// <summary>The EmfSetMetaRgn record type.</summary>
        EmfSetMetaRgn = 28,
        /// <summary>The EmfExcludeClipRect record type.</summary>
        EmfExcludeClipRect = 29,
        /// <summary>The EmfIntersectClipRect record type.</summary>
        EmfIntersectClipRect = 30,
        /// <summary>The EmfScaleViewportExtEx record type.</summary>
        EmfScaleViewportExtEx = 31,
        /// <summary>The EmfScaleWindowExtEx record type.</summary>
        EmfScaleWindowExtEx = 32,
        /// <summary>The EmfSaveDC record type.</summary>
        EmfSaveDC = 33,
        /// <summary>The EmfRestoreDC record type.</summary>
        EmfRestoreDC = 34,
        /// <summary>The EmfSetWorldTransform record type.</summary>
        EmfSetWorldTransform = 35,
        /// <summary>The EmfModifyWorldTransform record type.</summary>
        EmfModifyWorldTransform = 36,
        /// <summary>The EmfSelectObject record type.</summary>
        EmfSelectObject = 37,
        /// <summary>The EmfCreatePen record type.</summary>
        EmfCreatePen = 38,
        /// <summary>The EmfCreateBrushIndirect record type.</summary>
        EmfCreateBrushIndirect = 39,
        /// <summary>The EmfDeleteObject record type.</summary>
        EmfDeleteObject = 40,
        /// <summary>The EmfAngleArc record type.</summary>
        EmfAngleArc = 41,
        /// <summary>The EmfEllipse record type.</summary>
        EmfEllipse = 42,
        /// <summary>The EmfRectangle record type.</summary>
        EmfRectangle = 43,
        /// <summary>The EmfRoundRect record type.</summary>
        EmfRoundRect = 44,
        /// <summary>The EmfRoundArc record type.</summary>
        EmfRoundArc = 45,
        /// <summary>The EmfChord record type.</summary>
        EmfChord = 46,
        /// <summary>The EmfPie record type.</summary>
        EmfPie = 47,
        /// <summary>The EmfSelectPalette record type.</summary>
        EmfSelectPalette = 48,
        /// <summary>The EmfCreatePalette record type.</summary>
        EmfCreatePalette = 49,
        /// <summary>The EmfSetPaletteEntries record type.</summary>
        EmfSetPaletteEntries = 50,
        /// <summary>The EmfResizePalette record type.</summary>
        EmfResizePalette = 51,
        /// <summary>The EmfRealizePalette record type.</summary>
        EmfRealizePalette = 52,
        /// <summary>The EmfExtFloodFill record type.</summary>
        EmfExtFloodFill = 53,
        /// <summary>The EmfLineTo record type.</summary>
        EmfLineTo = 54,
        /// <summary>The EmfArcTo record type.</summary>
        EmfArcTo = 55,
        /// <summary>The EmfPolyDraw record type.</summary>
        EmfPolyDraw = 56,
        /// <summary>The EmfSetArcDirection record type.</summary>
        EmfSetArcDirection = 57,
        /// <summary>The EmfSetMiterLimit record type.</summary>
        EmfSetMiterLimit = 58,
        /// <summary>The EmfBeginPath record type.</summary>
        EmfBeginPath = 59,
        /// <summary>The EmfEndPath record type.</summary>
        EmfEndPath = 60,
        /// <summary>The EmfCloseFigure record type.</summary>
        EmfCloseFigure = 61,
        /// <summary>The EmfFillPath record type.</summary>
        EmfFillPath = 62,
        /// <summary>The EmfStrokeAndFillPath record type.</summary>
        EmfStrokeAndFillPath = 63,
        /// <summary>The EmfStrokePath record type.</summary>
        EmfStrokePath = 64,
        /// <summary>The EmfFlattenPath record type.</summary>
        EmfFlattenPath = 65,
        /// <summary>The EmfWidenPath record type.</summary>
        EmfWidenPath = 66,
        /// <summary>The EmfSelectClipPath record type.</summary>
        EmfSelectClipPath = 67,
        /// <summary>The EmfAbortPath record type.</summary>
        EmfAbortPath = 68,
        /// <summary>The EmfReserved069 record type.</summary>
        EmfReserved069 = 69,
        /// <summary>The EmfGdiComment record type.</summary>
        EmfGdiComment = 70,
        /// <summary>The EmfFillRgn record type.</summary>
        EmfFillRgn = 71,
        /// <summary>The EmfFrameRgn record type.</summary>
        EmfFrameRgn = 72,
        /// <summary>The EmfInvertRgn record type.</summary>
        EmfInvertRgn = 73,
        /// <summary>The EmfPaintRgn record type.</summary>
        EmfPaintRgn = 74,
        /// <summary>The EmfExtSelectClipRgn record type.</summary>
        EmfExtSelectClipRgn = 75,
        /// <summary>The EmfBitBlt record type.</summary>
        EmfBitBlt = 76,
        /// <summary>The EmfStretchBlt record type.</summary>
        EmfStretchBlt = 77,
        /// <summary>The EmfMaskBlt record type.</summary>
        EmfMaskBlt = 78,
        /// <summary>The EmfPlgBlt record type.</summary>
        EmfPlgBlt = 79,
        /// <summary>The EmfSetDIBitsToDevice record type.</summary>
        EmfSetDIBitsToDevice = 80,
        /// <summary>The EmfStretchDIBits record type.</summary>
        EmfStretchDIBits = 81,
        /// <summary>The EmfExtCreateFontIndirect record type.</summary>
        EmfExtCreateFontIndirect = 82,
        /// <summary>The EmfExtTextOutA record type.</summary>
        EmfExtTextOutA = 83,
        /// <summary>The EmfExtTextOutW record type.</summary>
        EmfExtTextOutW = 84,
        /// <summary>The EmfPolyBezier16 record type.</summary>
        EmfPolyBezier16 = 85,
        /// <summary>The EmfPolygon16 record type.</summary>
        EmfPolygon16 = 86,
        /// <summary>The EmfPolyline16 record type.</summary>
        EmfPolyline16 = 87,
        /// <summary>The EmfPolyBezierTo16 record type.</summary>
        EmfPolyBezierTo16 = 88,
        /// <summary>The EmfPolylineTo16 record type.</summary>
        EmfPolylineTo16 = 89,
        /// <summary>The EmfPolyPolyline16 record type.</summary>
        EmfPolyPolyline16 = 90,
        /// <summary>The EmfPolyPolygon16 record type.</summary>
        EmfPolyPolygon16 = 91,
        /// <summary>The EmfPolyDraw16 record type.</summary>
        EmfPolyDraw16 = 92,
        /// <summary>The EmfCreateMonoBrush record type.</summary>
        EmfCreateMonoBrush = 93,
        /// <summary>The EmfCreateDibPatternBrushPt record type.</summary>
        EmfCreateDibPatternBrushPt = 94,
        /// <summary>The EmfExtCreatePen record type.</summary>
        EmfExtCreatePen = 95,
        /// <summary>The EmfPolyTextOutA record type.</summary>
        EmfPolyTextOutA = 96,
        /// <summary>The EmfPolyTextOutW record type.</summary>
        EmfPolyTextOutW = 97,
        /// <summary>The EmfSetIcmMode record type.</summary>
        EmfSetIcmMode = 98,
        /// <summary>The EmfCreateColorSpace record type.</summary>
        EmfCreateColorSpace = 99,
        /// <summary>The EmfSetColorSpace record type.</summary>
        EmfSetColorSpace = 100,
        /// <summary>The EmfDeleteColorSpace record type.</summary>
        EmfDeleteColorSpace = 101,
        /// <summary>The EmfGlsRecord record type.</summary>
        EmfGlsRecord = 102,
        /// <summary>The EmfGlsBoundedRecord record type.</summary>
        EmfGlsBoundedRecord = 103,
        /// <summary>The EmfPixelFormat record type.</summary>
        EmfPixelFormat = 104,
        /// <summary>The EmfDrawEscape record type.</summary>
        EmfDrawEscape = 105,
        /// <summary>The EmfExtEscape record type.</summary>
        EmfExtEscape = 106,
        /// <summary>The EmfStartDoc record type.</summary>
        EmfStartDoc = 107,
        /// <summary>The EmfSmallTextOut record type.</summary>
        EmfSmallTextOut = 108,
        /// <summary>The EmfForceUfiMapping record type.</summary>
        EmfForceUfiMapping = 109,
        /// <summary>The EmfNamedEscpae record type.</summary>
        EmfNamedEscpae = 110,
        /// <summary>The EmfColorCorrectPalette record type.</summary>
        EmfColorCorrectPalette = 111,
        /// <summary>The EmfSetIcmProfileA record type.</summary>
        EmfSetIcmProfileA = 112,
        /// <summary>The EmfSetIcmProfileW record type.</summary>
        EmfSetIcmProfileW = 113,
        /// <summary>The EmfAlphaBlend record type.</summary>
        EmfAlphaBlend = 114,
        /// <summary>The EmfSetLayout record type.</summary>
        EmfSetLayout = 115,
        /// <summary>The EmfTransparentBlt record type.</summary>
        EmfTransparentBlt = 116,
        /// <summary>The EmfReserved117 record type.</summary>
        EmfReserved117 = 117,
        /// <summary>The EmfGradientFill record type.</summary>
        EmfGradientFill = 118,
        /// <summary>The EmfSetLinkedUfis record type.</summary>
        EmfSetLinkedUfis = 119,
        /// <summary>The EmfSetTextJustification record type.</summary>
        EmfSetTextJustification = 120,
        /// <summary>The EmfColorMatchToTargetW record type.</summary>
        EmfColorMatchToTargetW = 121,
        /// <summary>The EmfCreateColorSpaceW record type.</summary>
        EmfCreateColorSpaceW = 122,
        /// <summary>The EmfMax record type.</summary>
        EmfMax = 122,
        /// <summary>The EmfMin record type.</summary>
        EmfMin = 1,
        /// <summary>The EmfPlusRecordBase record type.</summary>
        EmfPlusRecordBase = 16384,
        /// <summary>The Invalid record type.</summary>
        Invalid = 16384,
        /// <summary>The Header record type.</summary>
        Header = 16385,
        /// <summary>The EndOfFile record type.</summary>
        EndOfFile = 16386,
        /// <summary>The Comment record type.</summary>
        Comment = 16387,
        /// <summary>The GetDC record type.</summary>
        GetDC = 16388,
        /// <summary>The MultiFormatStart record type.</summary>
        MultiFormatStart = 16389,
        /// <summary>The MultiFormatSection record type.</summary>
        MultiFormatSection = 16390,
        /// <summary>The MultiFormatEnd record type.</summary>
        MultiFormatEnd = 16391,
        /// <summary>The Object record type.</summary>
        Object = 16392,
        /// <summary>The Clear record type.</summary>
        Clear = 16393,
        /// <summary>The FillRects record type.</summary>
        FillRects = 16394,
        /// <summary>The DrawRects record type.</summary>
        DrawRects = 16395,
        /// <summary>The FillPolygon record type.</summary>
        FillPolygon = 16396,
        /// <summary>The DrawLines record type.</summary>
        DrawLines = 16397,
        /// <summary>The FillEllipse record type.</summary>
        FillEllipse = 16398,
        /// <summary>The DrawEllipse record type.</summary>
        DrawEllipse = 16399,
        /// <summary>The FillPie record type.</summary>
        FillPie = 16400,
        /// <summary>The DrawPie record type.</summary>
        DrawPie = 16401,
        /// <summary>The DrawArc record type.</summary>
        DrawArc = 16402,
        /// <summary>The FillRegion record type.</summary>
        FillRegion = 16403,
        /// <summary>The FillPath record type.</summary>
        FillPath = 16404,
        /// <summary>The DrawPath record type.</summary>
        DrawPath = 16405,
        /// <summary>The FillClosedCurve record type.</summary>
        FillClosedCurve = 16406,
        /// <summary>The DrawClosedCurve record type.</summary>
        DrawClosedCurve = 16407,
        /// <summary>The DrawCurve record type.</summary>
        DrawCurve = 16408,
        /// <summary>The DrawBeziers record type.</summary>
        DrawBeziers = 16409,
        /// <summary>The DrawImage record type.</summary>
        DrawImage = 16410,
        /// <summary>The DrawImagePoints record type.</summary>
        DrawImagePoints = 16411,
        /// <summary>The DrawString record type.</summary>
        DrawString = 16412,
        /// <summary>The SetRenderingOrigin record type.</summary>
        SetRenderingOrigin = 16413,
        /// <summary>The SetAntiAliasMode record type.</summary>
        SetAntiAliasMode = 16414,
        /// <summary>The SetTextRenderingHint record type.</summary>
        SetTextRenderingHint = 16415,
        /// <summary>The SetTextContrast record type.</summary>
        SetTextContrast = 16416,
        /// <summary>The SetInterpolationMode record type.</summary>
        SetInterpolationMode = 16417,
        /// <summary>The SetPixelOffsetMode record type.</summary>
        SetPixelOffsetMode = 16418,
        /// <summary>The SetCompositingMode record type.</summary>
        SetCompositingMode = 16419,
        /// <summary>The SetCompositingQuality record type.</summary>
        SetCompositingQuality = 16420,
        /// <summary>The Save record type.</summary>
        Save = 16421,
        /// <summary>The Restore record type.</summary>
        Restore = 16422,
        /// <summary>The BeginContainer record type.</summary>
        BeginContainer = 16423,
        /// <summary>The BeginContainerNoParams record type.</summary>
        BeginContainerNoParams = 16424,
        /// <summary>The EndContainer record type.</summary>
        EndContainer = 16425,
        /// <summary>The SetWorldTransform record type.</summary>
        SetWorldTransform = 16426,
        /// <summary>The ResetWorldTransform record type.</summary>
        ResetWorldTransform = 16427,
        /// <summary>The MultiplyWorldTransform record type.</summary>
        MultiplyWorldTransform = 16428,
        /// <summary>The TranslateWorldTransform record type.</summary>
        TranslateWorldTransform = 16429,
        /// <summary>The ScaleWorldTransform record type.</summary>
        ScaleWorldTransform = 16430,
        /// <summary>The RotateWorldTransform record type.</summary>
        RotateWorldTransform = 16431,
        /// <summary>The SetPageTransform record type.</summary>
        SetPageTransform = 16432,
        /// <summary>The ResetClip record type.</summary>
        ResetClip = 16433,
        /// <summary>The SetClipRect record type.</summary>
        SetClipRect = 16434,
        /// <summary>The SetClipPath record type.</summary>
        SetClipPath = 16435,
        /// <summary>The SetClipRegion record type.</summary>
        SetClipRegion = 16436,
        /// <summary>The OffsetClip record type.</summary>
        OffsetClip = 16437,
        /// <summary>The DrawDriverString record type.</summary>
        DrawDriverString = 16438,
        /// <summary>The Total record type.</summary>
        Total = 16439,
        /// <summary>The Max record type.</summary>
        Max = 16438,
        /// <summary>The Min record type.</summary>
        Min = 16385,
    }

    /// <summary>Called for each record as a metafile is played back.</summary>
    public delegate void PlayRecordCallback (EmfPlusRecordType recordType, int flags, int dataSize, IntPtr recordData);

    /// <summary>The header of a Windows metafile.</summary>
    public sealed class MetaHeader
    {
        /// <summary>Initializes a new instance of the <see cref="MetaHeader"/> class.</summary>
        public MetaHeader () { }

        /// <summary>Gets or sets whether the metafile is in memory or on disk.</summary>
        public short Type { get; set; }

        /// <summary>Gets or sets the size of the header, in words.</summary>
        public short HeaderSize { get; set; }

        /// <summary>Gets or sets the format version.</summary>
        public short Version { get; set; }

        /// <summary>Gets or sets the size of the metafile, in words.</summary>
        public int Size { get; set; }

        /// <summary>Gets or sets the number of objects in the metafile.</summary>
        public short NoObjects { get; set; }

        /// <summary>Gets or sets the size of the largest record, in words.</summary>
        public int MaxRecord { get; set; }

        /// <summary>Gets or sets the number of parameters.</summary>
        public short NoParameters { get; set; }
    }

    /// <summary>The header prefixed to a placeable Windows metafile.</summary>
    public sealed class WmfPlaceableFileHeader
    {
        /// <summary>Initializes a new instance of the <see cref="WmfPlaceableFileHeader"/> class.</summary>
        public WmfPlaceableFileHeader () { }

        /// <summary>Gets or sets the magic number identifying a placeable metafile.</summary>
        public int Key { get; set; }

        /// <summary>Gets or sets the metafile handle, always zero on disk.</summary>
        public short Hmf { get; set; }

        /// <summary>Gets or sets the left edge of the bounding rectangle.</summary>
        public short BboxLeft { get; set; }

        /// <summary>Gets or sets the top edge of the bounding rectangle.</summary>
        public short BboxTop { get; set; }

        /// <summary>Gets or sets the right edge of the bounding rectangle.</summary>
        public short BboxRight { get; set; }

        /// <summary>Gets or sets the bottom edge of the bounding rectangle.</summary>
        public short BboxBottom { get; set; }

        /// <summary>Gets or sets the number of metafile units per inch.</summary>
        public short Inch { get; set; }

        /// <summary>Gets or sets the reserved field, always zero.</summary>
        public int Reserved { get; set; }

        /// <summary>Gets or sets the checksum of the preceding fields.</summary>
        public short Checksum { get; set; }
    }

    /// <summary>Describes a metafile: its kind, size, resolution and bounds.</summary>
    public sealed class MetafileHeader
    {
        internal MetafileHeader () { }

        /// <summary>Gets the kind of metafile.</summary>
        public MetafileType Type { get; internal set; } = MetafileType.Invalid;

        /// <summary>Gets the size of the metafile, in bytes.</summary>
        public int MetafileSize { get; internal set; }

        /// <summary>Gets the format version.</summary>
        public int Version { get; internal set; }

        /// <summary>Gets the horizontal resolution, in dots per inch.</summary>
        public float DpiX { get; internal set; }

        /// <summary>Gets the vertical resolution, in dots per inch.</summary>
        public float DpiY { get; internal set; }

        /// <summary>Gets the rectangle the metafile was recorded against.</summary>
        public Rectangle Bounds { get; internal set; }

        /// <summary>Gets the WMF header, when this describes a Windows metafile.</summary>
        public MetaHeader? WmfHeader { get; internal set; }

        /// <summary>Gets the size of the EMF+ header, in bytes.</summary>
        public int EmfPlusHeaderSize { get; internal set; }

        /// <summary>Gets the horizontal resolution the metafile was recorded at.</summary>
        public int LogicalDpiX { get; internal set; }

        /// <summary>Gets the vertical resolution the metafile was recorded at.</summary>
        public int LogicalDpiY { get; internal set; }

        /// <summary>Returns whether this describes a Windows metafile.</summary>
        public bool IsWmf () => Type is MetafileType.Wmf or MetafileType.WmfPlaceable;

        /// <summary>Returns whether this describes a placeable Windows metafile.</summary>
        public bool IsWmfPlaceable () => Type == MetafileType.WmfPlaceable;

        /// <summary>Returns whether this describes an enhanced metafile.</summary>
        public bool IsEmf () => Type == MetafileType.Emf;

        /// <summary>Returns whether this describes an enhanced metafile of any flavour.</summary>
        public bool IsEmfOrEmfPlus () => Type is MetafileType.Emf or MetafileType.EmfPlusOnly or MetafileType.EmfPlusDual;

        /// <summary>Returns whether this describes an EMF+ metafile of either flavour.</summary>
        public bool IsEmfPlus () => Type is MetafileType.EmfPlusOnly or MetafileType.EmfPlusDual;

        /// <summary>Returns whether this describes an EMF+ metafile holding EMF records too.</summary>
        public bool IsEmfPlusDual () => Type == MetafileType.EmfPlusDual;

        /// <summary>Returns whether this describes an EMF+ metafile holding only EMF+ records.</summary>
        public bool IsEmfPlusOnly () => Type == MetafileType.EmfPlusOnly;

        /// <summary>Returns whether the metafile was recorded against a display rather than a printer.</summary>
        public bool IsDisplay () => IsEmfPlus ();
    }
}

namespace Majorsilence.Forms.Drawing
{
    /// <summary>The raster operation a pixel-block copy applies.</summary>
    public enum CopyPixelOperation
    {
        /// <summary>The Blackness raster operation.</summary>
        Blackness = 66,
        /// <summary>The CaptureBlt raster operation.</summary>
        CaptureBlt = 1073741824,
        /// <summary>The DestinationInvert raster operation.</summary>
        DestinationInvert = 5570569,
        /// <summary>The MergeCopy raster operation.</summary>
        MergeCopy = 12583114,
        /// <summary>The MergePaint raster operation.</summary>
        MergePaint = 12255782,
        /// <summary>The NoMirrorBitmap raster operation.</summary>
        NoMirrorBitmap = -2147483648,
        /// <summary>The NotSourceCopy raster operation.</summary>
        NotSourceCopy = 3342344,
        /// <summary>The NotSourceErase raster operation.</summary>
        NotSourceErase = 1114278,
        /// <summary>The PatCopy raster operation.</summary>
        PatCopy = 15728673,
        /// <summary>The PatInvert raster operation.</summary>
        PatInvert = 5898313,
        /// <summary>The PatPaint raster operation.</summary>
        PatPaint = 16452105,
        /// <summary>The SourceAnd raster operation.</summary>
        SourceAnd = 8913094,
        /// <summary>The SourceCopy raster operation.</summary>
        SourceCopy = 13369376,
        /// <summary>The SourceErase raster operation.</summary>
        SourceErase = 4457256,
        /// <summary>The SourceInvert raster operation.</summary>
        SourceInvert = 6684742,
        /// <summary>The SourcePaint raster operation.</summary>
        SourcePaint = 15597702,
        /// <summary>The Whiteness raster operation.</summary>
        Whiteness = 16711778,
    }
}
