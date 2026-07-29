using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Text
{
    /// <summary>
    /// Base class for the font-family collections. Cross-platform replacement for
    /// <c>System.Drawing.Text.FontCollection</c>.
    /// </summary>
    public abstract class FontCollection : IDisposable
    {
        /// <summary>Gets the font families in this collection.</summary>
        public abstract FontFamily[] Families { get; }

        /// <summary>Releases the resources used by this collection.</summary>
        public virtual void Dispose () => GC.SuppressFinalize (this);
    }

    /// <summary>
    /// Enumerates the fonts installed on the host system. Cross-platform replacement for
    /// <c>System.Drawing.Text.InstalledFontCollection</c>, backed by SkiaSharp's own
    /// <see cref="SKFontManager"/> font enumeration (which is fully cross-platform: it wraps
    /// DirectWrite on Windows, CoreText on macOS and fontconfig on Linux).
    /// </summary>
    public sealed class InstalledFontCollection : FontCollection
    {
        /// <summary>Initializes a new InstalledFontCollection.</summary>
        public InstalledFontCollection () { }

        /// <inheritdoc/>
        public override FontFamily[] Families => FontFamily.Families;
    }

    /// <summary>
    /// A collection of font families loaded from files or memory at runtime rather than installed on
    /// the system. Cross-platform replacement for <c>System.Drawing.Text.PrivateFontCollection</c>.
    /// </summary>
    /// <remarks>
    /// Families added here are registered process-wide, so a <see cref="Font"/> constructed with one
    /// of their names resolves to the loaded typeface even though the font is not installed --
    /// matching the GDI+ behaviour where a private font is usable via
    /// <c>new Font(collection.Families[0], size)</c>. Disposing the collection unregisters its
    /// families again.
    /// </remarks>
    public sealed class PrivateFontCollection : FontCollection
    {
        private readonly List<string> familyNames = new ();
        private readonly List<SKTypeface> typefaces = new ();
        private readonly List<SKData> retainedData = new ();
        private bool disposed;

        /// <summary>Initializes a new, empty PrivateFontCollection.</summary>
        public PrivateFontCollection () { }

        /// <inheritdoc/>
        public override FontFamily[] Families => familyNames.Select (n => new FontFamily (n)).ToArray ();

        /// <summary>
        /// Adds a font file (.ttf / .otf / .ttc) to this collection. Unreadable or malformed files
        /// are ignored, matching the compat layer's no-throw policy for unusable resources.
        /// </summary>
        public void AddFontFile (string filename)
        {
            ObjectDisposedException.ThrowIf (disposed, this);
            ArgumentNullException.ThrowIfNull (filename);
            if (!File.Exists (filename))
                throw new FileNotFoundException ("Font file not found.", filename);

            SKTypeface? typeface = null;
            try {
                typeface = SKTypeface.FromFile (filename);
            } catch (Exception) {
                typeface = null;
            }

            Register (typeface, retainedData: null);
        }

        /// <summary>
        /// Adds a font held in unmanaged memory to this collection (as produced by, for example,
        /// <c>Marshal.AllocCoTaskMem</c> plus <c>Marshal.Copy</c> of an embedded resource). The
        /// bytes are copied, so the caller may free <paramref name="memory"/> immediately after.
        /// </summary>
        public void AddMemoryFont (IntPtr memory, int length)
        {
            ObjectDisposedException.ThrowIf (disposed, this);
            if (memory == IntPtr.Zero)
                throw new ArgumentNullException (nameof (memory));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (length);

            var bytes = new byte[length];
            Marshal.Copy (memory, bytes, 0, length);
            AddMemoryFont (bytes);
        }

        /// <summary>
        /// Adds a font held in a managed byte array. Majorsilence.Forms.Drawing extension over the
        /// GDI+ surface -- the safe equivalent of <see cref="AddMemoryFont(IntPtr, int)"/> for the
        /// common "font is an embedded resource" case.
        /// </summary>
        public void AddMemoryFont (byte[] fontData)
        {
            ObjectDisposedException.ThrowIf (disposed, this);
            ArgumentNullException.ThrowIfNull (fontData);

            // The SKData keeps the native buffer alive for the typeface's lifetime -- same pattern
            // FontSubstitution uses for the embedded fallback fonts.
            var data = SKData.CreateCopy (fontData);
            SKTypeface? typeface = null;
            try {
                typeface = SKTypeface.FromData (data);
            } catch (Exception) {
                typeface = null;
            }

            if (typeface is null) {
                data.Dispose ();
                return;
            }

            Register (typeface, data);
        }

        private void Register (SKTypeface? typeface, SKData? retainedData)
        {
            if (typeface is null) {
                retainedData?.Dispose ();
                return;
            }

            typefaces.Add (typeface);
            if (retainedData is not null)
                this.retainedData.Add (retainedData);

            if (!familyNames.Contains (typeface.FamilyName, StringComparer.OrdinalIgnoreCase))
                familyNames.Add (typeface.FamilyName);

            PrivateFontRegistry.Add (typeface);
        }

        /// <inheritdoc/>
        public override void Dispose ()
        {
            if (disposed)
                return;
            disposed = true;

            foreach (var typeface in typefaces) {
                PrivateFontRegistry.Remove (typeface);
                typeface.Dispose ();
            }
            typefaces.Clear ();

            foreach (var data in retainedData)
                data.Dispose ();
            retainedData.Clear ();

            familyNames.Clear ();
            base.Dispose ();
        }
    }

    /// <summary>
    /// Process-wide index of typefaces loaded through <see cref="PrivateFontCollection"/>, so that
    /// <see cref="Font"/> can resolve a family name that is not installed on the system.
    /// </summary>
    internal static class PrivateFontRegistry
    {
        private static readonly object gate = new ();

        // Family name -> the typefaces registered for it, in registration order.
        private static readonly Dictionary<string, List<SKTypeface>> byFamily =
            new (StringComparer.OrdinalIgnoreCase);

        public static bool IsEmpty {
            get { lock (gate) return byFamily.Count == 0; }
        }

        public static void Add (SKTypeface typeface)
        {
            lock (gate) {
                if (!byFamily.TryGetValue (typeface.FamilyName, out var list))
                    byFamily[typeface.FamilyName] = list = new List<SKTypeface> ();
                list.Add (typeface);
            }
        }

        public static void Remove (SKTypeface typeface)
        {
            lock (gate) {
                if (!byFamily.TryGetValue (typeface.FamilyName, out var list))
                    return;
                list.Remove (typeface);
                if (list.Count == 0)
                    byFamily.Remove (typeface.FamilyName);
            }
        }

        /// <summary>
        /// Returns the best registered typeface for the family/style, or null when the family was
        /// never registered privately. Prefers an exact weight/slant match, then any entry for the
        /// family (SkiaSharp synthesizes bold/italic when the real face isn't present).
        /// </summary>
        public static SKTypeface? Resolve (string familyName, SKFontStyle style)
        {
            if (string.IsNullOrEmpty (familyName))
                return null;

            lock (gate) {
                if (byFamily.Count == 0 || !byFamily.TryGetValue (familyName, out var list) || list.Count == 0)
                    return null;

                foreach (var typeface in list) {
                    if (typeface.FontWeight == style.Weight && typeface.FontSlant == style.Slant)
                        return typeface;
                }
                return list[0];
            }
        }
    }
}
