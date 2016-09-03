using System;
using System.Collections.Generic;

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
            public Id? lastItemId;
            public Id? firstItemId;
            public Id? previousId;
            public Id? nextId;
            public Dictionary<Id, Id> incomingToOutgoingIdLookup;
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
                var innerToOuterIdLookup = new Dictionary<Id, Id>();

                Action<Id> handleInboundRefreshRequest = id => {
                    var listKey = innerToOuterIdLookup[id];

                    var attachment = outerAttachments[listKey];

                    // We delegate the refresh request to the correct inner live list.
                    attachment.subscription.Refresh(id);
                };

                var listOfListsSubscription = listOfLists.Subscribe((type, item, id, previousId, nextId) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            var attachment = new FlattenOuterListAttachment<T>();

                            outerAttachments.Add(id, attachment);

                            attachment.previousId = previousId;
                            attachment.nextId = nextId;

                            // We're translating keys as all incoming keys of all lists may not be unique.
                            attachment.incomingToOutgoingIdLookup = new Dictionary<Id, Id>();

                            attachment.subscription = item.Subscribe((type2, item2, id2, previousId2, nextId2) =>
                            {
                                Id nid2;

                                switch (type2)
                                {
                                    case ListEventType.Add:
                                        nid2 = IdHelper.Create();

                                        attachment.incomingToOutgoingIdLookup[id2] = nid2;

                                        innerToOuterIdLookup[id2] = id;

                                        if (previousId2 == attachment.lastItemId)
                                            attachment.lastItemId = id2;
                                        if (nextId2 == attachment.firstItemId)
                                            attachment.firstItemId = id2;

                                        break;
                                    case ListEventType.Remove:
                                        nid2 = attachment.incomingToOutgoingIdLookup[id2];

                                        innerToOuterIdLookup.Remove(id2);

                                        if (id2 == attachment.lastItemId)
                                            attachment.lastItemId = previousId2;
                                        if (id2 == attachment.firstItemId)
                                            attachment.firstItemId = nextId2;

                                        break;
                                    default:
                                        throw new Exception("Unexpected event type.");
                                }

                                var npreviousId2 = attachment.previousId
                                    ?.ApplyTo(outerAttachments)?.lastItemId
                                    ?.ApplyTo(attachment.incomingToOutgoingIdLookup);

                                var nnextId2 = attachment.nextId
                                    ?.ApplyTo(outerAttachments)?.firstItemId
                                    ?.ApplyTo(attachment.incomingToOutgoingIdLookup);

                                onNext(type2, item2, nid2, npreviousId2, nnextId2);
                            });
                            break;

                        case ListEventType.Remove:
                            var oldAttachment = outerAttachments[id];

                            InternalExtensions.DisposeSafely(ref oldAttachment.subscription);

                            outerAttachments.Remove(id);
                            break;
                    }
                });

                return new ActionCompositeLiveListSubscription(handleInboundRefreshRequest, listOfListsSubscription);
            });
        }
    }
}
