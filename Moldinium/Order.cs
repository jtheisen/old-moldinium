using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;

namespace IronStone.Moldinium
{
    public interface ILiveIndex<out TSource> : ILiveList<TSource>, IDisposable
    {
        ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer, Func<Int32> skip, Func<Int32> take);
    }

    public static class ThingWithId
    {
        public static ThingWithId<TSource> Create<TSource>(TSource thing, Id id)
        {
            return new ThingWithId<TSource>(thing, id);
        }
    }

    [DebuggerDisplay("{Id}: {Thing}")]
    public struct ThingWithId<TSource>
    {
        public readonly TSource Thing;
        public readonly Id Id;
        public SerialDisposable Subscriptions;

        public ThingWithId(TSource thing, Id id)
        {
            this.Thing = thing;
            this.Id = id;
            this.Subscriptions = null;
        }
    }

    public abstract class AbstractComparerEvaluator<TSource> : IComparer<ThingWithId<TSource>>
    {
        public abstract void SetLhs(TSource value);
        public abstract void SetRhs(TSource value);

        public abstract Int32 Compare();

        public int Compare(ThingWithId<TSource> lhs, ThingWithId<TSource> rhs)
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

    public interface IIndexedLiveList<TSource> : ILiveList<TSource>
    {
        ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer, Func<Int32> skip, Func<Int32> take);
    }

    public interface IOrderedLiveList<TSource> : IIndexedLiveList<TSource>
    {
        ILiveIndex<TSource> MakeIndex(AbstractComparerEvaluator<TSource> evaluator);
    }

    internal class IndexedLiveList<TSource> : IIndexedLiveList<TSource>
    {
        static Func<Int32> DefaultSkip = () => 0;
        static Func<Int32> DefaultTake = () => Int32.MaxValue;

        IIndexedLiveList<TSource> source;
        Func<Int32> skip;
        Func<Int32> take;

        public IndexedLiveList(IIndexedLiveList<TSource> source, Func<Int32> skip, Func<Int32> take)
        {
            this.source = source;
            this.skip = skip ?? DefaultSkip;
            this.take = take ?? DefaultTake;
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            return this.Subscribe(observer, null, null);
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer, Func<Int32> skip, Func<Int32> take)
        {
            return source.Subscribe(observer, () => (skip ?? DefaultSkip)() + this.skip(), () => Math.Min((take ?? DefaultTake)(), this.take()));
        }
    }

    internal abstract class AbstractOrderedLiveList<TSource> : IOrderedLiveList<TSource>
    {
        public abstract ILiveIndex<TSource> MakeIndex(AbstractComparerEvaluator<TSource> evaluator = null);

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            return this.Subscribe(observer, null, null);
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer, Func<Int32> skip, Func<Int32> take)
        {
            var index = MakeIndex();

            return LiveListSubscription.Create(
                index.Subscribe(observer, skip, take),
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


    // TODO: LiveIndex should manage its subscriptions and each subscription should have
    // two cursors to mark where the subscription's sublist ends

    // There should be two standard cursors: beginning and end, and then cursors that
    // are built relative to existing ones.

    internal class LiveIndex<TSource> : ILiveIndex<TSource>
    {
        AbstractComparerEvaluator<TSource> evaluator;

        Action<TSource, Id> handleOnChange;

        Func<TSource, Unit> evaluationSelector;

        ILiveListSubscription sourceSubscription;

        HashSet<Subscription> subscriptions;

        List<ThingWithId<TSource>> list = new List<ThingWithId<TSource>>();



        class Subscription : ILiveListSubscription
        {
            public LiveIndex<TSource> container;
            public DLiveListObserver<TSource> observer;
            public List<ThingWithId<TSource>> list;

            public Func<Int32> skipSelector;
            public Func<Int32> takeSelector;

            SerialDisposable skipSubscription = null;
            SerialDisposable takeSubscription = null;

            Int32 skip;
            Int32 take;

            public Subscription(LiveIndex<TSource> container, DLiveListObserver<TSource> observer, Func<Int32> skipSelector, Func<Int32> takeSelector)
            {
                this.container = container;
                this.observer = observer;
                this.list = container.list;
                this.skipSelector = skipSelector;
                this.takeSelector = takeSelector;

                container.subscriptions.Add(this);
            }

            void UpdateSkip()
            {
                var newSkip = Repository.Instance.EvaluateAndSubscribe(ref skipSubscription, skipSelector, UpdateSkip);

                var skipDiff = newSkip - skip;

                var absSkipDiff = Math.Abs(skipDiff);

                if (skipDiff == 0)
                {
                    // nothing to do
                }
                else if (absSkipDiff > take)
                {
                    ForEachForwards(ListEventType.Remove, skip, skip + take - 2, true, false);
                    ForOne(ListEventType.Remove, skip + take - 1, true, true);
                    ForOne(ListEventType.Add, newSkip, true, true);
                    ForEachForwards(ListEventType.Remove, newSkip + 1, skip + take - 1, false, true);
                }
                else if (skipDiff > 0)
                {
                    ForEachForwards(ListEventType.Remove, skip, newSkip - 1, true, false);
                    ForEachForwards(ListEventType.Add, skip + take, newSkip + take - 1, false, true);
                }
                else // skipDiff < 0
                {
                    ForEachBackwards(ListEventType.Remove, skip + take - 1, newSkip + take, false, true);
                    ForEachBackwards(ListEventType.Add, skip - 1, newSkip, true, false);
                }

                skip = newSkip;
            }

            void UpdateBar()
            {
                var newTake = Repository.Instance.EvaluateAndSubscribe(ref takeSubscription, takeSelector, UpdateBar);

                if (newTake > take)
                {
                    ForEachForwards(ListEventType.Add, skip + take, skip + newTake - 1, false, true);
                }
                else
                {
                    ForEachBackwards(ListEventType.Remove, skip + take - 1, skip + newTake, false, true);
                }

                take = newTake;
            }

            void ForEachForwards(ListEventType type, Int32 from, Int32 to, Boolean leftEdge, Boolean rightEdge)
            {
                for (int i = from; i <= to; ++i)
                    ForOne(type, i, leftEdge, rightEdge);
            }

            void ForEachBackwards(ListEventType type, Int32 from, Int32 to, Boolean leftEdge, Boolean rightEdge)
            {
                for (int i = from; i >= to; --i)
                    ForOne(type, i, leftEdge, rightEdge);
            }

            void ForOne(ListEventType type, Int32 i, Boolean leftEdge, Boolean rightEdge)
            {
                observer(type, list[i].Thing, list[i].Id, leftEdge ? (Id?)null : list[i - 1].Id, rightEdge ? (Id?)null : list[i + 1].Id);
            }

            public void Dispose()
            {
                container.subscriptions.Remove(this);
            }

            public void Refresh(Id id)
            {
                

                //ForOne(ListEventType.Remove, )
            }
        }

        internal LiveIndex(ILiveList<TSource> source, AbstractComparerEvaluator<TSource> evaluator)
        {
            this.evaluator = evaluator;
            handleOnChange = (item, id) => sourceSubscription.Refresh(id);
            evaluationSelector = item => { evaluator.SetLhs(item); return Unit.Default; };
            sourceSubscription = source.Subscribe(Handle);
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            return new Subscription(this, observer, null, null);
        }

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer, Func<Int32> skip, Func<Int32> take)
        {
            var subscrption = new Subscription(this, observer, skip, take);
            subscriptions.Add(subscrption);
            return subscrption;
        }

        void Handle(ListEventType type, TSource item, Id id, Id? previousId, Id? nextId)
        {
            var twi = ThingWithId.Create(item, id);

            // I used to do binary search on both types, but obviously we can't yet work like this: Any id whiches sort item changed
            // is in the wrong place. We should search for it using the old id, but so far we don't keep track of that.

            switch (type)
            {
                case ListEventType.Add:
                    var insertionIndex = list.BinarySearch(twi, evaluator);
                    if (insertionIndex >= 0) throw new Exception("Item was already inserted.");
                    Repository.Instance.EvaluateAndSubscribe(ref twi.Subscriptions, evaluationSelector, handleOnChange, item, id);
                    list.Insert(~insertionIndex, twi);
                    break;
                case ListEventType.Remove:
                    var removalIndex = list.FindIndex(twk2 => evaluator.Compare(twi, twk2) == 0);
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

        public static IIndexedLiveList<TSource> Take<TSource>(this IIndexedLiveList<TSource> source, Func<Int32> take)
        {
            return new IndexedLiveList<TSource>(source, null, take);
        }

        public static IIndexedLiveList<TSource> Skip<TSource>(this IIndexedLiveList<TSource> source, Func<Int32> skip)
        {
            return new IndexedLiveList<TSource>(source, skip, null);
        }
    }
}
