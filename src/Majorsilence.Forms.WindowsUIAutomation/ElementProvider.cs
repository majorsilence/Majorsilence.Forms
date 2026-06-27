using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using Majorsilence.Forms.Automation;

namespace Majorsilence.Forms.WindowsUIAutomation
{
    /// <summary>
    /// A UI Automation provider for one node of the Majorsilence.Forms automation tree, identified by its
    /// child-index path from the window root (an empty path is the window root itself). It carries no live
    /// state: every property read re-resolves the element against a fresh tree snapshot on the UI thread
    /// (via <see cref="UiaBridge"/>), so what UIA reports always matches the current UI.
    /// </summary>
    internal sealed class ElementProvider :
        IRawElementProviderSimple,
        IRawElementProviderFragment,
        IRawElementProviderFragmentRoot,
        IInvokeProvider,
        IValueProvider,
        IToggleProvider
    {
        private readonly UiaBridge _bridge;
        private readonly int[] _path;

        public ElementProvider (UiaBridge bridge, int[] path)
        {
            _bridge = bridge;
            _path = path;
        }

        private bool IsRoot => _path.Length == 0;

        // ── IRawElementProviderSimple ──────────────────────────────────────────────

        public ProviderOptions ProviderOptions => ProviderOptions.ServerSideProvider;

        // Only the root is hosted in the HWND; child fragments return null (the framework chains up).
        public IRawElementProviderSimple? HostRawElementProvider => IsRoot ? _bridge.HostProvider : null;

        public object? GetPropertyValue (int propertyId)
        {
            var el = _bridge.Resolve (_path);
            if (el == null)
                return null;

            if (propertyId == AutomationElementIdentifiers.NameProperty.Id) return el.Name;
            if (propertyId == AutomationElementIdentifiers.AutomationIdProperty.Id) return el.AutomationId;
            if (propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id) return UiaMappings.ControlTypeId (el.Role);
            if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id) return el.Enabled;
            if (propertyId == AutomationElementIdentifiers.HasKeyboardFocusProperty.Id) return el.Focused;
            if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id) return UiaMappings.IsFocusable (el.Role);
            if (propertyId == AutomationElementIdentifiers.IsControlElementProperty.Id) return true;
            if (propertyId == AutomationElementIdentifiers.IsContentElementProperty.Id) return true;
            return null;
        }

        public object? GetPatternProvider (int patternId)
        {
            var el = _bridge.Resolve (_path);
            if (el == null)
                return null;

            if (patternId == InvokePatternIdentifiers.Pattern.Id && UiaMappings.IsInvokable (el.Role)) return this;
            if (patternId == ValuePatternIdentifiers.Pattern.Id && UiaMappings.IsValueEditable (el.Role)) return this;
            if (patternId == TogglePatternIdentifiers.Pattern.Id && UiaMappings.IsTogglable (el.Role)) return this;
            return null;
        }

        // ── IRawElementProviderFragment ────────────────────────────────────────────

        public Rect BoundingRectangle => IsRoot ? Rect.Empty : _bridge.ScreenRect (_path);

        public IRawElementProviderFragmentRoot FragmentRoot => _bridge.Root;

        public IRawElementProviderSimple[]? GetEmbeddedFragmentRoots () => null;

        public int[] GetRuntimeId ()
        {
            // AppendRuntimeId prefixes the HWND-derived id; the index path makes us unique within the window.
            var id = new int[_path.Length + 1];
            id[0] = AutomationInteropProvider.AppendRuntimeId;
            Array.Copy (_path, 0, id, 1, _path.Length);
            return id;
        }

        public IRawElementProviderFragment? Navigate (NavigateDirection direction) => _bridge.Navigate (_path, direction);

        public void SetFocus () => _bridge.InvokePath (_path);  // clicking routes focus through the real pipeline

        // ── IRawElementProviderFragmentRoot ────────────────────────────────────────

        public IRawElementProviderFragment? ElementProviderFromPoint (double x, double y) => _bridge.FromPoint (x, y);

        public IRawElementProviderFragment? GetFocus () => _bridge.FocusedProvider ();

        // ── IInvokeProvider ────────────────────────────────────────────────────────

        public void Invoke () => _bridge.InvokePath (_path);

        // ── IValueProvider ─────────────────────────────────────────────────────────

        public bool IsReadOnly => false;

        public string Value => _bridge.Resolve (_path)?.Value ?? string.Empty;

        public void SetValue (string value) => _bridge.SetValuePath (_path, value);

        // ── IToggleProvider ────────────────────────────────────────────────────────

        public ToggleState ToggleState => _bridge.Resolve (_path)?.Value == "true" ? ToggleState.On : ToggleState.Off;

        public void Toggle () => _bridge.InvokePath (_path);  // clicking toggles the checked state
    }
}
