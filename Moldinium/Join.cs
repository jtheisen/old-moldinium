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

        Dictionary<TKey, WeakReference<Grouping>> groupingsByGroupingKey;
        Dictionary<Id, Grouping> groupings;

        Grouping first, last;

        Dictionary<Id, Info> infos;

        struct Info
        {
            public TKey key;
            public Id? previoudId;
            public Id? nextId;
            public Id? previousIdInSameGroup;
            public Id? nextIdInSameGroup;
            public TElement element;
        }

        class Grouping : AbstractLiveList<TElement>, ILiveListGrouping<TKey, TElement>
        {
            public LiveLookup<TKey, TSource, TElement> container;
            public TKey key;

            public Id? id;
            public Grouping previous;
            public Grouping next;
            public Id? lastId;
            public Id? firstId;

            public Grouping(LiveLookup<TKey, TSource, TElement>  container, TKey key)
            {
                this.container = container;
                this.key = key;
            }

            public TKey Key { get { return key; } }

            protected override void Refresh(DLiveListObserver<TElement> observer, Id id)
            {
                container.sourceSubscription.Refresh(id);
            }

            protected override void Bootstrap(DLiveListObserver<TElement> observer)
            {
                if (!firstId.HasValue) return;

                Id? id = firstId;

                do
                {
                    Info info = container.infos[id.Value];
                    observer(ListEventType.Add, info.element, id.Value, info.previousIdInSameGroup, info.nextIdInSameGroup);
                    id = info.nextIdInSameGroup;
                }
                while (id.HasValue);
            }
        }

        public ILiveList<TElement> this[TKey key] {
            get {
                Grouping grouping;
                if (!groupingsByGroupingKey[key].TryGetTarget(out grouping))
                {
                    grouping = new Grouping(this, key);
                    groupingsByGroupingKey[key] = new WeakReference<Grouping>(grouping);
                }
                return grouping;
            }
        }

        public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;
            this.comparer = comparer;
            this.sourceSubscription = source.Subscribe(Handle);
        }


        // FIXME: What about when the order of the groupings ought to change?
        // FIXME: we really ought to translate the ids

        void Handle(ListEventType type, TSource item, Id id, Id? previousId, Id? nextId)
        {
            Info info, info2;

            var found = infos.TryGetValue(id, out info);

            WeakReference<Grouping> groupingReference;
            Grouping grouping;

            switch (type)
            {
                case ListEventType.Add:
                    Debug.Assert(!found);

                    var key = info.key = keySelector(item);
                    var element = info.element = elementSelector(item);

                    info.previoudId = previousId;
                    info.nextId = nextId;

                    info2 = info;

                    var previousIdCandidate = previousId;
                    while (previousIdCandidate.HasValue && infos.TryGetValue(previousIdCandidate.Value, out info2) && !comparer.Equals(info2.key, key)) previousIdCandidate = info2.nextId;
                    var previousIdInSameGroup = info.previousIdInSameGroup = previousIdCandidate;

                    var nextIdCandidate = nextId;
                    while (nextIdCandidate.HasValue && infos.TryGetValue(nextIdCandidate.Value, out info2) && !comparer.Equals(info2.key, key)) nextIdCandidate = info2.previoudId;
                    var nextIdInSameGroup = info.nextIdInSameGroup = nextIdCandidate;

                    infos[id] = info;


                    if (!groupingsByGroupingKey.TryGetValue(key, out groupingReference) || !groupingReference.TryGetTarget(out grouping))
                    {
                        grouping = new Grouping(this, key);
                        groupingReference = new WeakReference<Grouping>(grouping);
                        groupingsByGroupingKey[key] = groupingReference;
                    }

                    if (!grouping.id.HasValue)
                    {
                        // We have either just created the grouping or it's a grouping that was already in
                        // the list at some point in the past and is now coming back.

                        grouping.id = IdHelper.Create();
                        grouping.firstId = ;
                        grouping.lastId = ;
                        grouping.next =;
                        grouping.previous =;

                        OnNext(ListEventType.Add, grouping, grouping.id.Value, grouping.previous?.id, grouping.next?.id);
                    }

                    grouping.OnNext(ListEventType.Add, element, id, previousIdInSameGroup, nextIdInSameGroup);

                    break;
                case ListEventType.Remove:
                    Debug.Assert(found);

                    groupingReference = groupingsByGroupingKey[info.key];

                    if (!groupingReference.TryGetTarget(out grouping))
                        throw new Exception("Could not find group in remove operation.");

                    if (info.nextIdInSameGroup.HasValue)
                    {
                        // IMPROVEME: Use C#7 ref returns or use reference infos
                        info2 = infos[info.nextIdInSameGroup.Value];
                        info2.previousIdInSameGroup = info.previousIdInSameGroup;
                        infos[info.nextIdInSameGroup.Value] = info2;
                    }
                    else
                    {
                        grouping.lastId = info.previousIdInSameGroup;
                    }

                    if (info.previousIdInSameGroup.HasValue)
                    {
                        // IMPROVEME: Use C#7 ref returns or use reference infos
                        info2 = infos[info.previousIdInSameGroup.Value];
                        info2.nextIdInSameGroup = info.nextIdInSameGroup;
                        infos[info.nextIdInSameGroup.Value] = info2;
                    }
                    else
                    {
                        grouping.firstId = info.nextIdInSameGroup;
                    }

                    infos.Remove(id);

                    grouping.OnNext(ListEventType.Remove, info.element, id, info.previousIdInSameGroup, info.nextIdInSameGroup);

                    if (grouping.Count == 0)
                    {
                        if (grouping.previous != null)
                            grouping.previous.next = grouping.next;
                        else
                            first = grouping.next;

                        if (grouping.next != null)
                            grouping.next.previous = grouping.previous;
                        else
                            last = grouping.previous;

                        groupings.Remove(grouping.id.Value);
                        groupingsByGroupingKey.Remove(info.key);

                        OnNext(ListEventType.Remove, grouping, grouping.id.Value, grouping.previous.id, grouping.next.id);

                        grouping.id = null;
                    }

                    break;
            }
        }

        protected override void Bootstrap(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer)
        {
            var grouping = first;

            while (grouping != null)
            {
                observer(ListEventType.Add, grouping, grouping.id.Value, grouping.previous.id, grouping.next.id);
                grouping = grouping.next;
            }
        }

        protected override void Refresh(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, Id id)
        {
            var grouping = groupings[id];

            observer(ListEventType.Remove, grouping, grouping.id.Value, grouping.previous?.id, grouping.next?.id);
            observer(ListEventType.Add, grouping, grouping.id.Value, grouping.previous?.id, grouping.next?.id);
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

                var outerSubscription = outerLookup.Subscribe((type, item, id, previousId, nextId) =>
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

                                onNext(ListEventType.Add, group, group.id, null, null);
                            }

                            group.outerSubscription = item.Subscribe(group.outerSubject);
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Id not found.");

                            InternalExtensions.DisposeSafely(ref group.outerSubscription);

                            if (group.outerSubscription == null && group.innerSubscription == null)
                                onNext(ListEventType.Remove, group, group.id, null, null);
                            break;
                    }
                });

                var innerSubscription = innerLookup.Subscribe((type, item, id, previousId, nextId) =>
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

                                onNext(ListEventType.Add, group, group.id, null, null);
                            }

                            group.innerSubscription = item.Subscribe(group.innerSubject);
                            break;
                        case ListEventType.Remove:
                            if (!found) throw new Exception("Id not found.");

                            InternalExtensions.DisposeSafely(ref group.innerSubscription);

                            if (group.outerSubscription == null && group.innerSubscription == null)
                                onNext(ListEventType.Remove, group, group.id, null, null);
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
