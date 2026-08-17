using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    public partial class Control
    {
        /// <summary>
        /// Asks another control to paint itself onto the supplied surface.
        /// </summary>
        /// <remarks>
        /// How a control fakes transparency: it renders its parent into a bitmap, draws that as its own
        /// background, then draws itself on top. WinForms exposes this as a protected member so a control
        /// can reach into a sibling's painting, which is why it takes the target as a parameter.
        /// </remarks>
        protected void InvokePaint (Control c, PaintEventArgs e)
        {
            ArgumentNullException.ThrowIfNull (c);
            c.OnPaint (e);
        }

        /// <summary>Asks another control to paint its background onto the supplied surface.</summary>
        /// <inheritdoc cref="InvokePaint"/>
        protected void InvokePaintBackground (Control c, PaintEventArgs e)
        {
            ArgumentNullException.ThrowIfNull (c);
            c.OnPaintBackground (e);
        }
    }

    /// <summary>
    /// WinForms compatibility: draws a group box in the current visual style.
    /// </summary>
    /// <remarks>
    /// Sits in the root namespace, where WinForms puts it -- ported code says <c>GroupBoxRenderer.Draw…</c>
    /// with no using beyond Majorsilence.Forms. Rendering goes through <see cref="ControlPaint"/> so the
    /// output matches a GroupBox drawn by this library rather than a Windows theme this platform has no
    /// access to.
    /// </remarks>
    public static class GroupBoxRenderer
    {
        /// <summary>Gets or sets whether the renderer follows the application's visual-style state.</summary>
        public static bool RenderMatchingApplicationState { get; set; } = true;

        /// <summary>Draws the background of the group box's parent behind it.</summary>
        /// <remarks>No-op: parent backgrounds are already painted before children here, so a renderer
        /// asking for one again would double-draw.</remarks>
        public static void DrawParentBackground (Graphics g, Rectangle bounds, Control childControl) { }

        /// <summary>Draws a group box with no caption.</summary>
        public static void DrawGroupBox (Graphics g, Rectangle bounds, VisualStyles.GroupBoxState state) =>
            DrawGroupBox (g, bounds, string.Empty, null, Color.Empty, TextFormatFlags.Left, state);

        /// <summary>Draws a group box with the specified caption.</summary>
        public static void DrawGroupBox (Graphics g, Rectangle bounds, string? groupBoxText,
            Majorsilence.Forms.Drawing.Font? font, VisualStyles.GroupBoxState state) =>
            DrawGroupBox (g, bounds, groupBoxText, font, Color.Empty, TextFormatFlags.Left, state);

        /// <summary>Draws a group box with the specified caption, colour and text formatting.</summary>
        public static void DrawGroupBox (Graphics g, Rectangle bounds, string? groupBoxText,
            Majorsilence.Forms.Drawing.Font? font, Color textColor, TextFormatFlags flags,
            VisualStyles.GroupBoxState state)
        {
            ArgumentNullException.ThrowIfNull (g);

            // Theme.UIFont is a typeface, not a Font -- build the drawable default from it.
            var caption_font = font ?? new Majorsilence.Forms.Drawing.Font (Theme.UIFont.FamilyName, Theme.ItemFontSize);

            // The caption sits on the border line, so the frame starts half a line-height down and the
            // text is punched out of it -- otherwise the border runs straight through the letters.
            var caption_height = string.IsNullOrEmpty (groupBoxText) ? 0
                : (int)Math.Ceiling (g.MeasureString (groupBoxText, caption_font).Height);
            var frame = bounds with {
                Y = bounds.Y + caption_height / 2,
                Height = Math.Max (0, bounds.Height - caption_height / 2),
            };

            ControlPaint.DrawBorder (g, frame, SystemColors.ControlDark, ButtonBorderStyle.Solid);

            if (string.IsNullOrEmpty (groupBoxText))
                return;

            var text_size = g.MeasureString (groupBoxText, caption_font);
            var text_bounds = new Rectangle (bounds.X + 8, bounds.Y,
                (int)Math.Ceiling (text_size.Width), caption_height);

            using (var background = new Majorsilence.Forms.Drawing.SolidBrush (SystemColors.Control))
                g.FillRectangle (background, text_bounds);

            var colour = textColor == Color.Empty ? SystemColors.ControlText : textColor;
            using (var brush = new Majorsilence.Forms.Drawing.SolidBrush (colour))
                g.DrawString (groupBoxText, caption_font, brush, text_bounds.X, text_bounds.Y);
        }
    }
}

namespace Majorsilence.Forms.VisualStyles
{
    /// <summary>Specifies the visual state of a group box.</summary>
    public enum GroupBoxState
    {
        /// <summary>The group box is enabled.</summary>
        Normal = 1,
        /// <summary>The group box is disabled.</summary>
        Disabled = 2
    }

    public partial class VisualStyleElement
    {
        /// <summary>Elements of a tab control.</summary>
        public static class Tab
        {
            /// <summary>The body of the tab control, below the tabs.</summary>
            public static class Pane
            {
                /// <summary>The pane in its normal state.</summary>
                public static VisualStyleElement Normal { get; } = CreateElement ("TAB", 9, 0);
            }

            /// <summary>An individual tab.</summary>
            public static class TabItem
            {
                /// <summary>The tab in its normal state.</summary>
                public static VisualStyleElement Normal { get; } = CreateElement ("TAB", 1, 1);
                /// <summary>The tab under the mouse.</summary>
                public static VisualStyleElement Hot { get; } = CreateElement ("TAB", 1, 2);
                /// <summary>The tab being pressed.</summary>
                public static VisualStyleElement Pressed { get; } = CreateElement ("TAB", 1, 3);
            }
        }

        /// <summary>Elements of a tree view.</summary>
        public static class TreeView
        {
            /// <summary>The expand/collapse glyph beside a node.</summary>
            public static class Glyph
            {
                /// <summary>The glyph of a collapsed node.</summary>
                public static VisualStyleElement Closed { get; } = CreateElement ("TREEVIEW", 2, 1);
                /// <summary>The glyph of an expanded node.</summary>
                public static VisualStyleElement Opened { get; } = CreateElement ("TREEVIEW", 2, 2);
            }
        }
    }
}

namespace Majorsilence.Forms.Design
{
    /// <summary>
    /// WinForms compatibility: provides data for a UI type editor's <c>PaintValue</c> callback.
    /// </summary>
    /// <remarks>
    /// Design-time only. A property editor overrides PaintValue to draw a swatch beside the value in the
    /// property grid -- an image-index editor draws the image, a colour editor a colour chip. Nothing
    /// invokes it at runtime, but the override has to compile for the control to build.
    /// </remarks>
    public class PaintValueEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public PaintValueEventArgs (System.ComponentModel.ITypeDescriptorContext? context, object? value,
            Graphics graphics, Rectangle bounds)
        {
            ArgumentNullException.ThrowIfNull (graphics);
            Context = context;
            Value = value;
            Graphics = graphics;
            Bounds = bounds;
        }

        /// <summary>Gets the descriptor context the value was reached through.</summary>
        public System.ComponentModel.ITypeDescriptorContext? Context { get; }

        /// <summary>Gets the value being painted.</summary>
        public object? Value { get; }

        /// <summary>Gets the surface to paint on.</summary>
        public Graphics Graphics { get; }

        /// <summary>Gets the rectangle to paint within.</summary>
        public Rectangle Bounds { get; }
    }

    /// <summary>
    /// WinForms compatibility: represents an item on the designer toolbox.
    /// </summary>
    /// <remarks>
    /// Design-time only, and there is no designer host here -- a control library subclasses this to control
    /// how dropping it on a form generates code. It exists so those subclasses compile.
    /// </remarks>
    public class ToolboxItem
    {
        /// <summary>Initializes a new instance.</summary>
        public ToolboxItem () { }

        /// <summary>Initializes a new instance for the specified component type.</summary>
        public ToolboxItem (Type? toolType) => TypeName = toolType?.FullName;

        /// <summary>Gets or sets the display name of this toolbox item.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Gets or sets the assembly-qualified name of the type this item creates.</summary>
        public string? TypeName { get; set; }

        /// <summary>Gets or sets the bitmap shown beside this item on the toolbox.</summary>
        public Bitmap? Bitmap { get; set; }

        /// <summary>Gets or sets whether this item's properties can still be changed.</summary>
        public bool Locked { get; set; }

        /// <summary>Creates the components this toolbox item represents. Returns none here (stub).</summary>
        public System.ComponentModel.IComponent[] CreateComponents () => CreateComponentsCore (null);

        /// <summary>Restores this item's state from a serialization stream.</summary>
        /// <remarks>Toolbox items are serializable so the designer can cache them; subclasses call this
        /// from their deserializing constructor. Nothing is stored here, so there is nothing to read
        /// back -- but the call has to resolve.</remarks>
        protected virtual void Deserialize (System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) { }

        /// <summary>Saves this item's state to a serialization stream.</summary>
        /// <inheritdoc cref="Deserialize"/>
        protected virtual void Serialize (System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) { }

        /// <summary>Creates the components, given a designer host.</summary>
        /// <remarks>The overridable half: a control library subclasses this to add several related
        /// components at once when its item is dropped on a form. There is no designer host here, so the
        /// base returns nothing.</remarks>
        protected virtual System.ComponentModel.IComponent[] CreateComponentsCore (
            System.ComponentModel.Design.IDesignerHost? host) => [];
    }
}
