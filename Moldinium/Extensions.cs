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
            return source.Subscribe((type, item, key, previousKey) => observer.OnNext(type, item, key, previousKey), observer.RefreshRequested);
        }

        public static IEnumerable<TSource> ToEnumerable<TSource>(this ILiveList<TSource> source)
        {
            var lst = new List<TSource>();

            using (source.Subscribe((type, item, key, previousKey) => lst.Add(item), null)) { }

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
            public Key? next, previous;
        }

        public static ILiveList<TSource> OrderByAny<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>((onNext, refreshRequest) =>
            {
                var keyToInfo = new Dictionary<Key, OrderByAnyInfo>();

                Key? lastKey = null;

                var subscription = source.Subscribe((type, item, key, ignoredPreviousKey) =>
                {
                    OrderByAnyInfo info;

                    var found = keyToInfo.TryGetValue(key, out info);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (found) throw new Exception("Key already added.");
                            onNext(ListEventType.Add, item, key, lastKey);
                            info.next = null;
                            info.previous = lastKey;
                            keyToInfo[key] = info;
                            lastKey = key;
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Key not found.");
                            onNext(ListEventType.Remove, item, key, info.previous);
                            keyToInfo.Remove(key);

                            if (info.previous.HasValue)
                            {
                                OrderByAnyInfo previousInfo;
                                if (!keyToInfo.TryGetValue(info.previous.Value, out previousInfo))
                                    throw new Exception("Previous key not found.");

                                previousInfo.next = info.next;
                                keyToInfo[info.previous.Value] = previousInfo;
                            }

                            if (info.next.HasValue)
                            {
                                OrderByAnyInfo nextInfo;
                                if (!keyToInfo.TryGetValue(info.next.Value, out nextInfo))
                                    throw new Exception("Next key not found.");

                                nextInfo.previous = info.previous;
                                keyToInfo[info.next.Value] = nextInfo;
                            }
                            else
                            {
                                if (lastKey != key)
                                    throw new Exception("Only the last key should have no next key.");

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
