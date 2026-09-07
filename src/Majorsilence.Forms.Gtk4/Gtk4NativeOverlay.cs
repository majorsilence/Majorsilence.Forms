using System.Collections.Generic;
using System.Drawing;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Positions a hosted native GTK widget over the Skia surface for a <see cref="MF.NativeControlHost"/>.
    /// Shared by <see cref="Gtk4WindowHost"/> and <see cref="MajorsilenceFormsPresenter"/>, both of which
    /// parent the <see cref="Gtk4SkiaSurface"/> inside a <c>Gtk.Overlay</c> so native widgets can be
    /// stacked on top.
    ///
    /// Unlike the Avalonia / Uno / WinForms backends there is no airspace problem: GTK 4 composites every
    /// widget (the drawing area and the native overlay alike) into one render tree, so a native widget on
    /// top of the Skia surface clips, blends and receives input correctly with nothing special.
    ///
    /// The native widget is added straight as an overlay child positioned with
    /// <c>halign/valign = Start</c> + top/left margins (the idiomatic GTK 4 way to place an overlay at
    /// x,y). <c>bounds</c>/<c>clip</c> arrive in logical pixels relative to the Majorsilence.Forms
    /// client origin, which are GTK's own logical (pre-scale) units, so no scaling conversion is
    /// needed. When the host is scrolled partly out of a viewport (<c>clip</c> smaller than
    /// <c>bounds</c>) the widget is placed and sized to the visible intersection — its content reflows
    /// into that box rather than translating under it, which the other backends avoid with a clip
    /// transform GTK 4 overlay children do not support here.
    /// </summary>
    internal static class Gtk4NativeOverlay
    {
        internal sealed class Entry
        {
            public Entry (global::Gtk.Widget child) => Child = child;

            public global::Gtk.Widget Child { get; }
            public bool InOverlay { get; set; }
        }

        internal static void Attach (global::Gtk.Overlay overlay, Dictionary<MF.NativeControlHost, Entry> overlays,
            MF.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not global::Gtk.Widget widget)
                return;

            if (overlays.TryGetValue (host, out var existing)) {
                if (ReferenceEquals (existing.Child, widget))
                    return;
                Detach (overlay, overlays, host);
            }

            if (widget.GetParent () is not null)
                widget.Unparent ();

            widget.SetHalign (global::Gtk.Align.Start);
            widget.SetValign (global::Gtk.Align.Start);
            widget.SetOverflow (global::Gtk.Overflow.Hidden);
            widget.SetVisible (false);

            overlays[host] = new Entry (widget);
        }

        internal static void Update (global::Gtk.Overlay overlay, Dictionary<MF.NativeControlHost, Entry> overlays,
            MF.NativeControlHost host, Rectangle bounds, Rectangle clip, bool visible)
        {
            if (!overlays.TryGetValue (host, out var entry))
                return;

            var rect = Rectangle.Intersect (bounds, clip);
            var show = visible && rect.Width > 0 && rect.Height > 0;

            entry.Child.SetVisible (show);
            if (!show) {
                if (entry.InOverlay) {
                    overlay.RemoveOverlay (entry.Child);
                    entry.InOverlay = false;
                }
                return;
            }

            entry.Child.SetMarginStart (rect.X);
            entry.Child.SetMarginTop (rect.Y);
            entry.Child.SetSizeRequest (rect.Width, rect.Height);

            if (!entry.InOverlay) {
                entry.InOverlay = true;
                var child = entry.Child;
                // SyncNativeControl runs inside the scene's layout pass, which here is inside GTK's
                // own snapshot — reparenting a widget mid-snapshot warns ("without a current
                // allocation"). Defer the add to the next idle turn so it lands between frames.
                global::GLib.Functions.IdleAdd (0, () => {
                    if (entry.InOverlay && child.GetParent () is null) {
                        overlay.AddOverlay (child);
                        overlay.SetMeasureOverlay (child, false);   // must not influence the overlay's own size
                        overlay.SetClipOverlay (child, true);       // keep it inside the overlay's bounds
                    }
                    return false;
                });
            }
        }

        internal static void Detach (global::Gtk.Overlay overlay, Dictionary<MF.NativeControlHost, Entry> overlays,
            MF.NativeControlHost host)
        {
            if (!overlays.Remove (host, out var entry))
                return;

            if (entry.InOverlay)
                overlay.RemoveOverlay (entry.Child);
        }
    }
}
