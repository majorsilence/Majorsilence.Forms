using System;
using System.Data.SqlTypes;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Validates that ComponentResourceManager recovers the design-time SqlParameter values WinForms
// serialized into a form's .resx as BinaryFormatter blobs — without running BinaryFormatter.
public class SqlTypeResourceTests
{
    private static string Resx (string blob) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="p.SqlValue" mimetype="application/x-microsoft.net.object.binary.base64"><value>{blob}</value></data>
        </root>
        """;

    [Fact]
    public void Recovers_SqlInt32 ()
    {
        // The captured parameter's design-time SqlValue is SqlInt32.Null — recovering the typed value
        // (rather than failing) is the point.
        var v = ComponentResourceManager.FromXml (Resx (SqlTypeResourceFixture.SqlInt32)).GetObject ("p.SqlValue");
        Assert.IsType<SqlInt32> (v);
    }

    [Fact]
    public void Recovers_SqlString ()
    {
        var v = ComponentResourceManager.FromXml (Resx (SqlTypeResourceFixture.SqlString)).GetObject ("p.SqlValue");
        Assert.IsType<SqlString> (v);
    }

    [Fact]
    public void Recovers_SqlBoolean ()
    {
        var v = ComponentResourceManager.FromXml (Resx (SqlTypeResourceFixture.SqlBoolean)).GetObject ("p.SqlValue");
        Assert.IsType<SqlBoolean> (v);
    }

    [Fact]
    public void Recovers_DBNull_from_UnitySerializationHolder ()
    {
        var v = ComponentResourceManager.FromXml (Resx (SqlTypeResourceFixture.UnityDbNull)).GetObject ("p.SqlValue");
        Assert.Equal (DBNull.Value, v);
    }
}
