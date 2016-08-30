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
            public ILiveListSubscription subscription;
            public Id? lastItemKey;
            public Id? previousId;
            public Dictionary<Id, Id> incomingToOutgoingKeyLookup;
        }

        /// <summary>
        /// Flattens the specified list of lists. This has O(1) complexity.
        /// </summary>
        /// <typeparam name="T">The item type of the list.</typeparam>
        /// <param name="listOfLists">The list of lists.</param>
        /// <returns>The flattened list.</returns>
        public static ILiveList<T> Flatten<T>(this ILiveList<ILiveList<T>> listOfLists)
        {
            return LiveList.Create<T>(onNext =>
            {
                var outerAttachments = new Dictionary<Id, FlattenOuterListAttachment<T>>();

                // inner incoming id -> outer incoming id it belongs to
                var innerToOuterKeyLookup = new Dictionary<Id, Id>();

                Action<Id> handleInboundRefreshRequest = id => {
                    var listKey = innerToOuterKeyLookup[id];

                    var attachment = outerAttachments[listKey];

                    // We delegate the refresh request to the correct inner live list.
                    attachment.subscription.Refresh(id);
                };

                var listOfListsSubscription = listOfLists.Subscribe((type, item, id, previousId) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            var attachment = new FlattenOuterListAttachment<T>();

                            outerAttachments.Add(id, attachment);

                            attachment.previousId = previousId;

                            // We're translating keys as all incoming keys of all lists may not be unique.
                            attachment.incomingToOutgoingKeyLookup = new Dictionary<Id, Id>();

                            attachment.subscription = item.Subscribe((type2, item2, key2, previousKey2) =>
                            {
                                Id nkey2;

                                switch (type2)
                                {
                                    case ListEventType.Add:
                                        nkey2 = IdHelper.Create();

                                        attachment.incomingToOutgoingKeyLookup[key2] = nkey2;

                                        innerToOuterKeyLookup[key2] = id;

                                        if (previousKey2 == attachment.lastItemKey)
                                        {
                                            attachment.lastItemKey = key2;
                                        }
                                        break;
                                    case ListEventType.Remove:
                                        nkey2 = attachment.incomingToOutgoingKeyLookup[key2];

                                        innerToOuterKeyLookup.Remove(key2);

                                        if (key2 == attachment.lastItemKey)
                                        {
                                            attachment.lastItemKey = previousKey2;
                                        }
                                        break;
                                    default:
                                        throw new Exception("Unexpected event type.");
                                }

                                if (previousKey2 == null)
                                {
                                    if (attachment.previousId.HasValue)
                                    {
                                        var previousAttachment = outerAttachments[attachment.previousId.Value];

                                        var npreviousKey2 = previousAttachment.lastItemKey
                                            .ApplyTo(attachment.incomingToOutgoingKeyLookup);

                                        onNext(type2, item2, nkey2, npreviousKey2);
                                    }
                                    else
                                    {
                                        onNext(type2, item2, nkey2, null);
                                    }
                                }
                                else
                                {
                                    var npreviousKey2 = previousKey2.ApplyTo(attachment.incomingToOutgoingKeyLookup);

                                    onNext(type2, item2, nkey2, npreviousKey2);
                                }

                            });
                            break;

                        case ListEventType.Remove:
                            var oldAttachment = outerAttachments[id];

                            InternalExtensions.DisposeSafely(ref oldAttachment.subscription);

                            outerAttachments.Remove(id);
                            break;
                    }
                });

                return new ActionLiveListSubscription(handleInboundRefreshRequest, listOfListsSubscription);
            });
        }
    }
}
