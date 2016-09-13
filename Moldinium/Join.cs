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
