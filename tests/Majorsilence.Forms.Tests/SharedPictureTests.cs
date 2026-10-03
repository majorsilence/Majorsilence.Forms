using System;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #371: on Android the single-view host recorded a new SKPicture every frame and left each retired one to the finalizer, so an animated
    // control piled up native memory the .NET GC never saw until the system killed the process. A picture is now shared by the view holding
    // the current one and by every draw op built from it, and disposed when the last lets go.
    public class SharedPictureTests
    {
        private static SKPicture Record ()
        {
            using var recorder = new SKPictureRecorder ();
            recorder.BeginRecording (new SKRect (0, 0, 10, 10)).Clear (SKColors.Red);
            return recorder.EndRecording ();
        }

        [Fact]
        public void The_picture_is_disposed_when_its_only_reference_is_released ()
        {
            var shared = new SharedPicture (Record ());
            Assert.False (shared.IsDisposed);

            shared.Release ();

            Assert.True (shared.IsDisposed);
            Assert.Throws<ObjectDisposedException> (() => shared.Picture);
        }

        [Fact]
        public void A_draw_op_still_holding_a_replaced_picture_keeps_it_alive ()
        {
            // The view lets go of the replaced picture while a render-thread draw op is still playing it: the picture must survive until
            // the op lets go too, and not a moment longer.
            var shared = new SharedPicture (Record ());
            var op = shared.Retain ();
            shared.Release ();                       // the view replaced it

            Assert.False (op.IsDisposed);
            Assert.NotNull (op.Picture);

            op.Release ();                           // the op was disposed

            Assert.True (shared.IsDisposed);
        }

        [Fact]
        public void Releasing_more_than_was_taken_never_disposes_early ()
        {
            var shared = new SharedPicture (Record ());
            shared.Retain ().Retain ();

            shared.Release ();
            shared.Release ();
            Assert.False (shared.IsDisposed);

            shared.Release ();
            Assert.True (shared.IsDisposed);
        }

        [Fact]
        public void Retaining_a_released_picture_throws ()
        {
            var shared = new SharedPicture (Record ());
            shared.Release ();

            Assert.Throws<ObjectDisposedException> (() => shared.Retain ());
        }
    }
}
