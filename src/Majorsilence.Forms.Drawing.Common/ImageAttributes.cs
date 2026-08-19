using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>
    /// A 5x5 matrix of color-transform coefficients. Cross-platform replacement for
    /// <c>System.Drawing.Imaging.ColorMatrix</c>.
    /// </summary>
    /// <remarks>
    /// GDI+ applies the matrix as a row-vector multiply: <c>[R G B A 1] * matrix</c>, with every
    /// component (including the translation row, <c>Matrix40</c>..<c>Matrix44</c>) normalized to the
    /// 0..1 range. Column <c>c</c> therefore holds the coefficients that produce output channel
    /// <c>c</c>. See <see cref="ImageAttributes.ToSKColorFilter"/> for the conversion to SkiaSharp's
    /// (transposed) 4x5 row-major layout.
    /// </remarks>
    public sealed class ColorMatrix
    {
        private readonly float[] values = new float[25];

        /// <summary>Initializes a new identity color matrix.</summary>
        public ColorMatrix ()
        {
            values[0] = values[6] = values[12] = values[18] = values[24] = 1f;
        }

        /// <summary>
        /// Initializes a new color matrix from a jagged 5x5 array. Rows or entries beyond the
        /// supplied data keep their identity-matrix value.
        /// </summary>
        public ColorMatrix (float[][] newColorMatrix) : this ()
        {
            Guard.ThrowIfNull (newColorMatrix);
            for (var row = 0; row < 5 && row < newColorMatrix.Length; row++) {
                var source = newColorMatrix[row];
                if (source is null)
                    continue;
                for (var col = 0; col < 5 && col < source.Length; col++)
                    values[row * 5 + col] = source[col];
            }
        }

        /// <summary>Gets or sets the element at the specified row and column.</summary>
        public float this[int row, int column] {
            get {
                Validate (row, column);
                return values[row * 5 + column];
            }
            set {
                Validate (row, column);
                values[row * 5 + column] = value;
            }
        }

        private static void Validate (int row, int column)
        {
            if ((uint)row > 4u)
                throw new ArgumentOutOfRangeException (nameof (row));
            if ((uint)column > 4u)
                throw new ArgumentOutOfRangeException (nameof (column));
        }

        /// <summary>Gets or sets the element at row 0, column 0.</summary>
        public float Matrix00 { get => values[0]; set => values[0] = value; }
        /// <summary>Gets or sets the element at row 0, column 1.</summary>
        public float Matrix01 { get => values[1]; set => values[1] = value; }
        /// <summary>Gets or sets the element at row 0, column 2.</summary>
        public float Matrix02 { get => values[2]; set => values[2] = value; }
        /// <summary>Gets or sets the element at row 0, column 3.</summary>
        public float Matrix03 { get => values[3]; set => values[3] = value; }
        /// <summary>Gets or sets the element at row 0, column 4.</summary>
        public float Matrix04 { get => values[4]; set => values[4] = value; }
        /// <summary>Gets or sets the element at row 1, column 0.</summary>
        public float Matrix10 { get => values[5]; set => values[5] = value; }
        /// <summary>Gets or sets the element at row 1, column 1.</summary>
        public float Matrix11 { get => values[6]; set => values[6] = value; }
        /// <summary>Gets or sets the element at row 1, column 2.</summary>
        public float Matrix12 { get => values[7]; set => values[7] = value; }
        /// <summary>Gets or sets the element at row 1, column 3.</summary>
        public float Matrix13 { get => values[8]; set => values[8] = value; }
        /// <summary>Gets or sets the element at row 1, column 4.</summary>
        public float Matrix14 { get => values[9]; set => values[9] = value; }
        /// <summary>Gets or sets the element at row 2, column 0.</summary>
        public float Matrix20 { get => values[10]; set => values[10] = value; }
        /// <summary>Gets or sets the element at row 2, column 1.</summary>
        public float Matrix21 { get => values[11]; set => values[11] = value; }
        /// <summary>Gets or sets the element at row 2, column 2.</summary>
        public float Matrix22 { get => values[12]; set => values[12] = value; }
        /// <summary>Gets or sets the element at row 2, column 3.</summary>
        public float Matrix23 { get => values[13]; set => values[13] = value; }
        /// <summary>Gets or sets the element at row 2, column 4.</summary>
        public float Matrix24 { get => values[14]; set => values[14] = value; }
        /// <summary>Gets or sets the element at row 3, column 0.</summary>
        public float Matrix30 { get => values[15]; set => values[15] = value; }
        /// <summary>Gets or sets the element at row 3, column 1.</summary>
        public float Matrix31 { get => values[16]; set => values[16] = value; }
        /// <summary>Gets or sets the element at row 3, column 2.</summary>
        public float Matrix32 { get => values[17]; set => values[17] = value; }
        /// <summary>Gets or sets the element at row 3, column 3.</summary>
        public float Matrix33 { get => values[18]; set => values[18] = value; }
        /// <summary>Gets or sets the element at row 3, column 4.</summary>
        public float Matrix34 { get => values[19]; set => values[19] = value; }
        /// <summary>Gets or sets the element at row 4, column 0.</summary>
        public float Matrix40 { get => values[20]; set => values[20] = value; }
        /// <summary>Gets or sets the element at row 4, column 1.</summary>
        public float Matrix41 { get => values[21]; set => values[21] = value; }
        /// <summary>Gets or sets the element at row 4, column 2.</summary>
        public float Matrix42 { get => values[22]; set => values[22] = value; }
        /// <summary>Gets or sets the element at row 4, column 3.</summary>
        public float Matrix43 { get => values[23]; set => values[23] = value; }
        /// <summary>Gets or sets the element at row 4, column 4.</summary>
        public float Matrix44 { get => values[24]; set => values[24] = value; }

        /// <summary>
        /// Converts this GDI+ (column-major, row-vector) 5x5 matrix into the 20-element,
        /// row-major 4x5 array SkiaSharp's <c>SKColorFilter.CreateColorMatrix</c> expects.
        /// </summary>
        /// <remarks>
        /// Skia computes <c>out[i] = m[i*5+0]*R + m[i*5+1]*G + m[i*5+2]*B + m[i*5+3]*A + m[i*5+4]</c>,
        /// so its row <c>i</c> is this matrix's column <c>i</c> — a plain transpose of the first four
        /// columns. Skia 3.x normalizes every coefficient <em>including</em> the trailing bias to the
        /// 0..1 range (verified empirically against SkiaSharp 3.119.4), which is the same convention
        /// GDI+ uses, so no 0..255 rescaling of the translation row is applied.
        /// </remarks>
        internal float[] ToSkiaColorMatrix ()
        {
            var m = new float[20];
            for (var outChannel = 0; outChannel < 4; outChannel++)
                for (var inChannel = 0; inChannel < 5; inChannel++)
                    m[outChannel * 5 + inChannel] = values[inChannel * 5 + outChannel];
            return m;
        }

        /// <summary>Returns true when this matrix is the identity matrix (no color transform).</summary>
        internal bool IsIdentity {
            get {
                for (var i = 0; i < 25; i++) {
                    var expected = i % 6 == 0 ? 1f : 0f;
                    if (Math.Abs (values[i] - expected) > 1e-6f)
                        return false;
                }
                return true;
            }
        }
    }

    /// <summary>Specifies which colors a color matrix applies to. Matches System.Drawing.Imaging.ColorMatrixFlag.</summary>
    public enum ColorMatrixFlag
    {
        /// <summary>All color values, including grays, are adjusted.</summary>
        Default = 0,
        /// <summary>Gray shades are not adjusted.</summary>
        SkipGrays = 1,
        /// <summary>Only gray shades are adjusted.</summary>
        AltGrays = 2
    }

    /// <summary>Specifies which category of object a color adjustment applies to. Matches System.Drawing.Imaging.ColorAdjustType.</summary>
    public enum ColorAdjustType
    {
        /// <summary>Color adjustments apply to all categories that have no category-specific setting.</summary>
        Default = 0,
        /// <summary>Color adjustments apply to bitmapped images.</summary>
        Bitmap = 1,
        /// <summary>Color adjustments apply to brush operations in metafiles.</summary>
        Brush = 2,
        /// <summary>Color adjustments apply to pen operations in metafiles.</summary>
        Pen = 3,
        /// <summary>Color adjustments apply to text drawn in metafiles.</summary>
        Text = 4,
        /// <summary>The number of adjustment categories.</summary>
        Count = 5,
        /// <summary>The number of adjustment categories that are valid for use.</summary>
        Any = 6
    }

    /// <summary>
    /// Defines a source color and the color it is remapped to. Cross-platform replacement for
    /// <c>System.Drawing.Imaging.ColorMap</c>.
    /// </summary>
    public sealed class ColorMap
    {
        /// <summary>Gets or sets the existing color to be converted.</summary>
        public Color OldColor { get; set; }

        /// <summary>Gets or sets the color that <see cref="OldColor"/> is converted to.</summary>
        public Color NewColor { get; set; }
    }

    /// <summary>
    /// Holds the color and gamma adjustments applied while an image is drawn. Cross-platform
    /// replacement for <c>System.Drawing.Imaging.ImageAttributes</c>.
    /// </summary>
    /// <remarks>
    /// Pass an instance to the matching <c>Graphics.DrawImage</c> overload. Adjustments that can be
    /// expressed as a per-pixel channel transform (<see cref="SetColorMatrix(ColorMatrix)"/>,
    /// <see cref="SetGamma(float)"/>) are applied through an <c>SKColorFilter</c> on the draw paint;
    /// adjustments that are per-pixel lookups (<see cref="SetColorKey(Color, Color)"/>,
    /// <see cref="SetRemapTable(ColorMap[])"/>) are applied by transforming a temporary copy of the
    /// source bitmap before it is drawn.
    /// </remarks>
    public sealed class ImageAttributes : IDisposable, ICloneable
    {
        private ColorMatrix? colorMatrix;
        private ColorMatrixFlag colorMatrixFlag = ColorMatrixFlag.Default;
        private float? gamma;
        private Color colorKeyLow;
        private Color colorKeyHigh;
        private bool hasColorKey;
        private ColorMap[]? remapTable;

        /// <summary>Initializes a new, empty ImageAttributes (no adjustments).</summary>
        public ImageAttributes () { }

        /// <summary>Sets the color-adjustment matrix used when drawing.</summary>
        public void SetColorMatrix (ColorMatrix newColorMatrix)
            => SetColorMatrix (newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

        /// <summary>Sets the color-adjustment matrix and the flag controlling which colors it affects.</summary>
        public void SetColorMatrix (ColorMatrix newColorMatrix, ColorMatrixFlag flags)
            => SetColorMatrix (newColorMatrix, flags, ColorAdjustType.Default);

        /// <summary>
        /// Sets the color-adjustment matrix for the specified category. Majorsilence.Forms.Drawing
        /// keeps a single set of adjustments rather than per-category ones (only the
        /// <see cref="ColorAdjustType.Bitmap"/>/<see cref="ColorAdjustType.Default"/> categories are
        /// reachable from a SkiaSharp draw call — the others exist for metafile recording, which is
        /// out of scope), so <paramref name="type"/> is accepted and ignored.
        /// </summary>
        public void SetColorMatrix (ColorMatrix newColorMatrix, ColorMatrixFlag mode, ColorAdjustType type)
        {
            Guard.ThrowIfNull (newColorMatrix);
            colorMatrix = newColorMatrix;
            colorMatrixFlag = mode;
        }

        /// <summary>Clears the color-adjustment matrix.</summary>
        public void ClearColorMatrix () => ClearColorMatrix (ColorAdjustType.Default);

        /// <summary>Clears the color-adjustment matrix for the specified category.</summary>
        public void ClearColorMatrix (ColorAdjustType type)
        {
            colorMatrix = null;
            colorMatrixFlag = ColorMatrixFlag.Default;
        }

        /// <summary>
        /// Sets the color-adjustment matrix and a separate grayscale-adjustment matrix. Only the
        /// color matrix participates in rendering: a separate gray matrix requires classifying each
        /// pixel as gray or not, which a channel-independent <c>SKColorFilter</c> cannot express.
        /// The gray matrix is stored so callers round-trip, and the flag is recorded.
        /// </summary>
        public void SetColorMatrices (ColorMatrix newColorMatrix, ColorMatrix? grayMatrix)
            => SetColorMatrices (newColorMatrix, grayMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

        /// <inheritdoc cref="SetColorMatrices(ColorMatrix, ColorMatrix)"/>
        public void SetColorMatrices (ColorMatrix newColorMatrix, ColorMatrix? grayMatrix, ColorMatrixFlag flags)
            => SetColorMatrices (newColorMatrix, grayMatrix, flags, ColorAdjustType.Default);

        /// <inheritdoc cref="SetColorMatrices(ColorMatrix, ColorMatrix)"/>
        public void SetColorMatrices (ColorMatrix newColorMatrix, ColorMatrix? grayMatrix, ColorMatrixFlag mode, ColorAdjustType type)
        {
            SetColorMatrix (newColorMatrix, mode, type);
            GrayMatrix = grayMatrix;
        }

        /// <summary>Gets the grayscale-adjustment matrix set by <see cref="SetColorMatrices(ColorMatrix, ColorMatrix)"/>, if any.</summary>
        public ColorMatrix? GrayMatrix { get; private set; }

        /// <summary>Sets the gamma correction value applied when drawing (1.0 = no correction).</summary>
        public void SetGamma (float value) => SetGamma (value, ColorAdjustType.Default);

        /// <summary>Sets the gamma correction value for the specified category.</summary>
        public void SetGamma (float gamma, ColorAdjustType type)
        {
            this.gamma = gamma <= 0f ? 1f : gamma;
        }

        /// <summary>Clears the gamma correction value.</summary>
        public void ClearGamma () => ClearGamma (ColorAdjustType.Default);

        /// <summary>Clears the gamma correction value for the specified category.</summary>
        public void ClearGamma (ColorAdjustType type) => gamma = null;

        /// <summary>
        /// Sets the transparent-color range: pixels whose channels all fall between
        /// <paramref name="colorLow"/> and <paramref name="colorHigh"/> (inclusive) are made fully
        /// transparent when drawn.
        /// </summary>
        public void SetColorKey (Color colorLow, Color colorHigh) => SetColorKey (colorLow, colorHigh, ColorAdjustType.Default);

        /// <inheritdoc cref="SetColorKey(Color, Color)"/>
        public void SetColorKey (Color colorLow, Color colorHigh, ColorAdjustType type)
        {
            colorKeyLow = colorLow;
            colorKeyHigh = colorHigh;
            hasColorKey = true;
        }

        /// <summary>Clears the transparent-color range.</summary>
        public void ClearColorKey () => ClearColorKey (ColorAdjustType.Default);

        /// <summary>Clears the transparent-color range for the specified category.</summary>
        public void ClearColorKey (ColorAdjustType type) => hasColorKey = false;

        /// <summary>Sets the color-remap table: exact source colors replaced with new colors when drawn.</summary>
        public void SetRemapTable (ColorMap[] map) => SetRemapTable (map, ColorAdjustType.Default);

        /// <inheritdoc cref="SetRemapTable(ColorMap[])"/>
        public void SetRemapTable (ColorMap[] map, ColorAdjustType type)
        {
            Guard.ThrowIfNull (map);
            remapTable = map;
        }

        /// <summary>Clears the color-remap table.</summary>
        public void ClearRemapTable () => ClearRemapTable (ColorAdjustType.Default);

        /// <summary>Clears the color-remap table for the specified category.</summary>
        public void ClearRemapTable (ColorAdjustType type) => remapTable = null;

        /// <summary>
        /// Sets the remap table used when filling with a texture brush. Stored separately from
        /// <see cref="SetRemapTable(ColorMap[])"/>, matching GDI+'s per-category split, but the draw
        /// path applies only the bitmap remap table -- brush fills go through the shader, which has no
        /// per-color substitution step.
        /// </summary>
        public void SetBrushRemapTable (ColorMap[] map)
        {
            Guard.ThrowIfNull (map);
            brushRemapTable = map;
        }

        /// <summary>Clears the brush remap table.</summary>
        public void ClearBrushRemapTable () => brushRemapTable = null;

        private ColorMap[]? brushRemapTable;

        /// <summary>
        /// Sets the alpha threshold above which a color is treated as fully opaque, as a fraction 0..1.
        /// </summary>
        /// <remarks>
        /// Stored and round-tripped. Applying it means a per-pixel pass on the source (as
        /// <see cref="SetColorKey(Color, Color)"/> does), which is not yet wired into the draw path.
        /// </remarks>
        public void SetThreshold (float threshold) => SetThreshold (threshold, ColorAdjustType.Default);

        /// <inheritdoc cref="SetThreshold(float)"/>
        public void SetThreshold (float threshold, ColorAdjustType type) => Threshold = threshold;

        /// <summary>Clears the alpha threshold.</summary>
        public void ClearThreshold () => ClearThreshold (ColorAdjustType.Default);

        /// <summary>Clears the alpha threshold for the specified category.</summary>
        public void ClearThreshold (ColorAdjustType type) => Threshold = null;

        /// <summary>Gets the alpha threshold set by <see cref="SetThreshold(float)"/>, if any.</summary>
        public float? Threshold { get; private set; }

        /// <summary>
        /// Turns off all color adjustment for subsequent draws without discarding the settings, so
        /// <see cref="ClearNoOp()"/> restores them. Honored by the draw path: while set, no color filter
        /// or per-pixel adjustment is applied.
        /// </summary>
        public void SetNoOp () => NoOp = true;

        /// <inheritdoc cref="SetNoOp()"/>
        public void SetNoOp (ColorAdjustType type) => NoOp = true;

        /// <summary>Re-enables the color adjustments suspended by <see cref="SetNoOp()"/>.</summary>
        public void ClearNoOp () => NoOp = false;

        /// <inheritdoc cref="ClearNoOp()"/>
        public void ClearNoOp (ColorAdjustType type) => NoOp = false;

        /// <summary>Gets whether color adjustment is currently suspended.</summary>
        public bool NoOp { get; private set; }

        /// <summary>
        /// Selects a single CMYK channel to output. Stored and round-tripped; the Skia draw path is RGBA
        /// throughout and has no CMYK separation stage.
        /// </summary>
        public void SetOutputChannel (ColorChannelFlag flags) => OutputChannel = flags;

        /// <inheritdoc cref="SetOutputChannel(ColorChannelFlag)"/>
        public void SetOutputChannel (ColorChannelFlag flags, ColorAdjustType type) => OutputChannel = flags;

        /// <summary>Clears the output channel selection.</summary>
        public void ClearOutputChannel () => OutputChannel = null;

        /// <inheritdoc cref="ClearOutputChannel()"/>
        public void ClearOutputChannel (ColorAdjustType type) => OutputChannel = null;

        /// <summary>Gets the output channel selected by <see cref="SetOutputChannel(ColorChannelFlag)"/>, if any.</summary>
        public ColorChannelFlag? OutputChannel { get; private set; }

        /// <summary>
        /// Sets the ICC profile used for the output channel. Not supported: color management is out of
        /// scope for this layer, so the path is stored and never opened.
        /// </summary>
        public void SetOutputChannelColorProfile (string colorProfileFilename) => OutputChannelColorProfile = colorProfileFilename;

        /// <inheritdoc cref="SetOutputChannelColorProfile(string)"/>
        public void SetOutputChannelColorProfile (string colorProfileFilename, ColorAdjustType type)
            => OutputChannelColorProfile = colorProfileFilename;

        /// <summary>Clears the output-channel color profile.</summary>
        public void ClearOutputChannelColorProfile () => OutputChannelColorProfile = null;

        /// <inheritdoc cref="ClearOutputChannelColorProfile()"/>
        public void ClearOutputChannelColorProfile (ColorAdjustType type) => OutputChannelColorProfile = null;

        /// <summary>Gets the ICC profile path set for the output channel, if any.</summary>
        public string? OutputChannelColorProfile { get; private set; }

        /// <summary>
        /// Applies this instance's color adjustments to the entries of <paramref name="palette"/>.
        /// </summary>
        /// <remarks>
        /// Applies the remap table and the color matrix, which are the adjustments that are meaningful
        /// against palette entries rather than pixels. The palette is modified in place, as GDI+ does.
        /// </remarks>
        public void GetAdjustedPalette (ColorPalette palette, ColorAdjustType type)
        {
            if (palette is null || NoOp)
                return;

            for (var i = 0; i < palette.Entries.Length; i++) {
                var color = palette.Entries[i];

                if (remapTable is not null) {
                    foreach (var map in remapTable) {
                        if (map.OldColor.ToArgb () == color.ToArgb ()) {
                            color = map.NewColor;
                            break;
                        }
                    }
                }

                if (colorMatrix is not null)
                    color = ApplyMatrix (colorMatrix, color);

                palette.Entries[i] = color;
            }
        }

        // The GDI+ convention: the color is a row vector [r g b a 1] multiplied by the 5x5 matrix, with
        // every component normalized to 0..1 (including the translation row).
        private static Color ApplyMatrix (ColorMatrix m, Color c)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f, a = c.A / 255f;

            float Component (int column) =>
                r * m[0, column] + g * m[1, column] + b * m[2, column] + a * m[3, column] + m[4, column];

            static int ToByte (float value) => MathCompat.Clamp ((int)MathFCompat.Round (value * 255f), 0, 255);

            return Color.FromArgb (ToByte (Component (3)), ToByte (Component (0)), ToByte (Component (1)), ToByte (Component (2)));
        }

        /// <summary>
        /// Gets or sets the wrap mode used when the source rectangle extends past the image. Stored
        /// and round-tripped; the SkiaSharp draw path always clamps to the source rectangle.
        /// </summary>
        public WrapMode WrapMode { get; set; } = WrapMode.Clamp;

        /// <summary>Sets the wrap mode used when the source rectangle extends past the image.</summary>
        public void SetWrapMode (WrapMode mode) => WrapMode = mode;

        /// <summary>Sets the wrap mode and clamp color used when the source rectangle extends past the image.</summary>
        public void SetWrapMode (WrapMode mode, Color color)
        {
            WrapMode = mode;
            ClampColor = color;
        }

        /// <summary>Sets the wrap mode, clamp color and clamp flag.</summary>
        public void SetWrapMode (WrapMode mode, Color color, bool clamp)
        {
            WrapMode = mode;
            ClampColor = color;
        }

        /// <summary>Gets the clamp color set by <see cref="SetWrapMode(WrapMode, Color)"/>.</summary>
        public Color ClampColor { get; private set; } = Color.Transparent;

        /// <summary>Gets whether any adjustment at all has been configured.</summary>
        internal bool IsEmpty => colorMatrix is null && gamma is null && !hasColorKey && remapTable is null;

        /// <summary>
        /// Builds the <see cref="SKColorFilter"/> that realizes the channel-transform adjustments
        /// (color matrix, then gamma) on a draw paint, or <c>null</c> when none are configured.
        /// Caller owns disposal.
        /// </summary>
        internal SKColorFilter? ToSKColorFilter ()
        {
            SKColorFilter? matrixFilter = null;
            if (colorMatrix is not null && !colorMatrix.IsIdentity)
                matrixFilter = SKColorFilter.CreateColorMatrix (colorMatrix.ToSkiaColorMatrix ());

            SKColorFilter? gammaFilter = null;
            if (gamma is { } g && Math.Abs (g - 1f) > 1e-6f) {
                // GDI+ gamma of g means the output is raised to the power 1/g.
                var table = new byte[256];
                var identity = new byte[256];
                var exponent = 1.0 / g;
                for (var i = 0; i < 256; i++) {
                    identity[i] = (byte)i;
                    table[i] = (byte)MathCompat.Clamp (Math.Round (Math.Pow (i / 255.0, exponent) * 255.0), 0, 255);
                }
                gammaFilter = SKColorFilter.CreateTable (identity, table, table, table);
            }

            if (matrixFilter is null)
                return gammaFilter;
            if (gammaFilter is null)
                return matrixFilter;

            // Gamma is applied after the matrix, so it is the outer filter.
            using (matrixFilter)
            using (gammaFilter)
                return SKColorFilter.CreateCompose (gammaFilter, matrixFilter);
        }

        /// <summary>
        /// Gets whether the per-pixel lookup adjustments (color key / remap table) are configured and
        /// therefore need <see cref="ApplyPixelAdjustments"/> to run before drawing.
        /// </summary>
        internal bool HasPixelAdjustments => hasColorKey || remapTable is { Length: > 0 };

        /// <summary>
        /// Returns a new bitmap with the color key and remap table applied, or <c>null</c> when
        /// neither is configured. Caller owns disposal of the returned bitmap.
        /// </summary>
        internal SKBitmap? ApplyPixelAdjustments (SKBitmap? source)
        {
            if (source is null || !HasPixelAdjustments)
                return null;

            var result = source.Copy (SKColorType.Bgra8888) ?? source.Copy ();
            if (result is null)
                return null;

            Dictionary<uint, SKColor>? remap = null;
            if (remapTable is { Length: > 0 }) {
                remap = new Dictionary<uint, SKColor> (remapTable.Length);
                foreach (var map in remapTable) {
                    if (map is null)
                        continue;
                    remap[Key (map.OldColor)] = new SKColor (map.NewColor.R, map.NewColor.G, map.NewColor.B, map.NewColor.A);
                }
            }

            for (var y = 0; y < result.Height; y++) {
                for (var x = 0; x < result.Width; x++) {
                    var pixel = result.GetPixel (x, y);

                    if (remap is not null && remap.TryGetValue (Key (pixel), out var replacement)) {
                        result.SetPixel (x, y, replacement);
                        continue;
                    }

                    if (hasColorKey && InKeyRange (pixel))
                        result.SetPixel (x, y, SKColors.Transparent);
                }
            }

            return result;
        }

        private static uint Key (Color c) => ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

        private static uint Key (SKColor c) => ((uint)c.Alpha << 24) | ((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue;

        private bool InKeyRange (SKColor c)
            => c.Red >= colorKeyLow.R && c.Red <= colorKeyHigh.R
            && c.Green >= colorKeyLow.G && c.Green <= colorKeyHigh.G
            && c.Blue >= colorKeyLow.B && c.Blue <= colorKeyHigh.B;

        /// <summary>Creates a copy of this ImageAttributes with the same adjustments.</summary>
        public object Clone () => new ImageAttributes {
            colorMatrix = colorMatrix,
            colorMatrixFlag = colorMatrixFlag,
            GrayMatrix = GrayMatrix,
            gamma = gamma,
            colorKeyLow = colorKeyLow,
            colorKeyHigh = colorKeyHigh,
            hasColorKey = hasColorKey,
            remapTable = remapTable is null ? null : (ColorMap[])remapTable.Clone (),
            WrapMode = WrapMode,
            ClampColor = ClampColor,
        };

        /// <summary>
        /// Releases the resources used by this ImageAttributes. No unmanaged state is held (the
        /// SKColorFilter is built fresh per draw and owned by the caller), so this is a no-op kept
        /// for API-shape compatibility with `using (var ia = new ImageAttributes())`.
        /// </summary>
        public void Dispose () => GC.SuppressFinalize (this);
    }
}
