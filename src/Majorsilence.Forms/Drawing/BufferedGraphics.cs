// Stays in Majorsilence.Forms rather than moving to Majorsilence.Forms.Drawing.Common with the rest of
// the GDI+ layer: every member here is typed on Majorsilence.Forms.Graphics, which is itself pinned to
// this assembly (Graphics.cs declares a partial of Control and calls Theme/TextMeasurer). Moving this
// file would make Majorsilence.Forms.Drawing.Common depend on Majorsilence.Forms -- a circular reference.
using System;
using System.Drawing;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// A drawing buffer used for double-buffering. Cross-platform replacement for
    /// System.Drawing.BufferedGraphics: drawing goes to an offscreen <see cref="Bitmap"/> via
    /// <see cref="Graphics"/>, and <see cref="Render()"/> blits it to the target surface in one step.
    /// </summary>
    public sealed partial class BufferedGraphics : IDisposable
    {
        private readonly Bitmap buffer;
        private readonly Majorsilence.Forms.Graphics? target;
        private bool disposed;

        internal BufferedGraphics (Bitmap buffer, Majorsilence.Forms.Graphics? target)
        {
            this.buffer = buffer;
            this.target = target;
            Graphics = Majorsilence.Forms.Graphics.FromImage (buffer);
        }

        /// <summary>Gets the <see cref="Graphics"/> that draws onto the offscreen buffer.</summary>
        public Majorsilence.Forms.Graphics Graphics { get; }

        /// <summary>Writes the buffer to the target surface supplied when it was allocated.</summary>
        public void Render () => target?.DrawImage (buffer, 0, 0);

        /// <summary>Writes the buffer to the specified target surface.</summary>
        public void Render (Majorsilence.Forms.Graphics targetGraphics) => targetGraphics?.DrawImage (buffer, 0, 0);

        /// <summary>Releases the buffer and its graphics.</summary>
        public void Dispose ()
        {
            if (disposed)
                return;
            disposed = true;
            Graphics.Dispose ();
            buffer.Dispose ();
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>
    /// Provides methods for creating graphics buffers. Cross-platform replacement for
    /// System.Drawing.BufferedGraphicsContext.
    /// </summary>
    public sealed class BufferedGraphicsContext : IDisposable
    {
        /// <summary>Gets or sets the maximum size of the buffer (advisory; not enforced).</summary>
        public System.Drawing.Size MaximumBuffer { get; set; } = new (3000, 3000);

        /// <summary>Allocates a buffer of the given size for double-buffered drawing onto the target.</summary>
        public BufferedGraphics Allocate (Majorsilence.Forms.Graphics targetGraphics, Rectangle targetRectangle)
        {
            var width = Math.Max (1, targetRectangle.Width);
            var height = Math.Max (1, targetRectangle.Height);
            return new BufferedGraphics (new Bitmap (width, height), targetGraphics);
        }

        /// <summary>
        /// Discards any cached buffer so the next <see cref="Allocate"/> builds a fresh one.
        /// </summary>
        /// <remarks>
        /// A no-op here, and honestly so rather than by omission: <see cref="Allocate"/> already
        /// creates a new bitmap on every call, so there is no retained buffer to invalidate. It exists
        /// because System.Drawing code calls it after a resize.
        /// </remarks>
        public void Invalidate () { }

        /// <summary>Releases the resources used by this context. No-op in Majorsilence.Forms.Drawing.</summary>
        public void Dispose () => GC.SuppressFinalize (this);
    }

    /// <summary>
    /// Provides access to the default <see cref="BufferedGraphicsContext"/>. Cross-platform replacement
    /// for System.Drawing.BufferedGraphicsManager.
    /// </summary>
    public static class BufferedGraphicsManager
    {
        private static readonly BufferedGraphicsContext current = new ();

        /// <summary>Gets the default buffered-graphics context for the application.</summary>
        public static BufferedGraphicsContext Current => current;
    }
}
