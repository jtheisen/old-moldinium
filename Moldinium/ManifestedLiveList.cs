using System;
using System.Collections;
using System.Collections.Generic;

namespace IronStone.Moldinium
{
    // Needed for tests, let's see what else we need it for.
    public class ManifestedLiveList<T> : ICollection, IEnumerable<T>, IEnumerable
    {
        public ManifestedLiveList(ILiveList<T> nested)
        {
            this.nested = nested;

            nested.Subscribe(OnNext, null);
        }

        void OnNext(ListEventType type, T item, Key key, Key? previousKey)
        {
            var index = previousKey.HasValue ? keys.IndexOf(previousKey.Value) : -1;

            switch (type)
            {
                case ListEventType.Add:
                    items.Insert(index + 1, item);
                    keys.Insert(index + 1, key);
                    break;
                case ListEventType.Remove:
                    items.RemoveAt(index + 1);
                    keys.RemoveAt(index + 1);
                    break;
                default:
                    break;
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)items).CopyTo(array, index);
        }

        Int32 ICollection.Count { get { return items.Count; } }

        Object ICollection.SyncRoot { get { return this; } }

        Boolean ICollection.IsSynchronized { get { return false; } }

        List<T> items = new List<T>();
        List<Key> keys = new List<Key>();

        ILiveList<T> nested;
    }
}
