using System;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Drawing.Imaging;
using Majorsilence.Forms.Printing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The metafile family. Recording and playback are a Windows GDI facility, so those throw; reading
    // a header is a fixed binary layout, so that is real -- and these tests build the headers byte by
    // byte rather than asserting a stub, because a parser that is not fed real bytes is not tested.
    public class MetafileParityTests
    {
        private static MemoryStream PlaceableWmf ()
        {
            var bytes = new byte[24];

            BitConverter.TryWriteBytes (bytes.AsSpan (0), 0x9AC6CDD7u);   // placeable magic
            BitConverter.TryWriteBytes (bytes.AsSpan (6), (short) 10);    // bbox left
            BitConverter.TryWriteBytes (bytes.AsSpan (8), (short) 20);    // bbox top
            BitConverter.TryWriteBytes (bytes.AsSpan (10), (short) 110);  // bbox right
            BitConverter.TryWriteBytes (bytes.AsSpan (12), (short) 220);  // bbox bottom
            BitConverter.TryWriteBytes (bytes.AsSpan (18), (short) 1440); // units per inch

            return new MemoryStream (bytes);
        }

        private static MemoryStream Emf ()
        {
            var bytes = new byte[88];

            BitConverter.TryWriteBytes (bytes.AsSpan (0), 1);              // EMR_HEADER
            BitConverter.TryWriteBytes (bytes.AsSpan (8), 0);              // bounds left
            BitConverter.TryWriteBytes (bytes.AsSpan (12), 0);             // bounds top
            BitConverter.TryWriteBytes (bytes.AsSpan (16), 640);           // bounds right
            BitConverter.TryWriteBytes (bytes.AsSpan (20), 480);           // bounds bottom
            BitConverter.TryWriteBytes (bytes.AsSpan (40), 0x464D4520u);   // " EMF"
            BitConverter.TryWriteBytes (bytes.AsSpan (44), 0x10000);       // version
            BitConverter.TryWriteBytes (bytes.AsSpan (48), 4096);          // file size
            BitConverter.TryWriteBytes (bytes.AsSpan (72), 1920);          // device pixels
            BitConverter.TryWriteBytes (bytes.AsSpan (80), 508);           // device millimetres

            return new MemoryStream (bytes);
        }

        [Fact]
        public void A_placeable_wmf_header_is_read ()
        {
            using var stream = PlaceableWmf ();
            var header = Metafile.GetMetafileHeader (stream);

            Assert.Equal (MetafileType.WmfPlaceable, header.Type);
            Assert.True (header.IsWmf ());
            Assert.True (header.IsWmfPlaceable ());
            Assert.False (header.IsEmf ());
            Assert.Equal (1440f, header.DpiX);
            Assert.Equal (new Rectangle (10, 20, 100, 200), header.Bounds);
        }

        [Fact]
        public void An_emf_header_is_read ()
        {
            using var stream = Emf ();
            var header = Metafile.GetMetafileHeader (stream);

            Assert.Equal (MetafileType.Emf, header.Type);
            Assert.True (header.IsEmf ());
            Assert.True (header.IsEmfOrEmfPlus ());
            Assert.False (header.IsEmfPlus ());
            Assert.Equal (4096, header.MetafileSize);
            Assert.Equal (new Rectangle (0, 0, 640, 480), header.Bounds);

            // 1920 device pixels over 508 millimetres is 96 dots per inch.
            Assert.Equal (96f, header.DpiX, 1);
        }

        [Fact]
        public void Bytes_that_are_neither_report_invalid ()
        {
            using var stream = new MemoryStream (new byte[88]);

            Assert.Equal (MetafileType.Invalid, Metafile.GetMetafileHeader (stream).Type);
        }

        [Fact]
        public void A_metafile_built_from_a_stream_carries_its_header ()
        {
            using var stream = Emf ();
            var metafile = new Metafile (stream);

            Assert.Equal (MetafileType.Emf, metafile.GetMetafileHeader ().Type);
        }

        [Fact]
        public void Recording_a_metafile_throws_rather_than_recording_nothing ()
        {
            Assert.Throws<PlatformNotSupportedException> (
                () => new Metafile (IntPtr.Zero, EmfType.EmfOnly));
            Assert.Throws<PlatformNotSupportedException> (
                () => new Metafile ("out.emf", IntPtr.Zero, new Rectangle (0, 0, 10, 10)));
        }

        [Fact]
        public void Playing_a_record_throws ()
        {
            using var stream = Emf ();
            var metafile = new Metafile (stream);

            Assert.Throws<PlatformNotSupportedException> (
                () => metafile.PlayRecord (EmfPlusRecordType.Header, 0, 0, []));
            Assert.Throws<PlatformNotSupportedException> (() => metafile.GetHenhmetafile ());
        }

        [Fact]
        public void A_metafile_comment_is_a_no_op_rather_than_a_throw ()
        {
            using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (4, 4);
            using var graphics = Graphics.FromImage (bitmap);

            // Upstream also does nothing when the surface is not recording, so a caller that comments
            // its drawing code behaves identically on both.
            graphics.AddMetafileComment ([1, 2, 3]);
        }

        [Fact]
        public void A_surface_reports_its_offset_and_clip ()
        {
            using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (100, 50);
            using var graphics = Graphics.FromImage (bitmap);

            graphics.GetContextInfo (out var offset, out var clip);

            Assert.Equal (graphics.ClipBounds.X, offset.X);
            Assert.NotNull (clip);
        }

        [Fact]
        public void The_devmode_members_throw_rather_than_returning_a_zero_handle ()
        {
            var printer = new PrinterSettings ();
            var page = new PageSettings ();

            Assert.Throws<PlatformNotSupportedException> (() => printer.GetHdevmode ());
            Assert.Throws<PlatformNotSupportedException> (() => printer.GetHdevnames ());
            Assert.Throws<PlatformNotSupportedException> (() => printer.SetHdevmode (IntPtr.Zero));
            Assert.Throws<PlatformNotSupportedException> (() => page.CopyToHdevmode (IntPtr.Zero));
        }

        [Fact]
        public void Margins_round_trip_through_their_converter ()
        {
            var converter = new MarginsConverter ();
            var margins = new Margins (10, 20, 30, 40);

            var text = Assert.IsType<string> (
                converter.ConvertTo (null, System.Globalization.CultureInfo.InvariantCulture, margins, typeof (string)));
            var back = Assert.IsType<Margins> (
                converter.ConvertFrom (null, System.Globalization.CultureInfo.InvariantCulture, text));

            // Left, Right, Top, Bottom -- Margins' own order, not a clockwise CSS-shaped guess.
            Assert.Equal (10, back.Left);
            Assert.Equal (20, back.Right);
            Assert.Equal (30, back.Top);
            Assert.Equal (40, back.Bottom);
        }
    }
}
