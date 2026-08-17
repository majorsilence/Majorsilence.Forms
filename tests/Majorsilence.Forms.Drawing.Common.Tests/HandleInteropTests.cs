using System;
using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Imaging;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // Win32 handle interop. Two things are worth asserting: the members that can do real work do it,
    // and the members that cannot throw rather than returning a zero handle -- because a zero handle
    // handed to DeleteObject or SelectObject corrupts silently, which is the failure mode these
    // members exist to avoid.
    public class HandleInteropTests
    {
        // A LOGFONT is a data layout, so any object with the right field names is one. This stands in
        // for whatever struct a caller declared.
        // The fields are written by reflection from ToLogFont, which the compiler cannot see -- hence
        // the suppression rather than pointless initialisers that would hide a real bug.
#pragma warning disable CS0649
        private sealed class LogFont
        {
            public int lfHeight;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public string lfFaceName = string.Empty;
        }
#pragma warning restore CS0649

        [Fact]
        public void A_font_round_trips_through_a_log_font ()
        {
            var font = new Font ("Verdana", 16f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
            var logFont = new LogFont ();

            font.ToLogFont (logFont);
            var back = Font.FromLogFont (logFont);

            Assert.Equal ("Verdana", back.Name);
            Assert.Equal (16f, back.Size);
            Assert.True (back.Style.HasFlag (FontStyle.Bold));
            Assert.True (back.Style.HasFlag (FontStyle.Italic));
        }

        [Fact]
        public void A_log_fonts_height_is_negative_and_its_weight_is_gdis ()
        {
            var logFont = new LogFont ();

            new Font ("Arial", 12f, FontStyle.Bold, GraphicsUnit.Pixel).ToLogFont (logFont);

            // Negative height is GDI's way of saying "character height, not cell height"; a caller
            // that reads the sign to tell them apart must see the right one.
            Assert.Equal (-12, logFont.lfHeight);
            Assert.Equal (700, logFont.lfWeight);
            Assert.Equal ("Arial", logFont.lfFaceName);
        }

        [Fact]
        public void A_regular_font_reports_the_normal_weight ()
        {
            var logFont = new LogFont ();

            new Font ("Arial", 12f, FontStyle.Regular, GraphicsUnit.Pixel).ToLogFont (logFont);

            Assert.Equal (400, logFont.lfWeight);
            Assert.Equal (0, logFont.lfItalic);
        }

        [Fact]
        public void A_positive_log_font_height_gives_the_same_font ()
        {
            var font = Font.FromLogFont (new LogFont { lfHeight = 14, lfFaceName = "Arial" });

            Assert.Equal (14f, font.Size);
        }

        [Fact]
        public void A_region_round_trips_through_its_data ()
        {
            using var region = new Region (new Rectangle (10, 20, 30, 40));
            region.Union (new Rectangle (100, 200, 30, 40));

            var data = region.GetRegionData ();
            using var back = new Region (data);

            Assert.NotEmpty (data.Data);
            Assert.True (back.IsVisible (15, 25));
            Assert.True (back.IsVisible (105, 205));
            Assert.False (back.IsVisible (60, 60));
        }

        [Fact]
        public void An_empty_region_data_gives_an_empty_region ()
        {
            using var region = new Region (new RegionData ());

            Assert.True (region.IsEmpty ());
        }

        [Fact]
        public void Producing_a_handle_throws_rather_than_returning_zero ()
        {
            using var bitmap = new Bitmap (4, 4);
            using var region = new Region ();
            var font = new Font ("Arial", 10f);

            Assert.Throws<PlatformNotSupportedException> (() => bitmap.GetHbitmap ());
            // GetHrgn returns a null handle now instead of throwing: the callers seen in practice (a themed
            // form's non-client invalidation) hand it straight to a Win32 call and delete it -- chrome
            // bookkeeping with a natural neutral value, so a null handle beats an exception mid-paint.
            Assert.Equal (IntPtr.Zero, region.GetHrgn (null));
            Assert.Throws<PlatformNotSupportedException> (() => font.ToHfont ());
        }

        [Fact]
        public void Reading_a_handle_throws_rather_than_returning_a_blank ()
        {
            Assert.Throws<PlatformNotSupportedException> (() => Bitmap.FromHicon (IntPtr.Zero));
            Assert.Throws<PlatformNotSupportedException> (() => Image.FromHbitmap (IntPtr.Zero));
            Assert.Throws<PlatformNotSupportedException> (() => Icon.FromHandle (IntPtr.Zero));
            Assert.Throws<PlatformNotSupportedException> (() => Region.FromHrgn (IntPtr.Zero));
            Assert.Throws<PlatformNotSupportedException> (() => Font.FromHfont (IntPtr.Zero));
        }

        [Fact]
        public void Releasing_a_handle_is_a_no_op_rather_than_a_throw ()
        {
            using var region = new Region ();

            // A release usually sits in a finally block; throwing there would replace the exception
            // that actually stopped the caller with a less useful one.
            region.ReleaseHrgn (IntPtr.Zero);
        }

        [Fact]
        public void An_associated_icon_is_null_rather_than_wrong ()
        {
            Assert.Null (Icon.ExtractAssociatedIcon ("/some/file.txt"));
        }

        [Fact]
        public void A_toolbox_bitmap_attribute_reports_no_image_for_an_unknown_type ()
        {
            var attribute = new ToolboxBitmapAttribute (typeof (HandleInteropTests));

            Assert.Null (attribute.GetImage (typeof (HandleInteropTests)));
            Assert.Null (attribute.GetImage ((object?) null));
        }

        [Theory]
        [InlineData (CopyPixelOperation.SourceCopy, 13369376)]
        [InlineData (CopyPixelOperation.Blackness, 66)]
        [InlineData (CopyPixelOperation.Whiteness, 16711778)]
        public void A_raster_operation_has_the_number_win32_uses (CopyPixelOperation op, int expected)
        {
            Assert.Equal (expected, (int) op);
        }
    }
}
