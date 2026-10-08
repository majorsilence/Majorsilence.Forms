using System;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // SVC-38: Cursor (Stream), Cursor (string), Cursor (Type, string) and Cursor (IntPtr) load and wrap real
    // cursors, and a loaded cursor reaches the window backend as an image with its hotspot. The .cur bytes
    // are built here, by hand, from the container layout (Win32 ICONDIR + ICONDIRENTRY + 32-bit DIB).
    [Collection ("Headless")]
    public sealed class CursorFileLoadingTests
    {
        public CursorFileLoadingTests () => HeadlessRenderer.Use ();

        private static HeadlessWindowHost Host (Form form) => (HeadlessWindowHost) form.Backend;

        private readonly record struct Frame (int Width, int Height, int HotX, int HotY, SKColor Fill, int MarkX = -1, int MarkY = -1);

        private static readonly SKColor mark_color = new SKColor (0x10, 0xC0, 0x30, 0xFF);

        // type 2 = .cur (entry words 4-7 are the hotspot), type 1 = .ico (planes and bit count).
        private static byte[] Container (int type, params Frame[] frames)
        {
            using var output = new MemoryStream ();
            using var writer = new BinaryWriter (output);

            writer.Write ((ushort) 0);
            writer.Write ((ushort) type);
            writer.Write ((ushort) frames.Length);

            var dibs = new byte[frames.Length][];
            for (var i = 0; i < frames.Length; i++)
                dibs[i] = Dib (frames[i]);

            var offset = 6 + 16 * frames.Length;
            for (var i = 0; i < frames.Length; i++) {
                var f = frames[i];
                writer.Write ((byte) f.Width);
                writer.Write ((byte) f.Height);
                writer.Write ((byte) 0);
                writer.Write ((byte) 0);
                writer.Write ((ushort) (type == 2 ? f.HotX : 1));
                writer.Write ((ushort) (type == 2 ? f.HotY : 32));
                writer.Write (dibs[i].Length);
                writer.Write (offset);
                offset += dibs[i].Length;
            }

            foreach (var dib in dibs)
                writer.Write (dib);

            return output.ToArray ();
        }

        private static byte[] Dib (Frame f)
        {
            using var output = new MemoryStream ();
            using var writer = new BinaryWriter (output);
            var maskStride = (f.Width + 31) / 32 * 4;

            writer.Write (40);
            writer.Write (f.Width);
            writer.Write (f.Height * 2);
            writer.Write ((ushort) 1);
            writer.Write ((ushort) 32);
            writer.Write (0);
            writer.Write (f.Width * f.Height * 4 + maskStride * f.Height);
            writer.Write (0);
            writer.Write (0);
            writer.Write (0);
            writer.Write (0);

            for (var row = f.Height - 1; row >= 0; row--) {
                for (var x = 0; x < f.Width; x++) {
                    var c = x == f.MarkX && row == f.MarkY ? mark_color : f.Fill;
                    writer.Write (c.Blue);
                    writer.Write (c.Green);
                    writer.Write (c.Red);
                    writer.Write (c.Alpha);
                }
            }

            writer.Write (new byte[maskStride * f.Height]);
            return output.ToArray ();
        }

        private static readonly SKColor red = new SKColor (0xE0, 0x20, 0x20, 0xFF);
        private static readonly SKColor blue = new SKColor (0x20, 0x20, 0xE0, 0xFF);

        private static Cursor Load (byte[] bytes) => new Cursor (new MemoryStream (bytes));

        // ── Decoding ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_cur_from_a_stream_has_its_frames_size_pixels_and_hotspot ()
        {
            using var cursor = Load (Container (2, new Frame (48, 40, 5, 7, red, MarkX: 3, MarkY: 2)));

            Assert.NotNull (cursor.Image);
            Assert.Equal (48, cursor.Image!.Width);
            Assert.Equal (40, cursor.Image.Height);
            Assert.Equal (new Point (5, 7), cursor.HotSpot);

            // The pixels are the file's, the right way up.
            Assert.Equal (red, cursor.Image.GetPixel (0, 0));
            Assert.Equal (mark_color, cursor.Image.GetPixel (3, 2));

            // Upstream's Size is the system cursor size, whatever the image.
            Assert.Equal (SystemInformation.CursorSize, cursor.Size);
        }

        [Fact]
        public void The_frame_for_the_system_cursor_size_is_used_with_its_own_hotspot ()
        {
            using var cursor = Load (Container (2,
                new Frame (16, 16, 1, 2, red),
                new Frame (32, 32, 10, 12, blue),
                new Frame (64, 64, 30, 31, red)));

            Assert.Equal (32, cursor.Image!.Width);
            Assert.Equal (blue, cursor.Image.GetPixel (4, 4));
            Assert.Equal (new Point (10, 12), cursor.HotSpot);
        }

        [Fact]
        public void An_ico_loads_as_a_cursor_that_tracks_the_pointer_at_its_centre ()
        {
            using var cursor = Load (Container (1, new Frame (32, 32, 0, 0, blue)));

            Assert.Equal (32, cursor.Image!.Width);
            Assert.Equal (new Point (16, 16), cursor.HotSpot);
        }

        [Fact]
        public void A_seekable_stream_is_read_from_its_start ()
        {
            var stream = new MemoryStream (Container (2, new Frame (32, 32, 3, 4, red)));
            stream.Position = stream.Length;

            using var cursor = new Cursor (stream);

            Assert.Equal (new Point (3, 4), cursor.HotSpot);
        }

        [Fact]
        public void A_cur_file_loads_by_name ()
        {
            var path = Path.Combine (Path.GetTempPath (), $"mf-cursor-{Guid.NewGuid ():N}.cur");
            File.WriteAllBytes (path, Container (2, new Frame (32, 32, 9, 8, red)));

            try {
                using var cursor = new Cursor (path);
                Assert.Equal (new Point (9, 8), cursor.HotSpot);
                Assert.Equal (32, cursor.Image!.Height);
            } finally {
                File.Delete (path);
            }
        }

        // ── Bad data: upstream's exceptions ─────────────────────────────────────────────────────

        public static TheoryData<byte[]> NotACursor => new () {
            Array.Empty<byte> (),
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            // A PNG: an image, but not a picture that can be a cursor.
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D },
            // A cursor header with no frames.
            new byte[] { 0, 0, 2, 0, 0, 0 },
            // A cursor whose one entry points past the end of the data.
            Truncated (),
        };

        private static byte[] Truncated ()
        {
            var bytes = Container (2, new Frame (32, 32, 1, 1, red));
            Array.Resize (ref bytes, 6 + 16 + 20);
            return bytes;
        }

        [Theory]
        [MemberData (nameof (NotACursor))]
        public void Data_that_is_not_a_cursor_throws_ArgumentException_naming_the_stream (byte[] data)
        {
            var ex = Assert.Throws<ArgumentException> (() => new Cursor (new MemoryStream (data)));
            Assert.Equal ("stream", ex.ParamName);
        }

        [Fact]
        public void A_file_that_is_not_a_cursor_throws_ArgumentException_naming_the_file ()
        {
            var path = Path.Combine (Path.GetTempPath (), $"mf-cursor-{Guid.NewGuid ():N}.cur");
            File.WriteAllBytes (path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            try {
                var ex = Assert.Throws<ArgumentException> (() => new Cursor (path));
                Assert.Equal ("fileName", ex.ParamName);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void Missing_input_throws_as_upstream ()
        {
            Assert.Equal ("stream", Assert.Throws<ArgumentNullException> (() => new Cursor ((Stream) null!)).ParamName);
            Assert.Throws<FileNotFoundException> (() => new Cursor (Path.Combine (Path.GetTempPath (), $"missing-{Guid.NewGuid ():N}.cur")));
            Assert.Equal ("type", Assert.Throws<ArgumentNullException> (() => new Cursor ((Type) null!, "x.cur")).ParamName);

            // A resource that is not there: upstream hands the null stream on to Cursor (Stream).
            Assert.Equal ("stream", Assert.Throws<ArgumentNullException> (() => new Cursor (typeof (CursorFileLoadingTests), "no-such.cur")).ParamName);
        }

        // ── Reaching the window ─────────────────────────────────────────────────────────────────

        [Fact]
        public void A_loaded_cursor_on_a_control_reaches_the_window_as_its_image_and_hotspot ()
        {
            using var cursor = Load (Container (2, new Frame (32, 32, 6, 11, red)));
            using var form = new Form { Width = 300, Height = 200 };
            var button = new Button { Left = 20, Top = 20, Width = 100, Height = 30, Cursor = cursor };
            form.Controls.Add (button);
            form.Show ();

            var at = button.GetPositionInForm ();
            HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);
            HeadlessRenderer.MouseMove (form, at.X + 6, at.Y + 6);

            Assert.Same (cursor.Image, Host (form).CustomCursor);
            Assert.Equal (new Point (6, 11), Host (form).CustomCursorHotSpot);

            // Off the button, the form's own cursor comes back and the custom one is gone.
            HeadlessRenderer.MouseMove (form, 250, 150);
            HeadlessRenderer.MouseMove (form, 251, 151);
            Assert.Null (Host (form).CustomCursor);
            Assert.Equal (CursorType.Arrow, Host (form).Cursor);
        }

        [Fact]
        public void A_loaded_cursor_on_the_form_reaches_the_window ()
        {
            using var cursor = Load (Container (2, new Frame (32, 32, 2, 3, blue)));
            using var form = new Form { Width = 300, Height = 200 };
            form.Show ();

            form.Cursor = cursor;

            Assert.Same (cursor.Image, Host (form).CustomCursor);
            Assert.Equal (new Point (2, 3), Host (form).CustomCursorHotSpot);
        }

        [Fact]
        public void Hiding_wins_over_a_loaded_cursor_and_Show_brings_it_back ()
        {
            using var cursor = Load (Container (2, new Frame (32, 32, 2, 3, blue)));
            using var form = new Form { Width = 300, Height = 200 };
            var button = new Button { Left = 20, Top = 20, Width = 100, Height = 30, Cursor = cursor };
            form.Controls.Add (button);
            form.Show ();

            var at = button.GetPositionInForm ();
            HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);
            HeadlessRenderer.MouseMove (form, at.X + 6, at.Y + 6);
            Assert.Same (cursor.Image, Host (form).CustomCursor);

            Cursor.Hide ();
            try {
                Assert.Null (Host (form).CustomCursor);
                Assert.Equal (CursorType.None, Host (form).Cursor);

                // The mouse-move path would put the button's cursor back; hidden, it must not.
                HeadlessRenderer.MouseMove (form, at.X + 7, at.Y + 7);
                Assert.Null (Host (form).CustomCursor);
                Assert.Equal (CursorType.None, Host (form).Cursor);
            } finally {
                Cursor.Show ();
            }

            Assert.Same (cursor.Image, Host (form).CustomCursor);
        }

        [Fact]
        public void A_disposed_cursor_shows_the_arrow_and_has_no_handle ()
        {
            var cursor = Load (Container (2, new Frame (32, 32, 2, 3, blue)));
            using var form = new Form { Width = 300, Height = 200, Cursor = cursor };
            form.Show ();
            Assert.NotNull (Host (form).CustomCursor);

            cursor.Dispose ();
            form.HandlePointerMoved (MouseButtons.None, 30, 30, Keys.None);

            Assert.Null (Host (form).CustomCursor);
            Assert.Equal (CursorType.Arrow, Host (form).Cursor);
            Assert.Throws<ObjectDisposedException> (() => cursor.Handle);
        }

        // ── Handles and equality ────────────────────────────────────────────────────────────────

        [Fact]
        public void Each_loaded_cursor_is_its_own_cursor_and_its_handle_gives_it_back ()
        {
            var bytes = Container (2, new Frame (32, 32, 4, 5, red));
            using var first = Load (bytes);
            using var second = Load (bytes);

            // Upstream compares HCURSORs, and every load makes a new one.
            Assert.NotEqual (first, second);
            Assert.NotEqual (first.Handle, second.Handle);
            Assert.NotEqual (IntPtr.Zero, first.Handle);

            using var wrapped = new Cursor (first.Handle);
            Assert.Equal (first, wrapped);
            Assert.True (first == wrapped);
            Assert.Equal (first.GetHashCode (), wrapped.GetHashCode ());
            Assert.Equal (first.HotSpot, wrapped.HotSpot);
            Assert.Same (first.Image, wrapped.Image);

            // Disposing the wrapper does not free the loading cursor's image, as upstream's wrapper does
            // not own the HCURSOR.
            wrapped.Dispose ();
            Assert.NotNull (first.Image);

            Assert.NotEqual (Cursors.Arrow, first);
            Assert.StartsWith ("[Cursor: ", first.ToString ());
        }

        [Fact]
        public void The_standard_cursors_have_distinct_handles_that_give_them_back ()
        {
            Assert.NotEqual (Cursors.Hand.Handle, Cursors.Arrow.Handle);
            Assert.Equal (Cursors.Hand, new Cursor (Cursors.Hand.Handle));
            Assert.Equal (CursorType.Hand, new Cursor (Cursors.Hand.Handle).CursorType);

            // A zero handle (Bitmap.GetHicon, Icon.Handle here) is the default cursor rather than upstream's
            // ArgumentException.
            Assert.Equal (Cursors.Default, new Cursor (IntPtr.Zero));

            // A handle that is not one of ours is kept, compares by itself, and shows the arrow.
            var foreign = new Cursor (new IntPtr (0x1234));
            Assert.Equal (new IntPtr (0x1234), foreign.Handle);
            Assert.Equal (new Cursor (new IntPtr (0x1234)), foreign);
            Assert.NotEqual (Cursors.Arrow, foreign);
            Assert.Equal (CursorType.Arrow, foreign.CursorType);
        }

        [Fact]
        public void The_encoder_round_trips_through_the_decoder ()
        {
            // WPF and the WinForms host build their native cursor from CursorFile.Encode; they cannot run
            // here, so this pins that what they are handed decodes back to the same image and hotspot.
            using var cursor = Load (Container (2, new Frame (32, 32, 7, 9, red, MarkX: 30, MarkY: 1)));
            var encoded = CursorFile.Encode (cursor.Image!, cursor.HotSpot);

            using var again = Load (encoded);
            Assert.Equal (new Point (7, 9), again.HotSpot);
            Assert.Equal (mark_color, again.Image!.GetPixel (30, 1));
            Assert.Equal (red, again.Image.GetPixel (0, 31));
        }
    }
}
