using System;
using System.Collections.Generic;

namespace IronStone.Moldinium
{
    internal interface IBilateralLiveList<out T>
    {
        ILiveListSubscription Subscribe(DBilateralLiveListObserver<T> observer);
    }

    internal delegate void DBilateralLiveListObserver<in T>(ListEventType type, T item, Id id, Id? previousId, Id? nextId);

    internal class BilateralLiveList<TSource> : IBilateralLiveList<TSource>
    {
        ILiveList<TSource> source;

        public BilateralLiveList(ILiveList<TSource> source)
        {
            this.source = source;
        }

        public ILiveListSubscription Subscribe(DBilateralLiveListObserver<TSource> observer)
        {
            var firstId = new Id?();
            var nextIds = new Dictionary<Id, Id?>();

            return source.Subscribe((type, item, id, previousId) =>
            {
                Id? nextId;

                switch (type)
                {
                    case ListEventType.Add:
                        {
                            if (previousId.HasValue)
                            {
                                if (!nextIds.TryGetValue(previousId.Value, out nextId))
                                    throw new Exception("Could not find previous key.");
                            }
                            else
                                nextId = firstId;

                            nextIds.Add(id, nextId);
                            observer(ListEventType.Add, item, id, previousId, nextId);
                        }
                        break;
                    case ListEventType.Remove:
                        {
                            if (!nextIds.TryGetValue(id, out nextId))
                                throw new Exception("Could not find key.");
                            nextIds.Remove(id);
                            observer(ListEventType.Remove, item, id, previousId, nextId);
                        }
                    break;
                }
            });
        }
    }

    public static partial class LiveList
    {
        internal static IBilateralLiveList<TSource> ToBilateralLiveList<TSource>(this ILiveList<TSource> source)
        {
            return new BilateralLiveList<TSource>(source);
        }

        public static ILiveList<TSource> Reverse<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>(onNext =>
                source.ToBilateralLiveList().Subscribe((type, item, id, previousId, nextId) => onNext(type, item, id, nextId))
            );
        }
    }
}
