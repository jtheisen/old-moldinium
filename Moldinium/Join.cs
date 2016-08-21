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

        IDisposable subscription;

        public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;

            groupingsByGroupingKey = new Dictionary<TKey, LiveListGrouping<TKey, TElement>>(comparer);
            groupingKeysByKey = new Dictionary<Key, TKey>();
            groupings = new LiveList<ILiveListGrouping<TKey, TElement>>();

            subscription = source.Subscribe(Handle, refreshRequested);
        }

        void Handle(ListEventType type, TSource item, Key key, Key? previousKey)
        {
            LiveListGrouping<TKey, TElement> grouping;

            switch (type)
            {
                case ListEventType.Add:
                    var groupingKey = keySelector(item); // FIXME: watchable support

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

                    grouping.OnNext(type, element, key, previousKey);

                    break;
                case ListEventType.Remove:
                    var groupingKeyToRemove = groupingKeysByKey[key];

                    if (!groupingsByGroupingKey.TryGetValue(groupingKeyToRemove, out grouping))
                        throw new Exception("Grouping key not found.");

                    grouping.OnNext(type, item, key, previousKey);

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
