using System;
using System.Drawing;
using Majorsilence.Forms;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat button. Backed by <see cref="Majorsilence.Forms.Button"/>.</summary>
    public class RadButton : Button
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat label. Backed by <see cref="Majorsilence.Forms.Label"/>.</summary>
    public class RadLabel : Label
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat link label. Backed by <see cref="Majorsilence.Forms.LinkLabel"/>.</summary>
    public class RadLinkLabel : LinkLabel { }

    /// <summary>Telerik-compat text box. Backed by <see cref="Majorsilence.Forms.TextBox"/>.</summary>
    public class RadTextBox : TextBox
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat text box control. Backed by <see cref="Majorsilence.Forms.TextBox"/>.</summary>
    public class RadTextBoxControl : TextBox
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat check box. Backed by <see cref="Majorsilence.Forms.CheckBox"/>.</summary>
    public class RadCheckBox : CheckBox
    {
        /// <summary>Initializes a new instance of the RadCheckBox class.</summary>
        public RadCheckBox ()
        {
            CheckedChanged += (_, _) => ToggleStateChanged?.Invoke (this, new StateChangedEventArgs (ToggleState));
        }

        /// <summary>Gets or sets whether the check box is checked (Telerik alias for <see cref="CheckBox.Checked"/>).</summary>
        public bool IsChecked {
            get => Checked;
            set => Checked = value;
        }

        /// <summary>Gets or sets the toggle state.</summary>
        public ToggleState ToggleState {
            get => CheckState switch {
                Majorsilence.Forms.CheckState.Checked => ToggleState.On,
                Majorsilence.Forms.CheckState.Indeterminate => ToggleState.Indeterminate,
                _ => ToggleState.Off
            };
            set => CheckState = value switch {
                ToggleState.On => Majorsilence.Forms.CheckState.Checked,
                ToggleState.Indeterminate => Majorsilence.Forms.CheckState.Indeterminate,
                _ => Majorsilence.Forms.CheckState.Unchecked
            };
        }

        /// <summary>Raised when the toggle state changes.</summary>
        public event EventHandler<StateChangedEventArgs>? ToggleStateChanged;
    }

    /// <summary>Telerik-compat radio button. Backed by <see cref="Majorsilence.Forms.RadioButton"/>.</summary>
    public class RadRadioButton : RadioButton
    {
        /// <summary>Initializes a new instance of the RadRadioButton class.</summary>
        public RadRadioButton ()
        {
            CheckedChanged += (_, _) => ToggleStateChanged?.Invoke (this, new StateChangedEventArgs (Checked ? ToggleState.On : ToggleState.Off));
        }

        /// <summary>Gets or sets whether the radio button is checked (Telerik alias for <see cref="RadioButton.Checked"/>).</summary>
        public bool IsChecked {
            get => Checked;
            set => Checked = value;
        }

        /// <summary>Raised when the toggle state changes.</summary>
        public event EventHandler<StateChangedEventArgs>? ToggleStateChanged;
    }

    /// <summary>Telerik-compat on/off switch. Backed by <see cref="Majorsilence.Forms.CheckBox"/>.</summary>
    public class RadToggleSwitch : CheckBox
    {
        /// <summary>Initializes a new instance of the RadToggleSwitch class.</summary>
        public RadToggleSwitch ()
        {
            CheckedChanged += (_, _) => ValueChanged?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>Gets or sets the switch value (Telerik's primary accessor; maps to <see cref="CheckBox.Checked"/>).</summary>
        public bool Value {
            get => Checked;
            set => Checked = value;
        }

        /// <summary>Gets or sets the text shown in the "on" position.</summary>
        public string OnText { get; set; } = "On";
        /// <summary>Gets or sets the text shown in the "off" position.</summary>
        public string OffText { get; set; } = "Off";
        /// <summary>Gets or sets how the switch changes state.</summary>
        public ToggleStateMode ToggleStateMode { get; set; } = ToggleStateMode.Click;
        /// <summary>Gets or sets the thumb thickness (Telerik spelling preserved). Stub.</summary>
        public int ThumbTickness { get; set; }
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when the value changes.</summary>
        public event EventHandler? ValueChanged;
    }

    /// <summary>Telerik-compat group box. Backed by <see cref="Majorsilence.Forms.GroupBox"/>.</summary>
    public class RadGroupBox : GroupBox
    {
        /// <summary>Gets or sets the header text (Telerik alias for <see cref="Control.Text"/>).</summary>
        public string HeaderText {
            get => Text;
            set => Text = value;
        }

        /// <summary>Gets or sets the footer text. Stub (not rendered).</summary>
        public string FooterText { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat panel. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadPanel : Panel
    {
        /// <summary>Gets or sets the header text. Stub.</summary>
        public new string Text { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat form. Backed by <see cref="Majorsilence.Forms.Form"/>.</summary>
    public class RadForm : Form
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets or sets the icon scaling mode. Stub.</summary>
        public object? IconScaling { get; set; }
        /// <summary>Gets the root element of the form (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when the theme changes. Stub.</summary>
        protected virtual void OnThemeChanged () { }
    }

    /// <summary>Telerik-compat ribbon form. Backed by <see cref="Majorsilence.Forms.Form"/>.</summary>
    public class RadRibbonForm : RadForm { }

    /// <summary>Telerik-compat indeterminate progress / waiting indicator. Backed by <see cref="Majorsilence.Forms.ProgressBar"/>.</summary>
    public class RadWaitingBar : ProgressBar
    {
        /// <summary>Initializes a new instance of the RadWaitingBar class.</summary>
        public RadWaitingBar ()
        {
            Style = ProgressBarStyle.Marquee;
        }

        /// <summary>Gets whether the bar is currently animating.</summary>
        public bool IsWaiting { get; private set; }
        /// <summary>Gets or sets the waiting animation style. Stub.</summary>
        public WaitingBarStyles WaitingStyle { get; set; } = WaitingBarStyles.Dash;
        /// <summary>Gets or sets the animation speed, in ms.</summary>
        public int WaitingSpeed { get; set; } = 100;
        /// <summary>Gets or sets the animation step. Stub.</summary>
        public int WaitingStep { get; set; } = 1;
        /// <summary>Gets or sets the number of waiting indicators. Stub.</summary>
        public int WaitingIndicators { get; set; } = 5;
        /// <summary>Gets or sets the size of each indicator. Stub.</summary>
        public Size WaitingIndicatorSize { get; set; }

        /// <summary>Starts the waiting animation.</summary>
        public void StartWaiting ()
        {
            IsWaiting = true;
            MarqueeAnimationSpeed = WaitingSpeed;
        }

        /// <summary>Stops the waiting animation.</summary>
        public void StopWaiting () => IsWaiting = false;

        /// <summary>Returns the child element at the given index (stub).</summary>
        public RadElement GetChildAt (int index) => new RadElement ();
    }

    /// <summary>Telerik-compat list control. Backed by <see cref="Majorsilence.Forms.ListBox"/>.</summary>
    public class RadListControl : ListBox { }

    /// <summary>Telerik-compat list data item.</summary>
    public class RadListDataItem
    {
        /// <summary>Initializes a new, empty instance.</summary>
        public RadListDataItem () { }
        /// <summary>Initializes a new instance with the specified text.</summary>
        public RadListDataItem (string text) { Text = text; }
        /// <summary>Initializes a new instance with the specified text and value.</summary>
        public RadListDataItem (string text, object? value) { Text = text; Value = value; }

        /// <summary>Gets or sets the display text.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Gets or sets the value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets whether the item is checked.</summary>
        public bool Checked { get; set; }
        /// <summary>Gets or sets the item tag.</summary>
        public object? Tag { get; set; }

        /// <inheritdoc/>
        public override string ToString () => Text;
    }

    /// <summary>Telerik-compat date/time picker. Backed by <see cref="Majorsilence.Forms.DateTimePicker"/>.</summary>
    public class RadDateTimePicker : DateTimePicker
    {
        /// <summary>Initializes a new instance of the RadDateTimePicker class.</summary>
        public RadDateTimePicker ()
        {
            ValueChanged += (_, _) => ValueChanging?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>Gets the picker element (stub).</summary>
        public RadElement DateTimePickerElement { get; } = new RadElement ();
        /// <summary>Raised before the value changes. Stub (fires alongside ValueChanged).</summary>
        public event EventHandler? ValueChanging;
        /// <summary>Raised when the drop-down calendar opens. Stub.</summary>
        public event EventHandler? Opened { add { } remove { } }
    }

    /// <summary>Provides data for a Telerik toggle state change.</summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance with the specified toggle state.</summary>
        public StateChangedEventArgs (ToggleState toggleState) => ToggleState = toggleState;
        /// <summary>Gets the new toggle state.</summary>
        public ToggleState ToggleState { get; }
    }
}
