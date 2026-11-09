using System.Drawing;
using SkiaSharp;
using Majorsilence.Forms.Telerik.Primitives;

namespace Majorsilence.Forms.Telerik
{
    // A tool strip's element tree, as code that styles Telerik's walks it (UI for WinForms 2026.2.520, probes/wfcellfmt):
    // RootRadElement > SplitPanelElement > FillPrimitive, BorderPrimitive, then the caption and tab strip elements. The
    // fill primitive draws the strip's background (a two-colour gradient in the default theme); code flattens the
    // chrome by giving it one colour and collapsing the border primitive.
    //
    // Here the strip has no gradient and no outer border of its own: the fill primitive's colour, once set, is what the
    // strip fills with, and the border primitive is there to be found (collapsing it leaves nothing to hide).
    public partial class ToolTabStrip
    {
        private RadElement? root_element;
        private FillPrimitive? chrome_fill;

        private RootRadElement BuildChrome ()
        {
            chrome_fill = new FillPrimitive { Changed = Invalidate };
            var panel = new SplitPanelElement ();
            panel.Children.Add (chrome_fill);
            panel.Children.Add (new BorderPrimitive ());

            var root = new RootRadElement ();
            root.Children.Add (panel);
            return root;
        }

        // Under the headers: the fill primitive's colour over the whole strip, once code has given it one.
        private void PaintChromeFill (PaintEventArgs e)
        {
            if (chrome_fill is not { BackColor.IsEmpty: false, Visibility: ElementVisibility.Visible } fill)
                return;

            using var device = e.DeviceSpace ();
            e.Canvas.FillRectangle (new Rectangle (0, 0, LogicalToDeviceUnits (ClientSize.Width), LogicalToDeviceUnits (ClientSize.Height)), fill.BackColor.ToSKColor ());
        }
    }

    /// <summary>Telerik-compat panel element of a dock strip: the parent of its chrome's fill and border primitives.</summary>
    public class SplitPanelElement : VisualElement
    {
    }
}
