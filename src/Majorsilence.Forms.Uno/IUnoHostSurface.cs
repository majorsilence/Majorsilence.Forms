namespace Majorsilence.Forms.Uno
{
    /// <summary>
    /// A surface that can parent Majorsilence.Forms popup overlays (combo dropdowns, menus, tooltips) into
    /// an existing Uno visual tree. Implemented both by the standalone <see cref="UnoWindowHost"/> and by
    /// the embedding <see cref="MajorsilenceFormsPresenter"/>, so popups attach to whichever XamlRoot is
    /// hosting the Majorsilence.Forms content.
    /// </summary>
    internal interface IUnoHostSurface
    {
        /// <summary>The XamlRoot of the surface's drawing canvas (popups attach their overlay to this).</summary>
        Microsoft.UI.Xaml.XamlRoot? CanvasXamlRoot { get; }

        /// <summary>The rasterization (DPI) scale of the surface.</summary>
        double HostScaling { get; }

        /// <summary>The surface's screen/window-relative position used to place popups.</summary>
        System.Drawing.Point Location { get; }
    }
}
