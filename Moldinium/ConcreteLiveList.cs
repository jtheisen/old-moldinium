using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace IronStone.Moldinium
{
    // Pretty much every ILiveList<T> really is actually this thing here.
    class ConcreteLiveList<T> : ILiveList<T>, IEnumerable<T>, INotifyCollectionChanged
    {
        public ConcreteLiveList(Func<DLiveListObserver<T>, ILiveListSubscription> subscribe)
        {
            this.subscribe = subscribe;
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<T> observer)
        {
            return subscribe(observer);
        }

        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add {
                if (null == collectionChanged)
                {
                    if (null != selfSubscription) throw new Exception(
                        "Unexpected state in ConcreteLiveList on collection change subscription.");
                    manifestation = new List<Id>();
                    selfSubscription = subscribe(ProcessEvent);
                }

                collectionChanged += value;
            }

            remove {
                collectionChanged -= value;

                if (null == collectionChanged)
                {
                    if (null == selfSubscription) throw new Exception(
                        "Unexpected state in ConcreteLiveList on collection change unsubscription.");
                    InternalExtensions.DisposeSafely(ref selfSubscription);
                    manifestation = null;
                }
            }
        }

        IEnumerator<T> GetEnumerator()
        {
            var lst = new List<T>();

            using (subscribe((type, item, id, previousId, nextId) => lst.Add(item))) { }

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

        void ProcessEvent(ListEventType type, T item, Id id, Id? previousId, Id? nextId)
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

        Func<DLiveListObserver<T>, ILiveListSubscription> subscribe;

        ILiveListSubscription selfSubscription = null;

        List<Id> manifestation = null;

        NotifyCollectionChangedEventHandler collectionChanged = null;
    }
}
