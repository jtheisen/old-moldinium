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
            public Key Key { get; set; }
            public TResult Image { get; set; }
        }

        public static ILiveList<TResult> Select<TSource, TResult>(this ILiveList<TSource> source, Func<TSource, TResult> selector)
        {
            return LiveList.Create<TResult>((onNext, downwardsRefreshRequests) =>
            {
                var attachments = new Dictionary<Key, SelectAttachment<TResult>>();

                // FIXME: We don't actually need a reverse mapping if we pass the keys as-is. But can we pass the keys that way?
                var reverseMapping = new Dictionary<Key, Key>();

                var upwardsRefreshRequests = new Subject<Key>();

                Action<ListEvent<TSource>> redo = v =>
                {
                    upwardsRefreshRequests.OnNext(v.Key);
                };

                var subscription = source.Subscribe(v =>
                {
                    var previousKey = v.PreviousKey.HasValue ? attachments[v.PreviousKey.Value].Key : (Key?)null;
                    switch (v.Type)
                    {
                        case ListEventType.Add:
                            var newAttachment = new SelectAttachment<TResult>();
                            newAttachment.Key = v.Key;
                            // FIXME: avoid boxing
                            newAttachment.Image = Repository.Instance.EvaluateAndSubscribe(v2 => selector(v2.Item), redo, v);
                            onNext(ListEvent.Make(ListEventType.Add, newAttachment.Image, v.Key, previousKey));
                            attachments[v.Key] = newAttachment;
                            reverseMapping[newAttachment.Key] = v.Key;
                            break;
                        case ListEventType.Remove:
                            var attachment = attachments[v.Key];
                            onNext(ListEvent.Make(ListEventType.Remove, attachment.Image, v.Key, previousKey));
                            attachments.Remove(v.Key);
                            reverseMapping.Remove(attachment.Key);
                            break;
                        default:
                            break;
                    }
                }, upwardsRefreshRequests);

                return new CompositeDisposable(
                    downwardsRefreshRequests?.Subscribe(key => upwardsRefreshRequests.OnNext(reverseMapping[key])) ?? Disposable.Empty,
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
            public Key Key { get; set; }
            public Boolean IsIn { get; set; }
        }

        // Only until the compiler bug is fixed.
        static Key? GetKey<TSource>(this WhereInfo<TSource>? source)
        {
            if (source == null) return null;

            return source.Value.Key;
        }

        public static ILiveList<TSource> Where<TSource>(this ILiveList<TSource> source, Func<TSource, Boolean> predicate)
        {
            return LiveList.Create<TSource>((onNext, downwardsRefreshRequests) =>
            {
                var manifestation = new List<WhereInfo<TSource>>();

                var upwardsRefreshRequests = new Subject<Key>();

                Action<ListEvent<TSource>> redo = v =>
                {
                    upwardsRefreshRequests.OnNext(v.Key);
                };

                var subscription = source.Subscribe(v =>
                {
                    switch (v.Type)
                    {
                        case ListEventType.Add:
                            {
                                var indexOfPreviousIn = -1;
                                var indexOfPrevious = -1;
                                if (v.PreviousKey.HasValue)
                                {
                                    for (++indexOfPrevious; indexOfPrevious < manifestation.Count; ++indexOfPrevious)
                                    {
                                        var current = manifestation[indexOfPrevious];
                                        if (current.IsIn) indexOfPreviousIn = indexOfPrevious;
                                        if (current.Key == v.PreviousKey) break;
                                    }

                                    if (indexOfPrevious == manifestation.Count) throw new Exception("Previous element not found in manifestation.");
                                }

                                var previous = indexOfPreviousIn < 0 ? (WhereInfo<TSource>?)null : manifestation[indexOfPreviousIn];

                                var isIn = predicate(v.Item);// FIXME Repository.Instance.EvaluateAndSubscribe(v2 => predicate(v2.Item), redo, v);

                                var info = new WhereInfo<TSource>() { Key = v.Key, IsIn = isIn };

                                manifestation.Insert(indexOfPrevious + 1, info);

                                if (isIn)
                                    onNext(ListEvent.Make(ListEventType.Add, v.Item, v.Key, previous.GetKey()));
                            }
                            break;
                        case ListEventType.Remove:
                            {
                                var indexOfPreviousIn = -1;
                                var indexOfTarget = 0;
                                for (; indexOfTarget < manifestation.Count; ++indexOfTarget)
                                {
                                    var current = manifestation[indexOfTarget];
                                    if (current.Key == v.Key) break;
                                    if (current.IsIn) indexOfPreviousIn = indexOfTarget;
                                }

                                if (indexOfTarget == manifestation.Count) throw new Exception("Target element not found in manifestation.");

                                var target = manifestation[indexOfTarget];

                                var previousIn = indexOfPreviousIn < 0 ? (WhereInfo<TSource>?)null : manifestation[indexOfPreviousIn];

                                manifestation.RemoveAt(indexOfTarget);

                                if (target.IsIn)
                                    onNext(ListEvent.Make(ListEventType.Remove, v.Item, v.Key, previousIn.GetKey()));
                            }
                            break;
                        default:
                            break;
                    }
                }, upwardsRefreshRequests);

                return new CompositeDisposable(
                    downwardsRefreshRequests?.Subscribe(key => upwardsRefreshRequests.OnNext(key)) ?? Disposable.Empty,
                    subscription);
            });
        }

    }
}

