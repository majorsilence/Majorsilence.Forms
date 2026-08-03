using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The small types the tail of the WinForms surface is written in terms of
    // (docs/winforms-gap-plan.md).
    //
    // Most are one-member interfaces, and their value is entirely in being nameable: a migrated app
    // writes `void Wire (IButtonControl button)` or `if (control is IDropTarget target)`, and that
    // code does not compile without the interface even though nothing in it needs implementing.
    // Declaring them costs a few lines and unblocks whole files.

    /// <summary>A control that can act as a form's accept or cancel button.</summary>
    public interface IButtonControl
    {
        /// <summary>Gets or sets the value returned to the parent form when the button is clicked.</summary>
        DialogResult DialogResult { get; set; }

        /// <summary>Tells the button whether it is the form's default button.</summary>
        void NotifyDefault (bool value);

        /// <summary>Generates a click as though the user had pressed the button.</summary>
        void PerformClick ();
    }

    /// <summary>A component that can be data-bound.</summary>
    public interface IBindableComponent : IComponent
    {
        /// <summary>Gets or sets the binding context for this component.</summary>
        BindingContext? BindingContext { get; set; }

        /// <summary>Gets the data bindings for this component.</summary>
        ControlBindingsCollection DataBindings { get; }
    }

    /// <summary>A component that owns a currency manager.</summary>
    public interface ICurrencyManagerProvider
    {
        /// <summary>Gets the currency manager for this component.</summary>
        CurrencyManager? CurrencyManager { get; }

        /// <summary>Gets the currency manager for a related list.</summary>
        CurrencyManager? GetRelatedCurrencyManager (string? dataMember);
    }

    /// <summary>A control that accepts dropped data.</summary>
    public interface IDropTarget
    {
        /// <summary>Called when the pointer enters the control during a drag.</summary>
        void OnDragEnter (DragEventArgs e);

        /// <summary>Called when the pointer leaves the control during a drag.</summary>
        void OnDragLeave (EventArgs e);

        /// <summary>Called as the pointer moves over the control during a drag.</summary>
        void OnDragOver (DragEventArgs e);

        /// <summary>Called when data is dropped on the control.</summary>
        void OnDragDrop (DragEventArgs e);
    }

    /// <summary>A drop target that handles the drop asynchronously.</summary>
    public interface IAsyncDropTarget : IDropTarget
    {
        /// <summary>Called when data is dropped, allowing the handler to complete later.</summary>
        void OnDragDropAsync (DragEventArgs e);
    }

    /// <summary>A data object whose stored values can be retrieved by type.</summary>
    public interface ITypedDataObject : IDataObject
    {
        /// <summary>Returns the stored value when it is of the requested type.</summary>
        bool TryGetData<T> (string format, out T? data);
    }

    /// <summary>Something that can run a command on behalf of a control.</summary>
    public interface ICommandExecutor
    {
        /// <summary>Raised when the command's ability to run changes.</summary>
        event EventHandler? CommandCanExecuteChanged;

        /// <summary>Runs the command.</summary>
        void Execute ();
    }

    /// <summary>Reads a file on behalf of a dialog.</summary>
    public interface IFileReaderService
    {
        /// <summary>Opens the named file for reading.</summary>
        System.IO.Stream? OpenFile (string path);
    }

    /// <summary>The ambient property values a control inherits from its parent.</summary>
    public class AmbientProperties
    {
        /// <summary>Gets or sets the ambient background colour.</summary>
        public Color BackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the ambient foreground colour.</summary>
        public Color ForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the ambient cursor.</summary>
        public Cursor? Cursor { get; set; }

        /// <summary>Gets or sets the ambient font.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }
    }

    /// <summary>The base of the framework's read-only collections.</summary>
    public abstract class BaseCollection : MarshalByRefObject, ICollection, IEnumerable
    {
        /// <summary>Gets the list the collection reads from.</summary>
        protected virtual ArrayList? List => null;

        /// <summary>Gets the number of items.</summary>
        public virtual int Count => List?.Count ?? 0;

        /// <summary>Gets whether access is synchronised.</summary>
        public bool IsSynchronized => false;

        /// <summary>Gets whether the collection is read-only.</summary>
        public bool IsReadOnly => false;

        /// <summary>Gets the object used to synchronise access.</summary>
        public object SyncRoot => this;

        /// <summary>Copies the collection into an array.</summary>
        public void CopyTo (Array array, int index) => List?.CopyTo (array, index);

        /// <inheritdoc/>
        public IEnumerator GetEnumerator () => List?.GetEnumerator () ?? Array.Empty<object> ().GetEnumerator ();
    }

    /// <summary>A collection of <see cref="Binding"/> objects.</summary>
    public class BindingsCollection : BaseCollection
    {
        private readonly ArrayList bindings = [];

        /// <inheritdoc/>
        protected override ArrayList List => bindings;

        /// <summary>Gets the binding at the given index.</summary>
        public Binding? this[int index] => bindings[index] as Binding;

        /// <summary>Adds a binding.</summary>
        protected internal void Add (Binding binding) => bindings.Add (binding);

        /// <summary>Removes a binding.</summary>
        protected internal void Remove (Binding binding) => bindings.Remove (binding);

        /// <summary>Removes every binding.</summary>
        protected internal void Clear () => bindings.Clear ();
    }

    /// <summary>The scroll state of one axis of a scrollable control.</summary>
    /// <remarks>WinForms splits this into HScrollProperties and VScrollProperties so a control can
    /// expose HorizontalScroll and VerticalScroll separately; the behaviour is identical.</remarks>
    public class ScrollPropertiesBase
    {
        /// <summary>Gets or sets whether the scroll bar is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the amount scrolled by a large step.</summary>
        public int LargeChange { get; set; } = 10;

        /// <summary>Gets or sets the highest scroll value.</summary>
        public int Maximum { get; set; } = 100;

        /// <summary>Gets or sets the lowest scroll value.</summary>
        public int Minimum { get; set; }

        /// <summary>Gets or sets the amount scrolled by a small step.</summary>
        public int SmallChange { get; set; } = 1;

        /// <summary>Gets or sets the current scroll position.</summary>
        public int Value { get; set; }

        /// <summary>Gets or sets whether the scroll bar is shown.</summary>
        public bool Visible { get; set; }
    }

    /// <summary>The horizontal scroll state of a scrollable control.</summary>
    public class HScrollProperties : ScrollPropertiesBase
    {
    }

    /// <summary>The vertical scroll state of a scrollable control.</summary>
    public class VScrollProperties : ScrollPropertiesBase
    {
    }

    /// <summary>The custom places shown in the sidebar of a file dialog.</summary>
    public class FileDialogCustomPlacesCollection : Collection<string>
    {
        /// <summary>Adds a place by path.</summary>
        public new void Add (string path) => base.Add (path);

        /// <summary>Adds a place by known-folder GUID.</summary>
        public void Add (Guid knownFolderGuid) => base.Add (knownFolderGuid.ToString ());
    }

    /// <summary>A row of tool strips within a <see cref="ToolStripPanel"/>.</summary>
    public class ToolStripPanelRow
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripPanelRow"/> class.</summary>
        public ToolStripPanelRow (ToolStripPanel toolStripPanel) => ToolStripPanel = toolStripPanel;

        /// <summary>Gets the panel this row belongs to.</summary>
        public ToolStripPanel ToolStripPanel { get; }

        /// <summary>Gets the strips on this row.</summary>
        public List<ToolStrip> Controls { get; } = [];

        /// <summary>Gets or sets the bounds of the row.</summary>
        public Rectangle Bounds { get; set; }

        /// <summary>Gets or sets the row's margin.</summary>
        public Padding Margin { get; set; }

        /// <summary>Gets the orientation the row lays its strips out in.</summary>
        public Orientation Orientation
            => ToolStripPanel.Dock is DockStyle.Left or DockStyle.Right ? Orientation.Vertical : Orientation.Horizontal;
    }
}
