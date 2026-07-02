using System;
using System.Collections;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Represents a collection of <see cref="DataGridTableStyle"/> objects for a <see cref="DataGrid"/>.
    /// </summary>
    public class GridTableStylesCollection : CollectionBase, ICollection, IEnumerable
    {
        /// <summary>
        ///  Occurs when the collection changes, before the new table style is added or inserted.
        /// </summary>
        public event EventHandler<DataGridTableStyle>? CollectionChanged;

        /// <summary>
        ///  Initializes a new instance of the <see cref="GridTableStylesCollection"/> class.
        /// </summary>
        public GridTableStylesCollection()
        {
        }

        /// <summary>
        ///  Gets or sets the table style at the specified index, or <see langword="null"/> if the index is out of range.
        /// </summary>
        public DataGridTableStyle? this[int index]
        {
            get
            {
                if (InnerList.Count > 0 && index < InnerList.Count)
                {
                    return (DataGridTableStyle?)InnerList[index];
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    InnerList[index] = value;
                }
            }
        }

        /// <summary>
        ///  Gets the number of table styles in the collection.
        /// </summary>
        public new int Count
        {
            get { return InnerList.Count; }
        }

        /// <summary>
        ///  Gets a value indicating whether the collection has a fixed size. Not implemented by this compat shim.
        /// </summary>
        public bool IsFixedSize => throw new NotImplementedException();

        /// <summary>
        ///  Adds the specified table style to the collection.
        /// </summary>
        public int Add(DataGridTableStyle item)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, item);
            }
            return InnerList.Add(item);
        }

        /// <summary>
        ///  Adds an array of table styles to the collection.
        /// </summary>
        public void AddRange(DataGridTableStyle[] styles)
        {
            if (CollectionChanged != null)
            {
                foreach (var item in styles)
                {
                    CollectionChanged(this, item);
                }
            }
            InnerList.AddRange(styles);
        }

        /// <summary>
        ///  Removes all table styles from the collection.
        /// </summary>
        public new void Clear()
        {
            InnerList.Clear();
        }

        /// <summary>
        ///  Determines whether the collection contains the specified table style.
        /// </summary>
        public bool Contains(DataGridTableStyle item)
        {
            return InnerList.Contains(item);
        }

        /// <summary>
        ///  Determines whether the collection contains a table style with the specified mapping name.
        /// </summary>
        public bool Contains(string? mappingName)
        {
            return InnerList.Cast<DataGridTableStyle>().Any(p => string.Equals(p.MappingName, mappingName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///  Copies the table styles in the collection to an array, starting at the specified index.
        /// </summary>
        public void CopyTo(DataGridTableStyle[] array, int arrayIndex)
        {
            InnerList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        ///  Returns the index of the specified table style in the collection, or -1 if it is not found.
        /// </summary>
        public int IndexOf(DataGridTableStyle item)
        {
            return InnerList.IndexOf(item);
        }

        /// <summary>
        ///  Inserts the specified table style into the collection at the given index.
        /// </summary>
        public void Insert(int index, DataGridTableStyle item)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, item);
            }
            InnerList.Insert(index, item);
        }

        /// <summary>
        ///  Removes the specified table style from the collection.
        /// </summary>
        public void Remove(DataGridTableStyle item)
        {
            InnerList.Remove(item);
        }

        /// <summary>
        ///  Removes the table style at the specified index from the collection.
        /// </summary>
        public new void RemoveAt(int index)
        {
            InnerList.RemoveAt(index);
        }
    }
}
