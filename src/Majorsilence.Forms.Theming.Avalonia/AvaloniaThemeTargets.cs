using System.Collections.Generic;

namespace Majorsilence.Forms.Theming.Avalonia
{
    // Inside the namespace, so Avalonia's control types win over the Majorsilence.Forms types of the
    // same name (Button, Label, TextBox...) that the enclosing namespace would otherwise find first.
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Presenters;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Styling;

    /// <summary>The CLR type a Fluent theme resource is declared as; the applier writes the same type.</summary>
    internal enum ResourceKind
    {
        /// <summary>An <c>IBrush</c> (written as a <c>SolidColorBrush</c>).</summary>
        Brush,

        /// <summary>A <c>Color</c>.</summary>
        Color,

        /// <summary>A <c>double</c> (a length in device-independent pixels).</summary>
        Double
    }

    /// <summary>Which border side a width declaration sets; <see cref="All"/> is <c>border-width</c>.</summary>
    internal enum BorderSide
    {
        All,
        Top,
        Right,
        Bottom,
        Left
    }

    /// <summary>Where one declaration lands in Avalonia: a Fluent resource key or a style setter.</summary>
    internal abstract class AvaloniaTarget
    {
    }

    /// <summary>
    /// A theme resource the Fluent control themes read through <c>{DynamicResource}</c>. Writing it
    /// into the application's resources restyles every state that uses it, which is the only reliable
    /// seam for hover and selection colours: the control themes set those on template parts, where an
    /// application style on the control itself cannot reach.
    /// </summary>
    internal sealed class ResourceTarget : AvaloniaTarget
    {
        public ResourceTarget (string key, ResourceKind kind, bool pressed = false, object? fixedValue = null)
        {
            Key = key;
            Kind = kind;
            Pressed = pressed;
            FixedValue = fixedValue;
        }

        public string Key { get; }

        public ResourceKind Kind { get; }

        /// <summary>A pressed-state key: a background colour is darkened a little so a press still reads.</summary>
        public bool Pressed { get; }

        /// <summary>
        /// A value written whenever the declaration is present, instead of the declared one -- for a
        /// companion resource that must change with it (the DataGrid selection opacities).
        /// </summary>
        public object? FixedValue { get; }
    }

    /// <summary>
    /// A setter in the generated <see cref="Styles"/>. Application styles outrank control-theme setters,
    /// so these win over Fluent's defaults for the properties the control template binds.
    /// </summary>
    internal sealed class StyleTarget : AvaloniaTarget
    {
        public StyleTarget (StyleSelector selector, AvaloniaProperty property, BorderSide side = BorderSide.All)
        {
            Selector = selector;
            Property = property;
            Side = side;
        }

        public StyleSelector Selector { get; }

        public AvaloniaProperty Property { get; }

        /// <summary>For <c>BorderThickness</c>: which side the declaration sets.</summary>
        public BorderSide Side { get; }
    }

    /// <summary>One style selector of the generated sheet, with a stable key so setters merge into one style.</summary>
    internal sealed class StyleSelector
    {
        public StyleSelector (string key, Func<Selector?, Selector> build)
        {
            Key = key;
            Build = build;
        }

        /// <summary>The selector in Avalonia syntax, for diagnostics and the support matrix.</summary>
        public string Key { get; }

        public Func<Selector?, Selector> Build { get; }

        public override string ToString () => Key;
    }

    /// <summary>The selectors the matrix uses, built once.</summary>
    internal static class S
    {
        public static readonly StyleSelector Window = new ("Window", x => x.OfType<Window> ());
        public static readonly StyleSelector Button = new ("Button", x => x.OfType<Button> ());
        public static readonly StyleSelector ToggleButton = new ("ToggleButton", x => x.OfType<ToggleButton> ());
        public static readonly StyleSelector CheckBox = new ("CheckBox", x => x.OfType<CheckBox> ());
        public static readonly StyleSelector RadioButton = new ("RadioButton", x => x.OfType<RadioButton> ());
        public static readonly StyleSelector ComboBox = new ("ComboBox", x => x.OfType<ComboBox> ());
        public static readonly StyleSelector TextBox = new ("TextBox", x => x.OfType<TextBox> ());
        public static readonly StyleSelector DatePicker = new ("CalendarDatePicker", x => x.OfType<CalendarDatePicker> ());
        public static readonly StyleSelector NumericUpDown = new ("NumericUpDown", x => x.OfType<NumericUpDown> ());
        public static readonly StyleSelector Label = new ("Label", x => x.OfType<Label> ());
        public static readonly StyleSelector Hyperlink = new ("HyperlinkButton", x => x.OfType<HyperlinkButton> ());
        public static readonly StyleSelector ListBox = new ("ListBox", x => x.OfType<ListBox> ());
        public static readonly StyleSelector ListBoxItemSelected = new ("ListBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter",
            x => x.OfType<ListBoxItem> ().Class (":selected").Template ().OfType<ContentPresenter> ().Name ("PART_ContentPresenter"));
        public static readonly StyleSelector TreeView = new ("TreeView", x => x.OfType<TreeView> ());
        public static readonly StyleSelector Menu = new ("Menu", x => x.OfType<Menu> ());
        public static readonly StyleSelector MenuBarItem = new ("Menu > MenuItem", x => x.OfType<Menu> ().Child ().OfType<MenuItem> ());
        public static readonly StyleSelector MenuBarItemHover = new ("Menu > MenuItem:pointerover /template/ Border#PART_LayoutRoot",
            x => x.OfType<Menu> ().Child ().OfType<MenuItem> ().Class (":pointerover").Template ().OfType<Border> ().Name ("PART_LayoutRoot"));
        public static readonly StyleSelector MenuBarItemHoverText = new ("Menu > MenuItem:pointerover /template/ ContentPresenter#PART_HeaderPresenter",
            x => x.OfType<Menu> ().Child ().OfType<MenuItem> ().Class (":pointerover").Template ().OfType<ContentPresenter> ().Name ("PART_HeaderPresenter"));
        public static readonly StyleSelector MenuFlyout = new ("MenuFlyoutPresenter", x => x.OfType<MenuFlyoutPresenter> ());
        public static readonly StyleSelector ContextMenu = new ("ContextMenu", x => x.OfType<ContextMenu> ());
        public static readonly StyleSelector Calendar = new ("Calendar", x => x.OfType<Calendar> ());
        public static readonly StyleSelector ScrollBar = new ("ScrollBar", x => x.OfType<ScrollBar> ());
        public static readonly StyleSelector GridSplitter = new ("GridSplitter", x => x.OfType<GridSplitter> ());
        public static readonly StyleSelector TabControl = new ("TabControl", x => x.OfType<TabControl> ());
        public static readonly StyleSelector TabItem = new ("TabItem", x => x.OfType<TabItem> ());
        public static readonly StyleSelector Slider = new ("Slider", x => x.OfType<Slider> ());
        public static readonly StyleSelector DataGrid = new ("DataGrid", x => x.OfType<DataGrid> ());
        public static readonly StyleSelector DataGridColumnHeader = new ("DataGridColumnHeader", x => x.OfType<DataGridColumnHeader> ());
        public static readonly StyleSelector DataGridRowHeader = new ("DataGridRowHeader", x => x.OfType<DataGridRowHeader> ());
        public static readonly StyleSelector DataGridRowSelected = new ("DataGridRow:selected", x => x.OfType<DataGridRow> ().Class (":selected"));
        public static readonly StyleSelector DataGridRowAlternate = new ("DataGridRow:nth-child(2n)", x => x.OfType<DataGridRow> ().NthChild (2, 0));
    }

    /// <summary>The <c>TemplatedControl</c> properties rule longhands map onto.</summary>
    internal static class P
    {
        public static AvaloniaProperty Background => TemplatedControl.BackgroundProperty;
        public static AvaloniaProperty Foreground => TemplatedControl.ForegroundProperty;
        public static AvaloniaProperty BorderBrush => TemplatedControl.BorderBrushProperty;
        public static AvaloniaProperty BorderThickness => TemplatedControl.BorderThicknessProperty;
        public static AvaloniaProperty CornerRadius => TemplatedControl.CornerRadiusProperty;
        public static AvaloniaProperty FontFamily => TemplatedControl.FontFamilyProperty;
        public static AvaloniaProperty FontSize => TemplatedControl.FontSizeProperty;
        public static AvaloniaProperty FontWeight => TemplatedControl.FontWeightProperty;
        public static AvaloniaProperty FontStyle => TemplatedControl.FontStyleProperty;

        /// <summary>The TemplatedControl property a plain longhand maps to, or null for per-side colours.</summary>
        public static AvaloniaProperty? For (string property) => property switch {
            "background-color" => Background,
            "color" => Foreground,
            "border-color" => BorderBrush,
            "border-width" or "border-top-width" or "border-right-width" or "border-bottom-width" or "border-left-width" => BorderThickness,
            "border-radius" => CornerRadius,
            "font-family" => FontFamily,
            "font-size" => FontSize,
            "font-weight" => FontWeight,
            "font-style" => FontStyle,
            _ => null
        };

        public static BorderSide SideOf (string property) => property switch {
            "border-top-width" or "border-top-color" => BorderSide.Top,
            "border-right-width" or "border-right-color" => BorderSide.Right,
            "border-bottom-width" or "border-bottom-color" => BorderSide.Bottom,
            "border-left-width" or "border-left-color" => BorderSide.Left,
            _ => BorderSide.All
        };
    }

    internal static class Targets
    {
        public static IReadOnlyList<AvaloniaTarget> Brushes (params string[] keys)
        {
            var list = new List<AvaloniaTarget> ();
            foreach (var key in keys)
                list.Add (new ResourceTarget (key, ResourceKind.Brush, key.EndsWith ("Pressed", StringComparison.Ordinal)));
            return list;
        }
    }
}
