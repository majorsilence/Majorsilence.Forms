using System.Collections.ObjectModel;
using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// Provides data for the FormClosing event.
    /// </summary>
    public class FormClosingEventArgs : System.ComponentModel.CancelEventArgs { }

    /// <summary>
    /// Provides data for the FormClosed event.
    /// </summary>
    public class FormClosedEventArgs : EventArgs { }

    /// <summary>
    /// Delegate for the FormClosed event.
    /// </summary>
    public delegate void FormClosedEventHandler (object sender, FormClosedEventArgs e);

    /// <summary>
    /// Delegate for the FormClosing event.
    /// </summary>
    public delegate void FormClosingEventHandler (object sender, FormClosingEventArgs e);

    /// <summary>
    /// Delegate for keyboard key events.
    /// </summary>
    public delegate void KeyEventHandler (object sender, KeyEventArgs e);


    /// <summary>
    /// Specifies how a form scales its contents.
    /// </summary>
    public enum AutoScaleMode
    {
        /// <summary>Scaling is disabled.</summary>
        None,
        /// <summary>Scale relative to the font.</summary>
        Font,
        /// <summary>Scale relative to the display DPI.</summary>
        Dpi,
        /// <summary>Inherited from the parent.</summary>
        Inherit
    }

    /// <summary>
    /// Specifies which buttons to display on a MessageBox.
    /// </summary>
    public enum MessageBoxButtons
    {
        /// <summary>OK button.</summary>
        OK,
        /// <summary>OK and Cancel buttons.</summary>
        OKCancel,
        /// <summary>Abort, Retry, and Ignore buttons.</summary>
        AbortRetryIgnore,
        /// <summary>Yes, No, and Cancel buttons.</summary>
        YesNoCancel,
        /// <summary>Yes and No buttons.</summary>
        YesNo,
        /// <summary>Retry and Cancel buttons.</summary>
        RetryCancel
    }

    /// <summary>
    /// Specifies the icon to display on a MessageBox.
    /// </summary>
    public enum MessageBoxIcon
    {
        /// <summary>No icon.</summary>
        None = 0,
        /// <summary>Error icon.</summary>
        Error = 16,
        /// <summary>Warning icon.</summary>
        Warning = 48,
        /// <summary>Information icon.</summary>
        Information = 64,
        /// <summary>Question icon.</summary>
        Question = 32
    }

    /// <summary>
    /// Displays a message box with a specified message, title, buttons, and icon.
    /// </summary>
    public static class MessageBox
    {
        /// <summary>Shows a message box with the specified text.</summary>
        public static DialogResult Show (string text)
            => Show (text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text and caption.</summary>
        public static DialogResult Show (string text, string caption)
            => Show (text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text, caption, and buttons.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons)
            => Show (text, caption, buttons, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text, caption, buttons, and icon.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var parent = Application.OpenForms.FirstOrDefault ();
            var form = new MessageBoxForm (caption, text);

            if (parent != null)
                form.ShowDialog (parent);

            return buttons == MessageBoxButtons.YesNo ? DialogResult.Yes : DialogResult.OK;
        }
    }

    /// <summary>
    /// Base class for items that appear in a MenuStrip or StatusStrip.
    /// </summary>
    public class ToolStripItem : MenuItem
    {
        /// <summary>Gets or sets the name of this item.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the size of this item (informational only).</summary>
        public Size Size { get; set; }

        /// <summary>Gets or sets whether this item is visible.</summary>
        public bool Visible { get; set; } = true;
    }

    /// <summary>
    /// Represents a menu item in a MenuStrip.
    /// </summary>
    public class ToolStripMenuItem : ToolStripItem
    {
        /// <summary>Gets the collection of sub-items for this menu item.</summary>
        public MenuItemCollection DropDownItems => Items;
    }

    /// <summary>
    /// Represents a separator in a MenuStrip or ContextMenuStrip.
    /// </summary>
    public class ToolStripSeparator : ToolStripItem
    {
        /// <summary>Initializes a new instance of the ToolStripSeparator class.</summary>
        public ToolStripSeparator ()
        {
            Padding = new Padding (3);
        }
    }

    /// <summary>
    /// Represents a combo box embedded in a MenuStrip.
    /// </summary>
    public class ToolStripComboBox : ToolStripItem
    {
        private readonly CompatComboBox combo_box = new CompatComboBox ();

        /// <summary>Gets the underlying ComboBox control.</summary>
        public CompatComboBox ComboBox => combo_box;

        /// <summary>Gets or sets the selected index.</summary>
        public int SelectedIndex {
            get => combo_box.SelectedIndex;
            set => combo_box.SelectedIndex = value;
        }

        /// <summary>Raised when the selected index changes.</summary>
        public event EventHandler? SelectedIndexChanged {
            add => combo_box.SelectedIndexChanged += value;
            remove => combo_box.SelectedIndexChanged -= value;
        }
    }

    /// <summary>
    /// A ComboBox with basic data-binding support for WinForms compatibility.
    /// </summary>
    public class CompatComboBox : ComboBox
    {
        private object? data_source;
        private string display_member = string.Empty;
        private string value_member = string.Empty;

        /// <summary>Gets or sets the data source for this combo box.</summary>
        public object? DataSource {
            get => data_source;
            set {
                data_source = value;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the property name to use for display.</summary>
        public string DisplayMember {
            get => display_member;
            set {
                display_member = value ?? string.Empty;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the property name to use as the value.</summary>
        public string ValueMember {
            get => value_member;
            set {
                value_member = value ?? string.Empty;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the binding context (no-op for compatibility).</summary>
        public object? BindingContext { get; set; }

        /// <summary>Gets or sets the selected value using ValueMember.</summary>
        public object? SelectedValue {
            get {
                if (SelectedIndex < 0 || data_source == null)
                    return null;

                var list = data_source as System.Collections.IList;

                if (list == null || SelectedIndex >= list.Count)
                    return null;

                var item = list[SelectedIndex];

                if (string.IsNullOrEmpty (value_member))
                    return item;

                return item?.GetType ().GetProperty (value_member)?.GetValue (item);
            }
            set {
                if (data_source == null || value == null)
                    return;

                var list = data_source as System.Collections.IList;

                if (list == null)
                    return;

                for (int i = 0; i < list.Count; i++) {
                    var item = list[i];
                    var item_value = string.IsNullOrEmpty (value_member)
                        ? item
                        : item?.GetType ().GetProperty (value_member)?.GetValue (item);

                    if (Equals (item_value, value)) {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        private void RefreshItems ()
        {
            Items.Clear ();

            var list = data_source as System.Collections.IList;

            if (list == null)
                return;

            foreach (var item in list) {
                string display;

                if (!string.IsNullOrEmpty (display_member))
                    display = item?.GetType ().GetProperty (display_member)?.GetValue (item)?.ToString () ?? string.Empty;
                else
                    display = item?.ToString () ?? string.Empty;

                Items.Add (display);
            }
        }
    }

    /// <summary>
    /// A collection of ToolStripItem objects.
    /// </summary>
    public class ToolStripItemCollection : Collection<ToolStripItem>
    {
        /// <summary>Adds a range of items to the collection.</summary>
        public void AddRange (IEnumerable<ToolStripItem> items)
        {
            foreach (var item in items)
                Add (item);
        }
    }

    /// <summary>
    /// Represents a text label in a StatusStrip.
    /// </summary>
    public class ToolStripStatusLabel : ToolStripItem { }

    /// <summary>
    /// Represents a progress bar embedded in a StatusStrip.
    /// </summary>
    public class ToolStripProgressBar : ToolStripItem
    {
        /// <summary>Gets or sets the current value.</summary>
        public int Value { get; set; }

        /// <summary>Gets or sets the maximum value.</summary>
        public int Maximum { get; set; } = 100;

        /// <summary>Gets or sets the minimum value.</summary>
        public int Minimum { get; set; }
    }

    /// <summary>
    /// Represents a menu bar docked at the top of a form. Alias for Menu.
    /// </summary>
    public class MenuStrip : Menu { }

    /// <summary>
    /// Represents a status bar with items docked at the bottom of a form.
    /// </summary>
    public class StatusStrip : Control
    {
        /// <summary>Initializes a new instance of the StatusStrip class.</summary>
        public StatusStrip ()
        {
            Dock = DockStyle.Bottom;
            Items = new ToolStripItemCollection ();
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
        }

        /// <summary>Gets the collection of items displayed in this StatusStrip.</summary>
        public ToolStripItemCollection Items { get; }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (1, 0, 16, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 22);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Top.Width = 1;
                style.FontSize = 13;
            });

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            Renderers.RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
