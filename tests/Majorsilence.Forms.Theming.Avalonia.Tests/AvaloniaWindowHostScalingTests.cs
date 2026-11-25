using System;
using System.Threading.Tasks;
using Xunit;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    using global::Avalonia;
    using global::Avalonia.Headless;
    using global::Avalonia.Input;
    using global::Avalonia.Controls;
    using global::Avalonia.Platform;

    // #731: on a Retina display (RenderScaling 2) a Form shown through the Avalonia desktop host drew
    // everything twice its size while pointer input still hit-tested the real layout, so a button was
    // highlighted from halfway to where it appeared and a click on it did nothing. The host tagged its
    // physical-pixel framebuffer 96 * scaling DPI, and Avalonia's Image then showed only the top-left
    // 1/scaling of it, stretched over the window. These capture what the window actually composites at a
    // scale other than 1 and check the control appears at its own size, where the pointer finds it.
    public class AvaloniaWindowHostScalingTests
    {
        private static readonly System.Drawing.Color Marker = System.Drawing.Color.FromArgb (255, 0x12, 0xAB, 0x34);

        private const int PanelWidth = 40;
        private const int PanelHeight = 20;

        [Theory]
        [InlineData (2.0, 1.0)]    // a Retina display
        [InlineData (1.5, 1.0)]    // Windows at 150%
        [InlineData (2.0, 1.5)]    // a Retina display with the app zoomed through Application.UiScale
        public Task A_control_draws_at_its_own_size_where_the_pointer_hits_it (double renderScaling, double uiScale) => Headless.Run (() => {
            var previousUiScale = MF.Application.UiScale;
            var form = new MF.Form { ClientSize = new System.Drawing.Size (300, 200) };
            var panel = new MF.Panel {
                Location = new System.Drawing.Point (60, 40),
                Size = new System.Drawing.Size (PanelWidth, PanelHeight),
                BackColor = Marker,
            };
            System.Drawing.Point? pressedAt = null;
            panel.MouseDown += (_, e) => pressedAt = e.Location;
            form.Controls.Add (panel);

            try {
                MF.Application.UiScale = uiScale;
                form.Show ();

                var host = Assert.IsType<MF.MajorsilenceFormsWindowHost> (form.Backend);
                host.SetRenderScaling (renderScaling);
                host.UpdateLayout ();
                host.IsDirty = true;
                host.PaintFrame ();

                var marker = FindMarker (host);
                Assert.True (marker.HasValue, "the panel was not drawn anywhere in the window");
                var box = marker!.Value;

                // The panel's logical size, through the window's whole scale, in the window's physical
                // pixels. Before the fix this came out renderScaling times too big.
                var scale = renderScaling * uiScale;
                Assert.InRange (box.Width, (int)(PanelWidth * scale) - 2, (int)(PanelWidth * scale) + 2);
                Assert.InRange (box.Height, (int)(PanelHeight * scale) - 2, (int)(PanelHeight * scale) + 2);

                // Pressing the middle of what is drawn presses the panel, at the middle of the panel.
                var centre = new Point ((box.Left + box.Width / 2.0) / renderScaling, (box.Top + box.Height / 2.0) / renderScaling);
                host.MouseDown (centre, MouseButton.Left);
                host.MouseUp (centre, MouseButton.Left);

                Assert.NotNull (pressedAt);
                Assert.InRange (pressedAt!.Value.X, PanelWidth / 2 - 2, PanelWidth / 2 + 2);
                Assert.InRange (pressedAt!.Value.Y, PanelHeight / 2 - 2, PanelHeight / 2 + 2);
            } finally {
                form.Close ();
                form.Dispose ();
                MF.Application.UiScale = previousUiScale;
            }
        });

        // The bounding box, in the captured frame's physical pixels, of every pixel painted Marker.
        private static System.Drawing.Rectangle? FindMarker (TopLevel window)
        {
            using var frame = window.CaptureRenderedFrame () ?? throw new InvalidOperationException ("no frame was rendered");

            var width = frame.PixelSize.Width;
            var height = frame.PixelSize.Height;
            var stride = width * 4;
            var pixels = new byte[stride * height];

            var handle = System.Runtime.InteropServices.GCHandle.Alloc (pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try {
                frame.CopyPixels (new PixelRect (frame.PixelSize), handle.AddrOfPinnedObject (), pixels.Length, stride);
            } finally {
                handle.Free ();
            }

            var redFirst = frame.Format == PixelFormat.Rgba8888;
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;

            for (var y = 0; y < height; y++) {
                for (var x = 0; x < width; x++) {
                    var i = y * stride + x * 4;
                    var r = redFirst ? pixels[i] : pixels[i + 2];
                    var g = pixels[i + 1];
                    var b = redFirst ? pixels[i + 2] : pixels[i];

                    if (r != Marker.R || g != Marker.G || b != Marker.B)
                        continue;

                    left = Math.Min (left, x);
                    top = Math.Min (top, y);
                    right = Math.Max (right, x);
                    bottom = Math.Max (bottom, y);
                }
            }

            return right < 0 ? null : System.Drawing.Rectangle.FromLTRB (left, top, right + 1, bottom + 1);
        }
    }
}
