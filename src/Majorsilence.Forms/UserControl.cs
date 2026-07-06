using System;
using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides an empty control that can be used to create other controls.
    /// In Majorsilence.Forms, UserControl is an alias for Panel with full tab-stop support.
    /// </summary>
    public class UserControl : Panel
    {
        /// <summary>
        /// Initializes a new instance of the UserControl class.
        /// </summary>
        public UserControl ()
        {
            TabStop = true;
            SetControlBehavior (ControlBehaviors.Selectable, true);
        }

        // AutoSizeMode is inherited from Panel (same Get/SetAutoSizeMode mechanism).

        /// <summary>Gets or sets how the control should scale when its parent changes DPI.</summary>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Font;

        /// <summary>Gets or sets the auto-scale dimensions (no-op stub).</summary>
        public System.Drawing.SizeF AutoScaleDimensions { get; set; }

        /// <summary>Gets or sets how the UserControl validates its children. Stub in Majorsilence.Forms.</summary>
        public AutoValidate AutoValidate { get; set; } = AutoValidate.EnablePreventFocusChange;

        /// <summary>Gets or sets the active control inside this UserControl.</summary>
        public Control? ActiveControl { get; set; }

        /// <summary>Validates all child controls by triggering Validating/Validated events. Stub in Majorsilence.Forms.</summary>
        public bool ValidateChildren () => true;

        /// <summary>Raised when the control is first displayed.</summary>
        public event EventHandler? Load;

        /// <summary>Raises the Load event.</summary>
        protected virtual void OnLoad (EventArgs e) => Load?.Invoke (this, e);
    }

    /// <summary>
    /// Provides focus-management functionality for controls that contain other controls.
    /// In Majorsilence.Forms this is an alias for Panel.
    /// </summary>
    public class ContainerControl : Panel
    {
        /// <summary>Gets or sets the active control inside this container.</summary>
        public Control? ActiveControl { get; set; }

        /// <summary>Gets or sets how the container validates its children. Stub in Majorsilence.Forms.</summary>
        public AutoValidate AutoValidate { get; set; } = AutoValidate.EnablePreventFocusChange;

        /// <summary>Gets or sets the auto-scale mode. Stub in Majorsilence.Forms.</summary>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Font;

        /// <summary>Gets or sets the auto-scale dimensions. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.SizeF AutoScaleDimensions { get; set; }

        /// <summary>Validates all children. Stub in Majorsilence.Forms.</summary>
        public bool ValidateChildren () => true;
    }
}
