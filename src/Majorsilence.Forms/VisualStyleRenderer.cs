using System;
using System.Drawing;
using Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.VisualStyles
{
    /// <summary>
    /// Names one part-and-state of a themed control, as <c>System.Windows.Forms.VisualStyles</c> does.
    /// </summary>
    /// <remarks>
    /// On Windows these identify a part of a visual-style (msstyles) theme to draw. There is no such theme
    /// engine here — controls are drawn by this library's own renderers — so an element is a name that a
    /// <see cref="VisualStyleRenderer"/> resolves to nothing. The type exists because code that draws a
    /// themed grip or arrow names an element to do it, and that code has to compile.
    ///
    /// The nested groups mirror the upstream class names and carry the elements this library has been asked
    /// for so far; the parts are grouped the same way upstream, so more can be added without changing shape.
    /// </remarks>
    public class VisualStyleElement
    {
        private VisualStyleElement (string className, int part, int state)
        {
            ClassName = className;
            Part = part;
            State = state;
        }

        /// <summary>Gets the visual-style class this element belongs to.</summary>
        public string ClassName { get; }

        /// <summary>Gets the part within the class.</summary>
        public int Part { get; }

        /// <summary>Gets the state of the part.</summary>
        public int State { get; }

        /// <summary>Creates an element from a class name, part and state.</summary>
        public static VisualStyleElement CreateElement (string className, int part, int state) =>
            new VisualStyleElement (className, part, state);

        /// <summary>Elements of the status bar.</summary>
        public static class Status
        {
            /// <summary>The resize grip in the corner of a status bar.</summary>
            public static class Gripper
            {
                /// <summary>The grip in its normal state.</summary>
                public static VisualStyleElement Normal { get; } = CreateElement ("STATUS", 3, 0);
            }

            /// <summary>A pane of a status bar.</summary>
            public static class Pane
            {
                /// <summary>The pane in its normal state.</summary>
                public static VisualStyleElement Normal { get; } = CreateElement ("STATUS", 1, 0);
            }

            /// <summary>The status bar itself.</summary>
            public static class Bar
            {
                /// <summary>The bar in its normal state.</summary>
                public static VisualStyleElement Normal { get; } = CreateElement ("STATUS", 0, 0);
            }
        }
    }

    /// <summary>
    /// Draws one part of a themed control — the cross-platform stand-in for
    /// <c>System.Windows.Forms.VisualStyles.VisualStyleRenderer</c>.
    /// </summary>
    /// <remarks>
    /// Per the stub policy, the drawing members no-op rather than throw: there is no msstyles theme engine
    /// off Windows, so a control that asks for a themed grip gets nothing drawn instead of an exception, and
    /// keeps its own painting for everything else. <see cref="IsSupported"/> reports false, which is the
    /// documented way for calling code to choose its own fallback drawing — code that checks it will take
    /// that path and never reach the no-ops.
    /// </remarks>
    public class VisualStyleRenderer
    {
        /// <summary>Initializes the renderer for the given element.</summary>
        public VisualStyleRenderer (VisualStyleElement element)
        {
            ArgumentNullException.ThrowIfNull (element);

            Class = element.ClassName;
            Part = element.Part;
            State = element.State;
        }

        /// <summary>Initializes the renderer for the given class, part and state.</summary>
        public VisualStyleRenderer (string className, int part, int state)
        {
            Class = className;
            Part = part;
            State = state;
        }

        /// <summary>Gets whether visual styles are available to draw with. Always false here.</summary>
        public static bool IsSupported => false;

        /// <summary>Gets the visual-style class being drawn.</summary>
        public string Class { get; private set; }

        /// <summary>Gets the part being drawn.</summary>
        public int Part { get; private set; }

        /// <summary>Gets the state being drawn.</summary>
        public int State { get; private set; }

        /// <summary>Returns whether the given element is defined by the current theme. Always false here.</summary>
        public static bool IsElementDefined (VisualStyleElement element) => false;

        /// <summary>Points the renderer at a different element.</summary>
        public void SetParameters (VisualStyleElement element)
        {
            ArgumentNullException.ThrowIfNull (element);

            Class = element.ClassName;
            Part = element.Part;
            State = element.State;
        }

        /// <inheritdoc cref="SetParameters(VisualStyleElement)"/>
        public void SetParameters (string className, int part, int state)
        {
            Class = className;
            Part = part;
            State = state;
        }

        /// <summary>Draws the element's background into the given bounds.</summary>
        public void DrawBackground (Graphics g, Rectangle bounds) { }

        /// <inheritdoc cref="DrawBackground(Graphics, Rectangle)"/>
        public void DrawBackground (Graphics g, Rectangle bounds, Rectangle clipRectangle) { }

        /// <summary>Draws the element's edge, returning the area inside it.</summary>
        public Rectangle DrawEdge (Graphics g, Rectangle bounds, Edges edges, EdgeStyle style, EdgeEffects effects)
            => bounds;

        /// <summary>Returns the content area inside the element's background.</summary>
        public Rectangle GetBackgroundContentRectangle (Graphics g, Rectangle bounds) => bounds;

        /// <summary>Returns the area the element's background needs for the given content.</summary>
        public Rectangle GetBackgroundExtent (Graphics g, Rectangle contentBounds) => contentBounds;

        /// <summary>Returns the element's preferred size.</summary>
        public Size GetPartSize (Graphics g, ThemeSizeType type) => Size.Empty;
    }

    /// <summary>
    /// Describes the visual style (msstyles theme) in force — the cross-platform stand-in for
    /// <c>System.Windows.Forms.VisualStyles.VisualStyleInformation</c>.
    /// </summary>
    /// <remarks>
    /// There is no msstyles engine here, so there is no style in force to describe:
    /// <see cref="IsEnabledByUser"/> reports false and the descriptive members report empty. That is the
    /// answer callers are written for — the upstream pattern is to check <see cref="IsEnabledByUser"/> (or
    /// test <see cref="ColorScheme"/> for emptiness) and fall back to a palette of their own, which is
    /// exactly what this library wants them to do. <see cref="VisualStyleRenderer.IsSupported"/> agrees.
    /// </remarks>
    public static class VisualStyleInformation
    {
        /// <summary>Gets whether visual styles are available on this machine. Always false.</summary>
        public static bool IsSupportedByOS => false;

        /// <summary>Gets whether the user has visual styles turned on. Always false.</summary>
        public static bool IsEnabledByUser => false;

        /// <summary>Gets the name of the colour scheme within the theme. Always empty.</summary>
        /// <remarks>On Windows this is what distinguishes the Blue, Olive and Silver variants of a theme,
        /// and callers switch a palette on it; empty means "no variant", so they keep their default.</remarks>
        public static string ColorScheme => string.Empty;

        /// <summary>Gets the theme's display name. Always empty.</summary>
        public static string DisplayName => string.Empty;

        /// <summary>Gets the theme author. Always empty.</summary>
        public static string Author => string.Empty;

        /// <summary>Gets the theme's company. Always empty.</summary>
        public static string Company => string.Empty;

        /// <summary>Gets a description of the theme. Always empty.</summary>
        public static string Description => string.Empty;

        /// <summary>Gets the path to the theme file. Always empty.</summary>
        public static string ThemeFilename => string.Empty;

        /// <summary>Gets the theme's size name. Always empty.</summary>
        public static string Size => string.Empty;

        /// <summary>Gets the theme's version. Always empty.</summary>
        public static string Version => string.Empty;

        /// <summary>Gets the smallest font the theme uses. Always empty.</summary>
        public static string MinimumColorDepth => string.Empty;

        /// <summary>Gets whether the theme supports flat menus. Always false.</summary>
        public static bool SupportsFlatMenus => false;

        /// <summary>Gets the colour the theme draws a text box's border in.</summary>
        /// <remarks>Answers from <see cref="SystemColors.WindowFrame"/>, so a control that uses it to
        /// outline a box gets a border that matches the palette actually in force rather than nothing.</remarks>
        public static Color TextControlBorder => SystemColors.WindowFrame;

        /// <summary>Gets the colour the theme highlights a hovered item with.</summary>
        /// <inheritdoc cref="TextControlBorder"/>
        public static Color ControlHighlightHot => SystemColors.Highlight;
    }

    /// <summary>Which edges of an element to draw.</summary>
    [Flags]
    public enum Edges
    {
        /// <summary>The left edge.</summary>
        Left = 1,
        /// <summary>The top edge.</summary>
        Top = 2,
        /// <summary>The right edge.</summary>
        Right = 4,
        /// <summary>The bottom edge.</summary>
        Bottom = 8,
        /// <summary>The diagonal.</summary>
        Diagonal = 0x10,
    }

    /// <summary>How an element's edge is drawn.</summary>
    public enum EdgeStyle
    {
        /// <summary>Raised outer, sunken inner.</summary>
        Raised = 5,
        /// <summary>Sunken outer, raised inner.</summary>
        Sunken = 10,
        /// <summary>Etched.</summary>
        Etched = 6,
        /// <summary>Bumped.</summary>
        Bump = 9,
    }

    /// <summary>Extra effects applied when drawing an element's edge.</summary>
    [Flags]
    public enum EdgeEffects
    {
        /// <summary>No effect.</summary>
        None = 0,
        /// <summary>Fill the interior.</summary>
        FillInterior = 0x0800,
        /// <summary>Draw flat.</summary>
        Flat = 0x1000,
        /// <summary>Draw soft.</summary>
        Soft = 0x1000,
        /// <summary>Draw only the monochrome edge.</summary>
        Mono = 0x8000,
    }

    /// <summary>Which size of a themed part to measure.</summary>
    public enum ThemeSizeType
    {
        /// <summary>The minimum size.</summary>
        Minimum = 0,
        /// <summary>The size the part was authored at.</summary>
        True = 1,
        /// <summary>The size to draw at.</summary>
        Draw = 2,
    }
}
