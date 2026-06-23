using System;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// A locator strategy for finding an <see cref="AutomationElement"/>. Mirrors the familiar
    /// Selenium <c>By</c> shape so the in-process API and the WebDriver server share one vocabulary.
    /// </summary>
    public sealed class By
    {
        private readonly Func<AutomationElement, bool> _predicate;

        private By (string description, Func<AutomationElement, bool> predicate)
        {
            Description = description;
            _predicate = predicate;
        }

        /// <summary>A human-readable description of this locator (used in error messages).</summary>
        public string Description { get; }

        /// <summary>Returns whether the element matches this locator.</summary>
        public bool Matches (AutomationElement element) => _predicate (element);

        /// <summary>Matches by automation id (the control's <see cref="Control.Name"/>).</summary>
        public static By Id (string id) => new ($"id={id}", e => e.AutomationId == id);

        /// <summary>Matches by accessible name (text/AccessibleName).</summary>
        public static By Name (string name) => new ($"name={name}", e => e.Name == name);

        /// <summary>Matches by role (case-insensitive), e.g. <c>button</c>, <c>textbox</c>.</summary>
        public static By Role (string role) =>
            new ($"role={role}", e => string.Equals (e.Role, role, StringComparison.OrdinalIgnoreCase));

        /// <summary>Matches by concrete control type name (case-insensitive), e.g. <c>RadButton</c>.</summary>
        public static By Type (string controlType) =>
            new ($"type={controlType}", e => string.Equals (e.ControlType, controlType, StringComparison.OrdinalIgnoreCase));

        /// <summary>Matches when the element's name or value equals the given text.</summary>
        public static By Text (string text) =>
            new ($"text={text}", e => e.Name == text || e.Value == text);
    }
}
