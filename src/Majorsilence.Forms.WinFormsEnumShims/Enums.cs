// These live under the "System.Windows.Forms" namespace deliberately: ComponentResourceManager's
// resolver hands this assembly back whenever a compiled .resx resource asks (by assembly-qualified
// name) for "System.Windows.Forms.DockStyle"/"AnchorStyles" (and the others declared here), so Type.GetType's by-name lookup
// within the returned assembly needs to find a type at exactly that namespace+name. The numeric
// values below are not arbitrary -- they're copied from System.Windows.Forms' own long-stable
// public values, since ComponentResourceManager converts a resolved value across to
// Majorsilence.Forms' own DockStyle/AnchorStyles by underlying integer, not by type identity.
namespace System.Windows.Forms
{
    /// <summary>Stand-in for <c>System.Windows.Forms.DockStyle</c> (see file remarks).</summary>
    public enum DockStyle
    {
        /// <summary>Not docked.</summary>
        None = 0,
        /// <summary>Docked to the top.</summary>
        Top = 1,
        /// <summary>Docked to the bottom.</summary>
        Bottom = 2,
        /// <summary>Docked to the left.</summary>
        Left = 3,
        /// <summary>Docked to the right.</summary>
        Right = 4,
        /// <summary>Fills the parent.</summary>
        Fill = 5,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.AnchorStyles</c> (see file remarks).</summary>
    [Flags]
    public enum AnchorStyles
    {
        /// <summary>Anchored to no edges.</summary>
        None = 0,
        /// <summary>Anchored to the top edge.</summary>
        Top = 0x01,
        /// <summary>Anchored to the bottom edge.</summary>
        Bottom = 0x02,
        /// <summary>Anchored to the left edge.</summary>
        Left = 0x04,
        /// <summary>Anchored to the right edge.</summary>
        Right = 0x08,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.TabAlignment</c> (see file remarks).</summary>
    public enum TabAlignment
    {
        /// <summary>Top.</summary>
        Top = 0,
        /// <summary>Bottom.</summary>
        Bottom = 1,
        /// <summary>Left.</summary>
        Left = 2,
        /// <summary>Right.</summary>
        Right = 3,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.TabAppearance</c> (see file remarks).</summary>
    public enum TabAppearance
    {
        /// <summary>Normal.</summary>
        Normal = 0,
        /// <summary>Buttons.</summary>
        Buttons = 1,
        /// <summary>FlatButtons.</summary>
        FlatButtons = 2,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.TabSizeMode</c> (see file remarks).</summary>
    public enum TabSizeMode
    {
        /// <summary>Normal.</summary>
        Normal = 0,
        /// <summary>FillToRight.</summary>
        FillToRight = 1,
        /// <summary>Fixed.</summary>
        Fixed = 2,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.ScrollBars</c> (see file remarks).</summary>
    public enum ScrollBars
    {
        /// <summary>None.</summary>
        None = 0,
        /// <summary>Horizontal.</summary>
        Horizontal = 1,
        /// <summary>Vertical.</summary>
        Vertical = 2,
        /// <summary>Both.</summary>
        Both = 3,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.FormStartPosition</c> (see file remarks).</summary>
    public enum FormStartPosition
    {
        /// <summary>Manual.</summary>
        Manual = 0,
        /// <summary>CenterScreen.</summary>
        CenterScreen = 1,
        /// <summary>WindowsDefaultLocation.</summary>
        WindowsDefaultLocation = 2,
        /// <summary>WindowsDefaultBounds.</summary>
        WindowsDefaultBounds = 3,
        /// <summary>CenterParent.</summary>
        CenterParent = 4,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.FlatStyle</c> (see file remarks).</summary>
    public enum FlatStyle
    {
        /// <summary>Flat.</summary>
        Flat = 0,
        /// <summary>Popup.</summary>
        Popup = 1,
        /// <summary>Standard.</summary>
        Standard = 2,
        /// <summary>System.</summary>
        System = 3,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.PictureBoxSizeMode</c> (see file remarks).</summary>
    public enum PictureBoxSizeMode
    {
        /// <summary>Normal.</summary>
        Normal = 0,
        /// <summary>StretchImage.</summary>
        StretchImage = 1,
        /// <summary>AutoSize.</summary>
        AutoSize = 2,
        /// <summary>CenterImage.</summary>
        CenterImage = 3,
        /// <summary>Zoom.</summary>
        Zoom = 4,
    }

    /// <summary>Stand-in for <c>System.Windows.Forms.Orientation</c> (see file remarks).</summary>
    public enum Orientation
    {
        /// <summary>Horizontal.</summary>
        Horizontal = 0,
        /// <summary>Vertical.</summary>
        Vertical = 1,
    }
}
