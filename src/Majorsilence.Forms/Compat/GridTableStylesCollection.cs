using System;
using System.Collections;
using System.Linq;

namespace Majorsilence.Forms
{
    public class GridTableStylesCollection : CollectionBase, ICollection, IEnumerable
    {
        public event EventHandler<DataGridTableStyle> CollectionChanged;

        public GridTableStylesCollection()
        {
        }

        public DataGridTableStyle this[int index]
        {
            get
            {
                if (InnerList.Count > 0 && index < InnerList.Count)
                {
                    return (DataGridTableStyle)InnerList[index];
                }
                return null;
            }
            set { InnerList[index] = value; }
        }

        public int Count
        {
            get { return InnerList.Count; }
        }

        public bool IsFixedSize => throw new NotImplementedException();

        public int Add(DataGridTableStyle item)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, item);
            }
            return InnerList.Add(item);
        }

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

        public void Clear()
        {
            InnerList.Clear();
        }

        public bool Contains(DataGridTableStyle item)
        {
            return InnerList.Contains(item);
        }

        public bool Contains(string mappingName)
        {
            return InnerList.Cast<DataGridTableStyle>().Any(p => string.Equals(p.MappingName, mappingName, StringComparison.OrdinalIgnoreCase));
        }

        public void CopyTo(DataGridTableStyle[] array, int arrayIndex)
        {
            InnerList.CopyTo(array, arrayIndex);
        }

        public int IndexOf(DataGridTableStyle item)
        {
            return InnerList.IndexOf(item);
        }

        public void Insert(int index, DataGridTableStyle item)
        {
            if (CollectionChanged != null)
            {
                CollectionChanged(this, item);
            }
            InnerList.Insert(index, item);
        }

        public void Remove(DataGridTableStyle item)
        {
            InnerList.Remove(item);
        }

        public void RemoveAt(int index)
        {
            InnerList.RemoveAt(index);
        }
    }
}
