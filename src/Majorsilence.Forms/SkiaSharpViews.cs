using System;
using Majorsilence.Forms;
using SkiaSharp;

namespace SkiaSharp.Views.Desktop
{
    // SkiaSharp.Views ships SKControl/SKGLControl for real WinForms, bound to System.Windows.Forms and so
    // unusable here -- the migrator strips that package. The types are worth having anyway: this whole
    // library already renders through SkiaSharp, so a control that simply hands its Skia surface to the
    // caller is a thin wrapper rather than a port, and it is what every SkiaSharp-drawing control
    // (charting libraries, custom canvases) is written against.
    //
    // The surface is an offscreen raster one, snapshotted onto the real canvas after the handler returns,
    // rather than the canvas itself: SKPaintSurfaceEventArgs is defined in terms of an SKSurface and
    // there is no way to wrap an existing SKCanvas in one. The extra blit is the price of matching the
    // upstream contract, and it keeps a handler that leaves the surface in an odd state -- unbalanced
    // save/restore, a stray clip -- from corrupting the shared canvas.

    /// <summary>
    /// Provides the drawing surface for a <see cref="SKControl.PaintSurface"/> handler.
    /// </summary>
    public class SKPaintSurfaceEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SKPaintSurfaceEventArgs (SKSurface surface, SKImageInfo info)
            : this (surface, info, info)
        {
        }

        /// <summary>Initializes a new instance with separate raw and logical sizes.</summary>
        public SKPaintSurfaceEventArgs (SKSurface surface, SKImageInfo info, SKImageInfo rawInfo)
        {
            ArgumentNullException.ThrowIfNull (surface);
            Surface = surface;
            Info = info;
            RawInfo = rawInfo;
        }

        /// <summary>Gets the surface to draw on.</summary>
        public SKSurface Surface { get; }

        /// <summary>Gets the surface's image info, in logical pixels.</summary>
        public SKImageInfo Info { get; }

        /// <summary>Gets the surface's image info, in raw device pixels.</summary>
        public SKImageInfo RawInfo { get; }
    }

    /// <summary>
    /// Provides the drawing surface for a <see cref="SKGLControl.PaintSurface"/> handler.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="SKPaintSurfaceEventArgs"/> only in type: upstream separates the two so a
    /// GL handler can be told the backend render target, and code is written against one or the other.
    /// </remarks>
    public class SKPaintGLSurfaceEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SKPaintGLSurfaceEventArgs (SKSurface surface, GRBackendRenderTarget? renderTarget)
            : this (surface, renderTarget, SKColorType.Unknown)
        {
        }

        /// <summary>Initializes a new instance with an explicit colour type.</summary>
        public SKPaintGLSurfaceEventArgs (SKSurface surface, GRBackendRenderTarget? renderTarget, SKColorType colorType)
        {
            ArgumentNullException.ThrowIfNull (surface);
            Surface = surface;
            BackendRenderTarget = renderTarget;
            ColorType = colorType;
        }

        /// <summary>Gets the surface to draw on.</summary>
        public SKSurface Surface { get; }

        /// <summary>Gets the backend render target, or null when there is no GL backend.</summary>
        public GRBackendRenderTarget? BackendRenderTarget { get; }

        /// <summary>Gets the colour type of the surface.</summary>
        public SKColorType ColorType { get; }
    }

    /// <summary>
    /// A control that raises <see cref="PaintSurface"/> so a handler can draw with SkiaSharp directly.
    /// </summary>
    public class SKControl : Control
    {
        /// <summary>
        /// Raised on every paint, with a Skia surface sized to the control's client area.
        /// </summary>
        /// <remarks>
        /// A handler owns the surface only for the duration of the call; it is snapshotted onto the
        /// control's own canvas afterwards and then disposed.
        /// </remarks>
        public event EventHandler<SKPaintSurfaceEventArgs>? PaintSurface;

        /// <summary>Raises the <see cref="PaintSurface"/> event.</summary>
        protected virtual void OnPaintSurface (SKPaintSurfaceEventArgs e) => PaintSurface?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (e?.Graphics?.Canvas is not { } canvas)
                return;

            // Device pixels, not logical: the surface is what the handler measures its drawing against, so
            // it has to match what actually lands on screen or a scaled display renders at half size.
            var width = DeviceScaledWidth;
            var height = DeviceScaledHeight;

            if (width <= 0 || height <= 0)
                return;

            var info = new SKImageInfo (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var surface = SKSurface.Create (info);

            if (surface is null)
                return;

            OnPaintSurface (new SKPaintSurfaceEventArgs (surface, info));

            using var snapshot = surface.Snapshot ();
            canvas.DrawImage (snapshot, 0, 0);
        }

        // The control's client size in device pixels. LogicalToDeviceUnits is the same conversion the
        // renderers use, so a surface built from these lines up with everything else drawn on the canvas.
        private int DeviceScaledWidth => LogicalToDeviceUnits (ClientSize.Width);

        private int DeviceScaledHeight => LogicalToDeviceUnits (ClientSize.Height);
    }

    /// <summary>
    /// A GPU-backed variant of <see cref="SKControl"/>.
    /// </summary>
    /// <remarks>
    /// Renders exactly as <see cref="SKControl"/> does -- there is no GL context to hand out, because
    /// painting goes through whichever surface the window backend provides. <see cref="GRContext"/> is
    /// therefore null, which is the documented "no GPU context" answer and what callers already handle
    /// (they pass it straight to a renderer that falls back to CPU). Kept as a separate type so code
    /// written against the GL control compiles and runs, just without GPU acceleration.
    /// </remarks>
    public class SKGLControl : Control
    {
        /// <inheritdoc cref="SKControl.PaintSurface"/>
        public event EventHandler<SKPaintGLSurfaceEventArgs>? PaintSurface;

        /// <summary>Gets the GPU context backing this control. Always null here; see the type remarks.</summary>
        public GRContext? GRContext => null;

        /// <summary>Gets or sets whether presentation waits for vertical sync. Not honoured here.</summary>
        /// <remarks>Frame presentation is the compositor's business and no backend exposes the choice, so
        /// this stores the value and nothing reads it -- setting it is neither an error nor an effect.</remarks>
        public bool VSync { get; set; } = true;

        /// <summary>Raises the <see cref="PaintSurface"/> event.</summary>
        protected virtual void OnPaintSurface (SKPaintGLSurfaceEventArgs e) => PaintSurface?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (e?.Graphics?.Canvas is not { } canvas)
                return;

            var width = LogicalToDeviceUnits (ClientSize.Width);
            var height = LogicalToDeviceUnits (ClientSize.Height);

            if (width <= 0 || height <= 0)
                return;

            var info = new SKImageInfo (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var surface = SKSurface.Create (info);

            if (surface is null)
                return;

            OnPaintSurface (new SKPaintGLSurfaceEventArgs (surface, null, info.ColorType));

            using var snapshot = surface.Snapshot ();
            canvas.DrawImage (snapshot, 0, 0);
        }
    }
}
