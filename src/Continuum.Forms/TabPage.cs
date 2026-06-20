namespace Continuum.Forms
{
    /// <summary>
    /// Represents a TabPage control.
    /// </summary>
    public class TabPage : Panel
    {
        /// <summary>
        /// Initializes a new instance of the TabPage class.
        /// </summary>
        public TabPage ()
        {
            Dock = DockStyle.Fill;
            TabStripItem = new TabStripItem ();
        }

        /// <summary>
        /// Initializes a new instance of the TabPage class with the specified text.
        /// </summary>
        public TabPage (string text) : this ()
        {
            TabStripItem.Text = text;
        }

        // The TabStripItem that accompanies this TabPage.
        internal TabStripItem TabStripItem { get; }

        /// <inheritdoc/>
        public override string Text {
            get => TabStripItem.Text;
            set => TabStripItem.Text = value ?? string.Empty;
        }

        /// <summary>Gets or sets the ToolTip text for this tab page.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the index into the ImageList of the image to display on this tab.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the key into the ImageList of the image to display on this tab.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the tab page is enabled.</summary>
        public new bool Enabled { get; set; } = true;

        /// <summary>Gets or sets whether the tab page uses the visual style of the tab control.</summary>
        public new bool UseVisualStyleBackColor { get; set; }
    }
}
