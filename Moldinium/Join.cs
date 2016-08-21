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


    public class LiveLookup<TKey, TSource, TElement> : ILiveLookoup<TKey, TElement>
    {
        Func<TSource, TKey> keySelector;
        Func<TSource, TElement> elementSelector;
        Dictionary<TKey, GroupingAndSubject> groupingsAndSubjectsByKey;
        Dictionary<Key, TKey> keysByKey;

        Subject<Key> refreshRequested;

        LiveList<ILiveListGrouping<TKey, TElement>> groupings;

        IDisposable subscription;

        struct GroupingAndSubject
        {
            public readonly ILiveListGrouping<TKey, TElement> Grouping;
            public readonly LiveListSubject<TElement> Subject;

            public GroupingAndSubject(ILiveListGrouping<TKey, TElement> grouping, LiveListSubject<TElement> tie)
            {
                Grouping = grouping;
                Subject = tie;
            }
        }

        public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
        {
            this.keySelector = keySelector;
            this.elementSelector = elementSelector;

            groupingsAndSubjectsByKey = new Dictionary<TKey, GroupingAndSubject>(comparer);
            keysByKey = new Dictionary<Key, TKey>();
            groupings = new LiveList<ILiveListGrouping<TKey, TElement>>();

            subscription = source.Subscribe(Handle, refreshRequested);
        }

        void Handle(ListEventType type, TSource item, Key key, Key? previousKey)
        {
            var lookupKey = keySelector(item); // FIXME: watchable support

            GroupingAndSubject groupingAndSubject;

            switch (type)
            {
                case ListEventType.Add:
                    if (!groupingsAndSubjectsByKey.TryGetValue(lookupKey, out groupingAndSubject))
                    {
                        var newKey = KeyHelper.Create();
                        var liveList = LiveList.Create<TElement>((onNext, downwardsRefreshRequests) => {
                            return null;
                        });
                        var subject = new LiveListSubject<TElement>();
                        var grouping = new LiveListGrouping<TKey, TElement>(lookupKey, subject);
                        groupingsAndSubjectsByKey[lookupKey] = groupingAndSubject = new GroupingAndSubject(grouping, subject);
                        keysByKey[newKey] = lookupKey;
                        groupings.Add(groupingAndSubject);
                    }

                    // FIXME: watchables
                    var element = elementSelector(item);

                    groupingAndSubject.Subject.OnNext(type, element, key, previousKey);

                    break;
                case ListEventType.Remove:
                    if (groupingsAndSubjectsByKey.TryGetValue(lookupKey, out groupingAndSubject))
                    {
                        // post
                    }

                    groupingsAndSubjectsByKey.Remove(lookupKey);
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

        public ILiveList<TElement> this[TKey key] { get { return groupingsAndSubjectsByKey[key].Value; } }
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
