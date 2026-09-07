using System;
using System.Drawing;
using SkiaSharp;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// The shared GTK widget both Gtk4 hosts draw and receive input through: a <c>Gtk.DrawingArea</c>
    /// whose draw func renders the owning <see cref="MF.WindowBase"/> via <c>RenderFrame</c> straight
    /// into a Cairo image-surface buffer, and whose event controllers (click / motion / scroll / key)
    /// forward into the neutral <c>WindowBase.Handle*</c> path.
    ///
    /// Used by <see cref="Gtk4WindowHost"/> (Majorsilence.Forms owns the top-level window) and by
    /// <see cref="MajorsilenceFormsPresenter"/> (Majorsilence.Forms embedded in a host GTK app) — the
    /// same split the Avalonia / Uno / WPF backends make between their window host and their presenter.
    /// Composition rather than a widget subclass: gir.core's GObject subclassing needs an extra
    /// integration package and a registration call, and the draw func / controllers do not need it.
    /// </summary>
    internal sealed class Gtk4SkiaSurface
    {
        private readonly Func<MF.WindowBase?> _owner;

        /// <summary>The GTK widget to parent into a window or a presenter.</summary>
        public global::Gtk.DrawingArea Widget { get; }

        /// <summary>The last pointer position seen, in widget (logical) coordinates.</summary>
        internal (double X, double Y) LastPointer { get; private set; }

        /// <summary>The GDK event time of the last input event (for <c>Gdk.Toplevel.BeginMove</c>/<c>BeginResize</c>).</summary>
        internal uint LastEventTime { get; private set; }

        /// <summary>The drawing area's last known allocation, in logical pixels.</summary>
        internal Size ClientLogical { get; private set; } = new (0, 0);

        public Gtk4SkiaSurface (Func<MF.WindowBase?> owner)
        {
            _owner = owner;

            MF.Theme.WarmupFonts ();

            Widget = global::Gtk.DrawingArea.New ();
            Widget.SetHexpand (true);
            Widget.SetVexpand (true);
            Widget.SetFocusable (true);
            Widget.SetDrawFunc (Render);
            Widget.OnResize += (_, e) => ClientLogical = new Size (e.Width, e.Height);

            WireInput ();
        }

        /// <summary>The current monitor scale factor (logical → device pixels).</summary>
        public double Scaling
        {
            get {
                var s = Widget.GetScaleFactor ();
                return s > 0 ? s : 1.0;
            }
        }

        /// <summary>Queues a repaint. Must be called on the GTK main thread (the core marshals invalidates).</summary>
        public void RequestRender () => Widget.QueueDraw ();

        /// <summary>Takes keyboard focus.</summary>
        public void Focus () => Widget.GrabFocus ();

        // ── Rendering ────────────────────────────────────────────────────────────

        private void Render (global::Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            var owner = _owner ();
            if (owner is null)
                return;

            ClientLogical = new Size (width, height);
            var scale = Math.Max (1, area.GetScaleFactor ());
            var physW = Math.Max (1, width * scale);
            var physH = Math.Max (1, height * scale);

            // A fresh image surface per frame: once it is used as a paint source cairo takes a snapshot
            // of it, and marking a snapshotted surface dirty on the next pass trips an assertion. Frames
            // are drawn on resize / explicit Invalidate, not continuously, so the per-paint allocation
            // is the same order of cost as the WPF backend's WriteableBitmap present.
            using var surface = new Cairo.ImageSurface (Cairo.Format.Argb32, physW, physH);
            RenderInto (owner, surface, physW, physH, scale);
            surface.MarkDirty ();

            cr.Save ();
            if (scale != 1)
                cr.Scale (1.0 / scale, 1.0 / scale);
            cr.SetSourceSurface (surface, 0, 0);
            cr.Paint ();
            cr.Restore ();
        }

        private static unsafe void RenderInto (MF.WindowBase owner, Cairo.ImageSurface surface, int physW, int physH, double scale)
        {
            var data = surface.GetData ();
            fixed (byte* ptr = data) {
                // Cairo ARGB32 on a little-endian host is byte-order BGRA, premultiplied — SkiaSharp's Bgra8888/Premul.
                var info = new SKImageInfo (physW, physH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var skSurface = SKSurface.Create (info, (IntPtr) ptr, surface.Stride);
                if (skSurface is null)
                    return;

                skSurface.Canvas.Clear (SKColors.Transparent);
                owner.RenderFrame (skSurface.Canvas, physW, physH, scale);
                skSurface.Canvas.Flush ();
            }
        }

        // ── Input ────────────────────────────────────────────────────────────────

        private void WireInput ()
        {
            var click = global::Gtk.GestureClick.New ();
            click.SetButton (0);   // 0 == report every button
            click.OnPressed += (g, e) => {
                var controller = (global::Gtk.EventController) g;
                LastEventTime = controller.GetCurrentEventTime ();
                LastPointer = (e.X, e.Y);
                Widget.GrabFocus ();
                var (x, y) = Device (e.X, e.Y);
                _owner ()?.HandlePointerPressed (
                    Gtk4KeyInterop.ToButton (((global::Gtk.GestureClick) g).GetCurrentButton ()),
                    x, y, Gtk4KeyInterop.ModifierKeys (controller.GetCurrentEventState ()));
            };
            click.OnReleased += (g, e) => {
                var controller = (global::Gtk.EventController) g;
                LastEventTime = controller.GetCurrentEventTime ();
                LastPointer = (e.X, e.Y);
                var (x, y) = Device (e.X, e.Y);
                _owner ()?.HandlePointerReleased (
                    Gtk4KeyInterop.ToButton (((global::Gtk.GestureClick) g).GetCurrentButton ()),
                    x, y, Gtk4KeyInterop.ModifierKeys (controller.GetCurrentEventState ()));
            };
            Widget.AddController (click);

            var motion = global::Gtk.EventControllerMotion.New ();
            motion.OnMotion += (m, e) => {
                var controller = (global::Gtk.EventController) m;
                LastEventTime = controller.GetCurrentEventTime ();
                LastPointer = (e.X, e.Y);
                var state = controller.GetCurrentEventState ();
                var (x, y) = Device (e.X, e.Y);
                _owner ()?.HandlePointerMoved (Gtk4KeyInterop.ButtonsFromState (state), x, y, Gtk4KeyInterop.ModifierKeys (state));
            };
            motion.OnLeave += (_, _) => {
                var (x, y) = Device (LastPointer.X, LastPointer.Y);
                _owner ()?.HandlePointerExited (MF.MouseButtons.None, x, y, MF.Keys.None);
            };
            Widget.AddController (motion);

            var scroll = global::Gtk.EventControllerScroll.New (global::Gtk.EventControllerScrollFlags.BothAxes);
            scroll.OnScroll += (s, e) => {
                var state = ((global::Gtk.EventController) s).GetCurrentEventState ();
                var (x, y) = Device (LastPointer.X, LastPointer.Y);
                // GTK: +dy scrolls the content down; WinForms wheel delta is +120 per notch scrolling up.
                var delta = new Point ((int) Math.Round (-e.Dx * 120), (int) Math.Round (-e.Dy * 120));
                _owner ()?.HandlePointerWheel (MF.MouseButtons.None, x, y, delta, Gtk4KeyInterop.ModifierKeys (state));
                return true;
            };
            Widget.AddController (scroll);

            var keys = global::Gtk.EventControllerKey.New ();
            keys.OnKeyPressed += (_, e) => {
                var owner = _owner ();
                if (owner is null)
                    return false;

                var handled = owner.HandleKeyDown (Gtk4KeyInterop.ToKeys (e.Keyval, e.State));

                // No CharacterReceived equivalent fires here; derive the printable character straight
                // off the key value (mirrors the Uno backend).
                var codepoint = Gdk.Functions.KeyvalToUnicode (e.Keyval);
                if (codepoint != 0) {
                    var ch = char.ConvertFromUtf32 ((int) codepoint);
                    if (!string.IsNullOrEmpty (ch) && !char.IsControl (ch[0]))
                        handled |= owner.HandleTextInput (ch);
                }
                return handled;
            };
            keys.OnKeyReleased += (_, e) => _owner ()?.HandleKeyUp (Gtk4KeyInterop.ToKeys (e.Keyval, e.State));
            Widget.AddController (keys);
        }

        // Widget coords are logical; RenderFrame and the neutral input path take device pixels.
        private (int X, int Y) Device (double x, double y)
        {
            var s = Scaling;
            return ((int) Math.Round (x * s), (int) Math.Round (y * s));
        }
    }
}
