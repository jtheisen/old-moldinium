using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    public class LiveListSubject<TSource> : ILiveList<TSource>, ILiveListObserver<TSource>
    {
        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            var info = new ObserverInfo() { Observer = observer, RefreshRequested = refreshRequested };

            info.Subscription = Disposable.Create(() => observers.Remove(info));

            info.refreshRequestSubscription = refreshRequested.Subscribe(inboundRefreshRequested);

            observers.Add(info);

            return info.Subscription;
        }

        public void OnNext(ListEventType type, TSource item, Key key, Key? previousKey)
        {
            foreach (var info in observers)
            {
                try
                {
                    info.Observer(type, item, key, previousKey);
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

        public IObservable<Key> RefreshRequested => inboundRefreshRequested;

        public Int32 Count { get; private set; }

        Subject<Key> inboundRefreshRequested = new Subject<Key>();

        class ObserverInfo
        {
            public DLiveListObserver<TSource> Observer;
            public IDisposable Subscription;
            public IObservable<Key> RefreshRequested;
            public IDisposable refreshRequestSubscription;
        }

        List<ObserverInfo> observers = new List<ObserverInfo>();
    }
}
