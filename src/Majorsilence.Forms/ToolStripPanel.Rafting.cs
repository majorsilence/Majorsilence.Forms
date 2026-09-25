using System;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // Rafting (W6 mechanisms): dragging a strip by its grip moves it to another row of the panel it
    // sits in. Join was the only way to move a strip before, which is why ToolStripPanel.Locked was
    // stored and read by nothing -- there was no gesture for it to refuse.
    public partial class ToolStripPanel
    {
        /// <summary>The strip currently being dragged by its grip, or null.</summary>
        internal ToolStrip? DraggingStrip { get; private set; }

        /// <summary>Where in the panel the drag started, in the panel's client space.</summary>
        internal Point DragOrigin { get; private set; }

        /// <summary>
        /// Begins a rafting drag of <paramref name="strip"/>, started at <paramref name="panelPoint"/>
        /// in this panel's client space. A locked panel refuses, and answers false.
        /// </summary>
        internal bool BeginRaft (ToolStrip strip, Point panelPoint)
        {
            // Locked is what this gesture exists to be refused by: a locked panel keeps the layout the
            // application gave it, and Join still works because that is the programmatic path.
            if (Locked || !Controls.Contains (strip))
                return false;

            DraggingStrip = strip;
            DragOrigin = panelPoint;
            return true;
        }

        /// <summary>
        /// Ends a rafting drag at <paramref name="panelPoint"/>. The strip joins the row under that
        /// point, or a new row past the end when it is below them all. Answers whether it moved.
        /// </summary>
        internal bool EndRaft (Point panelPoint)
        {
            if (DraggingStrip is not { } strip)
                return false;

            DraggingStrip = null;

            // A press and release in the same place is not a drag, so the strip stays where it is.
            if (Math.Abs (panelPoint.X - DragOrigin.X) <= SystemInformation.DragSize.Width
                && Math.Abs (panelPoint.Y - DragOrigin.Y) <= SystemInformation.DragSize.Height)
                return false;

            var target = PointToRow (panelPoint);
            var index = target is null ? Rows.Length : Array.IndexOf (Rows, target);
            var current = RowIndexOf (strip);

            if (index == current)
                return false;

            // Join takes the strip off the row it was on.
            Join (strip, index);
            PerformLayout ();
            return true;
        }

        /// <summary>The row the strip currently sits on, or -1.</summary>
        internal int RowIndexOf (ToolStrip strip)
        {
            for (var i = 0; i < Rows.Length; i++)
                if (Rows[i].Controls.Contains (strip))
                    return i;

            return -1;
        }
    }

    public partial class ToolStrip
    {
        // The grip drag that rafts this strip between the rows of the panel it is in (W6 mechanisms).
        private bool rafting;

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (e.Button != MouseButtons.Left || GripStyle != ToolStripGripStyle.Visible)
                return;

            // The grip band is the strip's own leading edge; the pointer is logical, as GripRectangle
            // is (RC-8).
            if (!GripRectangle.Contains (e.Location) || Parent is not ToolStripPanel panel)
                return;

            rafting = panel.BeginRaft (this, PanelPoint (panel, e.Location));
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (!rafting || Parent is not ToolStripPanel panel)
                return;

            rafting = false;
            panel.EndRaft (PanelPoint (panel, e.Location));
        }

        // A point in this strip's client space, in the panel's.
        private Point PanelPoint (ToolStripPanel panel, Point local)
            => new Point (local.X + Left - panel.Left, local.Y + Top - panel.Top);
    }
}
