using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The container that fills an MDI parent form's client area and hosts its child forms. Each child is
    /// wrapped in an <see cref="MdiChildWindow"/> frame. The client owns child z-order/activation, the
    /// cascade/tile/arrange layouts (<see cref="Form.LayoutMdi"/>), and clamps children to its bounds.
    /// </summary>
    public partial class MdiClient : ScrollableControl
    {
        private const int CascadeStep = 26;

        private MdiChildWindow? active;

        // Guards against re-entrancy: clamping a child's bounds in OnLayout re-triggers the client's layout.
        private bool laying_out;

        // WinForms z-order: in the Controls collection INDEX 0 is topmost; activation moves a frame
        // there. This separate list tracks ACTIVATION order (most recent = last) for
        // window-list/Next purposes and is independent of the Controls indices.
        private readonly List<MdiChildWindow> frames = new ();

        /// <summary>Initializes a new instance of the <see cref="MdiClient"/> class.</summary>
        public MdiClient ()
        {
            Dock = DockStyle.Fill;
            SetControlBehavior (ControlBehaviors.Selectable, false);
            Style.BackgroundColor = Theme.ControlMidColor;
        }

        /// <summary>The MDI parent form this client belongs to.</summary>
        internal Form? Owner { get; set; }

        /// <summary>The child forms hosted in this client, in creation order.</summary>
        public IReadOnlyList<Form> ChildForms => frames.Select (f => f.ChildForm).ToList ();

        /// <summary>The currently active (focused) child form, or null.</summary>
        public Form? ActiveChild => active?.ChildForm;

        // ── Child lifecycle ───────────────────────────────────────────────────────

        internal MdiChildWindow AddChild (Form child)
        {
            var frame = new MdiChildWindow (this, child);
            child.MdiHost = frame;

            // Initial size: the child's designer-set client size.
            var content = child.InitialMdiContentSize;
            frame.SetContentSize (content);

            // Open centred in the client area, then cascade successive children off that centre so a
            // stack of windows stays individually reachable instead of perfectly overlapping. Opening
            // hard against the top-left corner left the caption bar butted into the container's own
            // chrome, which made it hard to see where the child window began.
            // The cascade offset is added to the centre rather than clamped into it: clamping collapsed
            // successive children onto the same spot whenever a child was nearly as wide as the client.
            // FitToClient below already pulls anything overhanging back inside, as it did when children
            // cascaded from the corner.
            var area = DisplayRectangle;
            var offset = (frames.Count % 8) * CascadeStep;
            var centreX = area.Width > frame.Width ? (area.Width - frame.Width) / 2 : 0;
            var centreY = area.Height > frame.Height ? (area.Height - frame.Height) / 2 : 0;

            frame.Location = new Point (centreX + offset, centreY + offset);
            frame.RestoreBounds = frame.Bounds;

            frames.Add (frame);
            Controls.Add (frame);
            Activate (child);
            FitToClient (frame);
            UpdateScrollExtent ();
            return frame;
        }

        internal void RemoveChild (MdiChildWindow frame)
        {
            var wasActive = active == frame;
            frames.Remove (frame);
            if (Controls.Contains (frame))
                Controls.Remove (frame);
            frame.ChildForm.MdiHost = null;

            if (wasActive) {
                active = null;
                // Activate the most recently activated remaining child.
                var next = frames.LastOrDefault ();
                if (next is not null)
                    Activate (next.ChildForm);
                else
                    Owner?.RaiseMdiChildActivate ();
            }

            UpdateScrollExtent ();
            Invalidate ();
        }

        // ── Activation / z-order ──────────────────────────────────────────────────

        internal void Activate (Form? child)
        {
            var frame = child is null ? null : frames.FirstOrDefault (f => f.ChildForm == child);
            if (frame is null || active == frame)
                return;

            active = frame;

            // Make it topmost: index 0 in the Controls list is painted last / hit first.
            Controls.SetChildIndex (frame, 0);

            // Track activation order in our list too (front = last).
            frames.Remove (frame);
            frames.Add (frame);

            GiveFocusToActiveChild ();

            Owner?.RaiseMdiChildActivate ();
            Invalidate ();
        }

        /// <summary>
        /// Moves keyboard focus off the container's own controls, so it belongs to the active child.
        /// </summary>
        /// <remarks>
        /// A container normally has selectable chrome of its own — a menu strip is the first selectable
        /// control on most MDI shells, and gets selected as soon as the window is shown. Nothing used to
        /// take that selection away when a child was activated, so the container went on claiming keyboard
        /// focus for a menu the user never opened, and <see cref="WindowBase.HandleTextInput"/> kept the
        /// keystrokes rather than routing them inward: the active child drew a caret and silently dropped
        /// everything typed at it. Clearing the container's selection is what "focus is in the child now"
        /// means here, since the child tracks its own focused control independently.
        ///
        /// Selecting a container control again (clicking its menu or toolbar) puts the selection back and
        /// keyboard returns to the container, which is the WinForms behaviour too.
        /// </remarks>
        internal void GiveFocusToActiveChild ()
        {
            if (active is null)
                return;

            if (FindForm () is { } owner)
                owner.adapter.SelectedControl = null;
        }

        // ── Geometry helpers used by the frame during drag/resize ──────────────────

        internal void MoveChild (MdiChildWindow frame, int x, int y)
        {
            frame.Location = new Point (x, y);
            ClampToClient (frame);
            if (frame.WindowState == FormWindowState.Normal)
                frame.RestoreBounds = frame.Bounds;
            UpdateScrollExtent ();
            Invalidate ();
        }

        internal void SetChildBounds (MdiChildWindow frame, Rectangle bounds)
        {
            frame.Bounds = bounds;
            ClampToClient (frame);
            if (frame.WindowState == FormWindowState.Normal)
                frame.RestoreBounds = frame.Bounds;
            frame.ChildForm.RaiseMdiResize ();
            UpdateScrollExtent ();
            Invalidate ();
        }

        // Re-fits children when the client is laid out — i.e. when the parent is first shown or resized.
        // A normal child is never allowed to be wider/taller than the client (so it stays within the
        // parent's bounds), and maximized children grow/shrink to keep filling the client. Minimized
        // children are left to ArrangeMinimized/LayoutMdi. This runs after the parent gains a real size,
        // which is the common case for children added in the parent's constructor (before Show).
        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            if (laying_out)
                return;

            var area = DisplayRectangle;
            if (area.Width <= 0 || area.Height <= 0)
                return;

            laying_out = true;
            try {
                foreach (var f in frames) {
                    if (f.WindowState == FormWindowState.Maximized) {
                        if (f.Bounds != new Rectangle (0, 0, area.Width, area.Height)) {
                            f.Bounds = new Rectangle (0, 0, area.Width, area.Height);
                            f.ChildForm.RaiseMdiResize ();
                        }
                    } else if (f.WindowState == FormWindowState.Normal) {
                        FitToClient (f);
                    }
                }
            } finally {
                laying_out = false;
            }

            UpdateScrollExtent ();
        }

        // Shrinks a normal child to fit within the client (never larger than the parent) and nudges it
        // back on-screen, leaving it untouched when it already fits. Shrink-only: a child is never grown
        // to fill a larger parent, matching MDI semantics. No-op until the client has a real size.
        private void FitToClient (MdiChildWindow frame)
        {
            if (frame.WindowState != FormWindowState.Normal)
                return;

            var area = DisplayRectangle;
            if (area.Width <= 0 || area.Height <= 0)
                return;

            var w = Math.Min (frame.Width, area.Width);
            var h = Math.Min (frame.Height, area.Height);
            var x = Math.Max (0, Math.Min (frame.Left, area.Width - w));
            var y = Math.Max (0, Math.Min (frame.Top, area.Height - h));

            var bounds = new Rectangle (x, y, w, h);
            if (frame.Bounds != bounds) {
                frame.Bounds = bounds;
                frame.RestoreBounds = frame.Bounds;
                frame.ChildForm.RaiseMdiResize ();
            }
        }

        // Keeps the child's top-left within the client so its caption stays reachable.
        private void ClampToClient (MdiChildWindow frame)
        {
            var area = DisplayRectangle;
            if (area.Width <= 0 || area.Height <= 0)
                return;

            var x = Math.Max (0, Math.Min (frame.Left, Math.Max (0, area.Width - MdiChildWindow.CaptionHeight)));
            var y = Math.Max (0, Math.Min (frame.Top, Math.Max (0, area.Height - MdiChildWindow.CaptionHeight)));
            if (x != frame.Left || y != frame.Top)
                frame.Location = new Point (x, y);
        }

        internal void LayoutMaximizedChild (MdiChildWindow frame)
        {
            var area = DisplayRectangle;
            frame.Bounds = new Rectangle (0, 0, area.Width, area.Height);
            frame.ChildForm.RaiseMdiResize ();
            Invalidate ();
        }

        internal void ArrangeMinimized ()
        {
            var area = DisplayRectangle;
            var x = 0;
            var y = Math.Max (0, area.Height - (MdiChildWindow.CaptionHeight + 2 * MdiChildWindow.FrameBorder));
            foreach (var f in frames.Where (f => f.WindowState == FormWindowState.Minimized)) {
                f.Location = new Point (x, y);
                x += MdiChildWindow.MinimizedWidth;
                if (x + MdiChildWindow.MinimizedWidth > area.Width && area.Width > MdiChildWindow.MinimizedWidth) {
                    x = 0;
                    y -= MdiChildWindow.CaptionHeight + 2 * MdiChildWindow.FrameBorder;
                }
            }
            Invalidate ();
        }

        // ── LayoutMdi (Cascade / Tile / ArrangeIcons) ──────────────────────────────

        /// <summary>Arranges the child forms in the requested layout.</summary>
        public void LayoutMdi (MdiLayout layout)
        {
            var open = frames.Where (f => f.WindowState != FormWindowState.Minimized).ToList ();
            var area = DisplayRectangle;
            if (area.Width <= 0 || area.Height <= 0)
                return;

            switch (layout) {
                case MdiLayout.Cascade:
                    CascadeLayout (open, area);
                    break;
                case MdiLayout.TileHorizontal:
                    TileLayout (open, area, horizontal: true);
                    break;
                case MdiLayout.TileVertical:
                    TileLayout (open, area, horizontal: false);
                    break;
                case MdiLayout.ArrangeIcons:
                    ArrangeMinimized ();
                    break;
            }

            UpdateScrollExtent ();
            Invalidate ();
        }

        private static void CascadeLayout (List<MdiChildWindow> open, Rectangle area)
        {
            var w = (int) (area.Width * 0.6);
            var h = (int) (area.Height * 0.6);
            var i = 0;
            foreach (var f in open) {
                Restore (f);
                var off = (i % 10) * CascadeStep;
                f.Bounds = new Rectangle (off, off, w, h);
                f.RestoreBounds = f.Bounds;
                f.ChildForm.RaiseMdiResize ();
                i++;
            }
        }

        private static void TileLayout (List<MdiChildWindow> open, Rectangle area, bool horizontal)
        {
            if (open.Count == 0)
                return;

            // Grid that stays close to square; horizontal favours rows, vertical favours columns.
            var count = open.Count;
            var cols = (int) Math.Ceiling (Math.Sqrt (count));
            var rows = (int) Math.Ceiling (count / (double) cols);
            if (horizontal)
                (cols, rows) = (rows, cols);

            var cellW = area.Width / cols;
            var cellH = area.Height / rows;

            for (var i = 0; i < count; i++) {
                var r = i / cols;
                var c = i % cols;
                Restore (open[i]);
                open[i].Bounds = new Rectangle (c * cellW, r * cellH, cellW, cellH);
                open[i].RestoreBounds = open[i].Bounds;
                open[i].ChildForm.RaiseMdiResize ();
            }
        }

        private static void Restore (MdiChildWindow f)
        {
            if (f.WindowState == FormWindowState.Maximized)
                f.SetNormalStateInternal ();
        }

        // ── Scrolling for children that overflow the client ────────────────────────

        private void UpdateScrollExtent ()
        {
            if (!AutoScroll)
                return;
            var right = 0;
            var bottom = 0;
            foreach (var f in frames) {
                right = Math.Max (right, f.Right);
                bottom = Math.Max (bottom, f.Bottom);
            }
            AutoScrollMinSize = new Size (right, bottom);
        }
    }
}
