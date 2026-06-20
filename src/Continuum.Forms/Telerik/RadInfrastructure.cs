using System;
using System.Drawing;

// Telerik WinControls compatibility layer for Continuum.Forms.
//
// These types mirror the public surface of the Telerik.WinControls.* controls used by migrated
// WinForms apps, but live in the `Continuum.Forms.Telerik` namespace and are backed by Continuum.Forms
// controls. To migrate, swap `Imports Telerik.WinControls.UI` (and related Telerik namespaces) for
// `Imports Continuum.Forms.Telerik`. Coverage is compile-and-approximate, not pixel-perfect: the rich
// "element tree" of Telerik is represented by lightweight stub elements so formatting handlers and
// designer code compile and run.
namespace Continuum.Forms.Telerik
{
    /// <summary>
    /// Lightweight stand-in for a Telerik visual element (RootElement, cell/row elements, etc.).
    /// Exposes the commonly-accessed styling members as settable no-ops so migrated code compiles.
    /// </summary>
    public class RadElement
    {
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
        /// <summary>Gets the child elements of this element.</summary>
        public System.Collections.Generic.List<RadElement> Children { get; } = new ();
        /// <summary>Returns the child element at the specified index, or a new stub element.</summary>
        public virtual RadElement GetChildAt (int index) => new RadElement ();
        /// <summary>Resets a property to its default value. No-op stub.</summary>
        public void ResetValue (object? property = null) { }
    }

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
        Rotate = 2
    }

}
