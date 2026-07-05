using System.Drawing;

// Telerik WinControls compatibility layer for Majorsilence.Forms.
//
// These types mirror the public surface of the Telerik.WinControls.* controls used by migrated
// WinForms apps, but live in the `Majorsilence.Forms.Telerik` namespace and are backed by Majorsilence.Forms
// controls. To migrate, swap `Imports Telerik.WinControls.UI` (and related Telerik namespaces) for
// `Imports Majorsilence.Forms.Telerik`. Coverage is compile-and-approximate, not pixel-perfect: the rich
// "element tree" of Telerik is represented by lightweight stub elements so formatting handlers and
// designer code compile and run.
namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Lightweight stand-in for a Telerik visual element (RootElement, cell/row elements, etc.).
    /// Exposes the commonly-accessed styling members as settable no-ops so migrated code compiles.
    /// </summary>
    public class RadElement
    {
        /// <summary>Gets the current behavior object (stub).</summary>
        public object? GetCurrentBehavior () => null;

        /// <summary>Gets or sets whether the element draws its border. Stored for Telerik compat.</summary>
        public bool DrawBorder { get; set; } = true;

        /// <summary>Gets or sets whether the element is enabled.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>Gets or sets the element visibility.</summary>
        public ElementVisibility Visibility { get; set; } = ElementVisibility.Visible;
        /// <summary>Gets or sets the minimum size of the element.</summary>
        public Size MinSize { get; set; }
        /// <summary>Gets or sets the maximum size of the element.</summary>
        public Size MaxSize { get; set; }
        /// <summary>Gets or sets the bounds of the element.</summary>
        public Rectangle ControlBounds { get; set; }
        /// <summary>Gets or sets the element background color.</summary>
        public Color BackColor { get; set; } = Color.Empty;
        /// <summary>Gets or sets the element foreground color.</summary>
        public Color ForeColor { get; set; } = Color.Empty;
        /// <summary>Gets or sets the element's padding. Stub — stored but not applied to layout.</summary>
        public Majorsilence.Forms.Padding Padding { get; set; }
        /// <summary>Gets the child elements of this element.</summary>
        public System.Collections.Generic.List<RadElement> Children { get; } = new ();
        /// <summary>Returns the child element at the specified index, or a new stub element.</summary>
        public virtual RadElement GetChildAt (int index) => new RadElement ();
        /// <summary>Returns the element at the given control coordinates, or null. Stub — the compat element tree has no hit testing.</summary>
        public RadElement? GetElementAtPoint (Point point) => null;
        /// <summary>Suspends element updates while values change. No-op stub (Telerik grid element BeginEdit).</summary>
        public void BeginEdit () { }
        /// <summary>Resumes element updates after changes. No-op stub (Telerik grid element EndEdit).</summary>
        public void EndEdit () { }
        /// <summary>Resets a property to its default value. No-op stub.</summary>
        public void ResetValue (object? property = null) { }
        /// <summary>Resets a property to its default value using the specified reset scope. No-op stub.</summary>
        public void ResetValue (object? property, ValueResetFlags flags) { }

        /// <summary>Property token for whether the element is enabled. Compat for <c>RadElement.EnabledProperty</c>.</summary>
        public static readonly RadProperty EnabledProperty = new RadProperty (nameof (Enabled));
    }

    /// <summary>
    /// Name-carrying stand-in for a Telerik dependency-property token (e.g. <c>LightVisualElement.BackColorProperty</c>).
    /// Used only as an opaque argument to <see cref="RadElement.ResetValue(object?, ValueResetFlags)"/>.
    /// </summary>
    public class RadProperty
    {
        internal RadProperty (string name) => Name = name;

        /// <summary>Gets the property name.</summary>
        public string Name { get; }
    }

    /// <summary>
    /// Lightweight stand-in for Telerik's <c>VisualElement</c> (the base of the styleable element tree).
    /// Exposes the commonly-accessed appearance members as settable no-ops, plus the static property
    /// tokens (<see cref="TextProperty"/>, etc.) used with <see cref="RadElement.ResetValue(object?, ValueResetFlags)"/>.
    /// </summary>
    public class VisualElement : RadElement
    {
        /// <summary>Gets or sets the element font.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }
        /// <summary>Gets or sets the text alignment.</summary>
        public ContentAlignment TextAlignment { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>Property token for the element's text. Compat for <c>VisualElement.TextProperty</c>. (Text itself lives on <see cref="RadItem"/>, the common derived class that carries text.)</summary>
        public static readonly RadProperty TextProperty = new RadProperty ("Text");
        /// <summary>Property token for the element's font. Compat for <c>VisualElement.FontProperty</c> / <c>LightVisualElement.FontProperty</c>.</summary>
        public static readonly RadProperty FontProperty = new RadProperty (nameof (Font));
        /// <summary>Property token for the element's foreground color. Compat for <c>...ForeColorProperty</c>.</summary>
        public static readonly RadProperty ForeColorProperty = new RadProperty (nameof (ForeColor));
        /// <summary>Property token for the element's background color. Compat for <c>...BackColorProperty</c>.</summary>
        public static readonly RadProperty BackColorProperty = new RadProperty (nameof (BackColor));
        /// <summary>Property token for whether the element draws its fill. Compat for <c>...DrawFillProperty</c>.</summary>
        public static readonly RadProperty DrawFillProperty = new RadProperty ("DrawFill");
        /// <summary>Property token for the element's gradient style. Compat for <c>...GradientStyleProperty</c>.</summary>
        public static readonly RadProperty GradientStyleProperty = new RadProperty ("GradientStyle");
    }

    /// <summary>
    /// Compat alias for Telerik's <c>LightVisualElement</c> — code frequently resets properties via
    /// <c>LightVisualElement.FontProperty</c> etc. regardless of the concrete element type. Same shape
    /// as <see cref="VisualElement"/> (the property tokens are inherited).
    /// </summary>
    public class LightVisualElement : VisualElement { }

    /// <summary>Compat for Telerik's <c>RootRadElement</c> (the top-level element of a control's element tree).</summary>
    public class RootRadElement : RadElement { }

    /// <summary>
    /// Lightweight stand-in for Telerik's <c>RadItem</c> (a clickable/text-bearing element such as a
    /// button or menu item).
    /// </summary>
    public class RadItem : VisualElement
    {
        /// <summary>Gets or sets the displayed text.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Gets or sets the name of the item.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets an object with additional user data about the item.</summary>
        public object? Tag { get; set; }
        /// <summary>Gets or sets the tooltip text shown for this item.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Raised when the item is clicked.</summary>
        public event EventHandler? Click;

        /// <summary>Simulates a click on the item, raising <see cref="Click"/>.</summary>
        public void PerformClick () => Click?.Invoke (this, EventArgs.Empty);
    }

    /// <summary>Lightweight stand-in for Telerik's <c>RadButtonElement</c> (a button hosted in an element tree, e.g. a grid command cell).</summary>
    public class RadButtonElement : RadItem
    {
        /// <summary>Initializes a new instance of the RadButtonElement class.</summary>
        public RadButtonElement () { }
        /// <summary>Initializes a new instance of the RadButtonElement class with the specified text.</summary>
        public RadButtonElement (string text) => Text = text;

        /// <summary>Gets or sets how the button's image/text are displayed.</summary>
        public DisplayStyle DisplayStyle { get; set; } = DisplayStyle.ImageAndText;
        /// <summary>Gets or sets the button image.</summary>
        public Majorsilence.Forms.Drawing.Image? Image { get; set; }
        /// <summary>Gets or sets how the image is scaled. Stub.</summary>
        public ImageScaling ImageScaling { get; set; } = ImageScaling.None;
        /// <summary>Gets or sets whether the button border is drawn. Stub.</summary>
        public bool ShowBorder { get; set; } = true;
        /// <summary>Gets the fill element used for gradient/back-color resets (e.g. <c>CommandButton.ButtonFillElement</c>). Stub.</summary>
        public LightVisualElement ButtonFillElement { get; } = new LightVisualElement ();
    }

    /// <summary>Lightweight stand-in for Telerik's <c>RadLabelElement</c> (a text-only element hosted in an element tree).</summary>
    public class RadLabelElement : RadItem { }

    /// <summary>Specifies the visibility of a Telerik element. Compat for ElementVisibility.</summary>
    public enum ElementVisibility
    {
        /// <summary>The element is visible.</summary>
        Visible = 0,
        /// <summary>The element is hidden but reserves layout space.</summary>
        Hidden = 1,
        /// <summary>The element is collapsed and reserves no space.</summary>
        Collapsed = 2
    }

    /// <summary>Specifies which parts of a property's value are reset by <see cref="RadElement.ResetValue(object?, ValueResetFlags)"/>. Compat for Telerik ValueResetFlags.</summary>
    [Flags]
    public enum ValueResetFlags
    {
        /// <summary>Reset nothing.</summary>
        None = 0,
        /// <summary>Reset the locally-set value.</summary>
        Local = 1,
        /// <summary>Reset an in-progress animation.</summary>
        Animation = 2,
        /// <summary>Reset a data-bound value.</summary>
        Binding = 4,
        /// <summary>Reset an inherited value.</summary>
        Inherited = 8,
        /// <summary>Reset a theme-supplied setting.</summary>
        ThemeSetting = 16,
        /// <summary>Reset all of the above.</summary>
        All = Local | Animation | Binding | Inherited | ThemeSetting
    }

    /// <summary>Specifies a gradient fill style. Compat for Telerik GradientStyles.</summary>
    public enum GradientStyles
    {
        /// <summary>No gradient; a solid fill.</summary>
        Solid = 0,
        /// <summary>A linear gradient.</summary>
        Linear = 1,
        /// <summary>A glass-style gradient.</summary>
        Glass = 2,
        /// <summary>A gradient with a highlight band.</summary>
        Highlight = 3
    }

    /// <summary>Specifies how an item's image and text are displayed. Compat for Telerik DisplayStyle.</summary>
    public enum DisplayStyle
    {
        /// <summary>Neither image nor text is shown.</summary>
        None = 0,
        /// <summary>Only the text is shown.</summary>
        Text = 1,
        /// <summary>Only the image is shown.</summary>
        Image = 2,
        /// <summary>Both the image and text are shown.</summary>
        ImageAndText = 3
    }

    /// <summary>Specifies how an image is scaled within its element. Compat for Telerik ImageScaling.</summary>
    public enum ImageScaling
    {
        /// <summary>No scaling; the image is drawn at its native size.</summary>
        None = 0,
        /// <summary>The image is stretched to fill the available space.</summary>
        Stretch = 1,
        /// <summary>The image is scaled uniformly to fit the available space.</summary>
        Zoom = 2,
        /// <summary>The image is clipped to the available space.</summary>
        Clip = 3
    }

    /// <summary>Specifies vertical alignment. Compat for Telerik RadVerticalAlignment.</summary>
    public enum RadVerticalAlignment
    {
        /// <summary>Aligned to the top.</summary>
        Top = 0,
        /// <summary>Aligned to the middle.</summary>
        Middle = 1,
        /// <summary>Aligned to the bottom.</summary>
        Bottom = 2,
        /// <summary>Stretched to fill the available space.</summary>
        Stretch = 3
    }

    /// <summary>Specifies when a scrollbar-like element is shown. Compat for Telerik ScrollState.</summary>
    public enum ScrollState
    {
        /// <summary>Always shown.</summary>
        AlwaysShow = 0,
        /// <summary>Shown only when needed, hidden otherwise.</summary>
        AutoHide = 1,
        /// <summary>Always hidden.</summary>
        AlwaysHide = 2
    }

    /// <summary>Specifies a two- or three-state toggle value. Compat for Telerik ToggleState.</summary>
    public enum ToggleState
    {
        /// <summary>The off/unchecked state.</summary>
        Off = 0,
        /// <summary>The on/checked state.</summary>
        On = 1,
        /// <summary>The indeterminate state.</summary>
        Indeterminate = 2
    }

    /// <summary>Specifies how a toggle control changes state. Compat for ToggleStateMode.</summary>
    public enum ToggleStateMode
    {
        /// <summary>Toggle on each click.</summary>
        Click = 0,
        /// <summary>Toggle on press.</summary>
        Press = 1
    }

    /// <summary>Specifies the visual style of a RadWaitingBar. Compat for WaitingBarStyles.</summary>
    public enum WaitingBarStyles
    {
        /// <summary>A single dot/indicator travels across the bar.</summary>
        Dash = 0,
        /// <summary>A block of indicators travels across the bar.</summary>
        DataCloud = 1,
        /// <summary>Indicators rotate.</summary>
        Rotate = 2,
        /// <summary>Dots orbit in a spinner arrangement.</summary>
        DotsSpinner = 3
    }

}
