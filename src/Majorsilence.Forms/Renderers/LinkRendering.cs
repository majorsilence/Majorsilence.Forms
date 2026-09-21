using System;
using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// The parts of drawing a hyperlink that every link surface in this assembly has to agree on.
    /// </summary>
    /// <remarks>
    /// There are three of them — <see cref="LinkLabel"/>, <see cref="ToolStripLabel"/> and
    /// <see cref="DataGridViewLinkCell"/> — and they were written at different times against the same
    /// WinForms enum. <see cref="LinkBehavior.SystemDefault"/> in particular is the DEFAULT value of
    /// every one of those properties, so a surface that resolved it differently would be wrong in the
    /// common case rather than an edge one. Resolving it in one place is what stops that.
    /// </remarks>
    internal static class LinkRendering
    {
        /// <summary>
        /// Whether a link with this behaviour is underlined right now.
        /// </summary>
        /// <param name="behavior">The link's <see cref="LinkBehavior"/>.</param>
        /// <param name="hovered">Whether the pointer is currently over the link.</param>
        internal static bool ShouldUnderline (LinkBehavior behavior, bool hovered)
            => (behavior == LinkBehavior.SystemDefault ? LinkBehavior.AlwaysUnderline : behavior) switch {
                LinkBehavior.AlwaysUnderline => true,
                LinkBehavior.HoverUnderline => hovered,
                LinkBehavior.NeverUnderline => false,
                _ => true
            };

        /// <summary>
        /// The run an underline occupies beneath a block of text, following the same horizontal
        /// alignment the text was drawn with.
        /// </summary>
        /// <remarks>
        /// The underline spans the TEXT, not the box it was drawn in: under a centred or right-aligned
        /// caption a box-width rule would run out under empty space.
        /// </remarks>
        /// <param name="bounds">The rectangle the text was drawn into.</param>
        /// <param name="text">The measured size of the text.</param>
        /// <param name="align">The alignment the text was drawn with.</param>
        internal static Rectangle UnderlineRun (Rectangle bounds, Size text, ContentAlignment align)
        {
            var width = Math.Min (text.Width, bounds.Width);

            var x = align switch {
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter
                    => bounds.Left + Math.Max (0, (bounds.Width - width) / 2),
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight
                    => bounds.Right - width,
                _ => bounds.Left
            };

            var y = align switch {
                ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight
                    => bounds.Top + text.Height,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight
                    => bounds.Bottom,
                _ => bounds.Top + (bounds.Height + text.Height) / 2
            };

            return new Rectangle (x, y, width, 0);
        }
    }
}
