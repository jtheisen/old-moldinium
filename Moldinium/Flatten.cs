using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    public static partial class LiveList
    {
        /// <summary>
        /// Concatenates two lists. This has an O(1) complexity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs">The first list.</param>
        /// <param name="rhs">The second list.</param>
        /// <returns>The concatenated list.</returns>
        public static ILiveList<T> Concat<T>(this ILiveList<T> lhs, ILiveList<T> rhs)
        {
            LiveList<ILiveList<T>> lists = new LiveList<ILiveList<T>>();
            lists.Add(lhs);
            lists.Add(rhs);
            return lists.Flatten();
        }

        class FlattenOuterListAttachment<T>
        {
            public IDisposable Subscription { get; set; }
            public Key? LastItemKey { get; set; }
            public Key? PreviousKey { get; set; }
            public Subject<Key> InboundRefreshRequest { get; set; }
            public Dictionary<Key, Key> IncomingToOutgoingKeyLookup { get; set; }
        }

        /// <summary>
        /// Flattens the specified list of lists. This has O(1) complexity.
        /// </summary>
        /// <typeparam name="T">The item type of the list.</typeparam>
        /// <param name="listOfLists">The list of lists.</param>
        /// <returns>The flattened list.</returns>
        public static ILiveList<T> Flatten<T>(this ILiveList<ILiveList<T>> listOfLists)
        {
            return LiveList.Create<T>((onNext, inboundRefreshRequest) =>
            {
                var outerAttachments = new Dictionary<Key, FlattenOuterListAttachment<T>>();

                // inner incoming key -> outer incoming key it belongs to
                var innerToOuterKeyLookup = new Dictionary<Key, Key>();

                var inboundRefreshRequestSubscription = inboundRefreshRequest?.Subscribe(key =>
                {
                    var listKey = innerToOuterKeyLookup[key];

                    var attachment = outerAttachments[listKey];

                    // We delegate the refresh request to the correct inner live list.
                    attachment.InboundRefreshRequest.OnNext(key);
                });

                var listOfListsSubscription = listOfLists.Subscribe((type, item, key, previousKey) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            var attachment = new FlattenOuterListAttachment<T>();

                            outerAttachments.Add(key, attachment);

                            attachment.PreviousKey = previousKey;

                            attachment.InboundRefreshRequest = new Subject<Key>();

                            // We're translating keys as all incoming keys of all lists may not be unique.
                            attachment.IncomingToOutgoingKeyLookup = new Dictionary<Key, Key>();

                            var outboundRefreshRequests = new Subject<Key>();

                            attachment.InboundRefreshRequest.Subscribe(t => outboundRefreshRequests.OnNext(t));

                            attachment.Subscription = item.Subscribe((type2, item2, key2, previousKey2) =>
                            {
                                Key nkey2;

                                switch (type2)
                                {
                                    case ListEventType.Add:
                                        nkey2 = KeyHelper.Create();

                                        attachment.IncomingToOutgoingKeyLookup[key2] = nkey2;

                                        innerToOuterKeyLookup[key2] = key;

                                        if (previousKey2 == attachment.LastItemKey)
                                        {
                                            attachment.LastItemKey = key2;
                                        }
                                        break;
                                    case ListEventType.Remove:
                                        nkey2 = attachment.IncomingToOutgoingKeyLookup[key2];

                                        innerToOuterKeyLookup.Remove(key2);

                                        if (key2 == attachment.LastItemKey)
                                        {
                                            attachment.LastItemKey = previousKey2;
                                        }
                                        break;
                                    default:
                                        throw new Exception("Unexpected event type.");
                                }

                                if (previousKey2 == null)
                                {
                                    if (attachment.PreviousKey.HasValue)
                                    {
                                        var previousAttachment = outerAttachments[attachment.PreviousKey.Value];

                                        var npreviousKey2 = previousAttachment.LastItemKey
                                            .ApplyTo(attachment.IncomingToOutgoingKeyLookup);

                                        onNext(type2, item2, nkey2, npreviousKey2);
                                    }
                                    else
                                    {
                                        onNext(type2, item2, nkey2, null);
                                    }
                                }
                                else
                                {
                                    var npreviousKey2 = previousKey2.ApplyTo(attachment.IncomingToOutgoingKeyLookup);

                                    onNext(type2, item2, nkey2, npreviousKey2);
                                }

                            }, outboundRefreshRequests);
                            break;

                        case ListEventType.Remove:
                            var oldAttachment = outerAttachments[key];

                            oldAttachment.Subscription.Dispose();

                            outerAttachments.Remove(key);
                            break;
                    }
                }, null);

                return new CompositeDisposable(
                    inboundRefreshRequestSubscription ?? Disposable.Empty,
                    listOfListsSubscription
                    );
            });
        }
    }
}
