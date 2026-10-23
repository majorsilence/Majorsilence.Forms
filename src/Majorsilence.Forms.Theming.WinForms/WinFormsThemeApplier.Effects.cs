using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    // box-shadow, outline and background-image (gradients) on real WinForms controls. WinForms has no property for any of them, so
    // the theme paints them itself (WinFormsCssTheme.PaintEffects, on by default):
    //  - an outer shadow and an outline are painted by the control's PARENT, in its Paint event, around the child. A child window
    //    is clipped out of its parent's painting, so what the parent draws shows only outside the child -- exactly where an outer
    //    shadow and an outline belong -- and the control itself is untouched (and not shrunk to make room, as in the core, which
    //    cannot paint outside a control);
    //  - an inset shadow is painted over the control's face from its own Paint event, on the controls that raise one;
    //  - a gradient is rendered with Skia into a bitmap that becomes the control's BackgroundImage and follows its size.
    // The shape is the control's own: square, or a button's rounded Region.
    internal static partial class WinFormsThemeApplier
    {
        private sealed class EffectLook
        {
            public IReadOnlyList<ShadowSpec> Shadows { get; set; } = Array.Empty<ShadowSpec> ();
            public OutlineSpec? Outline { get; set; }
            public (int TopLeft, int TopRight, int BottomRight, int BottomLeft) Corners { get; set; }
            public Bitmap? Outer { get; set; }
            public int Pad { get; set; }
            public Bitmap? Inset { get; set; }
            public string ShadowKey { get; set; } = "";
            public Rectangle LastBounds { get; set; }
            public bool Hooked { get; set; }
            public bool PaintHooked { get; set; }
            public Bitmap? Gradient { get; set; }
            public string GradientKey { get; set; } = "";
            public bool OwnsBackgroundImage { get; set; }
            public bool GradientQueued { get; set; }
        }

        // Which children a parent paints effects for.
        private sealed class EffectParent
        {
            public List<WeakReference<WF.Control>> Children { get; } = new ();
            public bool Hooked { get; set; }
        }

        private static readonly ConditionalWeakTable<WF.Control, EffectParent> effect_parents = new ();

        private static string? EffectSelectorOf (WF.Control control) => control switch {
            WF.Form => "Form",
            WF.Button => "Button",
            WF.LinkLabel => "LinkLabel",
            WF.Label => "Label",
            WF.TextBoxBase => "TextBox",
            WF.NumericUpDown => "NumericUpDown",
            WF.ComboBox => "ComboBox",
            WF.CheckBox => "CheckBox",
            WF.RadioButton => "RadioButton",
            WF.GroupBox => "GroupBox",
            WF.ListBox => "ListBox",
            WF.ListView => "ListView",
            WF.TreeView => "TreeView",
            WF.DataGridView => "DataGridView",
            WF.PictureBox => "PictureBox",
            WF.ProgressBar => "ProgressBar",
            WF.TrackBar => "TrackBar",
            WF.MonthCalendar => "MonthCalendar",
            WF.PropertyGrid => "PropertyGrid",
            WF.TabControl => "TabControl",
            WF.SplitContainer => "SplitContainer",
            WF.Splitter => "Splitter",
            WF.DateTimePicker => "DateTimePicker",
            WF.Panel => "Panel",
            _ => null
        };

        // The controls that raise Paint themselves, which an inset shadow needs; and the ones that paint a BackgroundImage.
        private static bool RaisesPaint (WF.Control control) => control is WF.ButtonBase or WF.Label or WF.Panel or WF.GroupBox or WF.PictureBox or WF.Form;

        private static bool PaintsBackgroundImage (WF.Control control) => control is WF.Form or WF.ButtonBase or WF.Label or WF.Panel or WF.GroupBox;

        private static bool IsStrip (WF.Control control) => control is WF.ToolStrip;

        private static ThemeCssState EffectiveState (WF.Control control, ApplyContext ctx, string selector)
        {
            if (!tracking.TryGetValue (control, out var track) || !track.Installed)
                return ThemeCssState.Normal;

            var state = StateOf (control, selector, track);
            return state == ThemeCssState.Active && !ctx.Rules.HasState (selector, ThemeCssState.Active) ? ThemeCssState.Hover : state;
        }

        // Brings a control's effects in line with the current context and state.
        private static void ApplyEffects (WF.Control control, ApplyContext ctx)
        {
            if (EffectSelectorOf (control) is not { } selector)
                return;

            var cs = states.GetOrCreateValue (control);

            if (!WinFormsCssTheme.PaintEffects) {
                ClearEffects (control, cs);
                return;
            }

            var state = EffectiveState (control, ctx, selector);
            var chain = new List<ThemeCssState> { ThemeCssState.Normal };
            chain.AddRange (Chain (state));

            IReadOnlyList<ShadowSpec> shadows = Array.Empty<ShadowSpec> ();
            int? width = null, offset = null;
            string? style = null;
            Color? color = null;

            foreach (var s in chain) {
                if (ctx.Rules.Value (selector, null, s, "box-shadow") is { } shadowValue)
                    shadows = EffectRenderer.ReadShadows (shadowValue);
                if (ctx.Rules.Length (selector, null, s, "outline-width") is { } w)
                    width = w;
                if (ctx.Rules.Length (selector, null, s, "outline-offset") is { } o)
                    offset = o;
                if (ctx.Rules.Value (selector, null, s, "outline-style") is { Kind: ThemeCssValueKind.Keyword } k)
                    style = k.Keyword;
                if (ctx.Rules.Color (selector, null, s, "outline-color") is { } c)
                    color = Flat (ctx, c);
            }

            var gradients = ctx.Rules.Value (selector, null, ThemeCssState.Normal, "background-image")?.Gradients ?? Array.Empty<ThemeCssGradient> ();
            var corners = selector == "Button" ? ctx.Rules.Corners (selector, null, ThemeCssState.Normal) ?? (0, 0, 0, 0) : (0, 0, 0, 0);

            var wantsOutline = style is "solid" or "dashed" or "auto";
            OutlineSpec? outline = wantsOutline
                ? new OutlineSpec (Math.Max (0, width ?? 3), style == "dashed", color ?? control.ForeColor, offset ?? 0)
                : null;

            if (shadows.Count == 0 && outline is null && gradients.Count == 0 && cs.Effects is null)
                return;

            var look = cs.Effects ??= new EffectLook ();
            look.Corners = corners;
            look.Outline = outline is { Width: > 0 } ? outline : null;

            if (IsStrip (control) || control is WF.Form) {
                // A window and a strip are not painted by a parent; their shadows and outlines are reported, not drawn.
                if (shadows.Count > 0 || look.Outline is not null)
                    ctx.Report ($"'{selector}' is not painted by a parent, so its box-shadow and outline are not drawn.");
                shadows = Array.Empty<ShadowSpec> ();
                look.Outline = null;
            }

            HookEffects (control, look);
            BuildShadows (control, ctx, look, shadows);
            BuildGradient (control, ctx, look, selector, gradients);
            WatchParent (control, look);
            Reposition (control, look);
        }

        private static void HookEffects (WF.Control control, EffectLook look)
        {
            if (look.Hooked)
                return;

            look.Hooked = true;
            control.ParentChanged += static (sender, _) => OnEffectChange (sender, rebuild: true);
            control.LocationChanged += static (sender, _) => OnEffectChange (sender, rebuild: false);
            control.VisibleChanged += static (sender, _) => OnEffectChange (sender, rebuild: false);
            control.SizeChanged += static (sender, _) => OnEffectChange (sender, rebuild: true);
            control.Disposed += static (sender, _) => {
                if (sender is WF.Control c && states.TryGetValue (c, out var cs))
                    ClearEffects (c, cs);
            };
        }

        private static void OnEffectChange (object? sender, bool rebuild)
        {
            if (sender is not WF.Control control || control.IsDisposed || !states.TryGetValue (control, out var cs) || cs.Effects is not { } look)
                return;

            if (rebuild && Current is { } ctx) {
                // A burst of resizes becomes one rebuild per message-loop turn.
                if (look.GradientQueued)
                    return;

                if (control.IsHandleCreated) {
                    look.GradientQueued = true;
                    control.BeginInvoke (() => {
                        look.GradientQueued = false;
                        if (!control.IsDisposed && Current is { } latest)
                            ApplyEffects (control, latest);
                    });
                } else {
                    ApplyEffects (control, ctx);
                }

                return;
            }

            WatchParent (control, look);
            Reposition (control, look);
        }

        // ---- shadows ---------------------------------------------------------------------------------

        private static void BuildShadows (WF.Control control, ApplyContext ctx, EffectLook look, IReadOnlyList<ShadowSpec> shadows)
        {
            var key = $"{control.Size}|{look.Corners}|{string.Join (";", shadows)}";
            look.Shadows = shadows;

            if (key == look.ShadowKey)
                return;

            look.ShadowKey = key;
            look.Outer?.Dispose ();
            look.Inset?.Dispose ();
            look.Outer = EffectRenderer.OuterShadow (control.Size, look.Corners, shadows, out var pad);
            look.Pad = pad;
            look.Inset = EffectRenderer.InsetShadow (control.Size, look.Corners, shadows);

            if (look.Inset is not null && !look.PaintHooked) {
                if (RaisesPaint (control)) {
                    look.PaintHooked = true;
                    control.Paint += static (sender, e) => {
                        if (sender is WF.Control c && states.TryGetValue (c, out var cs) && cs.Effects?.Inset is { } inset)
                            e.Graphics.DrawImageUnscaled (inset, 0, 0);
                    };
                } else {
                    ctx.Report ($"'{control.GetType ().Name}' does not raise Paint, so its inset box-shadow is not drawn; an outer shadow still is.");
                }
            }

            control.Invalidate ();
        }

        // Makes sure the control's parent paints its shadow and outline, and that the parent is one we may paint on.
        private static void WatchParent (WF.Control control, EffectLook look)
        {
            if (control.Parent is not { } parent)
                return;

            var wants = look.Outer is not null || look.Outline is not null;
            var host = effect_parents.GetOrCreateValue (parent);

            if (wants && !host.Children.Exists (w => w.TryGetTarget (out var c) && ReferenceEquals (c, control)))
                host.Children.Add (new WeakReference<WF.Control> (control));

            if (!wants || host.Hooked)
                return;

            if (BorderPainter.OverridesPaint (parent.GetType ())) {
                Current?.Report ($"'{parent.GetType ().Name}' overrides OnPaint, so the shadows and outlines of the controls on it are left alone.");
                return;
            }

            host.Hooked = true;
            parent.Paint += static (sender, e) => {
                if (sender is WF.Control p && Current is not null)
                    PaintChildEffects (p, e);
            };
        }

        private static void PaintChildEffects (WF.Control parent, WF.PaintEventArgs e)
        {
            if (!effect_parents.TryGetValue (parent, out var host))
                return;

            foreach (var weak in host.Children.ToArray ()) {
                if (!weak.TryGetTarget (out var child) || child.IsDisposed || !ReferenceEquals (child.Parent, parent) || (!child.Visible && parent.Visible))   // a parent that is not shown yet makes every child read as hidden
                    continue;

                if (!states.TryGetValue (child, out var cs) || cs.Effects is not { } look)
                    continue;

                if (look.Outer is { } outer) {
                    var area = new Rectangle (child.Left - look.Pad, child.Top - look.Pad, outer.Width, outer.Height);
                    if (e.ClipRectangle.IntersectsWith (area))
                        e.Graphics.DrawImageUnscaled (outer, area.Location);
                }

                if (look.Outline is { } outline)
                    DrawOutline (e.Graphics, child.Bounds, look.Corners, outline);
            }
        }

        // A ring outside the control's border box: it takes no room and follows the corner radii, which grow with the offset.
        private static void DrawOutline (Graphics g, Rectangle box, (int TopLeft, int TopRight, int BottomRight, int BottomLeft) corners, OutlineSpec outline)
        {
            var grow = outline.Offset + outline.Width / 2f;
            var ring = RectangleF.Inflate (box, grow, grow);
            if (ring.Width <= 0 || ring.Height <= 0)
                return;

            var previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var pen = new Pen (outline.Color, outline.Width) { Alignment = PenAlignment.Center };
            if (outline.Dashed)
                pen.DashPattern = new[] { 3f, 3f };

            int Grow (int radius) => radius <= 0 ? 0 : (int) Math.Round (radius + grow);
            using var path = RoundedPath (Rectangle.Round (ring), Grow (corners.TopLeft), Grow (corners.TopRight), Grow (corners.BottomRight), Grow (corners.BottomLeft));
            g.DrawPath (pen, path);
            g.SmoothingMode = previous;
        }

        // Invalidates where the control was and is, widened by what it paints around itself.
        private static void Reposition (WF.Control control, EffectLook look)
        {
            var reach = look.Pad + (look.Outline is { } o ? o.Offset + o.Width + 2 : 0) + 2;
            var now = control.Bounds;

            if (control.Parent is { } parent && (look.Outer is not null || look.Outline is not null || look.LastBounds != Rectangle.Empty)) {
                var area = Rectangle.Union (now, look.LastBounds == Rectangle.Empty ? now : look.LastBounds);
                area.Inflate (reach, reach);
                parent.Invalidate (area);
            }

            look.LastBounds = now;
        }

        // ---- gradients -------------------------------------------------------------------------------

        private static void BuildGradient (WF.Control control, ApplyContext ctx, EffectLook look, string selector, IReadOnlyList<ThemeCssGradient> gradients)
        {
            if (!PaintsBackgroundImage (control)) {
                if (gradients.Count > 0)
                    ctx.Report ($"'{control.GetType ().Name}' does not paint a BackgroundImage, so its background-image is not drawn.");
                return;
            }

            if (gradients.Count == 0) {
                if (look.OwnsBackgroundImage) {
                    control.BackgroundImage = null;
                    look.OwnsBackgroundImage = false;
                }

                look.Gradient?.Dispose ();
                look.Gradient = null;
                look.GradientKey = "";
                return;
            }

            var size = control is WF.Form form ? form.ClientSize : control.ClientSize;
            if (size.Width <= 0 || size.Height <= 0)
                return;

            var key = $"{size}|{control.BackColor.ToArgb ()}|{string.Join (",", gradients.Select (g => g.Css))}";

            if (key != look.GradientKey) {
                var previous = look.Gradient;

                // An image the app put on the control is its own.
                if (control.BackgroundImage is not null && !ReferenceEquals (control.BackgroundImage, previous)) {
                    ctx.Report ($"'{control.GetType ().Name}' has a BackgroundImage of its own, so its background-image is not drawn.");
                    return;
                }

                look.Gradient = EffectRenderer.Gradient (size, control.BackColor, gradients);
                look.GradientKey = key;
                control.BackgroundImage = look.Gradient;
                control.BackgroundImageLayout = WF.ImageLayout.Stretch;
                look.OwnsBackgroundImage = true;
                previous?.Dispose ();
            }
        }

        private static void ClearEffects (WF.Control control, ControlState cs)
        {
            if (cs.Effects is not { } look)
                return;

            var hadParentPaint = look.Outer is not null || look.Outline is not null;
            look.Outer?.Dispose ();
            look.Outer = null;
            look.Inset?.Dispose ();
            look.Inset = null;
            look.Outline = null;
            look.Shadows = Array.Empty<ShadowSpec> ();
            look.ShadowKey = "";

            if (look.OwnsBackgroundImage && !control.IsDisposed)
                control.BackgroundImage = null;

            look.OwnsBackgroundImage = false;
            look.Gradient?.Dispose ();
            look.Gradient = null;
            look.GradientKey = "";

            if (hadParentPaint && control.Parent is { IsDisposed: false } parent) {
                var area = control.Bounds;
                area.Inflate (look.Pad + 40, look.Pad + 40);
                parent.Invalidate (area);
            }

            if (!control.IsDisposed)
                control.Invalidate ();
        }
    }
}
