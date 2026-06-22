using System.Drawing;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a window backend that can host a native control of the
    /// underlying UI toolkit (an Avalonia control or an Uno UIElement) *inside* a Majorsilence.Forms scene.
    ///
    /// This is the "airspace" overlay: Majorsilence.Forms renders to a single Skia surface, so a native
    /// element can't be composited into that surface — instead the backend places the real native element
    /// as a sibling on top of the Skia surface and keeps it aligned to the bounds of the owning
    /// <see cref="NativeControlHost"/>. A backend that can't do this simply doesn't implement the
    /// interface, and <see cref="NativeControlHost"/> degrades to an empty placeholder.
    /// </summary>
    public interface INativeControlHostBackend
    {
        /// <summary>Adds <paramref name="nativeControl"/> to the overlay for the given host placeholder.</summary>
        void AttachNativeControl (NativeControlHost host, object nativeControl);

        /// <summary>
        /// Positions/sizes the host's native control to <paramref name="logicalBounds"/> and clips it to
        /// <paramref name="clipBounds"/> (both in logical pixels, relative to the Majorsilence.Forms
        /// client/content origin), then sets its visibility. <paramref name="clipBounds"/> is the visible
        /// region after intersecting with every scrolling/clipping ancestor's viewport; it equals
        /// <paramref name="logicalBounds"/> when the host is fully visible, and is used to clip the native
        /// element so it doesn't spill outside a scroll viewport.
        /// </summary>
        void UpdateNativeControl (NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible);

        /// <summary>Removes the host's native control from the overlay.</summary>
        void DetachNativeControl (NativeControlHost host);
    }
}
