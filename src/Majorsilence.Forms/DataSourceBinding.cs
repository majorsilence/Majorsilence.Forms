using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Shared list-binding helpers for the data-bound controls (<see cref="ListBox"/>,
    /// <see cref="ComboBox"/>).
    ///
    /// Two things they all need, and which a plain <c>is IList</c> plus
    /// <c>GetType().GetProperty(member)</c> gets wrong:
    ///
    /// 1. The common WinForms data source is a <c>DataTable</c>, which is NOT an
    ///    <see cref="IList"/> -- it is an <see cref="IListSource"/> whose <c>GetList()</c> returns the
    ///    default view. Treating a non-IList source as "no source" left the item collection empty, so
    ///    a form that bound a table and then selected a row threw
    ///    ArgumentOutOfRangeException from the SelectedIndex setter.
    /// 2. Items from such a view are <c>DataRowView</c>s, whose columns are exposed as
    ///    <see cref="PropertyDescriptor"/>s rather than CLR properties, so reflection over
    ///    <c>GetProperty</c> never finds DisplayMember/ValueMember. WinForms reads these through
    ///    <see cref="TypeDescriptor"/>, which handles both shapes.
    /// </summary>
    internal static class DataSourceBinding
    {
        /// <summary>
        /// The bindable list behind a data source: the source itself when it is already a list, or the
        /// list an <see cref="IListSource"/> (DataTable, DataView, DataSet) produces. Null when the
        /// source cannot be enumerated as a list.
        /// </summary>
        internal static IList? AsList (object? dataSource) => dataSource switch {
            IList list => list,
            IListSource source => source.GetList (),
            _ => null,
        };

        /// <summary>
        /// The value of <paramref name="member"/> on <paramref name="item"/>, or null when the item is
        /// null or exposes no such member. Property descriptors are consulted first so custom type
        /// descriptors (notably <c>DataRowView</c>'s columns) resolve before CLR reflection.
        /// </summary>
        [UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "DataSource item types require runtime reflection — same as WinForms.")]
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "TypeDescriptor.GetProperties is how WinForms itself resolves DisplayMember/ValueMember; a data source's item type is only known at runtime. A trimmed-away descriptor degrades to the CLR property lookup below, and then to the item's own ToString.")]
        internal static object? MemberValue (object? item, string? member)
        {
            if (item is null || string.IsNullOrEmpty (member))
                return null;

            var descriptor = TypeDescriptor.GetProperties (item)[member];

            if (descriptor is not null)
                return descriptor.GetValue (item);

            return item.GetType ().GetProperty (member)?.GetValue (item);
        }

        /// <summary>
        /// The text a bound control shows for <paramref name="item"/>: the display member's value when
        /// one is set and resolvable, else the item's own string form.
        /// </summary>
        internal static string DisplayText (object? item, string? displayMember)
        {
            if (!string.IsNullOrEmpty (displayMember)) {
                var value = MemberValue (item, displayMember);

                if (value is not null)
                    return value.ToString () ?? string.Empty;
            }

            return item?.ToString () ?? string.Empty;
        }
    }
}
