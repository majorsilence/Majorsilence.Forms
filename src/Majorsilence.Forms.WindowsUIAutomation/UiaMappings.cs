using System.Windows.Automation;

namespace Majorsilence.Forms.WindowsUIAutomation
{
    // Maps the framework's coarse role strings (see AutomationProvider.RoleOf) onto UI Automation
    // control types and pattern eligibility. Unknown roles fall back to the Custom control type.
    internal static class UiaMappings
    {
        public static int ControlTypeId (string? role) => Normalize (role) switch {
            "button" => ControlType.Button.Id,
            "checkbox" => ControlType.CheckBox.Id,
            "radio" => ControlType.RadioButton.Id,
            "textbox" => ControlType.Edit.Id,
            "combobox" => ControlType.ComboBox.Id,
            "list" => ControlType.List.Id,
            "label" => ControlType.Text.Id,
            "tablist" => ControlType.Tab.Id,
            "progressbar" => ControlType.ProgressBar.Id,
            "scrollbar" => ControlType.ScrollBar.Id,
            "group" => ControlType.Group.Id,
            "window" => ControlType.Window.Id,
            _ => ControlType.Custom.Id
        };

        public static bool IsInvokable (string? role) => Normalize (role) == "button";

        public static bool IsValueEditable (string? role) => Normalize (role) is "textbox" or "combobox";

        public static bool IsTogglable (string? role) => Normalize (role) is "checkbox" or "radio";

        public static bool IsFocusable (string? role) =>
            Normalize (role) is "button" or "checkbox" or "radio" or "textbox" or "combobox" or "list";

        private static string Normalize (string? role) => role?.ToLowerInvariant () ?? string.Empty;
    }
}
