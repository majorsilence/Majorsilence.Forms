using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The in-client frame that hosts one MDI child <see cref="Form"/> inside an <see cref="MdiClient"/>.
    /// It draws the child's caption bar (title, icon, minimize/maximize/close) and border, composites the
    /// child form's content into the interior (via <see cref="WindowBase.RenderFrame"/> into its own
    /// buffer), and bridges move/resize/caption interaction plus forwards interior input to the child.
    /// The child form itself never owns an on-screen OS window while hosted.
    /// </summary>
    internal sealed class MdiChildWindow : Control
    {
        // Logical metrics (scaled to device pixels at paint/hit time).
        internal const int CaptionHeight = 28;
        internal const int FrameBorder = 4;
        internal const int ButtonWidth = 30;
        internal const int MinimizedWidth = 160;

        private enum Drag { None, Move, ResizeL, ResizeR, ResizeT, ResizeB, ResizeTL, ResizeTR, ResizeBL, ResizeBR }

        private Drag drag = Drag.None;
        private Point drag_start;            // device px, MDI-client-relative (stable while the frame moves)
        private Rectangle drag_origin;       // logical bounds at drag start
        private SKBitmap? content_buffer;

        public MdiChildWindow (MdiClient client, Form child)
        {
            Client = client;
            ChildForm = child;
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        public MdiClient Client { get; }

        public Form ChildForm { get; }

        /// <summary>The window state of the hosted child (normal/minimized/maximized).</summary>
        public FormWindowState WindowState { get; private set; } = FormWindowState.Normal;

        /// <summary>Bounds (logical, MDI-client-relative) to return to when restored from min/max.</summary>
        public Rectangle RestoreBounds { get; set; }

        // ── Geometry the hosted Form reports as its own ──────────────────────────

        /// <summary>The logical size available to the child form's content (interior minus chrome).</summary>
        public Size ContentSize => new Size (
            Math.Max (0, Width - 2 * FrameBorder),
            Math.Max (0, Height - CaptionHeight - 2 * FrameBorder));

        /// <summary>Resizes the frame so the child's content area is <paramref name="content"/> logical pixels.</summary>
        public void SetContentSize (Size content)
        {
            Size = new Size (content.Width + 2 * FrameBorder, content.Height + CaptionHeight + 2 * FrameBorder);
        }

        // ── Painting ─────────────────────────────────────────────────────────────

        // A size change alone doesn't mark this control dirty (Control.OnResize's invalidate is a
        // no-op here), so without this, resizing the frame reallocates its back buffer to the new
        // size in the paint walk but never repaints into it: OnPaint (and the RenderFrame call inside
        // it that lays out the hosted Form's content) simply gets skipped. That leaves the hosted
        // Form's own children (e.g. Dock/Anchor-based controls) laid out for the old size.
        protected override void OnResize (EventArgs e)
        {
            base.OnResize (e);
            Invalidate ();
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            var scaling = e.Scaling;
            int D (int logical) => (int) Math.Round (logical * scaling);

            var w = ScaledWidth;
            var h = ScaledHeight;
            var border = D (FrameBorder);
            var caption = D (CaptionHeight);
            var active = Client.ActiveChild == ChildForm;

            // Frame background + caption strip.
            e.Canvas.Clear (Theme.BorderMidColor);
            var captionColor = active ? Theme.AccentColor : Theme.AccentColor2;
            e.Canvas.FillRectangle (new Rectangle (0, 0, w, caption + border), captionColor);

            // Title text (leave room for the caption buttons on the right).
            var buttonsWidth = D (ButtonWidth) * VisibleButtonCount ();
            var textRect = new Rectangle (border + D (4), border, w - 2 * border - buttonsWidth - D (4), caption);
            if (textRect.Width > 0)
                e.Canvas.DrawText (ChildForm.Text ?? string.Empty, Theme.UIFont,
                    e.LogicalToDeviceUnits (Theme.FontSize), textRect, Theme.ForegroundColorOnAccent,
                    ContentAlignment.MiddleLeft, ellipsis: true);

            PaintCaptionButtons (e, w, border, caption);

            // Content: render the child form into its own buffer (its OnPaintBackground does a full
            // canvas Clear, so it must be isolated), then composite it into the interior.
            if (WindowState != FormWindowState.Minimized) {
                var cw = w - 2 * border;
                var ch = h - caption - 2 * border;
                if (cw > 0 && ch > 0) {
                    EnsureContentBuffer (cw, ch);
                    using (var canvas = new SKCanvas (content_buffer)) {
                        ChildForm.RenderFrame (canvas, cw, ch, scaling);
                        canvas.Flush ();
                    }
                    e.Canvas.DrawBitmap (content_buffer, border, caption + border);
                }
            }
        }

        private int VisibleButtonCount () =>
            1 + (ChildForm.MaximizeBox ? 1 : 0) + (ChildForm.MinimizeBox ? 1 : 0);

        private void PaintCaptionButtons (PaintEventArgs e, int w, int border, int caption)
        {
            var bw = (int) Math.Round (ButtonWidth * e.Scaling);
            var x = w - border - bw;

            // Close (rightmost).
            ControlPaint.DrawCloseGlyph (e, CenterGlyph (new Rectangle (x, border, bw, caption), e));
            if (ChildForm.MaximizeBox) {
                x -= bw;
                if (WindowState == FormWindowState.Maximized)
                    ControlPaint.DrawRestoreGlyph (e, CenterGlyph (new Rectangle (x, border, bw, caption), e));
                else
                    ControlPaint.DrawMaximizeGlyph (e, CenterGlyph (new Rectangle (x, border, bw, caption), e));
            }
            if (ChildForm.MinimizeBox) {
                x -= bw;
                ControlPaint.DrawMinimizeGlyph (e, CenterGlyph (new Rectangle (x, border, bw, caption), e));
            }
        }

        private static Rectangle CenterGlyph (Rectangle button, PaintEventArgs e)
        {
            var size = e.LogicalToDeviceUnits (10);
            return new Rectangle (button.X + (button.Width - size) / 2, button.Y + (button.Height - size) / 2, size, size);
        }

        private void EnsureContentBuffer (int w, int h)
        {
            if (content_buffer is null || content_buffer.Width != w || content_buffer.Height != h) {
                content_buffer?.Dispose ();
                content_buffer = new SKBitmap (new SKImageInfo (w, h, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            }
        }

        // ── Caption-button hit testing (logical, control-relative) ────────────────

        private enum CaptionHit { None, Close, Maximize, Minimize }

        private CaptionHit HitCaptionButton (int lx, int ly)
        {
            if (ly < FrameBorder || ly > FrameBorder + CaptionHeight)
                return CaptionHit.None;

            var x = Width - FrameBorder - ButtonWidth;
            if (lx >= x && lx < x + ButtonWidth) return CaptionHit.Close;
            if (ChildForm.MaximizeBox) { x -= ButtonWidth; if (lx >= x && lx < x + ButtonWidth) return CaptionHit.Maximize; }
            if (ChildForm.MinimizeBox) { x -= ButtonWidth; if (lx >= x && lx < x + ButtonWidth) return CaptionHit.Minimize; }
            return CaptionHit.None;
        }

        // ── Input ──────────────────────────────────────────────────────────────

        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            Client.Activate (ChildForm);

            var scaling = FrameScaling;
            var lx = (int) (e.X / scaling);
            var ly = (int) (e.Y / scaling);

            // Caption buttons first.
            switch (HitCaptionButton (lx, ly)) {
                case CaptionHit.Close: ChildForm.Close (); return;
                case CaptionHit.Maximize: ToggleMaximize (); return;
                case CaptionHit.Minimize: ToggleMinimize (); return;
            }

            // Resize edges (only when restored & resizable).
            if (WindowState == FormWindowState.Normal) {
                var mode = HitResizeEdge (lx, ly);
                if (mode != Drag.None) {
                    drag = mode;
                    drag_start = e.ScreenLocation;
                    drag_origin = Bounds;
                    return;
                }
            }

            // Caption (not a button) → move.
            if (ly >= FrameBorder && ly < FrameBorder + CaptionHeight && WindowState != FormWindowState.Maximized) {
                drag = Drag.Move;
                drag_start = e.ScreenLocation;
                drag_origin = Bounds;
                return;
            }

            // Interior → forward to the hosted form.
            ForwardToChild (e, c => c.HandlePointerPressed (e.Button, InteriorX (e), InteriorY (e), e.Modifiers));
        }

        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (drag != Drag.None) {
                ApplyDrag (e);
                return;
            }

            ForwardToChild (e, c => c.HandlePointerMoved (e.Button, InteriorX (e), InteriorY (e), e.Modifiers));
        }

        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (drag != Drag.None) {
                drag = Drag.None;
                return;
            }

            ForwardToChild (e, c => c.HandlePointerReleased (e.Button, InteriorX (e), InteriorY (e), e.Modifiers));
        }

        private double FrameScaling => FindForm ()?.Scaling ?? 1.0;

        private int InteriorX (MouseEventArgs e) => e.X - (int) Math.Round (FrameBorder * FrameScaling);
        private int InteriorY (MouseEventArgs e) => e.Y - (int) Math.Round ((FrameBorder + CaptionHeight) * FrameScaling);

        private void ForwardToChild (MouseEventArgs e, Action<Form> dispatch)
        {
            if (WindowState == FormWindowState.Minimized)
                return;

            var scaling = FrameScaling;
            var lx = (int) (e.X / scaling);
            var ly = (int) (e.Y / scaling);
            // Only the interior (below the caption, inside the border) maps to the child's client area.
            if (lx < FrameBorder || lx >= Width - FrameBorder || ly < FrameBorder + CaptionHeight || ly >= Height - FrameBorder)
                return;

            dispatch (ChildForm);
        }

        private Drag HitResizeEdge (int lx, int ly)
        {
            var left = lx < FrameBorder;
            var right = lx >= Width - FrameBorder;
            var top = ly < FrameBorder;
            var bottom = ly >= Height - FrameBorder;

            if (top && left) return Drag.ResizeTL;
            if (top && right) return Drag.ResizeTR;
            if (bottom && left) return Drag.ResizeBL;
            if (bottom && right) return Drag.ResizeBR;
            if (left) return Drag.ResizeL;
            if (right) return Drag.ResizeR;
            if (top) return Drag.ResizeT;
            if (bottom) return Drag.ResizeB;
            return Drag.None;
        }

        private void ApplyDrag (MouseEventArgs e)
        {
            var scaling = FrameScaling;
            // Track against the MDI-client-relative position (ScreenLocation), not the frame-relative e.X/e.Y:
            // the frame moves as we drag it, so frame-relative deltas feed back on themselves and jitter.
            var dx = (int) ((e.ScreenLocation.X - drag_start.X) / scaling);
            var dy = (int) ((e.ScreenLocation.Y - drag_start.Y) / scaling);
            var b = drag_origin;
            var min = MinChildSize;

            int l = b.Left, t = b.Top, r = b.Right, btm = b.Bottom;

            switch (drag) {
                case Drag.Move:
                    Client.MoveChild (this, b.X + dx, b.Y + dy);
                    return;
                case Drag.ResizeL: l = Math.Min (b.Left + dx, r - min.Width); break;
                case Drag.ResizeR: r = Math.Max (b.Right + dx, l + min.Width); break;
                case Drag.ResizeT: t = Math.Min (b.Top + dy, btm - min.Height); break;
                case Drag.ResizeB: btm = Math.Max (b.Bottom + dy, t + min.Height); break;
                case Drag.ResizeTL: l = Math.Min (b.Left + dx, r - min.Width); t = Math.Min (b.Top + dy, btm - min.Height); break;
                case Drag.ResizeTR: r = Math.Max (b.Right + dx, l + min.Width); t = Math.Min (b.Top + dy, btm - min.Height); break;
                case Drag.ResizeBL: l = Math.Min (b.Left + dx, r - min.Width); btm = Math.Max (b.Bottom + dy, t + min.Height); break;
                case Drag.ResizeBR: r = Math.Max (b.Right + dx, l + min.Width); btm = Math.Max (b.Bottom + dy, t + min.Height); break;
            }

            Client.SetChildBounds (this, new Rectangle (l, t, r - l, btm - t));
        }

        private Size MinChildSize => new Size (
            Math.Max (3 * ButtonWidth + 2 * FrameBorder, 2 * FrameBorder + 40),
            CaptionHeight + 2 * FrameBorder + 10);

        // ── Min / max ────────────────────────────────────────────────────────────

        public void ToggleMaximize ()
        {
            if (WindowState == FormWindowState.Maximized)
                Restore ();
            else
                Maximize ();
        }

        public void ToggleMinimize ()
        {
            if (WindowState == FormWindowState.Minimized)
                Restore ();
            else
                Minimize ();
        }

        public void Maximize ()
        {
            if (WindowState == FormWindowState.Normal)
                RestoreBounds = Bounds;
            WindowState = FormWindowState.Maximized;
            Client.LayoutMaximizedChild (this);
            ChildForm.RaiseMdiResize ();
            Invalidate ();
        }

        public void Minimize ()
        {
            if (WindowState == FormWindowState.Normal)
                RestoreBounds = Bounds;
            WindowState = FormWindowState.Minimized;
            Size = new Size (MinimizedWidth, CaptionHeight + 2 * FrameBorder);
            Client.ArrangeMinimized ();
            Invalidate ();
        }

        public void Restore ()
        {
            WindowState = FormWindowState.Normal;
            Client.SetChildBounds (this, RestoreBounds);
            ChildForm.RaiseMdiResize ();
            Invalidate ();
        }

        // Clears maximized/minimized state without repositioning — the caller (a LayoutMdi pass) sets the
        // bounds itself.
        internal void SetNormalStateInternal () => WindowState = FormWindowState.Normal;
    }
}
