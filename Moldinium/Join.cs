using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
        ILiveList<TElement> this[TKey id] { get; }
    }

    // FIXME this must be internal
    public class LiveLookup<TKey, TSource, TElement> : AbstractLiveList<ILiveListGrouping<TKey, TElement>>, ILiveLookup<TKey, TElement>
    {
        Func<TSource, TKey> keySelector;
        Func<TSource, TElement> elementSelector;
        IEqualityComparer<TKey> comparer;

        ILiveListSubscription sourceSubscription;

        Dictionary<TKey, Grouping> groupingsByGroupingKey;
        Dictionary<Id, Grouping> groupings;
        Id lastId;
        Boolean haveElements;

        Dictionary<Id, Info> infos;

        struct Info
        {
            public TKey key;
            public Id? previousId;
            public Id? previousIdInSameGroup;
            public TElement element;
        }

        class Grouping : AbstractLiveList<TElement>, ILiveListGrouping<TKey, TElement>
        {
            public Id id;
            public Id? previousId;

            public LiveLookup<TKey, TSource, TElement> container;
            public TKey key;
            public TKey previousKey;
            public TKey nextKey;
            public Id lastId;

            public Grouping(LiveLookup<TKey, TSource, TElement>  container, TKey key, TKey previousKey, TKey nextKey)
            {
                this.container = container;
                this.key = key;
                this.nextKey = nextKey;
            }

            public TKey Key { get { return key; } }

            protected override void Refresh(DLiveListObserver<TElement> observer, Id id)
            {
                container.sourceSubscription.Refresh(id);
            }

            protected override void Bootstrap(DLiveListObserver<TElement> observer)
            {
                Info info = container.infos[lastId];
                Id id = lastId;

                do
                {
                    // FIXME wrong order!
                    info = container.infos[id];
                    observer(ListEventType.Add, info.element, lastId, info.previousIdInSameGroup);
                }
                while (info.previousIdInSameGroup.HasValue);
            }
        }

        public ILiveList<TElement> this[TKey id] => groupingsByGroupingKey[id];

        public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;
            this.comparer = comparer;
            this.sourceSubscription = source.Subscribe(Handle);
        }

        void Handle(ListEventType type, TSource item, Id id, Id? previousId)
        {
            Info info;

            var found = infos.TryGetValue(id, out info);

            Grouping grouping;

            switch (type)
            {
                case ListEventType.Add:
                    Debug.Assert(!found);

                    var key = info.key = keySelector(item);
                    var element = info.element = elementSelector(item);
                    info.previousId = previousId;

                    var previousIdCandidate = previousId;
                    Info info2 = info;
                    while (previousIdCandidate.HasValue && infos.TryGetValue(previousIdCandidate.Value, out info2) && !comparer.Equals(info2.key, key)) ;
                    var previousIdInSameGroup = info.previousIdInSameGroup = previousIdCandidate;

                    infos[id] = info;


                    if (!groupingsByGroupingKey.TryGetValue(key, out grouping))
                    {
                        grouping = new Grouping(this, key, /* find previous key by working forward from key of previousId */, /* find next key analogously */);
                        OnNext(ListEventType.Add, grouping, id, previousGrouping.id);
                    }

                    // FIXME: we really ought to translate the ids
                    grouping.OnNext(ListEventType.Add, element, id, previousIdInSameGroup);

                    break;
                case ListEventType.Remove:
                    Debug.Assert(found);

                    grouping = groupingsByGroupingKey[info.key];

                    // FIXME: maintain linked list in elements
                    grouping.OnNext(ListEventType.Add, info.element, id, info.previousIdInSameGroup);

                    if (grouping.Count == 0)
                    {
                        // FIXME: maintain linked list in groupings
                        groupingsByGroupingKey.Remove(info.key);
                        OnNext(ListEventType.Remove, grouping, grouping.id, grouping.previousId);
                    }

                    break;
            }
        }

        protected override void Bootstrap(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer)
        {
            if (!haveElements) return;

            var id = lastId;

            Grouping grouping;

            do
            {
                // FIXME wrong order!
                grouping = groupings[id];
                observer(ListEventType.Add, grouping, grouping.id, grouping.previousId);
            }
            while (grouping.previousId.HasValue);
        }

        protected override void Refresh(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, Id id)
        {
            var grouping = groupings[id];

            observer(ListEventType.Remove, grouping, grouping.id, grouping.previousId);
            observer(ListEventType.Add, grouping, grouping.id, grouping.previousId);
        }

        public void Dispose()
        {
            InternalExtensions.DisposeSafely(ref sourceSubscription);
        }
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

        public ILiveListSubscription Subscribe(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer)
        {
            var lookup = MakeLookup();

            return lookup.Subscribe(observer);
        }
    }

    public static partial class LiveList
    {
        static ILiveList<DoubleGroupByInfo<TKey, TOuter, TInner>> DoubleGroupBy<TOuter, TInner, TKey>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, IEqualityComparer<TKey> comparer = null)
        {
            return LiveList.Create<DoubleGroupByInfo<TKey, TOuter, TInner>>(onNext =>
            {
                var outerLookup = outer.ToLookup(outerKeySelector, comparer);
                var innerLookup = inner.ToLookup(innerKeySelector, comparer);

                var groups = new Dictionary<TKey, DoubleGroupByInfo<TKey, TOuter, TInner>>();

                Action<Id> handleRefreshRequest = Id =>
                {
                    // FIXME
                };

                var outerSubscription = outerLookup.Subscribe((type, item, id, previousId) =>
                {
                    DoubleGroupByInfo<TKey, TOuter, TInner> group;

                    var found = groups.TryGetValue(item.Key, out group);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!found)
                            {
                                group.id = IdHelper.Create();
                                group.lkey = item.Key;
                                group.outerSubject = new LiveListSubject<TOuter>();
                                group.innerSubject = new LiveListSubject<TInner>();

                                onNext(ListEventType.Add, group, group.id, null);
                            }

                            group.outerSubscription = item.Subscribe(group.outerSubject);
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Id not found.");

                            InternalExtensions.DisposeSafely(ref group.outerSubscription);

                            if (group.outerSubscription == null && group.innerSubscription == null)
                                onNext(ListEventType.Remove, group, group.id, null);
                            break;
                    }
                });

                var innerSubscription = innerLookup.Subscribe((type, item, id, previousId) =>
                {
                    DoubleGroupByInfo<TKey, TOuter, TInner> group;

                    var found = groups.TryGetValue(item.Key, out group);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!found)
                            {
                                group.id = IdHelper.Create();
                                group.lkey = item.Key;
                                group.outerSubject = new LiveListSubject<TOuter>();
                                group.innerSubject = new LiveListSubject<TInner>();

                                onNext(ListEventType.Add, group, group.id, null);
                            }

                            group.innerSubscription = item.Subscribe(group.innerSubject);
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Id not found.");

                            InternalExtensions.DisposeSafely(ref group.innerSubscription);

                            if (group.outerSubscription == null && group.innerSubscription == null)
                                onNext(ListEventType.Remove, group, group.id, null);
                            break;
                    }
                });

                IDisposable releaseDisposable = Disposable.Create(() =>
                {
                    foreach (var group in groups.Values)
                    {
                        group.innerSubscription?.Dispose();
                        group.outerSubscription?.Dispose();
                    }
                });

                //FIXME: kill all subjects
                return LiveListSubscription.Create(
                    handleRefreshRequest,
                    innerSubscription,
                    outerSubscription,
                    releaseDisposable);
            });
        }

        struct DoubleGroupByInfo<TKey, TOuter, TInner>
        {
            public Id id;
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
            return outer.DoubleGroupBy(inner, outerKeySelector, innerKeySelector, comparer).OrderByAny().Select(t => t.outerSubject.Select(o => resultSelector(o, t.innerSubject))).Flatten();
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
