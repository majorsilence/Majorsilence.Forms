using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>Everything one apply resolved; the tree walker and the deferred hooks (HandleCreated,
    /// DrawItem, ControlAdded) all read the CURRENT context so a re-apply changes what they do.</summary>
    internal sealed class ApplyContext
    {
        public ApplyContext (ThemeRuleSet rules, TokenSnapshot tokens, StripColors strips)
        {
            Rules = rules;
            Tokens = tokens;
            Strips = strips;
        }

        public ThemeRuleSet Rules { get; }
        public TokenSnapshot Tokens { get; }
        public StripColors Strips { get; }
    }

    /// <summary>
    /// Walks a System.Windows.Forms control tree and sets native properties from the applied
    /// stylesheet chain — the WinForms half of <see cref="WinFormsCssTheme"/>. Also owns the
    /// per-control opt-out: a BackColor/ForeColor/Font the app set explicitly (or changed after we
    /// styled it) is pinned and never overwritten, mirroring "explicit per-control values win".
    /// </summary>
    internal static class WinFormsThemeApplier
    {
        /// <summary>The last applied context; null until the first apply.</summary>
        public static ApplyContext? Current { get; set; }

        // What this applier has done to each control it has seen. Weak so tracked controls can be
        // collected normally.
        private sealed class ControlState
        {
            public HashSet<string> Pinned { get; } = new ();
            public Dictionary<string, object?> Applied { get; } = new ();
            public bool ChildHookInstalled { get; set; }
            public bool HandleHookInstalled { get; set; }
            public bool TabDrawHookInstalled { get; set; }
            public bool ItemDrawHookInstalled { get; set; }
            public bool RegionHookInstalled { get; set; }
            public int CornerRadius { get; set; }
        }

        private static readonly ConditionalWeakTable<WF.Control, ControlState> states = new ();

        private static readonly Dictionary<string, Font> font_cache = new ();

        // ---- tree walking ---------------------------------------------------------------------

        /// <summary>Applies the current context to a control and everything under it, and hooks
        /// ControlAdded so controls created later are styled too.</summary>
        public static void ApplyTree (WF.Control control)
        {
            if (Current is null)
                return;

            ApplyControl (control, Current);
            HookChildren (control);

            if (control.ContextMenuStrip is { } contextMenu)
                ApplyControl (contextMenu, Current);

            foreach (WF.Control child in control.Controls)
                ApplyTree (child);
        }

        private static void HookChildren (WF.Control control)
        {
            var state = states.GetOrCreateValue (control);
            if (state.ChildHookInstalled)
                return;

            state.ChildHookInstalled = true;
            control.ControlAdded += static (_, e) => {
                if (e.Control is not null)
                    ApplyTree (e.Control);
            };
        }

        // ---- the pinned-property setter ---------------------------------------------------------

        // Sets an ambient property (BackColor / ForeColor / Font) unless the app owns it: a value the
        // app set explicitly before we first saw the control (PropertyDescriptor.ShouldSerializeValue,
        // the public spelling of ShouldSerialize*), or one it changed after we styled it (the current
        // value no longer matches what we last set).
        private static void SetTracked (WF.Control control, string property, object value)
        {
            var state = states.GetOrCreateValue (control);
            if (state.Pinned.Contains (property))
                return;

            var descriptor = TypeDescriptor.GetProperties (control)[property];
            if (descriptor is null)
                return;

            var explicitlySet = descriptor.ShouldSerializeValue (control);

            if (!state.Applied.TryGetValue (property, out var applied) || ReferenceEquals (applied, Ambient)) {
                // First sight, or last time the inherited value already matched and we set nothing: an
                // explicit value now means the app owns it.
                if (explicitlySet) {
                    state.Pinned.Add (property);
                    return;
                }
            } else if (!Equals (descriptor.GetValue (control), applied)) {
                state.Pinned.Add (property);
                return;
            }

            if (!explicitlySet && Equals (descriptor.GetValue (control), value)) {
                // Inherited from the parent and already right: leave the ambient inheritance intact
                // (it follows the parent if the next apply moves it) rather than pinning a copy.
                state.Applied[property] = Ambient;
                return;
            }

            if (!Equals (descriptor.GetValue (control), value))
                descriptor.SetValue (control, value);

            state.Applied[property] = value;
        }

        // Marker for "the inherited value matched, nothing was set" in ControlState.Applied.
        private static readonly object Ambient = new ();

        private static void SetBackColor (WF.Control control, Color color) => SetTracked (control, "BackColor", color);

        private static void SetForeColor (WF.Control control, Color color) => SetTracked (control, "ForeColor", color);

        private static void SetFont (WF.Control control, FontSpec spec)
        {
            if (spec.IsEmpty)
                return;

            SetTracked (control, "Font", BuildFont (control.Font, spec));
        }

        // ---- fonts ------------------------------------------------------------------------------

        /// <summary>Builds the font one rule asks for on top of a basis font: unmentioned pieces
        /// (family, size, weight, slant) keep the basis value. CSS px convert to points at 96 dpi.</summary>
        public static Font BuildFont (Font basis, FontSpec spec)
        {
            var family = spec.Families is null ? basis.FontFamily : ResolveFamily (spec.Families) ?? basis.FontFamily;
            var size = spec.SizePixels is { } px ? px * 72f / 96f : basis.SizeInPoints;
            var style = basis.Style;

            if (spec.Weight is { } weight)
                style = weight >= 600 ? style | FontStyle.Bold : style & ~FontStyle.Bold;
            if (spec.Style is { } slant)
                style = slant == "normal" ? style & ~FontStyle.Italic : style | FontStyle.Italic;

            var key = $"{family.Name}|{size}|{(int) style}";

            lock (font_cache) {
                if (!font_cache.TryGetValue (key, out var font)) {
                    font = new Font (family, size, style, GraphicsUnit.Point);
                    font_cache[key] = font;
                }

                return font;
            }
        }

        private static FontFamily? ResolveFamily (IReadOnlyList<string> families)
        {
            foreach (var name in families) {
                switch (name.ToLowerInvariant ()) {
                    case "sans-serif":
                        return FontFamily.GenericSansSerif;
                    case "serif":
                        return FontFamily.GenericSerif;
                    case "monospace":
                        return FontFamily.GenericMonospace;
                }

                try {
                    return new FontFamily (name);
                } catch (ArgumentException) {
                    // Not installed; try the next family in the list, like CSS does.
                }
            }

            return null;
        }

        // ---- per-control application --------------------------------------------------------------

        private static void ApplyControl (WF.Control control, ApplyContext ctx)
        {
            switch (control) {
                case WF.Form form:
                    ApplyForm (form, ctx);
                    break;
                case WF.DataGridView grid:
                    ApplyDataGridView (grid, ctx);
                    break;
                case WF.PropertyGrid propertyGrid:
                    ApplyPropertyGrid (propertyGrid, ctx);
                    break;
                case WF.TreeView tree:
                    ApplyTreeView (tree, ctx);
                    break;
                case WF.ListView listView:
                    ApplyListLike (listView, "ListView", ctx, width => listView.BorderStyle = ToBorderStyle (width));
                    ApplyListViewSelection (listView);
                    break;
                case WF.ListBox listBox: // covers CheckedListBox
                    ApplyListLike (listBox, "ListBox", ctx, width => listBox.BorderStyle = ToBorderStyle (width));
                    ApplyListBoxSelection (listBox);
                    break;
                case WF.ProgressBar progress:
                    ApplyProgressBar (progress, ctx);
                    break;
                case WF.ComboBox combo:
                    combo.FlatStyle = WF.FlatStyle.Flat;
                    SetBackColor (combo, Resolve (ctx, "ComboBox", "background-color", ctx.Tokens.ControlMid));
                    SetForeColor (combo, Resolve (ctx, "ComboBox", "color", ctx.Tokens.Foreground));
                    SetFont (combo, ctx.Rules.Font ("ComboBox"));
                    break;
                case WF.NumericUpDown numeric:
                    SetBackColor (numeric, Resolve (ctx, "NumericUpDown", "background-color", ctx.Tokens.ControlMid));
                    SetForeColor (numeric, Resolve (ctx, "NumericUpDown", "color", ctx.Tokens.Foreground));
                    SetFont (numeric, ctx.Rules.Font ("NumericUpDown"));
                    if (ctx.Rules.Length ("NumericUpDown", null, false, "border-width") is { } numericBorder)
                        numeric.BorderStyle = ToBorderStyle (numericBorder);
                    break;
                case WF.DateTimePicker picker:
                    SetBackColor (picker, Resolve (ctx, "TextBox", "background-color", ctx.Tokens.ControlLow));
                    SetForeColor (picker, Resolve (ctx, "TextBox", "color", ctx.Tokens.Foreground));
                    SetFont (picker, ctx.Rules.Font ("TextBox"));
                    break;
                case WF.TextBoxBase textBox:
                    SetBackColor (textBox, Resolve (ctx, "TextBox", "background-color", ctx.Tokens.ControlLow));
                    SetForeColor (textBox, Resolve (ctx, "TextBox", "color", ctx.Tokens.Foreground));
                    SetFont (textBox, ctx.Rules.Font ("TextBox"));
                    if (ctx.Rules.Length ("TextBox", null, false, "border-width") is { } textBorder)
                        textBox.BorderStyle = ToBorderStyle (textBorder);
                    break;
                case WF.LinkLabel link:
                    ApplyLinkLabel (link, ctx);
                    break;
                case WF.Label label:
                    ApplyDeclaredOnly (label, "Label", ctx);
                    break;
                case WF.Button button:
                    ApplyButton (button, ctx);
                    break;
                case WF.CheckBox checkBox:
                    ApplyDeclaredOnly (checkBox, "CheckBox", ctx);
                    break;
                case WF.RadioButton radio:
                    ApplyDeclaredOnly (radio, "RadioButton", ctx);
                    break;
                case WF.GroupBox groupBox:
                    ApplyDeclaredOnly (groupBox, "GroupBox", ctx);
                    break;
                case WF.MonthCalendar calendar:
                    SetBackColor (calendar, Resolve (ctx, "MonthCalendar", "background-color", ctx.Tokens.ControlLow));
                    SetForeColor (calendar, Resolve (ctx, "MonthCalendar", "color", ctx.Tokens.Foreground));
                    break;
                case WF.TrackBar trackBar:
                    ApplyDeclaredOnly (trackBar, "TrackBar", ctx);
                    break;
                case WF.TabControl tabControl:
                    ApplyTabControl (tabControl, ctx);
                    break;
                case WF.SplitContainer splitContainer:
                    ApplyDeclaredOnly (splitContainer, "SplitContainer", ctx);
                    break;
                case WF.Splitter splitter:
                    ApplyDeclaredOnly (splitter, "Splitter", ctx);
                    break;
                case WF.MenuStrip menuStrip:
                    ApplyStrip (menuStrip, "Menu", ctx, ctx.Strips.MenuBackground);
                    break;
                case WF.StatusStrip statusStrip:
                    ApplyStrip (statusStrip, "StatusBar", ctx, ctx.Strips.StatusBarBackground);
                    break;
                case WF.ToolStripDropDown dropDown: // ContextMenuStrip, ToolStripDropDownMenu
                    ApplyStrip (dropDown, "MenuDropDown", ctx, ctx.Strips.DropDownBackground);
                    break;
                case WF.ToolStrip toolStrip:
                    ApplyStrip (toolStrip, "ToolBar", ctx, ctx.Strips.ToolBarBackground);
                    break;
                case WF.PictureBox pictureBox:
                    ApplyDeclaredOnly (pictureBox, "PictureBox", ctx);
                    if (ctx.Rules.Length ("PictureBox", null, false, "border-width") is { } pictureBorder)
                        pictureBox.BorderStyle = ToBorderStyle (pictureBorder);
                    break;
                case WF.ScrollBar:
                    // Native-drawn; nothing applies (the support matrix says so and the diagnostics
                    // report any ScrollBar rule).
                    break;
                case WF.Panel panel: // covers FlowLayoutPanel, TableLayoutPanel, TabPage, SplitterPanel
                    ApplyDeclaredOnly (panel, "Panel", ctx);
                    if (panel is not WF.TabPage && ctx.Rules.Length ("Panel", null, false, "border-width") is { } panelBorder)
                        panel.BorderStyle = ToBorderStyle (panelBorder);
                    break;
                default:
                    // Everything else inherits the form's ambient BackColor / ForeColor / Font.
                    break;
            }
        }

        // The effective colour of one (selector, property): the rule value if the sheet declares it,
        // the theme token default otherwise, flattened over the theme background because WinForms
        // does not composite alpha.
        private static Color Resolve (ApplyContext ctx, string selector, string property, Color tokenDefault, string? part = null, bool hover = false)
            => TokenSnapshot.Flatten (ctx.Rules.Color (selector, part, hover, property) ?? tokenDefault, ctx.Tokens.Background);

        // For controls whose Majorsilence.Forms default is the ambient background: apply only what the
        // sheet declares and let WinForms ambient inheritance provide the rest.
        private static void ApplyDeclaredOnly (WF.Control control, string selector, ApplyContext ctx)
        {
            if (ctx.Rules.Color (selector, null, false, "background-color") is { } bg)
                SetBackColor (control, TokenSnapshot.Flatten (bg, ctx.Tokens.Background));
            if (ctx.Rules.Color (selector, null, false, "color") is { } fg)
                SetForeColor (control, TokenSnapshot.Flatten (fg, ctx.Tokens.Background));
            SetFont (control, ctx.Rules.Font (selector));
        }

        private static WF.BorderStyle ToBorderStyle (int width)
            => width <= 0 ? WF.BorderStyle.None : WF.BorderStyle.FixedSingle;

        // ---- forms --------------------------------------------------------------------------------

        private static void ApplyForm (WF.Form form, ApplyContext ctx)
        {
            var bg = Resolve (ctx, "Form", "background-color", ctx.Tokens.Background);
            var fg = Resolve (ctx, "Form", "color", ctx.Tokens.Foreground);

            SetBackColor (form, bg);
            SetForeColor (form, fg);
            SetFont (form, ctx.Rules.Font ("Form"));

            var state = states.GetOrCreateValue (form);
            if (!state.HandleHookInstalled) {
                state.HandleHookInstalled = true;
                form.HandleCreated += static (sender, _) => {
                    if (sender is WF.Form f && Current is { } current)
                        ApplyTitleBar (f, current);
                };
            }

            if (form.IsHandleCreated)
                ApplyTitleBar (form, ctx);
        }

        private static void ApplyTitleBar (WF.Form form, ApplyContext ctx)
        {
            var bg = Resolve (ctx, "Form", "background-color", ctx.Tokens.Background);
            var fg = Resolve (ctx, "Form", "color", ctx.Tokens.Foreground);
            var border = ctx.Rules.Color ("Form", null, false, "border-color");

            Dwm.ApplyTitleBar (form.Handle, bg, fg,
                border is { } b ? TokenSnapshot.Flatten (b, ctx.Tokens.Background) : null,
                ctx.Tokens.IsDark);
        }

        // ---- buttons ------------------------------------------------------------------------------

        private static void ApplyButton (WF.Button button, ApplyContext ctx)
        {
            button.FlatStyle = WF.FlatStyle.Flat;

            SetBackColor (button, Resolve (ctx, "Button", "background-color", ctx.Tokens.ControlMid));
            SetForeColor (button, Resolve (ctx, "Button", "color", ctx.Tokens.Foreground));
            SetFont (button, ctx.Rules.Font ("Button"));

            var width = ctx.Rules.Length ("Button", null, false, "border-width") ?? 1;
            button.FlatAppearance.BorderSize = Math.Max (0, width);
            if (width > 0)
                button.FlatAppearance.BorderColor = Resolve (ctx, "Button", "border-color", ctx.Tokens.BorderLow);

            var hover = Resolve (ctx, "Button", "background-color", ctx.Tokens.Accent, hover: true);
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = TokenSnapshot.Darken (hover, 0.12);

            var state = states.GetOrCreateValue (button);
            state.CornerRadius = ctx.Rules.Length ("Button", null, false, "border-radius") ?? 0;

            if (!state.RegionHookInstalled) {
                state.RegionHookInstalled = true;
                button.Resize += static (sender, _) => {
                    if (sender is WF.Button b)
                        UpdateRegion (b);
                };
            }

            UpdateRegion (button);
        }

        // border-radius, approximately: a rounded Region clipping the control. No anti-aliasing --
        // that is the documented limit of the seam.
        private static void UpdateRegion (WF.Button button)
        {
            if (!states.TryGetValue (button, out var state))
                return;

            var radius = state.CornerRadius;

            if (radius <= 0) {
                if (button.Region is not null)
                    button.Region = null;
                return;
            }

            var bounds = button.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var diameter = Math.Min (radius * 2, Math.Min (bounds.Width, bounds.Height));
            using var path = new GraphicsPath ();
            path.AddArc (bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc (bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc (bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc (bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure ();
            button.Region = new Region (path);
        }

        // ---- link labels ----------------------------------------------------------------------------

        private static void ApplyLinkLabel (WF.LinkLabel link, ApplyContext ctx)
        {
            if (ctx.Rules.Color ("LinkLabel", null, false, "background-color") is { } bg)
                SetBackColor (link, TokenSnapshot.Flatten (bg, ctx.Tokens.Background));
            SetFont (link, ctx.Rules.Font ("LinkLabel"));

            var linkColor = Resolve (ctx, "LinkLabel", "color", ctx.Tokens.Accent);
            link.LinkColor = linkColor;
            link.VisitedLinkColor = linkColor;

            // :hover color has no true hover seam; ActiveLinkColor (shown while pressed) is the
            // documented approximation.
            link.ActiveLinkColor = ctx.Rules.Color ("LinkLabel", null, true, "color") is { } active
                ? TokenSnapshot.Flatten (active, ctx.Tokens.Background)
                : TokenSnapshot.Darken (linkColor, 0.2);
        }

        // ---- lists and trees -------------------------------------------------------------------------

        private static void ApplyListLike (WF.Control control, string selector, ApplyContext ctx, Action<int> setBorder)
        {
            SetBackColor (control, Resolve (ctx, selector, "background-color", ctx.Tokens.ControlLow));
            SetForeColor (control, Resolve (ctx, selector, "color", ctx.Tokens.Foreground));
            SetFont (control, ctx.Rules.Font (selector));

            if (ctx.Rules.Length (selector, null, false, "border-width") is { } width)
                setBorder (width);
        }

        private static void ApplyTreeView (WF.TreeView tree, ApplyContext ctx)
        {
            ApplyListLike (tree, "TreeView", ctx, width => tree.BorderStyle = ToBorderStyle (width));
            tree.LineColor = Resolve (ctx, "TreeView", "color", ctx.Tokens.Foreground);

            // The selected node otherwise follows SystemColors.Highlight. OwnerDrawText leaves the
            // glyphs, lines and images native and hands us only the text cell, which is all the
            // ::selection part colours.
            var state = states.GetOrCreateValue (tree);
            if (!state.ItemDrawHookInstalled) {
                if (tree.DrawMode != WF.TreeViewDrawMode.Normal)
                    return;   // the app owner-draws already; its colours win

                state.ItemDrawHookInstalled = true;
                tree.DrawMode = WF.TreeViewDrawMode.OwnerDrawText;
                tree.DrawNode += static (sender, e) => {
                    if (sender is WF.TreeView tv && Current is { } current)
                        DrawTreeNode (tv, e, current);
                };
            }

            tree.Invalidate ();
        }

        private static void DrawTreeNode (WF.TreeView tree, WF.DrawTreeNodeEventArgs e, ApplyContext ctx)
        {
            if (e.Node is null || (e.State & WF.TreeNodeStates.Selected) == 0 || e.Bounds.Width <= 0) {
                e.DrawDefault = true;
                return;
            }

            var bg = Resolve (ctx, "TreeView", "background-color", ctx.Tokens.HighlightLow, part: "selection");
            var fg = ctx.Rules.Color ("TreeView", "selection", false, "color") is { } c ? TokenSnapshot.Flatten (c, bg) : tree.ForeColor;

            using (var brush = new SolidBrush (bg))
                e.Graphics.FillRectangle (brush, e.Bounds);

            WF.TextRenderer.DrawText (e.Graphics, e.Node.Text, e.Node.NodeFont ?? tree.Font, e.Bounds, fg,
                WF.TextFormatFlags.VerticalCenter | WF.TextFormatFlags.Left | WF.TextFormatFlags.SingleLine | WF.TextFormatFlags.NoPrefix);
        }

        // ListBox selection: owner-draw the items (fixed height, same as Normal) so the selected one
        // takes the ::selection colours. CheckedListBox only accepts DrawMode.Normal, and a list the
        // app already owner-draws keeps its own painting.
        private static void ApplyListBoxSelection (WF.ListBox list)
        {
            if (list is WF.CheckedListBox)
                return;

            var state = states.GetOrCreateValue (list);
            if (!state.ItemDrawHookInstalled) {
                if (list.DrawMode != WF.DrawMode.Normal)
                    return;

                state.ItemDrawHookInstalled = true;
                list.DrawMode = WF.DrawMode.OwnerDrawFixed;
                list.DrawItem += static (sender, e) => {
                    if (sender is WF.ListBox lb && Current is { } current)
                        DrawListBoxItem (lb, e, current);
                };
            }

            list.Invalidate ();
        }

        private static void DrawListBoxItem (WF.ListBox list, WF.DrawItemEventArgs e, ApplyContext ctx)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count)
                return;

            var selected = (e.State & WF.DrawItemState.Selected) != 0;
            var bg = selected ? Resolve (ctx, "ListBox", "background-color", ctx.Tokens.HighlightLow, part: "selection") : list.BackColor;
            var fg = list.Enabled ? list.ForeColor : ctx.Tokens.ForegroundDisabled;
            if (selected && ctx.Rules.Color ("ListBox", "selection", false, "color") is { } c)
                fg = TokenSnapshot.Flatten (c, bg);

            using (var brush = new SolidBrush (bg))
                e.Graphics.FillRectangle (brush, e.Bounds);

            var textBounds = new Rectangle (e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 2, e.Bounds.Height);
            WF.TextRenderer.DrawText (e.Graphics, list.GetItemText (list.Items[e.Index]), e.Font ?? list.Font, textBounds, fg,
                WF.TextFormatFlags.VerticalCenter | WF.TextFormatFlags.Left | WF.TextFormatFlags.SingleLine | WF.TextFormatFlags.NoPrefix
                | (list.RightToLeft == WF.RightToLeft.Yes ? WF.TextFormatFlags.RightToLeft | WF.TextFormatFlags.Right : 0));
        }

        // ListView selection and column headers, in Details view only: the other views (icons, tiles,
        // groups) and check boxes are laid out natively in ways owner draw cannot reproduce, so those
        // lists keep the system look.
        private static void ApplyListViewSelection (WF.ListView list)
        {
            var state = states.GetOrCreateValue (list);
            if (!state.ItemDrawHookInstalled) {
                if (list.OwnerDraw || list.CheckBoxes)
                    return;

                state.ItemDrawHookInstalled = true;
                list.OwnerDraw = true;
                list.DrawColumnHeader += static (sender, e) => {
                    if (sender is WF.ListView lv && Current is { } current)
                        DrawListViewHeader (lv, e, current);
                    else
                        e.DrawDefault = true;
                };
                list.DrawItem += static (sender, e) => {
                    if (sender is WF.ListView lv && Current is { } current)
                        DrawListViewItem (lv, e, current);
                    else
                        e.DrawDefault = true;
                };
                list.DrawSubItem += static (sender, e) => {
                    if (sender is WF.ListView lv && Current is { } current)
                        DrawListViewSubItem (lv, e, current);
                    else
                        e.DrawDefault = true;
                };
            }

            list.Invalidate ();
        }

        private static bool IsSelected (WF.ListViewItem item, WF.ListViewItemStates state)
            => item.Selected || (state & WF.ListViewItemStates.Selected) != 0;

        private static void DrawListViewHeader (WF.ListView list, WF.DrawListViewColumnHeaderEventArgs e, ApplyContext ctx)
        {
            var bg = list.BackColor;
            var line = TokenSnapshot.Flatten (ctx.Tokens.BorderLow, bg);

            using (var brush = new SolidBrush (bg))
                e.Graphics.FillRectangle (brush, e.Bounds);
            using (var pen = new Pen (line)) {
                e.Graphics.DrawLine (pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                e.Graphics.DrawLine (pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            var textBounds = new Rectangle (e.Bounds.X + 4, e.Bounds.Y, Math.Max (0, e.Bounds.Width - 8), e.Bounds.Height);
            WF.TextRenderer.DrawText (e.Graphics, e.Header?.Text ?? "", list.Font, textBounds, list.ForeColor,
                Align (e.Header?.TextAlign ?? WF.HorizontalAlignment.Left) | WF.TextFormatFlags.VerticalCenter | WF.TextFormatFlags.SingleLine | WF.TextFormatFlags.EndEllipsis | WF.TextFormatFlags.NoPrefix);
        }

        private static void DrawListViewItem (WF.ListView list, WF.DrawListViewItemEventArgs e, ApplyContext ctx)
        {
            if (list.View != WF.View.Details || e.Item is null) {
                e.DrawDefault = true;
                return;
            }

            if (IsSelected (e.Item, e.State))
                using (var brush = new SolidBrush (Resolve (ctx, "ListView", "background-color", ctx.Tokens.HighlightLow, part: "selection")))
                    e.Graphics.FillRectangle (brush, e.Bounds);
        }

        private static void DrawListViewSubItem (WF.ListView list, WF.DrawListViewSubItemEventArgs e, ApplyContext ctx)
        {
            if (list.View != WF.View.Details || e.SubItem is null || e.Item is null) {
                e.DrawDefault = true;
                return;
            }

            var selected = IsSelected (e.Item, e.ItemState);
            if (!selected) {
                e.DrawDefault = true;
                return;
            }

            var bg = Resolve (ctx, "ListView", "background-color", ctx.Tokens.HighlightLow, part: "selection");
            var fg = ctx.Rules.Color ("ListView", "selection", false, "color") is { } c ? TokenSnapshot.Flatten (c, bg) : list.ForeColor;
            var bounds = e.Bounds;

            // The first column carries the item's image, which the default painter would have drawn.
            if (e.ColumnIndex == 0 && list.SmallImageList is { } images) {
                var index = e.Item.ImageIndex >= 0 ? e.Item.ImageIndex : images.Images.IndexOfKey (e.Item.ImageKey);
                if (index >= 0 && index < images.Images.Count) {
                    images.Draw (e.Graphics, bounds.X + 2, bounds.Y + (bounds.Height - images.ImageSize.Height) / 2, index);
                    bounds = new Rectangle (bounds.X + images.ImageSize.Width + 4, bounds.Y, Math.Max (0, bounds.Width - images.ImageSize.Width - 4), bounds.Height);
                }
            }

            var textBounds = new Rectangle (bounds.X + 2, bounds.Y, Math.Max (0, bounds.Width - 4), bounds.Height);
            WF.TextRenderer.DrawText (e.Graphics, e.SubItem.Text, e.SubItem.Font ?? list.Font, textBounds, fg,
                Align (e.Header?.TextAlign ?? WF.HorizontalAlignment.Left) | WF.TextFormatFlags.VerticalCenter | WF.TextFormatFlags.SingleLine | WF.TextFormatFlags.EndEllipsis | WF.TextFormatFlags.NoPrefix);
        }

        private static WF.TextFormatFlags Align (WF.HorizontalAlignment alignment) => alignment switch {
            WF.HorizontalAlignment.Right => WF.TextFormatFlags.Right,
            WF.HorizontalAlignment.Center => WF.TextFormatFlags.HorizontalCenter,
            _ => WF.TextFormatFlags.Left,
        };

        // ---- progress bars ----------------------------------------------------------------------

        // No selector maps here (the grammar has none for ProgressBar); the fill follows
        // --accent-color-2 and the track --control-mid-high-color, as the Majorsilence.Forms renderer
        // reads them. WinForms forwards ForeColor / BackColor to PBM_SETBARCOLOR / PBM_SETBKCOLOR
        // itself, on every handle creation, so the colours are ordinary pinned properties here --
        // sending the messages directly from HandleCreated is futile, ProgressBar.OnHandleCreated
        // runs after the event and re-sends its own. The bar only honours them with visual styles
        // switched off for its window, which is the one thing that needs the handle.
        private static void ApplyProgressBar (WF.ProgressBar bar, ApplyContext ctx)
        {
            // Without visual styles the Blocks style paints segmented chunks; Continuous is what the
            // flat theme look wants. Marquee is left alone. Changing Style recreates the handle, so it
            // must happen here and never inside the HandleCreated hook.
            if (bar.Style == WF.ProgressBarStyle.Blocks)
                bar.Style = WF.ProgressBarStyle.Continuous;

            SetForeColor (bar, TokenSnapshot.Flatten (ctx.Tokens.Accent2, ctx.Tokens.Background));
            SetBackColor (bar, TokenSnapshot.Flatten (ctx.Tokens.ControlMidHigh, ctx.Tokens.Background));

            var state = states.GetOrCreateValue (bar);
            if (!state.HandleHookInstalled) {
                state.HandleHookInstalled = true;
                bar.HandleCreated += static (sender, _) => {
                    if (sender is WF.ProgressBar p)
                        NativeMethods.DisableVisualStyles (p.Handle);
                };
            }

            if (bar.IsHandleCreated) {
                NativeMethods.DisableVisualStyles (bar.Handle);
                bar.Invalidate ();
            }
        }

        // ---- grids ---------------------------------------------------------------------------------

        private static void ApplyDataGridView (WF.DataGridView grid, ApplyContext ctx)
        {
            var bg = Resolve (ctx, "DataGridView", "background-color", ctx.Tokens.ControlLow);
            var fg = Resolve (ctx, "DataGridView", "color", ctx.Tokens.Foreground);
            var selectionBg = Resolve (ctx, "DataGridView", "background-color", ctx.Tokens.Accent, part: "selection");
            var selectionFg = Resolve (ctx, "DataGridView", "color", ctx.Tokens.ForegroundOnAccent, part: "selection");

            grid.BackgroundColor = bg;
            grid.GridColor = Resolve (ctx, "DataGridView", "border-color", ctx.Tokens.BorderLow);
            grid.EnableHeadersVisualStyles = false;

            grid.DefaultCellStyle.BackColor = bg;
            grid.DefaultCellStyle.ForeColor = fg;
            grid.DefaultCellStyle.SelectionBackColor = selectionBg;
            grid.DefaultCellStyle.SelectionForeColor = selectionFg;

            var gridFont = ctx.Rules.Font ("DataGridView");
            if (!gridFont.IsEmpty)
                grid.DefaultCellStyle.Font = BuildFont (grid.Font, gridFont);

            var headerBg = Resolve (ctx, "DataGridView", "background-color", ctx.Tokens.ControlLow, part: "header");
            var headerFg = Resolve (ctx, "DataGridView", "color", fg, part: "header");
            grid.ColumnHeadersDefaultCellStyle.BackColor = headerBg;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = headerFg;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBg;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = headerFg;

            var headerFont = ctx.Rules.Font ("DataGridView", "header");
            if (!headerFont.IsEmpty)
                grid.ColumnHeadersDefaultCellStyle.Font = BuildFont (grid.Font, headerFont);

            var rowHeaderBg = Resolve (ctx, "DataGridView", "background-color", headerBg, part: "row-header");
            var rowHeaderFg = Resolve (ctx, "DataGridView", "color", headerFg, part: "row-header");
            grid.RowHeadersDefaultCellStyle.BackColor = rowHeaderBg;
            grid.RowHeadersDefaultCellStyle.ForeColor = rowHeaderFg;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = rowHeaderBg;
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = rowHeaderFg;

            if (ctx.Rules.Color ("DataGridView", "alternating-row", false, "background-color") is { } alternating) {
                grid.AlternatingRowsDefaultCellStyle.BackColor = TokenSnapshot.Flatten (alternating, bg);
                grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = selectionBg;
                grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = selectionFg;
            }
        }

        private static void ApplyPropertyGrid (WF.PropertyGrid grid, ApplyContext ctx)
        {
            var bg = Resolve (ctx, "PropertyGrid", "background-color", ctx.Tokens.ControlLow);
            var fg = Resolve (ctx, "PropertyGrid", "color", ctx.Tokens.Foreground);

            SetBackColor (grid, bg);
            SetFont (grid, ctx.Rules.Font ("PropertyGrid"));
            grid.ViewBackColor = bg;
            grid.ViewForeColor = fg;
            grid.HelpBackColor = bg;
            grid.HelpForeColor = fg;
            grid.CategoryForeColor = fg;
            grid.LineColor = Resolve (ctx, "PropertyGrid", "border-color", ctx.Tokens.BorderLow);
        }

        // ---- strips ---------------------------------------------------------------------------------

        private static void ApplyStrip (WF.ToolStrip strip, string selector, ApplyContext ctx, Color background)
        {
            SetBackColor (strip, background);
            SetForeColor (strip, Resolve (ctx, selector, "color", ctx.Tokens.Foreground));
            SetFont (strip, ctx.Rules.Font (selector));
        }

        // ---- tabs -----------------------------------------------------------------------------------

        private static void ApplyTabControl (WF.TabControl tabControl, ApplyContext ctx)
        {
            var font = ctx.Rules.Font ("TabStrip");
            if (font.IsEmpty)
                font = ctx.Rules.Font ("TabControl");
            SetFont (tabControl, font);

            // TabControl { background-color } applies to the pages (the band is native-drawn); the
            // pages themselves also pick up any Panel rule when the walk reaches them.
            if (ctx.Rules.Color ("TabControl", null, false, "background-color") is { } pageBg)
                foreach (WF.TabPage page in tabControl.TabPages)
                    SetBackColor (page, TokenSnapshot.Flatten (pageBg, ctx.Tokens.Background));
            if (ctx.Rules.Color ("TabControl", null, false, "color") is { } pageFg)
                foreach (WF.TabPage page in tabControl.TabPages)
                    SetForeColor (page, TokenSnapshot.Flatten (pageFg, ctx.Tokens.Background));

            // Any colour on the header row needs owner draw (the matrix marks all of it approximate).
            if (!ctx.Rules.HasAny ("TabStrip"))
                return;

            var state = states.GetOrCreateValue (tabControl);
            if (!state.TabDrawHookInstalled) {
                state.TabDrawHookInstalled = true;
                tabControl.DrawMode = WF.TabDrawMode.OwnerDrawFixed;
                tabControl.DrawItem += static (sender, e) => {
                    if (sender is WF.TabControl tc && Current is { } current)
                        DrawTab (tc, e, current);
                };
            }

            tabControl.Invalidate ();
        }

        private static void DrawTab (WF.TabControl tabControl, WF.DrawItemEventArgs e, ApplyContext ctx)
        {
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count)
                return;

            var selected = e.Index == tabControl.SelectedIndex;
            var stripBg = Resolve (ctx, "TabStrip", "background-color", ctx.Tokens.Background);

            var bg = selected
                ? Resolve (ctx, "TabStrip", "background-color", ctx.Tokens.ControlLow, part: "selected")
                : ctx.Rules.Color ("TabStrip", "item", false, "background-color") is { } itemBg
                    ? TokenSnapshot.Flatten (itemBg, ctx.Tokens.Background)
                    : stripBg;
            var fg = selected
                ? Resolve (ctx, "TabStrip", "color", ctx.Tokens.Foreground, part: "selected")
                : Resolve (ctx, "TabStrip", "color", ctx.Tokens.Foreground, part: "item");

            var bounds = tabControl.GetTabRect (e.Index);

            using (var brush = new SolidBrush (bg))
                e.Graphics.FillRectangle (brush, bounds);

            if (selected) {
                var underlineColor = ctx.Rules.Color ("TabStrip", "selected", false, "border-bottom-color") is { } u
                    ? TokenSnapshot.Flatten (u, bg)
                    : ctx.Tokens.Accent2;
                var underlineWidth = ctx.Rules.Length ("TabStrip", "selected", false, "border-bottom-width") ?? 3;

                if (underlineWidth > 0)
                    using (var brush = new SolidBrush (underlineColor))
                        e.Graphics.FillRectangle (brush, bounds.X, bounds.Bottom - underlineWidth, bounds.Width, underlineWidth);
            }

            WF.TextRenderer.DrawText (e.Graphics, tabControl.TabPages[e.Index].Text, tabControl.Font, bounds, fg,
                WF.TextFormatFlags.HorizontalCenter | WF.TextFormatFlags.VerticalCenter | WF.TextFormatFlags.SingleLine);
        }
    }
}
