using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    // This interface is the key to efficient paging, will have to build on this
    public interface IOrderedLiveList<out T> : ILiveList<T>
    {
        IDisposable Subscribe(DLiveListObserver<T> observer, IObservable<Key> refreshRequested, IComparer<T> comparer, Int32 skip, Int32 take);
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
            return LiveList.CreateOrdered<TSource>((onNext, downwardsRefreshRequests, lessSignificantComparer, skip, take) =>
            {
                return source.OrderBy().Subscribe(onNext, downwardsRefreshRequests,
                    new CombinatingComparer<TKey, TSource>(keySelector,
                        comparer ?? Comparer<TKey>.Default, lessSignificantComparer), skip, take);
            });
        }

        public static IOrderedLiveList<TSource> ThenBy<TSource, TKey>(
            this IOrderedLiveList<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            return LiveList.CreateOrdered<TSource>((onNext, downwardsRefreshRequests, lessSignificantComparer, skip, take) =>
            {
                return source.Subscribe(onNext, downwardsRefreshRequests,
                    new CombinatingComparer<TKey, TSource>(keySelector,
                        comparer ?? Comparer<TKey>.Default, lessSignificantComparer), skip, take);
            });
        }

        static IOrderedLiveList<TSource> OrderBy<TSource>(this ILiveList<TSource> source)
        {

        }

        static IOrderedLiveList<T> CreateOrdered<T>(Func<DLiveListObserver<T>, IObservable<Key>, IComparer<T>, Int32, Int32, IDisposable> subscribe)
        {
            
        }

        // FIXME: the descending versions
    }
}
