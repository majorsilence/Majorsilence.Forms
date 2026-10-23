using System.Collections.Generic;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    // Classes and ids in theme CSS (docs/theming-selectors-plan.md).
    //
    // A rule naming a type styles the type's default styles (Button.DefaultStyle, DefaultStyleHover, ...), which every instance reads
    // through to. That cannot express ".primary": the same type needs different values per control. So a control that some class or id
    // rule matches gets a *cascade layer* per state, slotted between each of its instance styles and the type-level style above it:
    //
    //   instance style (explicit values) -> cascade layer (matching rules) -> type-level style -> ...
    //
    // Each layer is filled by running every matching rule that applies in that state in ascending specificity (ties to the later
    // rule), so the cascade is resolved once, here, and renderers read the result through the chain as they always have. A control
    // nothing matches keeps the type-level chain as it was, and pays one static read in CurrentStyle (two when rules are in use).
    //
    // Layers are rebuilt lazily: Theme.ScopedRulesVersion moves on when a sheet, token or scope changes, and a control's classes or name
    // clear its own stamp; the next read of CurrentStyle rebuilds. Nothing keeps a registry of live controls.
    public partial class Control
    {
        private static readonly int s_styleClassesProperty = PropertyStore.CreateKey ();
        private static readonly int s_styleCascadeProperty = PropertyStore.CreateKey ();

        // The Theme.ScopedRulesVersion this control's layers were built for; 0 means they must be rebuilt.
        private int style_cascade_stamp;

        /// <summary>
        /// Gets the style classes of this control: the names a theme's <c>.class</c> selectors match (<c>Button.primary { ... }</c>).
        /// Changing them restyles the control. <see cref="CssClass"/> reads and writes the same set as a space-separated string.
        /// </summary>
        public ControlStyleClasses StyleClasses {
            get {
                if (Properties.GetObject (s_styleClassesProperty) is not ControlStyleClasses classes)
                    Properties.SetObject (s_styleClassesProperty, classes = new ControlStyleClasses (this));

                return classes;
            }
        }

        /// <summary>
        /// Gets or sets the style classes as one space-separated string, e.g. <c>"primary large"</c> -- the form a designer or a
        /// property grid edits. Setting it replaces the set; see <see cref="StyleClasses"/> for adding and removing one.
        /// </summary>
        /// <exception cref="ArgumentException">A name in the string is not a class name a selector can write.</exception>
        [System.ComponentModel.DefaultValue ("")]
        public string CssClass {
            get => Properties.GetObject (s_styleClassesProperty) is ControlStyleClasses classes ? classes.ToString () : string.Empty;
            set {
                if (value is { Length: > 0 } || Properties.GetObject (s_styleClassesProperty) is ControlStyleClasses { Count: > 0 })
                    StyleClasses.Replace (value);
            }
        }

        // The classes changed: restyle, and let a transition move from how the control looked.
        internal void StyleClassesChanged () => InvalidateStyleCascade (animate: true);

        // The control's classes or name changed, so the layers built for them are stale. With `animate`, a CSS transition on the style it
        // moves into runs from how the control looks now: the state style that was showing is replaced, as the transition's starting
        // point, by a copy that stays chained to the layer being discarded.
        private void InvalidateStyleCascade (bool animate)
        {
            if (!Theme.ScopedRulesInUse)
                return;

            if (animate && ControlStyle.TransitionsInUse && Properties.GetObject (s_styleTransitionProperty) is StyleTransitionState { Target: { } target } state)
                state.Target = target.SnapshotOwn ();

            style_cascade_stamp = 0;
            Invalidate ();
        }

        private void EnsureStyleCascade ()
        {
            style_cascade_stamp = Theme.ScopedRulesVersion;
            RebuildStyleCascade ();
        }

        // The instance styles that currently sit under a layer, and the parent each had before it was linked.
        private sealed class StyleCascade
        {
            public readonly List<(ControlStyle Style, ControlStyle BaseParent)> Linked = new (2);
        }

        // A class or id rule that reaches this control, or a type-level state rule that has to be ordered against them.
        private readonly struct CascadeEntry
        {
            public CascadeEntry (ThemeStyleSheet.ControlRule rule, int order)
            {
                Rule = rule;
                Order = order;
            }

            public ThemeStyleSheet.ControlRule Rule { get; }
            public int Order { get; }
        }

        // The rules the cascade of state `state` applies. A class or id rule applies from its own state on up -- ".primary" in a hovered
        // control as well as a resting one -- as in CSS. A type-level rule is only listed for its own state: the type's default
        // styles already carry it, and the type's built-in look for a state (a button's hover colour) is meant to show through
        // a "Button { ... }" rule exactly as it does for a control with no classes.
        private static bool AppliesInState (ThemeCssState rule, ThemeCssState state)
            => rule == state
               || (rule == ThemeCssState.Normal && state != ThemeCssState.Normal)
               || (rule == ThemeCssState.Focus && state == ThemeCssState.FocusVisible);

        private void RebuildStyleCascade ()
        {
            var cascade = Properties.GetObject (s_styleCascadeProperty) as StyleCascade;
            var entries = GatherCascadeEntries (out var nearest);

            if (entries is null && cascade is null)
                return;

            // New layers are always made, never the old ones refilled: a transition may still be reading the old look through them.
            if (cascade is not null) {
                foreach (var (style, baseParent) in cascade.Linked)
                    style.SetCascadeParent (baseParent);

                cascade.Linked.Clear ();
            }

            // Whatever animation the control's style resolves to may have changed.
            ControlStyle.BumpGeneration ();

            if (entries is null)
                return;

            if (cascade is null)
                Properties.SetObject (s_styleCascadeProperty, cascade = new StyleCascade ());

            // The layers' values are the control's scope's tokens, as everything painted for it is.
            using (ThemeScope.EnterFor (this)) {
                LinkCascadeLayer (cascade, entries, nearest, ThemeCssState.Normal);

                // Only a state the type has a look of its own for (Button, TrackBar, a text box's focus) has a state style chained to the
                // type; on any other control the state styles derive from Style, which carries the resting layer.
                LinkCascadeLayer (cascade, entries, nearest, ThemeCssState.Hover);
                LinkCascadeLayer (cascade, entries, nearest, ThemeCssState.Active);
                LinkCascadeLayer (cascade, entries, nearest, ThemeCssState.Disabled);

                if (UsesFocusStyle) {
                    LinkCascadeLayer (cascade, entries, nearest, ThemeCssState.Focus);
                    LinkCascadeLayer (cascade, entries, nearest, ThemeCssState.FocusVisible);
                }
            }
        }

        private void LinkCascadeLayer (StyleCascade cascade, List<CascadeEntry> entries, ThemeCssSelector? nearest, ThemeCssState state)
        {
            // Nothing to layer unless a class or id rule applies, and the state style is not even made before that is known.
            var any = false;

            foreach (var entry in entries)
                if (AppliesInCascade (entry.Rule, state, nearest) && entry.Rule.IsScoped) {
                    any = true;
                    break;
                }

            if (!any)
                return;

            var style = state switch {
                ThemeCssState.Hover => StyleHover,
                ThemeCssState.Active => StyleActive,
                ThemeCssState.Disabled => StyleDisabled,
                ThemeCssState.Focus => StyleFocus,
                ThemeCssState.FocusVisible => StyleFocusVisible,
                _ => Style
            };

            var parent = style._parent;

            // A state style chained to another of this control's own (a hover style derived from Style) already reads that one's layer.
            if (parent is null || ReferenceEquals (parent, Style) || ReferenceEquals (parent, StyleFocus))
                return;

            var layer = new ControlStyle (parent);

            foreach (var entry in entries)
                if (AppliesInCascade (entry.Rule, state, nearest))
                    entry.Rule.Apply (layer);

            style.SetCascadeParent (layer);
            cascade.Linked.Add ((style, parent));
        }

        private static bool AppliesInCascade (ThemeStyleSheet.ControlRule rule, ThemeCssState state, ThemeCssSelector? nearest)
        {
            if (!AppliesInState (rule.State, state))
                return false;

            // ".primary:hover" names no type, so it cannot be checked against one when the sheet is parsed: it applies to the controls whose
            // type has that state. (A control type's own state styles do not exist otherwise: see RebuildStyleCascade.)
            return rule.State == ThemeCssState.Normal || !rule.Selector.IsAny || (nearest?.Supports (rule.State) ?? false);
        }

        // Every class and id rule of the sheets in effect for this control that it matches, plus the type-level state rules of its type
        // (they have to be ordered against the others), in cascade order: ascending specificity, ties to the later rule. Null when no class
        // or id rule matches, which is every control nobody gave a class or a name a rule is written for.
        private List<CascadeEntry>? GatherCascadeEntries (out ThemeCssSelector? nearest)
        {
            nearest = null;

            var classes = Properties.GetObject (s_styleClassesProperty) as ControlStyleClasses;
            var name = Name;

            // A rule that names a class or an id needs this control to have one.
            if ((classes is null || classes.Count == 0) && name.Length == 0)
                return null;

            var sheets = ResolveThemeScope ()?.OwnRuleSheets ?? Theme.CurrentStyleSheets;
            List<CascadeEntry>? entries = null;
            var scoped = false;
            var order = 0;
            var resolved = false;
            ThemeCssSelector? type = null;

            foreach (var sheet in sheets) {
                if (!sheet.UsesClassesOrIds)
                    continue;

                if (!resolved) {
                    type = nearest = ThemeCssReference.NearestSelector (GetType ());
                    resolved = true;
                }

                foreach (var rule in sheet.ControlRules) {
                    order++;

                    if (rule.Part is not null)
                        continue;

                    if (!rule.Selector.IsAny && !ReferenceEquals (rule.Selector, type))
                        continue;

                    if (rule.IsScoped) {
                        if (rule.Model.Id is { } id && !string.Equals (id, name, StringComparison.Ordinal))
                            continue;

                        var all = true;
                        foreach (var required in rule.Model.Classes)
                            if (classes is null || !classes.Contains (required)) {
                                all = false;
                                break;
                            }

                        if (!all)
                            continue;

                        scoped = true;
                    } else if (rule.State == ThemeCssState.Normal) {
                        continue;
                    }

                    (entries ??= new List<CascadeEntry> ()).Add (new CascadeEntry (rule, order));
                }
            }

            if (!scoped)
                return null;

            entries!.Sort (static (a, b) => {
                var bySpecificity = a.Rule.Model.Specificity.CompareTo (b.Rule.Model.Specificity);
                return bySpecificity != 0 ? bySpecificity : a.Order.CompareTo (b.Order);
            });

            return entries;
        }
    }
}
