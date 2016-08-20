using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    public interface ILiveListGrouping<TKey, TSource> : ILiveList<TSource>
    {
        TKey Key { get; }
    }

    public class LiveListGrouping<TKey, TSource> : ILiveListGrouping<TKey, TSource>
    {
        TKey key;

        ILiveList<TSource> nested;

        public LiveListGrouping(TKey key, ILiveList<TSource> nested)
        {
            this.key = key;
            this.nested = nested;
        }

        public TKey Key { get { return key; } }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            return nested.Subscribe(observer, refreshRequested);
        }
    }

    public static partial class LiveList
    {
        public static ILiveList<ILiveListGrouping<TKey, TSource>> GroupBy<TSource, TKey>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException(); // FIXME
        }

        public static ILiveList<TResult> GroupBy<TSource, TKey, TResult>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, ILiveList<TSource>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, comparer).Select(g => resultSelector(g.Key, g));
        }

        //public static ILiveList<ILiveListGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        //{
        //    return source.ToLookup(keySelector, elementSelector, comparer);

        //    // FIXME: Having the two selector's evaluated outside of the lookup seems easier, but what are the performance implications?

        //    //return source.GroupBy(keySelector, comparer).Select(g => new LiveListGrouping<TKey, TElement>(g.Key, g.Select(elementSelector)));
        //}

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, comparer).Select(g => resultSelector(g.Key, g.Select(elementSelector)));
        }
    }
}
