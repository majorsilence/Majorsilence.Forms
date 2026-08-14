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

    /// <summary>
    /// The service a <see cref="UITypeEditor"/> uses to show its editing UI.
    /// </summary>
    /// <remarks>
    /// WinForms puts this in System.Windows.Forms.Design, which does not exist off Windows, so an
    /// editor that drops down a control could not even be compiled. As with the designers below, this
    /// is the shape rather than a working host: nothing here provides the service, so
    /// GetService returns null at runtime and the editor falls back to its plain value.
    /// </remarks>
    public interface IWindowsFormsEditorService
    {
        /// <summary>Closes a previously opened drop-down control.</summary>
        void CloseDropDown ();

        /// <summary>Shows the given control in a drop-down below the property.</summary>
        void DropDownControl (Control? control);

        /// <summary>Shows the given form as a modal dialog.</summary>
        Majorsilence.Forms.DialogResult ShowDialog (Form dialog);
    }

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
        /// <remarks>Takes the non-generic <see cref="System.Collections.IDictionary"/>, as WinForms
        /// declares it -- it was generic here, so a designer overriding the WinForms signature had no
        /// suitable method to override.</remarks>
        public virtual void InitializeNewComponent (System.Collections.IDictionary? defaultValues) { }

        /// <summary>Gets the smart-tag action lists this designer offers for its component.</summary>
        /// <remarks>A control library overrides this to return one list per control; the lists are built
        /// but never shown, since there is no designer action UI service here to show them.</remarks>
        public virtual DesignerActionListCollection ActionLists { get; } = new DesignerActionListCollection ();

        /// <summary>Gets the components that travel with this one when it is copied or deleted.</summary>
        /// <remarks>A composite control's designer overrides this to include the parts it owns, so that
        /// cutting the whole takes the parts with it.</remarks>
        public virtual System.Collections.ICollection AssociatedComponents => Array.Empty<IComponent> ();

        /// <summary>Gets how much of the component was inherited from a base class.</summary>
        protected virtual InheritanceAttribute? InheritanceAttribute => System.ComponentModel.InheritanceAttribute.NotInherited;

        /// <summary>Adjusts the property descriptors the property grid will show, before filtering.</summary>
        /// <remarks>
        /// These six are WinForms' <c>IDesignerFilter</c> surface, and a designer that hides or re-declares
        /// a property (to give it a design-time editor, or to keep an inherited one out of the grid) does it
        /// here. They are never called — nothing here builds a property grid for a design surface — but a
        /// designer that overrides them has to compile.
        /// </remarks>
        protected virtual void PreFilterProperties (System.Collections.IDictionary properties) { }

        /// <inheritdoc cref="PreFilterProperties"/>
        protected virtual void PostFilterProperties (System.Collections.IDictionary properties) { }

        /// <inheritdoc cref="PreFilterProperties"/>
        protected virtual void PreFilterAttributes (System.Collections.IDictionary attributes) { }

        /// <inheritdoc cref="PreFilterProperties"/>
        protected virtual void PostFilterAttributes (System.Collections.IDictionary attributes) { }

        /// <inheritdoc cref="PreFilterProperties"/>
        protected virtual void PreFilterEvents (System.Collections.IDictionary events) { }

        /// <inheritdoc cref="PreFilterProperties"/>
        protected virtual void PostFilterEvents (System.Collections.IDictionary events) { }

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

        /// <summary>Gets the snap lines this control offers for aligning its neighbours.</summary>
        /// <remarks>Empty by default. A designer overrides this to publish the baselines of the text
        /// inside its control, which is what lets a label line up with the text in a box beside it.</remarks>
        public virtual System.Collections.IList SnapLines => new List<object> ();

        /// <summary>Called when the pointer enters the designed control on the design surface.</summary>
        /// <remarks>
        /// These five, and the drag pair below, are how a designer tracks the pointer over its own control
        /// without the control itself seeing the input. Never called here: there is no design surface
        /// forwarding input to designers.
        /// </remarks>
        protected virtual void OnMouseEnter () { }

        /// <inheritdoc cref="OnMouseEnter"/>
        protected virtual void OnMouseLeave () { }

        /// <inheritdoc cref="OnMouseEnter"/>
        protected virtual void OnMouseHover () { }

        /// <inheritdoc cref="OnMouseEnter"/>
        protected virtual void OnDragEnter (DragEventArgs de) { }

        /// <inheritdoc cref="OnMouseEnter"/>
        protected virtual void OnDragOver (DragEventArgs de) { }

        /// <inheritdoc cref="OnMouseEnter"/>
        protected virtual void OnDragLeave (EventArgs e) { }

        /// <inheritdoc cref="OnMouseEnter"/>
        protected virtual void OnDragDrop (DragEventArgs de) { }

        /// <summary>Gets the designer of one of the control's internal child controls.</summary>
        /// <remarks>A composite control exposes the designers of the parts a user may select — the panel
        /// inside a group box, say — through this and <see cref="NumberOfInternalControlDesigners"/>.</remarks>
        public virtual ControlDesigner? InternalControlDesigner (int internalControlIndex) => null;

        /// <inheritdoc cref="InternalControlDesigner"/>
        public virtual int NumberOfInternalControlDesigners () => 0;

        /// <summary>Gets whether this control may be parented to the given designer's control.</summary>
        public virtual bool CanBeParentedTo (IDesigner parentDesigner) => true;

        /// <summary>Gets or sets whether the designer hides resize handles that would not fit.</summary>
        /// <remarks>
        /// Set in <c>Initialize</c> by nearly every control designer, which is why it is the single most
        /// common design-time member a themed control library touches. Stored only: the handles it governs
        /// are drawn by the design surface, and there is none here.
        /// </remarks>
        public bool AutoResizeHandles { get; set; }

        /// <summary>Lets one of the control's internal children be designed in its own right.</summary>
        /// <remarks>
        /// A composite control calls this for each part a user should be able to drop controls into -- the
        /// panel inside a group box, the two halves of a split container. Returns false: enabling design
        /// mode requires a design surface to enable it on, and a caller that checks the result correctly
        /// concludes the child is not designable.
        /// </remarks>
        protected bool EnableDesignMode (Control? child, string name) => false;
    }

    /// <summary>A designer for a control that can contain other controls.</summary>
    public class ParentControlDesigner : ControlDesigner
    {
        /// <summary>Gets or sets whether the designer draws a grid on its surface.</summary>
        public virtual bool DrawGrid { get; set; }

        /// <summary>Gets the grid spacing used when snapping child controls.</summary>
        protected virtual Size GridSize { get; set; } = new Size (8, 8);

        /// <summary>Gets whether the given control may be dropped into this container.</summary>
        /// <remarks>A container that only accepts particular children — a docking area that takes pages,
        /// say — overrides this to refuse the rest.</remarks>
        public virtual bool CanParent (Control control) => true;

        /// <inheritdoc cref="CanParent(Control)"/>
        public virtual bool CanParent (ControlDesigner controlDesigner) => true;

        /// <summary>Adds snap lines for the container's padding edges to the given list.</summary>
        /// <remarks>
        /// A container designer overrides <see cref="ControlDesigner.SnapLines"/>, calls this to get the
        /// four padding edges, then adds its own. The list is created when the caller passes null -- which
        /// is how the overrides upstream are written -- so a derived designer can chain into it safely.
        /// There is no design surface to consume the lines; what matters is that the list comes back usable.
        /// </remarks>
        protected void AddPaddingSnapLines (ref System.Collections.ArrayList? snapLines)
            => snapLines ??= new System.Collections.ArrayList ();
    }

    /// <summary>A designer for a control that scrolls its contents.</summary>
    public class ScrollableControlDesigner : ParentControlDesigner
    {
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

        /// <summary>Disposes an item the editor created and the user then removed.</summary>
        /// <remarks>
        /// Real, and it matters: a collection editor that adds a component, has it rejected, and never
        /// disposes it leaks whatever that component holds. WinForms routes designer-hosted components
        /// through the host's DestroyComponent; with no host, disposing directly is the whole of the work.
        /// </remarks>
        protected virtual void DestroyInstance (object? instance)
        {
            if (instance is IDisposable disposable)
                disposable.Dispose ();
        }

        /// <summary>Gets the descriptor context the property is being edited in.</summary>
        /// <remarks>Null: there is no property grid supplying one. A caller reads it to reach the
        /// container that owns the components it is editing, so it must be null-checked -- as it must
        /// upstream too, where the grid has not always set it by the time an editor asks.</remarks>
        protected ITypeDescriptorContext? Context => null;

        /// <summary>Gets the items currently in the collection.</summary>
        protected virtual object[] GetItems (object? editValue)
            => editValue is System.Collections.IEnumerable items ? items.Cast<object> ().ToArray () : [];

        /// <summary>Stores the edited items back into the collection.</summary>
        protected virtual object? SetItems (object? editValue, object[] value) => editValue;

        /// <inheritdoc/>
        public override UITypeEditorEditStyle GetEditStyle () => UITypeEditorEditStyle.Modal;

        /// <summary>Creates the form used to edit the collection.</summary>
        /// <remarks>An editor with its own editing UI overrides this and returns its own form, which is
        /// how a themed collection editor replaces the default list-and-buttons dialog.</remarks>
        protected virtual CollectionForm CreateCollectionForm () => new DefaultCollectionForm (this);

        // The base CreateCollectionForm has to return something concrete, and CollectionForm is abstract
        // (as it is upstream) so that a derived editor's override is the only thing that decides the UI.
        private sealed class DefaultCollectionForm : CollectionForm
        {
            public DefaultCollectionForm (CollectionEditor editor) : base (editor) { }

            protected override void OnEditValueChanged () { }
        }

        /// <summary>The dialog that edits a collection property's items.</summary>
        /// <remarks>
        /// Nested inside the editor, as WinForms nests it, so a derived editor writes
        /// <c>CollectionForm</c> unqualified exactly as it did before the migration. It is a real
        /// <see cref="Form"/> and will show if something calls it; what is absent is the property grid
        /// that would normally open it.
        /// </remarks>
        public abstract class CollectionForm : Form
        {
            /// <summary>Initializes the form for the editor that owns it.</summary>
            protected CollectionForm (CollectionEditor editor)
            {
                CollectionEditor = editor.OrThrowIfNull ();
                Items = [];
            }

            /// <summary>Gets the editor that created this form.</summary>
            protected CollectionEditor CollectionEditor { get; }

            /// <summary>Gets or sets the collection being edited.</summary>
            protected object? EditValue {
                get => edit_value;
                set {
                    edit_value = value;
                    OnEditValueChanged ();
                }
            }

            private object? edit_value;

            /// <summary>Gets or sets the items of the collection being edited.</summary>
            protected object[] Items { get; set; }

            /// <summary>Gets the descriptor context the property is being edited in.</summary>
            /// <remarks>Null: there is no property grid supplying one.</remarks>
            protected ITypeDescriptorContext? Context => null;

            /// <summary>Gets the service that would host this form in the property grid.</summary>
            /// <inheritdoc cref="Context"/>
            protected IWindowsFormsEditorService? EditorService => null;

            /// <summary>Called when <see cref="EditValue"/> has been replaced.</summary>
            protected abstract void OnEditValueChanged ();

            /// <summary>Creates one new item of the given type, through the editor that owns this form.</summary>
            /// <remarks>
            /// The form is where the "add" button lives, so the form is what asks for the instance; routing
            /// it to the editor is what lets an editor with a custom <c>CreateInstance</c> still decide what
            /// gets made. Same reasoning for <see cref="DestroyInstance"/> on the way out.
            /// </remarks>
            protected object? CreateInstance (
                [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers (
                    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
                Type itemType) => CollectionEditor.CreateInstance (itemType);

            /// <inheritdoc cref="CollectionEditor.DestroyInstance"/>
            protected void DestroyInstance (object? instance) => CollectionEditor.DestroyInstance (instance);

            /// <summary>Reports an error raised while editing the collection.</summary>
            protected virtual void DisplayError (Exception e) { }

            /// <summary>Shows the form through the property grid's editor service.</summary>
            protected virtual Majorsilence.Forms.DialogResult ShowEditorDialog (IWindowsFormsEditorService edSvc)
                => edSvc is null ? ShowDialog () : edSvc.ShowDialog (this);
        }
    }

    /// <summary>Edits a string property in a resizable multi-line box.</summary>
    /// <remarks>Attached with <c>[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]</c> to any
    /// property that holds prose — a tooltip body, a heading's description. The attribute is what has to
    /// resolve for the owning control to compile, which is why the type matters even with no grid here.</remarks>
    public class MultilineStringEditor : UITypeEditor
    {
        /// <inheritdoc/>
        public override UITypeEditorEditStyle GetEditStyle () => UITypeEditorEditStyle.DropDown;
    }

    /// <summary>Edits a folder-path property by browsing for a folder.</summary>
    public class FolderNameEditor : UITypeEditor
    {
        /// <inheritdoc/>
        public override UITypeEditorEditStyle GetEditStyle () => UITypeEditorEditStyle.Modal;

        /// <summary>Configures the browser before it is shown.</summary>
        /// <remarks>A derived editor overrides this to set the description and starting folder.</remarks>
        protected virtual void InitializeDialog (FolderBrowser folderBrowser) { }

        /// <summary>Where the folder browser starts from.</summary>
        public enum FolderBrowserFolder
        {
            /// <summary>The user's desktop.</summary>
            Desktop = 0,
            /// <summary>The user's favourites.</summary>
            Favorites = 6,
            /// <summary>My Computer.</summary>
            MyComputer = 17,
            /// <summary>My Documents.</summary>
            MyDocuments = 5,
            /// <summary>My Pictures.</summary>
            MyPictures = 39,
            /// <summary>The network neighbourhood.</summary>
            NetAndDialUpConnections = 49,
            /// <summary>The network's root.</summary>
            NetworkNeighborhood = 18,
            /// <summary>The printers folder.</summary>
            Printers = 4,
            /// <summary>The recently-used list.</summary>
            Recent = 8,
            /// <summary>The Send To menu.</summary>
            SendTo = 9,
            /// <summary>The start menu.</summary>
            StartMenu = 11,
            /// <summary>The templates folder.</summary>
            Templates = 21,
        }

        /// <summary>How the folder browser presents itself.</summary>
        [Flags]
        public enum FolderBrowserStyles
        {
            /// <summary>The default browser.</summary>
            BrowseForComputer = 0x1000,
            /// <summary>Browse for everything, not only folders.</summary>
            BrowseForEverything = 0x4000,
            /// <summary>Browse for a printer.</summary>
            BrowseForPrinter = 0x2000,
            /// <summary>Only folders in the file system may be chosen.</summary>
            RestrictToFilesystem = 0x0001,
            /// <summary>Only folders below the starting folder may be chosen.</summary>
            RestrictToSubfolders = 0x0008,
            /// <summary>Show a text box for typing a path.</summary>
            ShowTextBox = 0x0010,
        }

        /// <summary>The folder browser a <see cref="FolderNameEditor"/> shows.</summary>
        /// <remarks>Backed by <see cref="FolderBrowserDialog"/> when shown, so the properties a derived
        /// editor sets in <see cref="InitializeDialog"/> are the ones the user sees.</remarks>
        public class FolderBrowser : Component
        {
            /// <summary>Gets or sets the prompt shown above the folder list.</summary>
            public string Description { get; set; } = string.Empty;

            /// <summary>Gets the folder the user chose.</summary>
            public string DirectoryPath { get; private set; } = string.Empty;

            /// <summary>Gets or sets the folder the browser starts from.</summary>
            public FolderBrowserFolder StartLocation { get; set; } = FolderBrowserFolder.Desktop;

            /// <summary>Gets or sets how the browser presents itself.</summary>
            public FolderBrowserStyles Style { get; set; } = FolderBrowserStyles.RestrictToFilesystem;

            /// <summary>Shows the browser.</summary>
            public Majorsilence.Forms.DialogResult ShowDialog ()
            {
                using var dialog = new FolderBrowserDialog { Description = Description };
                var result = dialog.ShowDialog ();

                if (result == Majorsilence.Forms.DialogResult.OK)
                    DirectoryPath = dialog.SelectedPath;

                return result;
            }
        }
    }

    /// <summary>One entry on a component's smart-tag panel.</summary>
    public abstract class DesignerActionItem
    {
        /// <summary>Initializes the item's display text, category and description.</summary>
        protected DesignerActionItem (string? displayName, string? category, string? description)
        {
            DisplayName = displayName;
            Category = category;
            Description = description;
        }

        /// <summary>Gets the text shown for this item.</summary>
        public virtual string? DisplayName { get; }

        /// <summary>Gets the category the item is grouped under.</summary>
        public virtual string? Category { get; }

        /// <summary>Gets the item's description, shown as a tooltip.</summary>
        public virtual string? Description { get; }

        /// <summary>Gets or sets whether the item may be merged with items of the same category.</summary>
        public bool AllowAssociate { get; set; }

        /// <summary>Gets or sets whether the item is shown in the source view.</summary>
        public bool ShowItemInSourceView { get; set; } = true;

        /// <summary>Gets the item's extra state.</summary>
        public System.Collections.IDictionary Properties { get; } = new System.Collections.Hashtable ();
    }

    /// <summary>A line of static text on a smart-tag panel.</summary>
    public class DesignerActionTextItem : DesignerActionItem
    {
        /// <summary>Initializes the text item.</summary>
        public DesignerActionTextItem (string? displayName, string? category)
            : base (displayName, category, null) { }
    }

    /// <summary>A group heading on a smart-tag panel.</summary>
    public sealed class DesignerActionHeaderItem : DesignerActionTextItem
    {
        /// <summary>Initializes the heading, which is its own category.</summary>
        public DesignerActionHeaderItem (string? displayName) : base (displayName, displayName) { }

        /// <summary>Initializes the heading under an explicit category.</summary>
        public DesignerActionHeaderItem (string? displayName, string? category) : base (displayName, category) { }
    }

    /// <summary>An entry on a smart-tag panel that calls a method on the action list.</summary>
    public class DesignerActionMethodItem : DesignerActionItem
    {
        /// <summary>Initializes the item for a method on the given list.</summary>
        public DesignerActionMethodItem (DesignerActionList? actionList, string memberName, string? displayName)
            : this (actionList, memberName, displayName, null, null, false) { }

        /// <inheritdoc cref="DesignerActionMethodItem(DesignerActionList, string, string)"/>
        public DesignerActionMethodItem (DesignerActionList? actionList, string memberName, string? displayName,
                                        bool includeAsDesignerVerb)
            : this (actionList, memberName, displayName, null, null, includeAsDesignerVerb) { }

        /// <inheritdoc cref="DesignerActionMethodItem(DesignerActionList, string, string)"/>
        public DesignerActionMethodItem (DesignerActionList? actionList, string memberName, string? displayName,
                                        string? category)
            : this (actionList, memberName, displayName, category, null, false) { }

        /// <inheritdoc cref="DesignerActionMethodItem(DesignerActionList, string, string)"/>
        public DesignerActionMethodItem (DesignerActionList? actionList, string memberName, string? displayName,
                                        string? category, bool includeAsDesignerVerb)
            : this (actionList, memberName, displayName, category, null, includeAsDesignerVerb) { }

        /// <inheritdoc cref="DesignerActionMethodItem(DesignerActionList, string, string)"/>
        public DesignerActionMethodItem (DesignerActionList? actionList, string memberName, string? displayName,
                                        string? category, string? description)
            : this (actionList, memberName, displayName, category, description, false) { }

        /// <inheritdoc cref="DesignerActionMethodItem(DesignerActionList, string, string)"/>
        public DesignerActionMethodItem (DesignerActionList? actionList, string memberName, string? displayName,
                                        string? category, string? description, bool includeAsDesignerVerb)
            : base (displayName, category, description)
        {
            ActionList = actionList;
            MemberName = memberName;
            IncludeAsDesignerVerb = includeAsDesignerVerb;
        }

        /// <summary>Gets the list this item belongs to.</summary>
        protected DesignerActionList? ActionList { get; }

        /// <summary>Gets the name of the method this item calls.</summary>
        public virtual string MemberName { get; }

        /// <summary>Gets whether the item also appears on the component's context menu.</summary>
        public virtual bool IncludeAsDesignerVerb { get; }

        /// <summary>Calls the named method on the action list.</summary>
        /// <remarks>Reflection, as upstream: the item names its method rather than holding a delegate,
        /// which is what lets a list declare its actions as ordinary methods.</remarks>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "The action list names its own method, so the method cannot be discovered "
                + "statically -- the same design as upstream. Nothing in a published app calls this: it is "
                + "reached only from a design surface, which does not exist here. If trimming removes the "
                + "method the lookup returns null and the call is a no-op rather than a failure.")]
        public virtual void Invoke () =>
            ActionList?.GetType ()
                .GetMethod (MemberName, System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke (ActionList, null);
    }

    /// <summary>An entry on a smart-tag panel that edits a property of the action list.</summary>
    public class DesignerActionPropertyItem : DesignerActionItem
    {
        /// <summary>Initializes the item for a property on the list.</summary>
        public DesignerActionPropertyItem (string memberName, string? displayName)
            : this (memberName, displayName, null, null) { }

        /// <inheritdoc cref="DesignerActionPropertyItem(string, string)"/>
        public DesignerActionPropertyItem (string memberName, string? displayName, string? category)
            : this (memberName, displayName, category, null) { }

        /// <inheritdoc cref="DesignerActionPropertyItem(string, string)"/>
        public DesignerActionPropertyItem (string memberName, string? displayName, string? category, string? description)
            : base (displayName, category, description) => MemberName = memberName;

        /// <summary>Gets the name of the property this item edits.</summary>
        public virtual string MemberName { get; }

        /// <summary>Gets or sets the component the property belongs to.</summary>
        public IComponent? RelatedComponent { get; set; }
    }

    /// <summary>A collection of <see cref="DesignerActionItem"/>.</summary>
    public class DesignerActionItemCollection : System.Collections.CollectionBase
    {
        /// <summary>Adds an item to the panel.</summary>
        public int Add (DesignerActionItem value) => List.Add (value);

        /// <summary>Gets or sets the item at the given index.</summary>
        public DesignerActionItem? this[int index] {
            get => (DesignerActionItem?)List[index];
            set => List[index] = value;
        }

        /// <summary>Returns whether the item is in the collection.</summary>
        public bool Contains (DesignerActionItem value) => List.Contains (value);

        /// <summary>Returns the index of the item, or -1.</summary>
        public int IndexOf (DesignerActionItem value) => List.IndexOf (value);

        /// <summary>Inserts an item at the given index.</summary>
        public void Insert (int index, DesignerActionItem value) => List.Insert (index, value);

        /// <summary>Removes the item.</summary>
        public void Remove (DesignerActionItem value) => List.Remove (value);
    }

    /// <summary>The smart-tag actions a designer offers for one component.</summary>
    /// <remarks>
    /// A control library declares one of these per control, with a method or property item per action, and
    /// returns it from its designer's <see cref="ComponentDesigner.ActionLists"/>. The list is built and
    /// its items are callable — <see cref="DesignerActionMethodItem.Invoke"/> works — but nothing here
    /// displays a panel, so the actions are only reachable in code.
    /// </remarks>
    public class DesignerActionList
    {
        /// <summary>Initializes the list for the component whose actions it describes.</summary>
        public DesignerActionList (IComponent? component) => Component = component;

        /// <summary>Gets the component the actions apply to.</summary>
        public IComponent? Component { get; }

        /// <summary>Gets or sets whether the panel opens as soon as the component is dropped.</summary>
        public virtual bool AutoShow { get; set; }

        /// <summary>Gets a service from the design surface's service container.</summary>
        /// <remarks>Always null; there is no design host. See <see cref="ComponentDesigner.GetService"/>.</remarks>
        public object? GetService (Type serviceType) => null;

        /// <summary>Gets the items to show, in display order.</summary>
        public virtual DesignerActionItemCollection GetSortedActionItems () => new DesignerActionItemCollection ();
    }

    /// <summary>A collection of <see cref="DesignerActionList"/>.</summary>
    public class DesignerActionListCollection : System.Collections.CollectionBase
    {
        /// <summary>Initializes an empty collection.</summary>
        public DesignerActionListCollection () { }

        /// <summary>Initializes the collection with the given lists.</summary>
        public DesignerActionListCollection (DesignerActionList[] value) => AddRange (value);

        /// <summary>Adds a list.</summary>
        public int Add (DesignerActionList value) => List.Add (value);

        /// <summary>Adds several lists.</summary>
        public void AddRange (DesignerActionList[] value)
        {
            ArgumentNullException.ThrowIfNull (value);

            foreach (var list in value)
                Add (list);
        }

        /// <summary>Adds the lists from another collection.</summary>
        public void AddRange (DesignerActionListCollection value)
        {
            ArgumentNullException.ThrowIfNull (value);

            foreach (DesignerActionList list in value)
                Add (list);
        }

        /// <summary>Gets or sets the list at the given index.</summary>
        public DesignerActionList? this[int index] {
            get => (DesignerActionList?)List[index];
            set => List[index] = value;
        }

        /// <summary>Returns whether the list is in the collection.</summary>
        public bool Contains (DesignerActionList value) => List.Contains (value);

        /// <summary>Returns the index of the list, or -1.</summary>
        public int IndexOf (DesignerActionList value) => List.IndexOf (value);

        /// <summary>Inserts a list at the given index.</summary>
        public void Insert (int index, DesignerActionList value) => List.Insert (index, value);

        /// <summary>Removes the list.</summary>
        public void Remove (DesignerActionList value) => List.Remove (value);
    }

    /// <summary>Drives the smart-tag panel that shows a component's <see cref="DesignerActionList"/> items.</summary>
    /// <remarks>
    /// A component's action list reaches for this after changing a property so the panel redraws with the
    /// new state -- an orientation toggle whose label has to flip, say. Every method is a no-op: there is
    /// no panel to refresh. It is requested through <c>GetService(typeof(DesignerActionUIService))</c>,
    /// which returns null here, so in practice the calls are guarded and never made; the type has to
    /// resolve for the <c>is</c> pattern around them to compile.
    /// </remarks>
    public class DesignerActionUIService : IDisposable
    {
        /// <summary>Rebuilds the panel for the given component so it reflects current property values.</summary>
        public void Refresh (object? component) { }

        /// <summary>Hides the panel for the given component.</summary>
        public void HideUI (object? component) { }

        /// <summary>Shows the panel for the given component.</summary>
        public void ShowUI (object? component) { }

        /// <summary>Gets whether the panel for the given component should be shown automatically.</summary>
        public bool ShouldAutoShow (System.ComponentModel.IComponent? component) => false;

        /// <summary>Raised when the set of action lists for a component has changed. Never raised.</summary>
#pragma warning disable CS0067
        public event EventHandler? DesignerActionListsChanged;
#pragma warning restore CS0067

        /// <summary>Releases the service.</summary>
        public void Dispose () => GC.SuppressFinalize (this);
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
