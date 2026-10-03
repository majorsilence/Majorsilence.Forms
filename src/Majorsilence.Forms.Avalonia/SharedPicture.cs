using System;
using System.Threading;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A recorded scene <see cref="SKPicture"/> with a reference count, disposed when the last holder releases it. The single-view host
    /// records a picture every frame on the UI thread and Avalonia plays it on the render thread, possibly after the next one has replaced
    /// it, so neither side can dispose it alone; and left to the finalizer a picture that holds copies of the controls' back buffers piled
    /// up native memory the .NET GC never saw (#371).
    /// </summary>
    internal sealed class SharedPicture
    {
        private SKPicture? _picture;
        private int _references = 1;   // the creator's

        public SharedPicture (SKPicture picture) => _picture = picture ?? throw new ArgumentNullException (nameof (picture));

        /// <summary>The picture; throws once every reference has been released.</summary>
        public SKPicture Picture => _picture ?? throw new ObjectDisposedException (nameof (SharedPicture));

        /// <summary>Whether the picture has been disposed.</summary>
        public bool IsDisposed => _picture is null;

        /// <summary>Takes a reference. Only valid while the caller, or someone it knows holds one, still holds it.</summary>
        public SharedPicture Retain ()
        {
            ObjectDisposedException.ThrowIf (Interlocked.Increment (ref _references) <= 1, this);

            return this;
        }

        /// <summary>Gives a reference up; the last one disposes the picture.</summary>
        public void Release ()
        {
            if (Interlocked.Decrement (ref _references) == 0)
                Interlocked.Exchange (ref _picture, null)?.Dispose ();
        }
    }
}
