using System.Drawing;
using Majorsilence.Forms;
using SkiaSharp;

namespace ControlGallery.Panels;

public class FlowLayoutPanelPanel : Panel
{
    private readonly SKColor[] colors = [
        SKColors.CornflowerBlue,
        SKColors.LightPink,
        SKColors.LightSeaGreen,
        SKColors.LightYellow,
        SKColors.LightCoral,
        SKColors.LightGray,
        SKColors.LightGreen,
        SKColors.LightGoldenrodYellow
    ];

    public FlowLayoutPanelPanel ()
    {
        // Dock is explicit because SplitContainer's constructor no longer forces DockStyle.Fill
        // (LAY-08): WinForms' inherits DockStyle.None, and the forced Fill silently overrode the
        // Anchor + Location + Size a designer emits. This was the only place in the tree relying on
        // it; the sibling SplitContainer samples already set Dock themselves.
        var container = Controls.Add (new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterColor = Color.DarkGray });

        var ltr = container.Panel1.Controls.Add (new FlowLayoutPanel { Dock = DockStyle.Fill });
        var ttb = container.Panel2.Controls.Add (new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown });

        foreach (var color in colors)
            ltr.Controls.Add (CreatePanel (color));

        foreach (var color in colors)
            ttb.Controls.Add (CreatePanel (color));
    }

    private static Panel CreatePanel (SKColor color)
    {
        var panel = new Panel { Height = 100, Width = 100 };
        panel.Style.BackgroundColor = color;

        return panel;
    }
}
