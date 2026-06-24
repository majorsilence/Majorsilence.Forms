using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// Serializes an <see cref="AutomationElement"/> tree to XML — the single source of truth shared by
    /// the WebDriver <c>GET .../source</c> command (page source) and the <see cref="By.XPath"/> locator.
    /// Both must see identical XML, or an XPath captured against the page source would not replay, so they
    /// consume the same document.
    ///
    /// Each control becomes an element whose tag is its <see cref="AutomationElement.ControlType"/>
    /// (sanitized to a valid XML name) and whose attributes carry its automation properties. The
    /// <see cref="AutomationXmlTree.Map"/> records the live element each <see cref="XElement"/> came from,
    /// keyed by reference, so XPath results map straight back to elements without a synthetic id attribute
    /// leaking into the page source.
    /// </summary>
    internal static class AutomationXml
    {
        /// <summary>Builds the XML rendering of <paramref name="root"/> plus the node→element reverse map.</summary>
        public static AutomationXmlTree ToXml (AutomationElement root)
        {
            var map = new Dictionary<XElement, AutomationElement> ();
            var rootEl = Build (root, map);
            return new AutomationXmlTree (new XDocument (rootEl), map);
        }

        private static XElement Build (AutomationElement e, Dictionary<XElement, AutomationElement> map)
        {
            var el = new XElement (TagName (e),
                new XAttribute ("id", e.AutomationId),
                new XAttribute ("name", e.Name),
                new XAttribute ("role", e.Role),
                new XAttribute ("type", e.ControlType),
                new XAttribute ("value", e.Value ?? string.Empty),
                new XAttribute ("enabled", e.Enabled ? "true" : "false"),
                new XAttribute ("visible", e.Visible ? "true" : "false"),
                new XAttribute ("focused", e.Focused ? "true" : "false"),
                new XAttribute ("x", e.Bounds.X.ToString (CultureInfo.InvariantCulture)),
                new XAttribute ("y", e.Bounds.Y.ToString (CultureInfo.InvariantCulture)),
                new XAttribute ("width", e.Bounds.Width.ToString (CultureInfo.InvariantCulture)),
                new XAttribute ("height", e.Bounds.Height.ToString (CultureInfo.InvariantCulture)));

            map[el] = e;

            foreach (var child in e.Children)
                el.Add (Build (child, map));

            return el;
        }

        // ControlType is normally a C# type name (already a valid XML name), but be defensive: generic type
        // names contain '`', and an empty or odd type would otherwise throw. Fall back to role, then "node".
        private static string TagName (AutomationElement e)
        {
            var raw = !string.IsNullOrEmpty (e.ControlType) ? e.ControlType
                    : !string.IsNullOrEmpty (e.Role) ? e.Role
                    : "node";
            return Sanitize (raw);
        }

        private static string Sanitize (string s)
        {
            var sb = new StringBuilder (s.Length);
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                var ok = i == 0
                    ? char.IsLetter (c) || c == '_'
                    : char.IsLetterOrDigit (c) || c == '_' || c == '-' || c == '.';
                sb.Append (ok ? c : '_');
            }
            return sb.Length == 0 ? "node" : sb.ToString ();
        }
    }

    /// <summary>An XML rendering of an automation tree plus the reverse map from each node to its element.</summary>
    internal readonly struct AutomationXmlTree
    {
        public AutomationXmlTree (XDocument document, IReadOnlyDictionary<XElement, AutomationElement> map)
        {
            Document = document;
            Map = map;
        }

        /// <summary>The serialized tree (root element = the window node).</summary>
        public XDocument Document { get; }

        /// <summary>Maps each <see cref="XElement"/> back to the live <see cref="AutomationElement"/> it came from.</summary>
        public IReadOnlyDictionary<XElement, AutomationElement> Map { get; }
    }
}
