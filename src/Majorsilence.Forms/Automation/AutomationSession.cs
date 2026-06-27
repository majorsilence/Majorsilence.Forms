using System;
using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// An in-process automation/UI-test handle for a window: query the element tree with <see cref="By"/>
    /// locators and drive controls (click, type) through the same neutral input pipeline a real backend
    /// uses. Works against any backend (headless or real). The WebDriver server is a thin remote shell
    /// over this class.
    /// </summary>
    public sealed class AutomationSession
    {
        private readonly WindowBase _window;

        /// <summary>Creates a session that drives the given window.</summary>
        public AutomationSession (WindowBase window)
        {
            _window = window ?? throw new ArgumentNullException (nameof (window));
        }

        /// <summary>Builds a fresh snapshot of the window's automation tree (rooted at the window node).</summary>
        public AutomationElement Root => AutomationProvider.BuildTree (_window);

        /// <summary>
        /// The window's automation tree rendered as XML page source — the same shape <see cref="By.XPath"/>
        /// queries against (tag = control type; attributes id/name/role/type/value/enabled/visible/bounds).
        /// Lets an inspector (e.g. Appium Inspector) display the tree and capture XPath locators.
        /// </summary>
        public string GetPageSource () => AutomationXml.ToXml (Root).Document.ToString ();

        /// <summary>Finds the first element matching the locator, or null.</summary>
        public AutomationElement? Find (By by)
        {
            ArgumentNullException.ThrowIfNull (by);
            return by.Select (Root).FirstOrDefault ();
        }

        /// <summary>Finds the first element matching the locator, or throws if none.</summary>
        public AutomationElement FindOrThrow (By by) =>
            Find (by) ?? throw new InvalidOperationException ($"No element matched locator: {by.Description}");

        /// <summary>Finds all elements matching the locator.</summary>
        public IReadOnlyList<AutomationElement> FindAll (By by)
        {
            ArgumentNullException.ThrowIfNull (by);
            return by.Select (Root).ToList ();
        }

        /// <summary>Clicks the element by synthesizing move → press → release at its center.</summary>
        public void Click (AutomationElement element)
        {
            ArgumentNullException.ThrowIfNull (element);

            var p = element.ClickPoint;
            _window.HandlePointerMoved (MouseButtons.Left, p.X, p.Y, Keys.None);
            _window.HandlePointerPressed (MouseButtons.Left, p.X, p.Y, Keys.None);
            _window.HandlePointerReleased (MouseButtons.Left, p.X, p.Y, Keys.None);
        }

        /// <summary>Clicks the element to focus it, then sends the text as input.</summary>
        public void SendKeys (AutomationElement element, string text)
        {
            ArgumentNullException.ThrowIfNull (element);

            Click (element);

            if (!string.IsNullOrEmpty (text))
                _window.HandleTextInput (text);
        }

        /// <summary>Sends a key (with optional modifiers) to the focused control.</summary>
        public void PressKey (Keys keys) => _window.HandleKeyDown (keys);

        /// <summary>Clears the element's editable text (focuses it first). No-op for non-text controls.</summary>
        public void Clear (AutomationElement element)
        {
            ArgumentNullException.ThrowIfNull (element);

            Click (element);

            if (element.Source is TextBox tb)
                tb.Text = string.Empty;
        }

        /// <summary>Gets the element's value if it has one, otherwise its accessible name.</summary>
        public string GetText (AutomationElement element)
        {
            ArgumentNullException.ThrowIfNull (element);
            return element.Value ?? element.Name;
        }
    }
}
