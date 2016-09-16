﻿using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace IronStone.Moldinium
{
    public class LiveLookup<TKey, TElement, TSource> : AbstractLiveList<ILiveListGrouping<TKey, TElement>>, ILiveLookup<TKey, TElement>
    {
        Func<TSource, TKey> keySelector;
        Func<TSource, TElement> elementSelector;
        IEqualityComparer<TKey> comparer;

        Dictionary<Id, Node> nodes;

        Dictionary<Id, Grouping> groupings;

        Grouping firstGrouping;
        Grouping lastGrouping;

        Dictionary<TKey, Grouping> keyToGroupings;

        ILiveListSubscription sourceSubscription;

        class Grouping : AbstractLiveList<TElement>, ILiveListGrouping<TKey, TElement>
        {
            LiveLookup<TKey, TElement, TSource> parent;

            public TKey key;

            public Id id;

            public Grouping previousGrouping;
            public Grouping nextGrouping;

            public Grouping(LiveLookup<TKey, TElement, TSource> parent, TKey key)
            {
                this.parent = parent;
                this.key = key;
            }

            public TKey Key => key;

            protected override void Bootstrap(DLiveListObserver<TElement> observer)
            {
                throw new NotImplementedException();
            }

            protected override void Refresh(DLiveListObserver<TElement> observer, Id id)
            {
                parent.sourceSubscription.Refresh(id);
            }
        }

        struct Node
        {
            public Grouping grouping;

            public TElement element;

            public Id? previousId;
            public Id? nextId;

            public Id? previousIdInGrouping;
            public Id? nextIdInGrouping;
        }

        public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;
            this.comparer = comparer;

            keyToGroupings = new Dictionary<TKey, Grouping>(comparer);

            sourceSubscription = source.Subscribe(Handle);
        }

        void Handle(ListEventType type, TSource item, Id id, Id? previousId, Id? nextId)
        {
            Node node;

            var key = keySelector(item);
            var grouping = node.grouping = GetGrouping(key);

            switch (type)
            {
                case ListEventType.Add:
                    var nodeOfPrevious = previousId.ApplyTo(nodes);
                    var nodeOfNext = nextId.ApplyTo(nodes);

                    var groupingOfPrevious = nodeOfPrevious?.grouping;
                    var groupingOfNext = nodeOfNext?.grouping;

                    var element = node.element = elementSelector(item);

                    node.previousId = previousId;
                    node.nextId = nextId;

                    var previousInSameGroupCandidateId = previousId;
                    Node previousInSameGroupNodeCandidate;
                    while (previousInSameGroupCandidateId.HasValue)
                    {
                        previousInSameGroupNodeCandidate = nodes[previousInSameGroupCandidateId.Value];
                        if (previousInSameGroupNodeCandidate.grouping == grouping) break;
                        previousInSameGroupCandidateId = previousInSameGroupNodeCandidate.previousId;
                    }
                    node.previousIdInGrouping = previousInSameGroupCandidateId;

                    var nextInSameGroupCandidateId = nextId;
                    Node nextInSameGroupNodeCandidate;
                    while (nextInSameGroupCandidateId.HasValue)
                    {
                        nextInSameGroupNodeCandidate = nodes[nextInSameGroupCandidateId.Value];
                        if (nextInSameGroupNodeCandidate.grouping == grouping) break;
                        nextInSameGroupCandidateId = nextInSameGroupNodeCandidate.nextId;
                    }
                    node.nextIdInGrouping = nextInSameGroupCandidateId;

                    nodes[id] = node;

                    if (grouping.Count == 0)
                    {
                        var groupingId = IdHelper.Create();

                        grouping.id = groupingId;
                        var previousGrouping = grouping.previousGrouping = groupingOfPrevious;
                        var nextGrouping = grouping.nextGrouping = groupingOfPrevious.nextGrouping;
                        previousGrouping.nextGrouping = grouping;
                        nextGrouping.previousGrouping = grouping;
 
                        OnNext(ListEventType.Add, grouping, groupingId, previousGrouping.id, nextGrouping.id);
                    }

                    grouping.OnNext(ListEventType.Add, element, id, previousInSameGroupCandidateId, nextInSameGroupCandidateId);

                    break;
                case ListEventType.Remove:

                    node = nodes[id];

                    grouping.OnNext(ListEventType.Remove, node.element, id, node.previousIdInGrouping, node.nextIdInGrouping);

                    nodes.Remove(id);

                    if (grouping.Count == 0)
                    {
                        OnNext(ListEventType.Remove, grouping, grouping.id, grouping.previousGrouping.id, grouping.nextGrouping.id);

                        grouping.id = default(Id);
                        grouping.previousGrouping = grouping.nextGrouping = null;
                    }

                    break;
            }
        }

        Grouping GetGrouping(TKey key)
        {
            Grouping grouping;

            if (!keyToGroupings.TryGetValue(key, out grouping))
                keyToGroupings[key] = grouping = new Grouping(this, key);

            return grouping;
        }

        public ILiveList<TElement> this[TKey key] => GetGrouping(key);

        public Boolean Contains(TKey key)
        {
            Grouping grouping;

            return keyToGroupings.TryGetValue(key, out grouping) && grouping.Count > 0;
        }

        public void Dispose()
        {
            InternalExtensions.DisposeProperly(ref sourceSubscription);
        }

        protected override void Refresh(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, Id id)
        {
            var grouping = groupings[id];
            observer(ListEventType.Remove, grouping, id, grouping.previousGrouping?.id, grouping.nextGrouping?.id);
            observer(ListEventType.Add, grouping, id, grouping.previousGrouping?.id, grouping.nextGrouping?.id);
        }

        protected override void Bootstrap(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer)
        {
            for (var grouping = firstGrouping; grouping != null; grouping = grouping.nextGrouping)
                observer(ListEventType.Add, grouping, grouping.id, grouping.previousGrouping?.id, grouping.nextGrouping?.id);
        }
    }

    public interface ILiveListGrouping<TKey, out TSource> : ILiveList<TSource>
    {
        TKey Key { get; }
    }

    public interface IGroupedLiveList<TKey, out TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>
    {
        ILiveLookup<TKey, TElement> MakeLookup();
    }

    public interface ILiveLookup<TKey, out TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>, IDisposable
    {
        ILiveList<TElement> this[TKey key] { get; }

        Boolean Contains(TKey key);

        Int32 Count { get; }
    }

    public static partial class LiveList
    {
        static ILiveList<DoubleGroupByInfo<TKey, TOuter, TInner>> DoubleGroupBy<TOuter, TInner, TKey>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, IEqualityComparer<TKey> comparer)
        {
            return LiveList.Create<DoubleGroupByInfo<TKey, TOuter, TInner>>(onNext =>
            {
                var outerLookup = outer.ToLookup(outerKeySelector, comparer);
                var innerLookup = inner.ToLookup(innerKeySelector, comparer);

                var groups = new Dictionary<TKey, DoubleGroupByInfo<TKey, TOuter, TInner>>(comparer);

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

                            InternalExtensions.DisposeProperly(ref group.outerSubscription);

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

                            InternalExtensions.DisposeProperly(ref group.innerSubscription);

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
            throw new NotImplementedException();
            //return new GroupedLivedList<TKey, TSource, TSource>(source, keySelector, s => s, comparer);
        }

        public static IGroupedLiveList<TKey, TElement> GroupBy<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
            //return new GroupedLivedList<TKey, TSource, TElement>(source, keySelector, elementSelector, comparer);
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
            return outer.DoubleGroupBy(inner, outerKeySelector, innerKeySelector, comparer).OrderByInsertion().Select(t => t.outerSubject.Select(o => resultSelector(o, t.innerSubject))).Flatten();
        }

        public static ILiveList<TResult> Join<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, il) => new { OuterItem = o, InnerList = il }, comparer)
                .SelectMany(p => p.InnerList, (p, i) => resultSelector(p.OuterItem, i));
        }

        public static ILiveLookup<TKey, TSource> ToLookup<TSource, TKey>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
            //return new LiveLookup<TKey, TSource, TSource>(source, keySelector, s => s, comparer);
        }

        public static ILiveLookup<TKey, TElement> ToLookup<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            throw new NotImplementedException();
            //return new LiveLookup<TKey, TSource, TElement>(source, keySelector, elementSelector, comparer);
        }

        public static ILiveList<TSource> Where<TSource>(this ILiveList<TSource> source, Predicate<TSource> predicate)
        {
            throw new NotImplementedException();
        }
    }
}
