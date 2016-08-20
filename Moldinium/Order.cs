using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    public interface ILiveIndex<out TSource> : ILiveList<TSource>
    {
    }

    public static class ThingWithKey
    {
        public static ThingWithKey<TSource> Create<TSource>(TSource thing, Key key)
        {
            return new ThingWithKey<TSource>(thing, key);
        }
    }

    public struct ThingWithKey<TSource>
    {
        public readonly TSource Thing;
        public readonly Key Key;

        public ThingWithKey(TSource thing, Key key)
        {
            this.Thing = thing;
            this.Key = key;
        }
    }

    public interface IOrderedLiveList<out TSource> : ILiveList<TSource>
    {
        IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested, IComparer<TSource> comparer);

        ILiveIndex<TSource> MakeIndex(IComparer<TSource> comparer);
    }

    public class FirstConcreteOrderedLiveList<TSource> : IOrderedLiveList<TSource>
    {
        ILiveList<TSource> source;

        public FirstConcreteOrderedLiveList(ILiveList<TSource> source)
        {
            this.source = source;
        }

        public ILiveIndex<TSource> MakeIndex(IComparer<TSource> comparer)
        {
            return new LiveIndex<TSource>(source, comparer);
        }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            throw new NotImplementedException(); // will never be called
        }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested, IComparer<TSource> comparer)
        {
            var liveIndex = new LiveIndex<TSource>(source, comparer);

            return liveIndex.Subscribe(observer, refreshRequested);
        }
    }

    public class ConcreteOrderedLiveList<TSource> : IOrderedLiveList<TSource>
    {
        Func<DLiveListObserver<TSource>, IObservable<Key>, IComparer<TSource>, IDisposable> subscribe;

        public ConcreteOrderedLiveList(Func<DLiveListObserver<TSource>, IObservable<Key>, IComparer<TSource>, IDisposable> subscribe)
        {
            this.subscribe = subscribe;
        }

        public ILiveIndex<TSource> MakeIndex()
        {
            return new LiveIndex(subscribe, comparer)
        }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            var liveIndex = new LiveIndex(subscribe, comparer);
        }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested, IComparer<TSource> comparer)
        {
            return subscribe(observer, refreshRequested, comparer);
        }
    }

    public class LiveIndex<TSource> : ILiveList<TSource>, IDisposable
    {
        IComparer<ThingWithKey<TSource>> comparer;

        internal LiveIndex(ILiveList<TSource> source, IComparer<ThingWithKey<TSource>> comparer)
        {
            this.comparer = comparer;
            sourceSubscription = source.Subscribe(Handle, null);
        }

        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            // FIXME: A refresh request will now refresh all subscribers, as they share the live list.
            return list.Select(twk => twk.Thing).Subscribe(observer, refreshRequested);
        }

        void Handle(ListEventType type, TSource item, Key key, Key? previousKey)
        {
            var twk = ThingWithKey.Create(item, key);

            var index = list.BinarySearch(twk, comparer);

            switch (type)
            {
                case ListEventType.Add:
                    if (index >= 0) throw new Exception("Item was already inserted.");
                    list.Insert(~index, twk);
                    break;
                case ListEventType.Remove:
                    if (index < 0) throw new Exception("Item was had not been inserted.");
                    list.RemoveAt(index);
                    break;
            }
        }

        public void Dispose()
        {
            sourceSubscription.Dispose();
        }

        IDisposable sourceSubscription;

        LiveList<ThingWithKey<TSource>> list = new LiveList<ThingWithKey<TSource>>();
    }

    class CombinatingComparer<TKey, TSource> : IComparer<TSource>
    {
        Func<TSource, TKey> selector1;
        IComparer<TKey> comparer1;
        IComparer<TSource> comparer2;

        public CombinatingComparer(Func<TSource, TKey> selector1, IComparer<TKey> comparer1, IComparer<TSource> comparer2)
        {
            this.selector1 = selector1;
            this.comparer1 = comparer1;
            this.comparer2 = comparer2;
        }

        public Int32 Compare(TSource x, TSource y)
        {
            var h = comparer1.Compare(selector1(x), selector1(y));

            if (h != 0) return h;

            return comparer2.Compare(x, y);
        }
    }

    public static partial class LiveList
    {
        public static IOrderedLiveList<TSource> OrderBy<TSource, TKey>(
            this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            return LiveList.CreateOrdered<TSource>((onNext, downwardsRefreshRequests, lessSignificantComparer) =>
            {
                return source.OrderBy().Subscribe(onNext, downwardsRefreshRequests,
                    new CombinatingComparer<TKey, TSource>(keySelector,
                        comparer ?? Comparer<TKey>.Default, lessSignificantComparer));
            });
        }

        public static IOrderedLiveList<TSource> ThenBy<TSource, TKey>(
            this IOrderedLiveList<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            return LiveList.CreateOrdered<TSource>((onNext, downwardsRefreshRequests, lessSignificantComparer) =>
            {
                return source.Subscribe(onNext, downwardsRefreshRequests,
                    new CombinatingComparer<TKey, TSource>(keySelector,
                        comparer ?? Comparer<TKey>.Default, lessSignificantComparer));
            });
        }

        static IOrderedLiveList<TSource> OrderBy<TSource>(this ILiveList<TSource> source)
        {
            throw new NotImplementedException(); // FIXME
        }

        static IOrderedLiveList<T> CreateOrdered<T>(Func<DLiveListObserver<T>, IObservable<Key>, IComparer<T>, IDisposable> subscribe)
        {
            throw new NotImplementedException(); // FIXME
        }

        // FIXME: the descending versions
    }
}
