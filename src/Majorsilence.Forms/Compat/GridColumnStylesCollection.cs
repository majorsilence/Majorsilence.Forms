using System;
using System.Collections;
using System.Linq;

namespace Majorsilence.Forms
{
    public class GridColumnStylesCollection : CollectionBase, ICollection, IEnumerable
    {
        public event EventHandler<IDataGridColumnStyle> CollectionChanged;

        public GridColumnStylesCollection()
        {
        }

        public IDataGridColumnStyle this[int index]
        {
            get { return (IDataGridColumnStyle)InnerList[index]; }
            set { InnerList[index] = value; }
        }

        public int Count
        {
            get { return InnerList.Count; }
        }

        public bool IsFixedSize => throw new NotImplementedException();

        public int Add(IDataGridColumnStyle item)
        {
            if (Contains(item.MappingName))
            {
                return -1;
            }
            CollectionChanged?.Invoke(this, item);
            return InnerList.Add(item);
        }

        public void AddRange(IDataGridColumnStyle[] styles)
        {
            foreach (var item in styles)
            {
                Add(item);
            }
        }

        public void Clear()
        {
            InnerList.Clear();
        }

        public bool Contains(IDataGridColumnStyle item)
        {
            return InnerList.Contains(item);
        }

        public bool Contains(string mappingName)
        {
            return InnerList.Cast<IDataGridColumnStyle>().Any(p => string.Equals(p.MappingName, mappingName, StringComparison.OrdinalIgnoreCase));
        }

        public void CopyTo(IDataGridColumnStyle[] array, int arrayIndex)
        {
            InnerList.CopyTo(array, arrayIndex);
        }

        public int IndexOf(DataGridColumnStyle item)
        {
            return InnerList.IndexOf(item);
        }

        public void Insert(int index, IDataGridColumnStyle item)
        {
            if (Contains(item.MappingName))
            {
                return;
            }
            CollectionChanged?.Invoke(this, item);
            InnerList.Insert(index, item);
        }

        public void Remove(IDataGridColumnStyle item)
        {
            InnerList.Remove(item);
        }

        public void RemoveAt(int index)
        {
            InnerList.RemoveAt(index);
        }
    }
}
