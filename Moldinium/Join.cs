﻿using System;
using System.Collections;
using System.Collections.Generic;
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
        TKey Id { get; }
    }

    public interface IGroupedLiveList<TKey, TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>
    {
        ILiveLookup<TKey, TElement> MakeLookup();
    }

    public interface ILiveLookup<TKey, TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>, IDisposable
    {
        ILiveList<TElement> this[TKey id] { get; }
    }

    public class LiveListGrouping<TKey, TSource> : LiveListSubject<TSource>, ILiveListGrouping<TKey, TSource>
    {
        TKey id;

        public LiveListGrouping(TKey id)
        {
            this.id = id;
        }

        public TKey Id { get { return id; } }
    }

    struct LiveLookupInfo<TKey, TSource, TElement>
    {
        public TKey key;
        public Id previousId;
        public Id previousIdInSameGroup;
        public TElement element;
    }

    //public class LiveLookup<TKey, TSource, TElement> : ILiveLookup<TKey, TElement>
    //{
    //    Func<TSource, TKey> keySelector;
    //    Func<TSource, TElement> elementSelector;
    //    Dictionary<TKey, LiveListGrouping<TKey, TElement>> groupingsByGroupingKey;

    //    Dictionary<Id, LiveLookupInfo<TKey, TSource, TElement>> infos;

    //    public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
    //    {
    //        this.keySelector = keySelector;
    //        this.elementSelector = elementSelector;
    //    }

    //    void Handle(ListEventType type, TSource item, Id id, Id? previousId)
    //    {
    //        LiveLookupInfo<TKey, TSource, TElement> info;

    //        var found = infos.TryGetValue(id, out info);

    //        switch (type)
    //        {
    //            case ListEventType.Add:
    //                var lkey = keySelector(item);
    //                info.element = elementSelector(item);
    //                break;
    //            case ListEventType.Remove:
    //                break;
    //        }
    //    }
    //}

    // FIXME:
    /*
     * We should have two lookups in the long run:
     * - one for smaller lists that preserves order and
     * - one for larger lists based on OrderedLiveLists that takes the order from that order
     * 
     * Also note that the problem of a lookup preserving order is the same problem as a where preserving order.
     */
    public class LiveLookup<TKey, TSource, TElement> : ILiveLookup<TKey, TElement>
    {
        Func<TSource, TKey> keySelector;
        Func<TSource, TElement> elementSelector;
        Dictionary<TKey, LiveListGrouping<TKey, TElement>> groupingsByGroupingKey;
        Dictionary<Id, TKey> groupingKeysByKey;

        IObservable<Id> refreshRequested;

        LiveList<ILiveListGrouping<TKey, TElement>> groupings;

        Dictionary<Id, Attachment> manifestation;

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
            groupingKeysByKey = new Dictionary<Id, TKey>();
            groupings = new LiveList<ILiveListGrouping<TKey, TElement>>();

            manifestation = new Dictionary<Id, Attachment>();

            subscription = source.Subscribe(Handle, refreshRequested);
        }

        void Handle(ListEventType type, TSource item, Id id, Id? previousId)
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
                        var newKey = IdHelper.Create();
                        groupingsByGroupingKey[groupingKey] = grouping = new LiveListGrouping<TKey, TElement>(groupingKey);
                        groupingKeysByKey[newKey] = groupingKey;
                        groupings.Add(grouping);
                    }

                    // FIXME: watchables
                    var element = elementSelector(item);

                    attachment.element = element;

                    manifestation[id] = attachment;

                    // FIXME: previousId is bs
                    grouping.OnNext(type, element, id, previousId);

                    break;
                case ListEventType.Remove:
                    groupingKey = groupingKeysByKey[id];

                    if (!groupingsByGroupingKey.TryGetValue(groupingKey, out grouping))
                        throw new Exception("Grouping id not found.");

                    if (!manifestation.TryGetValue(id, out attachment))
                        throw new Exception("Id not found.");

                    grouping.OnNext(type, attachment.element, id, previousId);

                    if (grouping.Count == 0)
                    {
                        groupingsByGroupingKey.Remove(groupingKey);
                        groupingKeysByKey.Remove(id);
                        // FIXME: Isn't this linear time?
                        groupings.Remove(grouping);
                    }

                    groupingsByGroupingKey.Remove(groupingKey);
                    break;
            }
        }

        public IDisposable Subscribe(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, IObservable<Id> refreshRequested)
        {
            return groupings.Subscribe(observer, refreshRequested);
        }

        public void Dispose()
        {
            subscription.Dispose();
        }

        public ILiveList<TElement> this[TKey id] { get { return groupingsByGroupingKey[id]; } }
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

        public IDisposable Subscribe(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, IObservable<Id> refreshRequested)
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

                var outerSubscription = outerLookup.Subscribe((type, item, id, previousId) =>
                {
                    DoubleGroupByInfo<TKey, TOuter, TInner> group;

                    var found = groups.TryGetValue(item.Id, out group);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!found)
                            {
                                group.id = IdHelper.Create();
                                group.lkey = item.Id;
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
                }, null);

                var innerSubscription = innerLookup.Subscribe((type, item, id, previousId) =>
                {
                    DoubleGroupByInfo<TKey, TOuter, TInner> group;

                    var found = groups.TryGetValue(item.Id, out group);

                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!found)
                            {
                                group.id = IdHelper.Create();
                                group.lkey = item.Id;
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
            return source.GroupBy(keySelector, comparer).Select(g => resultSelector(g.Id, g));
        }

        public static ILiveList<TResult> GroupBy<TSource, TKey, TElement, TResult>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<TKey, ILiveList<TElement>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
        {
            return source.GroupBy(keySelector, elementSelector, comparer).Select(g => resultSelector(g.Id, g));
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
