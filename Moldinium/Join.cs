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

    public interface IGroupedLiveList<TKey, TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>
    {
        ILiveLookup<TKey, TElement> MakeLookup();
    }

    public interface ILiveLookup<TKey, TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>, IDisposable
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

    public class LiveLookup<TKey, TSource, TElement> : ILiveLookup<TKey, TElement>
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

    class GroupedLivedList<TKey, TSource, TElement> : IGroupedLiveList<TKey, TElement>
    {
        ILiveList<TSource> source;
        Func<TSource, TKey> keySelector;
        Func<TSource, TElement> elementSelector;
        IEqualityComparer<TKey> comparer;

        public ILiveLookup<TKey, TElement> MakeLookup()
        {
            return new LiveLookup<TKey, TSource, TElement>(source, keySelector, elementSelector = null, comparer = null);
        }

        public GroupedLivedList(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.source = source;
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        public IDisposable Subscribe(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, IObservable<Key> refreshRequested)
        {
            var lookup = MakeLookup();

            return lookup.Subscribe(observer, refreshRequested);
        }
    }

    public static partial class LiveList
    {
        static ILiveList<DoubleGroupByInfo<TKey, TOuter, TInner>> DoubleGroupBy<TOuter, TInner, TKey>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, IEqualityComparer<TKey> comparer = null)
        {
            return LiveList.Create<DoubleGroupByInfo<TKey, TOuter, TInner>>((onNext, downwardsRefreshRequest) =>
            {
                var outerLookup = outer.ToLookup(outerKeySelector, comparer);
                var innerLookup = inner.ToLookup(innerKeySelector, comparer);

                var groups = new Dictionary<TKey, DoubleGroupByInfo<TKey, TOuter, TInner>>();

                var outerSubscription = outerLookup.Subscribe((type, item, key, previousKey) =>
                {
                    DoubleGroupByInfo<TKey, TOuter, TInner> group;

                    var found = groups.TryGetValue(item.Key, out group);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!found)
                            {
                                group.key = KeyHelper.Create();
                                group.lkey = item.Key;
                                group.outerSubject = new LiveListSubject<TOuter>();
                                group.innerSubject = new LiveListSubject<TInner>();

                                onNext(ListEventType.Add, group, group.key, null);
                            }

                            group.outerSubscription = item.Subscribe(group.outerSubject);
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Key not found.");

                            InternalExtensions.DisposeSafely(ref group.outerSubscription);

                            if (group.outerSubscription == null && group.innerSubscription == null)
                                onNext(ListEventType.Remove, group, group.key, null);
                            break;
                    }
                }, null);

                var innerSubscription = innerLookup.Subscribe((type, item, key, previousKey) =>
                {
                    DoubleGroupByInfo<TKey, TOuter, TInner> group;

                    var found = groups.TryGetValue(item.Key, out group);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!found)
                            {
                                group.key = KeyHelper.Create();
                                group.lkey = item.Key;
                                group.outerSubject = new LiveListSubject<TOuter>();
                                group.innerSubject = new LiveListSubject<TInner>();

                                onNext(ListEventType.Add, group, group.key, null);
                            }

                            group.innerSubscription = item.Subscribe(group.innerSubject);
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Key not found.");

                            InternalExtensions.DisposeSafely(ref group.innerSubscription);

                            if (group.outerSubscription == null && group.innerSubscription == null)
                                onNext(ListEventType.Remove, group, group.key, null);
                            break;
                    }
                }, null);

                IDisposable releaseDisposable = Disposable.Create(() =>
                {
                    foreach (var group in groups.Values)
                    {
                        group.innerSubscription?.Dispose();
                        group.outerSubscription?.Dispose();
                    }
                });

                //FIXME: kill all subjects
                return new CompositeDisposable(
                    innerSubscription,
                    outerSubscription,
                    releaseDisposable);
            });
        }

        struct DoubleGroupByInfo<TKey, TOuter, TInner>
        {
            public Key key;
            public TKey lkey;
            public LiveListSubject<TOuter> outerSubject;
            public LiveListSubject<TInner> innerSubject;
            public IDisposable outerSubscription;
            public IDisposable innerSubscription;
        }
    }

    public static partial class LiveList
    {
        public static IGroupedLiveList<TKey, TSource> GroupBy<TSource, TKey>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            return new GroupedLivedList<TKey, TSource, TSource>(source, keySelector, s => s, comparer);
        }

        public static IGroupedLiveList<TKey, TElement> GroupBy<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            return new GroupedLivedList<TKey, TSource, TElement>(source, keySelector, elementSelector, comparer);
        }

        public static ILiveList<TResult> GroupBy<TSource, TKey, TResult>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, ILiveList<TSource>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, comparer).Select(g => resultSelector(g.Key, g));
        }

        public static ILiveList<TResult> GroupBy<TSource, TKey, TElement, TResult>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, ILiveList<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, elementSelector, comparer).Select(g => resultSelector(g.Key, g));
        }

        public static ILiveList<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner,
            Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, ILiveList<TInner>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            // IMPROVEME: The OrderBy could be replaced with something that brings any order.
            return outer.DoubleGroupBy(inner, outerKeySelector, innerKeySelector, comparer).OrderBy(g => g.key).Select(t => t.outerSubject.Select(o => resultSelector(o, t.innerSubject))).Flatten();
        }

        public static ILiveList<TResult> Join<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, il) => new { OuterItem = o, InnerList = il }, comparer)
                .SelectMany(p => p.InnerList, (p, i) => resultSelector(p.OuterItem, i));
        }

        public static ILiveLookup<TKey, TSource> ToLookup<TSource, TKey>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            return new LiveLookup<TKey, TSource, TSource>(source, keySelector, s => s, comparer);
        }

        public static ILiveLookup<TKey, TElement> ToLookup<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            return new LiveLookup<TKey, TSource, TElement>(source, keySelector, elementSelector, comparer);
        }
    }
}
