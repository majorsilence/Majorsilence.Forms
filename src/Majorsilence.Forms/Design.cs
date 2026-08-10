using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;

namespace Majorsilence.Forms.Design
{
    // Basic design-time support. WinForms spreads this across three namespaces -- ComponentDesigner and
    // CollectionEditor in System.ComponentModel.Design, ControlDesigner/BehaviorService/Glyph/Adorner in
    // System.Windows.Forms.Design, UITypeEditor in System.Drawing.Design -- and a control library carries
    // a designer per control plus adorner glyphs and collection editors for its properties. None of that
    // compiled here at all, so such a library could not be ported without deleting a third of itself.
    //
    // What this is: the types and the shapes, so those designers compile and can be attached with
    // [Designer]/[Editor] as written. What it is NOT: a design surface. There is no Visual Studio host
    // here to create designers, no selection service to drive them, and no adorner window to paint
    // glyphs onto -- so nothing instantiates these at runtime, and the verbs and glyphs a designer
    // registers are never shown. That is the honest boundary: the code survives the migration intact and
    // is ready for a design surface, rather than being deleted and rewritten if one ever arrives.

    /// <summary>The base designer for a component.</summary>
    public class ComponentDesigner : IDesigner
    {
        /// <inheritdoc/>
        public IComponent Component { get; private set; } = null!;

        /// <inheritdoc/>
        public virtual DesignerVerbCollection Verbs { get; } = new DesignerVerbCollection ();

        /// <summary>Gets the properties the designer exposes for the component.</summary>
        protected virtual IDictionary<string, object?> ShadowProperties { get; } = new Dictionary<string, object?> (StringComparer.Ordinal);

        /// <inheritdoc/>
        public virtual void Initialize (IComponent component) => Component = component;

        /// <inheritdoc/>
        public virtual void DoDefaultAction () { }

        /// <summary>Gets a service from the design surface's service container.</summary>
        /// <remarks>Always null: there is no design host to provide one. A designer asking for
        /// IDesignerHost or ISelectionService must null-check the result, which upstream also requires
        /// before the surface is ready.</remarks>
        protected virtual object? GetService (Type serviceType) => null;

        /// <summary>Announces that a component property is about to change.</summary>
        /// <remarks>No-op: the change service that would record it for undo lives in the design host.</remarks>
        protected void RaiseComponentChanging (MemberDescriptor? member) { }

        /// <summary>Announces that a component property has changed.</summary>
        /// <inheritdoc cref="RaiseComponentChanging"/>
        protected void RaiseComponentChanged (MemberDescriptor? member, object? oldValue, object? newValue) { }

        /// <summary>Called after the component is created by the designer.</summary>
        public virtual void InitializeNewComponent (IDictionary<string, object?>? defaultValues) { }

        /// <summary>Releases the designer's resources.</summary>
        protected virtual void Dispose (bool disposing) { }

        /// <inheritdoc/>
        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>Which resize handles a designer offers for its control.</summary>
    [Flags]
    public enum SelectionRules
    {
        /// <summary>No handles.</summary>
        None = 0,
        /// <summary>The control may be moved.</summary>
        Moveable = 0x10000000,
        /// <summary>The control may be resized from its left edge.</summary>
        LeftSizeable = 0x1,
        /// <summary>The control may be resized from its right edge.</summary>
        RightSizeable = 0x2,
        /// <summary>The control may be resized from its top edge.</summary>
        TopSizeable = 0x4,
        /// <summary>The control may be resized from its bottom edge.</summary>
        BottomSizeable = 0x8,
        /// <summary>The control may be resized from any edge.</summary>
        AllSizeable = LeftSizeable | RightSizeable | TopSizeable | BottomSizeable,
        /// <summary>The control is locked in place.</summary>
        Locked = 0x20000000,
        /// <summary>The control is visible on the design surface.</summary>
        Visible = 0x40000000,
    }

    /// <summary>The base designer for a control.</summary>
    public class ControlDesigner : ComponentDesigner
    {
        /// <summary>Gets the control being designed.</summary>
        public virtual Control? Control => Component as Control;

        /// <summary>Gets the behavior service for the design surface hosting this designer.</summary>
        /// <remarks>Null: there is no design surface here. A designer that reaches for it to register
        /// adorners should null-check, exactly as it must upstream before the surface is ready.</remarks>
        protected Majorsilence.Forms.Design.Behavior.BehaviorService? BehaviorService => null;

        /// <summary>Gets which resize handles this control offers.</summary>
        public virtual SelectionRules SelectionRules => SelectionRules.Visible | SelectionRules.Moveable | SelectionRules.AllSizeable;

        /// <summary>Gets whether the designer allows child controls to be dropped onto it.</summary>
        public virtual bool AllowDrop { get; set; }

        /// <summary>Paints the designer's own adornments over the control.</summary>
        protected virtual void OnPaintAdornments (PaintEventArgs pe) { }

        /// <summary>Handles a Windows message routed to the designed control.</summary>
        /// <remarks>Never called: there is no Win32 message pump here (see Control.OnNotifyMessage).</remarks>
        protected virtual void WndProc (ref Message m) { }

        /// <summary>Returns whether the point is inside the designed control for hit-testing purposes.</summary>
        protected virtual bool GetHitTest (Point point) => false;

        /// <summary>Enables or disables the designer's drag handling.</summary>
        protected virtual void EnableDragDrop (bool value) { }
    }

    /// <summary>A designer for a control that can contain other controls.</summary>
    public class ParentControlDesigner : ControlDesigner
    {
        /// <summary>Gets or sets whether the designer draws a grid on its surface.</summary>
        public virtual bool DrawGrid { get; set; }

        /// <summary>Gets the grid spacing used when snapping child controls.</summary>
        protected virtual Size GridSize { get; set; } = new Size (8, 8);
    }


    /// <summary>How a <see cref="UITypeEditor"/> presents itself in the property grid.</summary>
    public enum UITypeEditorEditStyle
    {
        /// <summary>No editor.</summary>
        None = 1,
        /// <summary>A modal dialog.</summary>
        Modal = 2,
        /// <summary>A drop-down.</summary>
        DropDown = 3,
    }

    /// <summary>Edits a property's value in the property grid.</summary>
    public class UITypeEditor
    {
        /// <summary>Gets how this editor presents itself.</summary>
        public virtual UITypeEditorEditStyle GetEditStyle () => UITypeEditorEditStyle.None;

        /// <inheritdoc cref="GetEditStyle()"/>
        public virtual UITypeEditorEditStyle GetEditStyle (ITypeDescriptorContext? context) => GetEditStyle ();

        /// <summary>Edits the value. Returns it unchanged: there is no property grid host to edit in.</summary>
        public virtual object? EditValue (ITypeDescriptorContext? context, IServiceProvider? provider, object? value) => value;

        /// <summary>Gets whether this editor paints a preview of the value.</summary>
        public virtual bool GetPaintValueSupported (ITypeDescriptorContext? context) => false;
    }

    /// <summary>Edits a collection property in the property grid.</summary>
    public class CollectionEditor : UITypeEditor
    {
        /// <summary>Initializes an editor for the given collection type.</summary>
        public CollectionEditor (Type type) => CollectionType = type;

        /// <summary>Gets the collection type being edited.</summary>
        protected Type CollectionType { get; }

        /// <summary>Gets the type of item the collection holds.</summary>
        protected virtual Type CreateCollectionItemType () => typeof (object);

        /// <summary>Gets the item types the editor offers to create.</summary>
        protected virtual Type[] CreateNewItemTypes () => [CreateCollectionItemType ()];

        /// <summary>Creates one new item of the given type.</summary>
        /// <remarks>The annotation is what trimming needs to keep the constructor: the type comes from
        /// CreateNewItemTypes at runtime, so the linker cannot see the instantiation.</remarks>
        protected virtual object? CreateInstance (
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers (
                System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            Type itemType) => Activator.CreateInstance (itemType);

        /// <summary>Gets the items currently in the collection.</summary>
        protected virtual object[] GetItems (object? editValue)
            => editValue is System.Collections.IEnumerable items ? items.Cast<object> ().ToArray () : [];

        /// <summary>Stores the edited items back into the collection.</summary>
        protected virtual object? SetItems (object? editValue, object[] value) => editValue;

        /// <inheritdoc/>
        public override UITypeEditorEditStyle GetEditStyle () => UITypeEditorEditStyle.Modal;
    }
}

namespace Majorsilence.Forms.Design.Behavior
{
    /// <summary>Reacts to input over a <see cref="Glyph"/>.</summary>
    public class Behavior
    {
        /// <summary>Gets the cursor shown while this behavior is active.</summary>
        public virtual Cursor? Cursor => Cursors.Default;

        /// <summary>Gets whether the behavior is a drag-drop source.</summary>
        public virtual bool IsDragDropSource => false;

        /// <summary>Called when a mouse button goes down over the glyph. Returns true when handled.</summary>
        public virtual bool OnMouseDown (Glyph? g, MouseButtons button, Point mouseLoc) => false;

        /// <summary>Called when the pointer moves over the glyph. Returns true when handled.</summary>
        public virtual bool OnMouseMove (Glyph? g, MouseButtons button, Point mouseLoc) => false;

        /// <summary>Called when a mouse button is released over the glyph. Returns true when handled.</summary>
        public virtual bool OnMouseUp (Glyph? g, MouseButtons button) => false;

        /// <summary>Called when the pointer enters the glyph.</summary>
        public virtual bool OnMouseEnter (Glyph? g) => false;

        /// <summary>Called when the pointer leaves the glyph.</summary>
        public virtual bool OnMouseLeave (Glyph? g) => false;
    }

    /// <summary>Something a designer paints onto the design surface, above the control.</summary>
    public abstract class Glyph
    {
        /// <summary>Initializes the glyph with the behavior that handles its input.</summary>
        protected Glyph (Behavior? behavior) => Behavior = behavior;

        /// <summary>Gets the behavior handling input for this glyph.</summary>
        public virtual Behavior? Behavior { get; }

        /// <summary>Gets the glyph's bounds on the design surface.</summary>
        public virtual Rectangle Bounds => Rectangle.Empty;

        /// <summary>Returns the cursor for the given point, or null when the point misses the glyph.</summary>
        public abstract Cursor? GetHitTest (Point p);

        /// <summary>Paints the glyph.</summary>
        public abstract void Paint (PaintEventArgs pe);

        /// <summary>Sets the behavior handling input for this glyph.</summary>
        protected void SetBehavior (Behavior? behavior) { }
    }

    /// <summary>A collection of <see cref="Glyph"/>.</summary>
    public class GlyphCollection : Collection<Glyph>
    {
        /// <summary>Adds several glyphs at once.</summary>
        public void AddRange (IEnumerable<Glyph> glyphs)
        {
            ArgumentNullException.ThrowIfNull (glyphs);

            foreach (var glyph in glyphs)
                Add (glyph);
        }
    }

    /// <summary>A layer of glyphs painted over the design surface.</summary>
    public class Adorner
    {
        /// <summary>Gets the glyphs in this layer.</summary>
        public GlyphCollection Glyphs { get; } = new GlyphCollection ();

        /// <summary>Gets or sets whether the layer is painted and hit-tested.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the behavior service this layer belongs to.</summary>
        public BehaviorService? BehaviorService { get; set; }

        /// <summary>Requests a repaint of this layer.</summary>
        public void Invalidate () { }
    }

    /// <summary>A collection of <see cref="Adorner"/>.</summary>
    public class AdornerCollection : Collection<Adorner>
    {
        /// <summary>Adds several adorner layers at once.</summary>
        public void AddRange (IEnumerable<Adorner> adorners)
        {
            ArgumentNullException.ThrowIfNull (adorners);

            foreach (var adorner in adorners)
                Add (adorner);
        }
    }

    /// <summary>Owns the adorner layers and routes design-surface input to their glyphs.</summary>
    /// <remarks>Present so designers that register adorners compile. Nothing constructs one here: there
    /// is no design surface to own, paint or hit-test the layers.</remarks>
    public class BehaviorService
    {
        /// <summary>Gets the adorner layers, painted in order.</summary>
        public AdornerCollection Adorners { get; } = new AdornerCollection ();

        /// <summary>Converts a point from a control's client coordinates to the adorner window's.</summary>
        public Point ControlToAdornerWindow (Control c) => c?.PointToScreen (Point.Empty) ?? Point.Empty;

        /// <summary>Converts a point from screen coordinates to the adorner window's.</summary>
        public Point ScreenToAdornerWindow (Point p) => p;

        /// <summary>Requests a repaint of the adorner window.</summary>
        public void Invalidate () { }

        /// <summary>Requests a repaint of part of the adorner window.</summary>
        public void Invalidate (Rectangle rect) { }

        /// <summary>Synchronises the adorner window with the design surface. No-op here.</summary>
        public void SyncSelection () { }
    }
}
