using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The ToolStrip hosted-editor surface (docs/winforms-gap-plan.md).
    //
    // ToolStripTextBox and ToolStripComboBox are facades: WinForms code sets a property on the item
    // and expects the hosted control to change. That is the whole contract, and ToolStripTextBox was
    // failing it -- Text was a private string on the item, so `box.Text = "x"` and `box.TextBox.Text`
    // disagreed and Cut/Paste/SelectAll acted on the value Text did not report. Both types now derive
    // from ToolStripControlHost, as they do upstream, and every member here reads or writes the
    // hosted control rather than a shadow copy of its state.
    //
    // Most of the text members are one line each because TextBoxBase now carries the editing surface;
    // before that pass there was nothing on the base to forward to.

    public partial class ToolStripControlHost
    {
        private AccessibleObject? accessibility_object;

        /// <summary>
        /// Puts the hosted control into the strip and moves it to wherever the strip laid this item out.
        /// </summary>
        /// <remarks>
        /// The hosting was the missing half of this type: it held a <see cref="Control"/> reference and
        /// forwarded properties to it, but never parented the control to anything or gave it a position, so
        /// the control was never displayed at all. What appeared instead was the item's Text, drawn by the
        /// strip's renderer -- a slider in a status strip came out as the literal words "kryptonSlider1" in
        /// the corner, which reads as a stray label rather than as an unhosted control. Every
        /// ToolStripControlHost was affected, ToolStripTextBox and ToolStripComboBox included.
        /// </remarks>
        public override void SetBounds (int x, int y, int width, int height,
            BoundsSpecified specified = BoundsSpecified.All)
        {
            base.SetBounds (x, y, width, height, specified);

            if (OwnerControl is not { } owner)
                return;

            if (!ReferenceEquals (Control.Parent, owner))
                owner.Controls.Add (Control);

            // The item's bounds are already in the strip's client coordinates, which is the space the
            // hosted control lives in too, so they transfer across unchanged.
            Control.Bounds = Bounds;
            Control.Visible = Visible;
        }

        /// <summary>
        /// The size the hosted editor wants: the size last assigned to <see cref="Size"/> (the resx value
        /// for a designer-placed <see cref="ToolStripTextBox"/> / <see cref="ToolStripComboBox"/>), else
        /// the hosted control's current size. The base <see cref="MenuItem.GetPreferredSize"/> measures
        /// the item's <em>Text</em>, which a control host does not draw -- so it returned only its padding
        /// and the strip squeezed the editor down to nothing.
        /// </summary>
        public override Size GetPreferredSize (Size proposedSize)
        {
            var want = PreferredSizeOverride;

            if (want.Width <= 0)
                want.Width = Control.Width > 0 ? Control.Width : 100;   // WinForms' default editor width
            if (want.Height <= 0)
                want.Height = Control.Height > 0 ? Control.Height : Control.PreferredSize.Height;

            // The strip's layout engine adds Margin around whatever this returns, so it is not folded
            // in here (that would double it) -- unlike the text-measuring renderers, which bake in
            // Padding because a drawn item has no hosted control to carry its own.
            return want;
        }

        /// <summary>Gets or sets whether the hosted control causes validation when it receives focus.</summary>
        public bool CausesValidation {
            get => Control.CausesValidation;
            set => Control.CausesValidation = value;
        }

        /// <summary>Gets or sets how the hosted control is aligned within the space the item is given.</summary>
        public ContentAlignment ControlAlign { get; set; } = ContentAlignment.MiddleCenter;

        /// <summary>Gets whether the hosted control currently has input focus.</summary>
        public bool Focused => Control.Focused;

        /// <summary>Gets the site of the hosted control.</summary>
        public System.ComponentModel.ISite? Site {
            get => Control.Site;
            set => Control.Site = value;
        }

        /// <summary>Gets the accessible object, which describes the hosted control rather than the item.</summary>
        public new AccessibleObject AccessibilityObject
            => accessibility_object ??= new ToolStripHostedControlAccessibleObject (Control, this);

        /// <summary>Gives the hosted control the input focus.</summary>
        public void Focus () => Control.Focus ();

        /// <summary>Raised when the hosted control receives focus.</summary>
        public event EventHandler? Enter { add => Control.Enter += value; remove => Control.Enter -= value; }

        /// <summary>Raised when the hosted control loses focus.</summary>
        public event EventHandler? Leave { add => Control.Leave += value; remove => Control.Leave -= value; }

        /// <summary>Raised when a character key is pressed while the hosted control has focus.</summary>
        public event KeyPressEventHandler? KeyPress { add => Control.KeyPress += value; remove => Control.KeyPress -= value; }

        /// <summary>Raised when the hosted control finishes validating.</summary>
        public event EventHandler? Validated { add => Control.Validated += value; remove => Control.Validated -= value; }

        /// <summary>Raised while the hosted control is validating.</summary>
        public event System.ComponentModel.CancelEventHandler? Validating {
            add => Control.Validating += value;
            remove => Control.Validating -= value;
        }
    }

    /// <summary>Exposes a <see cref="ToolStripControlHost"/>'s hosted control to accessibility clients.</summary>
    public class ToolStripHostedControlAccessibleObject : AccessibleObject
    {
        private readonly Control control;
        private readonly ToolStripControlHost? host;

        /// <summary>Initializes a new instance of the <see cref="ToolStripHostedControlAccessibleObject"/> class.</summary>
        public ToolStripHostedControlAccessibleObject (Control toolStripHostedControl, ToolStripControlHost? toolStripControlHost)
        {
            control = toolStripHostedControl;
            host = toolStripControlHost;
        }

        /// <summary>Gets the name reported to assistive technology.</summary>
        public override string? Name => host?.AccessibleName ?? control.AccessibleName ?? control.Text;

        /// <summary>Gets the role reported to assistive technology.</summary>
        public override AccessibleRole Role
            => host?.AccessibleRole is { } role and not AccessibleRole.Default ? role : AccessibleRole.Client;

        /// <summary>Gets the screen bounds of the hosted control.</summary>
        public override Rectangle Bounds => control.Bounds;
    }

    public partial class ToolStripTextBox
    {
        /// <summary>Gets or sets the alignment of the text in the hosted text box.</summary>
        public HorizontalAlignment TextBoxTextAlign {
            get => TextBox.TextAlign;
            set {
                if (TextBox.TextAlign == value)
                    return;

                TextBox.TextAlign = value;
                TextBoxTextAlignChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="TextBoxTextAlign"/> changes.</summary>
        public event EventHandler? TextBoxTextAlignChanged;

        /// <summary>Gets or sets the border drawn around the hosted text box.</summary>
        public BorderStyle BorderStyle {
            get => TextBox.BorderStyle;
            set => TextBox.BorderStyle = value;
        }

        /// <summary>Gets or sets whether the hosted text box holds more than one line.</summary>
        public bool Multiline {
            get => TextBox.Multiline;
            set => TextBox.Multiline = value;
        }

        /// <summary>Gets or sets whether text wraps at the hosted text box's edge.</summary>
        public bool WordWrap {
            get => TextBox.WordWrap;
            set => TextBox.WordWrap = value;
        }

        /// <summary>Gets or sets whether the selection stays visible when focus is lost.</summary>
        public bool HideSelection {
            get => TextBox.HideSelection;
            set => TextBox.HideSelection = value;
        }

        /// <summary>Gets or sets whether the text has been edited since it was last set.</summary>
        public bool Modified {
            get => TextBox.Modified;
            set => TextBox.Modified = value;
        }

        /// <summary>Gets or sets whether the standard editing shortcuts are enabled.</summary>
        public bool ShortcutsEnabled {
            get => TextBox.ShortcutsEnabled;
            set => TextBox.ShortcutsEnabled = value;
        }

        /// <summary>Gets or sets the casing applied to typed characters.</summary>
        public CharacterCasing CharacterCasing {
            get => TextBox.CharacterCasing;
            set => TextBox.CharacterCasing = value;
        }

        /// <summary>Gets or sets the auto-complete behaviour of the hosted text box.</summary>
        public AutoCompleteMode AutoCompleteMode {
            get => TextBox.AutoCompleteMode;
            set => TextBox.AutoCompleteMode = value;
        }

        /// <summary>Gets or sets where auto-complete candidates come from.</summary>
        public AutoCompleteSource AutoCompleteSource {
            get => TextBox.AutoCompleteSource;
            set => TextBox.AutoCompleteSource = value;
        }

        /// <summary>Gets or sets the custom auto-complete candidates.</summary>
        public AutoCompleteStringCollection AutoCompleteCustomSource {
            get => TextBox.AutoCompleteCustomSource;
            set => TextBox.AutoCompleteCustomSource = value;
        }

        /// <summary>Gets or sets the lines of text in the hosted text box.</summary>
        public string[] Lines {
            get => TextBox.Lines;
            set => TextBox.Lines = value;
        }

        /// <summary>Gets or sets the start of the selection.</summary>
        public int SelectionStart {
            get => TextBox.SelectionStart;
            set => TextBox.SelectionStart = value;
        }

        /// <summary>Gets or sets the length of the selection.</summary>
        public int SelectionLength {
            get => TextBox.SelectionLength;
            set => TextBox.SelectionLength = value;
        }

        /// <summary>Gets or sets the selected text.</summary>
        public string SelectedText {
            get => TextBox.SelectedText;
            set => TextBox.SelectedText = value;
        }

        /// <summary>Gets the number of characters in the hosted text box.</summary>
        public int TextLength => TextBox.TextLength;

        /// <summary>Gets whether there is an edit <see cref="ToolStripTextBox.Undo"/> would reverse.</summary>
        public bool CanUndo => TextBox.CanUndo;

        /// <summary>Appends text to the hosted text box.</summary>
        public void AppendText (string text) => TextBox.AppendText (text);

        /// <summary>Clears the hosted text box.</summary>
        public void Clear () => TextBox.Clear ();

        /// <summary>Clears the undo buffer.</summary>
        public void ClearUndo () => TextBox.ClearUndo ();

        /// <summary>Clears the selection.</summary>
        public void DeselectAll () => TextBox.DeselectAll ();

        /// <summary>Scrolls the hosted text box so the caret is visible.</summary>
        public void ScrollToCaret () => TextBox.ScrollToCaret ();

        /// <summary>Gets the character nearest the given point.</summary>
        public char GetCharFromPosition (Point pt) => TextBox.GetCharFromPosition (pt);

        /// <summary>Gets the index of the character nearest the given point.</summary>
        public int GetCharIndexFromPosition (Point pt) => TextBox.GetCharIndexFromPosition (pt);

        /// <summary>Gets the index the given line starts at.</summary>
        public int GetFirstCharIndexFromLine (int lineNumber) => TextBox.GetFirstCharIndexFromLine (lineNumber);

        /// <summary>Gets the index the line containing the caret starts at.</summary>
        public int GetFirstCharIndexOfCurrentLine () => TextBox.GetFirstCharIndexOfCurrentLine ();

        /// <summary>Gets the line the given character index falls on.</summary>
        public int GetLineFromCharIndex (int index) => TextBox.GetLineFromCharIndex (index);

        /// <summary>Gets the location of the character at the given index.</summary>
        public Point GetPositionFromCharIndex (int index) => TextBox.GetPositionFromCharIndex (index);

        /// <summary>Raised when the hosted text box's AcceptsTab changes.</summary>
        public event EventHandler? AcceptsTabChanged { add => TextBox.AcceptsTabChanged += value; remove => TextBox.AcceptsTabChanged -= value; }

        /// <summary>Raised when the hosted text box's BorderStyle changes.</summary>
        public event EventHandler? BorderStyleChanged { add => TextBox.BorderStyleChanged += value; remove => TextBox.BorderStyleChanged -= value; }

        /// <summary>Raised when the hosted text box's HideSelection changes.</summary>
        public event EventHandler? HideSelectionChanged { add => TextBox.HideSelectionChanged += value; remove => TextBox.HideSelectionChanged -= value; }

        /// <summary>Raised when the hosted text box's Modified changes.</summary>
        public event EventHandler? ModifiedChanged { add => TextBox.ModifiedChanged += value; remove => TextBox.ModifiedChanged -= value; }

        /// <summary>Raised when the hosted text box's Multiline changes.</summary>
        public event EventHandler? MultilineChanged { add => TextBox.MultilineChanged += value; remove => TextBox.MultilineChanged -= value; }

        /// <summary>Raised when the hosted text box's ReadOnly changes.</summary>
        public event EventHandler? ReadOnlyChanged { add => TextBox.ReadOnlyChanged += value; remove => TextBox.ReadOnlyChanged -= value; }
    }

    public partial class ToolStripComboBox
    {
        /// <summary>Gets or sets the selected item.</summary>
        public object? SelectedItem {
            get => ComboBox.SelectedItem;
            set => ComboBox.SelectedItem = value;
        }

        /// <summary>Gets or sets whether the drop-down portion is shown.</summary>
        public bool DroppedDown {
            get => ComboBox.DroppedDown;
            set => ComboBox.DroppedDown = value;
        }

        /// <summary>Gets or sets the height of the drop-down portion.</summary>
        public int DropDownHeight {
            get => ComboBox.DropDownHeight;
            set => ComboBox.DropDownHeight = value;
        }

        /// <summary>Gets or sets the number of items shown before the drop-down scrolls.</summary>
        public int MaxDropDownItems {
            get => ComboBox.MaxDropDownItems;
            set => ComboBox.MaxDropDownItems = value;
        }

        /// <summary>Gets or sets whether the drop-down resizes to avoid showing a partial item.</summary>
        public bool IntegralHeight {
            get => ComboBox.IntegralHeight;
            set => ComboBox.IntegralHeight = value;
        }

        /// <summary>Gets or sets whether the items are sorted.</summary>
        public bool Sorted {
            get => ComboBox.Sorted;
            set => ComboBox.Sorted = value;
        }

        /// <summary>Gets or sets the maximum number of characters that can be typed.</summary>
        public int MaxLength {
            get => ComboBox.MaxLength;
            set => ComboBox.MaxLength = value;
        }

        /// <summary>Gets or sets the flat-style appearance of the hosted combo box.</summary>
        public FlatStyle FlatStyle {
            get => ComboBox.FlatStyle;
            set => ComboBox.FlatStyle = value;
        }

        /// <summary>Gets or sets the start of the selection in the editable portion.</summary>
        public int SelectionStart {
            get => ComboBox.SelectionStart;
            set => ComboBox.SelectionStart = value;
        }

        /// <summary>Gets or sets the length of the selection in the editable portion.</summary>
        public int SelectionLength {
            get => ComboBox.SelectionLength;
            set => ComboBox.SelectionLength = value;
        }

        /// <summary>Gets or sets the selected text in the editable portion.</summary>
        public string SelectedText {
            get => ComboBox.SelectedText;
            set => ComboBox.SelectedText = value;
        }

        /// <summary>Gets or sets the auto-complete behaviour of the hosted combo box.</summary>
        public AutoCompleteMode AutoCompleteMode {
            get => ComboBox.AutoCompleteMode;
            set => ComboBox.AutoCompleteMode = value;
        }

        /// <summary>Gets or sets where auto-complete candidates come from.</summary>
        public AutoCompleteSource AutoCompleteSource {
            get => ComboBox.AutoCompleteSource;
            set => ComboBox.AutoCompleteSource = value;
        }

        /// <summary>Gets or sets the custom auto-complete candidates.</summary>
        public AutoCompleteStringCollection AutoCompleteCustomSource {
            get => ComboBox.AutoCompleteCustomSource;
            set => ComboBox.AutoCompleteCustomSource = value;
        }

        /// <summary>Suspends painting while items are added in bulk.</summary>
        public void BeginUpdate () => ComboBox.BeginUpdate ();

        /// <summary>Resumes painting after <see cref="BeginUpdate"/>.</summary>
        public void EndUpdate () => ComboBox.EndUpdate ();

        /// <summary>Selects all text in the editable portion.</summary>
        public void SelectAll () => ComboBox.Select (0, ComboBox.Text.Length);

        /// <summary>Finds the first item that starts with the given string.</summary>
        public int FindString (string s) => ComboBox.FindString (s);

        /// <inheritdoc cref="FindString(string)"/>
        public int FindString (string s, int startIndex) => ComboBox.FindString (s, startIndex);

        /// <summary>Finds the first item that exactly equals the given string.</summary>
        public int FindStringExact (string s) => ComboBox.FindStringExact (s);

        /// <inheritdoc cref="FindStringExact(string)"/>
        public int FindStringExact (string s, int startIndex) => ComboBox.FindStringExact (s, startIndex);

        /// <summary>Gets the height of the item at the given index.</summary>
        public int GetItemHeight (int index) => ComboBox.GetItemHeight (index);

        /// <summary>Raised when the drop-down portion is shown.</summary>
        public event EventHandler? DropDown { add => ComboBox.DropDown += value; remove => ComboBox.DropDown -= value; }

        /// <summary>Raised when the drop-down portion closes.</summary>
        public event EventHandler? DropDownClosed { add => ComboBox.DropDownClosed += value; remove => ComboBox.DropDownClosed -= value; }

        /// <summary>Raised when <see cref="ToolStripComboBox.DropDownStyle"/> changes.</summary>
        public event EventHandler? DropDownStyleChanged { add => ComboBox.DropDownStyleChanged += value; remove => ComboBox.DropDownStyleChanged -= value; }

        /// <summary>Raised when the text changes as a result of the user editing it.</summary>
        public event EventHandler? TextUpdate { add => ComboBox.TextUpdate += value; remove => ComboBox.TextUpdate -= value; }
    }
}
