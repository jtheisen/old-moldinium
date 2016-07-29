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

                Action<TSource, Key> redo = (item, key) =>
                {
                    upwardsRefreshRequests.OnNext(key);
                };

                var watchableSubscriptions = new SerialDisposable();

                var subscription = source.Subscribe((type, item, key, previousKey) =>
                {
                    var previousMappedKey = previousKey.HasValue ? attachments[previousKey.Value].Key : (Key?)null;
                    switch (type)
                    {
                        case ListEventType.Add:
                            var newAttachment = new SelectAttachment<TResult>();
                            newAttachment.Key = key;
                            // FIXME: avoid boxing
                            newAttachment.Image = Repository.Instance.EvaluateAndSubscribe(watchableSubscriptions, selector, redo, item, key);
                            onNext(ListEventType.Add, newAttachment.Image, key, previousMappedKey);
                            attachments[key] = newAttachment;
                            reverseMapping[newAttachment.Key] = key;
                            break;
                        case ListEventType.Remove:
                            var attachment = attachments[key];
                            onNext(ListEventType.Remove, attachment.Image, key, previousMappedKey);
                            attachments.Remove(key);
                            reverseMapping.Remove(attachment.Key);
                            break;
                        default:
                            break;
                    }
                }, upwardsRefreshRequests);

                return new CompositeDisposable(
                    downwardsRefreshRequests?.Subscribe(key => upwardsRefreshRequests.OnNext(reverseMapping[key])) ?? Disposable.Empty,
                    watchableSubscriptions,
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

                var subscription = source.Subscribe((type, item, key, previousKey) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            {
                                var indexOfPreviousIn = -1;
                                var indexOfPrevious = -1;
                                if (previousKey.HasValue)
                                {
                                    for (++indexOfPrevious; indexOfPrevious < manifestation.Count; ++indexOfPrevious)
                                    {
                                        var current = manifestation[indexOfPrevious];
                                        if (current.IsIn) indexOfPreviousIn = indexOfPrevious;
                                        if (current.Key == previousKey) break;
                                    }

                                    if (indexOfPrevious == manifestation.Count) throw new Exception("Previous element not found in manifestation.");
                                }

                                var previous = indexOfPreviousIn < 0 ? (WhereInfo<TSource>?)null : manifestation[indexOfPreviousIn];

                                var isIn = predicate(item);// FIXME Repository.Instance.EvaluateAndSubscribe(v2 => predicate(v2.Item), redo, v);

                                var info = new WhereInfo<TSource>() { Key = key, IsIn = isIn };

                                manifestation.Insert(indexOfPrevious + 1, info);

                                if (isIn)
                                    onNext(ListEventType.Add, item, key, previous.GetKey());
                            }
                            break;
                        case ListEventType.Remove:
                            {
                                var indexOfPreviousIn = -1;
                                var indexOfTarget = 0;
                                for (; indexOfTarget < manifestation.Count; ++indexOfTarget)
                                {
                                    var current = manifestation[indexOfTarget];
                                    if (current.Key == key) break;
                                    if (current.IsIn) indexOfPreviousIn = indexOfTarget;
                                }

                                if (indexOfTarget == manifestation.Count) throw new Exception("Target element not found in manifestation.");

                                var target = manifestation[indexOfTarget];

                                var previousIn = indexOfPreviousIn < 0 ? (WhereInfo<TSource>?)null : manifestation[indexOfPreviousIn];

                                manifestation.RemoveAt(indexOfTarget);

                                if (target.IsIn)
                                    onNext(ListEventType.Remove, item, key, previousIn.GetKey());
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

