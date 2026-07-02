namespace Majorsilence.Forms
{
    /// <summary>
    ///  Specifies how a menu item is merged with items of another menu.
    /// </summary>
    public enum MenuMerge
    {
        /// <summary>
        ///  The menu item is added to the collection of existing menu items in a merged menu.
        /// </summary>
        Add,

        /// <summary>
        ///  The menu item replaces an existing menu item at the same position in a merged menu.
        /// </summary>
        Replace,

        /// <summary>
        ///  All submenu items of this menu item are merged with those of existing menu items at the same position in a merged menu.
        /// </summary>
        MergeItems,

        /// <summary>
        ///  The menu item is not included in a merged menu.
        /// </summary>
        Remove
    }
}
