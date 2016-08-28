using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    public static partial class LiveList
    {
        struct SelectAttachment<TResult>
        {
            public Id Id;
            public TResult Image;
            public SerialDisposable WatchableSubscriptions;
        }

        public static ILiveList<TResult> Select<TSource, TResult>(this ILiveList<TSource> source, Func<TSource, TResult> selector)
        {
            return LiveList.Create<TResult>((onNext, downwardsRefreshRequests) =>
            {
                var attachments = new Dictionary<Id, SelectAttachment<TResult>>();

                // FIXME: We don't actually need a reverse mapping if we pass the keys as-is. But can we pass the keys that way?
                var reverseMapping = new Dictionary<Id, Id>();

                var upwardsRefreshRequests = new Subject<Id>();

                Action<TSource, Id> redo = (item, id) =>
                {
                    upwardsRefreshRequests.OnNext(id);
                };

                var subscription = source.Subscribe((type, item, id, previousId) =>
                {
                    var previousMappedKey = previousId.HasValue ? attachments[previousId.Value].Id : (Id?)null;
                    switch (type)
                    {
                        case ListEventType.Add:
                            var newAttachment = new SelectAttachment<TResult>();
                            
                            newAttachment.Id = id;
                            // FIXME: avoid boxing
                            newAttachment.Image = Repository.Instance.EvaluateAndSubscribe(ref newAttachment.WatchableSubscriptions, selector, redo, item, id);
                            onNext(ListEventType.Add, newAttachment.Image, id, previousMappedKey);
                            attachments[id] = newAttachment;
                            reverseMapping[newAttachment.Id] = id;
                            break;
                        case ListEventType.Remove:
                            var attachment = attachments[id];
                            attachment.WatchableSubscriptions?.Dispose();
                            onNext(ListEventType.Remove, attachment.Image, id, previousMappedKey);
                            attachments.Remove(id);
                            reverseMapping.Remove(attachment.Id);
                            break;
                        default:
                            break;
                    }
                }, upwardsRefreshRequests);

                return new CompositeDisposable(
                    downwardsRefreshRequests?.Subscribe(id => upwardsRefreshRequests.OnNext(reverseMapping[id])) ?? Disposable.Empty,
                    subscription);
            });
        }

        public static ILiveList<TResult> SelectMany<TSource, TResult>(this ILiveList<TSource> source, Func<TSource, ILiveList<TResult>> selector)
        {
            return source.Select(selector).Flatten();
        }

        public static ILiveList<TResult> SelectMany<TSource, TCollection, TResult>(this ILiveList<TSource> source, Func<TSource, ILiveList<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
        {
            return source.SelectMany(s => collectionSelector(s).Select(c => resultSelector(s, c)));
        }


        struct WhereInfo<TSource>
        {
            public Id Id { get; set; }
            public Boolean IsIn { get; set; }
        }

        // Only until the compiler bug is fixed.
        static Id? GetKey<TSource>(this WhereInfo<TSource>? source)
        {
            if (source == null) return null;

            return source.Value.Id;
        }

        public static ILiveList<TSource> Where<TSource>(this ILiveList<TSource> source, Func<TSource, Boolean> predicate)
        {
            return LiveList.Create<TSource>((onNext, downwardsRefreshRequests) =>
            {
                var manifestation = new List<WhereInfo<TSource>>();

                var upwardsRefreshRequests = new Subject<Id>();

                Action<ListEvent<TSource>> redo = v =>
                {
                    upwardsRefreshRequests.OnNext(v.Id);
                };

                var subscription = source.Subscribe((type, item, id, previousId) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            {
                                var indexOfPreviousIn = -1;
                                var indexOfPrevious = -1;
                                if (previousId.HasValue)
                                {
                                    for (++indexOfPrevious; indexOfPrevious < manifestation.Count; ++indexOfPrevious)
                                    {
                                        var current = manifestation[indexOfPrevious];
                                        if (current.IsIn) indexOfPreviousIn = indexOfPrevious;
                                        if (current.Id == previousId) break;
                                    }

                                    if (indexOfPrevious == manifestation.Count) throw new Exception("Previous element not found in manifestation.");
                                }

                                var previous = indexOfPreviousIn < 0 ? (WhereInfo<TSource>?)null : manifestation[indexOfPreviousIn];

                                var isIn = predicate(item);// FIXME Repository.Instance.EvaluateAndSubscribe(v2 => predicate(v2.Item), redo, v);

                                var info = new WhereInfo<TSource>() { Id = id, IsIn = isIn };

                                manifestation.Insert(indexOfPrevious + 1, info);

                                if (isIn)
                                    onNext(ListEventType.Add, item, id, previous.GetKey());
                            }
                            break;
                        case ListEventType.Remove:
                            {
                                var indexOfPreviousIn = -1;
                                var indexOfTarget = 0;
                                for (; indexOfTarget < manifestation.Count; ++indexOfTarget)
                                {
                                    var current = manifestation[indexOfTarget];
                                    if (current.Id == id) break;
                                    if (current.IsIn) indexOfPreviousIn = indexOfTarget;
                                }

                                if (indexOfTarget == manifestation.Count) throw new Exception("Target element not found in manifestation.");

                                var target = manifestation[indexOfTarget];

                                var previousIn = indexOfPreviousIn < 0 ? (WhereInfo<TSource>?)null : manifestation[indexOfPreviousIn];

                                manifestation.RemoveAt(indexOfTarget);

                                if (target.IsIn)
                                    onNext(ListEventType.Remove, item, id, previousIn.GetKey());
                            }
                            break;
                        default:
                            break;
                    }
                }, upwardsRefreshRequests);

                return new CompositeDisposable(
                    downwardsRefreshRequests?.Subscribe(id => upwardsRefreshRequests.OnNext(id)) ?? Disposable.Empty,
                    subscription);
            });
        }

    }
}

