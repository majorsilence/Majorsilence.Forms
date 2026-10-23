using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>How a control's border is reached.</summary>
    internal enum BorderPaintMode
    {
        /// <summary>The border is the window's non-client frame (<c>BorderStyle.FixedSingle</c>): repaint it on <c>WM_NCPAINT</c>.</summary>
        NonClient,

        /// <summary>The control paints its own 1px border inside its client area (flat combo box, up-down): draw over it after <c>WM_PAINT</c>.</summary>
        ClientOverlay,

        /// <summary>A group box: erase the frame it drew and redraw it, leaving the caption alone.</summary>
        GroupBox
    }

    /// <summary>
    /// Gives a native control a border in a theme colour. System.Windows.Forms exposes only
    /// <c>BorderStyle</c>, never a border colour, so this subclasses the control's window (the way
    /// <see cref="WF.NativeWindow"/> is meant to be used) and repaints the 1px frame after the control has painted it, on
    /// <c>WM_NCPAINT</c>, <c>WM_PAINT</c> and <c>WM_PRINT</c> (which <c>DrawToBitmap</c> uses). It adds nothing else: no
    /// window styles change, the control keeps painting itself, and detaching it puts the original frame back.
    /// </summary>
    internal sealed class BorderPainter : WF.NativeWindow
    {
        private const int WM_NCPAINT = 0x0085;
        private const int WM_PAINT = 0x000F;
        private const int WM_PRINT = 0x0317;
        private const long PRF_CLIENT = 0x4;
        private const long PRF_NONCLIENT = 0x2;

        private readonly WF.Control control;

        public BorderPainter (WF.Control control, BorderPaintMode mode)
        {
            this.control = control;
            Mode = mode;

            control.HandleCreated += OnHandleCreated;
            control.HandleDestroyed += OnHandleDestroyed;

            if (control.IsHandleCreated)
                AssignHandle (control.Handle);
        }

        public BorderPaintMode Mode { get; }

        public Color Color { get; set; }

        /// <summary>The frame's thickness in device pixels.</summary>
        public int Thickness { get; set; } = 1;

        /// <summary>Whether the frame is dashes (<c>border-style: dashed</c>) rather than a line.</summary>
        public bool Dashed { get; set; }

        private void OnHandleCreated (object? sender, EventArgs e)
        {
            if (Handle == IntPtr.Zero && control.IsHandleCreated)
                AssignHandle (control.Handle);
        }

        private void OnHandleDestroyed (object? sender, EventArgs e)
        {
            if (Handle != IntPtr.Zero)
                ReleaseHandle ();
        }

        /// <summary>Stops painting and lets the control draw its own frame again.</summary>
        public void Detach ()
        {
            control.HandleCreated -= OnHandleCreated;
            control.HandleDestroyed -= OnHandleDestroyed;

            if (Handle != IntPtr.Zero)
                ReleaseHandle ();

            if (control.IsHandleCreated)
                control.Invalidate (true);
        }

        /// <summary>Repaints the frame now (the colour changed).</summary>
        public void Refresh ()
        {
            if (control.IsHandleCreated)
                control.Invalidate (true);
        }

        protected override void WndProc (ref WF.Message m)
        {
            switch (m.Msg) {
                case WM_NCPAINT when Mode == BorderPaintMode.NonClient:
                    base.WndProc (ref m);
                    Paint (IntPtr.Zero, nonClient: true);
                    return;

                case WM_PAINT when Mode != BorderPaintMode.NonClient:
                    base.WndProc (ref m);
                    Paint (IntPtr.Zero, nonClient: false);
                    return;

                case WM_PRINT:
                    base.WndProc (ref m);
                    var flags = (long) m.LParam;
                    if (Mode == BorderPaintMode.NonClient ? (flags & PRF_NONCLIENT) != 0 : (flags & PRF_CLIENT) != 0)
                        Paint (m.WParam, Mode == BorderPaintMode.NonClient);
                    return;
            }

            base.WndProc (ref m);
        }

        private void Paint (IntPtr hdc, bool nonClient)
        {
            if (!control.IsHandleCreated || control.IsDisposed)
                return;

            var owned = hdc == IntPtr.Zero;
            var dc = owned ? (nonClient ? GetWindowDC (Handle) : GetDC (Handle)) : hdc;
            if (dc == IntPtr.Zero)
                return;

            try {
                using var g = Graphics.FromHdc (dc);
                var size = nonClient ? control.Size : control.ClientSize;

                // WM_PRINT hands over the window's DC, whose origin is the window's corner; the client area sits inside the
                // non-client frame (a ProgressBar's, say). A paint message hands over the client's own DC.
                if (!nonClient && !owned)
                    g.TranslateTransform ((control.Width - control.ClientSize.Width) / 2, (control.Height - control.ClientSize.Height) / 2);

                if (Mode == BorderPaintMode.GroupBox)
                    DrawGroupBoxFrame (g, size);
                else
                    DrawFrame (g, size);
            } finally {
                if (owned)
                    _ = ReleaseDC (Handle, dc);
            }
        }

        private void DrawFrame (Graphics g, Size size)
        {
            var t = Math.Max (1, Thickness);

            if (Dashed) {
                // Dashes leave gaps, and the control's own solid frame would show through them: paint the frame's band in the
                // control's background first. Each dash and gap is three border-widths long, as in Majorsilence.Forms.
                using (var erase = new SolidBrush (control.BackColor)) {
                    g.FillRectangle (erase, 0, 0, size.Width, t);
                    g.FillRectangle (erase, 0, size.Height - t, size.Width, t);
                    g.FillRectangle (erase, 0, t, t, Math.Max (0, size.Height - 2 * t));
                    g.FillRectangle (erase, size.Width - t, t, t, Math.Max (0, size.Height - 2 * t));
                }

                using var pen = new Pen (Color, t) { DashPattern = new[] { 3f, 3f } };
                var half = t / 2f;
                g.DrawLine (pen, 0, half, size.Width, half);
                g.DrawLine (pen, 0, size.Height - half, size.Width, size.Height - half);
                g.DrawLine (pen, half, 0, half, size.Height);
                g.DrawLine (pen, size.Width - half, 0, size.Width - half, size.Height);
                return;
            }

            using var brush = new SolidBrush (Color);
            g.FillRectangle (brush, 0, 0, size.Width, t);
            g.FillRectangle (brush, 0, size.Height - t, size.Width, t);
            g.FillRectangle (brush, 0, t, t, Math.Max (0, size.Height - 2 * t));
            g.FillRectangle (brush, size.Width - t, t, t, Math.Max (0, size.Height - 2 * t));
        }

        // A group box draws its frame from half the caption's height down. Erase the band that frame occupies (outside the
        // caption, whose pixels must survive) and draw a plain rectangle in the theme colour with a gap for the caption.
        private void DrawGroupBoxFrame (Graphics g, Size size)
        {
            var t = Math.Max (1, Thickness);
            var scale = control.DeviceDpi / 96f;
            var top = Math.Max (0, control.Font.Height / 2);
            var hasCaption = !string.IsNullOrEmpty (control.Text);
            var captionLeft = (int) (8 * scale) - 2;
            var captionRight = hasCaption ? captionLeft + 4 + WF.TextRenderer.MeasureText (control.Text, control.Font, new Size (int.MaxValue, int.MaxValue), WF.TextFormatFlags.NoPadding).Width + 4 : captionLeft;

            using (var erase = new SolidBrush (control.BackColor)) {
                var band = t + 2;
                // top: left and right of the caption
                g.FillRectangle (erase, 0, Math.Max (0, top - 1), Math.Max (0, captionLeft), band);
                g.FillRectangle (erase, Math.Min (size.Width, captionRight), Math.Max (0, top - 1), Math.Max (0, size.Width - captionRight), band);
                g.FillRectangle (erase, 0, top, band, Math.Max (0, size.Height - top));
                g.FillRectangle (erase, size.Width - band, top, band, Math.Max (0, size.Height - top));
                g.FillRectangle (erase, 0, size.Height - band, size.Width, band);
            }

            using var brush = new SolidBrush (Color);
            g.FillRectangle (brush, 0, top, Math.Max (0, captionLeft), t);
            g.FillRectangle (brush, Math.Min (size.Width, captionRight), top, Math.Max (0, size.Width - captionRight), t);
            g.FillRectangle (brush, 0, top, t, Math.Max (0, size.Height - top));
            g.FillRectangle (brush, size.Width - t, top, t, Math.Max (0, size.Height - top));
            g.FillRectangle (brush, 0, size.Height - t, size.Width, t);
        }

        // ---- who may be painted --------------------------------------------------------------------

        private static readonly ConcurrentDictionary<Type, bool> paints_itself = new ();
        private static readonly string[] paint_methods = { "OnPaint", "OnPaintBackground", "WndProc" };

        /// <summary>
        /// Whether a control type draws itself in a way a repainted frame would fight: a type outside System.Windows.Forms that
        /// overrides <c>OnPaint</c>, <c>OnPaintBackground</c> or <c>WndProc</c> (an app's owner-drawn text box, a third-party
        /// control). Such a control keeps its own painting.
        /// </summary>
        public static bool PaintsItself (Type type)
            => paints_itself.GetOrAdd (type, static t => {
                var framework = typeof (WF.Control).Assembly;
                const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                for (var current = t; current is not null && current.Assembly != framework; current = current.BaseType)
                    foreach (var name in paint_methods)
                        if (current.GetMethod (name, Declared) is not null)
                            return true;

                return false;
            });

        private static readonly ConcurrentDictionary<Type, bool> overrides_paint = new ();

        /// <summary>
        /// Whether a type outside System.Windows.Forms overrides <c>OnPaint</c>: its <c>Paint</c> event is then raised only if the
        /// override calls the base, which the theme cannot know, so effects painted from that event are not attempted on it.
        /// </summary>
        public static bool OverridesPaint (Type type)
            => overrides_paint.GetOrAdd (type, static t => {
                var framework = typeof (WF.Control).Assembly;

                for (var current = t; current is not null && current.Assembly != framework; current = current.BaseType)
                    if (current.GetMethod ("OnPaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null)
                        return true;

                return false;
            });

        [DllImport ("user32.dll")]
        private static extern IntPtr GetWindowDC (IntPtr hwnd);

        [DllImport ("user32.dll")]
        private static extern IntPtr GetDC (IntPtr hwnd);

        [DllImport ("user32.dll")]
        private static extern int ReleaseDC (IntPtr hwnd, IntPtr hdc);
    }
}
