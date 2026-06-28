namespace Majorsilence.Forms
{
    public enum MenuMerge
    {
        //
        // Summary:
        //     The Majorsilence.Forms.MenuItem is added to the collection of existing Majorsilence.Forms.MenuItem
        //     objects in a merged menu.
        Add,
        //
        // Summary:
        //     The Majorsilence.Forms.MenuItem replaces an existing Majorsilence.Forms.MenuItem
        //     at the same position in a merged menu.
        Replace,
        //
        // Summary:
        //     All submenu items of this Majorsilence.Forms.MenuItem are merged with those
        //     of existing Majorsilence.Forms.MenuItem objects at the same position in a merged
        //     menu.
        MergeItems,
        //
        // Summary:
        //     The Majorsilence.Forms.MenuItem is not included in a merged menu.
        Remove
    }
}
