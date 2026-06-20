namespace Continuum.Forms.Backends
{
    /// <summary>
    /// A backend-neutral set of standard mouse cursors. A backend maps these to its own
    /// native cursor types (e.g. Avalonia's <c>StandardCursorType</c>).
    /// </summary>
    public enum CursorType
    {
        /// <summary>The standard arrow cursor.</summary>
        Arrow,
        /// <summary>The application-starting (arrow + busy) cursor.</summary>
        AppStarting,
        /// <summary>The cross-hair cursor.</summary>
        Cross,
        /// <summary>The hand (link) cursor.</summary>
        Hand,
        /// <summary>The help (arrow + question mark) cursor.</summary>
        Help,
        /// <summary>The text I-beam cursor.</summary>
        Ibeam,
        /// <summary>The "not allowed" cursor.</summary>
        No,
        /// <summary>The up-arrow cursor.</summary>
        UpArrow,
        /// <summary>The wait/busy cursor.</summary>
        Wait,
        /// <summary>The move-all (4-way) resize cursor.</summary>
        SizeAll,
        /// <summary>The north-south (vertical) resize cursor.</summary>
        SizeNorthSouth,
        /// <summary>The west-east (horizontal) resize cursor.</summary>
        SizeWestEast,
        /// <summary>The top-edge resize cursor.</summary>
        TopSide,
        /// <summary>The bottom-edge resize cursor.</summary>
        BottomSide,
        /// <summary>The left-edge resize cursor.</summary>
        LeftSide,
        /// <summary>The right-edge resize cursor.</summary>
        RightSide,
        /// <summary>The top-left corner resize cursor.</summary>
        TopLeftCorner,
        /// <summary>The top-right corner resize cursor.</summary>
        TopRightCorner,
        /// <summary>The bottom-left corner resize cursor.</summary>
        BottomLeftCorner,
        /// <summary>The bottom-right corner resize cursor.</summary>
        BottomRightCorner,
        /// <summary>The drag-copy cursor.</summary>
        DragCopy,
        /// <summary>The drag-link cursor.</summary>
        DragLink,
        /// <summary>The drag-move cursor.</summary>
        DragMove
    }
}
