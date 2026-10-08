// Lives in Majorsilence.Forms beside Graphics rather than with IDeviceContext in Drawing.Common: its one
// member is typed on Graphics, which is pinned to this assembly (see BufferedGraphics.cs).
namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// An <see cref="IDeviceContext"/> that can hand over the <see cref="Graphics"/> it draws through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream, <c>TextRenderer</c> draws on any <see cref="IDeviceContext"/> by asking it for an HDC
    /// (<c>DeviceContextHdcScope</c>, which takes the graphics from the internal
    /// <c>IGraphicsHdcProvider</c> when the context offers one). There is no HDC behind a Skia canvas,
    /// so this interface is that route made public: a wrapper that implements
    /// <see cref="IDeviceContext"/> over a <see cref="Graphics"/> -- the way Krypton and several
    /// commercial suites interpose on painting -- implements this too, and
    /// <see cref="TextRenderer"/> draws through the graphics it returns.
    /// </para>
    /// <para>
    /// A device context that is neither a <see cref="Graphics"/> nor one of these cannot be drawn on,
    /// and <see cref="TextRenderer"/> says so with an <see cref="System.InvalidOperationException"/>
    /// rather than drawing nothing, as upstream does for a context whose HDC is null.
    /// </para>
    /// </remarks>
    public interface IGraphicsDeviceContext : IDeviceContext
    {
        /// <summary>
        /// Returns the graphics this device context draws through, or null when it has none
        /// (for example, after it has been disposed).
        /// </summary>
        Graphics? GetGraphics ();
    }
}
