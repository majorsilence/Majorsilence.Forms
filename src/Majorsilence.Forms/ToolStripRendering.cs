using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The ToolStrip rendering surface (docs/winforms-gap-plan.md, item 4).
    //
    // ToolStripRenderer was an empty abstract class here -- 41 of 41 members missing -- which meant
    // custom toolbar and menu chrome was not merely unstyled but impossible to write: there was
    // nothing to override. Every LOB app that themes its toolbars does so by subclassing this.
    //
    // The shape is WinForms' own three-part pattern, and it has to be all three parts to be useful:
    //   public  DrawX(args)      what the ToolStrip calls to paint something
    //   event   RenderX          what a caller hooks without subclassing
    //   protected virtual OnRenderX(args)   what a subclass overrides
    // DrawX raises the event and then calls OnRenderX, so both extension routes work and a subclass
    // that overrides OnRenderX sees every paint the ToolStrip performs.
    //
    // The base implementations are intentionally empty rather than drawing a default chrome: this
    // layer paints ToolStrips through its own theme, and a base that painted would double-draw
    // underneath a subclass that also paints. ToolStripProfessionalRenderer/ToolStripSystemRenderer
    // remain the styled options.

    /// <summary>Provides the data needed to render a <see cref="ToolStrip"/> grip.</summary>
    public class ToolStripGripRenderEventArgs : ToolStripRenderEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripGripRenderEventArgs"/> class.</summary>
        public ToolStripGripRenderEventArgs (Graphics g, ToolStrip toolStrip)
            : base (g, toolStrip, Rectangle.Empty, Color.Empty) { }

        /// <summary>Gets the bounds of the grip.</summary>
        public Rectangle GripBounds { get; init; }

        /// <summary>Gets whether the grip is drawn horizontally or vertically.</summary>
        public ToolStripGripDisplayStyle GripDisplayStyle { get; init; }

        /// <summary>Gets the visibility of the grip.</summary>
        public ToolStripGripStyle GripStyle { get; init; }
    }

    /// <summary>Provides the data needed to render a <see cref="ToolStripItem"/>'s image.</summary>
    public class ToolStripItemImageRenderEventArgs : ToolStripItemRenderEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripItemImageRenderEventArgs"/> class.</summary>
        public ToolStripItemImageRenderEventArgs (Graphics g, ToolStripItem item, Rectangle imageRectangle)
            : base (g, item) => ImageRectangle = imageRectangle;

        /// <summary>Initializes a new instance with an explicit image.</summary>
        public ToolStripItemImageRenderEventArgs (Graphics g, ToolStripItem item, Majorsilence.Forms.Drawing.Image image, Rectangle imageRectangle)
            : base (g, item)
        {
            Image = image;
            ImageRectangle = imageRectangle;
        }

        /// <summary>Gets the image to draw.</summary>
        public Majorsilence.Forms.Drawing.Image? Image { get; }

        /// <summary>Gets the rectangle the image is drawn in.</summary>
        public Rectangle ImageRectangle { get; }
    }

    /// <summary>Provides the data needed to render a <see cref="ToolStripItem"/>'s text.</summary>
    public class ToolStripItemTextRenderEventArgs : ToolStripItemRenderEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripItemTextRenderEventArgs"/> class.</summary>
        public ToolStripItemTextRenderEventArgs (Graphics g, ToolStripItem item, string text,
            Rectangle textRectangle, Color textColor, Majorsilence.Forms.Drawing.Font textFont, TextFormatFlags format)
            : base (g, item)
        {
            Text = text;
            TextRectangle = textRectangle;
            TextColor = textColor;
            TextFont = textFont;
            TextFormat = format;
        }

        /// <summary>Initializes a new instance using a content alignment instead of format flags.</summary>
        public ToolStripItemTextRenderEventArgs (Graphics g, ToolStripItem item, string text,
            Rectangle textRectangle, Color textColor, Majorsilence.Forms.Drawing.Font textFont, ContentAlignment textAlign)
            : base (g, item)
        {
            Text = text;
            TextRectangle = textRectangle;
            TextColor = textColor;
            TextFont = textFont;
            TextAlign = textAlign;
        }

        /// <summary>Gets or sets the text to draw.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the color the text is drawn in.</summary>
        public Color TextColor { get; set; }

        /// <summary>Gets or sets the font the text is drawn with.</summary>
        public Majorsilence.Forms.Drawing.Font? TextFont { get; set; }

        /// <summary>Gets or sets the rectangle the text is drawn in.</summary>
        public Rectangle TextRectangle { get; set; }

        /// <summary>Gets or sets the formatting applied to the text.</summary>
        public TextFormatFlags TextFormat { get; set; }

        /// <summary>Gets or sets the alignment of the text within its rectangle.</summary>
        public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>Gets or sets the direction the text runs in.</summary>
        public ToolStripTextDirection TextDirection { get; set; } = ToolStripTextDirection.Horizontal;
    }

    /// <summary>Provides the data needed to render a <see cref="ToolStripSeparator"/>.</summary>
    public class ToolStripSeparatorRenderEventArgs : ToolStripItemRenderEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripSeparatorRenderEventArgs"/> class.</summary>
        public ToolStripSeparatorRenderEventArgs (Graphics g, ToolStripSeparator separator, bool vertical)
            : base (g, separator) => Vertical = vertical;

        /// <summary>Gets whether the separator is drawn vertically.</summary>
        public bool Vertical { get; }
    }
}
