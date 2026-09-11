using System.Drawing;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>
    /// The strip colours one apply resolved: rule values where the sheet has them, theme tokens
    /// otherwise. Consumed by <see cref="ThemeColorTable"/>/<see cref="ThemeToolStripRenderer"/>,
    /// which repaint every MenuStrip, ToolStrip, StatusStrip and drop-down in the process via
    /// <see cref="WF.ToolStripManager.Renderer"/>.
    /// </summary>
    internal sealed class StripColors
    {
        public Color MenuBackground;
        public Color ToolBarBackground;
        public Color StatusBarBackground;
        public Color DropDownBackground;
        public Color DropDownBorder;
        public Color ItemHover;
        public Color ItemHoverText;
        public Color Pressed;
        public Color Text;
        public Color Separator;
        public Color DisabledText;

        public static StripColors Resolve (ThemeRuleSet rules, TokenSnapshot tokens)
        {
            var menuBg = rules.Color ("Menu", null, false, "background-color") ?? tokens.Background;
            var hover = rules.Color ("Menu", "item", true, "background-color")
                ?? rules.Color ("ToolBar", "item", true, "background-color")
                ?? rules.Color ("MenuDropDown", "item", true, "background-color")
                ?? tokens.HighlightLow;

            return new StripColors {
                MenuBackground = TokenSnapshot.Flatten (menuBg, tokens.Background),
                ToolBarBackground = TokenSnapshot.Flatten (rules.Color ("ToolBar", null, false, "background-color") ?? menuBg, tokens.Background),
                StatusBarBackground = TokenSnapshot.Flatten (rules.Color ("StatusBar", null, false, "background-color") ?? tokens.Background, tokens.Background),
                DropDownBackground = TokenSnapshot.Flatten (rules.Color ("MenuDropDown", null, false, "background-color") ?? tokens.ControlLow, tokens.Background),
                DropDownBorder = TokenSnapshot.Flatten (rules.Color ("MenuDropDown", null, false, "border-color") ?? tokens.BorderLow, tokens.Background),
                ItemHover = TokenSnapshot.Flatten (hover, tokens.Background),
                ItemHoverText = TokenSnapshot.Flatten (
                    rules.Color ("Menu", "item", true, "color")
                        ?? rules.Color ("ToolBar", "item", true, "color")
                        ?? rules.Color ("MenuDropDown", "item", true, "color")
                        ?? tokens.Foreground,
                    tokens.Background),
                Pressed = TokenSnapshot.Flatten (tokens.HighlightMid, tokens.Background),
                Text = TokenSnapshot.Flatten (
                    rules.Color ("Menu", null, false, "color")
                        ?? rules.Color ("MenuDropDown", null, false, "color")
                        ?? tokens.Foreground,
                    tokens.Background),
                Separator = TokenSnapshot.Flatten (tokens.BorderLow, tokens.Background),
                DisabledText = TokenSnapshot.Flatten (tokens.ForegroundDisabled, tokens.Background),
            };
        }
    }

    /// <summary>
    /// A <see cref="WF.ProfessionalColorTable"/> built from the theme, flattening every gradient to the
    /// theme's flat surfaces.
    /// </summary>
    internal sealed class ThemeColorTable : WF.ProfessionalColorTable
    {
        private readonly StripColors c;

        public ThemeColorTable (StripColors colors)
        {
            c = colors;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => c.MenuBackground;
        public override Color MenuStripGradientEnd => c.MenuBackground;
        public override Color ToolStripGradientBegin => c.ToolBarBackground;
        public override Color ToolStripGradientMiddle => c.ToolBarBackground;
        public override Color ToolStripGradientEnd => c.ToolBarBackground;
        public override Color StatusStripGradientBegin => c.StatusBarBackground;
        public override Color StatusStripGradientEnd => c.StatusBarBackground;
        public override Color ToolStripPanelGradientBegin => c.ToolBarBackground;
        public override Color ToolStripPanelGradientEnd => c.ToolBarBackground;
        public override Color ToolStripContentPanelGradientBegin => c.ToolBarBackground;
        public override Color ToolStripContentPanelGradientEnd => c.ToolBarBackground;
        public override Color ToolStripBorder => c.ToolBarBackground;
        public override Color ToolStripDropDownBackground => c.DropDownBackground;
        public override Color ImageMarginGradientBegin => c.DropDownBackground;
        public override Color ImageMarginGradientMiddle => c.DropDownBackground;
        public override Color ImageMarginGradientEnd => c.DropDownBackground;
        public override Color MenuBorder => c.DropDownBorder;
        public override Color MenuItemBorder => c.ItemHover;
        public override Color MenuItemSelected => c.ItemHover;
        public override Color MenuItemSelectedGradientBegin => c.ItemHover;
        public override Color MenuItemSelectedGradientEnd => c.ItemHover;
        public override Color MenuItemPressedGradientBegin => c.Pressed;
        public override Color MenuItemPressedGradientMiddle => c.Pressed;
        public override Color MenuItemPressedGradientEnd => c.Pressed;
        public override Color ButtonSelectedHighlight => c.ItemHover;
        public override Color ButtonSelectedHighlightBorder => c.ItemHover;
        public override Color ButtonSelectedGradientBegin => c.ItemHover;
        public override Color ButtonSelectedGradientMiddle => c.ItemHover;
        public override Color ButtonSelectedGradientEnd => c.ItemHover;
        public override Color ButtonSelectedBorder => c.ItemHover;
        public override Color ButtonPressedHighlight => c.Pressed;
        public override Color ButtonPressedHighlightBorder => c.Pressed;
        public override Color ButtonPressedGradientBegin => c.Pressed;
        public override Color ButtonPressedGradientMiddle => c.Pressed;
        public override Color ButtonPressedGradientEnd => c.Pressed;
        public override Color ButtonPressedBorder => c.Pressed;
        public override Color ButtonCheckedHighlight => c.Pressed;
        public override Color ButtonCheckedHighlightBorder => c.Pressed;
        public override Color ButtonCheckedGradientBegin => c.Pressed;
        public override Color ButtonCheckedGradientMiddle => c.Pressed;
        public override Color ButtonCheckedGradientEnd => c.Pressed;
        public override Color CheckBackground => c.ItemHover;
        public override Color CheckSelectedBackground => c.Pressed;
        public override Color CheckPressedBackground => c.Pressed;
        public override Color GripDark => c.Separator;
        public override Color GripLight => c.ToolBarBackground;
        public override Color SeparatorDark => c.Separator;
        public override Color SeparatorLight => c.ToolBarBackground;
        public override Color OverflowButtonGradientBegin => c.ToolBarBackground;
        public override Color OverflowButtonGradientMiddle => c.ToolBarBackground;
        public override Color OverflowButtonGradientEnd => c.ToolBarBackground;
        public override Color RaftingContainerGradientBegin => c.ToolBarBackground;
        public override Color RaftingContainerGradientEnd => c.ToolBarBackground;
    }

    /// <summary>
    /// The renderer <see cref="WinFormsCssTheme"/> installs on
    /// <see cref="WF.ToolStripManager.Renderer"/>. Beyond the colour table it pins item text colours,
    /// so drop-downs that never appear in a form's control tree (ContextMenuStrip and friends) still
    /// get themed text.
    /// </summary>
    internal sealed class ThemeToolStripRenderer : WF.ToolStripProfessionalRenderer
    {
        private readonly StripColors c;

        public ThemeToolStripRenderer (StripColors colors) : base (new ThemeColorTable (colors))
        {
            c = colors;
            RoundedEdges = false;
        }

        internal StripColors Colors => c;

        protected override void OnRenderItemText (WF.ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
                e.TextColor = c.DisabledText;
            else if (e.Item.Selected || e.Item.Pressed)
                e.TextColor = c.ItemHoverText;
            else
                e.TextColor = c.Text;

            base.OnRenderItemText (e);
        }

        protected override void OnRenderArrow (WF.ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item is { Enabled: false } ? c.DisabledText
                : e.Item is { Selected: true } || e.Item is { Pressed: true } ? c.ItemHoverText
                : c.Text;
            base.OnRenderArrow (e);
        }
    }
}
