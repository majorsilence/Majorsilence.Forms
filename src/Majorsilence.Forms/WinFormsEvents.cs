using System;
using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    // WinForms enums, event-data classes and event-handler delegates, generated from the real
    // System.Windows.Forms reference assembly (see docs/winforms-gap-plan.md, item 2).
    //
    // These are not optional scaffolding. A control declaring
    //     public event DataGridViewCellEventHandler CellClick;
    // does not compile without the delegate, and *.Designer.cs wires handlers by delegate type -- so
    // their absence broke exactly the generated files a migration cannot hand-edit. Shapes and enum
    // values come from reflection over the reference assembly rather than from memory, for the same
    // reason item 1's values had to: transcription is how those went wrong.
    //
    // A handful of event-data classes and delegates are deliberately NOT here, because they carry
    // types this layer does not have (ToolStripContentPanel, HtmlElement, ListViewItem.ListViewSubItem)
    // or derive from a base whose constructor cannot be satisfied (the StatusBar* pair). Faking those
    // would trade a compile error for a wrong runtime shape; the API gap audit
    // (tools/Majorsilence.Forms.ApiDiff) keeps reporting them until the real types exist.

    /// <summary>Specifies the accessible navigation. Matches <c>System.Windows.Forms.AccessibleNavigation</c>, including its numeric values.</summary>
    public enum AccessibleNavigation
    {
        /// <summary>down.</summary>
        Down = 2,
        /// <summary>first child.</summary>
        FirstChild = 7,
        /// <summary>last child.</summary>
        LastChild = 8,
        /// <summary>left.</summary>
        Left = 3,
        /// <summary>next.</summary>
        Next = 5,
        /// <summary>previous.</summary>
        Previous = 6,
        /// <summary>right.</summary>
        Right = 4,
        /// <summary>up.</summary>
        Up = 1,
    }

    /// <summary>Specifies the accessible selection. Matches <c>System.Windows.Forms.AccessibleSelection</c>, including its numeric values.</summary>
    [Flags]
    public enum AccessibleSelection
    {
        /// <summary>none.</summary>
        None = 0,
        /// <summary>take focus.</summary>
        TakeFocus = 1,
        /// <summary>take selection.</summary>
        TakeSelection = 2,
        /// <summary>extend selection.</summary>
        ExtendSelection = 4,
        /// <summary>add selection.</summary>
        AddSelection = 8,
        /// <summary>remove selection.</summary>
        RemoveSelection = 16,
    }

    /// <summary>Specifies the accessible states. Matches <c>System.Windows.Forms.AccessibleStates</c>, including its numeric values.</summary>
    [Flags]
    public enum AccessibleStates
    {
        /// <summary>none.</summary>
        None = 0,
        /// <summary>unavailable.</summary>
        Unavailable = 1,
        /// <summary>selected.</summary>
        Selected = 2,
        /// <summary>focused.</summary>
        Focused = 4,
        /// <summary>pressed.</summary>
        Pressed = 8,
        /// <summary>checked.</summary>
        Checked = 16,
        /// <summary>mixed.</summary>
        Mixed = 32,
        /// <summary>indeterminate.</summary>
        Indeterminate = 32,
        /// <summary>read only.</summary>
        ReadOnly = 64,
        /// <summary>hot tracked.</summary>
        HotTracked = 128,
        /// <summary>default.</summary>
        Default = 0x100,
        /// <summary>expanded.</summary>
        Expanded = 0x200,
        /// <summary>collapsed.</summary>
        Collapsed = 0x400,
        /// <summary>busy.</summary>
        Busy = 0x800,
        /// <summary>floating.</summary>
        Floating = 0x1000,
        /// <summary>marqueed.</summary>
        Marqueed = 0x2000,
        /// <summary>animated.</summary>
        Animated = 0x4000,
        /// <summary>invisible.</summary>
        Invisible = 0x8000,
        /// <summary>offscreen.</summary>
        Offscreen = 0x10000,
        /// <summary>sizeable.</summary>
        Sizeable = 0x20000,
        /// <summary>moveable.</summary>
        Moveable = 0x40000,
        /// <summary>self voicing.</summary>
        SelfVoicing = 0x80000,
        /// <summary>focusable.</summary>
        Focusable = 0x100000,
        /// <summary>selectable.</summary>
        Selectable = 0x200000,
        /// <summary>linked.</summary>
        Linked = 0x400000,
        /// <summary>traversed.</summary>
        Traversed = 0x800000,
        /// <summary>multi selectable.</summary>
        MultiSelectable = 0x1000000,
        /// <summary>ext selectable.</summary>
        ExtSelectable = 0x2000000,
        /// <summary>alert low.</summary>
        AlertLow = 0x4000000,
        /// <summary>alert medium.</summary>
        AlertMedium = 0x8000000,
        /// <summary>alert high.</summary>
        AlertHigh = 0x10000000,
        /// <summary>protected.</summary>
        Protected = 0x20000000,
        /// <summary>has popup.</summary>
        HasPopup = 0x40000000,
        /// <summary>valid.</summary>
        Valid = 0x3FFFFFFF,
    }

    /// <summary>Specifies the arrange direction. Matches <c>System.Windows.Forms.ArrangeDirection</c>, including its numeric values.</summary>
    [Flags]
    public enum ArrangeDirection
    {
        /// <summary>down.</summary>
        Down = 4,
        /// <summary>left.</summary>
        Left = 0,
        /// <summary>right.</summary>
        Right = 0,
        /// <summary>up.</summary>
        Up = 4,
    }

    /// <summary>Specifies the arrange starting position. Matches <c>System.Windows.Forms.ArrangeStartingPosition</c>, including its numeric values.</summary>
    [Flags]
    public enum ArrangeStartingPosition
    {
        /// <summary>bottom left.</summary>
        BottomLeft = 0,
        /// <summary>bottom right.</summary>
        BottomRight = 1,
        /// <summary>hide.</summary>
        Hide = 8,
        /// <summary>top left.</summary>
        TopLeft = 2,
        /// <summary>top right.</summary>
        TopRight = 3,
    }

    /// <summary>Specifies the binding complete state. Matches <c>System.Windows.Forms.BindingCompleteState</c>, including its numeric values.</summary>
    public enum BindingCompleteState
    {
        /// <summary>success.</summary>
        Success = 0,
        /// <summary>data error.</summary>
        DataError = 1,
        /// <summary>exception.</summary>
        Exception = 2,
    }

    /// <summary>Specifies the boot mode. Matches <c>System.Windows.Forms.BootMode</c>, including its numeric values.</summary>
    public enum BootMode
    {
        /// <summary>normal.</summary>
        Normal = 0,
        /// <summary>fail safe.</summary>
        FailSafe = 1,
        /// <summary>fail safe with network.</summary>
        FailSafeWithNetwork = 2,
    }

    /// <summary>Specifies the border3 d side. Matches <c>System.Windows.Forms.Border3DSide</c>, including its numeric values.</summary>
    [Flags]
    public enum Border3DSide
    {
        /// <summary>left.</summary>
        Left = 1,
        /// <summary>top.</summary>
        Top = 2,
        /// <summary>right.</summary>
        Right = 4,
        /// <summary>bottom.</summary>
        Bottom = 8,
        /// <summary>middle.</summary>
        Middle = 0x800,
        /// <summary>all.</summary>
        All = 0x80F,
    }

    /// <summary>Specifies the caption button. Matches <c>System.Windows.Forms.CaptionButton</c>, including its numeric values.</summary>
    public enum CaptionButton
    {
        /// <summary>close.</summary>
        Close = 0,
        /// <summary>help.</summary>
        Help = 4,
        /// <summary>maximize.</summary>
        Maximize = 2,
        /// <summary>minimize.</summary>
        Minimize = 1,
        /// <summary>restore.</summary>
        Restore = 3,
    }

    /// <summary>Specifies the control update mode. Matches <c>System.Windows.Forms.ControlUpdateMode</c>, including its numeric values.</summary>
    public enum ControlUpdateMode
    {
        /// <summary>on property changed.</summary>
        OnPropertyChanged = 0,
        /// <summary>never.</summary>
        Never = 1,
    }

    /// <summary>Specifies the data grid parent rows label style. Matches <c>System.Windows.Forms.DataGridParentRowsLabelStyle</c>, including its numeric values.</summary>
    public enum DataGridParentRowsLabelStyle
    {
        /// <summary>none.</summary>
        None = 0,
        /// <summary>table name.</summary>
        TableName = 1,
        /// <summary>column name.</summary>
        ColumnName = 2,
        /// <summary>both.</summary>
        Both = 3,
    }

    /// <summary>Specifies the data grid view header border style. Matches <c>System.Windows.Forms.DataGridViewHeaderBorderStyle</c>, including its numeric values.</summary>
    public enum DataGridViewHeaderBorderStyle
    {
        /// <summary>custom.</summary>
        Custom = 0,
        /// <summary>single.</summary>
        Single = 1,
        /// <summary>raised.</summary>
        Raised = 2,
        /// <summary>sunken.</summary>
        Sunken = 3,
        /// <summary>none.</summary>
        None = 4,
    }

    /// <summary>Specifies the docking behavior. Matches <c>System.Windows.Forms.DockingBehavior</c>, including its numeric values.</summary>
    public enum DockingBehavior
    {
        /// <summary>never.</summary>
        Never = 0,
        /// <summary>ask.</summary>
        Ask = 1,
        /// <summary>auto dock.</summary>
        AutoDock = 2,
    }

    /// <summary>Specifies the drop image type. Matches <c>System.Windows.Forms.DropImageType</c>, including its numeric values.</summary>
    public enum DropImageType
    {
        /// <summary>invalid.</summary>
        Invalid = -1,
        /// <summary>none.</summary>
        None = 0,
        /// <summary>copy.</summary>
        Copy = 1,
        /// <summary>move.</summary>
        Move = 2,
        /// <summary>link.</summary>
        Link = 4,
        /// <summary>label.</summary>
        Label = 6,
        /// <summary>warning.</summary>
        Warning = 7,
        /// <summary>no image.</summary>
        NoImage = 8,
    }

    /// <summary>Specifies the frame style. Matches <c>System.Windows.Forms.FrameStyle</c>, including its numeric values.</summary>
    public enum FrameStyle
    {
        /// <summary>dashed.</summary>
        Dashed = 0,
        /// <summary>thick.</summary>
        Thick = 1,
    }

    /// <summary>Specifies the get child at point skip. Matches <c>System.Windows.Forms.GetChildAtPointSkip</c>, including its numeric values.</summary>
    [Flags]
    public enum GetChildAtPointSkip
    {
        /// <summary>none.</summary>
        None = 0,
        /// <summary>invisible.</summary>
        Invisible = 1,
        /// <summary>disabled.</summary>
        Disabled = 2,
        /// <summary>transparent.</summary>
        Transparent = 4,
    }

    /// <summary>Specifies the grid item type. Matches <c>System.Windows.Forms.GridItemType</c>, including its numeric values.</summary>
    public enum GridItemType
    {
        /// <summary>property.</summary>
        Property = 0,
        /// <summary>category.</summary>
        Category = 1,
        /// <summary>array value.</summary>
        ArrayValue = 2,
        /// <summary>root.</summary>
        Root = 3,
    }

    /// <summary>Specifies the html element insertion orientation. Matches <c>System.Windows.Forms.HtmlElementInsertionOrientation</c>, including its numeric values.</summary>
    public enum HtmlElementInsertionOrientation
    {
        /// <summary>before begin.</summary>
        BeforeBegin = 0,
        /// <summary>after begin.</summary>
        AfterBegin = 1,
        /// <summary>before end.</summary>
        BeforeEnd = 2,
        /// <summary>after end.</summary>
        AfterEnd = 3,
    }

    /// <summary>Specifies the insert key mode. Matches <c>System.Windows.Forms.InsertKeyMode</c>, including its numeric values.</summary>
    public enum InsertKeyMode
    {
        /// <summary>default.</summary>
        Default = 0,
        /// <summary>insert.</summary>
        Insert = 1,
        /// <summary>overwrite.</summary>
        Overwrite = 2,
    }

    /// <summary>Specifies the item bounds portion. Matches <c>System.Windows.Forms.ItemBoundsPortion</c>, including its numeric values.</summary>
    public enum ItemBoundsPortion
    {
        /// <summary>entire.</summary>
        Entire = 0,
        /// <summary>icon.</summary>
        Icon = 1,
        /// <summary>label.</summary>
        Label = 2,
        /// <summary>item only.</summary>
        ItemOnly = 3,
    }

    /// <summary>Specifies the list view alignment. Matches <c>System.Windows.Forms.ListViewAlignment</c>, including its numeric values.</summary>
    public enum ListViewAlignment
    {
        /// <summary>default.</summary>
        Default = 0,
        /// <summary>top.</summary>
        Top = 2,
        /// <summary>left.</summary>
        Left = 1,
        /// <summary>snap to grid.</summary>
        SnapToGrid = 5,
    }

    /// <summary>Specifies the list view group collapsed state. Matches <c>System.Windows.Forms.ListViewGroupCollapsedState</c>, including its numeric values.</summary>
    public enum ListViewGroupCollapsedState
    {
        /// <summary>default.</summary>
        Default = 0,
        /// <summary>expanded.</summary>
        Expanded = 1,
        /// <summary>collapsed.</summary>
        Collapsed = 2,
    }

    /// <summary>Specifies the list view hit test locations. Matches <c>System.Windows.Forms.ListViewHitTestLocations</c>, including its numeric values.</summary>
    [Flags]
    public enum ListViewHitTestLocations
    {
        /// <summary>none.</summary>
        None = 1,
        /// <summary>above client area.</summary>
        AboveClientArea = 0x100,
        /// <summary>below client area.</summary>
        BelowClientArea = 16,
        /// <summary>left of client area.</summary>
        LeftOfClientArea = 64,
        /// <summary>right of client area.</summary>
        RightOfClientArea = 32,
        /// <summary>image.</summary>
        Image = 2,
        /// <summary>state image.</summary>
        StateImage = 0x200,
        /// <summary>label.</summary>
        Label = 4,
    }

    /// <summary>Specifies the list view item states. Matches <c>System.Windows.Forms.ListViewItemStates</c>, including its numeric values.</summary>
    [Flags]
    public enum ListViewItemStates
    {
        /// <summary>checked.</summary>
        Checked = 8,
        /// <summary>default.</summary>
        Default = 32,
        /// <summary>focused.</summary>
        Focused = 16,
        /// <summary>grayed.</summary>
        Grayed = 2,
        /// <summary>hot.</summary>
        Hot = 64,
        /// <summary>indeterminate.</summary>
        Indeterminate = 0x100,
        /// <summary>marked.</summary>
        Marked = 128,
        /// <summary>selected.</summary>
        Selected = 1,
        /// <summary>show keyboard cues.</summary>
        ShowKeyboardCues = 0x200,
    }

    /// <summary>Specifies the power state. Matches <c>System.Windows.Forms.PowerState</c>, including its numeric values.</summary>
    public enum PowerState
    {
        /// <summary>suspend.</summary>
        Suspend = 0,
        /// <summary>hibernate.</summary>
        Hibernate = 1,
    }

    /// <summary>Specifies the pre process control state. Matches <c>System.Windows.Forms.PreProcessControlState</c>, including its numeric values.</summary>
    public enum PreProcessControlState
    {
        /// <summary>message processed.</summary>
        MessageProcessed = 0,
        /// <summary>message needed.</summary>
        MessageNeeded = 1,
        /// <summary>message not needed.</summary>
        MessageNotNeeded = 2,
    }

    /// <summary>Specifies the rich text box language options. Matches <c>System.Windows.Forms.RichTextBoxLanguageOptions</c>, including its numeric values.</summary>
    [Flags]
    public enum RichTextBoxLanguageOptions
    {
        /// <summary>auto font.</summary>
        AutoFont = 2,
        /// <summary>auto font size adjust.</summary>
        AutoFontSizeAdjust = 16,
        /// <summary>auto keyboard.</summary>
        AutoKeyboard = 1,
        /// <summary>dual font.</summary>
        DualFont = 128,
        /// <summary>ime always send notify.</summary>
        ImeAlwaysSendNotify = 8,
        /// <summary>ime cancel complete.</summary>
        ImeCancelComplete = 4,
        /// <summary>u i fonts.</summary>
        UIFonts = 32,
    }

    /// <summary>Specifies the rich text box selection attribute. Matches <c>System.Windows.Forms.RichTextBoxSelectionAttribute</c>, including its numeric values.</summary>
    public enum RichTextBoxSelectionAttribute
    {
        /// <summary>mixed.</summary>
        Mixed = -1,
        /// <summary>none.</summary>
        None = 0,
        /// <summary>all.</summary>
        All = 1,
    }

    /// <summary>Specifies the rich text box selection types. Matches <c>System.Windows.Forms.RichTextBoxSelectionTypes</c>, including its numeric values.</summary>
    [Flags]
    public enum RichTextBoxSelectionTypes
    {
        /// <summary>empty.</summary>
        Empty = 0,
        /// <summary>text.</summary>
        Text = 1,
        /// <summary>object.</summary>
        Object = 2,
        /// <summary>multi char.</summary>
        MultiChar = 4,
        /// <summary>multi object.</summary>
        MultiObject = 8,
    }

    /// <summary>Specifies the rich text box word punctuations. Matches <c>System.Windows.Forms.RichTextBoxWordPunctuations</c>, including its numeric values.</summary>
    public enum RichTextBoxWordPunctuations
    {
        /// <summary>level1.</summary>
        Level1 = 128,
        /// <summary>level2.</summary>
        Level2 = 256,
        /// <summary>custom.</summary>
        Custom = 512,
        /// <summary>all.</summary>
        All = 896,
    }

    /// <summary>Specifies the screen capture mode. Matches <c>System.Windows.Forms.ScreenCaptureMode</c>, including its numeric values.</summary>
    public enum ScreenCaptureMode
    {
        /// <summary>allow.</summary>
        Allow = 0,
        /// <summary>hide content.</summary>
        HideContent = 1,
        /// <summary>hide window.</summary>
        HideWindow = 2,
    }

    /// <summary>Specifies the search direction hint. Matches <c>System.Windows.Forms.SearchDirectionHint</c>, including its numeric values.</summary>
    public enum SearchDirectionHint
    {
        /// <summary>up.</summary>
        Up = 38,
        /// <summary>down.</summary>
        Down = 40,
        /// <summary>left.</summary>
        Left = 37,
        /// <summary>right.</summary>
        Right = 39,
    }

    /// <summary>Specifies the security i d type. Matches <c>System.Windows.Forms.SecurityIDType</c>, including its numeric values.</summary>
    public enum SecurityIDType
    {
        /// <summary>user.</summary>
        User = 1,
        /// <summary>group.</summary>
        Group = 2,
        /// <summary>domain.</summary>
        Domain = 3,
        /// <summary>alias.</summary>
        Alias = 4,
        /// <summary>well known group.</summary>
        WellKnownGroup = 5,
        /// <summary>deleted account.</summary>
        DeletedAccount = 6,
        /// <summary>invalid.</summary>
        Invalid = 7,
        /// <summary>unknown.</summary>
        Unknown = 8,
        /// <summary>computer.</summary>
        Computer = 9,
    }

    /// <summary>Specifies the shortcut. Matches <c>System.Windows.Forms.Shortcut</c>, including its numeric values.</summary>
    public enum Shortcut
    {
        /// <summary>none.</summary>
        None = 0,
        /// <summary>ctrl a.</summary>
        CtrlA = 131137,
        /// <summary>ctrl b.</summary>
        CtrlB = 131138,
        /// <summary>ctrl c.</summary>
        CtrlC = 131139,
        /// <summary>ctrl d.</summary>
        CtrlD = 131140,
        /// <summary>ctrl e.</summary>
        CtrlE = 131141,
        /// <summary>ctrl f.</summary>
        CtrlF = 131142,
        /// <summary>ctrl g.</summary>
        CtrlG = 131143,
        /// <summary>ctrl h.</summary>
        CtrlH = 131144,
        /// <summary>ctrl i.</summary>
        CtrlI = 131145,
        /// <summary>ctrl j.</summary>
        CtrlJ = 131146,
        /// <summary>ctrl k.</summary>
        CtrlK = 131147,
        /// <summary>ctrl l.</summary>
        CtrlL = 131148,
        /// <summary>ctrl m.</summary>
        CtrlM = 131149,
        /// <summary>ctrl n.</summary>
        CtrlN = 131150,
        /// <summary>ctrl o.</summary>
        CtrlO = 131151,
        /// <summary>ctrl p.</summary>
        CtrlP = 131152,
        /// <summary>ctrl q.</summary>
        CtrlQ = 131153,
        /// <summary>ctrl r.</summary>
        CtrlR = 131154,
        /// <summary>ctrl s.</summary>
        CtrlS = 131155,
        /// <summary>ctrl t.</summary>
        CtrlT = 131156,
        /// <summary>ctrl u.</summary>
        CtrlU = 131157,
        /// <summary>ctrl v.</summary>
        CtrlV = 131158,
        /// <summary>ctrl w.</summary>
        CtrlW = 131159,
        /// <summary>ctrl x.</summary>
        CtrlX = 131160,
        /// <summary>ctrl y.</summary>
        CtrlY = 131161,
        /// <summary>ctrl z.</summary>
        CtrlZ = 131162,
        /// <summary>ctrl shift a.</summary>
        CtrlShiftA = 196673,
        /// <summary>ctrl shift b.</summary>
        CtrlShiftB = 196674,
        /// <summary>ctrl shift c.</summary>
        CtrlShiftC = 196675,
        /// <summary>ctrl shift d.</summary>
        CtrlShiftD = 196676,
        /// <summary>ctrl shift e.</summary>
        CtrlShiftE = 196677,
        /// <summary>ctrl shift f.</summary>
        CtrlShiftF = 196678,
        /// <summary>ctrl shift g.</summary>
        CtrlShiftG = 196679,
        /// <summary>ctrl shift h.</summary>
        CtrlShiftH = 196680,
        /// <summary>ctrl shift i.</summary>
        CtrlShiftI = 196681,
        /// <summary>ctrl shift j.</summary>
        CtrlShiftJ = 196682,
        /// <summary>ctrl shift k.</summary>
        CtrlShiftK = 196683,
        /// <summary>ctrl shift l.</summary>
        CtrlShiftL = 196684,
        /// <summary>ctrl shift m.</summary>
        CtrlShiftM = 196685,
        /// <summary>ctrl shift n.</summary>
        CtrlShiftN = 196686,
        /// <summary>ctrl shift o.</summary>
        CtrlShiftO = 196687,
        /// <summary>ctrl shift p.</summary>
        CtrlShiftP = 196688,
        /// <summary>ctrl shift q.</summary>
        CtrlShiftQ = 196689,
        /// <summary>ctrl shift r.</summary>
        CtrlShiftR = 196690,
        /// <summary>ctrl shift s.</summary>
        CtrlShiftS = 196691,
        /// <summary>ctrl shift t.</summary>
        CtrlShiftT = 196692,
        /// <summary>ctrl shift u.</summary>
        CtrlShiftU = 196693,
        /// <summary>ctrl shift v.</summary>
        CtrlShiftV = 196694,
        /// <summary>ctrl shift w.</summary>
        CtrlShiftW = 196695,
        /// <summary>ctrl shift x.</summary>
        CtrlShiftX = 196696,
        /// <summary>ctrl shift y.</summary>
        CtrlShiftY = 196697,
        /// <summary>ctrl shift z.</summary>
        CtrlShiftZ = 196698,
        /// <summary>f1.</summary>
        F1 = 112,
        /// <summary>f2.</summary>
        F2 = 113,
        /// <summary>f3.</summary>
        F3 = 114,
        /// <summary>f4.</summary>
        F4 = 115,
        /// <summary>f5.</summary>
        F5 = 116,
        /// <summary>f6.</summary>
        F6 = 117,
        /// <summary>f7.</summary>
        F7 = 118,
        /// <summary>f8.</summary>
        F8 = 119,
        /// <summary>f9.</summary>
        F9 = 120,
        /// <summary>f10.</summary>
        F10 = 121,
        /// <summary>f11.</summary>
        F11 = 122,
        /// <summary>f12.</summary>
        F12 = 123,
        /// <summary>shift f1.</summary>
        ShiftF1 = 65648,
        /// <summary>shift f2.</summary>
        ShiftF2 = 65649,
        /// <summary>shift f3.</summary>
        ShiftF3 = 65650,
        /// <summary>shift f4.</summary>
        ShiftF4 = 65651,
        /// <summary>shift f5.</summary>
        ShiftF5 = 65652,
        /// <summary>shift f6.</summary>
        ShiftF6 = 65653,
        /// <summary>shift f7.</summary>
        ShiftF7 = 65654,
        /// <summary>shift f8.</summary>
        ShiftF8 = 65655,
        /// <summary>shift f9.</summary>
        ShiftF9 = 65656,
        /// <summary>shift f10.</summary>
        ShiftF10 = 65657,
        /// <summary>shift f11.</summary>
        ShiftF11 = 65658,
        /// <summary>shift f12.</summary>
        ShiftF12 = 65659,
        /// <summary>ctrl f1.</summary>
        CtrlF1 = 131184,
        /// <summary>ctrl f2.</summary>
        CtrlF2 = 131185,
        /// <summary>ctrl f3.</summary>
        CtrlF3 = 131186,
        /// <summary>ctrl f4.</summary>
        CtrlF4 = 131187,
        /// <summary>ctrl f5.</summary>
        CtrlF5 = 131188,
        /// <summary>ctrl f6.</summary>
        CtrlF6 = 131189,
        /// <summary>ctrl f7.</summary>
        CtrlF7 = 131190,
        /// <summary>ctrl f8.</summary>
        CtrlF8 = 131191,
        /// <summary>ctrl f9.</summary>
        CtrlF9 = 131192,
        /// <summary>ctrl f10.</summary>
        CtrlF10 = 131193,
        /// <summary>ctrl f11.</summary>
        CtrlF11 = 131194,
        /// <summary>ctrl f12.</summary>
        CtrlF12 = 131195,
        /// <summary>ctrl shift f1.</summary>
        CtrlShiftF1 = 196720,
        /// <summary>ctrl shift f2.</summary>
        CtrlShiftF2 = 196721,
        /// <summary>ctrl shift f3.</summary>
        CtrlShiftF3 = 196722,
        /// <summary>ctrl shift f4.</summary>
        CtrlShiftF4 = 196723,
        /// <summary>ctrl shift f5.</summary>
        CtrlShiftF5 = 196724,
        /// <summary>ctrl shift f6.</summary>
        CtrlShiftF6 = 196725,
        /// <summary>ctrl shift f7.</summary>
        CtrlShiftF7 = 196726,
        /// <summary>ctrl shift f8.</summary>
        CtrlShiftF8 = 196727,
        /// <summary>ctrl shift f9.</summary>
        CtrlShiftF9 = 196728,
        /// <summary>ctrl shift f10.</summary>
        CtrlShiftF10 = 196729,
        /// <summary>ctrl shift f11.</summary>
        CtrlShiftF11 = 196730,
        /// <summary>ctrl shift f12.</summary>
        CtrlShiftF12 = 196731,
        /// <summary>ins.</summary>
        Ins = 45,
        /// <summary>ctrl ins.</summary>
        CtrlIns = 131117,
        /// <summary>shift ins.</summary>
        ShiftIns = 65581,
        /// <summary>del.</summary>
        Del = 46,
        /// <summary>ctrl del.</summary>
        CtrlDel = 131118,
        /// <summary>shift del.</summary>
        ShiftDel = 65582,
        /// <summary>alt right arrow.</summary>
        AltRightArrow = 262183,
        /// <summary>alt left arrow.</summary>
        AltLeftArrow = 262181,
        /// <summary>alt up arrow.</summary>
        AltUpArrow = 262182,
        /// <summary>alt down arrow.</summary>
        AltDownArrow = 262184,
        /// <summary>alt bksp.</summary>
        AltBksp = 262152,
        /// <summary>alt f1.</summary>
        AltF1 = 262256,
        /// <summary>alt f2.</summary>
        AltF2 = 262257,
        /// <summary>alt f3.</summary>
        AltF3 = 262258,
        /// <summary>alt f4.</summary>
        AltF4 = 262259,
        /// <summary>alt f5.</summary>
        AltF5 = 262260,
        /// <summary>alt f6.</summary>
        AltF6 = 262261,
        /// <summary>alt f7.</summary>
        AltF7 = 262262,
        /// <summary>alt f8.</summary>
        AltF8 = 262263,
        /// <summary>alt f9.</summary>
        AltF9 = 262264,
        /// <summary>alt f10.</summary>
        AltF10 = 262265,
        /// <summary>alt f11.</summary>
        AltF11 = 262266,
        /// <summary>alt f12.</summary>
        AltF12 = 262267,
        /// <summary>alt0.</summary>
        Alt0 = 262192,
        /// <summary>alt1.</summary>
        Alt1 = 262193,
        /// <summary>alt2.</summary>
        Alt2 = 262194,
        /// <summary>alt3.</summary>
        Alt3 = 262195,
        /// <summary>alt4.</summary>
        Alt4 = 262196,
        /// <summary>alt5.</summary>
        Alt5 = 262197,
        /// <summary>alt6.</summary>
        Alt6 = 262198,
        /// <summary>alt7.</summary>
        Alt7 = 262199,
        /// <summary>alt8.</summary>
        Alt8 = 262200,
        /// <summary>alt9.</summary>
        Alt9 = 262201,
        /// <summary>ctrl0.</summary>
        Ctrl0 = 131120,
        /// <summary>ctrl1.</summary>
        Ctrl1 = 131121,
        /// <summary>ctrl2.</summary>
        Ctrl2 = 131122,
        /// <summary>ctrl3.</summary>
        Ctrl3 = 131123,
        /// <summary>ctrl4.</summary>
        Ctrl4 = 131124,
        /// <summary>ctrl5.</summary>
        Ctrl5 = 131125,
        /// <summary>ctrl6.</summary>
        Ctrl6 = 131126,
        /// <summary>ctrl7.</summary>
        Ctrl7 = 131127,
        /// <summary>ctrl8.</summary>
        Ctrl8 = 131128,
        /// <summary>ctrl9.</summary>
        Ctrl9 = 131129,
        /// <summary>ctrl shift0.</summary>
        CtrlShift0 = 196656,
        /// <summary>ctrl shift1.</summary>
        CtrlShift1 = 196657,
        /// <summary>ctrl shift2.</summary>
        CtrlShift2 = 196658,
        /// <summary>ctrl shift3.</summary>
        CtrlShift3 = 196659,
        /// <summary>ctrl shift4.</summary>
        CtrlShift4 = 196660,
        /// <summary>ctrl shift5.</summary>
        CtrlShift5 = 196661,
        /// <summary>ctrl shift6.</summary>
        CtrlShift6 = 196662,
        /// <summary>ctrl shift7.</summary>
        CtrlShift7 = 196663,
        /// <summary>ctrl shift8.</summary>
        CtrlShift8 = 196664,
        /// <summary>ctrl shift9.</summary>
        CtrlShift9 = 196665,
    }

    /// <summary>Specifies the struct format. Matches <c>System.Windows.Forms.StructFormat</c>, including its numeric values.</summary>
    public enum StructFormat
    {
        /// <summary>ansi.</summary>
        Ansi = 1,
        /// <summary>unicode.</summary>
        Unicode = 2,
        /// <summary>auto.</summary>
        Auto = 3,
    }

    /// <summary>Specifies the task dialog expander position. Matches <c>System.Windows.Forms.TaskDialogExpanderPosition</c>, including its numeric values.</summary>
    public enum TaskDialogExpanderPosition
    {
        /// <summary>after text.</summary>
        AfterText = 0,
        /// <summary>after footnote.</summary>
        AfterFootnote = 1,
    }

    /// <summary>Specifies the task dialog progress bar state. Matches <c>System.Windows.Forms.TaskDialogProgressBarState</c>, including its numeric values.</summary>
    public enum TaskDialogProgressBarState
    {
        /// <summary>normal.</summary>
        Normal = 0,
        /// <summary>paused.</summary>
        Paused = 1,
        /// <summary>error.</summary>
        Error = 2,
        /// <summary>marquee.</summary>
        Marquee = 3,
        /// <summary>marquee paused.</summary>
        MarqueePaused = 4,
        /// <summary>none.</summary>
        None = 5,
    }

    /// <summary>Specifies the task dialog startup location. Matches <c>System.Windows.Forms.TaskDialogStartupLocation</c>, including its numeric values.</summary>
    public enum TaskDialogStartupLocation
    {
        /// <summary>center screen.</summary>
        CenterScreen = 0,
        /// <summary>center owner.</summary>
        CenterOwner = 1,
    }

    /// <summary>Specifies the tool bar appearance. Matches <c>System.Windows.Forms.ToolBarAppearance</c>, including its numeric values.</summary>
    public enum ToolBarAppearance
    {
        /// <summary>normal.</summary>
        Normal = 0,
        /// <summary>flat.</summary>
        Flat = 1,
    }

    /// <summary>Specifies the tool bar text align. Matches <c>System.Windows.Forms.ToolBarTextAlign</c>, including its numeric values.</summary>
    public enum ToolBarTextAlign
    {
        /// <summary>underneath.</summary>
        Underneath = 0,
        /// <summary>right.</summary>
        Right = 1,
    }

    /// <summary>Specifies the tool strip drop down direction. Matches <c>System.Windows.Forms.ToolStripDropDownDirection</c>, including its numeric values.</summary>
    public enum ToolStripDropDownDirection
    {
        /// <summary>above left.</summary>
        AboveLeft = 0,
        /// <summary>above right.</summary>
        AboveRight = 1,
        /// <summary>below left.</summary>
        BelowLeft = 2,
        /// <summary>below right.</summary>
        BelowRight = 3,
        /// <summary>left.</summary>
        Left = 4,
        /// <summary>right.</summary>
        Right = 5,
        /// <summary>default.</summary>
        Default = 7,
    }

    /// <summary>Specifies the tool strip grip display style. Matches <c>System.Windows.Forms.ToolStripGripDisplayStyle</c>, including its numeric values.</summary>
    public enum ToolStripGripDisplayStyle
    {
        /// <summary>horizontal.</summary>
        Horizontal = 0,
        /// <summary>vertical.</summary>
        Vertical = 1,
    }

    /// <summary>Specifies the tool strip item placement. Matches <c>System.Windows.Forms.ToolStripItemPlacement</c>, including its numeric values.</summary>
    public enum ToolStripItemPlacement
    {
        /// <summary>main.</summary>
        Main = 0,
        /// <summary>overflow.</summary>
        Overflow = 1,
        /// <summary>none.</summary>
        None = 2,
    }

    /// <summary>Specifies the tool strip manager render mode. Matches <c>System.Windows.Forms.ToolStripManagerRenderMode</c>, including its numeric values.</summary>
    public enum ToolStripManagerRenderMode
    {
        /// <summary>custom.</summary>
        Custom = 0,
        /// <summary>system.</summary>
        System = 1,
        /// <summary>professional.</summary>
        Professional = 2,
    }

    /// <summary>Specifies the tree node states. Matches <c>System.Windows.Forms.TreeNodeStates</c>, including its numeric values.</summary>
    [Flags]
    public enum TreeNodeStates
    {
        /// <summary>checked.</summary>
        Checked = 8,
        /// <summary>default.</summary>
        Default = 32,
        /// <summary>focused.</summary>
        Focused = 16,
        /// <summary>grayed.</summary>
        Grayed = 2,
        /// <summary>hot.</summary>
        Hot = 64,
        /// <summary>indeterminate.</summary>
        Indeterminate = 0x100,
        /// <summary>marked.</summary>
        Marked = 128,
        /// <summary>selected.</summary>
        Selected = 1,
        /// <summary>show keyboard cues.</summary>
        ShowKeyboardCues = 0x200,
    }

    /// <summary>Specifies the tree view hit test locations. Matches <c>System.Windows.Forms.TreeViewHitTestLocations</c>, including its numeric values.</summary>
    [Flags]
    public enum TreeViewHitTestLocations
    {
        /// <summary>none.</summary>
        None = 1,
        /// <summary>image.</summary>
        Image = 2,
        /// <summary>label.</summary>
        Label = 4,
        /// <summary>indent.</summary>
        Indent = 8,
        /// <summary>above client area.</summary>
        AboveClientArea = 0x100,
        /// <summary>below client area.</summary>
        BelowClientArea = 0x200,
        /// <summary>left of client area.</summary>
        LeftOfClientArea = 0x800,
        /// <summary>right of client area.</summary>
        RightOfClientArea = 0x400,
        /// <summary>right of label.</summary>
        RightOfLabel = 32,
        /// <summary>state image.</summary>
        StateImage = 64,
        /// <summary>plus minus.</summary>
        PlusMinus = 16,
    }

    /// <summary>Specifies the u i cues. Matches <c>System.Windows.Forms.UICues</c>, including its numeric values.</summary>
    [Flags]
    public enum UICues
    {
        /// <summary>show focus.</summary>
        ShowFocus = 1,
        /// <summary>show keyboard.</summary>
        ShowKeyboard = 2,
        /// <summary>shown.</summary>
        Shown = 3,
        /// <summary>change focus.</summary>
        ChangeFocus = 4,
        /// <summary>change keyboard.</summary>
        ChangeKeyboard = 8,
        /// <summary>changed.</summary>
        Changed = 12,
        /// <summary>none.</summary>
        None = 0,
    }

    /// <summary>Specifies the validation constraints. Matches <c>System.Windows.Forms.ValidationConstraints</c>, including its numeric values.</summary>
    [Flags]
    public enum ValidationConstraints
    {
        /// <summary>none.</summary>
        None = 0,
        /// <summary>selectable.</summary>
        Selectable = 1,
        /// <summary>enabled.</summary>
        Enabled = 2,
        /// <summary>visible.</summary>
        Visible = 4,
        /// <summary>tab stop.</summary>
        TabStop = 8,
        /// <summary>immediate children.</summary>
        ImmediateChildren = 16,
    }

    /// <summary>Specifies the web browser encryption level. Matches <c>System.Windows.Forms.WebBrowserEncryptionLevel</c>, including its numeric values.</summary>
    public enum WebBrowserEncryptionLevel
    {
        /// <summary>insecure.</summary>
        Insecure = 0,
        /// <summary>mixed.</summary>
        Mixed = 1,
        /// <summary>unknown.</summary>
        Unknown = 2,
        /// <summary>bit40.</summary>
        Bit40 = 3,
        /// <summary>bit56.</summary>
        Bit56 = 4,
        /// <summary>fortezza.</summary>
        Fortezza = 5,
        /// <summary>bit128.</summary>
        Bit128 = 6,
    }

    /// <summary>Specifies the web browser refresh option. Matches <c>System.Windows.Forms.WebBrowserRefreshOption</c>, including its numeric values.</summary>
    public enum WebBrowserRefreshOption
    {
        /// <summary>normal.</summary>
        Normal = 0,
        /// <summary>if expired.</summary>
        IfExpired = 1,
        /// <summary>continue.</summary>
        Continue = 2,
        /// <summary>completely.</summary>
        Completely = 3,
    }

    /// <summary>Provides data for the binding manager data error event.</summary>
    public class BindingManagerDataErrorEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="BindingManagerDataErrorEventArgs"/> class.</summary>
        public BindingManagerDataErrorEventArgs (Exception exception)
        {
            Exception = exception;
        }

        /// <summary>Gets the exception.</summary>
        public Exception Exception { get; }
    }

    /// <summary>Provides data for the column reordered event.</summary>
    public class ColumnReorderedEventArgs : CancelEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ColumnReorderedEventArgs"/> class.</summary>
        public ColumnReorderedEventArgs (int oldDisplayIndex, int newDisplayIndex, ColumnHeader header)
        {
            OldDisplayIndex = oldDisplayIndex;
            NewDisplayIndex = newDisplayIndex;
            Header = header;
        }

        /// <summary>Gets the old display index.</summary>
        public int OldDisplayIndex { get; }
        /// <summary>Gets the new display index.</summary>
        public int NewDisplayIndex { get; }
        /// <summary>Gets the header.</summary>
        public ColumnHeader Header { get; }
    }

    /// <summary>Provides data for the column width changed event.</summary>
    public class ColumnWidthChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ColumnWidthChangedEventArgs"/> class.</summary>
        public ColumnWidthChangedEventArgs (int columnIndex)
        {
            ColumnIndex = columnIndex;
        }

        /// <summary>Gets the column index.</summary>
        public int ColumnIndex { get; }
    }

    /// <summary>Provides data for the column width changing event.</summary>
    public class ColumnWidthChangingEventArgs : CancelEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ColumnWidthChangingEventArgs"/> class.</summary>
        public ColumnWidthChangingEventArgs (int columnIndex, int newWidth, bool cancel)
        {
            ColumnIndex = columnIndex;
            NewWidth = newWidth;
        }

        /// <summary>Gets the column index.</summary>
        public int ColumnIndex { get; }
        /// <summary>Gets or sets the new width.</summary>
        public int NewWidth { get; set; }
    }

    /// <summary>Provides data for the data grid view auto size columns mode event.</summary>
    public class DataGridViewAutoSizeColumnsModeEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewAutoSizeColumnsModeEventArgs"/> class.</summary>
        public DataGridViewAutoSizeColumnsModeEventArgs (DataGridViewAutoSizeColumnMode[] previousModes)
        {
            PreviousModes = previousModes;
        }

        /// <summary>Gets the previous modes.</summary>
        public DataGridViewAutoSizeColumnMode[] PreviousModes { get; }
    }

    /// <summary>Provides data for the data grid view auto size mode event.</summary>
    public class DataGridViewAutoSizeModeEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewAutoSizeModeEventArgs"/> class.</summary>
        public DataGridViewAutoSizeModeEventArgs (bool previousModeAutoSized)
        {
            PreviousModeAutoSized = previousModeAutoSized;
        }

        /// <summary>Gets the previous mode auto sized.</summary>
        public bool PreviousModeAutoSized { get; }
    }

    /// <summary>Provides data for the data grid view binding complete event.</summary>
    public class DataGridViewBindingCompleteEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewBindingCompleteEventArgs"/> class.</summary>
        public DataGridViewBindingCompleteEventArgs (ListChangedType listChangedType)
        {
            ListChangedType = listChangedType;
        }

        /// <summary>Gets the list changed type.</summary>
        public ListChangedType ListChangedType { get; }
    }

    /// <summary>Provides data for the data grid view cell context menu strip needed event.</summary>
    public class DataGridViewCellContextMenuStripNeededEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewCellContextMenuStripNeededEventArgs"/> class.</summary>
        public DataGridViewCellContextMenuStripNeededEventArgs (int columnIndex, int rowIndex) : base(columnIndex, rowIndex)
        {
        }

        /// <summary>Gets or sets the context menu strip.</summary>
        public ContextMenuStrip ContextMenuStrip { get; set; } = default!;
    }

    /// <summary>Provides data for the data grid view cell style content changed event.</summary>
    public class DataGridViewCellStyleContentChangedEventArgs : EventArgs
    {
        /// <summary>Gets the cell style.</summary>
        public DataGridViewCellStyle CellStyle { get; } = default!;
        /// <summary>Gets the cell style scope.</summary>
        public DataGridViewCellStyleScopes CellStyleScope { get; }
    }

    /// <summary>Provides data for the data grid view column state changed event.</summary>
    public class DataGridViewColumnStateChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewColumnStateChangedEventArgs"/> class.</summary>
        public DataGridViewColumnStateChangedEventArgs (DataGridViewColumn dataGridViewColumn, DataGridViewElementStates stateChanged)
        {
            StateChanged = stateChanged;
        }

        /// <summary>Gets the column.</summary>
        public DataGridViewColumn Column { get; } = default!;
        /// <summary>Gets the state changed.</summary>
        public DataGridViewElementStates StateChanged { get; }
    }

    /// <summary>Provides data for the data grid view row context menu strip needed event.</summary>
    public class DataGridViewRowContextMenuStripNeededEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewRowContextMenuStripNeededEventArgs"/> class.</summary>
        public DataGridViewRowContextMenuStripNeededEventArgs (int rowIndex)
        {
            RowIndex = rowIndex;
        }

        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }
        /// <summary>Gets or sets the context menu strip.</summary>
        public ContextMenuStrip ContextMenuStrip { get; set; } = default!;
    }

    /// <summary>Provides data for the data grid view row error text needed event.</summary>
    public class DataGridViewRowErrorTextNeededEventArgs : EventArgs
    {
        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }
        /// <summary>Gets or sets the error text.</summary>
        public string ErrorText { get; set; } = default!;
    }

    /// <summary>Provides data for the data grid view row height info needed event.</summary>
    public class DataGridViewRowHeightInfoNeededEventArgs : EventArgs
    {
        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }
        /// <summary>Gets or sets the height.</summary>
        public int Height { get; set; }
        /// <summary>Gets or sets the minimum height.</summary>
        public int MinimumHeight { get; set; }
    }

    /// <summary>Provides data for the data grid view row height info pushed event.</summary>
    public class DataGridViewRowHeightInfoPushedEventArgs : HandledEventArgs
    {
        /// <summary>Gets the row index.</summary>
        public int RowIndex { get; }
        /// <summary>Gets the height.</summary>
        public int Height { get; }
        /// <summary>Gets the minimum height.</summary>
        public int MinimumHeight { get; }
    }

    /// <summary>Provides data for the date bold event.</summary>
    public class DateBoldEventArgs : EventArgs
    {
        /// <summary>Gets the start date.</summary>
        public DateTime StartDate { get; }
        /// <summary>Gets the size.</summary>
        public int Size { get; }
        /// <summary>Gets or sets the days to bold.</summary>
        public int[] DaysToBold { get; set; } = default!;
    }

    /// <summary>Provides data for the dpi changed event.</summary>
    public class DpiChangedEventArgs : CancelEventArgs
    {
        /// <summary>Gets the device dpi old.</summary>
        public int DeviceDpiOld { get; }
        /// <summary>Gets the device dpi new.</summary>
        public int DeviceDpiNew { get; }
        /// <summary>Gets the suggested rectangle.</summary>
        public Rectangle SuggestedRectangle { get; }
    }

    /// <summary>Provides data for the draw list view column header event.</summary>
    public class DrawListViewColumnHeaderEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DrawListViewColumnHeaderEventArgs"/> class.</summary>
        public DrawListViewColumnHeaderEventArgs (Graphics graphics, Rectangle bounds, int columnIndex, ColumnHeader header, ListViewItemStates state, Color foreColor, Color backColor, Font font)
        {
            Graphics = graphics;
            Bounds = bounds;
            ColumnIndex = columnIndex;
            Header = header;
            State = state;
            ForeColor = foreColor;
            BackColor = backColor;
            Font = font;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; }
        /// <summary>Gets the bounds.</summary>
        public Rectangle Bounds { get; }
        /// <summary>Gets the column index.</summary>
        public int ColumnIndex { get; }
        /// <summary>Gets the header.</summary>
        public ColumnHeader Header { get; }
        /// <summary>Gets the state.</summary>
        public ListViewItemStates State { get; }
        /// <summary>Gets the fore color.</summary>
        public Color ForeColor { get; }
        /// <summary>Gets the back color.</summary>
        public Color BackColor { get; }
        /// <summary>Gets the font.</summary>
        public Font Font { get; }
        /// <summary>Gets or sets the draw default.</summary>
        public bool DrawDefault { get; set; }
    }

    /// <summary>Provides data for the draw list view item event.</summary>
    public partial class DrawListViewItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DrawListViewItemEventArgs"/> class.</summary>
        public DrawListViewItemEventArgs (Graphics graphics, ListViewItem item, Rectangle bounds, int itemIndex, ListViewItemStates state)
        {
            Graphics = graphics;
            Item = item;
            Bounds = bounds;
            ItemIndex = itemIndex;
            State = state;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; }
        /// <summary>Gets the item.</summary>
        public ListViewItem Item { get; }
        /// <summary>Gets the bounds.</summary>
        public Rectangle Bounds { get; }
        /// <summary>Gets the item index.</summary>
        public int ItemIndex { get; }
        /// <summary>Gets the state.</summary>
        public ListViewItemStates State { get; }
        /// <summary>Gets or sets the draw default.</summary>
        public bool DrawDefault { get; set; }
    }

    /// <summary>Provides data for the draw list view sub item event.</summary>
    public partial class DrawListViewSubItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DrawListViewSubItemEventArgs"/> class.</summary>
        public DrawListViewSubItemEventArgs (Graphics graphics, Rectangle bounds, ListViewItem item, ListViewItem.ListViewSubItem subItem, int itemIndex, int columnIndex, ColumnHeader header, ListViewItemStates itemState)
        {
            Graphics = graphics;
            Bounds = bounds;
            Item = item;
            SubItem = subItem;
            ItemIndex = itemIndex;
            ColumnIndex = columnIndex;
            Header = header;
            ItemState = itemState;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; }
        /// <summary>Gets the bounds.</summary>
        public Rectangle Bounds { get; }
        /// <summary>Gets the item.</summary>
        public ListViewItem Item { get; }
        /// <summary>Gets the sub item.</summary>
        public ListViewItem.ListViewSubItem SubItem { get; }
        /// <summary>Gets the item index.</summary>
        public int ItemIndex { get; }
        /// <summary>Gets the column index.</summary>
        public int ColumnIndex { get; }
        /// <summary>Gets the header.</summary>
        public ColumnHeader Header { get; }
        /// <summary>Gets the item state.</summary>
        public ListViewItemStates ItemState { get; }
        /// <summary>Gets or sets the draw default.</summary>
        public bool DrawDefault { get; set; }
    }

    /// <summary>Provides data for the draw tool tip event.</summary>
    public partial class DrawToolTipEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DrawToolTipEventArgs"/> class.</summary>
        public DrawToolTipEventArgs (Graphics graphics, IWin32Window associatedWindow, Control associatedControl, Rectangle bounds, string toolTipText, Color backColor, Color foreColor, Font font)
        {
            Graphics = graphics;
            AssociatedWindow = associatedWindow;
            AssociatedControl = associatedControl;
            Bounds = bounds;
            ToolTipText = toolTipText;
            Font = font;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; }
        /// <summary>Gets the associated window.</summary>
        public IWin32Window AssociatedWindow { get; }
        /// <summary>Gets the associated control.</summary>
        public Control AssociatedControl { get; }
        /// <summary>Gets the bounds.</summary>
        public Rectangle Bounds { get; }
        /// <summary>Gets the tool tip text.</summary>
        public string ToolTipText { get; }
        /// <summary>Gets the font.</summary>
        public Font Font { get; }
    }

    /// <summary>Provides data for the draw tree node event.</summary>
    public class DrawTreeNodeEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DrawTreeNodeEventArgs"/> class.</summary>
        public DrawTreeNodeEventArgs (Graphics graphics, TreeNode node, Rectangle bounds, TreeNodeStates state)
        {
            Graphics = graphics;
            Node = node;
            Bounds = bounds;
            State = state;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; }
        /// <summary>Gets the node.</summary>
        public TreeNode Node { get; }
        /// <summary>Gets the bounds.</summary>
        public Rectangle Bounds { get; }
        /// <summary>Gets the state.</summary>
        public TreeNodeStates State { get; }
        /// <summary>Gets or sets the draw default.</summary>
        public bool DrawDefault { get; set; }
    }

    /// <summary>Provides data for the html element error event.</summary>
    public class HtmlElementErrorEventArgs : EventArgs
    {
        /// <summary>Gets the description.</summary>
        public string Description { get; } = default!;
        /// <summary>Gets or sets the handled.</summary>
        public bool Handled { get; set; }
        /// <summary>Gets the line number.</summary>
        public int LineNumber { get; }
        /// <summary>Gets the url.</summary>
        public Uri Url { get; } = default!;
    }

    /// <summary>Provides data for the html element event.</summary>
    public class HtmlElementEventArgs : EventArgs
    {
        /// <summary>Gets the mouse buttons pressed.</summary>
        public MouseButtons MouseButtonsPressed { get; }
        /// <summary>Gets the client mouse position.</summary>
        public Point ClientMousePosition { get; }
        /// <summary>Gets the offset mouse position.</summary>
        public Point OffsetMousePosition { get; }
        /// <summary>Gets the mouse position.</summary>
        public Point MousePosition { get; }
        /// <summary>Gets or sets the bubble event.</summary>
        public bool BubbleEvent { get; set; }
        /// <summary>Gets the key pressed code.</summary>
        public int KeyPressedCode { get; }
        /// <summary>Gets the alt key pressed.</summary>
        public bool AltKeyPressed { get; }
        /// <summary>Gets the ctrl key pressed.</summary>
        public bool CtrlKeyPressed { get; }
        /// <summary>Gets the shift key pressed.</summary>
        public bool ShiftKeyPressed { get; }
        /// <summary>Gets the event type.</summary>
        public string EventType { get; } = default!;
        /// <summary>Gets or sets the return value.</summary>
        public bool ReturnValue { get; set; }
    }

    /// <summary>Provides data for the item changed event.</summary>
    public class ItemChangedEventArgs : EventArgs
    {
        /// <summary>Gets the index.</summary>
        public int Index { get; }
    }

    /// <summary>Provides data for the list control convert event.</summary>
    public class ListControlConvertEventArgs : ConvertEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ListControlConvertEventArgs"/> class.</summary>
        public ListControlConvertEventArgs (object value, Type desiredType, object listItem) : base(value, desiredType)
        {
            ListItem = listItem;
        }

        /// <summary>Gets the list item.</summary>
        public object ListItem { get; }
    }

    /// <summary>Provides data for the list view group event.</summary>
    public class ListViewGroupEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ListViewGroupEventArgs"/> class.</summary>
        public ListViewGroupEventArgs (int groupIndex)
        {
            GroupIndex = groupIndex;
        }

        /// <summary>Gets the group index.</summary>
        public int GroupIndex { get; }
    }

    /// <summary>Provides data for the list view item mouse hover event.</summary>
    public class ListViewItemMouseHoverEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ListViewItemMouseHoverEventArgs"/> class.</summary>
        public ListViewItemMouseHoverEventArgs (ListViewItem item)
        {
            Item = item;
        }

        /// <summary>Gets the item.</summary>
        public ListViewItem Item { get; }
    }

    /// <summary>Provides data for the list view item selection changed event.</summary>
    public class ListViewItemSelectionChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ListViewItemSelectionChangedEventArgs"/> class.</summary>
        public ListViewItemSelectionChangedEventArgs (ListViewItem item, int itemIndex, bool isSelected)
        {
            Item = item;
            ItemIndex = itemIndex;
            IsSelected = isSelected;
        }

        /// <summary>Gets the item.</summary>
        public ListViewItem Item { get; }
        /// <summary>Gets the item index.</summary>
        public int ItemIndex { get; }
        /// <summary>Gets the is selected.</summary>
        public bool IsSelected { get; }
    }

    /// <summary>Provides data for the list view virtual items selection range changed event.</summary>
    public class ListViewVirtualItemsSelectionRangeChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ListViewVirtualItemsSelectionRangeChangedEventArgs"/> class.</summary>
        public ListViewVirtualItemsSelectionRangeChangedEventArgs (int startIndex, int endIndex, bool isSelected)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            IsSelected = isSelected;
        }

        /// <summary>Gets the start index.</summary>
        public int StartIndex { get; }
        /// <summary>Gets the end index.</summary>
        public int EndIndex { get; }
        /// <summary>Gets the is selected.</summary>
        public bool IsSelected { get; }
    }

    /// <summary>Provides data for the popup event.</summary>
    public class PopupEventArgs : CancelEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="PopupEventArgs"/> class.</summary>
        public PopupEventArgs (IWin32Window associatedWindow, Control associatedControl, bool isBalloon, Size size)
        {
            AssociatedWindow = associatedWindow;
            AssociatedControl = associatedControl;
            IsBalloon = isBalloon;
        }

        /// <summary>Gets the associated window.</summary>
        public IWin32Window AssociatedWindow { get; }
        /// <summary>Gets the associated control.</summary>
        public Control AssociatedControl { get; }
        /// <summary>Gets or sets the tool tip size.</summary>
        public Size ToolTipSize { get; set; }
        /// <summary>Gets the is balloon.</summary>
        public bool IsBalloon { get; }
    }

    /// <summary>Provides data for the property tab changed event.</summary>
    public class PropertyTabChangedEventArgs : EventArgs
    {
    }

    /// <summary>Provides data for the property value changed event.</summary>
    public class PropertyValueChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="PropertyValueChangedEventArgs"/> class.</summary>
        public PropertyValueChangedEventArgs (GridItem changedItem, object oldValue)
        {
            ChangedItem = changedItem;
            OldValue = oldValue;
        }

        /// <summary>Gets the changed item.</summary>
        public GridItem ChangedItem { get; }
        /// <summary>Gets the old value.</summary>
        public object OldValue { get; }
    }

    /// <summary>Provides data for the question event.</summary>
    public class QuestionEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="QuestionEventArgs"/> class.</summary>
        public QuestionEventArgs (bool response)
        {
            Response = response;
        }

        /// <summary>Gets or sets the response.</summary>
        public bool Response { get; set; }
    }

    /// <summary>Provides data for the search for virtual item event.</summary>
    public class SearchForVirtualItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="SearchForVirtualItemEventArgs"/> class.</summary>
        public SearchForVirtualItemEventArgs (bool isTextSearch, bool isPrefixSearch, bool includeSubItemsInSearch, string text, Point startingPoint, SearchDirectionHint direction, int startIndex)
        {
            IsTextSearch = isTextSearch;
            IsPrefixSearch = isPrefixSearch;
            IncludeSubItemsInSearch = includeSubItemsInSearch;
            Text = text;
            StartingPoint = startingPoint;
            Direction = direction;
            StartIndex = startIndex;
        }

        /// <summary>Gets the is text search.</summary>
        public bool IsTextSearch { get; }
        /// <summary>Gets the is prefix search.</summary>
        public bool IsPrefixSearch { get; }
        /// <summary>Gets the include sub items in search.</summary>
        public bool IncludeSubItemsInSearch { get; }
        /// <summary>Gets or sets the index.</summary>
        public int Index { get; set; }
        /// <summary>Gets the text.</summary>
        public string Text { get; }
        /// <summary>Gets the starting point.</summary>
        public Point StartingPoint { get; }
        /// <summary>Gets the direction.</summary>
        public SearchDirectionHint Direction { get; }
        /// <summary>Gets the start index.</summary>
        public int StartIndex { get; }
    }

    /// <summary>Provides data for the selected grid item changed event.</summary>
    public class SelectedGridItemChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="SelectedGridItemChangedEventArgs"/> class.</summary>
        public SelectedGridItemChangedEventArgs (GridItem oldSel, GridItem newSel)
        {
        }

        /// <summary>Gets the old selection.</summary>
        public GridItem OldSelection { get; } = default!;
        /// <summary>Gets the new selection.</summary>
        public GridItem NewSelection { get; } = default!;
    }

    /// <summary>Provides data for the task dialog link clicked event.</summary>
    public class TaskDialogLinkClickedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="TaskDialogLinkClickedEventArgs"/> class.</summary>
        public TaskDialogLinkClickedEventArgs (string linkHref)
        {
            LinkHref = linkHref;
        }

        /// <summary>Gets the link href.</summary>
        public string LinkHref { get; }
    }

    /// <summary>Provides data for the tool strip arrow render event.</summary>
    public class ToolStripArrowRenderEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripArrowRenderEventArgs"/> class.</summary>
        public ToolStripArrowRenderEventArgs (Graphics g, ToolStripItem toolStripItem, Rectangle arrowRectangle, Color arrowColor, ArrowDirection arrowDirection)
        {
            ArrowRectangle = arrowRectangle;
            ArrowColor = arrowColor;
        }

        /// <summary>Gets or sets the arrow rectangle.</summary>
        public Rectangle ArrowRectangle { get; set; }
        /// <summary>Gets or sets the arrow color.</summary>
        public Color ArrowColor { get; set; }
        /// <summary>Gets or sets the direction.</summary>
        public ArrowDirection Direction { get; set; }
        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; } = default!;
        /// <summary>Gets the item.</summary>
        public ToolStripItem Item { get; } = default!;
    }

    /// <summary>Provides data for the tool strip content panel render event.</summary>
    public class ToolStripContentPanelRenderEventArgs : EventArgs
    {
        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; } = default!;
        /// <summary>Gets or sets the handled.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>Provides data for the tool strip item render event.</summary>
    public class ToolStripItemRenderEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripItemRenderEventArgs"/> class.</summary>
        public ToolStripItemRenderEventArgs (Graphics g, ToolStripItem item)
        {
            Item = item;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; } = default!;
        /// <summary>Gets the item.</summary>
        public ToolStripItem Item { get; }
        /// <summary>Gets the tool strip.</summary>
        public ToolStrip ToolStrip { get; } = default!;
    }

    /// <summary>Provides data for the tool strip panel render event.</summary>
    public class ToolStripPanelRenderEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripPanelRenderEventArgs"/> class.</summary>
        public ToolStripPanelRenderEventArgs (Graphics g, ToolStripPanel toolStripPanel)
        {
            ToolStripPanel = toolStripPanel;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; } = default!;
        /// <summary>Gets the tool strip panel.</summary>
        public ToolStripPanel ToolStripPanel { get; }
        /// <summary>Gets or sets the handled.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>Provides data for the tool strip render event.</summary>
    public class ToolStripRenderEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripRenderEventArgs"/> class.</summary>
        public ToolStripRenderEventArgs (Graphics g, ToolStrip toolStrip, Rectangle affectedBounds, Color backColor)
        {
            ToolStrip = toolStrip;
            AffectedBounds = affectedBounds;
            BackColor = backColor;
        }

        /// <summary>Gets the graphics.</summary>
        public Graphics Graphics { get; } = default!;
        /// <summary>Gets the affected bounds.</summary>
        public Rectangle AffectedBounds { get; }
        /// <summary>Gets the tool strip.</summary>
        public ToolStrip ToolStrip { get; }
        /// <summary>Gets the back color.</summary>
        public Color BackColor { get; }
        /// <summary>Gets the connected area.</summary>
        public Rectangle ConnectedArea { get; }
    }

    /// <summary>Provides data for the u i cues event.</summary>
    public class UICuesEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="UICuesEventArgs"/> class.</summary>
        public UICuesEventArgs (UICues uicues)
        {
        }

        /// <summary>Gets the show focus.</summary>
        public bool ShowFocus { get; }
        /// <summary>Gets the show keyboard.</summary>
        public bool ShowKeyboard { get; }
        /// <summary>Gets the change focus.</summary>
        public bool ChangeFocus { get; }
        /// <summary>Gets the change keyboard.</summary>
        public bool ChangeKeyboard { get; }
        /// <summary>Gets the changed.</summary>
        public UICues Changed { get; }
    }

    /// <summary>Provides data for the up down event.</summary>
    public class UpDownEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="UpDownEventArgs"/> class.</summary>
        public UpDownEventArgs (int buttonPushed)
        {
        }

        /// <summary>Gets the button i d.</summary>
        public int ButtonID { get; }
    }

    /// <summary>Provides data for the web browser progress changed event.</summary>
    public class WebBrowserProgressChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="WebBrowserProgressChangedEventArgs"/> class.</summary>
        public WebBrowserProgressChangedEventArgs (long currentProgress, long maximumProgress)
        {
            CurrentProgress = currentProgress;
            MaximumProgress = maximumProgress;
        }

        /// <summary>Gets the current progress.</summary>
        public long CurrentProgress { get; }
        /// <summary>Gets the maximum progress.</summary>
        public long MaximumProgress { get; }
    }

    /// <summary>Represents the method that handles the binding complete event.</summary>
    public delegate void BindingCompleteEventHandler (object sender, BindingCompleteEventArgs e);

    /// <summary>Represents the method that handles the binding manager data error event.</summary>
    public delegate void BindingManagerDataErrorEventHandler (object sender, BindingManagerDataErrorEventArgs e);

    /// <summary>Represents the method that handles the cache virtual items event.</summary>
    public delegate void CacheVirtualItemsEventHandler (object sender, CacheVirtualItemsEventArgs e);

    /// <summary>Represents the method that handles the column reordered event.</summary>
    public delegate void ColumnReorderedEventHandler (object sender, ColumnReorderedEventArgs e);

    /// <summary>Represents the method that handles the column width changed event.</summary>
    public delegate void ColumnWidthChangedEventHandler (object sender, ColumnWidthChangedEventArgs e);

    /// <summary>Represents the method that handles the column width changing event.</summary>
    public delegate void ColumnWidthChangingEventHandler (object sender, ColumnWidthChangingEventArgs e);

    /// <summary>Represents the method that handles the contents resized event.</summary>
    public delegate void ContentsResizedEventHandler (object sender, ContentsResizedEventArgs e);

    /// <summary>Represents the method that handles the control event.</summary>
    public delegate void ControlEventHandler (object sender, ControlEventArgs e);

    /// <summary>Represents the method that handles the data grid view auto size column mode event.</summary>
    public delegate void DataGridViewAutoSizeColumnModeEventHandler (object sender, DataGridViewAutoSizeColumnModeEventArgs e);

    /// <summary>Represents the method that handles the data grid view auto size columns mode event.</summary>
    public delegate void DataGridViewAutoSizeColumnsModeEventHandler (object sender, DataGridViewAutoSizeColumnsModeEventArgs e);

    /// <summary>Represents the method that handles the data grid view auto size mode event.</summary>
    public delegate void DataGridViewAutoSizeModeEventHandler (object sender, DataGridViewAutoSizeModeEventArgs e);

    /// <summary>Represents the method that handles the data grid view binding complete event.</summary>
    public delegate void DataGridViewBindingCompleteEventHandler (object sender, DataGridViewBindingCompleteEventArgs e);

    /// <summary>Represents the method that handles the data grid view cell cancel event.</summary>
    public delegate void DataGridViewCellCancelEventHandler (object sender, DataGridViewCellCancelEventArgs e);

    /// <summary>Represents the method that handles the data grid view cell context menu strip needed event.</summary>
    public delegate void DataGridViewCellContextMenuStripNeededEventHandler (object sender, DataGridViewCellContextMenuStripNeededEventArgs e);

    /// <summary>Represents the method that handles the data grid view cell state changed event.</summary>
    public delegate void DataGridViewCellStateChangedEventHandler (object sender, DataGridViewCellStateChangedEventArgs e);

    /// <summary>Represents the method that handles the data grid view cell style content changed event.</summary>
    public delegate void DataGridViewCellStyleContentChangedEventHandler (object sender, DataGridViewCellStyleContentChangedEventArgs e);

    /// <summary>Represents the method that handles the data grid view cell tool tip text needed event.</summary>
    public delegate void DataGridViewCellToolTipTextNeededEventHandler (object sender, DataGridViewCellToolTipTextNeededEventArgs e);

    /// <summary>Represents the method that handles the data grid view cell value event.</summary>
    public delegate void DataGridViewCellValueEventHandler (object sender, DataGridViewCellValueEventArgs e);

    /// <summary>Represents the method that handles the data grid view column state changed event.</summary>
    public delegate void DataGridViewColumnStateChangedEventHandler (object sender, DataGridViewColumnStateChangedEventArgs e);

    /// <summary>Represents the method that handles the data grid view editing control showing event.</summary>
    public delegate void DataGridViewEditingControlShowingEventHandler (object sender, DataGridViewEditingControlShowingEventArgs e);

    /// <summary>Represents the method that handles the data grid view row context menu strip needed event.</summary>
    public delegate void DataGridViewRowContextMenuStripNeededEventHandler (object sender, DataGridViewRowContextMenuStripNeededEventArgs e);

    /// <summary>Represents the method that handles the data grid view row error text needed event.</summary>
    public delegate void DataGridViewRowErrorTextNeededEventHandler (object sender, DataGridViewRowErrorTextNeededEventArgs e);

    /// <summary>Represents the method that handles the data grid view row height info needed event.</summary>
    public delegate void DataGridViewRowHeightInfoNeededEventHandler (object sender, DataGridViewRowHeightInfoNeededEventArgs e);

    /// <summary>Represents the method that handles the data grid view row height info pushed event.</summary>
    public delegate void DataGridViewRowHeightInfoPushedEventHandler (object sender, DataGridViewRowHeightInfoPushedEventArgs e);

    /// <summary>Represents the method that handles the data grid view row post paint event.</summary>
    public delegate void DataGridViewRowPostPaintEventHandler (object sender, DataGridViewRowPostPaintEventArgs e);

    /// <summary>Represents the method that handles the data grid view row pre paint event.</summary>
    public delegate void DataGridViewRowPrePaintEventHandler (object sender, DataGridViewRowPrePaintEventArgs e);

    /// <summary>Represents the method that handles the data grid view sort compare event.</summary>
    public delegate void DataGridViewSortCompareEventHandler (object sender, DataGridViewSortCompareEventArgs e);

    /// <summary>Represents the method that handles the date bold event.</summary>
    public delegate void DateBoldEventHandler (object sender, DateBoldEventArgs e);

    /// <summary>Represents the method that handles the date range event.</summary>
    public delegate void DateRangeEventHandler (object sender, DateRangeEventArgs e);

    /// <summary>Represents the method that handles the dpi changed event.</summary>
    public delegate void DpiChangedEventHandler (object sender, DpiChangedEventArgs e);

    /// <summary>Represents the method that handles the draw list view column header event.</summary>
    public delegate void DrawListViewColumnHeaderEventHandler (object sender, DrawListViewColumnHeaderEventArgs e);

    /// <summary>Represents the method that handles the draw list view item event.</summary>
    public delegate void DrawListViewItemEventHandler (object sender, DrawListViewItemEventArgs e);

    /// <summary>Represents the method that handles the draw list view sub item event.</summary>
    public delegate void DrawListViewSubItemEventHandler (object sender, DrawListViewSubItemEventArgs e);

    /// <summary>Represents the method that handles the draw tool tip event.</summary>
    public delegate void DrawToolTipEventHandler (object sender, DrawToolTipEventArgs e);

    /// <summary>Represents the method that handles the draw tree node event.</summary>
    public delegate void DrawTreeNodeEventHandler (object sender, DrawTreeNodeEventArgs e);

    /// <summary>Represents the method that handles the html element error event.</summary>
    public delegate void HtmlElementErrorEventHandler (object sender, HtmlElementErrorEventArgs e);

    /// <summary>Represents the method that handles the html element event.</summary>
    public delegate void HtmlElementEventHandler (object sender, HtmlElementEventArgs e);

    /// <summary>Represents the method that handles the input language changed event.</summary>
    public delegate void InputLanguageChangedEventHandler (object sender, InputLanguageChangedEventArgs e);

    /// <summary>Represents the method that handles the input language changing event.</summary>
    public delegate void InputLanguageChangingEventHandler (object sender, InputLanguageChangingEventArgs e);

    /// <summary>Represents the method that handles the invalidate event.</summary>
    public delegate void InvalidateEventHandler (object sender, InvalidateEventArgs e);

    /// <summary>Represents the method that handles the item changed event.</summary>
    public delegate void ItemChangedEventHandler (object sender, ItemChangedEventArgs e);

    /// <summary>Represents the method that handles the item check event.</summary>
    public delegate void ItemCheckEventHandler (object sender, ItemCheckEventArgs e);

    /// <summary>Represents the method that handles the item checked event.</summary>
    public delegate void ItemCheckedEventHandler (object sender, ItemCheckedEventArgs e);

    /// <summary>Represents the method that handles the item drag event.</summary>
    public delegate void ItemDragEventHandler (object sender, ItemDragEventArgs e);

    /// <summary>Represents the method that handles the label edit event.</summary>
    public delegate void LabelEditEventHandler (object sender, LabelEditEventArgs e);

    /// <summary>Represents the method that handles the layout event.</summary>
    public delegate void LayoutEventHandler (object sender, LayoutEventArgs e);

    /// <summary>Represents the method that handles the link clicked event.</summary>
    public delegate void LinkClickedEventHandler (object sender, LinkClickedEventArgs e);

    /// <summary>Represents the method that handles the list control convert event.</summary>
    public delegate void ListControlConvertEventHandler (object sender, ListControlConvertEventArgs e);

    /// <summary>Represents the method that handles the list view item mouse hover event.</summary>
    public delegate void ListViewItemMouseHoverEventHandler (object sender, ListViewItemMouseHoverEventArgs e);

    /// <summary>Represents the method that handles the list view item selection changed event.</summary>
    public delegate void ListViewItemSelectionChangedEventHandler (object sender, ListViewItemSelectionChangedEventArgs e);

    /// <summary>Represents the method that handles the list view virtual items selection range changed event.</summary>
    public delegate void ListViewVirtualItemsSelectionRangeChangedEventHandler (object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e);

    /// <summary>Represents the method that handles the mask input rejected event.</summary>
    public delegate void MaskInputRejectedEventHandler (object sender, MaskInputRejectedEventArgs e);

    /// <summary>Represents the method that handles the navigate event.</summary>
    public delegate void NavigateEventHandler (object sender, NavigateEventArgs ne);

    /// <summary>Represents the method that handles the popup event.</summary>
    public delegate void PopupEventHandler (object sender, PopupEventArgs e);

    /// <summary>Represents the method that handles the preview key down event.</summary>
    public delegate void PreviewKeyDownEventHandler (object sender, PreviewKeyDownEventArgs e);

    /// <summary>Represents the method that handles the property tab changed event.</summary>
    public delegate void PropertyTabChangedEventHandler (object s, PropertyTabChangedEventArgs e);

    /// <summary>Represents the method that handles the property value changed event.</summary>
    public delegate void PropertyValueChangedEventHandler (object s, PropertyValueChangedEventArgs e);

    /// <summary>Represents the method that handles the question event.</summary>
    public delegate void QuestionEventHandler (object sender, QuestionEventArgs e);

    /// <summary>Represents the method that handles the retrieve virtual item event.</summary>
    public delegate void RetrieveVirtualItemEventHandler (object sender, RetrieveVirtualItemEventArgs e);

    /// <summary>Represents the method that handles the search for virtual item event.</summary>
    public delegate void SearchForVirtualItemEventHandler (object sender, SearchForVirtualItemEventArgs e);

    /// <summary>Represents the method that handles the selected grid item changed event.</summary>
    public delegate void SelectedGridItemChangedEventHandler (object sender, SelectedGridItemChangedEventArgs e);

    /// <summary>Represents the method that handles the table layout cell paint event.</summary>
    public delegate void TableLayoutCellPaintEventHandler (object sender, TableLayoutCellPaintEventArgs e);

    /// <summary>Represents the method that handles the tool bar button click event.</summary>
    public delegate void ToolBarButtonClickEventHandler (object sender, ToolBarButtonClickEventArgs e);

    /// <summary>Represents the method that handles the tool strip arrow render event.</summary>
    public delegate void ToolStripArrowRenderEventHandler (object sender, ToolStripArrowRenderEventArgs e);

    /// <summary>Represents the method that handles the tool strip content panel render event.</summary>
    public delegate void ToolStripContentPanelRenderEventHandler (object sender, ToolStripContentPanelRenderEventArgs e);

    /// <summary>Represents the method that handles the tool strip drop down closed event.</summary>
    public delegate void ToolStripDropDownClosedEventHandler (object sender, ToolStripDropDownClosedEventArgs e);

    /// <summary>Represents the method that handles the tool strip drop down closing event.</summary>
    public delegate void ToolStripDropDownClosingEventHandler (object sender, ToolStripDropDownClosingEventArgs e);

    /// <summary>Represents the method that handles the tool strip item render event.</summary>
    public delegate void ToolStripItemRenderEventHandler (object sender, ToolStripItemRenderEventArgs e);

    /// <summary>Represents the method that handles the tool strip panel render event.</summary>
    public delegate void ToolStripPanelRenderEventHandler (object sender, ToolStripPanelRenderEventArgs e);

    /// <summary>Represents the method that handles the tool strip render event.</summary>
    public delegate void ToolStripRenderEventHandler (object sender, ToolStripRenderEventArgs e);

    /// <summary>Represents the method that handles the tree node mouse click event.</summary>
    public delegate void TreeNodeMouseClickEventHandler (object sender, TreeNodeMouseClickEventArgs e);

    /// <summary>Represents the method that handles the tree node mouse hover event.</summary>
    public delegate void TreeNodeMouseHoverEventHandler (object sender, TreeNodeMouseHoverEventArgs e);

    /// <summary>Represents the method that handles the type validation event.</summary>
    public delegate void TypeValidationEventHandler (object sender, TypeValidationEventArgs e);

    /// <summary>Represents the method that handles the u i cues event.</summary>
    public delegate void UICuesEventHandler (object sender, UICuesEventArgs e);

    /// <summary>Represents the method that handles the up down event.</summary>
    public delegate void UpDownEventHandler (object source, UpDownEventArgs e);

    /// <summary>Represents the method that handles the web browser document completed event.</summary>
    public delegate void WebBrowserDocumentCompletedEventHandler (object sender, WebBrowserDocumentCompletedEventArgs e);

    /// <summary>Represents the method that handles the web browser navigated event.</summary>
    public delegate void WebBrowserNavigatedEventHandler (object sender, WebBrowserNavigatedEventArgs e);

    /// <summary>Represents the method that handles the web browser navigating event.</summary>
    public delegate void WebBrowserNavigatingEventHandler (object sender, WebBrowserNavigatingEventArgs e);

    /// <summary>Represents the method that handles the web browser progress changed event.</summary>
    public delegate void WebBrowserProgressChangedEventHandler (object sender, WebBrowserProgressChangedEventArgs e);
}
