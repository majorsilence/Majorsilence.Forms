using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Defines the style of a control.
    /// </summary>
    public class ControlStyle
    {
        internal readonly ControlStyle? _parent;

        // The defaults a type-level style was declared with, kept so they can be re-run after a CSS
        // stylesheet clears the style (see ResetWithStyleSheetRules).
        private readonly Action<ControlStyle>? _setDefaults;

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the static DefaultStyle property.
        /// </summary>
        public ControlStyle (ControlStyle? parent, Action<ControlStyle> setDefaults)
        {
            _parent = parent;
            _setDefaults = setDefaults;

            border = new ControlBorderStyle (parent?.border) { Owner = this };

            // A type's static styles are created lazily, possibly while a ThemeScope is current; their
            // values are the application theme's regardless.
            using (ThemeScope.Push (null))
                setDefaults (this);

            Theme.ThemeChanged += (o, e) => ApplyDefaults ();
        }

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the instance Style property.
        /// </summary>
        public ControlStyle (ControlStyle parent)
        {
            _parent = parent;

            border = new ControlBorderStyle (parent?.border);
        }

        // A style outside any chain and not type-level: Clone and the DataGridViewCellStyle conversion.
        private ControlStyle ()
        {
            border = new ControlBorderStyle (null);
        }

        // A ThemeScope's stand-in for a type-level style; see ScopedTwin.
        private ControlStyle (bool scopedTwin)
        {
            is_twin = scopedTwin;
            border = new ControlBorderStyle (null);
        }

        // --- ThemeScope ----------------------------------------------------------------------------
        //
        // A type-level style (one with declared defaults, e.g. Button.DefaultStyle) holds the application
        // theme's values. While a ThemeScope is current, reading its theme-derived values (colours, font,
        // border, shadow) reads the scope's twin of it instead: a value bag built by running the same
        // defaults with the scope current, then the scope's CSS rules. Every chain still walks the original
        // styles, so renderers that read Button.DefaultStyle directly, and instance styles whose parent is
        // it, follow the scope with no changes. Setting a value always sets this style's own.

        private readonly bool is_twin;

        internal ControlStyle? ScopedTwin
            => _setDefaults is not null && !is_twin && ThemeScope.Current is { } scope ? scope.TwinOf (this) : null;

        internal static ControlStyle CreateScopedTwin (ControlStyle original) => new ControlStyle (scopedTwin: true);

        // Rebuilds this twin of `original` (with its scope current): the original's own values first when
        // `copyOriginal` (values an application set on the type-level style directly carry over), then the
        // declared defaults, which now read the scope's tokens, then `rules`.
        internal void RebuildAsTwin (ControlStyle original, Action<ControlStyle>? rules, bool copyOriginal)
        {
            background_color = copyOriginal ? original.background_color : null;
            foreground_color = copyOriginal ? original.foreground_color : null;
            font = copyOriginal ? original.font : null;
            font_size = copyOriginal ? original.font_size : null;
            box_shadow = copyOriginal ? original.box_shadow : null;
            transition = copyOriginal ? original.transition : null;
            border.CopyValuesFrom (copyOriginal ? original.border : null);

            original._setDefaults?.Invoke (this);
            rules?.Invoke (this);
        }

        /// <summary>
        /// The declarations a CSS theme (<see cref="ThemeStyleSheet"/>) assigned to this type-level
        /// style, or null when no stylesheet targets it. Re-run after the declared defaults on every
        /// theme change so a `Button { background-color: ... }` rule keeps winning over the
        /// theme-derived default the type declares -- the defaults callback would otherwise overwrite
        /// it the next time any Theme property changed.
        /// </summary>
        internal Action<ControlStyle>? StyleSheetRules { get; private set; }

        // Runs the declared defaults and then the stylesheet layer on top, which is the order that makes
        // "CSS overrides the type's defaults" hold both at apply time and after later theme changes.
        private void ApplyDefaults ()
        {
            // ThemeChanged can be raised from code running under a scope (an event handler); the type's
            // own values are the application theme's.
            using (ThemeScope.Push (null)) {
                _setDefaults?.Invoke (this);
                StyleSheetRules?.Invoke (this);
            }
        }

        /// <summary>
        /// Replaces the stylesheet layer on a type-level style and rebuilds it from scratch: every
        /// value is cleared, the declared defaults re-run, then <paramref name="rules"/> (if any).
        /// Clearing first is what lets a stylesheet that no longer mentions a property return the
        /// style to its default instead of leaving the previous sheet's value behind.
        /// </summary>
        internal void ResetWithStyleSheetRules (Action<ControlStyle>? rules)
        {
            StyleSheetRules = rules;

            background_color = null;
            foreground_color = null;
            font = null;
            font_size = null;
            box_shadow = null;
            transition = null;
            border.ClearValues ();

            ApplyDefaults ();
        }

        /// <summary>
        /// Gets or sets how cell content is aligned when this style is used as a grid cell style.
        /// Stored for WinForms DataGridViewCellStyle compatibility; grid renderers currently apply
        /// their own per-column alignment.
        /// </summary>
        public DataGridViewContentAlignment Alignment { get; set; } = DataGridViewContentAlignment.NotSet;

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public SKColor? BackgroundColor { get => (ScopedTwin ?? this).background_color; set => background_color = value; }

        private SKColor? background_color;

        /// <summary>
        /// Provides access to border style properties.
        /// </summary>
        public ControlBorderStyle Border => (ScopedTwin ?? this).border;

        private readonly ControlBorderStyle border;

        /// <summary>
        /// Gets or sets a hard, offset, no-blur shadow painted behind the control (see
        /// <see cref="ControlBoxShadow"/>), or null for none. Set by the CSS <c>box-shadow</c> property
        /// (#285); unlike <see cref="BackgroundColor"/> there is no theme default, so a style chain
        /// that never sets one paints no shadow at all.
        /// </summary>
        public ControlBoxShadow? BoxShadow { get => (ScopedTwin ?? this).box_shadow; set => box_shadow = value; }

        private ControlBoxShadow? box_shadow;

        /// <summary>
        /// Gets the shadow this style chain resolves to: this style's own <see cref="BoxShadow"/> if
        /// set, otherwise the nearest ancestor's, otherwise null.
        /// </summary>
        public ControlBoxShadow? GetBoxShadow () => BoxShadow ?? _parent?.GetBoxShadow ();

        /// <summary>
        /// Gets or sets how a control animates into this style when it enters it -- the CSS <c>transition</c> property (see
        /// <see cref="ControlTransition"/>) -- or null to inherit the parent style's. Each of its lists left null is inherited separately,
        /// so a <c>Button:active</c> rule that sets only a duration keeps the properties a <c>Button</c> rule chose.
        /// </summary>
        public ControlTransition? Transition {
            get => (ScopedTwin ?? this).transition;
            set {
                transition = value;
                if (value is not null)
                    TransitionsInUse = true;
            }
        }

        // Set once any style anywhere gets a transition; until then Control.CurrentStyle tracks no state changes at all.
        internal static bool TransitionsInUse { get; private set; }

        private ControlTransition? transition;

        /// <summary>
        /// Gets the transition this style chain resolves to, each list from the nearest style that sets it, or null when no style in
        /// the chain sets any.
        /// </summary>
        public ControlTransition? GetTransition ()
        {
            var parent = _parent?.GetTransition ();
            return Transition is { } own ? own.Inherit (parent) : parent;
        }

        /// <summary>
        /// Gets or sets the font.
        /// </summary>
        public SKTypeface? Font { get => (ScopedTwin ?? this).font; set => font = value; }

        private SKTypeface? font;

        /// <summary>
        /// Gets or sets the font size.
        /// </summary>
        public int? FontSize { get => (ScopedTwin ?? this).font_size; set => font_size = value; }

        private int? font_size;

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        public SKColor? ForegroundColor { get => (ScopedTwin ?? this).foreground_color; set => foreground_color = value; }

        private SKColor? foreground_color;

        /// <summary>
        /// Gets the computed background color.
        /// </summary>
        public SKColor GetBackgroundColor () => BackgroundColor ?? _parent?.GetBackgroundColor () ?? Theme.ControlMidColor;

        /// <summary>
        /// Gets the background color defined anywhere in this style chain (instance style up through
        /// the type's default styles), or null when the chain never sets one. The root
        /// <see cref="Control.DefaultStyle"/> is excluded: its theme color is the definition of
        /// "nothing set", so a null result lets the control resolve its WinForms-style ambient
        /// background from its parent control instead.
        /// </summary>
        internal SKColor? TryGetBackgroundColor ()
            => ReferenceEquals (this, Control.DefaultStyle)
                ? null
                : BackgroundColor ?? _parent?.TryGetBackgroundColor ();

        /// <summary>
        /// Gets the foreground color defined anywhere in this style chain, or null when the chain
        /// never sets one (the root <see cref="Control.DefaultStyle"/> is excluded, as in
        /// <see cref="TryGetBackgroundColor"/>) so the control can resolve its WinForms-style ambient
        /// foreground from its parent control instead.
        /// </summary>
        internal SKColor? TryGetForegroundColor ()
            => ReferenceEquals (this, Control.DefaultStyle)
                ? null
                : ForegroundColor ?? _parent?.TryGetForegroundColor ();

        /// <summary>
        /// Gets the font defined anywhere in this style chain, or null when the chain never sets one
        /// (the root <see cref="Control.DefaultStyle"/> is excluded, as in
        /// <see cref="TryGetBackgroundColor"/>) so the control can resolve its WinForms-style ambient
        /// font from its parent control instead.
        /// </summary>
        internal SKTypeface? TryGetFont ()
            => ReferenceEquals (this, Control.DefaultStyle) ? null : Font ?? _parent?.TryGetFont ();

        /// <summary>Companion to <see cref="TryGetFont"/> for the font size.</summary>
        internal int? TryGetFontSize ()
            => ReferenceEquals (this, Control.DefaultStyle) ? null : FontSize ?? _parent?.TryGetFontSize ();

        /// <summary>
        /// Gets the computed font.
        /// </summary>
        public SKTypeface GetFont () => Font ?? _parent?.GetFont () ?? Theme.UIFont;

        /// <summary>
        /// Gets the computed font size.
        /// </summary>
        public int GetFontSize () => FontSize ?? _parent?.GetFontSize () ?? Theme.FontSize;

        /// <summary>
        /// Gets the computed foreground color.
        /// </summary>
        public SKColor GetForegroundColor () => ForegroundColor ?? _parent?.GetForegroundColor () ?? Theme.ForegroundColor;

        // WinForms compatibility (DataGridViewCellStyle surface): System.Drawing.Color accessors.
        // BackColor/ForeColor bridge to the underlying SkiaSharp colors; the Selection* colors are
        // stored for compatibility (Majorsilence.Forms paints selection via the theme).

        /// <summary>Gets or sets the background color as a <see cref="System.Drawing.Color"/>. WinForms compatibility.</summary>
        public System.Drawing.Color BackColor {
            get => BackgroundColor is { } c ? System.Drawing.Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue) : System.Drawing.Color.Empty;
            set => BackgroundColor = value.IsEmpty ? null : value.ToSKColor ();
        }

        /// <summary>Gets or sets the foreground color as a <see cref="System.Drawing.Color"/>. WinForms compatibility.</summary>
        public System.Drawing.Color ForeColor {
            get => ForegroundColor is { } c ? System.Drawing.Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue) : System.Drawing.Color.Empty;
            set => ForegroundColor = value.IsEmpty ? null : value.ToSKColor ();
        }

        /// <summary>Gets or sets the selection background color. WinForms compatibility stub (stored, not rendered).</summary>
        public System.Drawing.Color SelectionBackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the selection foreground color. WinForms compatibility stub (stored, not rendered).</summary>
        public System.Drawing.Color SelectionForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>
        /// Projects this style onto a <see cref="DataGridViewCellStyle"/>, used when the grid builds a
        /// cell's inherited style (the WinForms style cascade is expressed in DataGridViewCellStyle
        /// terms, while the grid stores its own default styles as ControlStyle). Only the members the
        /// two types share are copied; the Skia typeface is not converted back to a
        /// <see cref="Majorsilence.Forms.Drawing.Font"/>.
        /// </summary>
        internal DataGridViewCellStyle ToDataGridViewCellStyle () => new DataGridViewCellStyle {
            BackColor = BackColor,
            ForeColor = ForeColor,
            SelectionBackColor = SelectionBackColor,
            SelectionForeColor = SelectionForeColor,
            Alignment = Alignment,
            WrapMode = WrapMode,
            Padding = Padding,
        };

        /// <summary>
        /// Gets or sets the padding around content when this style is used as a grid cell style.
        /// </summary>
        /// <remarks>Carried across both conversions alongside <see cref="Alignment"/> and
        /// <see cref="WrapMode"/>. Grid cells read and restore this while they are sited, so it has to
        /// round-trip rather than merely exist.</remarks>
        public Padding Padding { get; set; } = Padding.Empty;

        /// <summary>
        /// Returns a copy of this style.
        /// </summary>
        /// <remarks>
        /// Cells clone a style before mutating it so the change does not leak into the style they inherited
        /// from. The copy is detached from the parent chain deliberately: it carries the values resolved at
        /// the time of the call, which is what a caller mutating a clone expects.
        /// </remarks>
        public ControlStyle Clone () => new ControlStyle () {
            BackColor = BackColor,
            ForeColor = ForeColor,
            SelectionBackColor = SelectionBackColor,
            SelectionForeColor = SelectionForeColor,
            Alignment = Alignment,
            WrapMode = WrapMode,
            Padding = Padding,
            Font = Font,
            FontSize = FontSize,
            BackgroundColor = BackgroundColor,
            ForegroundColor = ForegroundColor,
            Transition = Transition,
        };

        /// <summary>
        /// Gets or sets how text wraps when this style is used as a grid cell style.
        /// </summary>
        /// <remarks>Stored for <see cref="DataGridViewCellStyle"/> compatibility and carried across both
        /// conversions, the same as <see cref="Alignment"/>; the grid renderers apply their own wrapping.</remarks>
        public DataGridViewTriState WrapMode { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>
        /// Converts a ControlStyle to a <see cref="DataGridViewCellStyle"/>, so WinForms code that reads a
        /// grid's cell-style property back into a DataGridViewCellStyle variable compiles.
        /// </summary>
        /// <remarks>
        /// The companion to the conversion below, which has existed for a while: assigning a
        /// DataGridViewCellStyle to one of the grid's ControlStyle-typed properties worked, and reading it
        /// back did not — so a derived grid could not re-expose <c>DefaultCellStyle</c> with the WinForms
        /// type, which is the first thing a themed grid does. Note it produces a copy, as the reverse
        /// direction always has: mutating the result changes the copy, so assign it back to have it stick.
        /// </remarks>
        public static implicit operator DataGridViewCellStyle (ControlStyle style)
            => style is null ? new DataGridViewCellStyle () : style.ToDataGridViewCellStyle ();

        /// <summary>
        /// Converts a DataGridViewCellStyle to a ControlStyle, so WinForms-style designer code
        /// (`grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = ...,
        /// Font = ..., ... };`) can assign directly to a ControlStyle-typed property.
        /// </summary>
        public static implicit operator ControlStyle (DataGridViewCellStyle style)
        {
            var result = new ControlStyle () {
                BackColor = style.BackColor,
                ForeColor = style.ForeColor,
                SelectionBackColor = style.SelectionBackColor,
                SelectionForeColor = style.SelectionForeColor,
                Alignment = style.Alignment,
                WrapMode = style.WrapMode,
                Padding = style.Padding,
            };
            if (style.Font is { } font) {
                result.Font = font.GetSKTypeface ();

                // Pixels, not points -- see the note on Control.Font's setter.
                result.FontSize = (int)System.Math.Round (font.PixelSize);
            }
            return result;
        }
    }
}
