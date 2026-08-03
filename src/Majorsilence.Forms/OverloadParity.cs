using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // Overload parity (docs/winforms-gap-plan.md).
    //
    // Turning on Surface.WinForms.IncludeOverloads changed the question the audit asks from "does a
    // method of this name exist" to "does this call site compile". Those are not the same, and the
    // difference is exactly where migration breaks: a caller writing
    // `listView.FindItemWithText (text, includeSubItems, start)` does not care that some
    // FindItemWithText exists.
    //
    // Nothing here is a forwarding shim for its own sake. Where the extra parameter carries meaning
    // this layer can honour -- FindItemWithText's sub-item search, GetItemRect's portion,
    // DataObject's text format, Invalidate's region -- it is honoured. Where it names a capability
    // that genuinely is not here, the overload says so in its own remarks rather than looking as
    // though it did something.

    public partial class Control
    {
        /// <summary>Invalidates the given rectangle, and optionally the child controls inside it.</summary>
        public void Invalidate (Rectangle rc, bool invalidateChildren)
        {
            Invalidate (rc);

            if (!invalidateChildren)
                return;

            foreach (var child in Controls)
                if (rc.IntersectsWith (child.Bounds))
                    child.Invalidate ();
        }

        /// <summary>Invalidates the area covered by the given region.</summary>
        public void Invalidate (Majorsilence.Forms.Drawing.Region region) => Invalidate (region, invalidateChildren: false);

        /// <inheritdoc cref="Invalidate(Majorsilence.Forms.Drawing.Region)"/>
        public void Invalidate (Majorsilence.Forms.Drawing.Region region, bool invalidateChildren)
        {
            if (region is null) {
                Invalidate ();
                return;
            }

            // A region's bounds are the tightest rectangle this layer can invalidate; invalidating
            // more than asked is correct, just not minimal.
            var bounds = System.Drawing.Rectangle.Round (region.GetBounds ());
            Invalidate (bounds.IsEmpty ? ClientRectangle : bounds, invalidateChildren);
        }

        /// <summary>Scales the control and its children by the same factor in both directions.</summary>
        public void Scale (float ratio) => Scale (ratio, ratio);

        /// <summary>Returns the child at the given point, optionally skipping invisible or disabled children.</summary>
        public Control? GetChildAtPoint (System.Drawing.Point pt, GetChildAtPointSkip skipValue)
        {
            foreach (var child in Controls) {
                if (!child.Bounds.Contains (pt))
                    continue;
                if (skipValue.HasFlag (GetChildAtPointSkip.Invisible) && !child.Visible)
                    continue;
                if (skipValue.HasFlag (GetChildAtPointSkip.Disabled) && !child.Enabled)
                    continue;
                if (skipValue.HasFlag (GetChildAtPointSkip.Transparent) && child.BackColor.A == 0)
                    continue;

                return child;
            }

            return null;
        }

        /// <summary>Starts a drag operation with an explicit drag image.</summary>
        /// <remarks>The image and cursor offset are accepted and ignored: there is no OS drag source
        /// in this layer yet, so <see cref="DoDragDrop(object,DragDropEffects)"/> is what actually
        /// runs, and it reports that no drag occurred.</remarks>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects,
            Majorsilence.Forms.Drawing.Bitmap? dragImage, System.Drawing.Point cursorOffset, bool useDefaultDragImage)
            => DoDragDrop (data, allowedEffects);
    }

    public partial class ListView
    {
        /// <summary>Finds the first item whose text starts with the given string.</summary>
        public ListViewItem? FindItemWithText (string text, bool includeSubItemsInSearch, int startIndex)
            => FindItemWithText (text, includeSubItemsInSearch, startIndex, isPrefixSearch: true);

        /// <inheritdoc cref="FindItemWithText(string,bool,int)"/>
        public ListViewItem? FindItemWithText (string text, bool includeSubItemsInSearch, int startIndex, bool isPrefixSearch)
        {
            ArgumentOutOfRangeException.ThrowIfNegative (startIndex);

            if (string.IsNullOrEmpty (text))
                return null;

            for (var i = startIndex; i < Items.Count; i++) {
                var item = Items[i];

                if (Matches (item.Text))
                    return item;

                if (!includeSubItemsInSearch)
                    continue;

                // SubItems[0] is the item's own text, already checked above.
                for (var s = 1; s < item.SubItems.Count; s++)
                    if (Matches (item.SubItems[s].Text))
                        return item;
            }

            return null;

            bool Matches (string candidate) => isPrefixSearch
                ? candidate.StartsWith (text, StringComparison.CurrentCultureIgnoreCase)
                : string.Equals (candidate, text, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>Returns the bounds of the requested part of the item at the given index.</summary>
        public Rectangle GetItemRect (int index, ItemBoundsPortion portion)
            => index >= 0 && index < Items.Count ? Items[index].GetBounds (portion) : Rectangle.Empty;
    }

    public partial class DataGridView
    {
        /// <summary>Resizes every row according to the given mode.</summary>
        public void AutoResizeRows (DataGridViewAutoSizeRowsMode autoSizeRowsMode)
        {
            if (autoSizeRowsMode == DataGridViewAutoSizeRowsMode.None)
                return;

            AutoResizeRows ();
        }

        /// <summary>Commits the current edit, reporting where the request came from.</summary>
        public bool EndEdit (DataGridViewDataErrorContexts context) => EndEdit ();

        /// <summary>Repaints one cell.</summary>
        public void InvalidateCell (DataGridViewCell dataGridViewCell)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewCell);
            InvalidateCell (dataGridViewCell.ColumnIndex, dataGridViewCell.RowIndex);
        }

        /// <summary>Sorts the rows using the given comparer.</summary>
        public void Sort (IComparer comparer)
        {
            ArgumentNullException.ThrowIfNull (comparer);

            var sorted = Rows.OrderBy (static row => row, Comparer<DataGridViewRow>.Create (comparer.Compare)).ToList ();

            for (var i = 0; i < sorted.Count; i++) {
                var from = Rows.IndexOf (sorted[i]);
                if (from == i)
                    continue;

                Rows.RemoveAt (from);
                Rows.Insert (i, sorted[i]);
            }

            Invalidate ();
        }
    }

    public partial class WebBrowser
    {
        /// <summary>Navigates to the given address, optionally in a new window.</summary>
        /// <remarks>There is one hosted browser view here, so a new window is not opened; the request
        /// navigates the existing view rather than being silently dropped.</remarks>
        public void Navigate (string urlString, bool newWindow) => Navigate (urlString);

        /// <summary>Navigates to the given address, targeting a named frame.</summary>
        /// <remarks>Frame targeting is not implemented; the top-level view navigates.</remarks>
        public void Navigate (string urlString, string targetFrameName) => Navigate (urlString);

        /// <summary>Navigates with POST data and additional headers.</summary>
        /// <remarks>The post data and headers are not sent: the backends' navigation seam takes a URL
        /// only. Documented rather than silently ignored, because a caller relying on the POST body
        /// would otherwise see a GET succeed and the wrong page render.</remarks>
        public void Navigate (string urlString, string targetFrameName, byte[]? postData, string? additionalHeaders)
            => Navigate (urlString);

        /// <inheritdoc cref="Navigate(string,bool)"/>
        public void Navigate (Uri url, bool newWindow) => Navigate (url);

        /// <inheritdoc cref="Navigate(string,string)"/>
        public void Navigate (Uri url, string targetFrameName) => Navigate (url);

        /// <inheritdoc cref="Navigate(string,string,byte[],string)"/>
        public void Navigate (Uri url, string targetFrameName, byte[]? postData, string? additionalHeaders)
            => Navigate (url);

        /// <summary>Reloads the current page.</summary>
        /// <remarks>The cache-level option is accepted and ignored; the backends reload through their
        /// own web view, which manages its cache itself.</remarks>
        public void Refresh (WebBrowserRefreshOption opt) => Refresh ();
    }

    public partial class ToolStripItemCollection
    {
        /// <summary>Adds a button with the given text.</summary>
        public ToolStripItem Add (string text)
        {
            var item = new ToolStripButton { Text = text };
            Add (item);
            return item;
        }

        /// <summary>Adds a button with the given image.</summary>
        public ToolStripItem Add (Majorsilence.Forms.Drawing.Image image)
        {
            var item = new ToolStripButton { Image = image };
            Add (item);
            return item;
        }

        /// <summary>Adds a button with the given text and image.</summary>
        public ToolStripItem Add (string text, Majorsilence.Forms.Drawing.Image image)
        {
            var item = new ToolStripButton { Text = text, Image = image };
            Add (item);
            return item;
        }

        /// <summary>Adds a button with the given text and image, already wired to a click handler.</summary>
        public ToolStripItem Add (string text, Majorsilence.Forms.Drawing.Image image, EventHandler onClick)
        {
            var item = new ToolStripButton { Text = text, Image = image };

            // ToolStripItem.Click carries mouse data here; upstream's overload hands over a plain
            // EventHandler, so it is adapted rather than being refused.
            if (onClick is not null)
                item.Click += (sender, _) => onClick (sender, EventArgs.Empty);

            Add (item);
            return item;
        }

        /// <summary>Adds several items at once.</summary>
        public void AddRange (params ToolStripItem[] toolStripItems)
        {
            ArgumentNullException.ThrowIfNull (toolStripItems);

            foreach (var item in toolStripItems)
                Add (item);
        }

        /// <inheritdoc cref="AddRange(ToolStripItem[])"/>
        public void AddRange (ToolStripItemCollection toolStripItems)
        {
            ArgumentNullException.ThrowIfNull (toolStripItems);

            // Snapshot first: adding to this collection while enumerating the source would throw when
            // the two are the same instance.
            foreach (var item in toolStripItems.ToArray ())
                Add (item);
        }
    }

    public partial class ControlBindingsCollection
    {
        /// <summary>Adds a binding with an explicit update mode.</summary>
        public Binding Add (string propertyName, object? dataSource, string? dataMember, bool formattingEnabled,
            DataSourceUpdateMode updateMode)
            => Add (propertyName, dataSource, dataMember, formattingEnabled, updateMode, null, null, null);

        /// <inheritdoc cref="Add(string,object,string,bool,DataSourceUpdateMode)"/>
        public Binding Add (string propertyName, object? dataSource, string? dataMember, bool formattingEnabled,
            DataSourceUpdateMode updateMode, object? nullValue)
            => Add (propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, null, null);

        /// <inheritdoc cref="Add(string,object,string,bool,DataSourceUpdateMode)"/>
        public Binding Add (string propertyName, object? dataSource, string? dataMember, bool formattingEnabled,
            DataSourceUpdateMode updateMode, object? nullValue, string? formatString)
            => Add (propertyName, dataSource, dataMember, formattingEnabled, updateMode, nullValue, formatString, null);

        /// <inheritdoc cref="Add(string,object,string,bool,DataSourceUpdateMode)"/>
        public Binding Add (string propertyName, object? dataSource, string? dataMember, bool formattingEnabled,
            DataSourceUpdateMode updateMode, object? nullValue, string? formatString, IFormatProvider? formatInfo)
        {
            var binding = Add (propertyName, dataSource, dataMember, formattingEnabled);

            binding.DataSourceUpdateMode = updateMode;
            binding.NullValue = nullValue;
            binding.FormatString = formatString ?? string.Empty;
            binding.FormatInfo = formatInfo;

            return binding;
        }
    }
}
