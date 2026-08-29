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
        private Point drag_start;            // logical px, MDI-client-relative (stable while the frame moves)
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
            var captionColor = MacStyleCaption
                // Light, but a shade off the content beneath it: an exactly-matching fill made the
                // boundary between caption and form invisible.
                ? (active ? MacCaptionActive : MacCaptionInactive)
                : (active ? Theme.AccentColor : Theme.AccentColor2);
            e.Canvas.FillRectangle (new Rectangle (0, 0, w, caption + border), captionColor);

            var buttonsWidth = D (CaptionButtonSlot) * VisibleButtonCount ();
            Rectangle textRect;
            ContentAlignment textAlign;
            SKColor textColor;

            if (MacStyleCaption) {
                // Rule under the caption: macOS separates title chrome from content with a line rather
                // than a colour change. Drawn in the frame outline colour so caption, separator and
                // window edge all read as one border.
                e.Canvas.FillRectangle (new Rectangle (0, caption + border - D (1), w, D (1)), FrameOutline);

                // Centred title, inset by the button run on BOTH sides so it stays optically centred
                // in the window and can never collide with the traffic lights.
                var inset = border + buttonsWidth + D (4);
                textRect = new Rectangle (inset, border, Math.Max (0, w - (2 * inset)), caption);
                textAlign = ContentAlignment.MiddleCenter;
                // An unfocused macOS window dims its title rather than recolouring the bar.
                textColor = active ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;
            } else {
                // Windows: title leads, buttons trail.
                textRect = new Rectangle (border + D (4), border, w - 2 * border - buttonsWidth - D (4), caption);
                textAlign = ContentAlignment.MiddleLeft;
                textColor = Theme.ForegroundColorOnAccent;
            }

            if (textRect.Width > 0)
                e.Canvas.DrawText (ChildForm.Text ?? string.Empty, Theme.UIFont,
                    e.LogicalToDeviceUnits (Theme.FontSize), textRect, textColor,
                    textAlign, ellipsis: true);

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

            // Outline last, so it frames the composited content instead of being painted over: without
            // it a child window's edge is indistinguishable from the MDI background behind it.
            var edge = D (1);
            e.Canvas.FillRectangle (new Rectangle (0, 0, w, edge), FrameOutline);                 // top
            e.Canvas.FillRectangle (new Rectangle (0, h - edge, w, edge), FrameOutline);          // bottom
            e.Canvas.FillRectangle (new Rectangle (0, 0, edge, h), FrameOutline);                 // left
            e.Canvas.FillRectangle (new Rectangle (w - edge, 0, edge, h), FrameOutline);          // right
        }

        private int VisibleButtonCount () =>
            1 + (ChildForm.MaximizeBox ? 1 : 0) + (ChildForm.MinimizeBox ? 1 : 0);

        /// <summary>
        /// Whether the caption buttons sit at the leading (left) edge, as macOS places its window
        /// controls, rather than the trailing edge Windows uses. Defaults to the host platform;
        /// settable so a host can pin one convention and so tests can exercise both.
        /// </summary>
        internal static bool CaptionButtonsOnLeft { get; set; } = OperatingSystem.IsMacOS ();

        /// <summary>
        /// Whether the caption is drawn in the macOS idiom: light chrome with a hairline separator,
        /// a centred title, and coloured traffic-light discs instead of Windows' accent-filled bar
        /// with square glyphs and a leading title. Tracks the button placement.
        /// </summary>
        internal static bool MacStyleCaption => CaptionButtonsOnLeft;

        // Logical width of one caption button cell. macOS' traffic lights are 12pt discs about 8pt
        // apart, so a 20pt cell reproduces their spacing; Windows' square glyph buttons are wider.
        internal static int CaptionButtonSlot => MacStyleCaption ? 20 : ButtonWidth;

        // Caption chrome for the macOS look: light, but distinct from the form content beneath so the
        // title bar reads as its own band. The active bar is the darker of the two, as on macOS.
        private static readonly SKColor MacCaptionActive = new SKColor (0xDE, 0xDE, 0xDE);
        private static readonly SKColor MacCaptionInactive = new SKColor (0xF0, 0xF0, 0xF0);

        // Frame outline: a single crisp line around the whole window and under the caption, so the
        // child's edges are unambiguous against a same-coloured MDI background.
        private static readonly SKColor FrameOutline = new SKColor (0x8A, 0x8A, 0x8A);

        // Traffic-light fills. An unfocused macOS window greys all three out.
        private static readonly SKColor MacClose = new SKColor (0xFF, 0x5F, 0x57);
        private static readonly SKColor MacMinimize = new SKColor (0xFE, 0xBC, 0x2E);
        private static readonly SKColor MacZoom = new SKColor (0x28, 0xC8, 0x40);
        private static readonly SKColor MacInactive = new SKColor (0xD6, 0xD6, 0xD6);

        /// <summary>
        /// The visible caption buttons in the order they appear along the caption, leading edge
        /// first. macOS orders its controls close/minimize/zoom from the left; Windows orders them
        /// minimize/maximize/close towards the right. Painting and hit testing both read this, so
        /// the drawn glyph and the clickable box cannot drift apart.
        /// </summary>
        internal CaptionHit[] CaptionButtonOrder ()
        {
            var order = CaptionButtonsOnLeft
                ? new[] { CaptionHit.Close, CaptionHit.Minimize, CaptionHit.Maximize }
                : new[] { CaptionHit.Minimize, CaptionHit.Maximize, CaptionHit.Close };

            return order.Where (hit => hit switch {
                CaptionHit.Maximize => ChildForm.MaximizeBox,
                CaptionHit.Minimize => ChildForm.MinimizeBox,
                _ => true,
            }).ToArray ();
        }

        /// <summary>
        /// Left edge of the caption button at <paramref name="index"/>, in the same units as
        /// <paramref name="buttonWidth"/> and <paramref name="totalWidth"/>. The run is packed
        /// against whichever edge the platform puts its controls on.
        /// </summary>
        internal static int CaptionButtonX (int index, int count, int buttonWidth, int totalWidth, int border)
            => CaptionButtonsOnLeft
                ? border + (index * buttonWidth)
                : totalWidth - border - ((count - index) * buttonWidth);

        private void PaintCaptionButtons (PaintEventArgs e, int w, int border, int caption)
        {
            var bw = (int) Math.Round (CaptionButtonSlot * e.Scaling);
            var order = CaptionButtonOrder ();
            var active = Client.ActiveChild == ChildForm;

            for (var i = 0; i < order.Length; i++) {
                var cell = new Rectangle (CaptionButtonX (i, order.Length, bw, w, border), border, bw, caption);

                if (MacStyleCaption) {
                    PaintMacTrafficLight (e, cell, order[i], active);
                    continue;
                }

                var box = CenterGlyph (cell, e);

                switch (order[i]) {
                    case CaptionHit.Close:
                        ControlPaint.DrawCloseGlyph (e, box);
                        break;
                    case CaptionHit.Maximize:
                        if (WindowState == FormWindowState.Maximized)
                            ControlPaint.DrawRestoreGlyph (e, box);
                        else
                            ControlPaint.DrawMaximizeGlyph (e, box);
                        break;
                    case CaptionHit.Minimize:
                        ControlPaint.DrawMinimizeGlyph (e, box);
                        break;
                }
            }
        }

        /// <summary>
        /// Draws one macOS traffic light: a filled disc with a slightly darker rim. macOS shows the
        /// glyph inside only while the pointer is over the group, so at rest these are plain discs.
        /// </summary>
        private static void PaintMacTrafficLight (PaintEventArgs e, Rectangle cell, CaptionHit hit, bool active)
        {
            var diameter = e.LogicalToDeviceUnits (12);
            var radius = diameter / 2;
            var cx = cell.X + (cell.Width / 2);
            var cy = cell.Y + (cell.Height / 2);

            var fill = active
                ? hit switch {
                    CaptionHit.Close => MacClose,
                    CaptionHit.Minimize => MacMinimize,
                    CaptionHit.Maximize => MacZoom,
                    _ => MacInactive,
                }
                : MacInactive;

            e.Canvas.FillCircle (cx, cy, radius, fill);

            // Rim: keeps a light disc from disappearing into light chrome.
            e.Canvas.DrawCircle (cx, cy, radius, Darken (fill), e.LogicalToDeviceUnits (1));
        }

        private static SKColor Darken (SKColor color)
            => new SKColor (
                (byte) (color.Red * 0.82),
                (byte) (color.Green * 0.82),
                (byte) (color.Blue * 0.82),
                color.Alpha);

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

        internal enum CaptionHit { None, Close, Maximize, Minimize }

        private CaptionHit HitCaptionButton (int lx, int ly)
        {
            if (ly < FrameBorder || ly > FrameBorder + CaptionHeight)
                return CaptionHit.None;

            var order = CaptionButtonOrder ();

            for (var i = 0; i < order.Length; i++) {
                var x = CaptionButtonX (i, order.Length, CaptionButtonSlot, Width, FrameBorder);
                if (lx >= x && lx < x + CaptionButtonSlot)
                    return order[i];
            }

            return CaptionHit.None;
        }

        // ── Input ──────────────────────────────────────────────────────────────

        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            Client.Activate (ChildForm);

            // Activate() is a no-op when this child is already the active one, so reclaim focus here
            // too: clicking back into a child after using the container's menu has to take keyboard
            // focus off that menu again.
            Client.GiveFocusToActiveChild ();

            // e.X/e.Y arrive already in this frame's logical coordinates (the window converts device
            // to logical once, at the boundary, and the child walk only subtracts logical offsets), so
            // they compare directly against FrameBorder / CaptionHeight / Width / Height.
            var lx = e.X;
            var ly = e.Y;

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

        // A hosted child form is not a child Control, so the wheel walk in Control.RaiseMouseWheel
        // reaches this frame and stops -- OnMouseDown/Move/Up forward to the child, but nothing
        // forwarded the wheel, so scrolling the mouse (or two-finger trackpad) over an MDI child's
        // content did nothing. Found in ReportDesigner: the preview/design surface would not scroll.
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            ForwardToChild (e, c => c.HandlePointerWheel (e.Button, InteriorX (e), InteriorY (e), e.DeltaPoint, e.Modifiers));
        }

        // e.X/e.Y are in this frame's logical coordinates (see OnMouseDown); the child's client area
        // starts one border in and one caption down.
        private int InteriorX (MouseEventArgs e) => e.X - FrameBorder;
        private int InteriorY (MouseEventArgs e) => e.Y - (FrameBorder + CaptionHeight);

        private void ForwardToChild (MouseEventArgs e, Action<Form> dispatch)
        {
            if (WindowState == FormWindowState.Minimized)
                return;

            // Only the interior (below the caption, inside the border) maps to the child's client area.
            if (e.X < FrameBorder || e.X >= Width - FrameBorder || e.Y < FrameBorder + CaptionHeight || e.Y >= Height - FrameBorder)
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
            // Track against the MDI-client-relative position (ScreenLocation), not the frame-relative e.X/e.Y:
            // the frame moves as we drag it, so frame-relative deltas feed back on themselves and jitter.
            // Both ScreenLocation and drag_start are logical MDI-client coordinates, as are drag_origin
            // and MoveChild's arguments.
            var dx = e.ScreenLocation.X - drag_start.X;
            var dy = e.ScreenLocation.Y - drag_start.Y;
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
            Math.Max (3 * CaptionButtonSlot + 2 * FrameBorder, 2 * FrameBorder + 40),
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
