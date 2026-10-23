using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    // :hover, :active, :disabled, :focus and :focus-visible on real WinForms controls. WinForms has no per-state styling, so
    // the state a control is in is worked out from its own events and the properties that look different in that state are
    // set (and set back on the way out) -- BackColor, ForeColor, a button's border colour, a link's colours, a repainted
    // text-box frame. It is not animated and a native renderer may draw a disabled control its own way, which the support
    // matrix says row by row. Nothing here touches a control the sheet does not mention a state for, except a Button, whose
    // hover and disabled looks the core defines without a rule (accent fill, text on accent, --foreground-disabled-color).
    internal static partial class WinFormsThemeApplier
    {
        private sealed class Tracking
        {
            public bool Hover { get; set; }
            public bool Pressed { get; set; }
            public bool Installed { get; set; }
        }

        private static readonly ConditionalWeakTable<WF.Control, Tracking> tracking = new ();

        private static readonly ThemeCssState[] focus_chain = { ThemeCssState.Focus };
        private static readonly ThemeCssState[] focus_visible_chain = { ThemeCssState.Focus, ThemeCssState.FocusVisible };
        private static readonly ThemeCssState[] no_states = Array.Empty<ThemeCssState> ();

        // The rule selector a control type's states are written against, or null for a control with none.
        private static string? StateSelectorOf (WF.Control control) => control switch {
            WF.Button => "Button",
            WF.LinkLabel => "LinkLabel",
            WF.TextBoxBase => "TextBox",
            WF.TrackBar => "TrackBar",
            _ => null
        };

        // The states layered onto the resting look for the state a control is in: :focus-visible is :focus plus its own rules.
        private static ThemeCssState[] Chain (ThemeCssState state) => state switch {
            ThemeCssState.Normal => no_states,
            ThemeCssState.FocusVisible => focus_visible_chain,
            ThemeCssState.Focus => focus_chain,
            _ => new[] { state }
        };

        private static bool HasStateRules (ApplyContext ctx, string selector)
            => ctx.Rules.HasState (selector, ThemeCssState.Hover) || ctx.Rules.HasState (selector, ThemeCssState.Active)
               || ctx.Rules.HasState (selector, ThemeCssState.Disabled) || ctx.Rules.HasState (selector, ThemeCssState.Focus)
               || ctx.Rules.HasState (selector, ThemeCssState.FocusVisible);

        // Installs the event hooks the first time a control needs them and brings its look in line with its current state.
        private static void ApplyStates (WF.Control control, ApplyContext ctx)
        {
            if (StateSelectorOf (control) is not { } selector)
                return;

            var track = tracking.GetOrCreateValue (control);

            if (!track.Installed) {
                // A button has state looks even without a rule; every other control only when the sheet has a state rule or a
                // transition or animation for it.
                if (control is not WF.Button && !HasStateRules (ctx, selector) && !ctx.Rules.HasMotion (selector))
                    return;

                track.Installed = true;
                control.MouseEnter += static (sender, _) => OnState (sender, t => t.Hover = true);
                control.MouseLeave += static (sender, _) => OnState (sender, t => t.Hover = false);
                control.MouseDown += static (sender, _) => OnState (sender, t => t.Pressed = true);
                control.MouseUp += static (sender, _) => OnState (sender, t => t.Pressed = false);
                control.EnabledChanged += static (sender, _) => OnState (sender, null);
                control.GotFocus += static (sender, _) => OnState (sender, null);
                control.LostFocus += static (sender, _) => OnState (sender, null);
                control.KeyUp += static (sender, _) => OnState (sender, null);
            }

            RefreshState (control, ctx);
        }

        private static void OnState (object? sender, Action<Tracking>? update)
        {
            if (sender is not WF.Control control || ContextFor (control) is not { } ctx || !tracking.TryGetValue (control, out var track))
                return;

            update?.Invoke (track);
            RefreshState (control, ctx);
        }

        // The state a control is in, by CSS's rules: disabled, then pressed, then hovered, then focused -- limited to the states the
        // selector has (a text box has :focus only), so a hovered text box is still a focused one.
        private static ThemeCssState StateOf (WF.Control control, string selector, Tracking track)
        {
            var info = ThemeCssReference.FindSelector (selector);

            if (!control.Enabled && info?.SupportsDisabled == true)
                return ThemeCssState.Disabled;
            if (track.Pressed && info?.SupportsActive == true)
                return ThemeCssState.Active;
            if (track.Hover && info?.SupportsHover == true)
                return ThemeCssState.Hover;
            if (control.Focused && info?.SupportsFocus == true)
                return control is WF.TextBoxBase || ShowsFocusCues (control) ? ThemeCssState.FocusVisible : ThemeCssState.Focus;

            return ThemeCssState.Normal;
        }

        private static void RefreshState (WF.Control control, ApplyContext ctx)
        {
            if (StateSelectorOf (control) is not { } selector || !tracking.TryGetValue (control, out var track))
                return;

            var state = ResolvedState (control, ctx, selector, track);

            SyncAnimation (control, ctx, selector, state);
            RefreshLook (control, ctx, state);
            ApplyEffects (control, ctx);
        }

        private static ThemeCssState ResolvedState (WF.Control control, ApplyContext ctx, string selector, Tracking track)
        {
            var state = StateOf (control, selector, track);

            // Without an :active rule a pressed button keeps its hovered look (darkened by FlatAppearance.MouseDownBackColor).
            return state == ThemeCssState.Active && !ctx.Rules.HasState (selector, ThemeCssState.Active) ? ThemeCssState.Hover : state;
        }

        // The colours of a control in a state: what a state change and every animation frame re-evaluates.
        private static void RefreshLook (WF.Control control, ApplyContext ctx, ThemeCssState state)
        {
            switch (control) {
                case WF.Button button:
                    RefreshButton (button, ctx, state);
                    break;
                case WF.LinkLabel link:
                    RefreshLink (link, ctx, state);
                    break;
                case WF.TextBoxBase text:
                    RefreshText (text, ctx, state);
                    break;
                case WF.TrackBar trackBar:
                    RefreshTrack (trackBar, ctx, state);
                    break;
            }
        }

        private static Color Flat (ApplyContext ctx, Color color) => TokenSnapshot.Flatten (color, ctx.Tokens.Background);

        // ---- buttons -----------------------------------------------------------------------------------

        private static void RefreshButton (WF.Button button, ApplyContext ctx, ThemeCssState state)
        {
            var back = Resolve (ctx, "Button", "background-color", ctx.Tokens.ControlMid);
            var fore = Resolve (ctx, "Button", "color", ctx.Tokens.Foreground);
            var border = Resolve (ctx, "Button", "border-color", ctx.Tokens.BorderLow);

            foreach (var s in Chain (state)) {
                // What the core's states look like without a rule: a hovered button is the accent with text on the accent and the
                // second accent as its border; a disabled one has --foreground-disabled-color text.
                if (s == ThemeCssState.Hover) {
                    back = Resolve (ctx, "Button", "background-color", ctx.Tokens.Accent, hover: true);
                    fore = ctx.Tokens.ForegroundOnAccent;
                    border = ctx.Tokens.Accent2;
                } else if (s == ThemeCssState.Disabled) {
                    fore = ctx.Tokens.ForegroundDisabled;
                }

                if (ctx.Rules.Color ("Button", null, s, "background-color") is { } b)
                    back = Flat (ctx, b);
                if (ctx.Rules.Color ("Button", null, s, "color") is { } f)
                    fore = Flat (ctx, f);
                if (ctx.Rules.Color ("Button", null, s, "border-color") is { } c)
                    border = Flat (ctx, c);
            }

            SetBackColor (button, Animated (button, ctx, "Button", state, "BackColor", "background-color", back));
            SetForeColor (button, Animated (button, ctx, "Button", state, "ForeColor", "color", fore));

            var width = ctx.Rules.Length ("Button", null, false, "border-width") ?? 1;
            if (width > 0)
                button.FlatAppearance.BorderColor = Animated (button, ctx, "Button", state, "BorderColor", "border-color", border);

            // The native hover and pressed fills: flat buttons paint these themselves.
            var hover = Resolve (ctx, "Button", "background-color", ctx.Tokens.Accent, hover: true);
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = ctx.Rules.Color ("Button", null, ThemeCssState.Active, "background-color") is { } pressed
                ? Flat (ctx, pressed)
                : TokenSnapshot.Darken (hover, 0.12);
        }

        // ---- link labels -------------------------------------------------------------------------------

        private static void RefreshLink (WF.LinkLabel link, ApplyContext ctx, ThemeCssState state)
        {
            var normal = Resolve (ctx, "LinkLabel", "color", ctx.Tokens.Accent);
            var color = normal;
            Color? back = ctx.Rules.Color ("LinkLabel", null, false, "background-color") is { } bg ? Flat (ctx, bg) : null;

            foreach (var s in Chain (state)) {
                // :active and :disabled have their own seams (ActiveLinkColor, DisabledLinkColor); the rest swap LinkColor.
                if (s is not (ThemeCssState.Active or ThemeCssState.Disabled) && ctx.Rules.Color ("LinkLabel", null, s, "color") is { } c)
                    color = Flat (ctx, c);
                if (ctx.Rules.Color ("LinkLabel", null, s, "background-color") is { } b)
                    back = Flat (ctx, b);
            }

            color = Animated (link, ctx, "LinkLabel", state, "LinkColor", "color", color);
            link.LinkColor = color;
            link.VisitedLinkColor = color;
            link.ActiveLinkColor = ctx.Rules.Color ("LinkLabel", null, ThemeCssState.Active, "color") is { } active
                ? Flat (ctx, active)
                : TokenSnapshot.Darken (normal, 0.2);
            link.DisabledLinkColor = ctx.Rules.Color ("LinkLabel", null, ThemeCssState.Disabled, "color") is { } disabled
                ? Flat (ctx, disabled)
                : ctx.Tokens.ForegroundDisabled;

            if (back is { } fill)
                SetBackColor (link, Animated (link, ctx, "LinkLabel", state, "BackColor", "background-color", fill));
            else if (ctx.Rules.HasState ("LinkLabel", ThemeCssState.Hover) || HasStateRules (ctx, "LinkLabel"))
                ResetToAmbientBack (link);
        }

        // Puts a control back to the colour it would inherit when no rule gives it one of its own (after a state's fill went away).
        private static void ResetToAmbientBack (WF.Control control)
        {
            if (control.Parent is { } parent)
                SetBackColor (control, parent.BackColor);
        }

        // ---- text boxes --------------------------------------------------------------------------------

        private static void RefreshText (WF.TextBoxBase text, ApplyContext ctx, ThemeCssState state)
        {
            var back = Resolve (ctx, "TextBox", "background-color", ctx.Tokens.ControlLow);
            var fore = Resolve (ctx, "TextBox", "color", ctx.Tokens.Foreground);
            Color? border = null;

            foreach (var s in Chain (state)) {
                if (ctx.Rules.Color ("TextBox", null, s, "background-color") is { } b)
                    back = Flat (ctx, b);
                if (ctx.Rules.Color ("TextBox", null, s, "color") is { } f)
                    fore = Flat (ctx, f);
                if (ctx.Rules.Color ("TextBox", null, s, "border-color") is { } c)
                    border = Flat (ctx, c);
            }

            SetBackColor (text, Animated (text, ctx, "TextBox", state, "BackColor", "background-color", back));
            SetForeColor (text, Animated (text, ctx, "TextBox", state, "ForeColor", "color", fore));

            if (states.TryGetValue (text, out var controlState) && controlState.Border is { } painter && controlState.BorderBase is { } baseColor) {
                var wanted = Animated (text, ctx, "TextBox", state, "BorderColor", "border-color", border ?? baseColor);
                if (painter.Color != wanted) {
                    painter.Color = wanted;
                    painter.Refresh ();
                }
            }
        }

        // ---- track bars --------------------------------------------------------------------------------

        private static void RefreshTrack (WF.TrackBar trackBar, ApplyContext ctx, ThemeCssState state)
        {
            Color? back = ctx.Rules.Color ("TrackBar", null, false, "background-color") is { } bg ? Flat (ctx, bg) : null;

            foreach (var s in Chain (state))
                if (ctx.Rules.Color ("TrackBar", null, s, "background-color") is { } b)
                    back = Flat (ctx, b);

            if (back is { } fill)
                SetBackColor (trackBar, Animated (trackBar, ctx, "TrackBar", state, "BackColor", "background-color", fill));
            else
                ResetToAmbientBack (trackBar);
        }

        // ---- focus cues --------------------------------------------------------------------------------

        private const int WM_QUERYUISTATE = 0x0129;
        private const int UISF_HIDEFOCUS = 0x1;

        [DllImport ("user32.dll")]
        private static extern IntPtr SendMessage (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        // Whether the window shows focus cues: WinForms hides them until the keyboard is used (Tab), the state a browser's
        // :focus-visible heuristic stands for. Unknown (no window yet) counts as hidden.
        private static bool ShowsFocusCues (WF.Control control)
        {
            var form = control.FindForm ();
            if (form is null || !form.IsHandleCreated)
                return false;

            return ((long) SendMessage (form.Handle, WM_QUERYUISTATE, IntPtr.Zero, IntPtr.Zero) & UISF_HIDEFOCUS) == 0;
        }
    }
}
