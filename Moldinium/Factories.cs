using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace IronStone.Moldinium
{
    public static partial class LiveList
    {
        /// <summary>
        /// Converts an observable collection to a live list. This has currently O(n) complexity,
        /// but could potentially be implemented with O(1).
        /// </summary>
        /// <typeparam name="T">The item type of the list.</typeparam>
        /// <param name="collection">The observable collection.</param>
        /// <returns>The live list.</returns>
        public static ILiveList<T> ToLiveList<T>(this ObservableCollection<T> collection)
        {
            // This could also be implemented without making a copy, but it's more difficult to get right.

            LiveList<T> lst = new LiveList<T>(collection);

            collection.CollectionChanged += (s, a) =>
            {
                switch (a.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        lst.InsertRange(a.NewStartingIndex, a.NewItems.Cast<T>());
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        for (int i = 0; i < a.OldItems.Count; ++i)
                            lst.RemoveAt(a.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        lst.Clear();
                        break;
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Move:
                    default:
                        throw new NotImplementedException();
                }
            };

            return lst;
        }

        public static ILiveList<T> Create<T>(Func<DLiveListObserver<T>, IObservable<Id>, IDisposable> subscribe)
        {
            return new ConcreteLiveList<T>(subscribe);
        }
    }
}
