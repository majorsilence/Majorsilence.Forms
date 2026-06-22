using System;
using System.Data.SqlTypes;
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
    /// It also recovers the design-time component values WinForms commonly serialized into a form's
    /// <c>.resx</c> — <c>System.Data.SqlTypes</c> scalars (a <c>SqlCommand</c>'s parameter defaults) and
    /// <c>DBNull</c> (serialized via <c>UnitySerializationHolder</c>).
    ///
    /// Anything else (arbitrary serialized objects, ActiveX <c>AxHost+State</c>, …) returns
    /// <see langword="null"/> — genuinely not portable, and left for manual handling.
    /// </summary>
    internal static class NrbfResourceReader
    {
        public static object? TryReadObject (byte[] blob)
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

                if (typeName.StartsWith ("System.Data.SqlTypes.", StringComparison.Ordinal))
                    return TryReadSqlType (record, typeName);

                if (typeName.StartsWith ("System.UnitySerializationHolder", StringComparison.Ordinal))
                    return TryReadUnityHolder (record);

                return null;
            }
            catch
            {
                return null;   // malformed payload or an unexpected NRBF shape — not fatal.
            }
        }

        // System.Data.SqlTypes scalars store a not-null flag plus the raw value. We rebuild the common
        // ones faithfully (a SqlCommand's design-time parameter defaults); exotic ones return null.
        private static object? TryReadSqlType (ClassRecord record, string typeName)
        {
            bool NotNull () => !record.HasMember ("m_fNotNull") || record.GetBoolean ("m_fNotNull");

            try
            {
                return typeName["System.Data.SqlTypes.".Length..] switch
                {
                    "SqlInt32" => NotNull () ? new SqlInt32 (record.GetInt32 ("m_value")) : SqlInt32.Null,
                    "SqlInt16" => NotNull () ? new SqlInt16 (record.GetInt16 ("m_value")) : SqlInt16.Null,
                    "SqlInt64" => NotNull () ? new SqlInt64 (record.GetInt64 ("m_value")) : SqlInt64.Null,
                    "SqlByte" => NotNull () ? new SqlByte (record.GetByte ("m_value")) : SqlByte.Null,
                    "SqlDouble" => NotNull () ? new SqlDouble (record.GetDouble ("m_value")) : SqlDouble.Null,
                    "SqlSingle" => NotNull () ? new SqlSingle (record.GetSingle ("m_value")) : SqlSingle.Null,
                    "SqlString" => NotNull () ? new SqlString (record.GetString ("m_value")) : SqlString.Null,
                    // SqlBoolean.m_value: 0 = Null, 1 = False, 2 = True.
                    "SqlBoolean" => record.GetByte ("m_value") switch { 0 => SqlBoolean.Null, 2 => SqlBoolean.True, _ => SqlBoolean.False },
                    "SqlDateTime" => NotNull () ? new SqlDateTime (record.GetInt32 ("m_day"), record.GetInt32 ("m_time")) : SqlDateTime.Null,
                    _ => null,   // SqlDecimal/SqlMoney/SqlGuid/… — rare; leave as a design-time default.
                };
            }
            catch
            {
                return null;
            }
        }

        // DBNull (and a few framework singletons) serialize through UnitySerializationHolder. We recover
        // the DBNull case — the usual design-time value of a SqlParameter.Value.
        private static DBNull? TryReadUnityHolder (ClassRecord record)
        {
            try
            {
                // DBNull serializes with a null "Data" member; Type/Assembly/Module holders carry the
                // (non-null) name there. In a form .resx the only value-typed holder is DBNull.
                var data = record.HasMember ("Data") ? record.GetString ("Data") : null;
                if (string.IsNullOrEmpty (data))
                    return DBNull.Value;
            }
            catch { /* a non-string Data member — not the DBNull shape we handle. */ }
            return null;
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
