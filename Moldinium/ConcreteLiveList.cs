using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace IronStone.Moldinium
{
    // Pretty much every ILiveList<T> really is actually this thing here.
    class ConcreteLiveList<T> : ILiveList<T>, IEnumerable<T>, INotifyCollectionChanged
    {
        public ConcreteLiveList(Func<DLiveListObserver<T>, IObservable<Id>, IDisposable> subscribe)
        {
            this.subscribe = subscribe;
        }

        public IDisposable Subscribe(DLiveListObserver<T> observer, IObservable<Id> refresh)
        {
            return subscribe(observer, refresh);
        }

        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add {
                if (null == collectionChanged)
                {
                    if (null != selfSubscription) throw new Exception(
                        "Unexpected state in ConcreteLiveList on collection change subscription.");
                    manifestation = new List<Id>();
                    selfSubscription = subscribe(ProcessEvent, null);
                }

                collectionChanged += value;
            }

            remove {
                collectionChanged -= value;

                if (null == collectionChanged)
                {
                    if (null == selfSubscription) throw new Exception(
                        "Unexpected state in ConcreteLiveList on collection change unsubscription.");
                    selfSubscription.Dispose();
                    selfSubscription = null;
                    manifestation = null;
                }
            }
        }

        IEnumerator<T> GetEnumerator()
        {
            var lst = new List<T>();

            using (subscribe((type, item, id, previousId) => lst.Add(item), null)) { }

            return lst.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ProcessEvent(ListEventType type, T item, Id id, Id? previousId)
        {
            switch (type)
            {
                case ListEventType.Add:
                    var previousKeyIndex = previousId.HasValue ? manifestation.IndexOf(previousId.Value) + 1 : 0;
                    manifestation.Insert(previousKeyIndex, id);
                    collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add, item, previousKeyIndex));
                    break;
                case ListEventType.Remove:
                    var index = manifestation.IndexOf(id);
                    if (index < 0) throw new Exception("Id not in list.");
                    collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Remove, item, index));
                    manifestation.RemoveAt(index);
                    break;
                default:
                    break;
            }
        }

        Func<DLiveListObserver<T>, IObservable<Id>, IDisposable> subscribe;

        IDisposable selfSubscription = null;

        List<Id> manifestation = null;

        NotifyCollectionChangedEventHandler collectionChanged = null;
    }
}
