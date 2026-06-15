namespace Modern.Forms
{
    /// <summary>
    /// Represents a collection of system provided mouse cursors.
    /// </summary>
    public static class Cursors
    {
        private static Cursor? app_starting;
        private static Cursor? arrow;
        private static Cursor? bottom_left_corner;
        private static Cursor? bottom_right_corner;
        private static Cursor? bottom_side;
        private static Cursor? cross;
        private static Cursor? drag_copy;
        private static Cursor? drag_link;
        private static Cursor? drag_move;
        private static Cursor? hand;
        private static Cursor? help;
        private static Cursor? ibeam;
        private static Cursor? left_side;
        private static Cursor? no;
        private static Cursor? right_side;
        private static Cursor? size_all;
        private static Cursor? size_north_south;
        private static Cursor? size_west_east;
        private static Cursor? top_left_corner;
        private static Cursor? top_right_corner;
        private static Cursor? top_side;
        private static Cursor? up_arrow;
        private static Cursor? wait;

        /// <summary>The default app starting cursor provided by the operating system.</summary>
        public static Cursor AppStarting => app_starting ??= new Cursor (Avalonia.Input.StandardCursorType.AppStarting);

        /// <summary>The default arrow cursor provided by the operating system.</summary>
        public static Cursor Arrow => arrow ??= new Cursor (Avalonia.Input.StandardCursorType.Arrow);

        /// <summary>The default bottom left corner cursor provided by the operating system.</summary>
        public static Cursor BottomLeftCorner => bottom_left_corner ??= new Cursor (Avalonia.Input.StandardCursorType.BottomLeftCorner);

        /// <summary>The default bottom right corner cursor provided by the operating system.</summary>
        public static Cursor BottomRightCorner => bottom_right_corner ??= new Cursor (Avalonia.Input.StandardCursorType.BottomRightCorner);

        /// <summary>The default bottom cursor provided by the operating system.</summary>
        public static Cursor BottomSide => bottom_side ??= new Cursor (Avalonia.Input.StandardCursorType.BottomSide);

        /// <summary>The default cross cursor provided by the operating system.</summary>
        public static Cursor Cross => cross ??= new Cursor (Avalonia.Input.StandardCursorType.Cross);

        /// <summary>The default drag copy cursor provided by the operating system.</summary>
        public static Cursor DragCopy => drag_copy ??= new Cursor (Avalonia.Input.StandardCursorType.DragCopy);

        /// <summary>The default drag link cursor provided by the operating system.</summary>
        public static Cursor DragLink => drag_link ??= new Cursor (Avalonia.Input.StandardCursorType.DragLink);

        /// <summary>The default drag move cursor provided by the operating system.</summary>
        public static Cursor DragMove => drag_move ??= new Cursor (Avalonia.Input.StandardCursorType.DragMove);

        /// <summary>The default hand cursor provided by the operating system.</summary>
        public static Cursor Hand => hand ??= new Cursor (Avalonia.Input.StandardCursorType.Hand);

        /// <summary>The default help cursor provided by the operating system.</summary>
        public static Cursor Help => help ??= new Cursor (Avalonia.Input.StandardCursorType.Help);

        /// <summary>The default ibeam cursor provided by the operating system.</summary>
        public static Cursor IBeam => ibeam ??= new Cursor (Avalonia.Input.StandardCursorType.Ibeam);

        /// <summary>The default left side cursor provided by the operating system.</summary>
        public static Cursor LeftSide => left_side ??= new Cursor (Avalonia.Input.StandardCursorType.LeftSide);

        /// <summary>The default no cursor provided by the operating system.</summary>
        public static Cursor No => no ??= new Cursor (Avalonia.Input.StandardCursorType.No);

        /// <summary>The default right side cursor provided by the operating system.</summary>
        public static Cursor RightSide => right_side ??= new Cursor (Avalonia.Input.StandardCursorType.RightSide);

        /// <summary>The default size all cursor provided by the operating system.</summary>
        public static Cursor SizeAll => size_all ??= new Cursor (Avalonia.Input.StandardCursorType.SizeAll);

        /// <summary>The default size north-south cursor provided by the operating system.</summary>
        public static Cursor SizeNorthSouth => size_north_south ??= new Cursor (Avalonia.Input.StandardCursorType.SizeNorthSouth);

        /// <summary>The default size west-east cursor provided by the operating system.</summary>
        public static Cursor SizeWestEast => size_west_east ??= new Cursor (Avalonia.Input.StandardCursorType.SizeWestEast);

        /// <summary>The default top left corner cursor provided by the operating system.</summary>
        public static Cursor TopLeftCorner => top_left_corner ??= new Cursor (Avalonia.Input.StandardCursorType.TopLeftCorner);

        /// <summary>The default top right corner cursor provided by the operating system.</summary>
        public static Cursor TopRightCorner => top_right_corner ??= new Cursor (Avalonia.Input.StandardCursorType.TopRightCorner);

        /// <summary>The default top side cursor provided by the operating system.</summary>
        public static Cursor TopSide => top_side ??= new Cursor (Avalonia.Input.StandardCursorType.TopSide);

        /// <summary>The default up arrow cursor provided by the operating system.</summary>
        public static Cursor UpArrow => up_arrow ??= new Cursor (Avalonia.Input.StandardCursorType.UpArrow);

        /// <summary>The default wait cursor provided by the operating system.</summary>
        public static Cursor Wait => wait ??= new Cursor (Avalonia.Input.StandardCursorType.Wait);

        /// <summary>The default cursor (alias for Arrow).</summary>
        public static Cursor Default => Arrow;

        /// <summary>The north-south resize cursor (alias for SizeNorthSouth).</summary>
        public static Cursor SizeNS => SizeNorthSouth;

        /// <summary>The west-east resize cursor (alias for SizeWestEast).</summary>
        public static Cursor SizeWE => SizeWestEast;

        /// <summary>The northeast-southwest resize cursor.</summary>
        public static Cursor SizeNESW => size_nesw ??= new Cursor (Avalonia.Input.StandardCursorType.BottomLeftCorner);

        /// <summary>The northwest-southeast resize cursor.</summary>
        public static Cursor SizeNWSE => size_nwse ??= new Cursor (Avalonia.Input.StandardCursorType.BottomRightCorner);

        /// <summary>The horizontal split cursor (alias for SizeWestEast).</summary>
        public static Cursor HSplit => SizeWestEast;

        /// <summary>The vertical split cursor (alias for SizeNorthSouth).</summary>
        public static Cursor VSplit => SizeNorthSouth;

        /// <summary>The no-move 2D cursor (alias for SizeAll).</summary>
        public static Cursor NoMove2D => SizeAll;

        /// <summary>The wait/busy cursor (alias for Wait).</summary>
        public static Cursor WaitCursor => Wait;

        /// <summary>The horizontal no-move cursor (stub — falls back to SizeWestEast).</summary>
        public static Cursor NoMoveHoriz => SizeWestEast;

        /// <summary>The vertical no-move cursor (stub — falls back to SizeNorthSouth).</summary>
        public static Cursor NoMoveVert => SizeNorthSouth;

        /// <summary>The pan-east cursor (stub — falls back to SizeWestEast).</summary>
        public static Cursor PanEast => SizeWestEast;

        /// <summary>The pan-northeast cursor (stub — falls back to SizeAll).</summary>
        public static Cursor PanNE => SizeAll;

        /// <summary>The pan-north cursor (stub — falls back to SizeNorthSouth).</summary>
        public static Cursor PanNorth => SizeNorthSouth;

        /// <summary>The pan-northwest cursor (stub — falls back to SizeAll).</summary>
        public static Cursor PanNW => SizeAll;

        /// <summary>The pan-southeast cursor (stub — falls back to SizeAll).</summary>
        public static Cursor PanSE => SizeAll;

        /// <summary>The pan-south cursor (stub — falls back to SizeNorthSouth).</summary>
        public static Cursor PanSouth => SizeNorthSouth;

        /// <summary>The pan-southwest cursor (stub — falls back to SizeAll).</summary>
        public static Cursor PanSW => SizeAll;

        /// <summary>The pan-west cursor (stub — falls back to SizeWestEast).</summary>
        public static Cursor PanWest => SizeWestEast;

        private static Cursor? size_nesw;
        private static Cursor? size_nwse;
    }
}
