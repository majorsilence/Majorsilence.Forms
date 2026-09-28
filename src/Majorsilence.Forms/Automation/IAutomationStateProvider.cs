using System.Collections.Generic;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// Implemented by a custom-painted control that wants to publish its own value and extra state to the
    /// automation tree (register item F19) -- the level a status widget is showing, for example, where
    /// <c>true</c>/<c>false</c>/a checked box's text is not the shape that fits.
    /// </summary>
    /// <remarks>
    /// Role and name already have a place: <see cref="Control.AccessibleRole"/> and
    /// <see cref="Control.AccessibleName"/> (upstream WinForms properties <see cref="AutomationProvider"/>
    /// already checks first, ahead of its own built-in-control inference) cover those for any control,
    /// custom-painted or not. Value and arbitrary extra state have no such existing home -- WinForms'
    /// MSAA-era accessibility model never had one -- so this interface is the new one, additive only:
    /// nothing here changes what an existing control (one that does not implement it) reports.
    /// </remarks>
    public interface IAutomationStateProvider
    {
        /// <summary>
        /// The control's value for <see cref="AutomationElement.Value"/>, or <c>null</c> if it has none.
        /// Read instead of <see cref="AutomationProvider"/>'s own built-in-control inference (Value 100%
        /// replaces the type-switch, not blends with it) whenever the control implements this interface.
        /// </summary>
        string? AutomationValue { get; }

        /// <summary>
        /// Extra state beyond role/name/value, as key/value pairs -- a status widget's level, for example.
        /// Each entry becomes a <c>state-{key}</c> attribute in the automation XML page source (see
        /// <see cref="AutomationElement.State"/>) and is readable through WebDriver's <c>getAttribute</c>
        /// under that same name, so a locator captured from either sees the same thing. Never null; an
        /// empty dictionary if there is nothing extra to report. Keys should be simple identifiers (letters,
        /// digits, <c>-</c>/<c>_</c>) -- an odd key is sanitized for the XML attribute *name* the same way
        /// <see cref="AutomationElement.ControlType"/> already is for the element's own tag, but
        /// <c>getAttribute</c> looks a key up in this dictionary directly, unsanitized, so the two would
        /// disagree for a key that needed it.
        /// </summary>
        IReadOnlyDictionary<string, string> AutomationState { get; }
    }
}
