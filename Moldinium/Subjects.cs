using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    // this should be interal
    public abstract class AbstractLiveList<TSource> : ILiveList<TSource>
    {
        List<Subscription> subscriptions = new List<Subscription>();

        class Subscription : ILiveListSubscription
        {
            public AbstractLiveList<TSource> container;
            public DLiveListObserver<TSource> observer;
            public ILiveListSubscription subscription;

            public Subscription(AbstractLiveList<TSource> container, DLiveListObserver<TSource> observer)
            {
                this.container = container;
                this.observer = observer;

                container.subscriptions.Add(this);
            }

            public void Dispose()
            {
                container.subscriptions.Remove(this);
            }

            public void Refresh(Id id)
            {
                container.Refresh(observer, id);
            }
        }

        protected abstract void Refresh(DLiveListObserver<TSource> observer, Id id);
        protected abstract void Bootstrap(DLiveListObserver<TSource> observer);

        public ILiveListSubscription Subscribe(DLiveListObserver<TSource> observer)
        {
            return new Subscription(this, observer);
        }

        protected void OnNext(ListEventType type, TSource item, Id id, Id? previousId)
        {
            foreach (var subscription in subscriptions)
            {
                try
                {
                    subscription.observer(type, item, id, previousId);
                }
                catch (Exception)
                {
                    throw; // FIXME
                }
            }

            switch (type)
            {
                case ListEventType.Add:
                    ++Count;
                    break;
                case ListEventType.Remove:
                    --Count;
                    break;
            }
        }

        public Int32 Count { get; private set; }
    }
}
