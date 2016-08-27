using System;
using System.Collections;
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
    }
}
