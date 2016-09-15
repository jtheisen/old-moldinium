using System;
using System.Collections.Generic;

namespace IronStone.Moldinium
{
    /// <summary>
    /// Provides various factories, helpers and extension methods for live lists.
    /// </summary>
    public static partial class LiveList
    {
        public static ILiveListSubscription Subscribe<T>(this ILiveList<T> source, ILiveListObserver<T> observer)
        {
            return source.Subscribe((type, item, id, previousId, nextId) => observer.OnNext(type, item, id, previousId, nextId));
        }

        public static IEnumerable<TSource> ToEnumerable<TSource>(this ILiveList<TSource> source)
        {
            var lst = new List<TSource>();

            using (source.Subscribe((type, item, id, previousId, nextId) => lst.Add(item))) { }

            return lst;
        }

        public static ILiveList<TSource> Wrap<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>(source.Subscribe);
        }

        public static ILiveList<TSource> AsLiveList<TSource>(this ILiveList<TSource> source)
        {
            return source;
        }

        struct OrderByInsertionInfo
        {
            public Id? next, previous;
        }

        public static ILiveList<TSource> OrderByInsertion<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>(onNext =>
            {
                var keyToInfo = new Dictionary<Id, OrderByInsertionInfo>();

                Id? lastId = null;

                var subscription = source.Subscribe((type, item, id, ignoredPreviousId, ignoredNextId) =>
                {
                    OrderByInsertionInfo info;

                    var found = keyToInfo.TryGetValue(id, out info);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (found) throw new Exception("Id already added.");
                            onNext(ListEventType.Add, item, id, lastId, null);
                            info.next = null;
                            info.previous = lastId;
                            keyToInfo[id] = info;
                            lastId = id;
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Id not found.");
                            onNext(ListEventType.Remove, item, id, info.previous, info.next);
                            keyToInfo.Remove(id);

                            if (info.previous.HasValue)
                            {
                                OrderByInsertionInfo previousInfo;
                                if (!keyToInfo.TryGetValue(info.previous.Value, out previousInfo))
                                    throw new Exception("Previous id not found.");

                                previousInfo.next = info.next;
                                keyToInfo[info.previous.Value] = previousInfo;
                            }

                            if (info.next.HasValue)
                            {
                                OrderByInsertionInfo nextInfo;
                                if (!keyToInfo.TryGetValue(info.next.Value, out nextInfo))
                                    throw new Exception("Next id not found.");

                                nextInfo.previous = info.previous;
                                keyToInfo[info.next.Value] = nextInfo;
                            }
                            else
                            {
                                if (lastId != id)
                                    throw new Exception("Only the last id should have no next id.");

                                lastId = info.previous;
                            }
                            break;
                    }
                });

                return subscription;
            });
        }

        public static ILiveList<TSource> Reverse<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>(onNext =>
                source.Subscribe((type, item, id, previousId, nextId) => onNext(type, item, id, nextId, previousId))
            );
        }
    }
}
