using System;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Defines a surface that text and graphics can be rendered to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart of <c>System.Drawing.IDeviceContext</c>, which lives in
    /// <c>System.Drawing.Common</c> — an assembly this layer deliberately does not reference, since
    /// being independent of it is the point. Reimplemented here for the same reason every other
    /// <c>System.Drawing</c> type is.
    /// </para>
    /// <para>
    /// It exists mainly because <c>TextRenderer</c>'s entire public surface is declared in terms of it
    /// upstream: <c>TextRenderer.DrawText (IDeviceContext, ...)</c>, not <c>(Graphics, ...)</c>. Code
    /// that passes a <c>Majorsilence.Forms.Drawing.Graphics</c> never names the interface, but code
    /// that declares a helper taking one does, and that helper has to compile.
    /// </para>
    /// <para>
    /// The HDC members are part of the shape and are honoured literally: there is no GDI device
    /// context behind a Skia canvas, so <see cref="GetHdc"/> returns <see cref="IntPtr.Zero"/>. That
    /// is the truthful answer rather than a thrown exception, because callers overwhelmingly pass the
    /// handle straight back to a P/Invoke that is itself absent on this platform.
    /// </para>
    /// </remarks>
    public interface IDeviceContext : IDisposable
    {
        /// <summary>Returns the Win32 device context handle, or <see cref="IntPtr.Zero"/> when there is none.</summary>
        IntPtr GetHdc ();

        /// <summary>Releases a handle previously obtained from <see cref="GetHdc"/>.</summary>
        void ReleaseHdc ();
    }
}
