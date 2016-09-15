using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    public interface ILiveIndex<out TSource> : ILiveList<TSource>, IDisposable
    {
    }

    public static class ThingWithKey
    {
        public static ThingWithKey<TSource> Create<TSource>(TSource thing, Id id)
        {
            return new ThingWithKey<TSource>(thing, id);
        }
    }

    [DebuggerDisplay("{Id}: {Thing}")]
    public struct ThingWithKey<TSource>
    {
        public readonly TSource Thing;
        public readonly Id Id;
        public SerialDisposable Subscriptions;

        public ThingWithKey(TSource thing, Id id)
        {
            this.Thing = thing;
            this.Id = id;
            this.Subscriptions = null;
        }
    }

    public abstract class AbstractComparerEvaluator<TSource> : IComparer<ThingWithKey<TSource>>
    {
        public abstract void SetLhs(TSource value);
        public abstract void SetRhs(TSource value);

        public abstract Int32 Compare();

        public int Compare(ThingWithKey<TSource> lhs, ThingWithKey<TSource> rhs)
        {
            SetLhs(lhs.Thing);
            SetRhs(rhs.Thing);
            var result = Compare();
            if (result != 0) return result;
            return Comparer<Id>.Default.Compare(lhs.Id, rhs.Id);
        }

        public static readonly TrivialComparerEvaluator<TSource> Trivial = new TrivialComparerEvaluator<TSource>();
    }

    public class TrivialComparerEvaluator<TSource> : AbstractComparerEvaluator<TSource>
    {
        public override int Compare() => 0;

        public override void SetLhs(TSource value) { }

        public override void SetRhs(TSource value) { }
    }

    public class NestedComparerEvaluator<TSource, TKey> : AbstractComparerEvaluator<TSource>
    {
        AbstractComparerEvaluator<TSource> nested;

        Func<TSource, TKey> keySelector;
        IComparer<TKey> comparer;
        Int32 direction;

        TSource lhsv, rhsv;
        TKey lhs, rhs;

        public NestedComparerEvaluator(Func<TSource, TKey> keySelector, IComparer<TKey> comparer, Int32 direction, AbstractComparerEvaluator<TSource> nested = null)
        {
            this.nested = nested;
            this.keySelector = keySelector;
            this.comparer = comparer;
            this.direction = direction;
        }

        public override Int32 Compare()
        {
            var result = nested?.Compare() ?? 0;
            if (result != 0) return result;
            result = direction * comparer.Compare(lhs, rhs);
            return result;
        }

        public override void SetLhs(TSource value)
        {
            nested.SetLhs(value);
            lhsv = value;
            lhs = keySelector.Invoke(value);
        }

        public override void SetRhs(TSource value)
        {
            nested.SetRhs(value);
            rhsv = value;
            rhs = keySelector.Invoke(value);
        }
    }


    public interface IOrderedLiveList<TSource> : ILiveList<TSource>
    {
        ILiveIndex<TSource> MakeIndex(AbstractComparerEvaluator<TSource> evaluator);
    }

    internal abstract class AbstractOrderedLiveList<TSource> : IOrderedLiveList<TSource>
    {
        public abstract ILiveIndex<TSource> MakeIndex(AbstractComparerEvaluator<TSource> evaluator = null);

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            var index = MakeIndex();

            return LiveListSubscription.Create(
                index.Subscribe(observer),
                index);
        }
    }

    internal class TrivialOrderedLiveList<TSource> : AbstractOrderedLiveList<TSource>
    {
        ILiveList<TSource> source;

        public TrivialOrderedLiveList(ILiveList<TSource> source)
        {
            this.source = source;
        }

        public override ILiveIndex<TSource> MakeIndex(AbstractComparerEvaluator<TSource> evaluator = null)
        {
            return new LiveIndex<TSource>(source, evaluator);
        }
    }

    internal class NestedOrderedLiveList<TSource> : AbstractOrderedLiveList<TSource>
    {
        IOrderedLiveList<TSource> nested;
        Func<AbstractComparerEvaluator<TSource>, AbstractComparerEvaluator<TSource>> nest;

        public NestedOrderedLiveList(IOrderedLiveList<TSource> nested, Func<AbstractComparerEvaluator<TSource>, AbstractComparerEvaluator<TSource>> nest)
        {
            this.nested = nested;
            this.nest = nest;
        }

        public override ILiveIndex<TSource> MakeIndex(AbstractComparerEvaluator<TSource> evaluator = null)
        {
            return nested.MakeIndex(nest(evaluator ?? AbstractComparerEvaluator<TSource>.Trivial));
        }
    }

    internal class LiveIndex<TSource> : ILiveIndex<TSource>
    {
        AbstractComparerEvaluator<TSource> evaluator;

        Action<TSource, Id> handleOnChange;

        Func<TSource, Unit> evaluationSelector;

        ILiveListSubscription sourceSubscription;

        LiveList<ThingWithKey<TSource>> list = new LiveList<ThingWithKey<TSource>>();

        internal LiveIndex(ILiveList<TSource> source, AbstractComparerEvaluator<TSource> evaluator)
        {
            this.evaluator = evaluator;
            handleOnChange = (item, id) => sourceSubscription.Refresh(id);
            evaluationSelector = item => { evaluator.SetLhs(item); return Unit.Default; };
            sourceSubscription = source.Subscribe(Handle);
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            // FIXME: A refresh request will now refresh all subscribers, as they share the live list.
            return list.Select(twk => twk.Thing).Subscribe(observer);
        }

        void Handle(ListEventType type, TSource item, Id id, Id? previousId, Id? nextId)
        {
            var twk = ThingWithKey.Create(item, id);

            // I used to do binary search on both types, but obviously we can't yet work like this: Any id whiches sort item changed
            // is in the wrong place. We should search for it using the old id, but so far we don't keep track of that.

            switch (type)
            {
                case ListEventType.Add:
                    var insertionIndex = list.BinarySearch(twk, evaluator);
                    if (insertionIndex >= 0) throw new Exception("Item was already inserted.");
                    Repository.Instance.EvaluateAndSubscribe(ref twk.Subscriptions, evaluationSelector, handleOnChange, item, id);
                    list.Insert(~insertionIndex, twk);
                    break;
                case ListEventType.Remove:
                    var removalIndex = list.FindIndex(twk2 => evaluator.Compare(twk, twk2) == 0);
                    if (removalIndex < 0) throw new Exception("Item had not been inserted.");
                    Remove(removalIndex);
                    break;
            }
        }

        public void Dispose()
        {
            InternalExtensions.DisposeProperly(ref sourceSubscription);
            for (int i = list.Count - 1; i >= 0; --i)
                Remove(i);
        }

        void Remove(Int32 i)
        {
            var removed = list[i];
            list.RemoveAt(i);
            InternalExtensions.DisposeProperly(ref removed.Subscriptions);
        }
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
            return new TrivialOrderedLiveList<TSource>(source).ThenBy(keySelector, comparer);
        }

        public static IOrderedLiveList<TSource> OrderByDescending<TSource, TKey>(
            this ILiveList<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            return new TrivialOrderedLiveList<TSource>(source).ThenByDescending(keySelector, comparer);
        }

        public static IOrderedLiveList<TSource> ThenBy<TSource, TKey>(
            this IOrderedLiveList<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            return new NestedOrderedLiveList<TSource>(source, evaluator =>
                new NestedComparerEvaluator<TSource, TKey>(keySelector, comparer ?? Comparer<TKey>.Default, 1, evaluator));
        }

        public static IOrderedLiveList<TSource> ThenByDescending<TSource, TKey>(
            this IOrderedLiveList<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
        {
            return new NestedOrderedLiveList<TSource>(source, evaluator =>
                new NestedComparerEvaluator<TSource, TKey>(keySelector, comparer ?? Comparer<TKey>.Default, -1, evaluator));
        }
    }
}
