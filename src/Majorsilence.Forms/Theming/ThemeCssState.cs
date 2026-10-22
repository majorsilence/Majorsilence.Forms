namespace Majorsilence.Forms
{
    /// <summary>
    /// The state a control rule styles: the resting look or one of the pseudo-classes the grammar accepts.
    /// A rule carries at most one (the grammar takes one pseudo-class per selector), so host appliers key
    /// their support matrices on this instead of a pile of booleans.
    /// </summary>
    public enum ThemeCssState
    {
        /// <summary>No pseudo-class: the control at rest.</summary>
        Normal,

        /// <summary><c>:hover</c> -- the pointer is over the control (or, on a part, over that piece).</summary>
        Hover,

        /// <summary><c>:active</c> -- the mouse is held down on the control.</summary>
        Active,

        /// <summary><c>:disabled</c> -- <c>Control.Enabled</c> is false.</summary>
        Disabled,

        /// <summary><c>:focus</c> -- the control has the keyboard focus.</summary>
        Focus,

        /// <summary><c>:focus-visible</c> -- focused with the window's focus cues showing (keyboard focus).</summary>
        FocusVisible
    }

    public sealed partial class ThemeCssSelector
    {
        /// <summary>
        /// Whether <c>Name:state</c> is meaningful for this control. <see cref="ThemeCssState.Normal"/> always is.
        /// </summary>
        /// <param name="state">The state to ask about.</param>
        public bool Supports (ThemeCssState state) => state switch {
            ThemeCssState.Hover => SupportsHover,
            ThemeCssState.Active => SupportsActive,
            ThemeCssState.Disabled => SupportsDisabled,
            ThemeCssState.Focus => SupportsFocus,
            ThemeCssState.FocusVisible => SupportsFocusVisible,
            _ => true
        };
    }
}
