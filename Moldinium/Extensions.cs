using System;
using System.Collections.Generic;

namespace IronStone.Moldinium
{
    /// <summary>
    /// Provides various factories, helpers and extension methods for live lists.
    /// </summary>
    public static partial class LiveList
    {
        public static IDisposable Subscribe<T>(this ILiveList<T> source, ILiveListObserver<T> observer)
        {
            return source.Subscribe((type, item, id, previousId) => observer.OnNext(type, item, id, previousId), observer.RefreshRequested);
        }

        public static IEnumerable<TSource> ToEnumerable<TSource>(this ILiveList<TSource> source)
        {
            var lst = new List<TSource>();

            using (source.Subscribe((type, item, id, previousId) => lst.Add(item), null)) { }

            return lst;
        }

        public static ILiveList<TSource> Wrap<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>((onNext, refreshRequest) => source.Subscribe(onNext, refreshRequest));
        }

        public static ILiveList<TSource> AsLiveList<TSource>(this ILiveList<TSource> source)
        {
            return source;
        }

        struct OrderByAnyInfo
        {
            public Id? next, previous;
        }

        public static ILiveList<TSource> OrderByAny<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>((onNext, refreshRequest) =>
            {
                var keyToInfo = new Dictionary<Id, OrderByAnyInfo>();

                Id? lastKey = null;

                var subscription = source.Subscribe((type, item, id, ignoredPreviousKey) =>
                {
                    OrderByAnyInfo info;

                    var found = keyToInfo.TryGetValue(id, out info);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (found) throw new Exception("Id already added.");
                            onNext(ListEventType.Add, item, id, lastKey);
                            info.next = null;
                            info.previous = lastKey;
                            keyToInfo[id] = info;
                            lastKey = id;
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Id not found.");
                            onNext(ListEventType.Remove, item, id, info.previous);
                            keyToInfo.Remove(id);

                            if (info.previous.HasValue)
                            {
                                OrderByAnyInfo previousInfo;
                                if (!keyToInfo.TryGetValue(info.previous.Value, out previousInfo))
                                    throw new Exception("Previous id not found.");

                                previousInfo.next = info.next;
                                keyToInfo[info.previous.Value] = previousInfo;
                            }

                            if (info.next.HasValue)
                            {
                                OrderByAnyInfo nextInfo;
                                if (!keyToInfo.TryGetValue(info.next.Value, out nextInfo))
                                    throw new Exception("Next id not found.");

                                nextInfo.previous = info.previous;
                                keyToInfo[info.next.Value] = nextInfo;
                            }
                            else
                            {
                                if (lastKey != id)
                                    throw new Exception("Only the last id should have no next id.");

                                lastKey = info.previous;
                            }
                            break;
                    }
                }, refreshRequest);

                return subscription;
            });
        }
    }
}
