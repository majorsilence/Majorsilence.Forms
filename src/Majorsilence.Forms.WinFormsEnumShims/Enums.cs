// These live under the "System.Windows.Forms" namespace deliberately: ComponentResourceManager's
// resolver hands this assembly back whenever a compiled .resx resource asks (by assembly-qualified
// name) for "System.Windows.Forms.DockStyle"/"AnchorStyles", so Type.GetType's by-name lookup
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
}
