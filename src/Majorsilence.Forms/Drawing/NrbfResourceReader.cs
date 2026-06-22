using System;
using System.Formats.Nrbf;
using System.IO;

// System.Formats.Nrbf ships as an [Experimental] API (SYSLIB5005). Its surface is stable enough for the
// narrow, read-only use here (pulling the image byte[] out of a legacy resx blob); suppress the gate.
#pragma warning disable SYSLIB5005

namespace Majorsilence.Forms
{
    /// <summary>
    /// Recovers images from a WinForms <c>.resx</c> <c>binary.base64</c> resource — a
    /// <c>BinaryFormatter</c>-serialized object — without invoking <c>BinaryFormatter</c> (removed from
    /// modern .NET). It reads the payload with <see cref="NrbfDecoder"/> (the supported safe reader for
    /// the legacy wire format) and pulls the raw bytes the framework types serialize:
    /// <list type="bullet">
    ///   <item><c>System.Drawing.Bitmap</c> / <c>Image</c> — a <c>Data</c> byte[] of image-file bytes;</item>
    ///   <item><c>System.Drawing.Icon</c> — an <c>IconData</c> byte[];</item>
    ///   <item><c>System.Windows.Forms.ImageListStreamer</c> — a <c>Data</c> byte[] decoded by
    ///         <see cref="ImageListStreamDecoder"/>.</item>
    /// </list>
    /// Anything else (arbitrary serialized objects) returns <see langword="null"/> — genuinely not
    /// portable, and left for manual handling.
    /// </summary>
    internal static class NrbfResourceReader
    {
        public static object? TryReadImage (byte[] blob)
        {
            try
            {
                using var stream = new MemoryStream (blob);
                if (NrbfDecoder.Decode (stream) is not ClassRecord record)
                    return null;

                var typeName = record.TypeName.FullName;

                if (typeName.StartsWith ("System.Drawing.Bitmap", StringComparison.Ordinal) ||
                    typeName.StartsWith ("System.Drawing.Image", StringComparison.Ordinal))
                {
                    var data = ReadByteArray (record, "Data");
                    return data is null ? null : Majorsilence.Drawing.Image.FromBytes (data);
                }

                if (typeName.StartsWith ("System.Drawing.Icon", StringComparison.Ordinal))
                {
                    var data = ReadByteArray (record, "IconData");
                    return data is null ? null : new Majorsilence.Drawing.Icon (new MemoryStream (data));
                }

                if (typeName.StartsWith ("System.Windows.Forms.ImageListStreamer", StringComparison.Ordinal))
                {
                    var data = ReadByteArray (record, "Data");
                    if (data is null)
                        return null;
                    var frames = ImageListStreamDecoder.Decode (data);
                    if (frames.Count == 0)
                        return null;
                    var size = new System.Drawing.Size (frames[0].Width, frames[0].Height);
                    return new ImageListStreamer (frames, size);
                }

                return null;
            }
            catch
            {
                return null;   // malformed payload or an unexpected NRBF shape — not fatal.
            }
        }

        private static byte[]? ReadByteArray (ClassRecord record, string memberName)
        {
            if (!record.HasMember (memberName))
                return null;
            return record.GetArrayRecord (memberName) is SZArrayRecord<byte> array
                ? array.GetArray ()
                : null;
        }
    }
}
