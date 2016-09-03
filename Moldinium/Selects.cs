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
            public TResult image;
            public SerialDisposable watchableSubscriptions;
        }

        public static ILiveList<TResult> Select<TSource, TResult>(this ILiveList<TSource> source, Func<TSource, TResult> selector)
        {
            return LiveList.Create<TResult>(onNext =>
            {
                var attachments = new Dictionary<Id, SelectAttachment<TResult>>();

                Action<TSource, Id> redo = null;

                var subscription = source.Subscribe((type, item, id, previousId, nextId) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            var newAttachment = new SelectAttachment<TResult>();
                            
                            // FIXME: avoid boxing
                            newAttachment.image = Repository.Instance.EvaluateAndSubscribe(ref newAttachment.watchableSubscriptions, selector, redo, item, id);
                            onNext(ListEventType.Add, newAttachment.image, id, previousId, nextId);
                            attachments[id] = newAttachment;
                            break;
                        case ListEventType.Remove:
                            var attachment = attachments[id];
                            attachment.watchableSubscriptions?.Dispose();
                            onNext(ListEventType.Remove, attachment.image, id, previousId, nextId);
                            attachments.Remove(id);
                            break;
                        default:
                            break;
                    }
                });

                redo = (item, id) => subscription.Refresh(id);

                // FIXME: clear watchable subscriptions!
                return LiveListSubscription.Create(subscription);
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

    }
}

