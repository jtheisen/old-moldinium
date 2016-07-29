using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IronStone.Moldinium
{
    public interface IGroupedLiveList<TKey, TSource> : ILiveList<TSource>
    {
        TKey Key { get; }
    }

    public class GroupedLiveList<TKey, TSource> : IGroupedLiveList<TKey, TSource>
    {
        TKey key;

        ILiveList<TSource> nested;

        public GroupedLiveList(TKey key, ILiveList<TSource> nested)
        {
            this.key = key;
            this.nested = nested;
        }

        public TKey Key { get { return key; } }

        public IEnumerator<TSource> GetEnumerator()
        {
            return nested.GetEnumerator();
        }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            return nested.Subscribe(observer, refreshRequested);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return nested.GetEnumerator();
        }
    }

    public static partial class Extensions
    {
        public static ILiveList<IGroupedLiveList<TKey, TSource>> GroupBy<TSource, TKey>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
        }

        public static ILiveList<TResult> GroupBy<TSource, TKey, TResult>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, ILiveList<TSource>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, comparer).Select(g => resultSelector(g.Key, g));
        }

        public static ILiveList<IGroupedLiveList<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, comparer).Select(g => new GroupedLiveList<TKey, TElement>(g.Key, g.Select(elementSelector)));
        }

        public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, IEnumerable<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, comparer).Select(g => resultSelector(g.Key, g.Select(elementSelector)));
        }

        public static ILiveList<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, ILiveList<TInner>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
        }

        public static ILiveList<TResult> Join<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, il) => new { OuterItem = o, InnerList = il }, comparer)
                .SelectMany(p => p.InnerList, (p, i) => resultSelector(p.OuterItem, i));
        }
    }
}
