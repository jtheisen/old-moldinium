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
    //public interface ILiveLookoup<TKey, TElement> : ILiveList<ILiveListGrouping<TKey, TElement>>
    //{
    //    ILiveList<TElement> this[TKey key] { get; }
    //}


    //public class TieLiveList<TSource> : ILiveList<TSource> // nonsense
    //{
    //    public TieLiveList(ILiveList<TSource> source)
    //    {
    //        this.source = source;
    //    }

    //    public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
    //    {
    //        source.Subscribe(Handle, refreshRequested2);

    //        var info = new ObserverInfo() { Observer = observer, RefreshRequested = refreshRequested };

    //        var subscription = new Disposable.Create(() => observers.Remove(info));

    //        info.Subscription = subscription;

    //        observers.Add(info);

    //        return subscription;
    //    }

    //    void Handle(ListEventType type, TSource item, Key key, Key? previousKey)
    //    {
    //        foreach (var info in observers)
    //        {
    //            try
    //            {
    //                info.Observer(type, item, key, previousKey);
    //            }
    //            catch (Exception)
    //            {
    //                throw; // FIXME
    //            }
    //        }
    //    }

    //    ILiveList<TSource> source;

    //    Subject<Key> refreshRequested2 = new Subject<Key>();

    //    class ObserverInfo
    //    {
    //        public DLiveListObserver<TSource> Observer;
    //        public IDisposable Subscription;
    //        public IObservable<Key> RefreshRequested;
    //    }

    //    List<ObserverInfo> observers = new List<ObserverInfo>();
    //}

    //public class LiveLookup<TKey, TSource, TElement> : TieLiveList<ILiveListGrouping<TKey, TElement>>, ILiveLookoup<TKey, TElement>
    //{
    //    Func<TSource, TKey> keySelector;
    //    Func<TSource, TElement> elementSelector;
    //    Dictionary<TKey, KeyValuePair<ILiveListGrouping<TKey, TElement>, LiveList<TElement>>> dictionary;

    //    public LiveLookup(ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
    //    {
    //        this.keySelector = keySelector;
    //        this.elementSelector = elementSelector;

    //        this.dictionary = new Dictionary<TKey, KeyValuePair<ILiveListGrouping<TKey, TElement>, LiveList<TElement>>>(comparer);

    //        source.Subscribe(Handle, refreshRequested);
    //    }

    //    void Handle(ListEventType type, TSource item, Key key, Key? previousKey)
    //    {
    //        var lookupKey = keySelector(item); // FIXME: watchable support

    //        KeyValuePair<ILiveListGrouping<TKey, TElement>, LiveList<TElement>> value;

    //        switch (type)
    //        {
    //            case ListEventType.Add:
    //                if (!dictionary.TryGetValue(lookupKey, out value))
    //                {
    //                    var liveList = new LiveList<TElement>();
    //                    var grouping = new LiveListGrouping<TKey, TElement>(lookupKey, liveList);
    //                    dictionary[lookupKey] = value = new KeyValuePair<ILiveListGrouping<TKey, TElement>, LiveList<TElement>>(grouping, liveList);
    //                }

    //                // post

    //                break;
    //            case ListEventType.Remove:
    //                if (dictionary.TryGetValue(lookupKey, out value))
    //                {
    //                    // post
    //                }

    //                dictionary.Remove(lookupKey);
    //                break;
    //        }
    //    }

    //    public IDisposable Subscribe(DLiveListObserver<ILiveListGrouping<TKey, TElement>> observer, IObservable<Key> refreshRequested)
    //    {
            
    //    }

    //    public ILiveList<TElement> this[TKey key] { get { return dictionary[key].Value; } }

    //    Subject<Key> refreshRequested;
    //}

    //public static partial class LiveList
    //{
    //    public static ILiveList<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, ILiveList<TInner>, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
    //    {
    //        LiveList.Create((onNext, asdf) =>
    //        {
    //            return new LiveLookup<TKey, TOuter, TOuter>(outer, outerKeySelector, s => s, comparer ?? EqualityComparer<TKey>.Default);
    //        });

            
    //    }

    //    public static ILiveList<TResult> Join<TOuter, TInner, TKey, TResult>(this ILiveList<TOuter> outer, ILiveList<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer = null)
    //    {
    //        return outer
    //            .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, il) => new { OuterItem = o, InnerList = il }, comparer)
    //            .SelectMany(p => p.InnerList, (p, i) => resultSelector(p.OuterItem, i));
    //    }

    //    public static ILiveLookoup<TKey, TElement> ToLookup<TSource, TKey, TElement>(this ILiveList<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer)
    //    {
    //        return new LiveLookup<TKey, TSource, TElement>(source, keySelector, elementSelector, comparer);
    //    }
    //}
}
