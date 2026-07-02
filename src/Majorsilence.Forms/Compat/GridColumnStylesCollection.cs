using System;
using System.Collections;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Represents a collection of <see cref="IDataGridColumnStyle"/> objects for a <see cref="DataGridTableStyle"/>.
    /// </summary>
    public class GridColumnStylesCollection : CollectionBase, ICollection, IEnumerable
    {
        /// <summary>
        ///  Occurs when the collection changes, before the new column style is added or inserted.
        /// </summary>
        public event EventHandler<IDataGridColumnStyle>? CollectionChanged;

        /// <summary>
        ///  Initializes a new instance of the <see cref="GridColumnStylesCollection"/> class.
        /// </summary>
        public GridColumnStylesCollection()
        {
        }

        /// <summary>
        ///  Gets or sets the column style at the specified index.
        /// </summary>
        public IDataGridColumnStyle? this[int index]
        {
            get { return (IDataGridColumnStyle?)InnerList[index]; }
            set
            {
                if (value != null)
                {
                    InnerList[index] = value;
                }
            }
        }

        /// <summary>
        ///  Gets the number of column styles in the collection.
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
        ///  Adds the specified column style to the collection, unless a style with the same mapping name already exists.
        /// </summary>
        public int Add(IDataGridColumnStyle item)
        {
            if (Contains(item.MappingName))
            {
                return -1;
            }
            CollectionChanged?.Invoke(this, item);
            return InnerList.Add(item);
        }

        /// <summary>
        ///  Adds an array of column styles to the collection.
        /// </summary>
        public void AddRange(IDataGridColumnStyle[] styles)
        {
            foreach (var item in styles)
            {
                Add(item);
            }
        }

        /// <summary>
        ///  Removes all column styles from the collection.
        /// </summary>
        public new void Clear()
        {
            InnerList.Clear();
        }

        /// <summary>
        ///  Determines whether the collection contains the specified column style.
        /// </summary>
        public bool Contains(IDataGridColumnStyle item)
        {
            return InnerList.Contains(item);
        }

        /// <summary>
        ///  Determines whether the collection contains a column style with the specified mapping name.
        /// </summary>
        public bool Contains(string? mappingName)
        {
            return InnerList.Cast<IDataGridColumnStyle>().Any(p => string.Equals(p.MappingName, mappingName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///  Copies the column styles in the collection to an array, starting at the specified index.
        /// </summary>
        public void CopyTo(IDataGridColumnStyle[] array, int arrayIndex)
        {
            InnerList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        ///  Returns the index of the specified column style in the collection, or -1 if it is not found.
        /// </summary>
        public int IndexOf(DataGridColumnStyle item)
        {
            return InnerList.IndexOf(item);
        }

        /// <summary>
        ///  Inserts the specified column style into the collection at the given index, unless a style with the same mapping name already exists.
        /// </summary>
        public void Insert(int index, IDataGridColumnStyle item)
        {
            if (Contains(item.MappingName))
            {
                return;
            }
            CollectionChanged?.Invoke(this, item);
            InnerList.Insert(index, item);
        }

        /// <summary>
        ///  Removes the specified column style from the collection.
        /// </summary>
        public void Remove(IDataGridColumnStyle item)
        {
            InnerList.Remove(item);
        }

        /// <summary>
        ///  Removes the column style at the specified index from the collection.
        /// </summary>
        public new void RemoveAt(int index)
        {
            InnerList.RemoveAt(index);
        }
    }
}
