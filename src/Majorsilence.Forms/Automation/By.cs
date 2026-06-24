using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// A locator strategy for finding an <see cref="AutomationElement"/>. Mirrors the familiar
    /// Selenium <c>By</c> shape so the in-process API and the WebDriver server share one vocabulary.
    /// Most strategies are per-element predicates; <see cref="XPath"/> is a tree query evaluated against
    /// the whole snapshot (see <see cref="Select"/>).
    /// </summary>
    public sealed class By
    {
        private readonly Func<AutomationElement, bool>? _predicate;
        private readonly Func<AutomationElement, IEnumerable<AutomationElement>>? _selector;

        private By (string description, Func<AutomationElement, bool> predicate)
        {
            Description = description;
            _predicate = predicate;
        }

        private By (string description, Func<AutomationElement, IEnumerable<AutomationElement>> selector)
        {
            Description = description;
            _selector = selector;
        }

        /// <summary>A human-readable description of this locator (used in error messages).</summary>
        public string Description { get; }

        /// <summary>
        /// Returns whether the element matches this locator. Not supported for tree-query locators such as
        /// <see cref="XPath"/> — use a session <c>Find</c>/<c>FindAll</c>, which calls <see cref="Select"/>.
        /// </summary>
        public bool Matches (AutomationElement element) =>
            _predicate != null
                ? _predicate (element)
                : throw new NotSupportedException ($"'{Description}' is a tree query; use Find/FindAll (Select), not Matches.");

        // Selects the matching elements from a tree root. Predicate locators test every node depth-first;
        // tree-query locators (XPath) evaluate against the whole tree. This is the single entry point the
        // session uses, so both kinds of locator flow through one code path.
        internal IEnumerable<AutomationElement> Select (AutomationElement root) =>
            _selector != null ? _selector (root) : root.Self ().Where (_predicate!);

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

        /// <summary>
        /// Matches via an XPath expression evaluated against the tree's XML rendering: each control is an
        /// element whose tag is its control type, with attributes <c>id</c>, <c>name</c>, <c>role</c>,
        /// <c>type</c>, <c>value</c>, <c>enabled</c>, <c>visible</c>, <c>x</c>, <c>y</c>, <c>width</c>,
        /// <c>height</c> — the same XML the WebDriver <c>source</c> command returns. Only element-selecting
        /// expressions are supported (the matched elements are returned, in document order).
        /// </summary>
        public static By XPath (string expression) =>
            new ($"xpath={expression}", root => EvaluateXPath (root, expression));

        private static List<AutomationElement> EvaluateXPath (AutomationElement root, string expression)
        {
            var tree = AutomationXml.ToXml (root);
            var results = new List<AutomationElement> ();
            foreach (var node in tree.Document.XPathSelectElements (expression))
                if (tree.Map.TryGetValue (node, out var el))
                    results.Add (el);
            return results;
        }
    }
}
