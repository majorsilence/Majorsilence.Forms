using System.Drawing;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A Majorsilence.Forms control that reserves a rectangle for a *native* control of the underlying UI
    /// toolkit (an Avalonia control, or an Uno UIElement). The active window backend overlays the real
    /// native element on top of the Skia surface and keeps it aligned to this placeholder's bounds — the
    /// "airspace" interop model, since native elements can't be composited into Majorsilence's Skia buffer.
    ///
    /// Assign the toolkit object to <see cref="NativeControl"/>. This works both when Majorsilence.Forms is
    /// embedded (via <see cref="HostedSurface"/>) and when it runs standalone, as long as the backend
    /// implements <see cref="INativeControlHostBackend"/>; otherwise the host renders as an empty
    /// placeholder. The native element draws above the Majorsilence scene and does not scroll its content
    /// into a clip region beyond the host's bounds, so keep it inside non-scrolling areas for now.
    /// </summary>
    public class NativeControlHost : Control
    {
        private object? native;
        private bool attached;
        private Rectangle last_bounds;
        private Rectangle last_clip;
        private bool last_visible;

        /// <summary>Initializes a new instance of the <see cref="NativeControlHost"/> class.</summary>
        public NativeControlHost ()
        {
            // The native element renders on top, so the placeholder itself paints nothing — let the host
            // background show through underneath.
            Style.BackgroundColor = SkiaSharp.SKColors.Transparent;
        }

        /// <summary>Gets the default style for all native-control hosts.</summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => { style.BackgroundColor = SkiaSharp.SKColors.Transparent; });

        /// <summary>Gets the ControlStyle properties for this instance.</summary>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets or sets the native toolkit control to host (an Avalonia <c>Control</c> or an Uno
        /// <c>UIElement</c>). Setting null removes any previously hosted control.
        /// </summary>
        public object? NativeControl {
            get => native;
            set {
                if (ReferenceEquals (native, value))
                    return;

                DetachCore ();
                native = value;
                attached = false;
                SyncNativeControl ();
                Invalidate ();
            }
        }

        private INativeControlHostBackend? HostBackend => FindWindow ()?.Backend as INativeControlHostBackend;

        // Visibility along the whole parent chain — the native overlay must hide when any ancestor hides.
        private bool EffectiveVisible ()
        {
            for (Control? c = this; c is not null and not ControlAdapter; c = c.Parent)
                if (!c.Visible)
                    return false;
            return true;
        }

        /// <summary>
        /// Re-aligns the hosted native control to this placeholder's current bounds and visibility.
        /// Called automatically on paint, visibility changes and (re)assignment; call it manually after
        /// moving/resizing the host outside the normal paint cycle.
        /// </summary>
        public void SyncNativeControl ()
        {
            var backend = HostBackend;
            if (backend is null || native is null)
                return;

            if (!attached) {
                backend.AttachNativeControl (this, native);
                attached = true;
                last_bounds = Rectangle.Empty;
                last_clip = Rectangle.Empty;
                last_visible = false;
            }

            var pos = GetPositionInForm ();
            var bounds = new Rectangle (pos.X, pos.Y, Width, Height);

            // The visible region is the host rect clipped by every clipping/scrolling ancestor's viewport
            // (its DisplayRectangle, which excludes borders and scrollbars). Majorsilence clips its own
            // children implicitly via per-control back buffers; a native overlay has none, so we compute
            // the clip explicitly and hand it to the backend.
            var clip = bounds;
            for (var parent = Parent; parent is not null and not ControlAdapter; parent = parent.Parent) {
                var pp = parent.GetPositionInForm ();
                var vp = parent.DisplayRectangle;
                clip = Rectangle.Intersect (clip, new Rectangle (pp.X + vp.X, pp.Y + vp.Y, vp.Width, vp.Height));
            }

            var visible = EffectiveVisible () && clip.Width > 0 && clip.Height > 0;

            if (bounds == last_bounds && clip == last_clip && visible == last_visible)
                return;

            last_bounds = bounds;
            last_clip = clip;
            last_visible = visible;
            backend.UpdateNativeControl (this, bounds, clip, visible);
        }

        private void DetachCore ()
        {
            if (attached && native is not null)
                HostBackend?.DetachNativeControl (this);
            attached = false;
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            SyncNativeControl ();
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged (EventArgs e)
        {
            base.OnVisibleChanged (e);
            SyncNativeControl ();
        }
    }
}
