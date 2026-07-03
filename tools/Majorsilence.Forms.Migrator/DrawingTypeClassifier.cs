namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Which bucket a <c>System.Drawing.&lt;Leaf&gt;</c> type name falls into, per <see cref="NamespaceMap"/>.
/// Extracted from <see cref="SourceConverter"/>'s Pass 2 so both the textual and Roslyn engines classify a
/// leaf type name identically — duplicating this logic is exactly the kind of thing that silently drifts
/// when someone adds a type to <see cref="NamespaceMap"/> later.
/// </summary>
internal enum DrawingTypeBucket
{
    /// <summary>A framework primitive (Color/Point/Size/Rectangle/…) — left alone, Majorsilence.Forms uses it as-is.</summary>
    Primitive,

    /// <summary>A GDI+ type (Bitmap/Brush/Pen/…) — redirected to <c>Majorsilence.Drawing</c>.</summary>
    MajorsilenceDrawing,

    /// <summary>A WinForms-compat drawing type (Graphics/SystemColors/…) — redirected to <c>Majorsilence.Forms</c>.</summary>
    MajorsilenceForms,

    /// <summary>A named type with no Majorsilence equivalent in either namespace — left as-is, warn.</summary>
    Unsupported,

    /// <summary>
    /// Not a leaf type at all — part of an already-unsupported sub-namespace (e.g. <c>System.Drawing.Design</c>),
    /// or simply unrecognized. Left as-is; the caller decides whether to warn (the sub-namespace passes handle
    /// their own reporting so this bucket alone shouldn't trigger a second, redundant warning).
    /// </summary>
    Unknown,
}

internal static class DrawingTypeClassifier
{
    /// <summary>
    /// Classifies a bare leaf name found after <c>System.Drawing.</c> (e.g. "Bitmap" from
    /// "System.Drawing.Bitmap"). Mirrors the bucket order used by <see cref="SourceConverter"/>'s Pass 2:
    /// primitives first, then Majorsilence.Drawing types, then Majorsilence.Forms types, then a check for
    /// whether the fully-qualified name falls under an already-flagged unsupported sub-namespace, else
    /// <see cref="DrawingTypeBucket.Unsupported"/>.
    /// </summary>
    public static DrawingTypeBucket Classify(string type)
    {
        if (NamespaceMap.DrawingPrimitives.Contains(type))
            return DrawingTypeBucket.Primitive;
        if (NamespaceMap.MajorsilenceDrawingTypes.Contains(type))
            return DrawingTypeBucket.MajorsilenceDrawing;
        if (NamespaceMap.MajorsilenceFormsTypes.Contains(type))
            return DrawingTypeBucket.MajorsilenceForms;

        // Don't mistake an unsupported sub-namespace (e.g. System.Drawing.Design.UITypeEditor) for a leaf
        // type — the caller's unsupported-namespace pass reports it once, cleanly.
        var qualified = $"System.Drawing.{type}";
        if (NamespaceMap.UnsupportedNamespaces.Any(u => u == qualified || u.StartsWith(qualified + ".", StringComparison.Ordinal)))
            return DrawingTypeBucket.Unknown;

        return DrawingTypeBucket.Unsupported;
    }
}
