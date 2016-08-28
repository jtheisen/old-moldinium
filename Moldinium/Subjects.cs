using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace IronStone.Moldinium
{
    // FIXME: Why isn't this manifested? A late subscription doesn't get the list!
    public class LiveListSubject<TSource> : ILiveList<TSource>, ILiveListObserver<TSource>
    {
        public IDisposable Subscribe(DLiveListObserver<TSource> observer, IObservable<Key> refreshRequested)
        {
            var info = new ObserverInfo() { observer = observer, refreshRequested = refreshRequested };

            info.subscription = Disposable.Create(() => observers.Remove(info));

            info.refreshRequestSubscription = refreshRequested.Subscribe(inboundRefreshRequested);

            observers.Add(info);

            return info.subscription;
        }

        public void OnNext(ListEventType type, TSource item, Key key, Key? previousKey)
        {
            foreach (var info in observers)
            {
                try
                {
                    info.observer(type, item, key, previousKey);
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
            public DLiveListObserver<TSource> observer;
            public IDisposable subscription;
            public IObservable<Key> refreshRequested;
            public IDisposable refreshRequestSubscription;
        }

        List<ObserverInfo> observers = new List<ObserverInfo>();
    }
}
