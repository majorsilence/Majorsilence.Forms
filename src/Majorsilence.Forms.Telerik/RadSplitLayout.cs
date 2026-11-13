using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms.Telerik
{
    // Telerik's split layout: how a RadDock, a RadSplitContainer and a DocumentContainer share their room between the
    // split panels they hold (tool and document tab strips, document containers, nested split containers). Measured on
    // UI for WinForms 2026.2.520 (.NET 10), along the container's orientation (Vertical lays the panels side by side):
    //   - the room is the container's length, inside the dock's padding of 5 (a nested container has none), with a
    //     SplitterWidth gap (4 by default) between neighbouring panels; a collapsed panel takes no room and no gap;
    //   - with a Fill panel (the main document container is one): the first fills what the others leave, and every
    //     other panel -- Auto, Absolute or a second Fill -- is its AbsoluteSize long. A panel that merely HOLDS the
    //     Fill panel, at any depth, fills its own container the same way;
    //   - without one: an Absolute panel is its AbsoluteSize long, and each Auto panel takes L x (1 / n +
    //     AutoSizeScale), rounded, of L, the room the Absolute panels and n - 1 splitters leave the n Auto panels --
    //     except the last, which takes whatever is left;
    //   - SplitterCorrection does not take part; across the orientation every panel fills the container.
    // Measured, for instance: two Auto panels in a 1000px dock, 493 each; with AutoSizeScale 0.2 on the first, 690 and
    // 296; an Absolute 300 then two Auto panels, 300, 343 and 339; a Fill panel beside an Auto one of AbsoluteSize
    // 250, 736 and 250.
    internal static class SplitLayout
    {
        // The dock's padding (Telerik's theme gives the dock 5 all round once it is shown).
        internal const int DockPadding = 5;

        internal static SplitPanelSizeInfo? SizeInfoOf (Control control) => control switch {
            SplitPanel panel => panel.SizeInfo,
            ToolTabStrip strip => strip.SizeInfo,
            DocumentTabStrip strip => strip.SizeInfo,
            DocumentContainer container => container.SizeInfo,
            _ => null,
        };

        internal static bool IsCollapsed (Control control) => control switch {
            SplitPanel panel => panel.Collapsed,
            DocumentContainer container => container.Collapsed,
            _ => false,
        };

        // The split panels a container lays out: its tab strips, document containers and split containers that take part
        // in layout and are not collapsed.
        internal static List<Control> PanelsOf (Control container, Control? excluded = null)
            => container.Controls.OfType<Control> ()
                .Where (c => SizeInfoOf (c) is not null && !ReferenceEquals (c, excluded) && !IsCollapsed (c)
                    && ((Layout.IArrangedElement) c).ParticipatesInLayout)
                .ToList ();

        // Whether a Fill panel sits anywhere below this one -- the panel itself does not count, since Arrange looks for
        // that first.
        private static bool HoldsFill (Control control)
        {
            foreach (Control child in control.Controls) {
                if (SizeInfoOf (child)?.SizeMode == SplitPanelSizeMode.Fill || HoldsFill (child))
                    return true;
            }

            return false;
        }

        internal static void Arrange (Orientation orientation, int splitterWidth, Rectangle client, IReadOnlyList<Control> panels)
        {
            var count = panels.Count;

            if (count == 0)
                return;

            var side_by_side = orientation == Orientation.Vertical;
            var length = side_by_side ? client.Width : client.Height;
            var splitter = Math.Max (0, splitterWidth);
            var room = length - splitter * (count - 1);
            var infos = panels.Select (p => SizeInfoOf (p)!).ToList ();
            var lengths = new int[count];

            int Along (SizeF size) => (int) Math.Round (side_by_side ? size.Width : size.Height);

            var fill = infos.FindIndex (i => i.SizeMode == SplitPanelSizeMode.Fill);

            // A panel that only HOLDS the Fill panel fills in its own container too, however deep it is. Measured on
            // 2026.2.520 with the main document container two levels down: a 900 x 420 dock holding a tool strip
            // (AbsoluteSize 200) and a split container gave the strip exactly 200 and the container the remaining 686,
            // rather than the even 443 each two Auto panels share. Telerik writes Auto on those containers, so without
            // this a layout it saved lays out differently from the dock it was saved from.
            if (fill < 0)
                fill = panels.ToList ().FindIndex (HoldsFill);

            if (fill >= 0) {
                var others = 0;

                for (var i = 0; i < count; i++) {
                    if (i == fill)
                        continue;

                    lengths[i] = Math.Max (0, Along (infos[i].AbsoluteSize));
                    others += lengths[i];
                }

                lengths[fill] = Math.Max (0, room - others);
            } else {
                var autos = new List<int> ();
                var absolute = 0;

                for (var i = 0; i < count; i++) {
                    if (infos[i].SizeMode == SplitPanelSizeMode.Absolute) {
                        lengths[i] = Math.Max (0, Along (infos[i].AbsoluteSize));
                        absolute += lengths[i];
                    } else {
                        autos.Add (i);
                    }
                }

                if (autos.Count > 0) {
                    var share = length - absolute - splitter * (autos.Count - 1);
                    var given = 0;

                    for (var k = 0; k < autos.Count - 1; k++) {
                        var scale = side_by_side ? infos[autos[k]].AutoSizeScale.Width : infos[autos[k]].AutoSizeScale.Height;
                        var value = Math.Max (0, (int) Math.Round (share * (1.0 / autos.Count + scale), MidpointRounding.AwayFromZero));
                        lengths[autos[k]] = value;
                        given += value;
                    }

                    lengths[autos[autos.Count - 1]] = Math.Max (0, room - absolute - given);
                }
            }

            var offset = side_by_side ? client.Left : client.Top;

            for (var i = 0; i < count; i++) {
                panels[i].Bounds = side_by_side
                    ? new Rectangle (offset, client.Top, lengths[i], client.Height)
                    : new Rectangle (client.Left, offset, client.Width, lengths[i]);
                offset += lengths[i] + splitter;
            }
        }
    }
}
