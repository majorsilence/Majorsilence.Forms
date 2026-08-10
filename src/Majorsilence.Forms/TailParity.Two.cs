using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Printing;

namespace Majorsilence.Forms
{
    // The second half of the flat tail (docs/winforms-gap-plan.md).
    //
    // The key-based collection members -- ToolStripItemCollection's ContainsKey, Find, IndexOfKey and
    // RemoveByKey -- are the ones with the most reach: a designer file names items by key, so code
    // generated against WinForms reaches for them constantly, and they are real here because the
    // items already carry a Name.
    //
    // TabControl.DeselectTab and BindingContext.Contains are likewise computed. The dialog flags that
    // follow are stored: they select between pages of a Win32 common dialog the backends do not show.

    public partial class ToolStripItemCollection
    {
        /// <summary>Gets whether this collection can be modified.</summary>
        public virtual bool IsReadOnly => false;

        /// <summary>Returns whether an item with the given name is in this collection.</summary>
        public virtual bool ContainsKey (string key) => IndexOfKey (key) >= 0;

        /// <summary>Returns the index of the item with the given name, or -1.</summary>
        public virtual int IndexOfKey (string key)
        {
            if (string.IsNullOrEmpty (key))
                return -1;

            for (var i = 0; i < Count; i++)
                if (this[i] is ToolStripItem item
                    && string.Equals (item.Name, key, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
        }

        /// <summary>Removes the item with the given name, if there is one.</summary>
        public virtual void RemoveByKey (string key)
        {
            var index = IndexOfKey (key);

            if (index >= 0)
                RemoveAt (index);
        }

        /// <summary>Returns every item with the given name, optionally searching drop-downs.</summary>
        public ToolStripItem[] Find (string key, bool searchAllChildren)
        {
            ArgumentException.ThrowIfNullOrEmpty (key);

            var found = new List<ToolStripItem> ();
            Collect (this, found);
            return [.. found];

            void Collect (IEnumerable<MenuItem> items, List<ToolStripItem> into)
            {
                foreach (var item in items) {
                    if (item is ToolStripItem stripItem
                        && string.Equals (stripItem.Name, key, StringComparison.OrdinalIgnoreCase))
                        into.Add (stripItem);

                    // A drop-down's items are children of this strip as far as a designer is
                    // concerned, which is what searchAllChildren is asking about. Testing HasItems on
                    // the MenuItem base rather than casting to ToolStripDropDownItem is deliberate:
                    // ToolStripMenuItem exposes DropDownItems but does not derive from that type here.
                    if (searchAllChildren && item.HasItems)
                        Collect (item.Items, into);
                }
            }
        }
    }

    public partial class TabControl
    {
        /// <summary>Moves the selection off the given tab.</summary>
        public void DeselectTab (int index)
        {
            if (index < 0 || index >= TabPages.Count || SelectedIndex != index)
                return;

            // WinForms moves to the next tab, wrapping to the previous one at the end, so that
            // deselecting never leaves the control with nothing selected.
            SelectedIndex = index + 1 < TabPages.Count ? index + 1 : Math.Max (0, index - 1);
        }

        /// <inheritdoc cref="DeselectTab(int)"/>
        public void DeselectTab (TabPage tabPage)
        {
            ArgumentNullException.ThrowIfNull (tabPage);
            DeselectTab (TabPages.IndexOf (tabPage));
        }

        /// <inheritdoc cref="DeselectTab(int)"/>
        public void DeselectTab (string tabPageName)
        {
            for (var i = 0; i < TabPages.Count; i++) {
                if (!string.Equals (TabPages[i].Name, tabPageName, StringComparison.OrdinalIgnoreCase))
                    continue;

                DeselectTab (i);
                return;
            }
        }

        /// <summary>Returns the page at the given index.</summary>
        public Control GetControl (int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative (index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual (index, TabPages.Count);

            return TabPages[index];
        }

        /// <summary>Raised when the right-to-left layout changes.</summary>
#pragma warning disable CS0067
        public event EventHandler? RightToLeftLayoutChanged;
#pragma warning restore CS0067
    }

    public partial class BindingContext
    {
        /// <summary>Gets whether this collection can be modified.</summary>
        public bool IsReadOnly => false;

        /// <summary>Returns whether a binding manager exists for the given source.</summary>
        public bool Contains (object dataSource) => Contains (dataSource, string.Empty);

        /// <inheritdoc cref="Contains(object)"/>
        public bool Contains (object dataSource, string? dataMember)
        {
            ArgumentNullException.ThrowIfNull (dataSource);
            return managers.ContainsKey ((dataSource, dataMember ?? string.Empty));
        }

        /// <summary>Moves a binding to a different context.</summary>
        public static void UpdateBinding (BindingContext newBindingContext, Binding binding)
        {
            ArgumentNullException.ThrowIfNull (binding);

            binding.BindingManagerBase = binding.DataSource is null || newBindingContext is null
                ? null
                : newBindingContext[binding.DataSource, binding.BindingMemberInfo.BindingMember];
        }

        /// <summary>Raised when a binding manager is added or removed.</summary>
#pragma warning disable CS0067
        public event CollectionChangeEventHandler? CollectionChanged;
#pragma warning restore CS0067
    }

    public partial class Label
    {
        /// <summary>Gets or sets how assistive technology is told about changes to this label.</summary>
        public AutomationLiveSetting LiveSetting { get; set; } = AutomationLiveSetting.Off;

        /// <summary>Gets or sets whether text is drawn through the compatible text renderer.</summary>
        public bool UseCompatibleTextRendering { get; set; }

        /// <summary>Gets the height the label needs for its current text.</summary>
        public virtual int PreferredHeight
            => (int)Math.Ceiling (TextMeasurer.MeasureText (Text ?? string.Empty, this).Height) + Padding.Vertical;

        /// <summary>Gets the width the label needs for its current text on one line.</summary>
        public virtual int PreferredWidth
            => (int)Math.Ceiling (TextMeasurer.MeasureText (Text ?? string.Empty, this).Width) + Padding.Horizontal;
    }

    /// <summary>How assistive technology is told about changes to a control's content.</summary>
    public enum AutomationLiveSetting
    {
        /// <summary>Changes are not announced.</summary>
        Off = 0,
        /// <summary>Changes are announced when the user is idle.</summary>
        Polite = 1,
        /// <summary>Changes are announced immediately.</summary>
        Assertive = 2,
    }

    public partial class TrackBar
    {
        /// <summary>Gets or sets whether the bar fills right to left when RightToLeft is set.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                RightToLeftLayoutChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool right_to_left_layout;

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Sets the minimum and maximum together.</summary>
        /// <remarks>Setting them one at a time can transiently invert the range and clamp Value;
        /// this applies both before anything reacts, which is the reason the method exists.</remarks>
        public void SetRange (int minValue, int maxValue)
        {
            if (minValue > maxValue)
                maxValue = minValue;

            Minimum = minValue;
            Maximum = maxValue;
        }

        /// <summary>Signals that initialization is starting.</summary>
        public void BeginInit () { }

        /// <summary>Signals that initialization has finished.</summary>
        public void EndInit () { }
    }

    public partial class DateTimePicker
    {
        /// <summary>Gets or sets the colour of days from adjacent months in the drop-down calendar.</summary>
        public Color CalendarTrailingForeColor { get; set; } = SystemColors.GrayText;

        /// <summary>Gets or sets which edge the drop-down calendar is aligned to.</summary>
        public LeftRightAlignment DropDownAlign { get; set; } = LeftRightAlignment.Left;

        /// <summary>Gets or sets whether the control lays out right to left when RightToLeft is set.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                RightToLeftLayoutChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool right_to_left_layout;

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Raised when the format changes.</summary>
#pragma warning disable CS0067
        public event EventHandler? FormatChanged;
#pragma warning restore CS0067
    }

    public partial class ToolTip
    {
        /// <summary>Gets or sets arbitrary data associated with this tooltip.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets whether the application draws the tooltip.</summary>
        public bool OwnerDraw { get; set; }

        /// <summary>Returns whether this tooltip can provide a tip for the given object.</summary>
        public bool CanExtend (object? target) => target is Control;

        // Owner-drawn tooltips are painted by the backends' own popup, which does not call back into
        // application code, so neither of these is raised yet.
#pragma warning disable CS0067
        /// <summary>Raised when an owner-drawn tooltip must be painted. Not raised by this layer yet.</summary>
        public event DrawToolTipEventHandler? Draw;

        /// <summary>Raised before a tooltip is shown. Not raised by this layer yet.</summary>
        public event PopupEventHandler? Popup;
#pragma warning restore CS0067
    }

    public partial class ImageList
    {
        /// <summary>Gets or sets arbitrary data associated with this image list.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the Win32 handle for the image list.</summary>
        /// <remarks>Zero, and <see cref="HandleCreated"/> is false to match: images are Skia bitmaps
        /// here, not an HIMAGELIST, and reporting a handle that is not one would break the Win32 call
        /// a caller would make next.</remarks>
        public IntPtr Handle => IntPtr.Zero;

        /// <inheritdoc cref="Handle"/>
        public bool HandleCreated => false;

        /// <summary>Raised when the underlying handle is recreated. Never raised; there is no handle.</summary>
#pragma warning disable CS0067
        public event EventHandler? RecreateHandle;
#pragma warning restore CS0067
    }

    public partial class DomainUpDown
    {
        /// <summary>Gets or sets whether the items are kept in alphabetical order.</summary>
        public bool Sorted { get; set; }

        /// <summary>Gets or sets whether moving past the last item wraps to the first.</summary>
        public bool Wrap { get; set; }
    }

    public partial class PrintPreviewControl
    {
        /// <summary>Gets or sets whether the preview scales to fit the control.</summary>
        public bool AutoZoom { get; set; } = true;

        /// <summary>Gets or sets how many pages are shown across.</summary>
        public int Columns { get; set; } = 1;

        /// <summary>Gets or sets how many pages are shown down.</summary>
        public int Rows { get; set; } = 1;

        /// <summary>Gets or sets whether the preview is drawn with anti-aliasing.</summary>
        public bool UseAntiAlias { get; set; }

        /// <summary>Gets or sets the first page shown.</summary>
        public int StartPage {
            get => start_page;
            set {
                var clamped = Math.Max (0, value);

                if (start_page == clamped)
                    return;

                start_page = clamped;
                StartPageChanged?.Invoke (this, EventArgs.Empty);
                InvalidatePreview ();
            }
        }

        private int start_page;

        /// <summary>Raised when <see cref="StartPage"/> changes.</summary>
        public event EventHandler? StartPageChanged;

        /// <summary>Discards the rendered preview so it is generated again.</summary>
        public void InvalidatePreview () => Invalidate ();
    }

    public partial class PrintDialog
    {
        /// <summary>Gets or sets whether the "current page" option is offered.</summary>
        public bool AllowCurrentPage { get; set; }

        /// <summary>Gets or sets whether the "print to file" box is ticked.</summary>
        public bool PrintToFile { get; set; }

        /// <summary>Gets or sets whether the help button is shown.</summary>
        /// <remarks>Stored: these four select between pages and buttons of the Win32 common print
        /// dialog, and the backends show their own printer chooser instead.</remarks>
        public bool ShowHelp { get; set; }

        /// <inheritdoc cref="ShowHelp"/>
        public bool ShowNetwork { get; set; } = true;

        /// <inheritdoc cref="ShowHelp"/>
        public bool UseEXDialog { get; set; }

        /// <summary>Returns every option to its default.</summary>
        public virtual void Reset ()
        {
            AllowCurrentPage = false;
            PrintToFile = false;
            ShowHelp = false;
            ShowNetwork = true;
            UseEXDialog = false;
        }
    }

    public partial class PageSetupDialog
    {
        /// <summary>Gets or sets whether margins are shown in millimetres rather than inches.</summary>
        public bool EnableMetric { get; set; } = true;

        /// <summary>Gets or sets the smallest margins the user may choose.</summary>
        public Margins MinMargins { get; set; }
            = new Margins (0, 0, 0, 0);

        /// <summary>Gets or sets whether the help button is shown.</summary>
        /// <remarks>Stored; see <see cref="PrintDialog.ShowHelp"/>.</remarks>
        public bool ShowHelp { get; set; }

        /// <inheritdoc cref="ShowHelp"/>
        public bool ShowNetwork { get; set; } = true;

        /// <summary>Returns every option to its default.</summary>
        public virtual void Reset ()
        {
            EnableMetric = true;
            ShowHelp = false;
            ShowNetwork = true;
            MinMargins = new Margins (0, 0, 0, 0);
        }
    }

    public partial class ToolStripPanelRow
    {
        /// <summary>Gets or sets the padding inside the row.</summary>
        public virtual Padding Padding { get; set; }

        /// <summary>Gets the area of the row its strips are laid out in.</summary>
        public Rectangle DisplayRectangle {
            get {
                var bounds = Bounds;
                return new Rectangle (
                    bounds.X + Padding.Left,
                    bounds.Y + Padding.Top,
                    Math.Max (0, bounds.Width - Padding.Horizontal),
                    Math.Max (0, bounds.Height - Padding.Vertical));
            }
        }

        /// <summary>Returns whether the given strip could be moved onto this row.</summary>
        public bool CanMove (ToolStrip toolStripToDrag)
            => toolStripToDrag is not null && !Controls.Contains (toolStripToDrag);
    }
}
