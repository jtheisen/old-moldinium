using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    public interface ILiveListGrouping<TKey, TSource> : ILiveList<TSource>
    {
        TKey Key { get; }
    }

    public interface ILiveLookoup<TKey, TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>, IDisposable
    {
        ILiveList<TElement> this[TKey key] { get; }
    }

    public class LiveListGrouping<TKey, TSource> : LiveListSubject<TSource>, ILiveListGrouping<TKey, TSource>
    {
        TKey key;

        public LiveListGrouping(TKey key)
        {
            this.key = key;
        }

        public TKey Key { get { return key; } }
    }

    public class LiveLookup<TKey, TSource, TElement> : ILiveLookoup<TKey, TElement>
    {
        Func<TSource, TKey> keySelector;
        Func<TSource, TElement> elementSelector;
        Dictionary<TKey, LiveListGrouping<TKey, TElement>> groupingsByGroupingKey;
        Dictionary<Key, TKey> groupingKeysByKey;

        Subject<Key> refreshRequested;

        LiveList<ILiveListGrouping<TKey, TElement>> groupings;

        Dictionary<Key, Attachment> manifestation;

        IDisposable subscription;

        struct Attachment
        {
            public TElement element;
        }

        public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;

            groupingsByGroupingKey = new Dictionary<TKey, LiveListGrouping<TKey, TElement>>(comparer);
            groupingKeysByKey = new Dictionary<Key, TKey>();
            groupings = new LiveList<ILiveListGrouping<TKey, TElement>>();

            manifestation = new Dictionary<Key, Attachment>();

            subscription = source.Subscribe(Handle, refreshRequested);
        }

        void Handle(ListEventType type, TSource item, Key key, Key? previousKey)
        {
            LiveListGrouping<TKey, TElement> grouping;
            TKey groupingKey;

            Attachment attachment;

            switch (type)
            {
                case ListEventType.Add:
                    groupingKey = keySelector(item); // FIXME: watchable support

                    if (!groupingsByGroupingKey.TryGetValue(groupingKey, out grouping))
                    {
                        var newKey = KeyHelper.Create();
                        var liveList = LiveList.Create<TElement>((onNext, downwardsRefreshRequests) => {
                            return null;
                        });
                        groupingsByGroupingKey[groupingKey] = new LiveListGrouping<TKey, TElement>(groupingKey);
                        groupingKeysByKey[newKey] = groupingKey;
                        groupings.Add(grouping);
                    }

                    // FIXME: watchables
                    var element = elementSelector(item);

                    attachment.element = element;

                    manifestation[key] = attachment;

                    grouping.OnNext(type, element, key, previousKey);

                    break;
                case ListEventType.Remove:
                    groupingKey = groupingKeysByKey[key];

                    if (!groupingsByGroupingKey.TryGetValue(groupingKey, out grouping))
                        throw new Exception("Grouping key not found.");

                    if (!manifestation.TryGetValue(key, out attachment))
                        throw new Exception("Key not found.");

                    grouping.OnNext(type, attachment.element, key, previousKey);

                    if (grouping.Count == 0)
                    {
                        groupingsByGroupingKey.Remove(groupingKey);
                        groupingKeysByKey.Remove(key);
                        groupings.Remove(grouping);
                    }

                    groupingsByGroupingKey.Remove(groupingKey);
                    break;
            }
        }

        public IDisposable Subscribe(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, IObservable<Key> refreshRequested)
        {
            return groupings.Subscribe(observer, refreshRequested);
        }

        public void Dispose()
        {
            subscription.Dispose();
        }

        public ILiveList<TElement> this[TKey key] { get { return groupingsByGroupingKey[key].Value; } }
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


        public static ILiveList<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, ILiveList<TInner>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            LiveList.Create((onNext, asdf) =>
            {
                return new LiveLookup<TKey, TOuter, TOuter>(outer, outerKeySelector, s => s, comparer ?? EqualityComparer<TKey>.Default);
            });


        }

        public static ILiveList<TResult> Join<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, il) => new { OuterItem = o, InnerList = il }, comparer)
                .SelectMany(p => p.InnerList, (p, i) => resultSelector(p.OuterItem, i));
        }

        public static ILiveLookoup<TKey, TElement> ToLookup<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            return new LiveLookup<TKey, TSource, TElement>(source, keySelector, elementSelector, comparer);
        }
    }
}
